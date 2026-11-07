using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	[TestClass]
	public class FileTests
	{
		[TestMethod]
		public async Task Should_Fail_On_Empty_Filename()
		{
			GalaxyParser parser = new();
			var filename = string.Empty;
			async Task<long> action() => await TestMethod(filename);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		[TestMethod]
		public async Task Should_Fail_On_Null_Filename()
		{
			GalaxyParser parser = new();
			string filename = null!;
			async Task<long> action() => await TestMethod(filename);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		[TestMethod]
		public async Task Should_Fail_On_Nonexistent_File()
		{
			GalaxyParser parser = new();
			string filename = "this.is.nonsense";
			async Task<long> action() => await TestMethod(filename);
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(action);
		}

		[TestMethod]
		public async Task Should_Return_Correctly_With_Valid_File()
		{
			GalaxyParser parser = new();
			long count = 0;

			// Unique temp file, deleted on dispose, so nothing is left in the test output folder.
			using var file = new TempFile(TestJson.NoBodiesNoStations);

			await foreach (var system in parser.ParseFileAsync(file.Path))
			{
				count++;
			}

			Assert.AreEqual(1, count);
		}

		private async Task<long> TestMethod(string filename)
		{
			GalaxyParser parser = new GalaxyParser();
			await foreach (StarSystem? _ in parser.ParseFileAsync(filename))
			{
				// Do nothing.  We're just seeing if the strings parse appropriately.
			}

			return 0;
		}
	}
}
