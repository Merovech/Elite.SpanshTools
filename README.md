# Elite.SpanshTools
Tools for working with Spansh data dumps for Elite Dangerous

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation
Elite.SpanshTools is available via NuGet:
ColorizedConsole is available via NuGet.
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

I've tested this on a full galaxy dump (~500gb unzipped) and the one-month dump (~22gb unzipped).  Time results were as follows:

Full Galaxy: 45 minutes  
One-Month: 2 minutes

Memory stayed below 200mb the whole time, though this is observational information, not scientific data.

## What's *in* this?
This package contains two major items:

1. An object model representing the JSON format of the data dumps.  The top-level object is `StarSystem`.  Where possible, the model provides empty collections when a property doesn't exist for the system (e.g., a body has no stations) rather than null values.

2. A class called `GalaxyParser` (along with `IGalaxyParser` for dependency injection purposes) that parses a data dump as a stream.  That is, it parses one line, does a `yield return`, and goes to the next line.  See below for the method signatures.

You can also find some benchmarks in a separate project if you want to look at the code at [the project's GitHub page](https://github.com/Merovech/Elite.SpanshTools).

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

## Caveats
There a few things to be aware of here:

1. This has been tested ONLY on galaxy dumps.  Spansh provides 13 dumps of various sizes; 6 are galaxy dumps and 7 are system dumps.  **I have not tested this on system files.**  That is coming in a future release (see Future Plans, below).  That said, the galaxy files will parse with complete systems.  It's just the 7 data dump files I haven't tested yet.

2. Of the three methods available on `GalaxyParser`, only `ParseFromStream()` will not dispose the stream for you.  The caller is responsible for handling that.  The other two will create and tear down streams based on their input.

3. The format of the dumps is an array of objects (see below), comma-separated.  Therefore, it is assumed that the format of any input is the same.
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
:x:= Incomplete
:grey_question:= Idea (needs investigation)

* :white_check_mark: 1.0.0.0 Release
* :x: Set up GitHub project management
  * :x: Work item board
  * :x: GitHub build actions
* :x: Test against system dumps (e.g., `systems.json.gz`, etc.)
  * :grey_question: If necessary, modify or create a second object model for these
* :grey_question: Performance improvement investigation
* :grey_question: Any community requests that come through :)

## Thanks and Final Notes
Hopefully the community will find this useful.  I certainly did for some tooling that I'm writing for other projects.  And it's been a fun exercise in managing huge amounts of data, which I don't get to do often.

Thanks to Spansh for all the work he does both in providing these dumps for the Elite community and creating phenomenally useful [tools](https://www.spansh.co.uk/plotter) for CMDRs.  I've used them for most of my exploration missions, and they're invaluable.