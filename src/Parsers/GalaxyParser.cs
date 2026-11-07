using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers
{
	public partial class GalaxyParser : IGalaxyParser
	{
		// Parsing of property names needs to be case-insensitive.  This is because the structure of the data
		// isn't consistent -- for example, while nearly all properties are camel-case, the properties of
		// solidComposition are all pascal-case.  So instead of trying to find the places where these are
		// hidden, we'll just default to case-insensitive parsing.  If this becomes a problem I can always
		// go around and track down where I need to pepper JsonProperyNameAttribute.
		private readonly JsonSerializerOptions serializerOptions;

		/// <summary>Creates a parser with the default serializer options.</summary>
		public GalaxyParser()
		{
			serializerOptions = new() { PropertyNameCaseInsensitive = true };
		}

		/// <summary>
		/// Creates a parser that deserializes with a copy of <paramref name="options"/>, for every parse method.
		/// For example, <see cref="JsonUnmappedMemberHandling.Disallow"/> makes properties in the data that the model
		/// doesn't map an error instead of silently ignoring them.
		/// </summary>
		/// <param name="options">The options to copy.  Later changes to this instance don't affect the parser, and
		/// the parser doesn't make it read-only.  Property names are always matched case-insensitively, whatever
		/// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> says, because the data mixes casing.</param>
		/// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
		public GalaxyParser(JsonSerializerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			serializerOptions = new(options) { PropertyNameCaseInsensitive = true };
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<StarSystem?> ParseFileAsync(string filename)
		{
			ValidateFilename(filename);

			using (FileStream fs = File.OpenRead(filename))
			{
				// So far as I can tell, the interaction between await and yield means I can't take this
				// code and refactor it out to its own method.  Which is disappointing.
				var items = JsonSerializer.DeserializeAsyncEnumerable<StarSystem>(fs, serializerOptions);
				await foreach (var item in items)
				{
					yield return item;
				}
			}
		}

		private static void ValidateFilename(string filename)
		{
			if (string.IsNullOrWhiteSpace(filename))
			{
				throw new ArgumentNullException(nameof(filename));
			}

			if (!File.Exists(filename))
			{
				throw new InvalidOperationException($"File '{filename}' not found.");
			}
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<StarSystem?> ParseStringAsync(string inputString)
		{
			if (string.IsNullOrWhiteSpace(inputString))
			{
				throw new ArgumentNullException(nameof(inputString));
			}

			using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(inputString ?? "")))
			{
				// So far as I can tell, the interaction between await and yield means I can't take this
				// code and refactor it out to its own method.  Which is disappointing.
				var items = JsonSerializer.DeserializeAsyncEnumerable<StarSystem>(ms, serializerOptions);
				await foreach (var item in items)
				{
					yield return item;
				}
			}
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<StarSystem?> ParseStreamAsync(Stream inputStream)
		{
			ArgumentNullException.ThrowIfNull(inputStream);

			// Length (used below) is only available on seekable streams, so reject the rest up front rather
			// than letting Length throw a NotSupportedException.
			if (!inputStream.CanSeek)
			{
				throw new InvalidOperationException("Stream must be seekable (CanSeek must be true).");
			}

			if (inputStream.Length == 0)
			{
				throw new ArgumentException("Cannot parse an empty stream.");
			}

			if (!inputStream.CanRead)
			{
				throw new ArgumentException("Stream is unreadable.");
			}

			var items = JsonSerializer.DeserializeAsyncEnumerable<StarSystem>(inputStream, serializerOptions);
			await foreach (var item in items)
			{
				yield return item;
			}
		}
	}
}
