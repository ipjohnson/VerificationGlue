# VerificationGlue hardening exercise

Users should be able to combine assertions, mocks, and test setup without changing normal C# evaluation, leaking state between tests, or losing failures during cleanup. This exercise establishes executable evidence for those guarantees across the supported surface of VerificationGlue.

Status: proposed exercise. Design review drv-60ynneww accepted 11 of its 12 decisions, and this text applies them. The existing suites below are foundations to extend. The coverage inventory, generated campaigns, and completion gates described here are proposed work.

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

The inventory is a checked-in machine-readable file. It is built at a named baseline commit, which step 1 records. CI does not write to it. Each CI run writes a separate run report, and reports join to the inventory by Scenario ID.

Each inventory entry needs these fields:

| Field | Purpose |
|---|---|
| Scenario ID | Stable reference for reports and regression cases |
| User-visible contract | Expected result, failure, diagnostic, or fallback |
| Support status | Supported, deliberately unsupported, or not yet classified |
| Contract decision | For an unresolved observable behavior, the decision needed, its owner, and whether it is approved |
| Dimensions | Expression, member shape, setup style, scope, framework, and other applicable factors |
| Configuration constraints | Combinations that are valid, with reasons for exclusions |
| Evidence | Exact test cases and consumer projects that exercise the contract |
| Follow-up | Missing evidence, defect, or decision needed, with its issue |

Verification status and run details live in the run reports, not in the inventory. A run report records, for each Scenario ID, the status (not yet verified, passed, failed, or blocked), the commit, the configuration, the environment, the seed where applicable, and the report location.

CI validates the inventory on each pull request:

- Every Scenario ID is unique and every report entry names an existing ID.
- Every evidence reference names a test or project that exists.
- While step 1 builds the inventory, an entry may record an explicit gap in place of evidence.
- At completion, a supported entry with missing required evidence fails the check.

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

## Mock conformance

A mock must honor its setup and recording contracts across sequences of operations. Compiling a mock establishes only one part of that contract.

### Shapes and public consumption

Extend the existing shape corpus with consumer cases that create, configure, invoke, and verify mocks through public APIs. Cover inherited interfaces, overlapping member names, overloads, generic constraints, nullability, properties, indexers, events, and supported parameter modifiers.

Exercise direct `Mock.Of<T>()` creation, attributed parameters, registry creation, and the documented generic-helper path. Include interfaces declared in the consumer and in referenced assemblies. Check both generated compilation and runtime behavior.

Maintain expected classifications for the pinned package sweep. Detect a previously supported interface becoming refused, as well as generated compilation errors. Review corpus changes separately so changes in the input set do not hide regressions.

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

The pull request budget is not fixed yet. The review proposed two limits, and they disagree:

- A separate job that finishes within 10 minutes.
- At most five minutes added to the pull request critical path.

Step 1 measures the initial harnesses and proposes case counts and a time budget against those measurements. The user settles the budget at the step 1 gate. The settled limits are recorded in configuration.

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
