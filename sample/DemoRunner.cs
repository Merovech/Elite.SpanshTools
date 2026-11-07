using System.Diagnostics;
using Elite.SpanshTools.Parsers;
using Elite.SpanshTools.Sample.Helpers;

namespace Elite.SpanshTools.Sample
{
	public class DemoRunner(string filename)
	{
		private string Filename { get; init; } = filename;

		private readonly IConsoleIO console = new SystemConsole();

		/// <summary>Test seam: lets unit tests substitute the console.  Production code uses the public constructor.</summary>
		internal DemoRunner(string filename, IConsoleIO console) : this(filename)
		{
			this.console = console;
		}

		public async Task RunDemo(DemoMethods methodToRun)
		{
			switch (methodToRun)
			{
				case DemoMethods.None:
					return;

				case DemoMethods.SystemsByFile:
					console.WriteLine($"Counting systems found in '{Filename}...");
					await RunDemoInternal(CountSystemsByFile);
					break;

				case DemoMethods.StationsByFile:
					console.WriteLine($"Counting stations found in '{Filename}...");
					await RunDemoInternal(CountStationsWithModel);
					break;

				case DemoMethods.SystemsByFileParallel:
					console.WriteLine($"Counting systems found in '{Filename}' in parallel...");
					await RunDemoInternal(CountSystemsInParallel);
					break;

				case DemoMethods.StationsByFileInBatches:
					console.WriteLine($"Counting stations found in '{Filename}' in parallel batches...");
					await RunDemoInternal(CountStationsInBatches);
					break;

				default:
					console.WriteLine("Unknown option selected.");
					break;
			}
		}

		private async Task RunDemoInternal(Func<Task> demoMethod)
		{
			Stopwatch sw = new();
			sw.Start();
			await demoMethod();
			sw.Stop();
			console.WriteLine($"Completed in {sw.Elapsed:c}");
			console.WriteLine("Press any key to continue.");
			console.ReadKey();
		}

		/// <summary>
		/// Counts stations in the file using the object model.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private async Task CountSystemsByFile()
		{
			long count = 0;

			GalaxyParser parser = new GalaxyParser();
			await foreach (var system in parser.ParseFileAsync(Filename))
			{
				if (system is null)
				{
					throw new NullReferenceException("The input file contains a null entry where a star system was expected.");
				}

				count++;
				if (count % 10000 == 0)
				{
					console.LogUpdate(count);
				}
			}

			console.ResetCursorLeft();
			console.WriteLine($"Systems found: {count}");
		}

		/// <summary>
		/// Counts stations in the file using the object model, which parses slower but is easier to
		/// work with.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private async Task CountStationsWithModel()
		{
			long systemCount = 0;
			long stationCount = 0;

			GalaxyParser parser = new GalaxyParser();
			await foreach (var system in parser.ParseFileAsync(Filename))
			{
				if (system is not null)
				{
					systemCount++;
					stationCount += StationsIn(system);

					if (systemCount % 10000 == 0)
					{
						console.LogUpdate(stationCount);
					}
				}
			}

			console.ResetCursorLeft();
			console.WriteLine($"Stations found: {stationCount}");
		}

		/// <summary>
		/// Counts systems using every core.  The callback runs on several threads at once, so the count is kept
		/// with Interlocked and console output is serialized with a lock.
		/// </summary>
		private async Task CountSystemsInParallel()
		{
			long count = 0;
			var consoleLock = new object();

			GalaxyParser parser = new GalaxyParser();
			await parser.ParseFileParallelAsync(Filename, system =>
			{
				if (system is null)
				{
					throw new NullReferenceException("The input file contains a null entry where a star system was expected.");
				}

				long current = Interlocked.Increment(ref count);
				if (current % 10000 == 0)
				{
					lock (consoleLock)
					{
						console.LogUpdate(current);
					}
				}
			});

			console.ResetCursorLeft();
			console.WriteLine($"Systems found: {count}");
		}

		/// <summary>
		/// Counts stations using every core for parsing, while this loop consumes the parsed batches on one thread,
		/// so no locking is needed here.
		/// </summary>
		private async Task CountStationsInBatches()
		{
			long systemCount = 0;
			long stationCount = 0;

			GalaxyParser parser = new GalaxyParser();
			await foreach (var batch in parser.ParseFileInBatchesAsync(Filename))
			{
				foreach (var system in batch)
				{
					if (system is null)
					{
						continue;
					}

					systemCount++;
					stationCount += StationsIn(system);

					if (systemCount % 10000 == 0)
					{
						console.LogUpdate(stationCount);
					}
				}
			}

			console.ResetCursorLeft();
			console.WriteLine($"Stations found: {stationCount}");
		}

		private static int StationsIn(Model.StarSystem system) => system.Stations.Count + system.Bodies.Sum(b => b.Stations.Count);
	}
}
