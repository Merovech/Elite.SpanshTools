# Elite.SpanshTools: project notes

Public repo (github.com/Merovech/Elite.SpanshTools), published as a NuGet package. Work on a branch and reach `main` only via PR.

## Layout
- `src/`: the library. Targets net8.0.
  - `Model/`: the classes the dump deserializes into; `StarSystem` is the root.
  - `Parsers/`: `GalaxyParser`, which implements `IGalaxyParser` (sequential) and `IParallelGalaxyParser` (in `GalaxyParser.Parallel.cs`), plus `ParallelParseOptions` and `ElementParseError`.
  - `Parsers/Internal/`: the parallel pipeline (`LineDelimited`, `FileChunkReader`, `PrefixedStream`, `FirstError`). Visible to the tests through `InternalsVisibleTo`.
- `test/`: MSTest project (parallel at method level). Only `src` and `sample` are unit tested.
- `sample/`: `DemoRunner` demo app (assembly name `ests`). Needs a real console; it calls `Console.Clear`/`ReadKey`.
- `benchmarks/`, `utilities/`: internal dev tools (benchmark, model verifier). No unit tests required.

## Commands
- Run tests: `dotnet test test/Elite.SpanshTools.Tests.csproj`
- Run the demo (real terminal): `dotnet run --project sample -- <spansh-file.json>`
- Model verification (monthly, against the full Spansh dump): `dotnet run -c Release --project utilities/Elite.SpanshTools.Utilities -- --data-location <folder> --data-file <name-in-folder | http(s) URL to a zip>`. It deletes the input file when no errors are found, writes `ModelVerificationErrors.txt` otherwise, and exits 1 on errors.

## Behavior worth knowing
- `ParseStreamAsync` requires a seekable, readable, non-empty stream (`InvalidOperationException` if not seekable). The caller disposes the stream.
- A JSON document that is just `null` yields zero items from all three sequential parse methods.
- Parallel API:
  - The fast path needs the Spansh layout: `[` alone on line 1, one element per line, then `]`. Anything else is detected and falls back to sequential System.Text.Json parsing, which `MaxLineLength` doesn't cover and which never calls `OnElementError`.
  - `MaxLineLength` (default 64 MiB) fails a longer line with `InvalidDataException`; it can't be skipped through `OnElementError`.
  - Gzip input is decompressed one level only.
  - Full speed needs server GC with concurrent GC off, set in the *application's* project file (the sample, benchmarks and verifier do this). The library can't set it.
- `GalaxyParser(JsonSerializerOptions)` copies the options and always forces `PropertyNameCaseInsensitive = true`.
- `DemoRunner.SystemsByFile` throws `NullReferenceException` on a null array entry; `StationsByFile` skips null entries.
- `DemoRunner` is tested through an internal `IConsoleIO` constructor (`FakeConsole` in tests). The public constructor uses the real console.

## Testing conventions
- `Assert.AreEqual(expected, actual)`: expected first.
- Tests that need files use `TempFile` (unique temp path, deleted on dispose); never fixed names in the output folder.
- Classes that swap `Console.In`/`Console.Out` must be `[DoNotParallelize]`.
- Not unit tested (needs production refactor): `HelperMethods.ValidateProgramArgs` failure paths (`Environment.Exit`) and `HelperMethods.LogUpdate` (`SetCursorPosition` throws when redirected).

## Status (2026-11-07)
- PR #31 (`parallel-line-parsing` into `main`) is open. It contains `add-thargoidwar-daysremaining`, which can be deleted once #31 merges.
- Version bumped to 1.4.0 with release notes (csproj and both README changelists). Model verification and a full-dump timing run passed after the security fixes. 299 tests pass.
- Next: merge #31, then publish 1.4.0 to NuGet.
