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
		internal static void ParseRegion(ReadOnlySpan<byte> region, long regionOffset, List<StarSystem?> output, JsonSerializerOptions options)
		{
			int position = 0;
			while (position < region.Length)
			{
				int newline = region[position..].IndexOf((byte)'\n');
				int end = newline < 0 ? region.Length : position + newline;
				ParseLine(region[position..end], regionOffset + position, output, options);
				position = end + 1;
			}
		}

		/// <summary>
		/// Parses one line: strips the array punctuation around the element ('[', ']', ','), skips blank lines, and
		/// deserializes what is left.
		/// </summary>
		/// <exception cref="JsonException">The element is invalid.  The message includes the line's byte offset.</exception>
		internal static void ParseLine(ReadOnlySpan<byte> line, long lineOffset, List<StarSystem?> output, JsonSerializerOptions options)
		{
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
				throw new JsonException(
					$"Invalid array element in the line starting at byte offset {lineOffset}: {ex.Message}",
					ex.Path,
					ex.LineNumber,
					ex.BytePositionInLine,
					ex);
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
