using System.Text.Json;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	[TestClass]
	public class StringTests
	{
		[TestMethod]
		[DataRow(TestJson.EmptyList, false)]
		[DataRow(TestJson.InvalidJson, true)]
		[DataRow(TestJson.InvalidNotArray, true)]
		[DataRow(TestJson.LargeSystem, false)]
		[DataRow(TestJson.MultipleSystems, false)]
		[DataRow(TestJson.NewLineAtEnd, false)]
		[DataRow(TestJson.NoBodiesNoStations, false)]
		[DataRow(TestJson.NoBodiesNoStations, false)]
		[DataRow(TestJson.YesBodiesNoStations, false)]
		public async Task Json_Should_Parse_As_Expected(string json, bool willThrowException)
		{
			async Task<long> action() => await TestMethod(json);

			if (willThrowException)
			{
				await Assert.ThrowsExceptionAsync<JsonException>(action);
			}
			else
			{
				await action();
			}
		}

		[TestMethod]
		public async Task Should_Throw_On_Null_String()
		{
			string json = null!;
			async Task<long> action() => await TestMethod(json);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		[TestMethod]
		public async Task Should_Throw_On_Empty_String()
		{
			string json = string.Empty;
			async Task<long> action() => await TestMethod(json);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		[TestMethod]
		public async Task Should_Throw_On_Whitespace_String()
		{
			string json = "     ";
			async Task<long> action() => await TestMethod(json);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		private async Task<long> TestMethod(string json)
		{
			GalaxyParser parser = new GalaxyParser();
			await foreach (StarSystem? _ in parser.ParseStringAsync(json))
			{
				// Do nothing.  We're just seeing if the strings parse appropriately.
			}

			return 0;
		}
	}
}
