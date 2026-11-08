namespace Elite.SpanshTools.Sample.Helpers
{
	/// <summary>
	/// Thin abstraction over the console operations used by <see cref="DemoRunner"/>, so the runner can be
	/// unit tested without a real terminal (Console.ReadKey and Console.SetCursorPosition throw when
	/// input/output is redirected, which is the case under a test host).
	/// </summary>
	internal interface IConsoleIO
	{
		void WriteLine(string message);

		void ReadKey();

		void ResetCursorLeft();

		void LogUpdate(long count);
	}

	/// <summary>
	/// The real console.  Behaves exactly as the previous direct Console calls in DemoRunner did.
	/// </summary>
	internal sealed class SystemConsole : IConsoleIO
	{
		public void WriteLine(string message) => Console.WriteLine(message);

		public void ReadKey() => Console.ReadKey();

		public void ResetCursorLeft() => Console.CursorLeft = 0;

		public void LogUpdate(long count) => HelperMethods.LogUpdate(count);
	}
}
