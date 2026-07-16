using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Utilities
{
	public class Options
	{
		[Option("data-location", Required = true, HelpText = "Existing folder that will hold (or already holds) the data file.")]
		public string DataLocation { get; set; } = string.Empty;

		[Option("data-file", Required = true, HelpText = "Either an http(s) URI to a zip that contains a single data file, or the name of a file already inside --data-location.")]
		public string DataFile { get; set; } = string.Empty;
	}

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
			var parseResult = Parser.Default.ParseArguments<Options>(args);
			if (parseResult.Errors.Any())
			{
				return 1;
			}

			var options = parseResult.Value;

			string dataLocation;
			try
			{
				dataLocation = Path.GetFullPath(options.DataLocation);
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error: --data-location '{options.DataLocation}' is not a valid path: {e.Message}");
				return 1;
			}

			if (!Directory.Exists(dataLocation))
			{
				Console.WriteLine($"Error: --data-location '{dataLocation}' does not exist or is not a folder.");
				return 1;
			}

			string dataFilePath;
			if (Uri.TryCreate(options.DataFile, UriKind.Absolute, out var uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				try
				{
					dataFilePath = await DownloadAndExtractAsync(uri, dataLocation);
				}
				catch (Exception e)
				{
					Console.WriteLine($"Error preparing data file from URI: {e.Message}");
					return 1;
				}
			}
			else
			{
				dataFilePath = Path.GetFullPath(Path.Combine(dataLocation, options.DataFile));
				if (!File.Exists(dataFilePath))
				{
					Console.WriteLine($"File '{dataFilePath}' not found.");
					return 1;
				}
			}

			return await VerifyAsync(dataFilePath);
		}

		private static async Task<string> DownloadAndExtractAsync(Uri uri, string dataLocation)
		{
			var archiveName = Path.GetFileName(uri.LocalPath);
			if (string.IsNullOrEmpty(archiveName))
			{
				throw new InvalidOperationException($"Unable to derive a filename from URI '{uri}'.");
			}

			if (!archiveName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Expected a .gz file, but URI '{uri}' points to '{archiveName}'.");
			}

			var archivePath = Path.GetFullPath(Path.Combine(dataLocation, archiveName));
			if (File.Exists(archivePath))
			{
				File.Delete(archivePath);
			}

			Console.WriteLine($"Downloading '{uri}' to '{archivePath}'...");
			var curl = new ProcessStartInfo("curl", ["-L", "-f", "-o", archivePath, uri.ToString()])
			{
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				UseShellExecute = false
			};

			using (var process = Process.Start(curl) ?? throw new InvalidOperationException("Failed to start curl."))
			{
				await process.WaitForExitAsync();
				if (process.ExitCode != 0)
				{
					throw new InvalidOperationException($"curl exited with code {process.ExitCode}.");
				}
			}

			var extractedName = Path.GetFileNameWithoutExtension(archiveName);
			var extractedPath = Path.GetFullPath(Path.Combine(dataLocation, extractedName));
			if (File.Exists(extractedPath))
			{
				File.Delete(extractedPath);
			}

			Console.WriteLine($"Extracting '{archivePath}' to '{extractedPath}'...");
			await using (var source = File.OpenRead(archivePath))
			await using (var gzip = new GZipStream(source, CompressionMode.Decompress))
			await using (var destination = File.Create(extractedPath))
			{
				var totalCompressed = source.Length;
				var buffer = new byte[81920];
				var lastUpdate = DateTime.MinValue;
				var animationFrames = new[] {'/', '-', '\\', '|'};
				var curAnimationFrame = 0;
				int bytesRead;
				while ((bytesRead = await gzip.ReadAsync(buffer)) > 0)
				{
					await destination.WriteAsync(buffer.AsMemory(0, bytesRead));

					var now = DateTime.UtcNow;
					if ((now - lastUpdate).TotalMilliseconds >= 1000)
					{
						var percent = totalCompressed > 0 ? (int)(source.Position * 100L / totalCompressed) : 0;
						Console.CursorLeft = 0;
						Console.Write($"Progress: {percent}% {animationFrames[curAnimationFrame % animationFrames.Length]}".PadRight(Console.BufferWidth));
						curAnimationFrame++;
						lastUpdate = now;
					}
				}

				Console.CursorLeft = 0;
				Console.WriteLine("Progress: 100%".PadRight(Console.BufferWidth));
			}

			Console.WriteLine($"Extracted '{extractedPath}'.");
			return extractedPath;
		}

		private static async Task<int> VerifyAsync(string dataFilePath)
		{
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
			Console.WriteLine($"Input file: {dataFilePath}");
			Console.WriteLine("Beginning verificiation process.");
			Console.WriteLine();

			try
			{
				DateTime start = DateTime.Now;
				Console.Write("Records parsed: 0");
				await foreach (var line in File.ReadLinesAsync(dataFilePath))
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
					Console.WriteLine($"\n{errors.Count} errors found.  Writing errors and related lines to ModelVerificationErrors.txt");
					Console.WriteLine("WARNING: If there are lot of errors, this could take a while and the file could be very large.");
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
