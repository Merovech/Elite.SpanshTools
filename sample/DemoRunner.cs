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

				// TO DO: Other IGalaxyParser methods.
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
					stationCount += system.Stations.Count;
					stationCount += system.Bodies.Sum(b => b.Stations.Count);

					if (systemCount % 10000 == 0)
					{
						console.LogUpdate(stationCount);
					}
				}
			}

			console.ResetCursorLeft();
			console.WriteLine($"Stations found: {stationCount}");
		}
	}
}
