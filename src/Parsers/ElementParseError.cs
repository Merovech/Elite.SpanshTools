using System.Text.Json;

namespace Elite.SpanshTools.Parsers
{
	/// <summary>
	/// An array element that could not be parsed, as passed to <see cref="ParallelParseOptions.OnElementError"/>.
	/// </summary>
	/// <param name="Exception">The error.  Its message includes <paramref name="ByteOffset"/>, and its
	/// <see cref="Exception.InnerException"/> is the serializer's original exception.</param>
	/// <param name="ByteOffset">Byte offset, within the (decompressed) input, of the start of the line holding the
	/// element.</param>
	/// <param name="Element">The element's JSON text, without the surrounding whitespace and array punctuation.</param>
	public sealed record ElementParseError(JsonException Exception, long ByteOffset, string Element);
}
