using System.Diagnostics;
using System.Runtime;
using BenchmarkDotNet.Running;
using Elite.SpanshTools.Parsers;

namespace Elite.SpanshTools.Benchmark
{
	internal class Program
	{
		record Result(string Name, int Iterations, long RecordsPerIteration, double Minimum, double Maximum, double Average, double RecordsPerSecond);
		int maxIterations = 10000;
		Stopwatch stopwatch = new();

		// Usage:
		//   Elite.SpanshTools.Benchmark                     BenchmarkDotNet, then the data.json file benchmarks
		//   Elite.SpanshTools.Benchmark --io-only           the data.json file benchmarks only
		//   Elite.SpanshTools.Benchmark --large-file <path> [--only sequential|parallel]
		//                                                   one timed pass over a large file, sequential and parallel
		static async Task Main(string[] args)
		{
			string? largeFile = ArgumentAfter(args, "--large-file");
			if (largeFile is not null)
			{
				await new Program().RunLargeFileBenchmark(largeFile, ArgumentAfter(args, "--only"));
				return;
			}

			bool ioOnly = args.Contains("--io-only");
			Console.WriteLine("This will run both a custom benchmark and BenchmarkDotNet.  Output will likely be big.");
			Console.WriteLine();
			Console.WriteLine("The BenchmarkDotNet test looks at the speed and memory performance of the two methods in the ");
			Console.WriteLine("GalaxyParser class that don't use File IO, and uses a single system for its input. The system ");
			Console.WriteLine("was chosen as one that is large enough to be significant (it has powers, factions, stations, ");
			Console.WriteLine("etc.) but not so large that it completely destroys Visual Studio's ability to handle the code file.");
			Console.WriteLine();
			Console.WriteLine("The custom benchmarks take a data file of 100 systems, including two very large ones (Sol and ");
			Console.WriteLine("Achenar), parse it, and count the systems found, 10,000 times each: once sequentially and twice ");
			Console.WriteLine("with the parallel API (default options, then small chunks).  The highest and lowest values are ");
			Console.WriteLine("thrown out as presumed outliers, and the remainder are used to calculate the minimum, maximum, ");
			Console.WriteLine("and average time each iteration took, along with approximately how many records per second can ");
			Console.WriteLine("be parsed.  Sol alone is about half of that file and is parsed by one thread, so the parallel gain ");
			Console.WriteLine("on it is capped at about 2x; use --large-file for a realistic comparison.");
			Console.WriteLine();
			Console.WriteLine($"Server GC: {GCSettings.IsServerGC}.  Logical processors: {Environment.ProcessorCount}.");
			Console.WriteLine();

			if (!Console.IsInputRedirected)
			{
				Console.WriteLine("Press any key to begin.");
				Console.ReadKey();
			}

			await new Program().RunBenchmarks(ioOnly);
		}

		public async Task RunBenchmarks(bool ioOnly)
		{
			if (!ioOnly)
			{
				Console.WriteLine("Running BenchmarkDotNet");
				BenchmarkRunner.Run<Benchmarker>();
			}

			var results = new List<Result>
			{
				await RunIoBenchmark(nameof(Benchmarker.BenchmarkWithFileIO), b => b.BenchmarkWithFileIO()),
				await RunIoBenchmark(nameof(Benchmarker.BenchmarkWithFileIOParallel), b => b.BenchmarkWithFileIOParallel()),
				await RunIoBenchmark(nameof(Benchmarker.BenchmarkWithFileIOParallelSmallChunks), b => b.BenchmarkWithFileIOParallelSmallChunks()),
			};

			Console.WriteLine();
			Console.WriteLine("Results:");
			Console.WriteLine("------------------------------------------------");
			foreach (var result in results)
			{
				OutputResultString(result);
				Console.WriteLine($"Relative to {results[0].Name}: {results[0].Average / result.Average:N2}x");
				Console.WriteLine();
			}
		}

