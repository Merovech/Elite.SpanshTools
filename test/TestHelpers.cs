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

		/// <summary>Element JSON for systems 1..count, as SystemJson builds them.</summary>
		internal static string[] Systems(int count, int stationsPerSystem = 0) =>
			Enumerable.Range(1, count).Select(i => SystemJson(i, $"Sys {i}", stationsPerSystem)).ToArray();

		/// <summary>
		/// The layout of the real Spansh dump: '[' on its own line, then one tab-indented element per line with a
		/// trailing comma (except the last), then ']' on its own line.
		/// </summary>
		internal static string SpanshFormat(IEnumerable<string> elements, string newline = "\n") =>
			"[" + newline + string.Join("," + newline, elements.Select(e => "\t" + e)) + newline + "]";

		internal static async Task<List<StarSystem?>> ParseFileParallelAsync(string filename, ParallelParseOptions? options = null)
		{
			var results = new System.Collections.Concurrent.ConcurrentQueue<StarSystem?>();
			await new GalaxyParser().ParseFileParallelAsync(filename, s => results.Enqueue(s), options);
			return results.ToList();
		}

		internal static async Task<List<StarSystem?>> ParseFileInBatchesAsync(string filename, ParallelParseOptions? options = null)
		{
			var results = new List<StarSystem?>();
			await foreach (var batch in new GalaxyParser().ParseFileInBatchesAsync(filename, options))
			{
				results.AddRange(batch);
			}

			return results;
		}

		internal static async Task<List<StarSystem?>> ParseStreamParallelAsync(Stream stream, ParallelParseOptions? options = null)
		{
			var results = new System.Collections.Concurrent.ConcurrentQueue<StarSystem?>();
			await new GalaxyParser().ParseStreamParallelAsync(stream, s => results.Enqueue(s), options);
			return results.ToList();
		}

		internal static async Task<List<StarSystem?>> ParseStreamInBatchesAsync(Stream stream, ParallelParseOptions? options = null)
		{
			var results = new List<StarSystem?>();
			await foreach (var batch in new GalaxyParser().ParseStreamInBatchesAsync(stream, options))
			{
				results.AddRange(batch);
			}

			return results;
		}

		/// <summary>Ids sorted ascending, with -1 standing in for null elements, for order-insensitive comparison.</summary>
		internal static long[] SortedIds(IEnumerable<StarSystem?> systems) =>
			systems.Select(s => s?.Id ?? -1).OrderBy(id => id).ToArray();

		internal static byte[] Gzip(string text)
		{
			using var ms = new MemoryStream();
			using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
			{
				var bytes = Encoding.UTF8.GetBytes(text);
				gz.Write(bytes, 0, bytes.Length);
			}

			return ms.ToArray();
		}
	}

	/// <summary>A readable stream that can't seek or report its length, like a network or decompression stream.</summary>
	internal sealed class ForwardOnlyStream(byte[] content) : Stream
	{
		private readonly MemoryStream inner = new(content);

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
		public override int Read(Span<byte> buffer) => inner.Read(buffer);
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}

	/// <summary>A uniquely-named temp file that is deleted on disposal.</summary>
	internal sealed class TempFile : IDisposable
	{
		internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"spansh-test-{Guid.NewGuid():N}.json");

		internal TempFile(string content, Encoding? encoding = null)
		{
			File.WriteAllText(Path, content, encoding ?? new UTF8Encoding(false));
		}

		internal TempFile(byte[] content)
		{
			File.WriteAllBytes(Path, content);
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
