# IDE debugging and discovery on the pinned stack

Research for VerificationGlue Phase 0 (`verification-glue.md` and `tests.md`). Date: 2026-11-16. The spike is committed under `spikes/ide`.

## Result

The test host side passes on MTP 2.5.3 and on the MTP 3 preview. The same generated framework discovers 3 tests, runs a single test by UID, reports file and line for navigation in server mode, and publishes with Native AOT with no trim or AOT warnings. The `__Verified` copies map every visible sequence point to the original statement in `src/UserTests.cs`, so a debugger reads the original file and lines from the PDB.

The breakpoint, stepping and navigation checks inside Visual Studio, Rider and VS Code are recorded in [IDE results](#ide-results).

Tier 3 stays opt-in. The cold-build budget has no user approval yet, and missing approval is not a pass.

Each claim below is marked **(verified)** when a run on the pinned stack showed it, or **(inference)** when it comes from reading code or documentation.

## Pinned stack

| Component | Version | Where it is pinned |
|---|---|---|
| .NET SDK | 10.0.403, osx-arm64 | `spikes/ide/global.json`, with `rollForward` set to `disable` |
| Microsoft.Testing.Platform | 2.5.3 | `spikes/ide/Mtp2/Mtp2.csproj` |
| Microsoft.Testing.Platform | 3.0.0-preview.26566.9 | `spikes/ide/Mtp3/Mtp3.csproj`, from the dnceng `test-tools` feed in `spikes/ide/NuGet.config`. nuget.org has no 3.x build. |
| JetBrains Rider | 2026.2.0.2 (macOS) | Installed on the spike machine |
| Visual Studio Code | 1.144.0 (macOS) | Installed on the spike machine |
| C# Dev Kit for VS Code | 11.11.98 | Current marketplace version on 2026-11-16 |
| C# extension for VS Code | 11.9.41 | Current marketplace version on 2026-11-16 |
| Visual Studio | Not pinned yet | Windows only. The spike machine runs macOS. |

The IDE rows record the version that ran each check. A result taken on another version replaces the row's version.

## Fallbacks

These were written before any IDE check ran. They say what the docs promise for an IDE when a check fails there. The IDE setup in `docs/guide/getting-started-tests.md`, which `tests.md` Phase 1 adds, repeats the fallback for every IDE that fails.

| IDE | Discovery or run fails | Navigation fails | Breakpoints or stepping in a `__Verified` copy fail |
|---|---|---|---|
| Visual Studio | The docs support that version through `dotnet test` and the test executable only. Test Explorer is listed as unsupported for it. | The docs say that Test Explorer cannot open the test source in that version. Discovery and run stay supported. | The setup page names Visual Studio. It documents the MSBuild property that switches tier 3 as the way to debug there. Tier 3 stays opt-in. |
| Rider | The docs support that version through `dotnet test`, the test executable, and a run configuration that starts the test executable. The unit test window is listed as unsupported. | The docs say that the unit test window cannot open the test source in that version. | The setup page names Rider and documents the same property. Tier 3 stays opt-in. |
| VS Code with C# Dev Kit | The docs support `dotnet test` in the terminal and a `launch.json` that starts the test executable. The Testing view is listed as unsupported. | The docs say that the Testing view cannot open the test source in that version. | The setup page names VS Code and documents the same property. Tier 3 stays opt-in. |

`verification-glue.md` does not name the property that switches tier 3 yet. The setup page uses the name that Phase 2 gives it.

Hot Reload has its own row in the results. A Hot Reload failure is documented as a limitation and does not block the default.

## The spike

`spikes/ide` builds one source set against both MTP versions.

| File | What it stands for |
|---|---|
| `src/UserTests.cs` | What the user writes. |
| `src/Generated/UserTests.Verified.g.cs` | Hand-written tier 3 output. It copies the file's `using` directives, emits a `{Name}__Verified` copy per method, and maps each copied statement to its original line with a `#line` span. |
| `src/Generated/Registration.g.cs` | Hand-written test generator output. The runner calls each `__Verified` copy directly. |
| `src/Generated/ICalc.Mock.g.cs` | Hand-written mocks generator output with the typed `Calls()` surface. |
| `src/Core.cs` | A stand-in runtime and MTP adapter. |
| `PdbCheck` | Prints the sequence points and local scopes of every `__Verified` method. |
| `tools/server_probe.py` | Drives the test host over the MTP server protocol, as Visual Studio and C# Dev Kit do. |

The rewrite covers the three cases that `verification-glue.md` Phase 0 lists:

| Case | Test | Lines in `src/UserTests.cs` |
|---|---|---|
| A `[Verify]` helper called from a `[VerifyTest]` body | `HelperCall` calls `CheckTotal`. The copy calls `CheckTotal__Verified`. | 19 to 25 and 28 to 32 |
| A body that depends on the file's `using` directives | `FileUsings` uses `StringBuilder`, `CultureInfo` and the alias `Money`. | 35 to 42 |
| A line that uses the typed mock surface | `MockCalls` runs `Verify(calc.Calls().Add(1, 2) == 1)`. | 45 to 51 |

### How the copy maps to the original

Each copied statement gets a `#line (line, column) - (line, column) column "file"` span. The capture locals of a `Verify` sit in a block whose braces are under `#line hidden`. The `if` and the `throw` of a `Verify` each get the span of the original `Verify` statement.

```csharp
#line hidden
        {
            int __v0;
#line (24, 9) - (24, 28) 9 "../UserTests.cs"
        if (!((__v0 = total) == 42))
#line (24, 9) - (24, 28) 9 "../UserTests.cs"
        throw global::VerificationGlue.VerifyFailure.Create("total == 42", "UserTests.cs", 24).With("total", __v0).Build();
#line hidden
        }
```

Findings from `PdbCheck` on the MTP 3 Debug build **(verified)**:

- Every visible sequence point in the four copies is in `spikes/ide/src/UserTests.cs`. The relative path in `#line` resolves against the directory of the generated file.
- Each point covers the original statement exactly. `HelperCall__Verified` has points at 20:5-20:6, 21:9-21:24, 22:9-22:27, 23:9-23:20, 24:9-24:29 twice (the `if` and the `throw`), and 25:5-25:6. The other copies follow the same pattern.
- The scaffold braces produce hidden points only. Stepping over a passing `Verify` line stops once.
- `__v0` and `__v1` live in a scope that covers only their `Verify` statement. A debugger shows them in its locals window while that line is current.
- The end column in a `#line` span is inclusive. A span written as `(20, 5) - (20, 6)` maps to 20:5-20:7 in the PDB, which is one column longer than the brace.

The first version of the copy put each `Verify` on one line as `{ int __v0; if (...) { throw ...; } }`. `PdbCheck` showed four sequence points on that line, three of them with columns past the end of the original line, such as 24:51-24:166. A debugger would stop on the line twice and highlight text that is not there. The tier 3 generator must emit the form above.

A breakpoint on a line inside a rewritten method has two locations, the original method and its copy. The runner calls only the copy **(inference)**.

## Host results

Each row ran on both MTP versions with the same source and the same commands. `out/` holds the AOT publish of each project.

| Check | MTP 2.5.3 | MTP 3.0.0-preview.26566.9 |
|---|---|---|
| Build, Debug | Passes with no warnings | Passes with no warnings |
| `--list-tests` | 3 tests | 3 tests |
| Full run | 3 of 3 pass | 3 of 3 pass |
| `--filter-uid "MyApp.Tests.OrderTests.MockCalls()"` | 1 test runs and passes | 1 test runs and passes |
| `dotnet test --project ... --filter-uid "MyApp.Tests.OrderTests.FileUsings()"` | 1 test runs and passes | 1 test runs and passes |
| Server mode `testing/discoverTests` | 3 nodes with `location.file`, `location.line-start`, `location.type` and `location.method` | Same |
| Server mode `testing/runTests` with one UID | Only that test runs and passes | Same |
| AOT publish, osx-arm64 | 0 warnings, 10.9 MB | 0 warnings, 10.3 MB |
| AOT `--list-tests`, full run and `--filter-uid` | 3 tests, 3 of 3 pass in 0.05 s wall time, 1 filtered test passes | 3 tests, 3 of 3 pass in 0.07 s wall time, 1 filtered test passes |
| AOT server mode discovery and one-UID run | Same as JIT | Same as JIT |

All rows are **(verified)**. The MTP 3 preview needed no source change from MTP 2.5.3 for the APIs the adapter uses: `ITestFramework`, `TestNodeUpdateMessage`, `TestNodeUidListFilter`, `TestFileLocationProperty` and `TestMethodIdentifierProperty`. The server reports itself as `test-anywhere` with the MTP version.

The MTP 2.5.3 column is historical comparison only, as the north star says. It shows that a failure on the preview comes from the preview and not from the generated framework.

## IDE results

A person runs these checks at each IDE with the procedure below. Each cell holds pass, fail with what happened, or not run.

### Discovery, run and navigation

| Check | Visual Studio | Rider 2026.2.0.2 | VS Code 1.144.0, C# Dev Kit 11.11.98 |
|---|---|---|---|
| MTP 2.5.3: 3 tests appear | not run | not run | not run |
| MTP 2.5.3: run one test from the tree runs only that test | not run | not run | not run |
| MTP 2.5.3: open a test from the tree goes to its method in `src/UserTests.cs` | not run | not run | not run |
| MTP 3 preview: 3 tests appear | not run | not run | not run |
| MTP 3 preview: run one test from the tree runs only that test | not run | not run | not run |
| MTP 3 preview: open a test from the tree goes to its method in `src/UserTests.cs` | not run | not run | not run |

### Debugging the `__Verified` copies, MTP 3 preview

| Check | Visual Studio | Rider 2026.2.0.2 | VS Code 1.144.0, C# Dev Kit 11.11.98 |
|---|---|---|---|
| Helper case: a breakpoint on line 22 binds and is hit when debugging `HelperCall` | not run | not run | not run |
| Helper case: step into line 22 lands on line 30 in `src/UserTests.cs` | not run | not run | not run |
| Helper case: a breakpoint on line 31 is hit, and step out returns to line 23 | not run | not run | not run |
| Using directives case: breakpoints on lines 37 and 39 are hit, and stepping visits 37, 38, 39, 40, 41, 42 in order | not run | not run | not run |
| Mock case: a breakpoint on line 49 is hit, and one step over goes to line 50 | not run | not run | not run |
| No step lands in `UserTests.Verified.g.cs`, `ICalc.Mock.g.cs` or `Registration.g.cs` with Just My Code on | not run | not run | not run |
| The call stack shows the `__Verified` frame at the original file and line | not run | not run | not run |
| Hot Reload: edit line 30 to `total * 3` while stopped and continue | not run | not run | not run |

### Procedure

1. Install the pinned SDK, then build both projects: `dotnet build spikes/ide/Mtp2` and `dotnet build spikes/ide/Mtp3`.
2. Open `spikes/ide/Mtp3/Mtp3.csproj` in the IDE. Turn on MTP support where the IDE needs it. In Visual Studio this is the "Use testing platform server mode" preview feature. In Rider it is "Enable Testing Platform support" under Unit Testing settings. In VS Code it is `dotnet.testWindow.useTestingPlatformProtocol`.
3. Fill the discovery, run and navigation rows for MTP 3. Repeat them with `spikes/ide/Mtp2/Mtp2.csproj`.
4. Debug each test from the test tree and fill the debugging rows. Line numbers refer to `spikes/ide/src/UserTests.cs`.
5. Write the IDE version and plugin versions in the column header if they differ from the pinned ones.

## Limitations

- Line numbers are part of every `#line` span, so an edit above a method changes its copy. `verification-glue.md` already accounts for this in the cold-build benchmark **(inference)**.
- The capture locals `__v0` and `__v1` show in the locals window while their `Verify` line is current **(verified from the PDB scopes)**. Debuggers hide compiler-generated names such as `<>s__1`, which C# source cannot declare **(inference)**.
- The spike's copies are hand-written. The tier 3 generator in Phase 2 must produce the same sequence points, and its tests should compare `PdbCheck` output.

## How to reproduce

```sh
cd spikes/ide
dotnet build Mtp2 && dotnet build Mtp3
Mtp3/bin/Debug/net10.0/Mtp3 --list-tests
Mtp3/bin/Debug/net10.0/Mtp3 --filter-uid "MyApp.Tests.OrderTests.MockCalls()"
dotnet run --project PdbCheck -- Mtp3/bin/Debug/net10.0/Mtp3.dll
python3 -I tools/server_probe.py Mtp3/bin/Debug/net10.0/Mtp3 "MyApp.Tests.OrderTests.HelperCall()"
dotnet publish Mtp3 -c Release -o out/Mtp3
out/Mtp3/Mtp3
```

Replace `Mtp3` with `Mtp2` for MTP 2.5.3.
