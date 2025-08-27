using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers
{
	public interface IGalaxyParser 
	{
		/// <summary>
		/// Parses a JSON file into a series of StarSystem objects in an enumerated manner.
		/// </summary>
		/// <param name="filename">The name of the file to be parsed.</param>
		/// <returns>A StarSystem object, via enumeration.</returns>
		/// <remarks>It is assumed that the file contains an array of JSON objects.</remarks>
		IAsyncEnumerable<StarSystem?> ParseFileAsync(string filename);

		/// <summary>
		/// Parses a JSON string into a series of StarSystem objects in an enumerated manner.
		/// </summary>
		/// <param name="inputString">The input string, assumed to contain an array.</param>
		/// <returns>A StarSystem object, via enumeration.</returns>
		/// <remarks>It is assumed that the file contains an array of JSON objects.</remarks>
		IAsyncEnumerable<StarSystem?> ParseStringAsync(string inputString);

		/// <summary>
		/// Parses a JSON string into a series of StarSystem objects in an enumerated manner.
		/// </summary>
		/// <param name="inputSteam">The input stream, assumed to contain an array. The caller
		/// is responsible for the disposal of the stream</param>
		/// <returns>A StarSystem object, via enumeration.</returns>
		/// <remarks>It is assumed that the stream contains an array of JSON objects.  In addition, 
		/// the caller is responsible for the disposal of the stream.</remarks>
		IAsyncEnumerable<StarSystem?> ParseStreamAsync(Stream inputSteam);
	}
}
