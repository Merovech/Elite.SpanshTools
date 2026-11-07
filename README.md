# Elite.SpanshTools
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

![NuGet Version](https://img.shields.io/nuget/v/Elite.SpanshTools) ![NuGet Downloads](https://img.shields.io/nuget/dt/Elite.SpanshTools)

[![CI Build (Release)](https://github.com/Merovech/Elite.SpanshTools/actions/workflows/ci-build-release.yaml/badge.svg)](https://github.com/Merovech/Elite.SpanshTools/actions/workflows/ci-build-release.yaml)
[![CI Build (Debug)](https://github.com/Merovech/Elite.SpanshTools/actions/workflows/ci-build-debug.yaml/badge.svg)](https://github.com/Merovech/Elite.SpanshTools/actions/workflows/ci-build-debug.yaml)

Tools for working with Spansh data dumps for Elite Dangerous  

## Installation
Elite.SpanshTools is available via NuGet:

``` powershell
# Using powershell
Install-Package Elite.SpanshTools
```
... or...
``` ps
# Using the .NET CLI
dotnet add package Elite.SpanshTools
```
... or any of the other ways you can get a NuGet package.

## What is this?
This is a set of tools and models for working programmatically in .NET with [Spansh data dumps](https://www.spansh.co.uk/dumps) for Elite Dangerous.  Specifically, it's for parsing files into an object model that can then be used in your tools, or just dumped to some format you feel like using to examine it.  The sky's the limit.

## What's *in* this?
This package contains two major items:

1. An object model representing the JSON format of the data dumps.  The top-level object is `StarSystem`.  Where possible, the model provides empty collections when a property doesn't exist for the system (e.g., a body has no stations) rather than null values, though there are plenty of nullable strings around.

2. A class called `GalaxyParser` (along with `IGalaxyParser` for dependency injection purposes) that parses a data dump as a stream.  That is, it parses one line, does a `yield return`, and goes to the next line.  See below for the method signatures.

You can also find some benchmarks in a separate project if you want to look at the code.  To run the benchmark, you can just build it and run it at the command line (I recommend running it outside of the Visual Studio debugger).  Use these commands, beginning at the source code root:

``` bash
$ cd benchmarks
$ dotnet build -C Release
$ bin/Release/net8.0
$ ./Elite.SpanshTools.Benchmark.exe
```

Finally, there's also a model verifier that I use to ensure everything parses correctly into the model.  Moving forward, I intend on running it once a month on Spansh's entire galaxy dump.  Note that it only runs on Debug builds.

## Usage
Parsing a dump into a model is really easy:

``` csharp
string fileName = "galaxy.json";    // Downloaded from Spansh
await foreach (var system in parser.ParseFileAsync(fileName))
{
	// Do something with the system here
}
```

That's it!  In most cases, the library will take care of opening and disposing the file stream for you.  (With one exception; see Caveats, below.)

##  `IGalaxyParser` Methods
``` csharp
/// <summary>
/// Parses a JSON file into a series of StarSystem objects in an enumerated manner.
/// </summary>
/// <param name="filename">The name of the file to be parsed.</param>
/// <returns>A StarSystem object, via enumeration.</returns>
/// <remarks>It is assumed that the file contains an array of JSON objects.</remarks>
IAsyncEnumerable<StarSystem?> ParseFileAsync(string filename);

/// <summary>
/// Parses a JSON string into a series of StarSystem objects in an enumerated manner.
/// </summary>
/// <param name="inputString">The input string, assumed to contain an array.</param>
/// <returns>A StarSystem object, via enumeration.</returns>
/// <remarks>It is assumed that the file contains an array of JSON objects.</remarks>
IAsyncEnumerable<StarSystem?> ParseStringAsync(string inputString);

/// <summary>
/// Parses a JSON string into a series of StarSystem objects in an enumerated manner.
/// </summary>
/// <param name="inputSteam">The input stream, assumed to contain an array. The caller
/// is responsible for the disposal of the stream</param>
/// <returns>A StarSystem object, via enumeration.</returns>
/// <remarks>It is assumed that the stream contains an array of JSON objects.  In addition, 
/// the caller is responsible for the disposal of the stream.  The stream must be readable and 
/// seekable (<see cref="Stream.CanSeek"/>); otherwise an <see cref="InvalidOperationException"/> is thrown.</remarks>
IAsyncEnumerable<StarSystem?> ParseStreamAsync(Stream inputSteam);
```

## Parallel parsing (`IParallelGalaxyParser`)
`GalaxyParser` also implements `IParallelGalaxyParser`, which parses on every core.  On a full galaxy dump this was about 8.4 times faster than `ParseFileAsync`, at the cost of much more memory (see Performance, below).

``` csharp
var parser = new GalaxyParser();

// Callback per system.  By default it is called from several threads at once, so it must be thread-safe.
long count = 0;
await parser.ParseFileParallelAsync("galaxy.json", system => Interlocked.Increment(ref count));

// Or batches, consumed on one thread while parsing continues on the others.
await foreach (var batch in parser.ParseFileInBatchesAsync("galaxy.json"))
{
	foreach (var system in batch)
	{
		// Do something with the system here
	}
}
```

| Method | Input | Delivery |
| :--- | :--- | :--- |
| `ParseFileParallelAsync` | File path (plain or gzip) | Callback per system |
| `ParseFileInBatchesAsync` | File path (plain or gzip) | `IAsyncEnumerable` of batches |
| `ParseStreamParallelAsync` | Any readable stream; need not be seekable | Callback per system |
| `ParseStreamInBatchesAsync` | Any readable stream; need not be seekable | `IAsyncEnumerable` of batches |

`ParallelParseOptions` (all optional):

| Option | Default | Meaning |
| :--- | :--- | :--- |
| `MaxDegreeOfParallelism` | `Environment.ProcessorCount` | Chunks parsed at once. |
| `ChunkSize` | 8 MiB | Bytes per unit of work.  Lines are never split, however long. |
| `PreserveOrder` | `false` | Deliver in file order.  Callbacks then run one at a time. |
| `MaxBufferedBatches` | 2 x `MaxDegreeOfParallelism` | Batches waiting for a slow consumer before parsing pauses; bounds memory. |
| `OnElementError` | `null` | Called with an `ElementParseError` (exception, byte offset, element text) when an element can't be parsed.  Return `true` to skip it and keep going, `false` to stop.  `null` stops at the first bad element. |

Things to know:

1. **Layout.** The speed-up applies to input laid out like the Spansh dumps: `[` on the first line, one element per line, `]` at the end.  Any other layout (minified, pretty-printed) is detected automatically and parsed sequentially, with the same results.  On the fast path each element is validated, but the commas between elements are not.
2. **Garbage collection.** Parsing allocates heavily.  For full speed, the *application* should use server GC with concurrent GC off; in testing this made parallel parsing over five times faster.  A library can't set this for you.  In the application's project file:
	``` xml
	<ServerGarbageCollection>true</ServerGarbageCollection>
	<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>
	```
3. **Compressed dumps.** Gzip input (such as `galaxy.json.gz` straight from Spansh) is detected by its content and decompressed on the fly, so you can skip unzipping it.  Decompression is single-threaded, so it, not parsing, sets the pace for compressed input.
4. **Errors.** By default the first error stops parsing and is rethrown: a `JsonException` naming the byte offset of the bad line, or whatever your callback threw.  With `OnElementError` you can skip bad elements instead (it runs on worker threads, so it must be thread-safe).  Skipping only works on the fast path; for other layouts a bad element always stops parsing.
5. **Streams.** As with `ParseStreamAsync`, the caller disposes the stream.  Unlike it, the stream does not need to be seekable.

### Custom serializer options
`new GalaxyParser(JsonSerializerOptions)` uses a copy of your options for every parse method, sequential and parallel.  Property names are always matched case-insensitively, because the Spansh data mixes casing.  For example, this reports properties in the data that the model doesn't know about (normally they are ignored), and collects them instead of stopping at the first:

``` csharp
var strict = new GalaxyParser(new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
var errors = new ConcurrentQueue<ElementParseError>();
await strict.ParseFileParallelAsync("galaxy.json", system => { }, new ParallelParseOptions
{
	OnElementError = error => { errors.Enqueue(error); return true; }
});
```

## Performance
But how does it perform?  Well, in addition to your machine, performance is dependent on the length of the individual records.  A system like Sol (that has a JSON record 1,760,997 characters long) or Achenar (869,049 characters) will take longer to parse than Hypi Flee AA-A h0 (157 characters).  Likewise, the galaxy files that contain details about systems, bodies, stations, markets, and commodities will take longer than the system files that contain top-level system information only.

That said, here are some numbers I found during my testing.

| File | File Size (unzipped) | Records Found | Time to Parse (mm:ss) | Records/Sec |
| :--- | -------------------: | ------------: | --------------------: | ----------: |
| galaxy.json | 481 GB | 161,064,963 | 41:26.76 | 64,769.10 |
| galaxy.json, sequential (Nov 2026) | 621 GB | 203,737,392 | 52:39.85 | 64,477 |
| galaxy.json, parallel (Nov 2026) | 621 GB | 203,737,392 | 06:14.25 | 544,381 |
| galaxy_1month.json | 21 GB | 2,968,209 | 01:31.44 | 32,461.46 |
| systems_neutron.json | 700 MB | 4,216,759 | 00:03.16 | 1,335,504.19 |
| systems_1month.json | 646 MB | 3,788,924 | 00:03.82 | 992,889.16 |

The two Nov 2026 rows are the same file on the same machine (an i7-14700K with 28 threads and an NVMe SSD), back to back on an otherwise idle system.  The sequential row used `ParseFileAsync` with the default GC; its peak memory was under 0.1 GB.  The parallel row used `ParseFileParallelAsync` with default options and server GC; its peak memory was 7.4 GB.  That is about 8.4x faster for about 90x the memory.  The parallel path is CPU-bound here (1.66 GB/s, against about 3 GB/s for the disk), so more cores help; lower `MaxDegreeOfParallelism` or `MaxBufferedBatches` to trade speed for memory.  On a 2 GB sample of the file, the parallel path took 13.2 s without server GC and 2.3 s with it.

## Caveats
There a few things to be aware of here:

1. Of the three methods available on `GalaxyParser`, only `ParseFromStream()` will not dispose the stream for you.  The caller is responsible for handling that.  The other two will create and tear down streams based on their input.

2. The format of the dumps is an array of objects (see below), comma-separated.  Therefore, it is assumed that the format of any input is the same.
``` json
[
    { "id64":1234567890987, "name":"system1", ... },
    { "id64":8675309521474, "name":"system2", ... },
    { "id64":2516514354896, "name":"system3", ... }
]
```

## Future Plans
:arrow_forward:= In Progress
:white_check_mark:= Complete
:black_square_button:= Not started
:grey_question:= Idea (needs investigation)

* :white_check_mark: 1.0.0 Release
* :white_check_mark: Set up GitHub project management
  * :white_check_mark: Work item board
  * :white_check_mark: GitHub build actions
* :white_check_mark: Unit tests
* :white_check_mark: XML documentation for the actual model
* :grey_question: Performance improvement investigation
* :grey_question: Any community requests that come through :)

## Changelist
:exclamation: indicates a potential breaking change.

### 1.3.0
* Added Intellisense descriptions to models and properties.
  * These come from the schema published by Spansh, with added annotations if a list/dictionary defaults to empty or the property is required.
* Ran the model against a full galaxy dump from 05 April 2026
  * No changes were necessary.

### 1.2.0
* Model changes to get to parity with the Galaxy dump from 05 March 2026
  * Factions now have `ActiveStates`, `PendingStates`, and `RecoveringStates`.
  * Removed `ControllingFaction` class and replaced it with the `Faction` class.
    * No change to the `System.ControllingFaction` properties; these classes were basically duplicated anyway.
* Some minor improvements to the model verifier
  * Now outputs errors and lines to disk; see console output for details.
    * **NOTE**: This file can get large, so be careful.
  * No longer requires a Debug build to run.  In fact, runs slightly faster with a Release build.
  * Minor output logging improvements.
  
### 1.1.1
* `Belt` was missing `Signals`; this has been added.
  * It has the same properties and whatnot as the one on `Body`.
* Added `ThargoidWarState` to `System`, as it was missing.
* Modifications to the model verifier to return all errors instead of exiting at the first one.
  * Also includes an elapsed time message.

### 1.1.0
* Added `ModelVerifier` project to ensure the model gets properly parsed from JSON.
* Added various properties after running the verifier and discovering they were missing.
* :exclamation: Fixed `RingSignals.SignalsInternal` since it wasn't properly being parsed.
  * Changed the data type to `Dictionary<string, int>?`
  * Renamed it to `SignalsMap`
  * *Note: This is technically a breaking change, but it was always parsing incorrectly into default values, so I highly doubt anyone will actually run into a problem here.*
* Some demo cleanup

### 1.0.1
* Minor fixes to non-required properties

### 1.0.0
* Initial release

## Thanks and Final Notes
Hopefully the community will find this useful.  I certainly did for some tooling that I'm writing for other projects.  And it's been a fun exercise in managing huge amounts of data, which I don't get to do often.

Thanks to Spansh for all the work he does both in providing these dumps to the Elite community and creating phenomenally useful [tools](https://www.spansh.co.uk/plotter) for CMDRs.  I've used them for most of my exploration missions, and they're invaluable.

If you have any questions, comments, or whatever, feel free to contact [Merovech](https://github.com/Merovech) on GitHub.  If you find any bugs, feel free to open an issue here!
