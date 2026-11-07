using System.Xml.Linq;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Guards the sample's GC settings.  MSBuild silently ignores a misspelled property, and without server GC the
	/// parallel demos run about five times slower, so check the exact property names and the generated runtime config.
	/// </summary>
	[TestClass]
	public class SampleProjectTests
	{
		[TestMethod]
		public void Sample_Project_Enables_Server_GC_Without_Concurrent_GC()
		{
			var project = XDocument.Load(FindSampleProject());
			string? Property(string name) => project.Descendants(name).SingleOrDefault()?.Value;

			Assert.AreEqual("true", Property("ServerGarbageCollection"));
			Assert.AreEqual("false", Property("ConcurrentGarbageCollection"));
		}

		[TestMethod]
		public void Sample_Runtime_Config_Has_Server_GC_On()
		{
			// The test project references the sample, so its runtime config is copied next to the test assembly.
			var config = Path.Combine(AppContext.BaseDirectory, "ests.runtimeconfig.json");
			Assert.IsTrue(File.Exists(config), $"Missing {config}");

			var text = File.ReadAllText(config);
			StringAssert.Contains(text, "\"System.GC.Server\": true");
			StringAssert.Contains(text, "\"System.GC.Concurrent\": false");
		}

		private static string FindSampleProject()
		{
			for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
			{
				var candidate = Path.Combine(dir.FullName, "sample", "Elite.SpanshTools.Sample.csproj");
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}

			throw new FileNotFoundException("Couldn't find sample/Elite.SpanshTools.Sample.csproj above the test output folder.");
		}
	}
}
