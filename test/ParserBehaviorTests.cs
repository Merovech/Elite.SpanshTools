using System.Text;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Behavior of GalaxyParser that the per-input-type test classes (File/Stream/String) don't cover:
	/// result content and ordering, parity between the three entry points, resource handling, and the
	/// guard clauses that were never reached.
	/// </summary>
	[TestClass]
	public class ParserBehaviorTests
	{
		// ---- Results: content, order, parity ----------------------------------------------------------------

		[TestMethod]
		public async Task String_Parse_Returns_Items_In_Document_Order()
		{
			var json = TestHelpers.ArrayOf(
				TestHelpers.SystemJson(3, "Third"),
				TestHelpers.SystemJson(1, "First"),
				TestHelpers.SystemJson(2, "Second"));

			var results = await TestHelpers.ParseStringAsync(json);

			CollectionAssert.AreEqual(new[] { "Third", "First", "Second" }, results.Select(r => r!.Name).ToArray());
			CollectionAssert.AreEqual(new long[] { 3, 1, 2 }, results.Select(r => r!.Id).ToArray());
		}

		[TestMethod]
		public async Task Empty_Array_Yields_No_Items_From_All_Three_Entry_Points()
		{
			using var file = new TempFile(TestJson.EmptyList);
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes(TestJson.EmptyList));

			Assert.AreEqual(0, (await TestHelpers.ParseStringAsync(TestJson.EmptyList)).Count);
			Assert.AreEqual(0, (await TestHelpers.ParseStreamAsync(ms)).Count);
			Assert.AreEqual(0, (await TestHelpers.ParseFileAsync(file.Path)).Count);
		}

		[TestMethod]
		public async Task Top_Level_Null_Yields_No_Items_From_All_Three_Entry_Points()
		{
			// A JSON document that is just 'null' is treated the same as an empty array, not as an error.
			using var file = new TempFile("null");
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes("null"));

			Assert.AreEqual(0, (await TestHelpers.ParseStringAsync("null")).Count);
			Assert.AreEqual(0, (await TestHelpers.ParseStreamAsync(ms)).Count);
			Assert.AreEqual(0, (await TestHelpers.ParseFileAsync(file.Path)).Count);
		}

		[TestMethod]
		public async Task File_Stream_And_String_Produce_Identical_Results()
		{
			var json = FullSystemJson.Value;
			using var file = new TempFile(json);
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

			var fromString = await TestHelpers.ParseStringAsync(json);
			var fromStream = await TestHelpers.ParseStreamAsync(ms);
			var fromFile = await TestHelpers.ParseFileAsync(file.Path);

			Assert.AreEqual(1, fromString.Count);
			Assert.AreEqual(1, fromStream.Count);
			Assert.AreEqual(1, fromFile.Count);

			var expected = System.Text.Json.JsonSerializer.Serialize(fromString[0]);
			Assert.AreEqual(expected, System.Text.Json.JsonSerializer.Serialize(fromStream[0]));
			Assert.AreEqual(expected, System.Text.Json.JsonSerializer.Serialize(fromFile[0]));
		}

		[TestMethod]
		public async Task Existing_Fixtures_Yield_Expected_System_Counts()
		{
			Assert.AreEqual(3, (await TestHelpers.ParseStringAsync(TestJson.MultipleSystems)).Count);
			Assert.AreEqual(1, (await TestHelpers.ParseStringAsync(TestJson.LargeSystem)).Count);
			Assert.AreEqual(1, (await TestHelpers.ParseStringAsync(TestJson.NewLineAtEnd)).Count);
		}

		[TestMethod]
		public async Task Large_System_Fixture_Has_Expected_Shape()
		{
			var s = (await TestHelpers.ParseStringAsync(TestJson.LargeSystem)).Single()!;

			Assert.AreEqual("Achenar", s.Name);
			Assert.AreEqual(164098653L, s.Id);
			Assert.AreEqual(15, s.Bodies.Count);
			Assert.AreEqual(10, s.Stations.Count);
			Assert.IsTrue(s.Bodies.Any(b => b.Stations.Count > 0), "Achenar has a body with stations on it.");
			Assert.IsTrue(s.Stations.Any(st => st.Market is { Commodities.Count: > 0 }));
		}

		[TestMethod]
		public async Task Many_Systems_Are_All_Returned()
		{
			var results = await TestHelpers.ParseStringAsync(TestHelpers.ArrayOfSystems(2500));

			Assert.AreEqual(2500, results.Count);
			Assert.AreEqual("Sys 1", results[0]!.Name);
			Assert.AreEqual("Sys 2500", results[^1]!.Name);
		}

		[TestMethod]
		public async Task Whitespace_Between_Tokens_And_Trailing_Newlines_Are_Tolerated()
		{
			var json = "\n  [ \r\n  " + TestHelpers.SystemJson(1, "A") + " ,\n\t" + TestHelpers.SystemJson(2, "B") + "\r\n]\r\n\r\n";
			var results = await TestHelpers.ParseStringAsync(json);

			Assert.AreEqual(2, results.Count);
		}

		[TestMethod]
		public async Task One_Parser_Instance_Can_Be_Reused_For_Multiple_Parses()
		{
			var parser = new GalaxyParser();

			for (int pass = 0; pass < 3; pass++)
			{
				int count = 0;
				await foreach (var _ in parser.ParseStringAsync(TestHelpers.ArrayOfSystems(4)))
				{
					count++;
				}

				Assert.AreEqual(4, count, $"Pass {pass}");
			}
		}

		[TestMethod]
		public async Task Concurrent_Parses_On_A_Shared_Parser_Do_Not_Interfere()
		{
			var parser = new GalaxyParser();

			async Task<int> Count(int n)
			{
				int c = 0;
				await foreach (var _ in parser.ParseStringAsync(TestHelpers.ArrayOfSystems(n)))
				{
					c++;
				}

				return c;
			}

			var counts = await Task.WhenAll(Count(10), Count(200), Count(3), Count(75));
			CollectionAssert.AreEqual(new[] { 10, 200, 3, 75 }, counts);
		}

		[TestMethod]
		public void Parser_Is_Usable_Through_The_Interface()
		{
			IGalaxyParser parser = new GalaxyParser();
			Assert.IsNotNull(parser);
		}

		// ---- Streaming: the parser must not buffer the whole document ---------------------------------------

		[TestMethod]
		public async Task Items_Are_Yielded_Incrementally_Before_A_Later_Malformed_Item_Throws()
		{
			// The point of an async enumerable over multi-GB dumps is that early items arrive before the rest
			// of the input is read.  A tiny payload can't show that (System.Text.Json validates what it has
			// buffered first), so put enough valid systems ahead of the bad one to exceed its read buffer.
			var good = Enumerable.Range(1, 5000).Select(i => TestHelpers.SystemJson(i, $"Sys {i}"));
			var json = "[" + string.Join(",", good) + ",{\"name\":\"Bad\"}]"; // last lacks required coords
			var seen = 0;

			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(async () =>
			{
				await foreach (var _ in new GalaxyParser().ParseStringAsync(json))
				{
					seen++;
				}
			});

			Assert.IsTrue(seen > 0, "No item was yielded before the exception; the parser is buffering the whole input.");
		}

		// ---- File -------------------------------------------------------------------------------------------

		[TestMethod]
		public async Task File_Whitespace_Filename_Throws_ArgumentNullException()
		{
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseFileAsync("   "));
		}

		[TestMethod]
		public async Task File_Missing_File_Message_Names_The_File()
		{
			var name = $"definitely-missing-{Guid.NewGuid():N}.json";

			var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TestHelpers.ParseFileAsync(name));

			StringAssert.Contains(ex.Message, name);
		}

		[TestMethod]
		public async Task File_Path_That_Is_A_Directory_Throws_InvalidOperationException()
		{
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TestHelpers.ParseFileAsync(Path.GetTempPath()));
		}

		[TestMethod]
		public async Task File_Is_Released_After_Full_Enumeration()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(3));

			await TestHelpers.ParseFileAsync(file.Path);

			File.Delete(file.Path); // Throws IOException if the parser kept the handle open.
			Assert.IsFalse(File.Exists(file.Path));
		}

		[TestMethod]
		public async Task File_Is_Released_When_The_Caller_Stops_Enumerating_Early()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(50));

			await foreach (var _ in new GalaxyParser().ParseFileAsync(file.Path))
			{
				break;
			}

			File.Delete(file.Path);
			Assert.IsFalse(File.Exists(file.Path));
		}

		[TestMethod]
		public async Task File_Is_Released_When_Parsing_Throws()
		{
			using var file = new TempFile("{invalid-json");

			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() => TestHelpers.ParseFileAsync(file.Path));

			File.Delete(file.Path);
			Assert.IsFalse(File.Exists(file.Path));
		}

		[TestMethod]
		public async Task File_Can_Be_Read_While_Another_Reader_Has_It_Open()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(2));
			using var other = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

			var results = await TestHelpers.ParseFileAsync(file.Path);

			Assert.AreEqual(2, results.Count);
		}

		[TestMethod]
		public async Task File_With_Utf8_Bom_Parses()
		{
			// Windows tools (and File.WriteAllText with Encoding.UTF8) routinely prepend a BOM.
			using var file = new TempFile(TestHelpers.ArrayOfSystems(2), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

			var results = await TestHelpers.ParseFileAsync(file.Path);

			Assert.AreEqual(2, results.Count);
		}

		[TestMethod]
		public async Task File_Empty_Content_Throws_JsonException()
		{
			using var file = new TempFile(string.Empty);

			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() => TestHelpers.ParseFileAsync(file.Path));
		}

		// ---- Stream -----------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Stream_Is_Left_Open_For_The_Caller_To_Dispose()
		{
			using var ms = new MemoryStream(Encoding.UTF8.GetBytes(TestHelpers.ArrayOfSystems(2)));

			var results = await TestHelpers.ParseStreamAsync(ms);

			Assert.AreEqual(2, results.Count);
			Assert.IsTrue(ms.CanRead, "ParseStreamAsync documents that the caller owns disposal.");
			Assert.IsTrue(ms.CanSeek);
		}

		[TestMethod]
		public async Task Stream_Unreadable_Stream_With_Content_Throws_Unreadable_ArgumentException()
		{
			// The existing 'readonly stream' test actually opens a write-only, zero-length file, so it is
			// stopped by the empty-stream check and never reaches the CanRead guard.
			using var stream = new UnreadableStream(length: 10);

			var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => TestHelpers.ParseStreamAsync(stream));

			StringAssert.Contains(ex.Message, "unreadable", StringComparison.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task Stream_NonSeekable_Stream_Throws_InvalidOperationException_Without_Being_Read()
		{
			// Network and compression streams can't report Length.  The parser must reject them explicitly
			// rather than fail incidentally with a NotSupportedException from the Length check.
			using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(TestHelpers.ArrayOfSystems(2)));

			var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TestHelpers.ParseStreamAsync(stream));

			StringAssert.Contains(ex.Message, "seek", StringComparison.OrdinalIgnoreCase);
			Assert.AreEqual(0, stream.ReadCalls, "A non-seekable stream must be rejected before any read is attempted.");
		}

		[TestMethod]
		public async Task Stream_Write_Only_FileStream_With_Content_Throws_ArgumentException()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(1));
			using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

			await Assert.ThrowsExceptionAsync<ArgumentException>(() => TestHelpers.ParseStreamAsync(fs));
		}

		[TestMethod]
		public async Task Stream_Empty_Stream_Message_Says_Empty()
		{
			using var ms = new MemoryStream();

			var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => TestHelpers.ParseStreamAsync(ms));

			StringAssert.Contains(ex.Message, "empty", StringComparison.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task Stream_Null_Stream_Reports_The_Parameter_Name()
		{
			var ex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseStreamAsync(null!));

			Assert.AreEqual("inputStream", ex.ParamName);
		}

		[TestMethod]
		public async Task Stream_Read_From_A_File_Stream_Parses()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(5));
			using var fs = File.OpenRead(file.Path);

			var results = await TestHelpers.ParseStreamAsync(fs);

			Assert.AreEqual(5, results.Count);
		}

		[TestMethod]
		public async Task Stream_Positioned_Past_Start_Parses_From_Current_Position()
		{
			// Callers who have already consumed a prefix (e.g. a header) can hand over the same stream.  The
			// prefix is invalid JSON, so the parse only succeeds if it starts at the current position.
			var prefix = "garbage";
			var bytes = Encoding.UTF8.GetBytes(prefix + TestHelpers.ArrayOfSystems(2));
			using var ms = new MemoryStream(bytes) { Position = prefix.Length };

			var results = await TestHelpers.ParseStreamAsync(ms);

			Assert.AreEqual(2, results.Count);
		}

		// ---- String -----------------------------------------------------------------------------------------

		[TestMethod]
		public async Task String_Null_And_Blank_Report_The_Parameter_Name()
		{
			foreach (var input in new string?[] { null, string.Empty, " ", "\t\r\n" })
			{
				var ex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => TestHelpers.ParseStringAsync(input!));
				Assert.AreEqual("inputString", ex.ParamName, $"Input: '{input}'");
			}
		}

		[TestMethod]
		public async Task String_Top_Level_Object_Instead_Of_Array_Throws_JsonException()
		{
			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(
				() => TestHelpers.ParseStringAsync(TestHelpers.SystemJson()));
		}

		/// <summary>A readable stream that can't seek or report its length, like a network or GZip stream.</summary>
		private sealed class NonSeekableStream(byte[] content) : Stream
		{
			private readonly MemoryStream inner = new(content);

			internal int ReadCalls { get; private set; }

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override void Flush() { }
			public override int Read(byte[] buffer, int offset, int count)
			{
				ReadCalls++;
				return inner.Read(buffer, offset, count);
			}

			public override int Read(Span<byte> buffer)
			{
				ReadCalls++;
				return inner.Read(buffer);
			}

			public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			{
				ReadCalls++;
				return inner.ReadAsync(buffer, cancellationToken);
			}

			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

		/// <summary>A stream that reports content but refuses to be read.</summary>
		private sealed class UnreadableStream(long length) : Stream
		{
			public override bool CanRead => false;
			public override bool CanSeek => true;
			public override bool CanWrite => false;
			public override long Length => length;
			public override long Position { get; set; }
			public override void Flush() { }
			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}
	}
}
