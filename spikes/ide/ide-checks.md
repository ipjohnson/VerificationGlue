# IDE checks for `docs/research/ide-report.md`

At the end you have a filled results block for Rider and one for VS Code, which go into the IDE results tables of the report. Each IDE takes about 20 minutes.

Each step says what to do and what a pass looks like. When a step does not match its pass, write what you saw next to its ID in the results block.

All line numbers refer to `src/UserTests.cs`. Lines 21 to 25 look like this:

```csharp
21        var total = 40;
22        CheckTotal(total);
23        total += 2;
24        Verify(total == 42);
25    }
```

## 0. Build once

From this directory, `spikes/ide`:

```sh
dotnet --version              # must print 10.0.403
dotnet build IdeSpike.slnx    # must end with "Build succeeded"
```

## Rider

### R0. Set up

1. Write down the version from **Rider > About JetBrains Rider**. The pinned one is 2026.2.0.2.
2. Open **Rider > Settings > Build, Execution, Deployment > Unit Testing > Testing Platform**. Tick **Enable Testing Platform support** and click **Save**. Rider turns it off by default. Without it, Rider explores the projects with the MSTest, NUnit, VSTest and xUnit providers only, and the Unit Tests window stays empty. `IdeSpike.slnx.DotSettings` turns it on for this solution too, so the box may already be ticked.
3. Rider's Testing Platform provider also skips a project that does not reference `Microsoft.Testing.Platform.MSBuild`. Both spike projects reference it. If the Unit Tests window is still empty after a build, use **File > Invalidate Caches** with **Restart**, or close and reopen the solution.
4. Open **Settings > Build, Execution, Deployment > Debugger**. Make sure **Enable external source debug** is off and **Just My Code** is on (in some versions this is "Debug only user code").
5. **File > Open**, pick `spikes/ide/IdeSpike.slnx`, and wait until the solution finishes loading.
6. Open the **Unit Tests** window (**View > Tool Windows > Unit Tests**) and click **Build** or **Refresh** at the top of it.

### R1 to R6. Discovery, run and navigation

| ID | Do | Pass |
|---|---|---|
| R1 | In the Unit Tests window, expand the `Mtp2` project. | `HelperCall`, `FileUsings` and `MockCalls` appear, under `MyApp.Tests` > `OrderTests` or as a flat list. |
| R2 | Right-click `MockCalls` under `Mtp2` and choose **Run**. | Only `MockCalls` gets a result, and it is green. The other two show no new result. |
| R3 | Double-click `FileUsings` under `Mtp2`. | `src/UserTests.cs` opens with the caret at line 35, `public void FileUsings()`, or within one line of it. |
| R4 | Repeat R1 under the `Mtp3` project. | Same as R1. |
| R5 | Repeat R2 under `Mtp3`. | Same as R2. |
| R6 | Repeat R3 under `Mtp3`. | Same as R3. |

### R7 to R14. Debugging, `Mtp3` only

Before each check, clear all breakpoints with **Run > View Breakpoints**, select all, and delete them. A debug session starts by right-clicking the test under `Mtp3` in the Unit Tests window and choosing **Debug**.

| ID | Do | Pass |
|---|---|---|
| R7 | Set a breakpoint on line 22. Debug `HelperCall`. | The breakpoint is shown as bound (a filled red dot with no warning sign), and the debugger stops on line 22 of `src/UserTests.cs`. |
| R8 | While stopped on line 22 from R7, press **Step Into** (F7). | The editor shows line 30, `var doubled = total * 2;`, in `src/UserTests.cs`. |
| R9 | Stop the session. Set a breakpoint on line 31 only. Debug `HelperCall`. When it stops on 31, press **Step Out** (Shift+F8). | It stops on line 31, and Step Out lands on line 23, `total += 2;`. Line 22 is also acceptable. |
| R10 | Stop the session. Set breakpoints on lines 37 and 39. Debug `FileUsings`. When it stops on 37, press **Step Over** (F8) five times. | It stops on 37. The five steps show lines 38, 39, 40, 41 and 42 in that order. No step stays on the same line twice. |
| R11 | Stop the session. Set a breakpoint on line 49. Debug `MockCalls`. When it stops, press **Step Over** once. | It stops on 49, and one Step Over goes to line 50. |
| R12 | Look back over R7 to R11. | No step opened `UserTests.Verified.g.cs`, `ICalc.Mock.g.cs` or `Registration.g.cs`. |
| R13 | Debug `HelperCall` with a breakpoint on line 31 again. Look at the **Frames** list. | The top frame is `CheckTotal__Verified` at `UserTests.cs:31`, and the next one is `HelperCall__Verified` at `UserTests.cs:22`. |

