using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Elite.SpanshTools.Benchmark
{
	internal class Program
	{
		record Result(string Name, int Iterations, double Minimum, double Maximum, double Average, double RecordsPerSecond);
		int maxIterations = 10000;
		Stopwatch stopwatch = new();

		static async Task Main()
		{
			Console.WriteLine("This will run both a custom benchmark and BenchmarkDotNet.  Output will likely be big.");
			Console.WriteLine();
			Console.WriteLine("The BenchmarkDotNet test looks at the speed and memory performance of the two methods in the ");
			Console.WriteLine("GalaxyParser class that don't use File IO, and uses a single system for its input. The system ");
			Console.WriteLine("was chosen as one that is large enough to be significant (it has powers, factions, stations, ");
			Console.WriteLine("etc.) but not so large that it completely destroys Visual Studio's ability to handle the code file.");
			Console.WriteLine();
			Console.WriteLine("The custom benchmark takes a data file of 100 systems, including two very large ones (Sol and ");
			Console.WriteLine("Achenar), parses it, and counts the systems it finds. It does this for 10,000 iterations. The ");
			Console.WriteLine("highest and lowest values are thrown out as presumed outliers, and the remainder are used to ");
			Console.WriteLine("calculate the minimum, maximum, and average time each iteration took, along with approximately ");
			Console.WriteLine("how many records per second can be parsed.");
			Console.WriteLine();
			Console.WriteLine("Press any key to begin.");
			Console.ReadKey();

			await new Program().RunBenchmarks();
		}

		public async Task RunBenchmarks()
		{
			Console.WriteLine("Running BenchmarkDotNet");
			BenchmarkRunner.Run<Benchmarker>();

			var ioResult = await RunIoBenchmark();

			Console.WriteLine();
			Console.WriteLine("Results:");
			Console.WriteLine("------------------------------------------------");
			OutputResultString(ioResult);
		}

		private async Task<Result> RunIoBenchmark()
		{
			string benchmarkName = nameof(Benchmarker.BenchmarkWithFileIO);
			string title = $"Running benchmark: {benchmarkName}, Progress:  0%";
			Console.Write(title);

			// Keep the location so we can output progress on the same line to avoid spamming Console with newlines
			int progressLoc = title.Length - 5;
			List<double> times = [];
			int iteration = 0;
			Benchmarker benchmarker = new();
			while (iteration < maxIterations)
			{
				stopwatch.Start();
				_ = await benchmarker.BenchmarkWithFileIO();
				stopwatch.Stop();
				times.Add(stopwatch.Elapsed.TotalMilliseconds);
				stopwatch.Reset();

				iteration++;
				if (iteration % 100 == 0)
				{
					Console.CursorLeft = progressLoc;
					Console.Write($"{(iteration / 100).ToString().PadLeft(3)}%".PadRight(Console.BufferWidth - Console.CursorLeft));
					Console.CursorLeft = 0;
				}
			}

			Console.CursorLeft = progressLoc;
			Console.WriteLine("100%");
			return CalculateResult(benchmarkName, times);
		}

		private Result CalculateResult(string benchmarkName, List<double> times)
		{
			times.Sort();

			// Throw out the first and last as likely outliers
			List<double> validResults = times[1..^1];
			double average = validResults.Average();
			double averagePerSecond = (times.Count / average) * 1000;
			return new(
				benchmarkName,
				times.Count,
				validResults[0],
				validResults[^1],
				average,
				averagePerSecond);
		}

		private void OutputResultString(Result result)
		{
			Console.WriteLine($"{result.Name} ({result.Iterations:N2} iterations, 100 items parsed per iteration)");
			Console.WriteLine($"Min: {result.Minimum:N2}ms | Max: {result.Maximum:N2}ms | Avg: {result.Average:N2}ms");
			Console.WriteLine($"Records parsed per second: {result.RecordsPerSecond:N2}");
		}
	}
}
