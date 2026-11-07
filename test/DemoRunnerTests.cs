using Elite.SpanshTools.Sample;
using Elite.SpanshTools.Sample.Helpers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Tests for the sample project's DemoRunner.  The runner is driven through its internal constructor
	/// with a FakeConsole, because Console.ReadKey / SetCursorPosition throw under a test host.
	/// </summary>
	[TestClass]
	public class DemoRunnerTests
	{
		private static (DemoRunner Runner, FakeConsole Console) Create(string filename)
		{
			var console = new FakeConsole();
			return (new DemoRunner(filename, console), console);
		}

		// ---- Option dispatch --------------------------------------------------------------------------------

		[TestMethod]
		public async Task None_Returns_Immediately_Without_Output_Or_Waiting_For_A_Key()
		{
			var (runner, console) = Create("does-not-matter.json");

			await runner.RunDemo(DemoMethods.None);

			Assert.AreEqual(0, console.Lines.Count);
			Assert.AreEqual(0, console.ReadKeyCalls);
			Assert.AreEqual(0, console.CursorResets);
		}

		[TestMethod]
		[DataRow(3)]
		[DataRow(99)]
		[DataRow(255)]
		[DataRow(-1)]
		public async Task Undefined_Option_Reports_Unknown_And_Does_Not_Wait_For_A_Key(int value)
		{
			var (runner, console) = Create("does-not-matter.json");

			await runner.RunDemo((DemoMethods)value);

			CollectionAssert.AreEqual(new[] { "Unknown option selected." }, console.Lines);
			Assert.AreEqual(0, console.ReadKeyCalls);
		}

		[TestMethod]
		public async Task Every_Defined_Selectable_Option_Is_Handled_And_Is_Not_Reported_Unknown()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(1));

			foreach (var option in Enum.GetValues<DemoMethods>().Where(o => o != DemoMethods.None))
			{
				var (runner, console) = Create(file.Path);

				await runner.RunDemo(option);

				CollectionAssert.DoesNotContain(console.Lines, "Unknown option selected.", $"{option} fell through to default.");
				Assert.AreEqual(1, console.ReadKeyCalls, $"{option} should wait for a key press when finished.");
			}
		}

		// ---- Count systems ----------------------------------------------------------------------------------

		[TestMethod]
		public async Task SystemsByFile_Counts_Systems_And_Reports_Progress_Lines_In_Order()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(7));
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.SystemsByFile);

			Assert.AreEqual(4, console.Lines.Count, string.Join(" | ", console.Lines));
			StringAssert.StartsWith(console.Lines[0], "Counting systems found in");
			StringAssert.Contains(console.Lines[0], file.Path);
			Assert.AreEqual("Systems found: 7", console.Lines[1]);
			StringAssert.StartsWith(console.Lines[2], "Completed in ");
			Assert.AreEqual("Press any key to continue.", console.Lines[3]);
			Assert.AreEqual(1, console.ReadKeyCalls);
			Assert.AreEqual(1, console.CursorResets);
		}

		[TestMethod]
		public async Task SystemsByFile_Reports_Zero_For_An_Empty_Array()
		{
			using var file = new TempFile("[]");
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.SystemsByFile);

			CollectionAssert.Contains(console.Lines, "Systems found: 0");
			Assert.AreEqual(0, console.LogUpdates.Count);
		}

		[TestMethod]
		public async Task SystemsByFile_Logs_Progress_Every_10000_Systems_But_Not_Before()
		{
			using var below = new TempFile(TestHelpers.ArrayOfSystems(9999));
			var (runnerBelow, consoleBelow) = Create(below.Path);
			await runnerBelow.RunDemo(DemoMethods.SystemsByFile);
			Assert.AreEqual(0, consoleBelow.LogUpdates.Count, "No progress update is due before the 10000th system.");
			CollectionAssert.Contains(consoleBelow.Lines, "Systems found: 9999");

			using var above = new TempFile(TestHelpers.ArrayOfSystems(20001));
			var (runnerAbove, consoleAbove) = Create(above.Path);
			await runnerAbove.RunDemo(DemoMethods.SystemsByFile);
			CollectionAssert.AreEqual(new long[] { 10000, 20000 }, consoleAbove.LogUpdates);
			CollectionAssert.Contains(consoleAbove.Lines, "Systems found: 20001");
		}

		[TestMethod]
		public async Task SystemsByFile_Missing_File_Propagates_And_Never_Waits_For_A_Key()
		{
			var (runner, console) = Create($"missing-{Guid.NewGuid():N}.json");

			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => runner.RunDemo(DemoMethods.SystemsByFile));

			Assert.AreEqual(0, console.ReadKeyCalls);
			Assert.IsFalse(console.Lines.Any(l => l.StartsWith("Systems found", StringComparison.Ordinal)));
		}

		[TestMethod]
		public async Task SystemsByFile_Null_Array_Entry_Throws_NullReferenceException_And_Never_Waits_For_A_Key()
		{
			var json = $"[{TestHelpers.SystemJson(1, "A")},null,{TestHelpers.SystemJson(2, "B")}]";
			using var file = new TempFile(json);
			var (runner, console) = Create(file.Path);

			await Assert.ThrowsExceptionAsync<NullReferenceException>(() => runner.RunDemo(DemoMethods.SystemsByFile));

			Assert.AreEqual(0, console.ReadKeyCalls);
			Assert.IsFalse(console.Lines.Any(l => l.StartsWith("Systems found", StringComparison.Ordinal)));
		}

		[TestMethod]
		[DataRow("null", DisplayName = "Top-level null")]
		[DataRow("  null\n", DisplayName = "Top-level null with whitespace")]
		public async Task SystemsByFile_Top_Level_Null_Is_Treated_As_An_Empty_Array(string content)
		{
			using var file = new TempFile(content);
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.SystemsByFile);

			CollectionAssert.Contains(console.Lines, "Systems found: 0");
			Assert.AreEqual(1, console.ReadKeyCalls);
		}

		[TestMethod]
		public async Task SystemsByFile_Invalid_Json_Propagates_JsonException()
		{
			using var file = new TempFile("{invalid-json");
			var (runner, console) = Create(file.Path);

			await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() => runner.RunDemo(DemoMethods.SystemsByFile));

			Assert.AreEqual(0, console.ReadKeyCalls);
		}

		// ---- Count stations ---------------------------------------------------------------------------------

		[TestMethod]
		public async Task StationsByFile_Counts_System_Level_And_Body_Level_Stations()
		{
			var json = TestHelpers.ArrayOf(
				// 2 system stations + 3 on one body + 1 on another = 6
				TestHelpers.SystemJson(1, "A", 2, TestHelpers.BodyJson(10, 3), TestHelpers.BodyJson(11, 1)),
				// 1 system station, a body with none = 1
				TestHelpers.SystemJson(2, "B", 1, TestHelpers.BodyJson(20, 0)),
				// nothing at all = 0
				TestHelpers.SystemJson(3, "C"));
			using var file = new TempFile(json);
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.StationsByFile);

			Assert.AreEqual(4, console.Lines.Count, string.Join(" | ", console.Lines));
			StringAssert.StartsWith(console.Lines[0], "Counting stations found in");
			StringAssert.Contains(console.Lines[0], file.Path);
			Assert.AreEqual("Stations found: 7", console.Lines[1]);
			StringAssert.StartsWith(console.Lines[2], "Completed in ");
			Assert.AreEqual(1, console.ReadKeyCalls);
			Assert.AreEqual(1, console.CursorResets);
		}

		[TestMethod]
		public async Task StationsByFile_Skips_Null_Array_Entries_Without_Throwing()
		{
			var json = $"[{TestHelpers.SystemJson(1, "A", 2)},null,{TestHelpers.SystemJson(2, "B", 1)}]";
			using var file = new TempFile(json);
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.StationsByFile);

			CollectionAssert.Contains(console.Lines, "Stations found: 3");
		}

		[TestMethod]
		public async Task StationsByFile_Reports_Zero_When_There_Are_No_Stations()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(3));
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.StationsByFile);

			CollectionAssert.Contains(console.Lines, "Stations found: 0");
		}

		[TestMethod]
		public async Task StationsByFile_Progress_Reports_The_Running_Station_Count_Not_The_System_Count()
		{
			// 10000 systems x 2 stations: the update fires at system 10000 and must report 20000 stations.
			using var file = new TempFile(TestHelpers.ArrayOfSystems(10000, stationsPerSystem: 2));
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.StationsByFile);

			CollectionAssert.AreEqual(new long[] { 20000 }, console.LogUpdates);
			CollectionAssert.Contains(console.Lines, "Stations found: 20000");
		}

		[TestMethod]
		public async Task StationsByFile_Missing_File_Propagates_And_Never_Waits_For_A_Key()
		{
			var (runner, console) = Create($"missing-{Guid.NewGuid():N}.json");

			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => runner.RunDemo(DemoMethods.StationsByFile));

			Assert.AreEqual(0, console.ReadKeyCalls);
		}

		// ---- Reuse / construction ---------------------------------------------------------------------------

		[TestMethod]
		public async Task One_Runner_Can_Run_Several_Demos_In_Sequence()
		{
			using var file = new TempFile(TestHelpers.ArrayOfSystems(3, stationsPerSystem: 1));
			var (runner, console) = Create(file.Path);

			await runner.RunDemo(DemoMethods.SystemsByFile);
			await runner.RunDemo(DemoMethods.StationsByFile);
			await runner.RunDemo(DemoMethods.None);

			CollectionAssert.Contains(console.Lines, "Systems found: 3");
			CollectionAssert.Contains(console.Lines, "Stations found: 3");
			Assert.AreEqual(2, console.ReadKeyCalls);
		}

		[TestMethod]
		public async Task Public_Constructor_Produces_A_Working_Runner_For_The_No_Console_Paths()
		{
			// The public constructor is what Program.cs uses; None never touches the console, so it is safe
			// to run here even though the real console isn't usable under a test host.
			var runner = new DemoRunner("anything.json");

			await runner.RunDemo(DemoMethods.None);
		}

		[TestMethod]
		public void DemoMethods_Values_Match_The_Menu_Numbering()
		{
			Assert.AreEqual(0, (int)DemoMethods.None);
			Assert.AreEqual(1, (int)DemoMethods.SystemsByFile);
			Assert.AreEqual(2, (int)DemoMethods.StationsByFile);
		}
	}
}
