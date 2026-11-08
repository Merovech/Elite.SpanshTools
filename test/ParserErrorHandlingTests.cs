using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Tests for caller-supplied serializer options (GalaxyParser(JsonSerializerOptions)) and for
	/// ParallelParseOptions.OnElementError, which lets the parallel API report a bad element and keep going.
	/// </summary>
	[TestClass]
	public class ParserErrorHandlingTests
	{
		private static readonly JsonSerializerOptions Strict = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

		private const string UnmappedJson = "{\"id64\":42,\"name\":\"New prop\",\"coords\":{\"x\":1,\"y\":2,\"z\":3},\"brandNewProperty\":true}";

		private static ParallelParseOptions Opts(int chunk = 256, Func<ElementParseError, bool>? onError = null, bool order = false) =>
			new() { MaxDegreeOfParallelism = 4, ChunkSize = chunk, PreserveOrder = order, OnElementError = onError };

		/// <summary>Runs one of the parallel entry points over <paramref name="content"/> and returns what it delivered.</summary>
		private static async Task<List<StarSystem?>> ParseAsync(string api, GalaxyParser parser, byte[] content, ParallelParseOptions options)
		{
			var results = new ConcurrentQueue<StarSystem?>();
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
					using var stream = new ForwardOnlyStream(content);
					await parser.ParseStreamParallelAsync(stream, results.Enqueue, options);
					break;
				}
				case "gzip":
				{
					using var file = new TempFile(TestHelpers.Gzip(Encoding.UTF8.GetString(content)));
					await parser.ParseFileParallelAsync(file.Path, results.Enqueue, options);
					break;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(api));
			}

			return results.ToList();
		}

		private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

		// ---- GalaxyParser(JsonSerializerOptions) ------------------------------------------------------------

		[TestMethod]
		public void Constructor_With_Null_Options_Throws_ArgumentNullException()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new GalaxyParser(null!));
		}

		[TestMethod]
		public async Task Default_Parser_Ignores_Unmapped_Properties()
		{
			var content = TestHelpers.SpanshFormat([TestHelpers.SystemJson(1), UnmappedJson]);
			using var file = new TempFile(content);

			Assert.AreEqual(2, (await TestHelpers.ParseFileAsync(file.Path)).Count);
			Assert.AreEqual(2, (await TestHelpers.ParseFileParallelAsync(file.Path, Opts())).Count);
		}

		[TestMethod]
		public async Task Strict_Options_Reject_Unmapped_Properties_In_Every_Parse_Method()
		{
			var parser = new GalaxyParser(Strict);
			var content = TestHelpers.SpanshFormat([TestHelpers.SystemJson(1), UnmappedJson]);
			using var file = new TempFile(content);

			await Assert.ThrowsExceptionAsync<JsonException>(async () => { await foreach (var _ in parser.ParseStringAsync(content)) { } }, "ParseStringAsync");
			await Assert.ThrowsExceptionAsync<JsonException>(async () => { await foreach (var _ in parser.ParseFileAsync(file.Path)) { } }, "ParseFileAsync");
			await Assert.ThrowsExceptionAsync<JsonException>(async () =>
			{
				using var ms = new MemoryStream(Utf8(content));
				await foreach (var _ in parser.ParseStreamAsync(ms)) { }
			}, "ParseStreamAsync");

			foreach (var api in new[] { "file", "batches", "stream", "gzip" })
			{
				await Assert.ThrowsExceptionAsync<JsonException>(() => ParseAsync(api, parser, Utf8(content), Opts()), api);
			}
		}

		[TestMethod]
		public async Task Supplied_Options_Still_Match_Property_Names_Case_Insensitively()
		{
			// The Spansh data mixes casing (solidComposition's members are PascalCase), so the parser always
			// matches case-insensitively, even if the caller's options say otherwise.
			var options = new JsonSerializerOptions(Strict) { PropertyNameCaseInsensitive = false };
			var json = "{\"ID64\":7,\"Name\":\"Upper\",\"Coords\":{\"X\":1,\"Y\":2,\"Z\":3}}";

			var system = (await ParseAsync("file", new GalaxyParser(options), Utf8(TestHelpers.SpanshFormat([json])), Opts())).Single();

			Assert.AreEqual(7, system!.Id);
			Assert.AreEqual("Upper", system.Name);
		}

		[TestMethod]
		public async Task Supplied_Options_Are_Copied_So_The_Callers_Instance_Stays_Mutable_And_Unshared()
		{
			var options = new JsonSerializerOptions();
			var parser = new GalaxyParser(options);
			using var file = new TempFile(TestHelpers.SpanshFormat([UnmappedJson]));
			await foreach (var _ in parser.ParseFileAsync(file.Path)) { }

			// Using the parser didn't freeze the caller's instance...
			Assert.IsFalse(options.IsReadOnly);

			// ...and later changes to it don't reach the parser.
			options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
			Assert.AreEqual(1, (await ParseAsync("file", parser, Utf8(TestHelpers.SpanshFormat([UnmappedJson])), Opts())).Count);
		}

		// ---- OnElementError ---------------------------------------------------------------------------------

		[TestMethod]
		[DataRow("file", 1)]
		[DataRow("file", 32)]
		[DataRow("file", 8 * 1024 * 1024)]
		[DataRow("batches", 64)]
		[DataRow("stream", 64)]
		[DataRow("gzip", 64)]
		public async Task Returning_True_Skips_Bad_Elements_And_Delivers_All_Others(string api, int chunk)
		{
			var elements = TestHelpers.Systems(300).ToList();
			var bad = new Dictionary<int, string>
			{
				[0] = UnmappedJson,                                   // first element
				[149] = "{\"id64\":150,\"name\":\"Truncated\",\"coords\":", // malformed
				[299] = "{\"id64\":300,\"name\":\"No coords\"}",           // missing required member; last element
			};
			foreach (var (index, json) in bad) elements[index] = json;
			var content = TestHelpers.SpanshFormat(elements);
			var errors = new ConcurrentQueue<ElementParseError>();

			var systems = await ParseAsync(api, new GalaxyParser(Strict), Utf8(content), Opts(chunk, e => { errors.Enqueue(e); return true; }));

			var expectedIds = Enumerable.Range(1, 300).Where(i => !bad.ContainsKey(i - 1)).Select(i => (long)i).ToArray();
			CollectionAssert.AreEqual(expectedIds, TestHelpers.SortedIds(systems));

			var sorted = errors.OrderBy(e => e.ByteOffset).ToList();
			Assert.AreEqual(bad.Count, sorted.Count);
			int i = 0;
			foreach (var json in bad.OrderBy(b => b.Key).Select(b => b.Value))
			{
				long offset = content.IndexOf("\t" + json, StringComparison.Ordinal);
				Assert.AreEqual(offset, sorted[i].ByteOffset, json);
				Assert.AreEqual(json, sorted[i].Element);
				StringAssert.Contains(sorted[i].Exception.Message, $"byte offset {offset}");
				i++;
			}
		}

		[TestMethod]
		public async Task Error_Offsets_And_Text_Are_Correct_With_Crlf_And_A_Bom()
		{
			var elements = TestHelpers.Systems(100).ToList();
			elements[40] = UnmappedJson;
			var content = TestHelpers.SpanshFormat(elements, "\r\n");
			byte[] bytes = [0xEF, 0xBB, 0xBF, .. Utf8(content)];
			long expectedOffset = 3 + Encoding.UTF8.GetByteCount(content[..content.IndexOf("\t" + UnmappedJson, StringComparison.Ordinal)]);

			foreach (var api in new[] { "file", "stream" })
			{
				var errors = new ConcurrentQueue<ElementParseError>();
				var systems = await ParseAsync(api, new GalaxyParser(Strict), bytes, Opts(32, e => { errors.Enqueue(e); return true; }));

				Assert.AreEqual(99, systems.Count, api);
				var error = errors.Single();
				Assert.AreEqual(expectedOffset, error.ByteOffset, api);
				Assert.AreEqual(UnmappedJson, error.Element, api);
			}
		}

		[TestMethod]
		public async Task Returning_False_Faults_With_The_Reported_Error()
		{
			var elements = TestHelpers.Systems(200).ToList();
			elements[120] = UnmappedJson;
			ElementParseError? reported = null;

			var thrown = await Assert.ThrowsExceptionAsync<JsonException>(() =>
				ParseAsync("file", new GalaxyParser(Strict), Utf8(TestHelpers.SpanshFormat(elements)), Opts(64, e => { reported = e; return false; })));

			Assert.IsNotNull(reported);
			Assert.AreSame(reported.Exception, thrown);
		}

		[TestMethod]
		public async Task Exception_From_OnElementError_Faults_The_Parse()
		{
			var elements = TestHelpers.Systems(200).ToList();
			elements[120] = UnmappedJson;

			var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
				ParseAsync("file", new GalaxyParser(Strict), Utf8(TestHelpers.SpanshFormat(elements)), Opts(64, _ => throw new InvalidOperationException("handler failed"))));

			Assert.AreEqual("handler failed", ex.Message);
		}

		[TestMethod]
		public async Task OnElementError_Is_Not_Called_When_Every_Element_Is_Valid()
		{
			int calls = 0;

			var systems = await ParseAsync("file", new GalaxyParser(Strict), Utf8(TestHelpers.SpanshFormat(TestHelpers.Systems(200))), Opts(64, _ => { Interlocked.Increment(ref calls); return true; }));

			Assert.AreEqual(200, systems.Count);
			Assert.AreEqual(0, calls);
		}

		[TestMethod]
		public async Task Skipped_Elements_Keep_The_Rest_In_Order_When_PreserveOrder_Is_Set()
		{
			var elements = TestHelpers.Systems(300).ToList();
			elements[10] = UnmappedJson;
			elements[200] = UnmappedJson.Replace("42", "43");

			var systems = await ParseAsync("batches", new GalaxyParser(Strict), Utf8(TestHelpers.SpanshFormat(elements)), Opts(32, _ => true, order: true));

			var expected = Enumerable.Range(1, 300).Where(i => i != 11 && i != 201).Select(i => (long)i).ToArray();
			CollectionAssert.AreEqual(expected, systems.Select(s => s!.Id).ToArray());
		}

		[TestMethod]
		public async Task Non_Line_Delimited_Input_Faults_Without_Calling_OnElementError()
		{
			// The sequential fallback can't resume after a bad element, so it can't honor "skip"; it faults as before.
			var content = "[" + TestHelpers.SystemJson(1) + "," + UnmappedJson + "]";
			int calls = 0;

			await Assert.ThrowsExceptionAsync<JsonException>(() =>
				ParseAsync("file", new GalaxyParser(Strict), Utf8(content), Opts(onError: _ => { calls++; return true; })));

			Assert.AreEqual(0, calls);
		}
	}
}
