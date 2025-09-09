using System.Text.Json;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	[TestClass]
	public class ParseTests
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
			IGalaxyParser parser = new GalaxyParser();
			var action = async () =>
			{
				await foreach (StarSystem? s in parser.ParseStringAsync(json))
				{
					// Do nothing
				}
			};

			if (willThrowException)
			{
				await Assert.ThrowsExceptionAsync<JsonException>(action);
			}
			else
			{
				await action.Invoke();
			}
		}
	}
}
