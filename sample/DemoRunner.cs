using System.Diagnostics;
using Elite.SpanshTools.Parsers;
using Elite.SpanshTools.Sample.Helpers;

namespace Elite.SpanshTools.Sample
{
	public class DemoRunner
	{
		private string Filename { get; init; }

		public DemoRunner(string filename)
		{
			Filename = filename;
		}

		public async Task RunDemo(DemoMethods methodToRun)
		{
			switch (methodToRun)
			{
				case DemoMethods.None:
					return;

				case DemoMethods.SystemsByFile:
					Console.WriteLine($"Counting systems found in '{Filename}...");
					await RunDemoInternal(CountSystemsByFile);
					break;

				case DemoMethods.StationsByFile:
					Console.WriteLine($"Counting stations found in '{Filename}...");
					await RunDemoInternal(CountStationsWithModel);
					break;

				// TO DO: Other IGalaxyParser methods.
				default:
					Console.WriteLine("Unknown option selected.");
					break;
			}
		}

		private async Task RunDemoInternal(Func<Task> demoMethod)
		{
			Stopwatch sw = new();
			sw.Start();
			await demoMethod();
			sw.Stop();
			Console.WriteLine($"Completed in {sw.Elapsed:c}");
			Console.WriteLine("Press any key to continue.");
			Console.ReadKey();
		}

		/// <summary>
		/// Counts stations in the file using the object model.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private async Task CountSystemsByFile()
		{
			long count = 0;

			IGalaxyParser parser = new GalaxyParser();
			await foreach (var system in parser.ParseFileAsync(Filename))
			{
				count++;
				if (count % 10000 == 0)
				{
					HelperMethods.LogUpdate(count);
				}
			}

			Console.CursorLeft = 0;
			Console.WriteLine($"Systems found: {count}");
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

			IGalaxyParser parser = new GalaxyParser();
			await foreach (var system in parser.ParseFileAsync(Filename))
			{
				if (system is not null)
				{
					systemCount++;
					stationCount += system.Stations.Count;
					stationCount += system.Bodies.Sum(b => b.Stations.Count);

					if (systemCount % 10000 == 0)
					{
						HelperMethods.LogUpdate(stationCount);
					}
				}
			}

			Console.CursorLeft = 0;
			Console.WriteLine($"Stations found: {stationCount}");
		}
	}
}
