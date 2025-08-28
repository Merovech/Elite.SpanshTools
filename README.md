# Elite.SpanshTools
Tools for working with Spansh data dumps for Elite Dangerous

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

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
/// the caller is responsible for the disposal of the stream.</remarks>
IAsyncEnumerable<StarSystem?> ParseStreamAsync(Stream inputSteam);
```

## Performance
But how does it perform?  Well, in addition to your machine, performance is dependent on the length of the individual records.  A system like Sol (that has a JSON record 1,760,997 characters long) or Achenar (869,049 characters) will take longer to parse than Hypi Flee AA-A h0 (157 characters).  Likewise, the galaxy files that contain details about systems, bodies, stations, markets, and commodities will take longer than the system files that contain top-level system information only.

That said, here are some numbers I found during my testing.

| File | File Size (unzipped) | Records Found | Time to Parse (mm:ss) | Records/Sec |
| :--- | -------------------: | ------------: | --------------------: | ----------: |
| galaxy.json | 481 GB | 161,064,963 | 41:26.76 | 64,769.10 |
| galaxy_1month.json | 21 GB | 2,968,209 | 01:31.44 | 32,461.46 |
| systems_neutron.json | 700 MB | 4,216,759 | 00:03.16 | 1,335,504.19 |
| systems_1month.json | 646 MB | 3,788,924 | 00:03.82 | 992,889.16 |

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

* :white_check_mark: 1.0.0.0 Release
* :white_square_button: Set up GitHub project management
  * :white_square_button: Work item board
  * :white_square_button: GitHub build actions
* :white_square_button: XML documentation for the actual model
* :white_square_button: Unit tests
* :grey_question: Performance improvement investigation
* :grey_question: Any community requests that come through :)

## Thanks and Final Notes
Hopefully the community will find this useful.  I certainly did for some tooling that I'm writing for other projects.  And it's been a fun exercise in managing huge amounts of data, which I don't get to do often.

Thanks to Spansh for all the work he does both in providing these dumps to the Elite community and creating phenomenally useful [tools](https://www.spansh.co.uk/plotter) for CMDRs.  I've used them for most of my exploration missions, and they're invaluable.

If you have any questions, comments, or whatever, feel free to contact [Merovech](https://github.com/Merovech) on GitHub.  If you find any bugs, feel free to open an issue here!