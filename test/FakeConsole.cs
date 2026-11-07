using Elite.SpanshTools.Sample.Helpers;

namespace Elite.SpanshTools.Tests
{
	/// <summary>Records what DemoRunner asks the console to do, without touching the real console.</summary>
	internal sealed class FakeConsole : IConsoleIO
	{
		internal List<string> Lines { get; } = [];

		internal List<long> LogUpdates { get; } = [];

		internal int ReadKeyCalls { get; private set; }

		internal int CursorResets { get; private set; }

		public void WriteLine(string message) => Lines.Add(message);

		public void ReadKey() => ReadKeyCalls++;

		public void ResetCursorLeft() => CursorResets++;

		public void LogUpdate(long count) => LogUpdates.Add(count);
	}
}
