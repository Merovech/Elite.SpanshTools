using System.Text;
using Elite.SpanshTools.Parsers.Internal;

namespace Elite.SpanshTools.Tests
{
	/// <summary>Direct tests of the internal layout detection and buffer-growth rules.</summary>
	[TestClass]
	public class LineDelimitedTests
	{
		private static Layout Detect(string text, bool complete = true) => LineDelimited.Detect(Encoding.UTF8.GetBytes(text), complete);

		[TestMethod]
		[DataRow("[\n{\"a\":1},\n{\"a\":2}\n]", DisplayName = "Spansh layout")]
		[DataRow("[\r\n\t{\"a\":1}\r\n]", DisplayName = "CRLF, single element")]
		[DataRow("﻿[\n{\"a\":1}\n]", DisplayName = "BOM")]
		[DataRow("  [  \n\n  null,\n{\"a\":1}\n]", DisplayName = "Whitespace, blank line, null first")]
		[DataRow("[\n{\"a\":1}]", DisplayName = "Closing bracket on the element line")]
		public void Detects_Line_Delimited(string text) => Assert.AreEqual(Layout.LineDelimited, Detect(text));

		[TestMethod]
		[DataRow("", DisplayName = "Empty")]
		[DataRow("[]", DisplayName = "Empty array")]
		[DataRow("[\n]", DisplayName = "Empty array, two lines")]
		[DataRow("[{\"a\":1}]", DisplayName = "Minified")]
		[DataRow("[\n  {\n    \"a\": 1\n  }\n]", DisplayName = "Pretty-printed")]
		[DataRow("[\n{\"a\":1},{\"a\":2}\n]", DisplayName = "Two elements on a line")]
		[DataRow("[\n1,\n2\n]", DisplayName = "Not objects")]
		[DataRow("{\"a\":1}\n{\"a\":2}", DisplayName = "No opening bracket")]
		[DataRow("[\n{\"a\":", DisplayName = "Truncated element")]
		public void Detects_Other_Layouts(string text) => Assert.AreEqual(Layout.Other, Detect(text));

		[TestMethod]
		[DataRow("[", DisplayName = "No newline yet")]
		[DataRow("[\n{\"a\":1}", DisplayName = "Second line not finished")]
		[DataRow("[\n\n   ", DisplayName = "Only blank lines so far")]
		public void Asks_For_More_Data_When_Undecided(string text) => Assert.AreEqual(Layout.NeedMoreData, Detect(text, complete: false));

		[TestMethod]
		public void Gzip_Is_Recognized_By_Its_Magic_Bytes()
		{
			Assert.IsTrue(LineDelimited.IsGzip(TestHelpers.Gzip("[]")));
			Assert.IsFalse(LineDelimited.IsGzip(Encoding.UTF8.GetBytes("[]")));
			Assert.IsFalse(LineDelimited.IsGzip([0x1F]));
			Assert.IsFalse(LineDelimited.IsGzip([]));
		}

		[TestMethod]
		[DataRow(1024, 0L, 2048, DisplayName = "Doubles by default")]
		[DataRow(1024, 5000L, 5000, DisplayName = "Honors a larger minimum")]
		public void NextBufferSize_Grows(int current, long minimum, int expected) =>
			Assert.AreEqual(expected, LineDelimited.NextBufferSize(current, minimum));

		[TestMethod]
		public void NextBufferSize_Caps_At_The_Largest_Array()
		{
			Assert.AreEqual(Array.MaxLength, LineDelimited.NextBufferSize(Array.MaxLength / 2 + 1, 0));
		}

		[TestMethod]
		public void NextBufferSize_Throws_When_A_Line_Cannot_Fit()
		{
			Assert.ThrowsException<InvalidDataException>(() => LineDelimited.NextBufferSize(Array.MaxLength, 0));
			Assert.ThrowsException<InvalidDataException>(() => LineDelimited.NextBufferSize(1024, (long)Array.MaxLength + 1));
		}
	}
}
