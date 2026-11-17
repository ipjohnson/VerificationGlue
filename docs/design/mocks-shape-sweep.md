# Mock shape sweep

Status: Phase 0 of [mocks.md](mocks.md), done. The interface class emitter generates a mock for all 1,452 public interfaces in the BCL and the top 100 NuGet packages. 1,413 compile. 39 are refused with VGM0001, and 0 fail to compile.

## What runs

The `mock-sweep` job in `.github/workflows/ci.yml` runs on each pull request and each push to main. It fails when any generated mock does not compile.

1. `test/MockSweep.Packages` pins the top 100 packages on the [nuget.org statistics page](https://www.nuget.org/stats/packages) of 2026-11-17 at their latest stable versions that day. Building it restores them and writes every compile reference, with the package or framework it came from, to `obj/sweep-references.txt`.
2. `test/MockSweep` reads that file and finds every public interface, nested ones included, in the `Microsoft.NETCore.App` reference assemblies and in each listed package's own assemblies. Generic interfaces are mocked as open definitions, so the mock class repeats their type parameters and constraints.
3. For each source it builds the model with `MockModelBuilder`, writes the class with `MockEmitter`, compiles all the mocks in one compilation, and assigns each error to the mock whose file holds it.
4. It writes `report.md` and `report.json`, and the source of each mock that failed under `failed/`. The job uploads them as the `mock-sweep` artifact.

Run it locally from the repository root:

```sh
dotnet build test/MockSweep.Packages
dotnet run --project test/MockSweep -- --references test/MockSweep.Packages/obj/sweep-references.txt --output artifacts/mock-sweep
```

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
| A type nested in a generic type, whose type arguments come from an extern-aliased assembly | Review of `TypeConversion` | Unsupported. The type is written with `global::`, and the mock fails to compile. No case in the sweep or the shape files has this shape |

## Not covered yet

The recursion depth for recursive mocks is set from this sweep in Phase 1, Preview C, once recursive mocks exist.
