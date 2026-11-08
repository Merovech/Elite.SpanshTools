using System.Collections.Concurrent;
using System.Text;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;
using Elite.SpanshTools.Parsers.Internal;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Limits on the parallel parser's buffering: the line-length cap, buffer sizes that can't overflow, and
	/// input that ends before its reported length.
	/// </summary>
	[TestClass]
	public class BufferLimitTests
	{
		private static ParallelParseOptions Opts(int maxLine, int chunk = 256, Func<ElementParseError, bool>? onError = null) =>
			new() { MaxDegreeOfParallelism = 4, ChunkSize = chunk, MaxLineLength = maxLine, OnElementError = onError };

		/// <summary>A file of small systems with one long one in the middle; returns the content and that line's offset and length.</summary>
		private static (string Content, long Offset, int Length) WithLongLine(int nameLength)
		{
			var elements = TestHelpers.Systems(100).ToList();
			elements[50] = TestHelpers.SystemJson(51, new string('x', nameLength));
			var content = TestHelpers.SpanshFormat(elements);

			int start = content.IndexOf("\t" + elements[50], StringComparison.Ordinal);
			int end = content.IndexOf('\n', start);
			return (content, start, end - start);
		}

		private static async Task<List<StarSystem?>> ParseAsync(string api, string content, ParallelParseOptions options)
		{
			var results = new ConcurrentQueue<StarSystem?>();
			var parser = new GalaxyParser();
			switch (api)
			{
				case "file":
				{
					using var file = new TempFile(content);
					await parser.ParseFileParallelAsync(file.Path, results.Enqueue, options);
					break;
				}
				case "batches":
				{
					using var file = new TempFile(content);
					await foreach (var batch in parser.ParseFileInBatchesAsync(file.Path, options))
					{
						foreach (var s in batch) results.Enqueue(s);
					}

					break;
				}
				case "stream":
				{
					using var stream = new ForwardOnlyStream(Encoding.UTF8.GetBytes(content));
					await parser.ParseStreamParallelAsync(stream, results.Enqueue, options);
					break;
				}
				case "gzip":
				{
					using var file = new TempFile(TestHelpers.Gzip(content));
					await parser.ParseFileParallelAsync(file.Path, results.Enqueue, options);
					break;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(api));
			}

			return results.ToList();
		}

		// ---- MaxLineLength --------------------------------------------------------------------------------

		[TestMethod]
		public void MaxLineLength_Defaults_To_64_MiB()
		{
			Assert.AreEqual(64 * 1024 * 1024, ParallelParseOptions.DefaultMaxLineLength);
			Assert.AreEqual(ParallelParseOptions.DefaultMaxLineLength, new ParallelParseOptions().MaxLineLength);
		}

		[TestMethod]
		[DataRow(0)]
		[DataRow(-1)]
		public async Task Invalid_MaxLineLength_Throws_ArgumentOutOfRangeException(int maxLine)
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(3)));

			await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => TestHelpers.ParseFileParallelAsync(file.Path, Opts(maxLine)));
		}

		[TestMethod]
		[DataRow("file", 256)]
		[DataRow("file", 64 * 1024)]
		[DataRow("batches", 256)]
		[DataRow("stream", 256)]
		[DataRow("stream", 64 * 1024)]
		[DataRow("gzip", 256)]
		public async Task Line_At_The_Limit_Parses_And_One_Byte_Over_Throws_InvalidDataException(string api, int chunk)
		{
			var (content, offset, length) = WithLongLine(20_000);

			Assert.AreEqual(100, (await ParseAsync(api, content, Opts(length, chunk))).Count);

			var ex = await Assert.ThrowsExceptionAsync<InvalidDataException>(() => ParseAsync(api, content, Opts(length - 1, chunk)));
			StringAssert.Contains(ex.Message, $"byte offset {offset}");
		}

		[TestMethod]
		public async Task Over_Long_Line_Is_Not_Skippable_Through_OnElementError()
		{
			var (content, _, length) = WithLongLine(20_000);
			int calls = 0;

			await Assert.ThrowsExceptionAsync<InvalidDataException>(() => ParseAsync("file", content, Opts(length - 1, onError: _ => { calls++; return true; })));

			Assert.AreEqual(0, calls);
		}

		[TestMethod]
		public void Chunk_Reader_Stops_Reading_A_Line_At_The_Limit()
		{
			// A 4 MB line against a 1 KB limit: the reader must give up after about the limit, not buffer the line.
			var (content, _, _) = WithLongLine(4 * 1024 * 1024);
			using var file = new TempFile(content);
			using var handle = File.OpenHandle(file.Path);
			long length = RandomAccess.GetLength(handle);
			using var reader = new FileChunkReader(handle, length, chunkSize: 256, maxLineLength: 1024);
			var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			Assert.ThrowsException<InvalidDataException>(() =>
			{
				for (long chunk = 0; chunk * 256 < length; chunk++)
				{
					reader.ParseChunk(chunk, [], options);
				}
			});

			Assert.IsTrue(reader.PeakBufferLength <= 16 * 1024, $"peak buffer {reader.PeakBufferLength:N0} bytes");
		}

		[TestMethod]
		public void Chunk_Reader_Reads_No_More_Of_A_Line_Than_The_Limit_Needs()
		{
			// Chunk and limit the same size, with a long line starting late in chunk 0: filling a doubled buffer would
			// read on well past the limit.  It needs at most the limit (plus a byte) of the line.
			const int size = 64 * 1024;
			var elements = TestHelpers.Systems(400).ToList(); // About 60 KB.
			elements.Add(TestHelpers.SystemJson(401, new string('x', 1024 * 1024)));
			var content = TestHelpers.SpanshFormat(elements);
			long lineStart = content.IndexOf("\t" + elements[^1], StringComparison.Ordinal);
			Assert.IsTrue(lineStart is > size / 2 and < size, "test setup");

			using var file = new TempFile(content);
			using var handle = File.OpenHandle(file.Path);
			using var reader = new FileChunkReader(handle, RandomAccess.GetLength(handle), chunkSize: size, maxLineLength: size);

			Assert.ThrowsException<InvalidDataException>(() => reader.ParseChunk(0, [], new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

			Assert.IsTrue(reader.BytesRead <= lineStart + size + 1, $"read {reader.BytesRead:N0} bytes; line starts at {lineStart:N0}");
		}

		[TestMethod]
		public async Task Stream_Reader_Stops_Reading_A_Line_At_The_Limit()
		{
			var (content, _, _) = WithLongLine(4 * 1024 * 1024);
			using var stream = new CountingStream(Encoding.UTF8.GetBytes(content));

			await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
				new GalaxyParser().ParseStreamParallelAsync(stream, _ => { }, Opts(1024, chunk: 256)));

			Assert.IsTrue(stream.BytesRead <= 256 * 1024, $"read {stream.BytesRead:N0} of {content.Length:N0} bytes");
		}

		[TestMethod]
		public async Task Gzip_Inside_Gzip_Is_Not_Decompressed_Again()
		{
			// One level of compression is what Spansh publishes.  Deeper nesting would recurse once per level, so
			// hostile input could nest it thousands deep; the inner layer is treated as (invalid) data instead.
			var once = TestHelpers.Gzip(TestHelpers.SpanshFormat(TestHelpers.Systems(10)));
			var twice = GzipBytes(once);

			using var file = new TempFile(twice);
			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() => TestHelpers.ParseFileParallelAsync(file.Path));

			using var single = new TempFile(once);
			Assert.AreEqual(10, (await TestHelpers.ParseFileParallelAsync(single.Path)).Count);
		}

		private static byte[] GzipBytes(byte[] data)
		{
			using var output = new MemoryStream();
			using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
			{
				gzip.Write(data);
			}

			return output.ToArray();
		}

		/// <summary>A forward-only stream that counts the bytes handed out.</summary>
		private sealed class CountingStream(byte[] content) : Stream
		{
			private readonly MemoryStream inner = new(content);

			public long BytesRead { get; private set; }

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override void Flush() { }
			public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
			public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));
			public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Count(inner.Read(buffer.Span)));
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			private int Count(int read)
			{
				BytesRead += read;
				return read;
			}
		}

		// ---- FileChunkReader ----------------------------------------------------------------------------

		[TestMethod]
		public void Chunk_Reader_Shrinks_Its_Buffer_After_A_Long_Line()
		{
			var (content, _, _) = WithLongLine(200_000);
			using var file = new TempFile(content);
			using var handle = File.OpenHandle(file.Path);
			long length = RandomAccess.GetLength(handle);
			using var reader = new FileChunkReader(handle, length, chunkSize: 256, maxLineLength: int.MaxValue);
			int initial = reader.BufferLength;

			var output = new List<StarSystem?>();
			for (long chunk = 0; chunk * 256 < length; chunk++)
			{
				reader.ParseChunk(chunk, output, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
				Assert.AreEqual(initial, reader.BufferLength, $"after chunk {chunk}");
			}

			Assert.AreEqual(100, output.Count);
		}

		[TestMethod]
		public void Chunk_Reader_Throws_IOException_When_The_File_Is_Shorter_Than_Reported()
		{
			// Larger than one chunk plus its read-ahead (64 + 4,096 bytes), so chunk 0 itself reads in full.
			var content = TestHelpers.SpanshFormat(TestHelpers.Systems(100));
			using var file = new TempFile(content);
			using var handle = File.OpenHandle(file.Path);
			long actual = RandomAccess.GetLength(handle);
			using var reader = new FileChunkReader(handle, actual + 4096, chunkSize: 64, maxLineLength: int.MaxValue);
			var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			// Chunk 0 leaves real lines (and newlines) in the buffer; a chunk wholly past the real end then reads
			// nothing, and must not fall back on those stale bytes.
			reader.ParseChunk(0, [], options);
			Assert.ThrowsException<IOException>(() => reader.ParseChunk((actual / 64) + 2, [], options));
		}

		// ---- Layout detection and buffer sizes ----------------------------------------------------------

		[TestMethod]
		[Timeout(10_000)]
		public void Layout_Detection_Treats_A_Short_Read_As_The_End_Of_The_Input()
		{
			// The input reports 1,000 bytes but has only these, as if it were truncated after its length was taken.
			var data = Encoding.UTF8.GetBytes("[\n{\"a\":");

			var layout = LineDelimited.DetectLayout(1000, (destination, offset) =>
			{
				if (offset >= data.Length) return 0;
				int n = Math.Min(destination.Length, data.Length - (int)offset);
				data.AsSpan((int)offset, n).CopyTo(destination);
				return n;
			});

			Assert.AreEqual(Layout.Other, layout);
		}

		[TestMethod]
		public void Layout_Detection_Reads_Whole_Input()
		{
			var data = Encoding.UTF8.GetBytes(TestHelpers.SpanshFormat(TestHelpers.Systems(5)));

			var layout = LineDelimited.DetectLayout(data.Length, (destination, offset) =>
			{
				int n = Math.Min(destination.Length, data.Length - (int)offset);
				data.AsSpan((int)offset, n).CopyTo(destination);
				return n;
			});

			Assert.AreEqual(Layout.LineDelimited, layout);
		}

		[TestMethod]
		[DataRow(8 * 1024 * 1024, 100, 8 * 1024 * 1024, DisplayName = "Small leftover: chunk size")]
		[DataRow(1024, 1000, 2000, DisplayName = "Large leftover: double it")]
		public void Carry_Over_Buffer_Size(int chunkSize, int leftover, int expected) =>
			Assert.AreEqual(expected, LineDelimited.CarryOverBufferSize(chunkSize, leftover));

		[TestMethod]
		public void Carry_Over_Buffer_Size_Does_Not_Overflow()
		{
			int size = LineDelimited.CarryOverBufferSize(1024, 1_100_000_000);

			Assert.AreEqual(Array.MaxLength, size);
		}
	}
}
