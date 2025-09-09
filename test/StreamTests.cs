using System;
using System.IO;
using System.Text;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Tests
{
	[TestClass]
	public class StreamTests
	{
		[TestMethod]
		public async Task Should_Throw_On_Null_Stream()
		{
			Stream stream = null!;
			async Task<long> action() => await TestMethod(stream);
			await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
		}

		[TestMethod]
		public async Task Should_Throw_On_Empty_Stream()
		{
			using MemoryStream ms = new();
			async Task<long> action() => await TestMethod(ms);
			await Assert.ThrowsExceptionAsync<ArgumentException>(action);
		}

		[TestMethod]
		public async Task Should_Throw_On_Readonly_Stream()
		{
			File.WriteAllText("test-file.json", "");

			using (FileStream fs = new("test-file.json", FileMode.Open, FileAccess.Write))
			{

				async Task<long> action() => await TestMethod(fs);
				await Assert.ThrowsExceptionAsync<ArgumentException>(action);
			}

			File.Delete("test-file.json");
		}

		[TestMethod]
		public async Task Should_Find_Values_In_Stream()
		{
			using MemoryStream ms = new(Encoding.UTF8.GetBytes(TestJson.NoBodiesYesStations));
			var retVal = await TestMethod(ms);
			Assert.AreEqual(1, retVal);
		}

		private async Task<long> TestMethod(Stream stream)
		{
			GalaxyParser parser = new GalaxyParser();
			long ret = 0;
			await foreach (StarSystem? _ in parser.ParseStreamAsync(stream))
			{
				ret++;
			}

			return ret;
		}
	}
}
