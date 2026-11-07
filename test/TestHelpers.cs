using System.Text;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>Shared helpers for building JSON fixtures and draining parser output.</summary>
	internal static class TestHelpers
	{
		internal const string Stamp = "2025-06-24 21:40:39+00";

		internal static async Task<List<StarSystem?>> ParseStringAsync(string json)
		{
			var results = new List<StarSystem?>();
			await foreach (var system in new GalaxyParser().ParseStringAsync(json))
			{
				results.Add(system);
			}

			return results;
		}

		/// <summary>Parses JSON that is expected to contain exactly one non-null system.</summary>
		internal static async Task<StarSystem> ParseSingleAsync(string json)
		{
			var results = await ParseStringAsync(json);
			Assert.AreEqual(1, results.Count);
			Assert.IsNotNull(results[0]);
			return results[0]!;
		}

		internal static async Task<List<StarSystem?>> ParseStreamAsync(Stream stream)
		{
			var results = new List<StarSystem?>();
			await foreach (var system in new GalaxyParser().ParseStreamAsync(stream))
			{
				results.Add(system);
			}

			return results;
		}

		internal static async Task<List<StarSystem?>> ParseFileAsync(string filename)
		{
			var results = new List<StarSystem?>();
			await foreach (var system in new GalaxyParser().ParseFileAsync(filename))
			{
				results.Add(system);
			}

			return results;
		}

		internal static string StationJson(long id) =>
			$"{{\"name\":\"Station {id}\",\"id\":{id},\"updateTime\":\"{Stamp}\"}}";

		internal static string BodyJson(long id, int stationCount = 0) =>
			$"{{\"id64\":{id},\"bodyId\":{id},\"name\":\"Body {id}\",\"updateTime\":\"{Stamp}\"," +
			$"\"stations\":[{string.Join(",", Enumerable.Range(0, stationCount).Select(i => StationJson((id * 1000) + i)))}]}}";

		/// <summary>The smallest JSON a system can be and still satisfy the 'required' members.</summary>
		internal static string SystemJson(long id = 1, string name = "Sys", int stationCount = 0, params string[] bodies) =>
			$"{{\"id64\":{id},\"name\":\"{name}\",\"coords\":{{\"x\":1.5,\"y\":-2.5,\"z\":3.5}}," +
			$"\"bodies\":[{string.Join(",", bodies)}]," +
			$"\"stations\":[{string.Join(",", Enumerable.Range(0, stationCount).Select(i => StationJson((id * 1000) + i)))}]}}";

		internal static string ArrayOf(params string[] systems) => "[" + string.Join(",", systems) + "]";

		internal static string ArrayOfSystems(int count, int stationsPerSystem = 0)
		{
			var sb = new StringBuilder("[");
			for (int i = 1; i <= count; i++)
			{
				if (i > 1)
				{
					sb.Append(',');
				}

				sb.Append(SystemJson(i, $"Sys {i}", stationsPerSystem));
			}

			return sb.Append(']').ToString();
		}
	}

	/// <summary>A uniquely-named temp file that is deleted on disposal.</summary>
	internal sealed class TempFile : IDisposable
	{
		internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"spansh-test-{Guid.NewGuid():N}.json");

		internal TempFile(string content, Encoding? encoding = null)
		{
			File.WriteAllText(Path, content, encoding ?? new UTF8Encoding(false));
		}

		public void Dispose()
		{
			try
			{
				File.Delete(Path);
			}
			catch (IOException)
			{
				// Best effort; a leaked temp file must not fail the test run.
			}
		}
	}
}
