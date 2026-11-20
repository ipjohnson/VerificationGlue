# VerificationGlue hardening exercise

Users should be able to combine assertions, mocks, and test setup without changing normal C# evaluation, leaking state between tests, or losing failures during cleanup. This exercise establishes executable evidence for those guarantees across the supported surface of VerificationGlue.

Status: step 1 done, and the user approved its gate on 2026-11-17. Design review drv-60ynneww accepted 11 of its 12 decisions, and this text applies them. The coverage inventory exists at its baseline commit, and the [step 1 gate summary](#step-1-gate-summary) lists what the gate decides. Step 2 is done: the [generated expression harness](#generated-expression-harness) runs in the `hardening` CI job, and no inventory entry has a step 2 gap. Step 3 has started: the [mock sequence harness](#the-mock-sequence-harness-as-built) runs in the same job. The other campaigns and the completion gates described here are proposed work.

Testing every possible C# program is not feasible. The goal is to define the supported contracts, exercise their boundaries and interactions, and retain reproducible evidence. A large test count or high line coverage alone does not establish that coverage.

## Related designs

The component designs stay the source of each component's contract:

- [Verify syntax and generator](verification-glue.md)
- [Mocks](mocks.md)
- [Tests](tests.md)

Each design keeps ownership of its open checklist items. The exercise records evidence against those items but does not move or close them. Two inputs from the mocks design feed step 3: the [issue mining](mocks.md#phase-2-comparison-and-mining) pass for each library, and the scenarios ported from the Rocks, Mockolate and Imposter suites listed under [conformance sources](mocks.md#sources).

The exercise does not block the open feature issues [#35](https://github.com/ipjohnson/VerificationGlue/issues/35) to [#49](https://github.com/ipjohnson/VerificationGlue/issues/49). A feature merged after the baseline commit adds its own inventory entries in its pull request.

## Existing evidence

| Area | Existing foundation | What it contributes |
|---|---|---|
| Assertions | [RewriteSpecs](../../test/VerificationGlue.Generator.Specs/RewriteSpecs.cs) | Cases for evaluation order, mutation, short-circuiting, async expressions, messages, and rewriting boundaries |
| Verify syntax | [VerificationGlue.Specs](../../test/VerificationGlue.Specs) | Runtime behavior of `Verify`, scopes, and package versions |
| Generator contract | [Contract.Specs](../../test/Contract.Specs) | The will-rewrite contract between the assertion generator and other generators |
| Mock behavior | [Behavior specs](../../test/VerificationGlue.Mocks.Specs/Behaviour) | Setup, matchers, callbacks, events, recursive mocks, verification, and async context isolation |
| Mock compatibility | [Comparison specs](../../test/NSubstituteComparison.Specs/ComparisonSpecs.cs) | Comparisons with NSubstitute that require intentional differences to be recorded |
| Mock shapes | [Shape sweep](mocks-shape-sweep.md) and [shape specs](../../test/VerificationGlue.Mocks.Generator.Specs/ShapeSpecs.cs) | Compilation of real package interfaces and selected language shapes |
| Test generator | [VerificationGlue.Tests.Generator.Specs](../../test/VerificationGlue.Tests.Generator.Specs) | Generated test code, parameter resolution, analyzers, tier 3 rewriting, and version mismatch diagnostics |
| Generator snapshots | [Generator.Harness.Specs](../../test/Generator.Harness.Specs) | The snapshot harness that the generator specs use to describe and compare output |
| Runner and lifecycle | [Runner specs](../../test/VerificationGlue.Tests.Specs/RunnerSpecs.cs) and [lifecycle specs](../../test/VerificationGlue.Tests.Specs/LifecycleSpecs.cs) | Process-level tests of registration, execution, scopes, cleanup, and reporting |
| Documentation | [Docs.Specs](../../test/Docs.Specs) | Checks that diagnostic and message pages match the code |
| Samples | [samples](../../samples) | Framework, container, migration, AOT, and packed consumer projects built in CI |
| Consumer environments | [CI](../../.github/workflows/ci.yml) and [package consumption script](../../eng/pack-and-consume.sh) | Framework samples, packed consumers, Native AOT, and performance checks |

The shape sweep report dated 2026-11-17 records 1,452 interfaces: 1,413 compile and 39 are refused. Some compiled interfaces contain members with explicitly unsupported behavior. These figures describe the recorded corpus, not a guarantee that every operation on every mock works.

The [sweep compiler](../../test/MockSweep/MockCompiler.cs) calls the model builder and emitter directly. Consumer tests must also exercise discovery through the public generator, mock registration, setup, invocation, and verification.

## Coverage inventory

Create one inventory that maps contracts to executable evidence. Reuse existing tests before adding cases. A directory or test name is a starting point for review, not proof that every dimension is covered.

The inventory is the checked-in file [`hardening-inventory.json`](hardening-inventory.json). It is built at a named baseline commit, which its `baseline` field records. CI does not write to it. Each CI run writes a separate run report, and reports join to the inventory by Scenario ID.

Each inventory entry needs these fields:

| Field | JSON | Purpose |
|---|---|---|
| Scenario ID | `id` | Stable reference for reports and regression cases, of the form `AREA-000` with an area from `areas` |
| User-visible contract | `contract`, `source` | Expected result, failure, diagnostic, or fallback, and the guide or design that states it |
| Support status | `support` | `supported`, `unsupported` for a deliberate boundary, or `unclassified` |
| Expected outcome | `outcome` | For an unsupported entry, the refusal, diagnostic, throwing member, fallback or failure that the boundary gives |
| Contract decision | `decision`, and `decisions` at the top | For an unresolved observable behavior, the decision needed, its proposed owner, and whether it is approved |
| Dimensions | `dimensions` | Expression, member shape, setup style, scope, framework, and other applicable factors |
| Configuration constraints | `constraints` | Combinations that are valid, with reasons for exclusions |
| Evidence | `evidence` | Exact test methods as `path#Type.Method`, and consumer projects, scripts or case files as paths |
| Gap | `gap` | Missing evidence, with its size and the step that fills it |
| Follow-up | `followUp` | Defect or decision needed, with its issue |

Verification status and run details live in the run reports, not in the inventory. A run report records, for each Scenario ID, the status (not yet verified, passed, failed, or blocked), the commit, the configuration, the environment, the seed where applicable, and the report location.

CI validates the inventory on each pull request. [`test/Inventory.Specs`](../../test/Inventory.Specs) holds the checks and runs in the `Run specs` step of the build job:

- Every Scenario ID is unique and names a declared area, and every decision names an existing ID. Report entries will join the same check when step 5 adds run reports.
- Every evidence reference names a test method with a test attribute, a project, or a file that exists.
- Every source names an existing file, and its heading when it names one.
- An unsupported entry names its expected outcome.
- While `allowGaps` is true, an entry may record an explicit gap in place of evidence. Each gap gives a size and a step.
- The gap list and the contract decisions in this doc match the inventory.
- At completion, step 5 sets `allowGaps` to false, and a supported entry with a gap fails the check.
- Verification status and run details are not inventory fields, so an entry that carries them fails to load.

A report separates results at the current commit and configuration from historical passes. A pass at an earlier commit or in another configuration does not count as current evidence.

Keep support status separate from verification status. An unsupported operation can pass its test by producing the promised diagnostic. A supported operation without evidence remains unverified.

An observable behavior that no design or guide settles needs approval from the user or a named contract owner before its cases count as passing. Until then its cases report as unverified. The inventory tracks each such decision and its owner in the contract decision field.

Review unsupported boundaries from the [mock shape report](mocks-shape-sweep.md), [rewrite tiers](../guide/tiers.md), and diagnostic pages. These include static abstract members, inaccessible members, and some pointer and `ref struct` signatures. Define the expected refusal, throwing member, or fallback for each boundary. Do not silently count it as a supported case.

## Assertion conformance

An assertion must preserve the evaluated expression's result, side effects, and exception behavior. Richer failure messages must not re-run an expression or evaluate a branch that C# would skip.

### Expression and placement matrix

| Dimension | Cases |
|---|---|
| Expression shape | Comparisons, arithmetic, boolean operators, conditional expressions, coalescing, null access, patterns, casts, and overloaded operators |
| Values | Null, default values, numeric boundaries, strings, nullable values, reference objects, and mutable structs |
| Effects | Getters, increments, method calls, `ref` and `out`, callbacks, enumeration, exceptions, and awaits |
| Placement | Consecutive assertions, branches, loops, helpers, lambdas, local functions, and async methods |
| Assertion API | `Verify`, interpolated messages, `.Because`, `Check`, nested scopes, `Throws`, and `ThrowsAsync` |
| Execution path | Generator absent, interceptors, eligible rewritten methods, and documented fallback paths |
| Outcome | Pass, assertion failure, expression exception, and cancellation where applicable |

Include interactions between statements. For example, an assertion that assigns an `out` variable affects a later assertion. A failed `Check` must allow subsequent checks to run within a scope. A failed `Verify` must stop execution according to its contract.

### Rewrite tier

Each generated assertion case records the [rewrite tier](../guide/tiers.md) it actually ran under. The case checks that tier against the tier it expects. A case that expects tier 3 and runs under tier 2 fails, even when its result matches.

Include a negative check. With the generator disabled, a case that requires rewriting must fail. This shows the tier check can detect a missing rewrite.

### Generated expression harness

Generate bounded, valid C# expressions from a grammar. Start with a small set of shapes and increase composition depth under an explicit time and case budget. Use isolated but equivalent initial state for each execution.

Compare an ordinary C# baseline with the corresponding generated execution. Record:

- The expression result or exception type and relevant payload.
- An ordered trace of getter reads, calls, mutations, and awaits.
- Final observable state.
- Which branches ran and which were skipped.
- The rewrite tier the generated execution ran under.

The baseline should evaluate the expression without VerificationGlue's rewrite logic. For an expression that throws, compare the expression exception rather than treating it as an assertion failure. For a false expression, separately check the assertion failure contract.

Check messages independently from semantic equivalence. Tiers intentionally offer different detail. Assert the promised source expression, file and line, captured values, skipped values, and unavailable values. Use snapshots for representative formatting, not as the sole behavioral oracle.

Exercise lazy messages with side effects on both passing and failing assertions. Include values whose formatting throws or mutates state and define the intended reporting behavior before accepting results.

Persist the seed, generated source, compiler configuration, and observation traces for a failure. Reduce a failing expression while preserving the mismatch. Add the reduced case to the deterministic regression suite.

#### The harness as built

[`test/Hardening.Specs`](../../test/Hardening.Specs) holds the harness. It is not in `VerificationGlue.slnx`, so the build job does not run it. The `hardening` job in [CI](../../.github/workflows/ci.yml) does.

The [grammar](../../test/Hardening.Specs/Grammar.cs) builds boolean expressions over a [`Probe`](../../test/Hardening.Specs/Probe.cs). An expression uses `!`, `&&`, `||`, `?:`, comparisons, `+`, `-` and `/`, `??`, `?.`, relational and property patterns, `out var`, `await`, a cancelled `await`, a throwing call, a getter, a call that changes state, and a write to the local `n`. Each probe call records an event, so the trace holds the order of reads, calls, mutations and awaits. Depth grows from 1 to 4 across the cases of a run. Case `i` of a seed comes from its own random generator, so a replay builds one case without the others.

Each batch of expressions compiles twice. The plain C# baseline compiles with no generator and no reference to VerificationGlue. The cases compile with `VerificationGlueGenerator` and tier 3 on, in each [form](../../test/Hardening.Specs/Forms.cs):

| Form | Expected tier |
|---|---|
| `Verify` and `Check` in a `[Verify]` method, called through its redirect | 3 |
| `Verify` and `Check` twice, in an `if` without braces in a loop of a `[Verify]` method | 3 |
| `Verify` and `Check` in an ordinary method | 2 |
| `Verify(() => ...)` and `Check(() => ...)` | 3 (lambda), or 2 as a fallback when the lambda writes `n` |
| `Verify` in a local function of a `[Verify]` method | 2 as a fallback |

Each form must match its baseline in outcome, trace, final state and skipped calls. The outcome is the result, the exception type and message, or the number of false results. Then the tier it ran under must be the tier it expects. The tier comes from the frame that made the first probe call: a `__Verified` copy is tier 3, the generated `VerifyLambdas` class is tier 3 (lambda), and elsewhere an interceptor at the call site makes it tier 2. A failure message must start with the condition's text, end with its file and line from tier 2 up, and have the shape of the tier that ran. A form that falls back on purpose counts as fallback, not as a pass. With the generator off, every form that requires rewriting fails its tier check, and every form still matches plain C#.

A mismatch prints the seed, the case and a replay command such as `HARDENING_SEED=20261119 HARDENING_CASES=300 HARDENING_CASE=57 dotnet test --project test/Hardening.Specs -c Release --vg-filter "name:A_fixed_seed_run_keeps_plain_CSharp_behavior"`. The harness shrinks the expression to one where no smaller expression gives the same kind of mismatch in the same form. It prints the `[Row(...)]` to add to [`RegressionSpecs`](../../test/Hardening.Specs/RegressionSpecs.cs). With `HARDENING_OUTPUT` set, it writes a report and, for each mismatch, the source, compiler configuration, environment and observations. The CI job keeps them for 30 days.

The fixed-seed run has 300 cases and about 2,700 form runs. It found no mismatch. The whole project takes 11 to 19 s on the development machine, depending on load. The `hardening` job fails when the step 2 cases take more than their 2-minute budget, after every case has run.

## Mock conformance

A mock must honor its setup and recording contracts across sequences of operations. Compiling a mock establishes only one part of that contract.

### Shapes and public consumption

Extend the existing shape corpus with consumer cases that create, configure, invoke, and verify mocks through public APIs. Cover inherited interfaces, overlapping member names, overloads, generic constraints, nullability, properties, indexers, events, and supported parameter modifiers.

Exercise direct `Mock.Of<T>()` creation, attributed parameters, registry creation, and the documented generic-helper path. Include interfaces declared in the consumer and in referenced assemblies. Check both generated compilation and runtime behavior.

Maintain expected classifications for the pinned package sweep. Detect a previously supported interface becoming refused, as well as generated compilation errors. Review corpus changes separately so changes in the input set do not hide regressions.

#### The shape consumers as built

[`test/Hardening.Specs/ShapeConsumers`](../../test/Hardening.Specs/ShapeConsumers) holds a step 3 class for each supported shape file. The project compiles the shape files themselves, so the public generator, the module initializer registry and the `Arg` interceptors build their mocks. The extern-aliased and `InternalsVisibleTo` libraries are projects under [`test/Hardening.Shapes`](../../test/Hardening.Shapes). Each case runs one scenario on a mock from each path: `Mock.Of<T>()`, a `[Mock]` parameter, `MockRegistry.Create(Type)` and the generic helper with `[assembly: GenerateMock]`. The scenario sets up, calls and checks the shape's members, and raises its events. A type marked `[Obsolete(error: true)]` has no `[Mock]` parameter or `GenerateMock`, because only obsolete code can name it. A VerificationGlue.Tests test method cannot take an extern-aliased type yet, so those `[Mock]` parameters go through the framework hook that [getting started](../guide/getting-started-mocks.md#host-frameworks) shows.

The consumers found three defects that the sweep and the shape specs cannot see, because neither runs the registry or a test framework. Two are fixed: the mock registry named a type obsolete as an error, and the test registration wrote an extern-aliased `GenerateMock` argument with `global::`. The third, test parameters of extern-aliased types, is a step of its own. The sweep's [pinned classifications](mocks-shape-sweep.md#pinned-classifications) fail the `mock-sweep` job on any change of outcome or of the swept set.

### Operations and sequences

Use a small independent model of matching, setup precedence, response sequences, and recorded calls. Generate legal operation sequences and compare their observable results with VerificationGlue.

A sequence may create a mock, configure it, invoke it, reconfigure it, await a result, and verify received calls. Vary the following factors:

| Dimension | Cases |
|---|---|
| Setup style | Typed setup, NSubstitute-style setup, and mixtures on one mock |
| Matching | Exact values, predicates, any value, nulls, defaults, multiple same-typed arguments, and overlapping matches |
| Response | Constants, factories, sequences, callbacks, exceptions, and unconfigured defaults |
| Member | Methods, overloads, generic methods, properties, indexers, events, and supported `ref` or `out` cases |
| Async result | Task and ValueTask forms, completion, fault, and cancellation where supported |
| Verification | No calls, exact counts, mismatches, repeated verification, and call diagnostics |
| Identity | Multiple instances, recursive mocks, repeated recursive access, and recursion boundaries |
| Context | Sequential calls, awaits, child tasks, parallel operations, and abandoned setup state |

Keep deliberate misuse in a separate set with explicit expected failures. Examples include setup without a preceding mock call and a matcher that cannot bind. Specify the state of the next operation after such a failure.

The required invariants include:

- Setup and verification do not add received calls.
- Verification does not consume response sequences.
- Separate instances do not share configuration or recorded calls.
- Reconfiguration follows the declared precedence rules.
- Recursive mock identity and depth follow their contracts.
- Matchers and pending setup state follow the documented async context rules.
- Exceptions and abandoned setup state do not silently corrupt subsequent operations.

Use barriers or controlled scheduling for concurrency cases. Define which orderings are legal before comparing observations. Repeated timing-dependent tests alone cannot establish isolation.

Extend the NSubstitute comparison for operations where compatibility is intended. Retain explicit differences. NSubstitute is an additional reference, not the definition of VerificationGlue's typed API or its intentional differences.

#### The mock sequence harness as built

[`test/Hardening.Specs/Mocks`](../../test/Hardening.Specs/Mocks) holds the step 3 cases. Each class has `[Trait("Step", "3")]`, and the `hardening` job runs them in their own step with a 2-minute budget. The mocks come from the public generator: `Mock.Of<IService>()` names the [subject](../../test/Hardening.Specs/Mocks/Subjects.cs), the generator follows its recursive members to `IChild` and `ILeaf`, and NSubstitute-style matchers go through the `Arg` interceptors.

A sequence is a list of ops on two root mocks and four slots that hold recursive mocks. The [generator](../../test/Hardening.Specs/Mocks/SequenceGenerator.cs) covers each factor of the table above:

| Factor | Ops and values |
|---|---|
| Setup style | `Returns` and `Throws`, `Setup()` with `Does`, `When(...).Do` and `When(...).Throw`, mixed on one mock |
| Matching | Exact values, `Arg.Any`, `Arg.Is` with a predicate or a value, the typed `Arg<T>` forms, null and default values, two `int` arguments |
| Response | Values, sequences, factories that read the arguments and set an out argument, exceptions, callbacks that write an argument, and unconfigured calls |
| Member | Overloads, a generic method, a property and an indexer with setters, an event, `out` and `ref` parameters |
| Async result | `Task<int>` and `ValueTask<int>` with a value, a fault and a cancellation |
| Verification | `Received`, `Received(n)`, `DidNotReceive`, `Calls()` counts, the received calls, and the count and listed calls of a failed check |
| Identity | Two root mocks, recursive mocks reached again, a configured recursive member, and `ILeaf.Next` beyond the depth |
| Context | Awaits, child tasks, a setup finished in a child task, an abandoned matcher, interleavings and parallel flows |

Each sequence runs on the [reference model](../../test/Hardening.Specs/Mocks/ReferenceModel.cs) and on the mocks, and the two lists of observations must be equal. The model implements the rules of [mocks.md](mocks.md) and does not call VerificationGlue.Mocks. The orderings are the legal ones of [`CD-04`](mocks.md#legal-orderings-across-awaits-child-tasks-and-parallel-calls). An interleaving runs a whole statement of another flow before the mock call of a setup or check in this flow, or between a setup's mock call and its `Returns`. The interpreter controls the point with semaphores: a hook on the last exact argument hands over before the mock call, and the setup blocks or awaits before `Returns`. Parallel flows start behind one barrier. Their observations are sorted, because which call takes which value of a response sequence depends on timing.

The [invariant checks](../../test/Hardening.Specs/Mocks/Invariants.cs) do not use the model. Each runs a changed sequence that the invariant says must give the same observations:

| Invariant | Check |
|---|---|
| Setup and verification do not add received calls | The received calls are the same before and after each setup and check that finishes |
| Verification does not consume response sequences | Each check runs twice. The second gives the same result, and every other statement gives the same observation |
| Separate instances do not share configuration or recorded calls | Leaving out every statement on the second root mock changes no observation of the first |
| Reconfiguration follows the declared precedence rules | Four setups of one call at the end, typed and NSubstitute-style, exact and with wildcards. After each, the call gives that setup's value |
| Recursive mock identity and depth follow their contracts | Each recursive member reached twice gives the same mock, and the member beyond the depth gives null |
| Matchers and pending setup state follow the documented async context rules | Child task statements run in this flow, setup awaits removed, blocking and awaiting swapped, abandoned matchers left out, and another flow's statement moved before the whole statement. No observation changes |
| Exceptions and abandoned setup state do not silently corrupt subsequent operations | Failed checks and abandoned matchers added between statements change no other observation |

[`MockMisuseSpecs`](../../test/Hardening.Specs/Mocks/MockMisuseSpecs.cs) holds the misuse set. Each case states its failure and what the next operation in the flow does. [`MockSchedulingSpecs`](../../test/Hardening.Specs/Mocks/MockSchedulingSpecs.cs) runs 168 interleavings of six statements with sixteen statements of another flow, on the same mock and on the other one.

A mismatch prints the seed, the case and a replay command such as `HARDENING_SEED=20261119 HARDENING_CASES=2000 HARDENING_CASE=57 dotnet test --project test/Hardening.Specs -c Release --vg-filter "name:A_fixed_seed_run_of_mock_sequences_matches_the_reference_model"`. It shrinks with the shrinker step 2 uses, until no op can be dropped, flattened or reduced without losing the mismatch. It prints the shrunk sequence as JSON in the `[Row(...)]` to add to [`MockRegressionSpecs`](../../test/Hardening.Specs/Mocks/MockRegressionSpecs.cs). Two specs break a rule in the model on purpose, the last setup winning and a check leaving sequences alone, and check that each run fails, shrinks to at most three ops and replays.

The fixed-seed run has 2,000 sequences and about 29,000 observations, and takes 0.5 s. All 26 step 3 cases take about 2 s on the development machine. Runs of 20,000 sequences on eight other seeds found one defect. A `Does` callback that wrote the argument of a recursive member changed which mock `MockAdapter.CallMock` returned for those arguments. It is fixed, and a regression row and a behaviour case cover it.

## Test construction and lifecycle

Tests must receive the intended dependencies, own the right resources, and report failures even when setup or cleanup also fails.

### Parameter resolution

Cover constructor and method parameters, `[Mock]`, custom providers, container resolution, built-in values, and defaults. Use the [parameter resolution guide](../guide/test-parameters.md) as the contract for precedence.

Exercise competing providers, providers that decline a value, missing values, and provider exceptions. Assert object identity where a mock is both a test parameter and a dependency of the SUT. Cover supported test return types and invalid test signatures through generator diagnostics.

Keep planned features separate. The guide identifies `[Row]` as future work. It must not appear as an already supported coverage claim.

### Failure injection and ownership

For assembly, class, and test scopes, inject failure and cancellation at each applicable boundary: scope creation, container construction, middleware entry, class construction, parameter resolution, the test body, middleware exit, and disposal.

Compare lifecycle traces against the [lifecycle contract](../guide/test-lifecycle.md). Assert that:

- Each acquired resource has one owner and is disposed exactly once.
- Remaining cleanup runs after a cleanup step fails.
- All failures appear against the correct test or scope in the promised order.
- A failing setup does not execute the test body.
- Filtered-out tests create no resources.
- Subsequent tests inherit no stale checks, mock state, or test context.
- Cancellation releases acquired resources and prevents further work as promised.

Include simultaneous body and cleanup failures. Exercise container-owned and runner-owned test instances separately. Run process-level cases for selection, reporting, exit status, and retry isolation under the supported retry mechanism.

#### The failure injection as built

[`test/Hardening.Lifecycle.Specs`](../../test/Hardening.Lifecycle.Specs) runs [`test/Hardening.Lifecycle.Fixture`](../../test/Hardening.Lifecycle.Fixture) as a process for each case. Neither is in `VerificationGlue.slnx`. The `hardening` job in [CI](../../.github/workflows/ci.yml) runs the specs on xunit v3, so the runner is not the only check of itself.

The fixture has a scope listener, middleware and a parameter provider on the assembly, the class and the method, and a container that builds at test, class or assembly scope. `VG_INJECT` names the faults, such as `test:body:exception;class:disposal:exception`. Each place that a fault can reach writes a line to the fixture's log, and each resource writes `acquire` and `release` lines. The resources are listeners, entered middleware, containers, the services they create and test class instances. A cancellation fault sends SIGINT to the fixture, as Ctrl+C does, waits for the run's token, and throws `OperationCanceledException`. The cancellation cases skip on Windows, which is outside the supported matrix.

| Scope | Boundaries |
|---|---|
| Test | Scope creation, container construction, middleware entry, class construction for a runner-owned and a container-owned instance, parameter resolution, the body, middleware exit, and disposal of the instance, the listener and the container |
| Class | Scope creation, container construction, middleware entry, parameter resolution, middleware exit, and disposal of the listener and the container |
| Assembly | The same as the class scope, with two classes sharing the assembly container |

Each boundary runs once with an exception and once with a cancellation. Four more runs combine faults: a body failure with several cleanup failures, a middleware entry failure with cleanup failures, a listener entry failure with class scope cleanup failures, and a disposal failure at every scope at once. Every case checks that:

- Each acquired resource is released exactly once.
- The faults fire in the expected order, and a shared scope or container that fails is not created again.
- Each failing test or scope node reports its faults in the order they fired, and every other test passes.
- A test whose setup fails does not run its body, and a test whose cleanup fails did run it.
- After a cancellation, nothing more is acquired and no body runs, and the run exits with code 3.

A separate case lets one test leave a failed `Check`, a mock call with no `Returns`, two queued matchers, a pending `Received` on a shared mock and its own output. The next test passes only when it sees none of them.

To check that the cases can fail, three changes to the runner were tried one at a time and reverted. Skipping a test's container disposal after a failure failed 17 of the 26 exception cases and 14 of the 22 cancellation cases. Reporting listener cleanup failures before earlier failures failed the 3 cases where one test has several cleanup failures. Starting tests after a cancellation failed 18 of the 22 cancellation cases. The other 4 cancel while a class or assembly scope is created or disposed, where the next test either fails before it acquires anything or does not exist.

The cases found that MTP drops every result published after a cancellation, so a cleanup failure after it was not reported. The runner now also writes those failures to the console output, and [tests.md](tests.md#scopes) records that as part of the `CD-05` answer. Three more cases cancel in the test body and then fail the test's middleware exit and each disposal step of the test, class and assembly scopes. The cancellation stops the next class from starting, so one run reaches only one class or assembly container. The case for `Lifecycle.TestScoped` reaches the test's instance disposal. The case for `Lifecycle.ClassScoped` reaches the class container, and the case for `Lifecycle.AssemblyScoped` reaches the assembly container. Each case checks that the console output holds every failure under its test or scope, in the order they fired. Without the runner change, these cases fail.

The 52 cases took 7.3 s on the development machine, after the fixture was built. The `hardening` job fails when they take more than their 1-minute budget, after every case has run.

### Framework and package consumers

Run the framework hooks in actual xunit, NUnit, MSTest, and TUnit projects. Include manual mock creation and attributed mock parameters. Verify that direct helper calls and reflection-invoked test methods receive the documented rewrite tier.

Run representative scenarios from packed packages, not only project references. Include generator discovery, build properties, version mismatch diagnostics, trimming, and Native AOT where supported. Record SDK, framework, operating system, and architecture in the compatibility matrix. Step 1 defines the supported matrix. An untested platform is not a release guarantee.

## Interaction coverage

Use exhaustive cases for small truth tables and boundary rules. Use pairwise coverage for the larger configuration matrix: every valid pair of factor values must occur in a selected case. Record exclusions and their reasons.

Pairwise coverage does not prove interactions involving three or more factors. Add explicit cases for these higher-risk combinations:

| Combination | Required observation |
|---|---|
| Awaited mock result inside a rewritten assertion with short-circuiting | The call occurs once and skipped branches remain skipped |
| Constructor mock plus container-created SUT plus test parameter | Dependency identity follows the resolution contract |
| Failed `Check` plus throwing body plus multiple cleanup failures | Every failure survives reporting and cleanup completes |
| Recursive generic async member plus mixed setup styles | Setup, returned identity, and received calls remain consistent |
| Child task plus pending verification plus background invocation | Context isolation and recording follow the declared rules |
| Packed consumer plus rewriting plus trimming or AOT | Public generation and execution work together in the supported configuration |

Generate a coverage report from executed cases. Report valid combinations covered, exclusions, unverified combinations, and failures. Do not derive this report from the number of tests alone.

## Test effectiveness and execution budgets

Use targeted mutation testing to check whether the suite detects broken behavior. Candidate mutations include evaluating a skipped operand, recording setup as a call, reversing setup precedence, consuming a response during verification, and skipping a cleanup stage.

Review surviving mutations. They may expose missing assertions or an equivalent mutation. Record that distinction rather than accepting a percentage without explanation.

| Run | Proposed contents |
|---|---|
| Each pull request | Deterministic regressions, bounded fixed-seed generation, shape classification checks, representative consumers, and existing performance gates |
| Scheduled campaign | Larger seeded expression and operation generation, controlled concurrency cases, broader supported environments, and targeted mutation runs |
| Release candidate | Supported compatibility matrix, packed consumer checks, AOT checks, and review of unresolved failures and exclusions |

Pull request cases from this exercise run in their own CI job, separate from the existing build and test jobs.

The review proposed two limits for the pull request budget:

- A separate job that finishes within 10 minutes.
- At most five minutes added to the pull request critical path.

Step 1 measured the existing suites, and the user settled the budget at the step 1 gate. [Measured run time and CI budget](#measured-run-time-and-ci-budget) has the settled limits. The settled limits are recorded in configuration.

A run that exceeds its budget reports the overrun. It does not drop a required check to fit. A timed-out or truncated campaign reports incomplete coverage rather than success for its intended case budget.

A scheduled campaign starts only after it has a named triage owner and a defined destination for its failures, such as an issue label.

Every generated failure should provide a replay command, seed, source or operation sequence, environment, expected observations, and actual observations. Retain reduced regressions permanently. Keep campaign artifacts for 30 days.

## Delivery sequence

The outcome is a supported behavior contract that users can rely on, with a repeatable check for each promise and a visible record of remaining uncertainty.

Each step has its own exit criterion and one issue.

1. [Build the inventory](https://github.com/ipjohnson/VerificationGlue/issues/54) from public APIs, guides, diagnostics, and existing specs. Classify boundaries and map exact tests to contracts. Exit: the user approves the step 1 gate.
2. [Add the assertion comparison harness](https://github.com/ipjohnson/VerificationGlue/issues/55). Exit: deterministic replay, shrinking, semantic traces, tier checks, and message checks work for an initial bounded grammar.
3. [Add the mock operation model and public consumer cases](https://github.com/ipjohnson/VerificationGlue/issues/56). Exit: sequence generation, explicit misuse outcomes, and regression checks for sweep classifications run in CI.
4. [Fill lifecycle and resolution gaps](https://github.com/ipjohnson/VerificationGlue/issues/57). Exit: failure-injection cases and process-level evidence cover ownership, isolation, and reporting.
5. [Add interaction selection and campaign reporting](https://github.com/ipjohnson/VerificationGlue/issues/58). Exit: reproducible CI jobs, measured budgets, targeted mutation results, and compatibility reports produce the completion report.

### Step 1 gate

Step 1 ends with a user approval gate. It delivers:

- The inventory at a named baseline commit.
- The gap list, with a size estimate for each gap.
- The contract decisions that need an owner or approval.
- The supported compatibility matrix.
- The non-goals.
- The measured harness costs and the proposed CI budget.

The approval decides which of steps 2 to 4 run, in what order, and which run in parallel. It also settles the CI budget.

Expand scope only when a newly discovered feature or platform is added to the supported contract. A defect found during the exercise should become a reduced regression and a tracked fix, not an unreviewed change in expected behavior.

The [step 1 gate summary](#step-1-gate-summary) below holds these deliverables.

## Step 1 gate summary

The inventory holds 166 entries at baseline commit `b28d1d6fcc103afd8687b9d860ef16ee77c9a0fd`, the head of `main` when step 1 started. 129 entries are supported, 30 are deliberately unsupported with an expected outcome, and 7 are not yet classified because they wait on a contract decision. 20 entries have no evidence. 48 entries have a gap, which includes partial gaps in entries that have some evidence.

| Area | Prefix | Entries |
|---|---|---|
| `Verify`, messages and the assertion analyzers | `VERIFY` | 15 |
| `Check` | `CHECK` | 7 |
| `VerifyScope` | `SCOPE` | 7 |
| `Throws` and `ThrowsAsync` | `THROWS` | 7 |
| Tier 3 rewriting | `REWRITE` | 20 |
| Mock behavior | `MOCK` | 54 |
| Mock shapes and the shape sweep | `SHAPE` | 10 |
| Test parameter resolution | `PARAM` | 11 |
| Test lifecycle and ownership | `LIFE` | 15 |
| Test registration and the runner process | `RUN` | 8 |
| Packages, frameworks and environments | `PKG` | 6 |
| Higher-order interactions | `MIX` | 6 |

The approval covers each of the following parts.

### Contract decisions

Each decision settles an observable behavior that no guide or design states. Its entries report as unverified until the decision is approved. The proposed owner is the component design that records the answer. The user approved all ten answers at the gate, and each owner design records its answer when the step that tests it starts.

| ID | Decision | Proposed owner | Scenarios | Proposed answer |
|---|---|---|---|---|
| `CD-01` | What a failure message shows, and which exception surfaces, when formatting a captured value or an interpolated message hole throws or changes state. | verification-glue.md | `VERIFY-013`, `VERIFY-014` | The `VerifyException` still surfaces. The value shows a placeholder that names the formatting exception's type. |
| `CD-02` | Which setup wins when typed and NSubstitute-style setups, or overlapping matchers, configure the same call. | mocks.md | `MOCK-008` | The last matching setup wins, whatever its style. |
| `CD-03` | Which argument equality decides recursive mock identity, what a recursive member returns after its parent is reconfigured, and whether the default returned at the depth limit is a contract tests may rely on. | mocks.md | `MOCK-045`, `MOCK-048` | The equality that exact matching uses. A new setup on the parent replaces the recursive mock. The default at the limit is a contract. |
| `CD-04` | Which orderings of pending setups, matchers and checks are legal across awaits, child tasks and parallel calls. | mocks.md | `MOCK-038`, `MOCK-039`, `MOCK-040`, `MOCK-041` | Step 3 writes the legal orderings into mocks.md before it generates cases. Other orderings are unsupported. |
| `CD-05` | The order of failures when a scope listener, a class scope or the assembly scope fails during cleanup, and when cancellation arrives during cleanup. | tests.md | `LIFE-007`, `LIFE-009`, `LIFE-013` | Failures in the order they happen. Cleanup still runs every step after cancellation. |
| `CD-06` | Whether the scope's exception keeps the exception the test threw, which check.md says it replaces outside VerificationGlue.Tests. | verification-glue.md | `SCOPE-006` | Keep both. The test's exception becomes the `InnerException`, tracked as a fix. |
| `CD-07` | How a scope collects Check failures from child tasks that run in parallel, and in what order it reports them. | verification-glue.md | `SCOPE-007` | Every failure is kept. Their order is not part of the contract. |
| `CD-08` | What a test reports when a parameter provider or the container throws while resolving a parameter. | tests.md | `PARAM-004` | The test fails with the exception, naming the parameter and the source. The body does not run, and cleanup runs. |
| `CD-09` | What happens when an Arg.Is predicate throws, and the state of the next mock operation after any misuse failure. | mocks.md | `MOCK-023`, `MOCK-042` | The predicate's exception leaves the mock call unchanged. A misuse failure clears the pending state. |
| `CD-10` | Whether a type nested in a generic type whose type arguments come from an extern-aliased assembly gets a VGM0001 refusal or is supported. | mocks.md | `SHAPE-010` | Refuse it with a new VGM0001 reason. |

### Supported matrix

| Dimension | Proposed support | Run today |
|---|---|---|
| SDK | .NET SDK 10.0.403 and later 10.0.4xx patches, from `global.json` | CI and local |
| Framework | `net10.0` for `VerificationGlue`, `VerificationGlue.Mocks` and `VerificationGlue.Tests`. The generators target `netstandard2.0` and need the Roslyn 4.14 compiler of that SDK. | CI and local |
| Test hosts | xunit.v3 4.0.3, NUnit 4.6.1, MSTest 4.5.3, TUnit 1.85.0, and Microsoft.Testing.Platform 3.0.0-preview.26566.9 for `VerificationGlue.Tests` | CI |
| OS and architecture | Linux x64 as the release guarantee. macOS arm64 and Windows x64 join the release candidate matrix in step 5. | Linux x64 in CI. macOS arm64 on the development machine only. Windows x64 never. |
| Native AOT | linux-x64 | CI, through `eng/pack-and-consume.sh` |

An earlier .NET version, .NET Framework and an IDE's debugger are outside the matrix. `docs/research/ide-report.md` records the IDE state.

### Non-goals

- Proving every C# program. The exercise covers the declared grammar, matrix and interactions.
- Class mocks and `[Row]` data rows. They are planned features, and the inventory lists them only as unsupported boundaries.
- A line coverage target.
- Compatibility with NSubstitute beyond the bounded comparison. NSubstitute is a reference, not the definition of the API.
- New performance budgets. The existing benchmark and cold-build budgets stay as they are.
- Features merged after the baseline commit, such as the `VerificationGlue.Tests.AspNetCore` package. Each adds its own entries in its pull request.

### Measured run time and CI budget

The five most recent green CI runs on `main`, on `ubuntu-latest`, took these times:

| Job or step | Time |
|---|---|
| `cold-build` job, the critical path | 639 to 1,033 s |
| `build` job | 233 to 299 s |
| `Run specs` step in `build` | 16 to 38 s |
| `Run generator benchmarks` and `Check generator budgets` | 103 to 133 s together |
| `Pack, consume, run tests under Native AOT and check version mismatch` | 31 to 79 s |
| `mock-sweep` job | 39 to 77 s |
| `docs` job | 10 to 21 s |

On the development machine, an Apple M3 Pro with 11 cores, `dotnet test --solution VerificationGlue.slnx -c Release --no-build` ran 457 tests in 9.5 s of wall time. The slowest projects were `VerificationGlue.Tests.Specs` at 8.6 s, which starts fixture processes, and `VerificationGlue.Tests.Generator.Specs` at 5.3 s. `Inventory.Specs`, which checks the inventory, took 0.5 s alone and 1.9 s in the parallel run.

The two limits from the review do not conflict on today's CI. A separate `hardening` job runs beside `cold-build`, which never took less than 10.6 minutes. A job that finishes within 10 minutes therefore adds nothing to the critical path. The approved budget:

- A separate `hardening` job with `timeout-minutes: 10`.
- A pull request case budget of 5 minutes inside it, which leaves room for checkout, SDK setup and the build. Step 2 gets 2 minutes, step 3 gets 2 minutes and step 4 gets 1 minute.
- If `cold-build` becomes shorter than the `hardening` job, the 5-minute limit on the critical path applies instead.
- Step 5 records the approved limits in configuration.

### Recommended order for steps 2 to 4

The completion criteria name two defects that block completion: a change to normal C# evaluation, and a failure lost in setup or cleanup. Step 2 and step 4 look for those, so they come first.

1. Start step 2 and step 4 in parallel. They touch separate projects. Step 2 adds the `hardening` CI job in its first pull request, and step 4 puts its cases in that job.
2. Start step 3 after step 2 merges. Step 3 reuses the seed, replay and shrinking that step 2 builds, and `CD-04` must be settled first.

| Step | Gaps | Sizes |
|---|---|---|
| 2 | 0 | |
| 3 | 15 | 2 l, 2 m, 5 s, 6 xs |
| 4 | 15 | 1 l, 1 m, 7 s, 6 xs |
| 5 | 6 | 2 m, 2 s, 2 xs |

### Gap list

Sizes: xs is one case in an existing spec. s is a few cases or one fixture test. m is a new spec class or fixture scenario. l is a new harness component.

| Scenario ID | Missing | Size | Step |
|---|---|---|---|
| `MOCK-008` | No case mixes setup styles or overlapping predicates on one call, and no guide states the rule for them. | m | 3 |
| `MOCK-020` | No case covers a ref argument. | xs | 3 |
| `MOCK-023` | No guide states the outcome, and no test exercises it. | s | 3 |
| `MOCK-030` | No test checks a sequence after Received, or repeats a check. | s | 3 |
| `MOCK-035` | No test returns a cancelled task. | xs | 3 |
| `MOCK-042` | No test checks the state of the next mock operation after the failure. | s | 3 |
| `MOCK-045` | No case uses arguments that are equal but not identical, or reconfigures the parent after access. | s | 3 |
| `MOCK-047` | No test covers either rule. | xs | 3 |
| `MOCK-048` | No test sets the depth to 0. | xs | 3 |
| `PKG-006` | Windows x64 and macOS arm64, if the gate adds them to the matrix, have no CI run. | m | 5 |
| `MIX-001` | No test combines the three factors. | s | 5 |
| `MIX-002` | The sample covers class scope only. No case at test or assembly scope. | xs | 5 |
| `MIX-004` | No test combines the factors. | s | 5 |
| `MIX-005` | Needs the controlled scheduling from step 3. | m | 5 |
| `MIX-006` | PackageConsumer does not turn tier 3 rewriting on, so the AOT run does not check a captured-value message. | xs | 5 |

### Gate approval

The user approved the step 1 gate on 2026-11-17, with every part as proposed:

- The inventory at baseline `b28d1d6fcc103afd8687b9d860ef16ee77c9a0fd`, the gap list and the non-goals.
- Contract decisions `CD-01` to `CD-10`, with their proposed owners and answers. The inventory marks each one approved.
- The order: steps 2 and 4 in parallel, then step 3 after step 2 merges.
- The CI budget: a separate `hardening` job with `timeout-minutes: 10`, and a 5-minute case budget split 2, 2 and 1 minutes across steps 2, 3 and 4.
- The matrix: Linux x64 as the release guarantee, with macOS arm64 and Windows x64 in the release candidate matrix from step 5.

## Completion criteria

The initial exercise is complete when:

- Every declared contract has executable evidence in its applicable configurations.
- Every deliberately unsupported boundary has a tested diagnostic, failure, or fallback.
- The selected pairwise combinations and explicit higher-order scenarios are accounted for.
- Generated failures can be replayed, and resolved failures have deterministic regressions.
- Representative packed consumers and the agreed compatibility matrix pass.
- Targeted mutations are detected or have reviewed explanations for surviving.
- No unexplained failures remain within the declared scope.
- Performance budgets and campaign limits are reported with the results.

Two kinds of defect block completion: a change to normal C# evaluation, and a failure lost in setup or cleanup. Another known defect may stay open only with all of the following:

- Explicit acceptance by the user.
- An issue.
- An owner.
- A review deadline.
- A skipped regression case that names the issue.

A deferred or quarantined case reports as failed or unverified, never as passing. An unverified entry within scope blocks completion unless a recorded scope decision removes it from scope.

The completion report must list exclusions and unverified areas alongside passing evidence. Code coverage can identify unvisited implementation paths, but it does not replace these criteria. Future changes must update the inventory and associated tests as part of changing the contract.