## VS Code

### V0. Set up

1. Write down the version from **Code > About Visual Studio Code**. The pinned one is 1.144.0.
2. Open the Extensions view (Shift+Cmd+X). Install **C# Dev Kit** by Microsoft, which also installs **C#**. Write down both versions from each extension's page. The pinned ones are C# Dev Kit 11.11.98 and C# 11.9.41.
3. Open **Settings** (Cmd+,), search for `useTestingPlatformProtocol`, and tick **Dotnet > Test Window: Use Testing Platform Protocol**. Reload the window when asked.
4. **File > Open Folder**, pick `spikes/ide`. If C# Dev Kit asks which solution to open, pick `IdeSpike.slnx`. Wait until the Solution Explorer shows `Mtp2` and `Mtp3`.
5. Open the **Testing** view (the flask icon). Click **Refresh Tests** at the top if the tree is empty.

### V1 to V6. Discovery, run and navigation

| ID | Do | Pass |
|---|---|---|
| V1 | In the Testing view, expand `Mtp2`. | `HelperCall`, `FileUsings` and `MockCalls` appear. |
| V2 | Hover `MockCalls` under `Mtp2` and click its **Run Test** (triangle) icon. | Only `MockCalls` gets a green tick. The other two keep their old state. |
| V3 | Hover `FileUsings` under `Mtp2` and click **Go to Test** (the file icon), or right-click and choose it. | `src/UserTests.cs` opens at line 35 or within one line of it. |
| V4 | Repeat V1 under `Mtp3`. | Same as V1. |
| V5 | Repeat V2 under `Mtp3`. | Same as V2. |
| V6 | Repeat V3 under `Mtp3`. | Same as V3. |

### V7 to V14. Debugging, `Mtp3` only

Before each check, remove all breakpoints with **Run > Remove All Breakpoints**. A debug session starts with the **Debug Test** icon (the triangle with a bug) next to the test under `Mtp3` in the Testing view. Step Into is F11, Step Over is F10, Step Out is Shift+F11, and Continue is F5.

| ID | Do | Pass |
|---|---|---|
| V7 | Set a breakpoint on line 22. Debug `HelperCall`. | The breakpoint is a filled red dot, not hollow or grey, and the debugger stops on line 22 of `src/UserTests.cs`. |
| V8 | While stopped on line 22 from V7, press Step Into. | The editor shows line 30, `var doubled = total * 2;`, in `src/UserTests.cs`. |
| V9 | Stop the session. Set a breakpoint on line 31 only. Debug `HelperCall`. When it stops on 31, press Step Out. | It stops on line 31, and Step Out lands on line 23, `total += 2;`. Line 22 is also acceptable. |
| V10 | Stop the session. Set breakpoints on lines 37 and 39. Debug `FileUsings`. When it stops on 37, press Step Over five times. | It stops on 37. The five steps show lines 38, 39, 40, 41 and 42 in that order. No step stays on the same line twice. |
| V11 | Stop the session. Set a breakpoint on line 49. Debug `MockCalls`. When it stops, press Step Over once. | It stops on 49, and one Step Over goes to line 50. |
| V12 | Look back over V7 to V11. | No step opened `UserTests.Verified.g.cs`, `ICalc.Mock.g.cs` or `Registration.g.cs`. |
| V13 | Debug `HelperCall` with a breakpoint on line 31 again. Look at **Call Stack** in the Run and Debug view. | The top frame is `CheckTotal__Verified` at `UserTests.cs:31`, and the next one is `HelperCall__Verified` at `UserTests.cs:22`. |

## Results block

Copy this, replace each `?` with `pass` or a short description of what you saw, and send it back.

```text
Rider version: ?
R1 ?  R2 ?  R3 ?  R4 ?  R5 ?  R6 ?
R7 ?  R8 ?  R9 ?  R10 ?  R11 ?  R12 ?  R13 ?

VS Code version: ?   C# Dev Kit: ?   C#: ?
V1 ?  V2 ?  V3 ?  V4 ?  V5 ?  V6 ?
V7 ?  V8 ?  V9 ?  V10 ?  V11 ?  V12 ?  V13 ?
```
