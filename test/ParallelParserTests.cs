using System.Text;
using System.Text.Json;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Tests for the parallel API (IParallelGalaxyParser).  The reference for "correct" is always the sequential
	/// ParseFileAsync / ParseStreamAsync output for the same input; the parallel path must agree with it, except
	/// for ordering where PreserveOrder is off.
	/// </summary>
	[TestClass]
	public class ParallelParserTests
	{
		private static ParallelParseOptions Opts(int dop = 4, int chunk = 256, bool order = false, int buffered = 0) =>
			new() { MaxDegreeOfParallelism = dop, ChunkSize = chunk, PreserveOrder = order, MaxBufferedBatches = buffered };

		private static async Task AssertSameAsSequentialAsync(string content, ParallelParseOptions? options = null)
		{
			using var file = new TempFile(content);
			var expected = TestHelpers.SortedIds(await TestHelpers.ParseFileAsync(file.Path));

			CollectionAssert.AreEqual(expected, TestHelpers.SortedIds(await TestHelpers.ParseFileParallelAsync(file.Path, options)), "callback API");
			CollectionAssert.AreEqual(expected, TestHelpers.SortedIds(await TestHelpers.ParseFileInBatchesAsync(file.Path, options)), "batch API");

			using var stream = new ForwardOnlyStream(Encoding.UTF8.GetBytes(content));
			CollectionAssert.AreEqual(expected, TestHelpers.SortedIds(await TestHelpers.ParseStreamParallelAsync(stream, options)), "stream API");
		}

		// ---- Line-delimited (Spansh) layout ----------------------------------------------------------------

		[TestMethod]
		public async Task Spansh_Layout_Matches_Sequential_Parse()
		{
			await AssertSameAsSequentialAsync(TestHelpers.SpanshFormat(TestHelpers.Systems(500, stationsPerSystem: 1)), Opts());
		}

		[TestMethod]
		[DataRow(1, 1)]
		[DataRow(1, 3)]
		[DataRow(7, 3)]
		[DataRow(64, 16)]
		[DataRow(4096, 2)]
		[DataRow(8 * 1024 * 1024, 4)]
		public async Task Every_System_Is_Parsed_Exactly_Once_Regardless_Of_Chunk_Boundaries(int chunkSize, int dop)
		{
			// Chunk sizes from one byte up put chunk boundaries at every possible position within lines.
			await AssertSameAsSequentialAsync(TestHelpers.SpanshFormat(TestHelpers.Systems(300)), Opts(dop, chunkSize));
		}

		[TestMethod]
		public async Task Windows_Line_Endings_Are_Handled()
		{
			await AssertSameAsSequentialAsync(TestHelpers.SpanshFormat(TestHelpers.Systems(200), "\r\n"), Opts());
		}

		[TestMethod]
		[DataRow("[\n\t{0},\n\t{1}\n]", DisplayName = "Closing bracket on its own line")]
		[DataRow("[\n\t{0},\n\t{1}]", DisplayName = "Closing bracket on the last element's line")]
		[DataRow("[\n\t{0},\n\t{1}\n]\n", DisplayName = "Trailing newline after the closing bracket")]
		[DataRow("[\n\t{0},\n\n   \n\t{1}\n\n]", DisplayName = "Blank and whitespace-only lines")]
		[DataRow("[\n\t{0}\n\t,{1}\n]", DisplayName = "Leading-comma style")]
		[DataRow("[\r\n  {0} ,\r\n  {1}  \r\n]", DisplayName = "Spaces around elements and commas")]
		public async Task Line_Layout_Variants_Match_Sequential_Parse(string template)
		{
			var content = template.Replace("{0}", TestHelpers.SystemJson(1, "A")).Replace("{1}", TestHelpers.SystemJson(2, "B"));
			await AssertSameAsSequentialAsync(content, Opts(dop: 2, chunk: 16));
		}

		[TestMethod]
		public async Task Null_Elements_Are_Delivered_As_Null()
		{
			var content = TestHelpers.SpanshFormat([TestHelpers.SystemJson(1, "A"), "null", TestHelpers.SystemJson(2, "B"), "null"]);
			using var file = new TempFile(content);

			var results = await TestHelpers.ParseFileParallelAsync(file.Path, Opts());

			Assert.AreEqual(4, results.Count);
			Assert.AreEqual(2, results.Count(r => r is null));
		}

		[TestMethod]
		public async Task Utf8_Bom_Is_Ignored()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(50)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

			var results = await TestHelpers.ParseFileParallelAsync(file.Path, Opts(chunk: 8));

			Assert.AreEqual(50, results.Count);
		}

		[TestMethod]
		[DataRow(1_000, DisplayName = "Longer than the chunk")]
		[DataRow(200_000, DisplayName = "Longer than the chunk and the initial read-ahead")]
		public async Task A_Line_Longer_Than_The_Chunk_Is_Parsed_Once(int nameLength)
		{
			var longName = new string('x', nameLength);
			var elements = new[] { TestHelpers.SystemJson(1, "Before"), TestHelpers.SystemJson(2, longName), TestHelpers.SystemJson(3, "After") };
			using var file = new TempFile(TestHelpers.SpanshFormat(elements));

			var results = await TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(dop: 3, chunk: 64, order: true));

			CollectionAssert.AreEqual(new[] { "Before", longName, "After" }, results.Select(r => r!.Name).ToArray());
		}

		[TestMethod]
		public async Task Unicode_Content_Is_Preserved()
		{
			var content = TestHelpers.SpanshFormat([TestHelpers.SystemJson(1, "Café ñ 中文"), TestHelpers.SystemJson(2, "Ωmega")]);
			using var file = new TempFile(content);

			var results = await TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(chunk: 3, order: true));

			CollectionAssert.AreEqual(new[] { "Café ñ 中文", "Ωmega" }, results.Select(r => r!.Name).ToArray());
		}

		[TestMethod]
		public async Task Full_Model_Content_Matches_Sequential_Parse()
		{
			// The fully populated fixture, re-laid-out as one element per line.
			var element = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(FullSystemJson.Value)[0]);
			using var file = new TempFile(TestHelpers.SpanshFormat([element, element]));

			var sequential = await TestHelpers.ParseFileAsync(file.Path);
			var parallel = await TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(chunk: 1024, order: true));

			Assert.AreEqual(2, parallel.Count);
			Assert.AreEqual(JsonSerializer.Serialize(sequential), JsonSerializer.Serialize(parallel));
		}

		// ---- Ordering ---------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Batches_With_PreserveOrder_Arrive_In_File_Order()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(2000)));

			var results = await TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(dop: 8, chunk: 128, order: true, buffered: 3));

			CollectionAssert.AreEqual(Enumerable.Range(1, 2000).Select(i => (long)i).ToArray(), results.Select(r => r!.Id).ToArray());
		}

		[TestMethod]
		public async Task Stream_Batches_With_PreserveOrder_Arrive_In_File_Order()
		{
			using var stream = new ForwardOnlyStream(Encoding.UTF8.GetBytes(TestHelpers.SpanshFormat(TestHelpers.Systems(2000))));

			var results = await TestHelpers.ParseStreamInBatchesAsync(stream, Opts(dop: 8, chunk: 128, order: true, buffered: 3));

			CollectionAssert.AreEqual(Enumerable.Range(1, 2000).Select(i => (long)i).ToArray(), results.Select(r => r!.Id).ToArray());
		}

		[TestMethod]
		public async Task Callback_With_PreserveOrder_Is_Called_In_File_Order_One_At_A_Time()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(1000)));
			var seen = new List<long>();
			int concurrent = 0, maxConcurrent = 0;

			await new GalaxyParser().ParseFileParallelAsync(file.Path, s =>
			{
				int now = Interlocked.Increment(ref concurrent);
				maxConcurrent = Math.Max(maxConcurrent, now);
				seen.Add(s!.Id);
				Interlocked.Decrement(ref concurrent);
			}, Opts(dop: 8, chunk: 100, order: true));

			Assert.AreEqual(1, maxConcurrent);
			CollectionAssert.AreEqual(Enumerable.Range(1, 1000).Select(i => (long)i).ToArray(), seen);
		}

		[TestMethod]
		public async Task Callback_Without_PreserveOrder_Runs_On_Several_Threads()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(4000)));
			var threads = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();

			await new GalaxyParser().ParseFileParallelAsync(file.Path, _ =>
			{
				threads[Environment.CurrentManagedThreadId] = true;
				Thread.SpinWait(2000);
			}, Opts(dop: 4, chunk: 512));

			Assert.IsTrue(threads.Count > 1, "Expected callbacks on more than one thread.");
		}

		[TestMethod]
		public async Task MaxDegreeOfParallelism_Of_One_Works()
		{
			await AssertSameAsSequentialAsync(TestHelpers.SpanshFormat(TestHelpers.Systems(100)), Opts(dop: 1, chunk: 64));
		}

		[TestMethod]
		public async Task Default_Options_Work()
		{
			await AssertSameAsSequentialAsync(TestHelpers.SpanshFormat(TestHelpers.Systems(1000)));
		}

		// ---- Other layouts fall back to the sequential parser ----------------------------------------------

		[TestMethod]
		[DataRow("[]", DisplayName = "Empty array")]
		[DataRow("[\n]", DisplayName = "Empty array over two lines")]
		[DataRow("null", DisplayName = "Top-level null")]
		[DataRow("[{0},{1}]", DisplayName = "Minified on one line")]
		[DataRow("[{0},\n{1}]", DisplayName = "First element on the bracket line")]
		[DataRow("[\n{0},{1}\n]", DisplayName = "Two elements on one line")]
		public async Task Non_Line_Delimited_Layouts_Match_Sequential_Parse(string template)
		{
			var content = template.Replace("{0}", TestHelpers.SystemJson(1, "A")).Replace("{1}", TestHelpers.SystemJson(2, "B"));
			await AssertSameAsSequentialAsync(content, Opts(chunk: 16));
		}

		[TestMethod]
		public async Task Pretty_Printed_Json_Matches_Sequential_Parse()
		{
			var compact = TestHelpers.ArrayOfSystems(25, stationsPerSystem: 2);
			var indented = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(compact), new JsonSerializerOptions { WriteIndented = true });

			await AssertSameAsSequentialAsync(indented, Opts(chunk: 32));
		}

		[TestMethod]
		public async Task Empty_File_Throws_JsonException_Like_The_Sequential_Parser()
		{
			using var file = new TempFile(string.Empty);

			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(file.Path, Opts()));
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileInBatchesAsync(file.Path, Opts()));
		}

		// ---- Compressed input -------------------------------------------------------------------------------

		[TestMethod]
		public async Task Gzip_File_Is_Detected_And_Decompressed()
		{
			// Detection is by content (gzip magic bytes), not by file extension.
			var content = TestHelpers.SpanshFormat(TestHelpers.Systems(400));
			using var file = new TempFile(TestHelpers.Gzip(content));

			var results = await TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(chunk: 100, order: true));

			CollectionAssert.AreEqual(Enumerable.Range(1, 400).Select(i => (long)i).ToArray(), results.Select(r => r!.Id).ToArray());
		}

		[TestMethod]
		public async Task Gzip_Stream_Is_Detected_And_Decompressed()
		{
			using var stream = new ForwardOnlyStream(TestHelpers.Gzip(TestHelpers.SpanshFormat(TestHelpers.Systems(300))));

			var results = await TestHelpers.ParseStreamParallelAsync(stream, Opts());

			CollectionAssert.AreEqual(Enumerable.Range(1, 300).Select(i => (long)i).ToArray(), TestHelpers.SortedIds(results));
		}

		[TestMethod]
		public async Task Gzip_Of_A_Non_Line_Delimited_Layout_Falls_Back_Correctly()
		{
			using var file = new TempFile(TestHelpers.Gzip(TestHelpers.ArrayOfSystems(40)));

			var results = await TestHelpers.ParseFileParallelAsync(file.Path, Opts());

			Assert.AreEqual(40, results.Count);
		}

		// ---- Streams ----------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Stream_Is_Left_Open_For_The_Caller()
		{
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes(TestHelpers.SpanshFormat(TestHelpers.Systems(20))));

			var results = await TestHelpers.ParseStreamParallelAsync(ms, Opts());

			Assert.AreEqual(20, results.Count);
			Assert.IsTrue(ms.CanRead);
		}

		[TestMethod]
		public async Task Stream_Null_Throws_ArgumentNullException()
		{
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseStreamParallelAsync(null!));
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseStreamInBatchesAsync(null!));
		}

		[TestMethod]
		public async Task Stream_Unreadable_Throws_ArgumentException()
		{
			var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(1)));
			try
			{
				using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
				await Assert.ThrowsExceptionAsync<ArgumentException>(() => TestHelpers.ParseStreamParallelAsync(fs));
			}
			finally
			{
				file.Dispose();
			}
		}

		[TestMethod]
		public async Task Stream_Seekable_And_Empty_Throws_ArgumentException_Like_ParseStreamAsync()
		{
			using var ms = new MemoryStream();
			await Assert.ThrowsExceptionAsync<ArgumentException>(() => TestHelpers.ParseStreamParallelAsync(ms));
		}

		[TestMethod]
		public async Task Stream_Forward_Only_And_Empty_Throws_JsonException()
		{
			using var stream = new ForwardOnlyStream([]);
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseStreamParallelAsync(stream));
		}

		// ---- Errors -----------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Malformed_Line_Throws_JsonException_Naming_The_Byte_Offset()
		{
			var good = TestHelpers.SystemJson(1, "Good");
			var bad = "{\"name\":\"Bad\",\"coords\":";
			var content = "[\n\t" + good + ",\n\t" + bad + ",\n\t" + TestHelpers.SystemJson(3, "Also good") + "\n]";
			int offset = content.IndexOf("\t" + bad, StringComparison.Ordinal);
			using var file = new TempFile(content);

			var ex = await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(file.Path, Opts(chunk: 32)));

			StringAssert.Contains(ex.Message, $"byte offset {offset}");
			Assert.IsInstanceOfType(ex.InnerException, typeof(JsonException));
		}

		[TestMethod]
		public async Task Stream_And_Gzip_Inputs_Use_The_Line_Path_And_Report_Decompressed_Offsets()
		{
			// Only the line-delimited path reports byte offsets, so these also prove the fast path was taken for
			// forward-only and compressed input rather than the sequential fallback.
			var bad = "{\"name\":\"Bad\"}";
			var content = TestHelpers.SpanshFormat([.. TestHelpers.Systems(50), bad]);
			int offset = content.IndexOf("\t" + bad, StringComparison.Ordinal);

			using var stream = new ForwardOnlyStream(Encoding.UTF8.GetBytes(content));
			var fromStream = await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseStreamParallelAsync(stream, Opts(chunk: 128)));
			StringAssert.Contains(fromStream.Message, $"byte offset {offset}");

			using var gz = new TempFile(TestHelpers.Gzip(content));
			var fromGzip = await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(gz.Path, Opts(chunk: 128)));
			StringAssert.Contains(fromGzip.Message, $"byte offset {offset}");
		}

		[TestMethod]
		public async Task Non_Line_Delimited_Input_Takes_The_Sequential_Path()
		{
			// The sequential fallback's errors don't carry the line-path's byte-offset wording.
			var content = "[" + TestHelpers.SystemJson(1) + ",{\"name\":\"Bad\"}]";
			using var file = new TempFile(content);

			var ex = await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(file.Path, Opts()));

			Assert.IsFalse(ex.Message.Contains("byte offset", StringComparison.Ordinal), ex.Message);
		}

		[TestMethod]
		public async Task Missing_Required_Member_In_One_Line_Throws_JsonException()
		{
			var elements = TestHelpers.Systems(200).ToList();
			elements[137] = "{\"id64\":999,\"name\":\"No coords\"}";
			using var file = new TempFile(TestHelpers.SpanshFormat(elements));

			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(file.Path, Opts()));
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileInBatchesAsync(file.Path, Opts(order: true)));
		}

		[TestMethod]
		public async Task Exception_From_The_Callback_Propagates_Unchanged()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(500)));

			var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
				new GalaxyParser().ParseFileParallelAsync(file.Path, s =>
				{
					if (s!.Id == 250) throw new InvalidOperationException("consumer failed");
				}, Opts()));

			Assert.AreEqual("consumer failed", ex.Message);
		}

		// ---- Cancellation and resources ---------------------------------------------------------------------

		[TestMethod]
		public async Task Already_Cancelled_Token_Throws_OperationCanceledException()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(10)));
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await AssertCancelledAsync(() => new GalaxyParser().ParseFileParallelAsync(file.Path, _ => { }, Opts(), cts.Token));
			await AssertCancelledAsync(async () =>
			{
				await foreach (var _ in new GalaxyParser().ParseFileInBatchesAsync(file.Path, Opts(), cts.Token)) { }
			});
		}

		[TestMethod]
		public async Task Cancelling_Mid_Parse_Stops_Early_And_Throws_OperationCanceledException()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(5000)));
			using var cts = new CancellationTokenSource();
			int seen = 0;

			await AssertCancelledAsync(() => new GalaxyParser().ParseFileParallelAsync(file.Path, _ =>
			{
				if (Interlocked.Increment(ref seen) == 100) cts.Cancel();
			}, Opts(chunk: 256), cts.Token));

			Assert.IsTrue(seen < 5000, $"Parsing should stop early; saw {seen}.");
		}

		[TestMethod]
		public async Task Breaking_Out_Of_Batches_Early_Releases_The_File()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(5000)));

			await foreach (var _ in new GalaxyParser().ParseFileInBatchesAsync(file.Path, Opts(chunk: 256)))
			{
				break;
			}

			File.Delete(file.Path); // Throws if a worker still holds the file open.
			Assert.IsFalse(File.Exists(file.Path));
		}

		[TestMethod]
		public async Task File_Is_Released_After_Success_And_After_Failure()
		{
			using var good = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(100)));
			using var bad = new TempFile(TestHelpers.SpanshFormat([TestHelpers.SystemJson(1), "{broken"]));

			await TestHelpers.ParseFileParallelAsync(good.Path, Opts());
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseFileParallelAsync(bad.Path, Opts()));

			File.Delete(good.Path);
			File.Delete(bad.Path);
			Assert.IsFalse(File.Exists(good.Path) || File.Exists(bad.Path));
		}

		private static async Task AssertCancelledAsync(Func<Task> action)
		{
			try
			{
				await action();
				Assert.Fail("Expected an OperationCanceledException.");
			}
			catch (OperationCanceledException)
			{
				// Expected (TaskCanceledException is a subclass).
			}
		}

		// ---- Arguments and options --------------------------------------------------------------------------

		[TestMethod]
		[DataRow(null)]
		[DataRow("")]
		[DataRow("   ")]
		public async Task Missing_Filename_Throws_ArgumentNullException(string? filename)
		{
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseFileParallelAsync(filename!));
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseFileInBatchesAsync(filename!));
		}

		[TestMethod]
		public async Task Nonexistent_File_Throws_InvalidOperationException()
		{
			var name = $"missing-{Guid.NewGuid():N}.json";
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TestHelpers.ParseFileParallelAsync(name));
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TestHelpers.ParseFileInBatchesAsync(name));
		}

		[TestMethod]
		public async Task Null_Callback_Throws_ArgumentNullException()
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(1)));
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes("[]"));

			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => new GalaxyParser().ParseFileParallelAsync(file.Path, null!));
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => new GalaxyParser().ParseStreamParallelAsync(ms, null!));
		}

		[TestMethod]
		[DataRow(0, 1024, 0, DisplayName = "MaxDegreeOfParallelism 0")]
		[DataRow(-1, 1024, 0, DisplayName = "MaxDegreeOfParallelism negative")]
		[DataRow(2, 0, 0, DisplayName = "ChunkSize 0")]
		[DataRow(2, -5, 0, DisplayName = "ChunkSize negative")]
		[DataRow(2, int.MaxValue, 0, DisplayName = "ChunkSize above 1 GiB")]
		[DataRow(2, 1024, -1, DisplayName = "MaxBufferedBatches negative")]
		public async Task Invalid_Options_Throw_ArgumentOutOfRangeException(int dop, int chunk, int buffered)
		{
			using var file = new TempFile(TestHelpers.SpanshFormat(TestHelpers.Systems(1)));
			var options = new ParallelParseOptions { MaxDegreeOfParallelism = dop, ChunkSize = chunk, MaxBufferedBatches = buffered };

			await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => TestHelpers.ParseFileParallelAsync(file.Path, options));
			await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => TestHelpers.ParseFileInBatchesAsync(file.Path, options));
		}

		[TestMethod]
		public void Default_Options_Are_Hardware_Neutral()
		{
			var options = new ParallelParseOptions();

			Assert.AreEqual(Environment.ProcessorCount, options.MaxDegreeOfParallelism);
			Assert.AreEqual(8 * 1024 * 1024, options.ChunkSize);
			Assert.IsFalse(options.PreserveOrder);
			Assert.AreEqual(0, options.MaxBufferedBatches, "0 means 'twice MaxDegreeOfParallelism'.");
		}

		[TestMethod]
		public void GalaxyParser_Implements_Both_Interfaces()
		{
			var parser = new GalaxyParser();
			Assert.IsInstanceOfType(parser, typeof(IGalaxyParser));
			Assert.IsInstanceOfType(parser, typeof(IParallelGalaxyParser));
		}
	}
}
