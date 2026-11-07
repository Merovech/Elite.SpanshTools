using Elite.SpanshTools.Sample.Helpers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Tests for the sample project's HelperMethods.  These swap Console.In/Out, which is process-global,
	/// so the class opts out of the assembly's parallel execution.
	///
	/// Not covered, because they can't be exercised in-process without changing the production code:
	/// ValidateProgramArgs' two failure paths (they call Environment.Exit) and LogUpdate (it calls
	/// Console.SetCursorPosition, which throws when output is redirected).
	/// </summary>
	[TestClass]
	[DoNotParallelize]
	public class HelperMethodsTests
	{
		private TextWriter? originalOut;
		private TextReader? originalIn;

		[TestInitialize]
		public void SaveConsole()
		{
			originalOut = Console.Out;
			originalIn = Console.In;
		}

		[TestCleanup]
		public void RestoreConsole()
		{
			Console.SetOut(originalOut!);
			Console.SetIn(originalIn!);
		}

		private static StringWriter CaptureOutput()
		{
			var writer = new StringWriter();
			Console.SetOut(writer);
			return writer;
		}

		private static void ProvideInput(params string[] lines) =>
			Console.SetIn(new StringReader(string.Join(Environment.NewLine, lines) + Environment.NewLine));

		// ---- AvailableOptions -------------------------------------------------------------------------------

		[TestMethod]
		public void AvailableOptions_Excludes_The_None_Entry()
		{
			Assert.AreEqual(Enum.GetValues<DemoMethods>().Length - 1, HelperMethods.AvailableOptions);
			Assert.AreEqual(2, HelperMethods.AvailableOptions);
		}

		// ---- GetSelection -----------------------------------------------------------------------------------

		[TestMethod]
		[DataRow("0", 0)]
		[DataRow("1", 1)]
		[DataRow("2", 2)]
		public void GetSelection_Returns_A_Valid_Choice_Without_Prompting_Again(string input, int expected)
		{
			var output = CaptureOutput();
			ProvideInput(input);

			var result = HelperMethods.GetSelection();

			Assert.AreEqual((byte)expected, result);
			Assert.AreEqual(string.Empty, output.ToString(), "A valid first entry must not produce a re-prompt.");
		}

		[TestMethod]
		[DataRow("3")]
		[DataRow("255")]
		[DataRow("256")]
		[DataRow("-1")]
		[DataRow("abc")]
		[DataRow("1.5")]
		[DataRow("")]
		[DataRow("   ")]
		public void GetSelection_Reprompts_On_Invalid_Input_Until_A_Valid_Choice_Arrives(string invalid)
		{
			var output = CaptureOutput();
			ProvideInput(invalid, "2");

			var result = HelperMethods.GetSelection();

			Assert.AreEqual((byte)2, result);
			StringAssert.Contains(output.ToString(), "Enter a selection (0 to exit): ");
		}

		[TestMethod]
		public void GetSelection_Prompts_Once_Per_Rejected_Entry()
		{
			var output = CaptureOutput();
			ProvideInput("x", "99", "-5", "1");

			var result = HelperMethods.GetSelection();

			Assert.AreEqual((byte)1, result);
			const string prompt = "Enter a selection (0 to exit): ";
			var text = output.ToString();
			int prompts = (text.Length - text.Replace(prompt, string.Empty).Length) / prompt.Length;
			Assert.AreEqual(3, prompts);
		}

		[TestMethod]
		public void GetSelection_Accepts_Zero_After_Invalid_Entries_So_The_User_Can_Always_Exit()
		{
			CaptureOutput();
			ProvideInput("nope", "0");

			Assert.AreEqual((byte)0, HelperMethods.GetSelection());
		}

		// ---- DisplayMenu ------------------------------------------------------------------------------------

		[TestMethod]
		public void DisplayMenu_Writes_A_Header_One_Numbered_Line_Per_Option_And_The_Prompt()
		{
			var output = CaptureOutput();

			HelperMethods.DisplayMenu();

			var text = output.ToString();
			var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

			Assert.AreEqual("Demos:", lines[0]);
			for (int option = 1; option <= HelperMethods.AvailableOptions; option++)
			{
				Assert.IsTrue(lines.Any(l => l.StartsWith($"{option}\t", StringComparison.Ordinal)),
					$"Menu is missing an entry for option {option}.");
			}

			Assert.IsFalse(lines.Any(l => l.StartsWith($"{HelperMethods.AvailableOptions + 1}\t", StringComparison.Ordinal)),
				"Menu lists more options than DemoMethods defines.");
			Assert.IsTrue(text.EndsWith("Enter a selection (0 to exit): ", StringComparison.Ordinal),
				"The prompt should be last and not be followed by a newline.");
		}

		// ---- ValidateProgramArgs ----------------------------------------------------------------------------

		[TestMethod]
		public void ValidateProgramArgs_Returns_The_First_Argument_When_The_File_Exists()
		{
			var output = CaptureOutput();
			using var file = new TempFile("[]");

			var result = HelperMethods.ValidateProgramArgs([file.Path]);

			Assert.AreEqual(file.Path, result);
			Assert.AreEqual(string.Empty, output.ToString());
		}

		[TestMethod]
		public void ValidateProgramArgs_Ignores_Extra_Arguments()
		{
			CaptureOutput();
			using var file = new TempFile("[]");

			var result = HelperMethods.ValidateProgramArgs([file.Path, "--unused", "extra"]);

			Assert.AreEqual(file.Path, result);
		}
	}
}
