using System.Text;
using System.Text.Json;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers
{
	public class GalaxyParser : IGalaxyParser
	{
		private readonly JsonSerializerOptions serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
			if (inputStream is null)
			{
				throw new ArgumentNullException(nameof(inputStream));
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
