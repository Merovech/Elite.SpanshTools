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
		public async Task Should_Throw_On_Empty_Write_Only_Stream()
		{
			// The file is zero-length, so this is rejected by the empty-stream check.  The unreadable-stream
			// guard is covered separately in ParserBehaviorTests, using streams that have content.
			using var file = new TempFile(string.Empty);
			using FileStream fs = new(file.Path, FileMode.Open, FileAccess.Write);

			async Task<long> action() => await TestMethod(fs);
			await Assert.ThrowsExceptionAsync<ArgumentException>(action);
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
