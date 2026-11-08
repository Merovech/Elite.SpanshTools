using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Utilities
{
	/// <summary>
	/// Collects element errors from the parser's worker threads, grouped by kind.  A new Spansh property usually
	/// shows up in thousands or millions of records, so only a few examples of each kind are kept (the ones
	/// earliest in the file), each truncated, and only so many kinds are tracked; errors of further kinds are only
	/// counted.  That keeps memory bounded however many errors there are.
	/// </summary>
	internal sealed partial class ErrorReport
	{
		private const int SamplesPerKind = 5;
		private const int MaxKinds = 1000;
		private const int MaxSampleChars = 64 * 1024;

		// Serializer messages and paths quote property names, which hostile data can make megabytes long.
		private const int MaxKeyChars = 1024;
		private const int MaxMessageChars = 4096;

		/// <summary>The kind that errors are counted under once <see cref="MaxKinds"/> kinds are being tracked.</summary>
		internal const string OtherKindsKey = "(other kinds: more than 1000 kinds found; examples not kept)";

		private readonly ConcurrentDictionary<string, Kind> kinds = new();
		private readonly Kind otherKinds = new(OtherKindsKey);
		private long count;
		private int kindCount; // ConcurrentDictionary.Count takes every lock, so keep our own.

		/// <summary>Total errors seen.</summary>
		public long Count => Interlocked.Read(ref count);

		/// <summary>The kinds seen, most frequent first.</summary>
		public IReadOnlyList<Kind> Kinds =>
			[.. kinds.Values.Concat(otherKinds.Count > 0 ? [otherKinds] : []).OrderByDescending(k => k.Count).ThenBy(k => k.Key, StringComparer.Ordinal)];

		/// <summary>Records an error and tells the parser to keep going.  Thread-safe.</summary>
		public bool Add(ElementParseError error)
		{
			Interlocked.Increment(ref count);
			var key = KeyOf(error);
			if (kinds.TryGetValue(key, out var kind))
			{
				kind.Add(error);
			}
			else if (Volatile.Read(ref kindCount) < MaxKinds)
			{
				// Racing threads can overshoot MaxKinds by at most the number of workers; that's fine.
				kinds.GetOrAdd(key, k =>
				{
					Interlocked.Increment(ref kindCount);
					return new Kind(k);
				}).Add(error);
			}
			else
			{
				otherKinds.Count1();
			}

			return true;
		}

		public async Task WriteAsync(string path)
		{
			await using var writer = new StreamWriter(path);
			foreach (var kind in Kinds)
			{
				await writer.WriteLineAsync($"==== {kind.Count:N0} occurrence(s): {kind.Key}");
				foreach (var sample in kind.Samples)
				{
					await writer.WriteLineAsync($"---- Byte offset {sample.ByteOffset:N0}: {sample.Message}");
					await writer.WriteLineAsync(sample.Element);
					if (sample.TruncatedChars > 0)
					{
						await writer.WriteLineAsync($"(... {sample.TruncatedChars:N0} more characters not kept)");
					}
				}

				await writer.WriteLineAsync();
			}
		}

		/// <summary>
		/// The serializer's message without its per-record location, plus the JSON path with array indexes removed,
		/// so the same problem in different records (or different bodies of one record) groups together.
		/// </summary>
		internal static string KeyOf(ElementParseError error)
		{
			var inner = error.Exception.InnerException ?? error.Exception;
			var message = inner.Message;
			int location = message.IndexOf(" Path: ", StringComparison.Ordinal);
			if (location >= 0)
			{
				message = message[..location];
			}

			var path = error.Exception.Path is null ? "(no path)" : ArrayIndex().Replace(error.Exception.Path, "[]");
			return Truncate($"{message} At {path}", MaxKeyChars);
		}

		private static string Truncate(string text, int maxChars) => text.Length <= maxChars ? text : text[..maxChars] + " (...)";

		[GeneratedRegex(@"\[\d+\]")]
		private static partial Regex ArrayIndex();

		/// <summary>A kept example: the error message and element text, each cut short (see the constants above).</summary>
		internal sealed record Sample(string Message, long ByteOffset, string Element, int TruncatedChars);

		internal sealed class Kind(string key)
		{
			private readonly List<Sample> samples = [];
			private long count;

			public string Key { get; } = key;

			public long Count => Interlocked.Read(ref count);

			public IReadOnlyList<Sample> Samples
			{
				get
				{
					lock (samples)
					{
						return [.. samples];
					}
				}
			}

			/// <summary>Counts an error without keeping an example.</summary>
			public void Count1() => Interlocked.Increment(ref count);

			public void Add(ElementParseError error)
			{
				Interlocked.Increment(ref count);
				lock (samples)
				{
					// Keep the earliest few by offset, so the report doesn't depend on thread timing.
					int index = samples.FindIndex(s => s.ByteOffset > error.ByteOffset);
					if (index < 0)
					{
						index = samples.Count;
					}

					if (index < SamplesPerKind)
					{
						// Copy just the kept part, so the full element text can be collected.
						var element = error.Element.Length > MaxSampleChars ? error.Element[..MaxSampleChars] : error.Element;
						samples.Insert(index, new Sample(Truncate(error.Exception.Message, MaxMessageChars), error.ByteOffset, element, error.Element.Length - element.Length));
						if (samples.Count > SamplesPerKind)
						{
							samples.RemoveAt(samples.Count - 1);
						}
					}
				}
			}
		}
	}
}
