using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	[TestClass]
	public class FileTests
	{
		private const string Filename = "single.json";

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
			string filename = Filename;
			long count = 0;

			CreateTestFile();

			await foreach (var system in parser.ParseFileAsync(filename))
			{
				count++;
			}

			Assert.AreEqual(count, 1);
		}

		private void CreateTestFile()
		{
			File.Delete(Filename);
			File.WriteAllText(Filename, TestJson.NoBodiesNoStations);
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
