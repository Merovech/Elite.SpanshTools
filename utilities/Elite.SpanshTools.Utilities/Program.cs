using System.Text.Json;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Utilities
{
	public class Program
	{
		static async Task<int> Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Usage: ModelVerifier.exe path\\to\\datafile");
				return 1;
			}

			if (!File.Exists(args[0]))
			{
				Console.WriteLine($"File '{args[0]}' not found.");
				return 1;
			}

			int recordCount = 0;
			Console.WriteLine("This tool parses a Spansh data file to the model to see if any properties are");
			Console.WriteLine("missing or incorrectly parsed.  If there are any errors, the tool will exit.");
			Console.WriteLine("Otherwise, the tool will run to the end.  The larger the data file, the more");
			Console.WriteLine("trustworthy the results, since there are more systems that will be encountered");
			Console.WriteLine("that could potentially have quirks.");
			Console.WriteLine();
			Console.WriteLine("For ideal results, this should be run against the full Spansh galaxy dump, but");
			Console.WriteLine("that takes a long time -- on the order of half an hour or more.");

			Console.WriteLine();
			Console.WriteLine($"Input file: {args[0]}");
			Console.WriteLine("Beginning verificiation process.");
			Console.WriteLine();

			try
			{
				DateTime start = DateTime.Now;
				Console.Write("Records parsed: 0");
				GalaxyParser parser = new GalaxyParser();
				await foreach (StarSystem? system in parser.ParseFileAsync(args[0]))
				{
					recordCount++;
					if (recordCount % 10000 == 0)
					{
						Console.CursorLeft = 0;
						Console.Write($"Records parsed: { recordCount }".PadRight(Console.BufferWidth));
					}
				}

				Console.CursorLeft = 0;
				Console.WriteLine($"Records parsed: {recordCount}".PadRight(Console.BufferWidth));
				Console.WriteLine("No errors found.  Model and data format are in sync.");
				Console.WriteLine($"Time elapsed: {DateTime.Now.Subtract(start):c}");

				return 0;
			}
			catch (JsonException e)
			{
				Console.WriteLine($"\nParsing error (line: {e.LineNumber}): {e.Message}");
				return 1;
			}
			catch (Exception e)
			{
				Console.WriteLine($"\nUnknown error: {e.Message}");
				return 1;
			}
		}
	}
}
