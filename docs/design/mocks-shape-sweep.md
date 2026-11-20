# Mock shape sweep

Status: Phase 0 of [mocks.md](mocks.md), done. The interface class emitter generates a mock for all 1,452 public interfaces in the BCL and the top 100 NuGet packages. 1,413 compile. 39 are refused with VGM0001, and 0 fail to compile.

## What runs

The `mock-sweep` job in `.github/workflows/ci.yml` runs on each pull request and each push to main. It fails when any generated mock does not compile, and when an interface's outcome differs from its [pinned classification](#pinned-classifications).

1. `test/MockSweep.Packages` pins the top 100 packages on the [nuget.org statistics page](https://www.nuget.org/stats/packages) of 2026-11-17 at their latest stable versions that day. Building it restores them and writes every compile reference, with the package or framework it came from, to `obj/sweep-references.txt`.
2. `test/MockSweep` reads that file and finds every public interface, nested ones included, in the `Microsoft.NETCore.App` reference assemblies and in each listed package's own assemblies. Generic interfaces are mocked as open definitions, so the mock class repeats their type parameters and constraints.
3. For each source it builds the model with `MockModelBuilder`, writes the class with `MockEmitter`, compiles all the mocks in one compilation, and assigns each error to the mock whose file holds it.
4. It compares each interface's outcome with [`test/MockSweep/classifications.tsv`](../../test/MockSweep/classifications.tsv).
5. It writes `report.md` and `report.json`, and the source of each mock that failed under `failed/`. The job uploads them as the `mock-sweep` artifact.

Run it locally from the repository root:

```sh
dotnet build test/MockSweep.Packages
dotnet run --project test/MockSweep -- --references test/MockSweep.Packages/obj/sweep-references.txt --output artifacts/mock-sweep --classifications test/MockSweep/classifications.tsv
```

### Pinned classifications

`classifications.tsv` holds one line per swept interface: its status, its source and its name. The status is `compiled`, `compiled with VGM0003`, `refused` or `failed`. A run lists each difference in the report under four headings, and any difference fails the job:

| Heading | Change |
|---|---|
| Regressions | The status moved toward `failed`, such as an interface that compiled and is now refused |
| Other outcome changes | The status moved toward `compiled` |
| Input changes: interfaces not in the pinned set | A new package version or SDK added the interface |
| Input changes: pinned interfaces no longer swept | The interface is gone from its source |

The input changes are listed apart, so a package update that adds interfaces cannot hide a regression among them. After reviewing each change, rewrite the file by adding `--write-classifications` to the command above, and commit the input changes apart from the outcome changes. [`SweepClassificationSpecs`](../../test/VerificationGlue.Mocks.Generator.Specs/SweepClassificationSpecs.cs) checks that an interface that compiled and is now refused fails the run.

The hand-written shape files in `test/VerificationGlue.Mocks.Generator.Specs/Shapes` run with the other specs. Each one declares its expected outcome in a header, as the conformance suite in `mocks.md` describes. `ShapeSpecs` compiles every interface the file declares and the type of every field of its `MockTargets` class, and checks the diagnostic IDs.

## Results

| Source | Interfaces | Compiled | With VGM0003 | Refused, VGM0001 | Failed |
|---|---|---|---|---|---|
| BCL | 291 | 254 | 7 | 37 | 0 |
| 83 packages with assemblies of their own | 1,161 | 1,159 | 4 | 2 | 0 |
| All | 1,452 | 1,413 | 11 | 39 | 0 |

17 of the 100 packages bring no assembly of their own. They are metapackages, analyzers, build-only packages, or packages whose assemblies the shared framework provides, which the BCL row covers. 33 of the other 83 declare no public interface.

### Refused with VGM0001

- 38 interfaces have static abstract members. In the BCL these are 32 generic math interfaces in `System.Numerics`, `IParsable<TSelf>`, `ISpanParsable<TSelf>`, `IUtf8SpanParsable<TSelf>`, and 2 COM marshalling interfaces. The 38th is `Amazon.Runtime.IAmazonService` in AWSSDK.Core.
- `Microsoft.EntityFrameworkCore.Migrations.IMigrationsDatabaseLock` has an abstract member that no other assembly can see, so no other assembly can implement it.

### Members that throw, reported by VGM0003

These counts answer the `ref struct` question in `mocks.md`.

| Reason | Members | Interfaces |
|---|---|---|
| A `ref struct` parameter | 7 | 6 |
| A `ref struct` return value | 1 | 1 |
| A pointer parameter, return value or property | 6 | 4 |

The 8 `ref struct` members are in 7 interfaces: `ISpanFormattable`, `IUtf8SpanFormattable` and `IBufferWriter<T>` in the BCL, two unmarshaller interfaces in AWSSDK.Core, `IBufferMessage` in Google.Protobuf, and `IJsonModel<T>` in System.ClientModel. The pointer members are all in the COM marshalling interfaces of `System.Runtime.InteropServices.Marshalling`.

## Triage

Each problem the sweep or the shape files found is fixed, reported by a named diagnostic, or documented as unsupported.

| Problem | Shown by | Outcome |
|---|---|---|
| A getter-only property got a setter, because `PropertyDefinition` starts with one | Sweep, every mock | Fixed |
| An interface marked `[Obsolete(error: true)]`, or one whose signatures use such a type, does not compile, because a `#pragma` cannot hide an error | Sweep, 4 interfaces | Fixed. The mock class is marked `[Obsolete]`, and C# reports no obsolete use inside an obsolete type. `Obsolete.cs` |
| `T? M<T>() where T : Exception` became `Nullable<T>` in the explicit implementation | `Constraints.cs` | Fixed. A type parameter that is a reference type gets `where T : class` |
| `T?` in `IGeneric<T>` closed over `int` was written `int?` | `Nullable.cs` | Fixed. A value type other than `Nullable<T>` loses its annotation |
| `ValueTuple<int>` was written `(int, int)` by CSharpAuthor | `Nullable.cs` | Fixed. A one-element tuple is written `global::System.ValueTuple<T>` |
| Two assemblies referenced through extern aliases declare the same type | `ExternAlias.cs` | Fixed. The type is written `alias::Namespace.Name` and the file declares the alias |
| A parameter named like a generated local, such as `__args` | `Names.cs` | Fixed. The parameter is renamed in the implementation |
| A property with a setter and no getter | `Names.cs` | Fixed. CSharpAuthor always writes a getter, so the emitter writes this property as text |
| Static abstract members | Sweep, 38 interfaces | VGM0001 |
| An abstract member the test project cannot see | Sweep, 1 interface | VGM0001 |
| Pointer members in a project without `AllowUnsafeBlocks` | `PointersWithoutUnsafe.cs` | VGM0001 |
| `ref struct` and pointer parameters, return values and properties | Sweep, 11 interfaces | VGM0003. The member throws `NotSupportedException` |
| An event whose delegate takes a `ref struct` | `RefStruct.cs` | Unsupported. Adding and removing handlers works, and raising the event throws `NotSupportedException` |
| An open generic interface whose type parameter has `allows ref struct` | `Constraints.cs` | Unsupported for `ref struct` type arguments. The mock class drops the anti-constraint so it can box its arguments. A mock of the interface closed over a `ref struct` reports VGM0003 on each member that uses it |
| A type nested in a generic type, whose type arguments come from an extern-aliased assembly | Review of `TypeConversion` | VGM0001 with its own reason, as `CD-10` of [hardening.md](hardening.md#contract-decisions) decides. The mocked type and any type its members name are checked. `ExternAliasNested.cs` |

## Recursion depth

The sweep compiles each mock with the members that return recursive mocks at the default depth, and all 1,413 compile. It then walks each interface it could mock as if a test named only that interface, and counts the mocks and generated lines the walk adds at each depth. The figures below are from a local run on 2026-11-17.

| Depth | Walk adds a mock | Mocks added | Lines for one interface, 95th percentile | Most lines for one interface |
|---|---|---|---|---|
| 1 | 480 of 1,413 | 1,850 | 6,835 | 14,358 |
| 2 | 480 of 1,413 | 5,463 | 15,287 | 21,570 |
| 3 | 480 of 1,413 | 8,832 | 22,467 | 30,820 |
| 4 | 480 of 1,413 | 11,054 | 27,443 | 39,108 |

The largest walks start in `Microsoft.EntityFrameworkCore.Metadata`. At depth 2, `IConventionEntityType` adds 50 mocks.

The rule: the default is the largest depth at which the walk from any one interface adds fewer lines than a third of the 500-interface compile benchmark in `mocks.md`, which generates 77,528 lines. A third is 25,842 lines. Depth 2 adds at most 21,570 and depth 3 at most 30,820, so `RecursiveMocks.DefaultDepth` is 2.

The line counts include each mock's typed `Setup()` and `Calls()` surface.
