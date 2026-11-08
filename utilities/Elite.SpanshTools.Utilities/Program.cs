using System.Diagnostics;
using System.IO.Compression;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using Elite.SpanshTools.Parsers;

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
			File.Delete(archivePath);
			return extractedPath;
		}

		private static async Task<int> VerifyAsync(string dataFilePath)
		{
			Console.WriteLine("This tool parses a Spansh data file to the model to see if any properties are");
			Console.WriteLine("missing or incorrectly parsed.  It runs to the end, collecting every error, and");
			Console.WriteLine("groups them by kind.  The larger the data file, the more trustworthy the results,");
			Console.WriteLine("since there are more systems that will be encountered that could potentially");
			Console.WriteLine("have quirks.");
			Console.WriteLine();
			Console.WriteLine("For ideal results, this should be run against the full Spansh galaxy dump.  It is");
			Console.WriteLine("parsed on every core, so it takes minutes rather than hours, but it needs several");
			Console.WriteLine("GB of memory while it runs.");

			Console.WriteLine();
			Console.WriteLine($"Input file: {dataFilePath}");
			Console.WriteLine($"Server GC: {GCSettings.IsServerGC}.  Logical processors: {Environment.ProcessorCount}.");
			Console.WriteLine("Beginning verification process.");
			Console.WriteLine();

			long parsed = 0;
			var errors = new ErrorReport();
			var stopwatch = Stopwatch.StartNew();
			using var progressCts = new CancellationTokenSource();
			var progress = ReportProgressAsync(() => Interlocked.Read(ref parsed) + errors.Count, () => errors.Count, progressCts.Token);

			try
			{
				var options = new ParallelParseOptions { OnElementError = errors.Add };
				await new GalaxyParser(SerializerOptions).ParseFileParallelAsync(dataFilePath, _ => Interlocked.Increment(ref parsed), options);
			}
			catch (Exception e)
			{
				await StopProgressAsync(progressCts, progress);
				Console.WriteLine($"\nUnknown error: {e.Message}");
				return 1;
			}

			await StopProgressAsync(progressCts, progress);
			stopwatch.Stop();

			Console.WriteLine($"Records parsed: {parsed + errors.Count:N0}".PadRight(ConsoleWidth()));
			if (errors.Count > 0)
			{
				Console.WriteLine($"\n{errors.Count:N0} errors found, of {errors.Kinds.Count:N0} kinds.  Writing them to {ErrorFileName}.");
				foreach (var kind in errors.Kinds)
				{
					Console.WriteLine($"  {kind.Count,12:N0}  {kind.Key}");
				}

				await errors.WriteAsync(ErrorFileName);
			}
			else
			{
				Console.WriteLine("No errors found.  Model and data format are in sync.");
				Console.WriteLine("Deleting the input file to save space.");
				File.Delete(dataFilePath);
			}

			Console.WriteLine($"Time elapsed: {stopwatch.Elapsed:c}");
			return errors.Count > 0 ? 1 : 0;
		}

		private const string ErrorFileName = "ModelVerificationErrors.txt";

		// Workers can't share the console cursor, so progress comes from a timer that reads the counters.
		private static async Task ReportProgressAsync(Func<long> parsed, Func<long> errors, CancellationToken cancellationToken)
		{
			if (Console.IsOutputRedirected)
			{
				return;
			}

			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
			try
			{
				while (await timer.WaitForNextTickAsync(cancellationToken))
				{
					Console.CursorLeft = 0;
					Console.Write($"Records parsed: {parsed():N0}, Errors found: {errors():N0}".PadRight(ConsoleWidth() - 1));
				}
			}
			catch (OperationCanceledException)
			{
			}

			Console.CursorLeft = 0;
		}

		private static async Task StopProgressAsync(CancellationTokenSource cts, Task progress)
		{
			cts.Cancel();
			await progress;
		}

		private static int ConsoleWidth() => Console.IsOutputRedirected ? 80 : Console.BufferWidth;
	}
}
