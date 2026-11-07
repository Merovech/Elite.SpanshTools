# Elite.SpanshTools
Tools for working with Spansh data dumps for Elite Dangerous.  An expanded version of this readme with additional information is on [the GitHub page](https://github.com/Merovech/Elite.SpanshTools).

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

## Parallel parsing
`GalaxyParser` also implements `IParallelGalaxyParser`, which parses on every core and is roughly ten times faster on a full galaxy dump:

``` csharp
// Callback per system, called from several threads at once (pass PreserveOrder = true for file order).
await parser.ParseFileParallelAsync("galaxy.json", system => { /* thread-safe work here */ });

// Or batches, consumed on one thread while parsing continues on the others.
await foreach (var batch in parser.ParseFileInBatchesAsync("galaxy.json"))
{
	foreach (var system in batch) { /* ... */ }
}
```

* Gzip input (such as `galaxy.json.gz` straight from Spansh) is detected and decompressed on the fly.  Stream versions accept non-seekable streams.
* The speed-up applies to the Spansh layout (one element per line).  Other layouts are detected and parsed sequentially with the same results.
* For full speed, set `<ServerGarbageCollection>true</ServerGarbageCollection>` and `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` in your *application's* project file.  Parsing allocates heavily, and in testing this made parallel parsing over five times faster.
* Tune with `ParallelParseOptions` (`MaxDegreeOfParallelism`, `ChunkSize`, `PreserveOrder`, `MaxBufferedBatches`).  The defaults scale with the machine's processor count.

## Performance
**But how does it perform?**
Well, in addition to your machine, performance is dependent on the length of the individual records.  A system like Sol (that has a JSON record 1,760,997 characters long) or Achenar (869,049 characters) will take longer to parse than Hypi Flee AA-A h0 (157 characters).  Likewise, the galaxy files that contain details about systems, bodies, stations, markets, and commodities will take longer than the system files that contain top-level system information only.

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

## Changelist
!! indicates a potential breaking change.

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
* !! Fixed `RingSignals.SignalsInternal` since it wasn't properly being parsed.
  * Changed the data type to `Dictionary<string, int>?`
  * Renamed it to `SignalsMap`
  * *Note: This is technically a breaking change, but it was always parsing incorrectly into default values, so I highly doubt anyone will actually run into a problem here.*
* Some demo cleanup

### 1.0.1
* Minor fixes to non-required properties

### 1.0.0
* Initial release

## Thanks and Final Notes
Thanks to Spansh for all the work he does both in providing these dumps to the Elite community and creating phenomenally useful [tools](https://www.spansh.co.uk/plotter) for CMDRs.  I've used them for most of my exploration missions, and they're invaluable.

If you have any questions, comments, or whatever, feel free to contact [Merovech](https://github.com/Merovech) on GitHub.