		private async Task<Result> RunIoBenchmark(string benchmarkName, Func<Benchmarker, Task<long>> benchmark)
		{
			string title = $"Running benchmark: {benchmarkName}, Progress:  0%";
			Console.Write(title);

			// Keep the location so we can output progress on the same line to avoid spamming Console with newlines.
			// Skipped when output is redirected, where the cursor can't be moved.
			bool showProgress = !Console.IsOutputRedirected;
			int progressLoc = title.Length - 5;
			List<double> times = [];
			long recordsPerIteration = 0;
			int iteration = 0;
			Benchmarker benchmarker = new();
			while (iteration < maxIterations)
			{
				stopwatch.Start();
				recordsPerIteration = await benchmark(benchmarker);
				stopwatch.Stop();
				times.Add(stopwatch.Elapsed.TotalMilliseconds);
				stopwatch.Reset();

				iteration++;
				if (showProgress && iteration % 100 == 0)
				{
					Console.CursorLeft = progressLoc;
					Console.Write($"{(iteration / 100).ToString().PadLeft(3)}%".PadRight(Console.BufferWidth - Console.CursorLeft));
					Console.CursorLeft = 0;
				}
			}

			if (showProgress)
			{
				Console.CursorLeft = progressLoc;
			}

			Console.WriteLine(showProgress ? "100%" : " done");
			return CalculateResult(benchmarkName, times, recordsPerIteration);
		}

		private Result CalculateResult(string benchmarkName, List<double> times, long recordsPerIteration)
		{
			times.Sort();

			// Throw out the first and last as likely outliers
			List<double> validResults = times[1..^1];
			double average = validResults.Average();

			// Records per iteration over milliseconds per iteration.  (This used to divide the iteration count, not
			// the records per iteration, which overstated the rate by a factor of 100.)
			double recordsPerSecond = recordsPerIteration / average * 1000;
			return new(
				benchmarkName,
				times.Count,
				recordsPerIteration,
				validResults[0],
				validResults[^1],
				average,
				recordsPerSecond);
		}

		private void OutputResultString(Result result)
		{
			Console.WriteLine($"{result.Name} ({result.Iterations:N0} iterations, {result.RecordsPerIteration:N0} items parsed per iteration)");
			Console.WriteLine($"Min: {result.Minimum:N2}ms | Max: {result.Maximum:N2}ms | Avg: {result.Average:N2}ms");
			Console.WriteLine($"Records parsed per second: {result.RecordsPerSecond:N2}");
		}

		private async Task RunLargeFileBenchmark(string path, string? only)
		{
			if (!File.Exists(path))
			{
				Console.WriteLine($"File '{path}' not found.");
				return;
			}

			long bytes = new FileInfo(path).Length;
			Console.WriteLine($"Large-file benchmark: {path} ({bytes / 1e9:N1} GB)");
			Console.WriteLine($"Server GC: {GCSettings.IsServerGC}.  Logical processors: {Environment.ProcessorCount}.");
			Console.WriteLine();

			var parser = new GalaxyParser();
			TimeSpan? sequentialTime = null, parallelTime = null;

			if (only is null or "parallel")
			{
				Console.WriteLine("Parallel (ParseFileParallelAsync, default options)...");
				long count = 0;
				var sw = Stopwatch.StartNew();
				await parser.ParseFileParallelAsync(path, _ => Interlocked.Increment(ref count));
				sw.Stop();
				parallelTime = sw.Elapsed;
				OutputLargeFileResult(count, bytes, sw.Elapsed);
			}

			if (only is null or "sequential")
			{
				Console.WriteLine("Sequential (ParseFileAsync)...");
				long count = 0;
				var sw = Stopwatch.StartNew();
				await foreach (var _ in parser.ParseFileAsync(path))
				{
					count++;
				}

				sw.Stop();
				sequentialTime = sw.Elapsed;
				OutputLargeFileResult(count, bytes, sw.Elapsed);
			}

			if (sequentialTime is not null && parallelTime is not null)
			{
				Console.WriteLine($"Parallel speed-up: {sequentialTime.Value / parallelTime.Value:N2}x");
			}
		}

		private static void OutputLargeFileResult(long records, long bytes, TimeSpan elapsed)
		{
			Console.WriteLine($"  {records:N0} records in {elapsed:hh\\:mm\\:ss\\.ff}: {records / elapsed.TotalSeconds:N0} records/s, {bytes / 1e9 / elapsed.TotalSeconds:N2} GB/s");
			Console.WriteLine($"  Peak working set: {Process.GetCurrentProcess().PeakWorkingSet64 / 1e9:N2} GB");
			Console.WriteLine();
		}

		private static string? ArgumentAfter(string[] args, string name)
		{
			int index = Array.IndexOf(args, name);
			return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
		}
	}
}
