namespace Elite.SpanshTools.Sample.Helpers
{
	internal static class HelperMethods
	{
		internal static string ValidateProgramArgs(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Usage:\nests.exe [path_to_file]");
				Environment.Exit(-1);
			}

			var filename = args[0];
			if (!File.Exists(filename))
			{
				Console.WriteLine($"File '{filename}' not found.");
				Environment.Exit(-1);
			}

			return filename;
		}

		internal static byte GetSelection()
		{
			var selection = Console.ReadLine();
			var result = byte.MaxValue;
			while (!byte.TryParse(selection, out result) || result < 0 || result > 5)
			{
				Console.Write("Enter a selection (0 to exit): ");
				selection = Console.ReadLine();
			}

			return result;
		}

		internal static void DisplayMenu()
		{
			Console.WriteLine("Demos:");
			Console.WriteLine("1\tCount systems using object model (slower parse, easier data access)");
			Console.WriteLine("2\tCount systems using JsonDocument (faster parse, slower/harder data access)");
			Console.WriteLine("3\tCount stations using object model (slower parse, easier data access)");
			Console.WriteLine("4\tCount stations using JsonDocument (faster parse, slower/harder data access)");
			Console.Write("Enter a selection (0 to exit): ");
		}

		public static void LogUpdate(long count)
		{
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write($"Found: {count}".PadRight(Console.BufferWidth - Console.CursorLeft));
			Console.CursorLeft = 0; // Looks better, honestly
		}
	}
}
