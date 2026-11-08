using System.Buffers;
using System.Text;
using System.Text.Json;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers.Internal
{
	internal enum Layout
	{
		/// <summary>'[' on its own line, then one array element per line.</summary>
		LineDelimited,

		/// <summary>Anything else; parsed sequentially.</summary>
		Other,

		/// <summary>The bytes seen so far don't settle it.</summary>
		NeedMoreData,
	}

	/// <summary>Recognizes and parses input laid out as one JSON array element per line.</summary>
	internal static class LineDelimited
	{
		/// <summary>The most input read while deciding the layout; a longer first element means "Other".</summary>
		internal const int MaxDetectionBytes = 64 * 1024 * 1024;

		private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

		internal static bool IsGzip(ReadOnlySpan<byte> head) => head.Length >= 2 && head[0] == 0x1F && head[1] == 0x8B;

		/// <summary>
		/// Size for a buffer that has to grow to hold a long line: at least <paramref name="minimum"/>, normally
		/// double the current size, never more than an array can hold.
		/// </summary>
		/// <exception cref="InvalidDataException">The line can't fit in any array.</exception>
		internal static int NextBufferSize(int currentSize, long minimum)
		{
			if (currentSize >= Array.MaxLength || minimum > Array.MaxLength)
			{
				throw new InvalidDataException($"A line is longer than the largest supported buffer ({Array.MaxLength:N0} bytes).");
			}

			return (int)Math.Min(Math.Max((long)currentSize * 2, minimum), Array.MaxLength);
		}

		/// <summary>Reads into <paramref name="destination"/> from <paramref name="offset"/>; returns the bytes read, 0 at the end.</summary>
		internal delegate int ReadAt(Span<byte> destination, long offset);

		/// <summary>
		/// Decides the layout of positionally readable input of <paramref name="length"/> bytes, reading as much of
		/// its start as <see cref="Detect"/> needs (at most <see cref="MaxDetectionBytes"/>).  Gzip input is "Other".
		/// </summary>
		internal static Layout DetectLayout(long length, ReadAt read)
		{
			int size = (int)Math.Min(length, 64 * 1024);
			while (true)
			{
				byte[] head = ArrayPool<byte>.Shared.Rent(Math.Max(size, 1));
				try
				{
					int count = 0;
					while (count < size)
					{
						int bytes = read(head.AsSpan(count, size - count), count);
						if (bytes <= 0)
						{
							break;
						}

						count += bytes;
					}

					if (IsGzip(head.AsSpan(0, count)))
					{
						return Layout.Other;
					}

					// A short read means the input ended early (it shrank after its length was taken), so what was read
					// is all there is.  Treating it as incomplete would ask for the same bytes again, forever.
					var layout = Detect(head.AsSpan(0, count), isComplete: count >= length || count < size);
					if (layout != Layout.NeedMoreData)
					{
						return layout;
					}

					if (size >= MaxDetectionBytes)
					{
						return Layout.Other;
					}

					size = (int)Math.Min(Math.Min((long)size * 4, MaxDetectionBytes), length);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(head);
				}
			}
		}

		/// <summary>
		/// Size of the buffer that receives the partial line left over after a segment is cut: the chunk size, or
		/// twice the leftover if that is larger.
		/// </summary>
		internal static int CarryOverBufferSize(int chunkSize, int leftover) =>
			(int)Math.Min(Math.Max(chunkSize, (long)leftover * 2), Array.MaxLength);

		/// <summary>The error for a line longer than <see cref="ParallelParseOptions.MaxLineLength"/>.</summary>
		internal static InvalidDataException LineTooLong(long lineOffset, int maxLineLength) =>
			new($"The line starting at byte offset {lineOffset} is longer than MaxLineLength ({maxLineLength:N0} bytes).");

		/// <summary>
		/// Decides the layout from the start of the input.  It is line-delimited when the first line is exactly '['
		/// and the first non-blank line after it holds exactly one complete element.
		/// </summary>
		/// <param name="data">The first bytes of the input.</param>
		/// <param name="isComplete">True when <paramref name="data"/> is the whole input.</param>
		internal static Layout Detect(ReadOnlySpan<byte> data, bool isComplete)
		{
			int firstNewline = data.IndexOf((byte)'\n');
			if (firstNewline < 0)
			{
				return isComplete ? Layout.Other : Layout.NeedMoreData;
			}

			var firstLine = Trim(data[..firstNewline]);
			if (firstLine.Length != 1 || firstLine[0] != (byte)'[')
			{
				return Layout.Other;
			}

			var rest = data[(firstNewline + 1)..];
			while (true)
			{
				int newline = rest.IndexOf((byte)'\n');
				if (newline < 0 && !isComplete)
				{
					return Layout.NeedMoreData;
				}

				var line = Trim(newline < 0 ? rest : rest[..newline]);
				if (!line.IsEmpty)
				{
					return IsSingleElement(line) ? Layout.LineDelimited : Layout.Other;
				}

				if (newline < 0)
				{
					return Layout.Other;
				}

				rest = rest[(newline + 1)..];
			}
		}

		/// <summary>Parses every line in <paramref name="region"/>, which must hold whole lines only.</summary>
		/// <param name="region">The lines.</param>
		/// <param name="regionOffset">Byte offset of <paramref name="region"/> within the input, for error messages.</param>
		/// <param name="output">Receives the parsed elements.</param>
		/// <param name="options">Serializer options.</param>
		/// <param name="onError">See <see cref="ParseLine"/>.</param>
		/// <param name="maxLineLength">See <see cref="ParseLine"/>.</param>
		internal static void ParseRegion(ReadOnlySpan<byte> region, long regionOffset, List<StarSystem?> output, JsonSerializerOptions options, Func<ElementParseError, bool>? onError = null, int maxLineLength = int.MaxValue)
		{
			int position = 0;
			while (position < region.Length)
			{
				int newline = region[position..].IndexOf((byte)'\n');
				int end = newline < 0 ? region.Length : position + newline;
				ParseLine(region[position..end], regionOffset + position, output, options, onError, maxLineLength);
				position = end + 1;
			}
		}

		/// <summary>
		/// Parses one line: strips the array punctuation around the element ('[', ']', ','), skips blank lines, and
		/// deserializes what is left.
		/// </summary>
		/// <param name="onError">When the element is invalid: null, or returning false, throws; returning true skips
		/// the element.</param>
		/// <param name="maxLineLength">The longest allowed line, in bytes, not counting the '\n'.</param>
		/// <exception cref="JsonException">The element is invalid and wasn't skipped.  The message includes the
		/// line's byte offset.</exception>
		/// <exception cref="InvalidDataException">The line is longer than <paramref name="maxLineLength"/>.</exception>
		internal static void ParseLine(ReadOnlySpan<byte> line, long lineOffset, List<StarSystem?> output, JsonSerializerOptions options, Func<ElementParseError, bool>? onError = null, int maxLineLength = int.MaxValue)
		{
			if (line.Length > maxLineLength)
			{
				throw LineTooLong(lineOffset, maxLineLength);
			}

			var element = StripSeparators(line);
			if (element.IsEmpty)
			{
				return;
			}

			try
			{
				output.Add(JsonSerializer.Deserialize<StarSystem>(element, options));
			}
			catch (JsonException ex)
			{
				var error = new JsonException(
					$"Invalid array element in the line starting at byte offset {lineOffset}: {ex.Message}",
					ex.Path,
					ex.LineNumber,
					ex.BytePositionInLine,
					ex);

				if (onError is not null && onError(new ElementParseError(error, lineOffset, Encoding.UTF8.GetString(element))))
				{
					return;
				}

				throw error;
			}
		}

		private static bool IsSingleElement(ReadOnlySpan<byte> line)
		{
			var element = StripSeparators(line);
			if (element.IsEmpty)
			{
				return false;
			}

			try
			{
				var reader = new Utf8JsonReader(element);
				if (!reader.Read())
				{
					return false;
				}

				if (reader.TokenType == JsonTokenType.StartObject)
				{
					reader.Skip();
				}
				else if (reader.TokenType != JsonTokenType.Null)
				{
					return false;
				}

				// Anything after the element (such as a second element) makes the reader throw.
				return !reader.Read();
			}
			catch (JsonException)
			{
				return false;
			}
		}

		private static ReadOnlySpan<byte> StripSeparators(ReadOnlySpan<byte> line)
		{
			var span = Trim(line);
			span = StripFirst(span, (byte)'[');
			span = StripFirst(span, (byte)',');
			span = StripLast(span, (byte)']');
			span = StripLast(span, (byte)',');
			return span;
		}

		private static ReadOnlySpan<byte> StripFirst(ReadOnlySpan<byte> span, byte value) =>
			!span.IsEmpty && span[0] == value ? Trim(span[1..]) : span;

		private static ReadOnlySpan<byte> StripLast(ReadOnlySpan<byte> span, byte value) =>
			!span.IsEmpty && span[^1] == value ? Trim(span[..^1]) : span;

		private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
		{
			if (span.StartsWith(Utf8Bom))
			{
				span = span[Utf8Bom.Length..];
			}

			int start = 0;
			while (start < span.Length && IsWhitespace(span[start]))
			{
				start++;
			}

			int end = span.Length;
			while (end > start && IsWhitespace(span[end - 1]))
			{
				end--;
			}

			return span[start..end];
		}

		private static bool IsWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
	}
}
