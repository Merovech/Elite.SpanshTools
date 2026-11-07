using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Utilities
{
	/// <summary>
	/// Collects element errors from the parser's worker threads, grouped by kind.  A new Spansh property usually
	/// shows up in thousands or millions of records, so only a few examples of each kind are kept (the ones
	/// earliest in the file) to keep memory bounded.
	/// </summary>
	internal sealed partial class ErrorReport
	{
		private const int SamplesPerKind = 5;

		private readonly ConcurrentDictionary<string, Kind> kinds = new();
		private long count;

		/// <summary>Total errors seen.</summary>
		public long Count => Interlocked.Read(ref count);

		/// <summary>The kinds seen, most frequent first.</summary>
		public IReadOnlyList<Kind> Kinds => [.. kinds.Values.OrderByDescending(k => k.Count).ThenBy(k => k.Key, StringComparer.Ordinal)];

		/// <summary>Records an error and tells the parser to keep going.  Thread-safe.</summary>
		public bool Add(ElementParseError error)
		{
			Interlocked.Increment(ref count);
			var key = KeyOf(error);
			kinds.GetOrAdd(key, k => new Kind(k)).Add(error);
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
					await writer.WriteLineAsync($"---- Byte offset {sample.ByteOffset:N0}: {sample.Exception.Message}");
					await writer.WriteLineAsync(sample.Element);
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
			return $"{message} At {path}";
		}

		[GeneratedRegex(@"\[\d+\]")]
		private static partial Regex ArrayIndex();

		internal sealed class Kind(string key)
		{
			private readonly List<ElementParseError> samples = [];
			private long count;

			public string Key { get; } = key;

			public long Count => Interlocked.Read(ref count);

			public IReadOnlyList<ElementParseError> Samples
			{
				get
				{
					lock (samples)
					{
						return [.. samples];
					}
				}
			}

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
						samples.Insert(index, error);
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
