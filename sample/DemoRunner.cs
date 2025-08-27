using System.Diagnostics;
using System.Text.Json;
using Elite.SpanshTools.Model;
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

		public void RunDemo(DemoMethods methodToRun)
		{
			switch (methodToRun)
			{
				case DemoMethods.None:
					return;

				case DemoMethods.SystemsByModel:
					Console.WriteLine("Counting systems with the data model.");
					RunDemoInternal(CountSystemsWithModel);
					break;

				case DemoMethods.SystemsByDocument:
					Console.WriteLine("Count systems with doc.");
					RunDemoInternal(CountSystemsWithDocument);
					break;

				case DemoMethods.StationsByModel:
					Console.WriteLine("Count stations with model.");
					RunDemoInternal(CountStationsWithModel);
					break;

				case DemoMethods.StationsByDocument:
					Console.WriteLine("Count stations with doc.");
					RunDemoInternal(CountStationsWithDocument);
					break;

				default:
					Console.WriteLine("Writing benchmark data.");
					new GalaxyParser().GenerateBenchmarkData(Filename);
					Console.WriteLine("Done.");
					Console.ReadKey();
					break;
			}
		}

		private void RunDemoInternal(Action demoMethod)
		{
			Stopwatch sw = new();
			sw.Start();
			demoMethod.Invoke();
			sw.Stop();
			Console.WriteLine($"Completed in {sw.Elapsed:c}");
			Console.WriteLine("Press any key to continue.");
			Console.ReadKey();
		}

		/// <summary>
		/// Counts stations in the file using the object model.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private void CountSystemsWithModel()
		{
			long count = 0;

			IGalaxyParser parser = new GalaxyParser();
			foreach (StarSystem system in parser.ParseToModel(Filename))
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
		/// Counts stations in the file using JsonDocument.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private void CountSystemsWithDocument()
		{
			long count = 0;

			IGalaxyParser parser = new GalaxyParser();
			foreach (JsonDocument system in parser.ParseToDocument(Filename))
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
		private void CountStationsWithModel()
		{
			long systemCount = 0;
			long stationCount = 0;

			IGalaxyParser parser = new GalaxyParser();
			foreach (StarSystem system in parser.ParseToModel(Filename))
			{
				systemCount++;
				stationCount += system.Stations.Count;
				stationCount += system.Bodies.Sum(b => b.Stations.Count);

				if (systemCount % 10000 == 0)
				{
					HelperMethods.LogUpdate(stationCount);
				}
			}

			Console.CursorLeft = 0;
			Console.WriteLine($"Stations found: {stationCount}");
		}

		/// <summary>
		/// Counts stations in the file using JsonDocument, which parses faster but it's more
		/// difficult to access data in the parsed object.
		/// </summary>
		/// <param name="filename">The path to the file containing the JSON.</param>
		private void CountStationsWithDocument()
		{
			long systemCount = 0;
			long stationCount = 0;

			IGalaxyParser parser = new GalaxyParser();
			foreach (JsonDocument system in parser.ParseToDocument(Filename))
			{
				systemCount++;
				JsonElement root = system.RootElement;

				if (root.TryGetProperty("stations", out JsonElement systemStationsElement))
				{
					stationCount += systemStationsElement.GetArrayLength();
				}

				// Bodies will always exist and have at least one item in it -- after all, even the jump point star is a body
				JsonElement bodies = root.GetProperty("bodies");
				var bodyEnumerator = bodies.EnumerateArray();
				while (bodyEnumerator.MoveNext())
				{
					JsonElement body = bodyEnumerator.Current;
					if (body.TryGetProperty("stations", out JsonElement bodyStationsElement))
					{
						stationCount += bodyStationsElement.GetArrayLength();
					}
				}

				if (systemCount % 10000 == 0)
				{
					HelperMethods.LogUpdate(stationCount);
				}
			}

			Console.CursorLeft = 0;
			Console.WriteLine($"Stations found: {stationCount}");
		}
	}
}
