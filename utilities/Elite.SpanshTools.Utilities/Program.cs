using System.Text.Json;
using System.Text.Json.Serialization;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Utilities
{
	public class Program
	{
		private readonly static JsonSerializerOptions SerializerOptions = new()
		{
			PropertyNameCaseInsensitive = true,

			// Ensures that if there are any properties in the data that aren't mapped to the model, an error will be thrown
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
		};

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
			List<(string Error, string Line)> errors = [];

			Console.WriteLine("This tool parses a Spansh data file to the model to see if any properties are");
			Console.WriteLine("missing or incorrectly parsed.  If there are any errors, the tool will exit.");
			Console.WriteLine("Otherwise, the tool will run to the end.  The larger the data file, the more");
			Console.WriteLine("trustworthy the results, since there are more systems that will be encountered");
			Console.WriteLine("that could potentially have quirks.");
			Console.WriteLine();
			Console.WriteLine("For ideal results, this should be run against the full Spansh galaxy dump, but");
			Console.WriteLine("that takes a long time -- on the order of hours.");

			Console.WriteLine();
			Console.WriteLine($"Input file: {args[0]}");
			Console.WriteLine("Beginning verificiation process.");
			Console.WriteLine();

			try
			{
				DateTime start = DateTime.Now;
				Console.Write("Records parsed: 0");
				await foreach (var line in File.ReadLinesAsync(args[0]))
				{
					if (line == "[" || line == "]")
					{
						continue;
					}

					try
					{
						var sanitizedLine = line;
						if (line.EndsWith(','))
						{
							sanitizedLine = line[..^1];
						}

						JsonSerializer.Deserialize<StarSystem>(sanitizedLine, SerializerOptions);
					}
					catch (JsonException e)
					{
						errors.Add(($"Parsing error (line: {e.LineNumber}): {e.Message}", line));
					}
					finally
					{
						recordCount++;

						// Processing is slow enough during verificationm that printing every 10k doesn't slow down the process in any meaningful way.
						if (recordCount % 10000 == 0)
						{
							Console.CursorLeft = 0;
							Console.Write($"Records parsed: {recordCount}, Errors found: {errors.Count}".PadRight(Console.BufferWidth));
						}
					}
				}

				Console.CursorLeft = 0;
				Console.WriteLine($"Records parsed: {recordCount}".PadRight(Console.BufferWidth));
				if (errors.Count > 0)
				{
					Console.WriteLine($"\n{errors.Count} errors found:");
					using (var writer = new StreamWriter("ModelVerificationErrors.txt"))
					{
						foreach (var (Error, Line) in errors)
						{
							await writer.WriteLineAsync(Error);
							await writer.WriteLineAsync("------------------------------");
							await writer.WriteLineAsync(Line);
							await writer.WriteLineAsync("------------------------------");
						}
					}
				}
				else
				{
					Console.WriteLine("No errors found.  Model and data format are in sync.");
				}

				Console.WriteLine($"Time elapsed: {DateTime.Now.Subtract(start):c}");

				return errors.Count > 0 ? 1 : 0;
			}
			catch (Exception e)
			{
				Console.WriteLine($"\nUnknown error: {e.Message}");
				return 1;
			}
		}
	}
}
