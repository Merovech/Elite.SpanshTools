using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers
{
	public class GalaxyParser : IGalaxyParser
	{
		// Parsing of property names needs to be case-insensitive.  This is because the structure of the data
		// isn't consistent -- for example, while nearly all properties are camel-case, the properties of
		// solidComposition are all pascal-case.  So instead of trying to find the places where these are
		// hidden, we'll just default to case-insensitive parsing.  If this becomes a problem I can always
		// go around and track down where I need to pepper JsonProperyNameAttribute.
#if DEBUG
		// Using JsonUnmappedMemberHandling.Disallow will cause parsing to throw, allowing us to determine 
		// if there are properties in the JSON that don't exist in the model and need to be added.  To ensure
		// this doesn't screw up users' applications, this will only be used in debug builds, while the NuGet
		// package itself is a Release build.
		private readonly JsonSerializerOptions serializerOptions = new() 
		{ 
			PropertyNameCaseInsensitive = true,
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow 
		};
#else
		private readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };
#endif

		/// <inheritdoc />
		public async IAsyncEnumerable<StarSystem?> ParseFileAsync(string filename)
		{
			if (string.IsNullOrWhiteSpace(filename))
			{
				throw new ArgumentNullException(nameof(filename));
			}

			if (!File.Exists(filename))
			{
				throw new InvalidOperationException($"File '{filename}' not found.");
			}

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
