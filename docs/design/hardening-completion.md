# Hardening completion report

This report measures the [hardening exercise](hardening.md) against each of its [completion criteria](hardening.md#completion-criteria). It lists the passing evidence, the exclusions, the unverified areas, the budgets and the campaign limits, and the one open defect.

The evidence of record for a commit is the `completion-gate` job of that commit's CI run. Its artifact holds `completion.md` and `completion.json`, with the decision for each inventory entry. [The completion gate](hardening.md#the-completion-gate) describes the job. The figures below come from a replay of every CI step that writes a run report, on the development machine on 2026-11-21, at commit `e04c852` of the step 5 completion branch, which is based on `main` at `c56fc09`. The replay ran on macOS arm64, so its reports were relabelled `os=linux-x64` before the gate read them, apart from the release candidate report.

## Summary

| Criterion | Result |
|---|---|
| Every declared contract has executable evidence in its applicable configurations | 165 of 166 entries verified in the replay. `PKG-006` needs the Windows x64 run, which only CI has. |
| Every deliberately unsupported boundary has a tested diagnostic, failure, or fallback | All 30 unsupported entries verified |
| The selected pairwise combinations and explicit higher-order scenarios are accounted for | `MIX-001` to `MIX-006` verified. 18 of the 30 selected combinations run in CI, and the other 12 are listed as unverified. |
| Generated failures can be replayed, and resolved failures have deterministic regressions | Each generator prints a replay command, and each defect found has a regression case |
| Representative packed consumers and the agreed compatibility matrix pass | Passed on Linux x64 with Native AOT, and on macOS arm64 and Windows x64 on the JIT at `259570d` |
| Targeted mutations are detected or have reviewed explanations for surviving | 6 of 6 detected. None needs an explanation. |
| No unexplained failures remain within the declared scope | No blocking defect is open. `#112` is open with the user's acceptance. |
| Performance budgets and campaign limits are reported with the results | Listed under [Budgets and limits](#budgets-and-limits) |

## Contracts with executable evidence

The [inventory](hardening-inventory.json) holds 166 entries. 136 are supported, 30 are unsupported with an expected outcome, and none is unclassified. No entry has a gap, and `allowGaps` is false, so `test/Inventory.Specs` fails on any new gap.

| Area | Entries | Unsupported |
|---|---|---|
| `VERIFY` | 15 | 0 |
| `CHECK` | 7 | 1 |
| `SCOPE` | 7 | 0 |
| `THROWS` | 7 | 0 |
| `REWRITE` | 20 | 7 |
| `MOCK` | 54 | 7 |
| `SHAPE` | 10 | 8 |
| `PARAM` | 11 | 2 |
| `LIFE` | 15 | 0 |
| `RUN` | 8 | 4 |
| `PKG` | 6 | 1 |
| `MIX` | 6 | 0 |

The replay wrote four run reports, and the gate read them:

| Run report | Job | Entries | Complete |
|---|---|---|---|
| `run-report-build` | `build` | 233 | yes |
| `hardening` | `hardening` | 21 | yes |
| `run-report-mock-sweep` | `mock-sweep` | 1 | yes |
| `run-report-macos-arm64` | `release-candidate` | 1 | yes |

An entry of a run report is one Scenario ID in one configuration, so an entry with evidence in several profiles has several. Every test that the inventory names ran in its step. The gate verified 165 of the 166 entries. `PKG-006` stayed unverified because no step ran `.github/workflows/release-candidate.yml` on `windows-x64`. CI runs that platform in the `release-candidate` job.

The [configuration matrix](hardening.md#the-configuration-matrix) gives each project one profile. Each profile runs on `linux-x64`, apart from `release-candidate`, which runs on `macos-arm64` and `windows-x64`.

## Unsupported boundaries

Each of the 30 unsupported entries names its expected outcome, and each passed in the replay. The outcomes are the VGM0001 refusal of a mock shape, a diagnostic, a throwing member, a fallback to a lower tier or a documented failure. [Review unsupported boundaries](hardening.md#coverage-inventory) lists the sources. The [mock shape report](mocks-shape-sweep.md) records the 39 refused interfaces of the sweep, and the `mock-sweep` job fails on any change of outcome.

## Combinations and higher-order scenarios

The explicit higher-order scenarios `MIX-001` to `MIX-006` passed. [The interaction cases as built](hardening.md#the-interaction-cases-as-built) describes each.

The pairwise selection has 30 combinations, which hold all 91 valid pairs. 18 come from the profiles that CI runs, and they hold 64 of the 91 pairs. The other 12 come from the pairwise fill. No CI step runs them, so they are unverified:

| # | os | runtime | consumption | host | tier |
|---|---|---|---|---|---|
| 19 | macos-arm64 | jit | project-reference | none | 2 |
| 20 | windows-x64 | jit | project-reference | none | 1 |
| 21 | macos-arm64 | jit | package | xunit.v3 | 1 |
| 22 | macos-arm64 | jit | package | NUnit | 1 |
| 23 | macos-arm64 | jit | package | MSTest | 1 |
| 24 | macos-arm64 | jit | package | TUnit | 1 |
| 25 | windows-x64 | jit | project-reference | xunit.v3 | 2 |
| 26 | windows-x64 | jit | project-reference | NUnit | 1 |
| 27 | windows-x64 | jit | project-reference | MSTest | 1 |
| 28 | windows-x64 | jit | project-reference | TUnit | 1 |
| 29 | linux-x64 | native-aot | project-reference | none | 2 |
| 30 | linux-x64 | jit | package | none | 2 |

The release candidate matrix runs the specs and the framework samples on macOS arm64 and Windows x64. Those runs are reported as one `release-candidate` outcome per platform, so they cover only the two release candidate combinations.

## Replay and regressions

Each generator prints a replay command with its seed, its case count and the failing case, and shrinks the failure before it prints it:

- The expression harness prints the `[Row(...)]` to add to [`RegressionSpecs`](../../test/Hardening.Specs/RegressionSpecs.cs).
- The mock sequence harness prints the shrunk sequence to add to [`MockRegressionSpecs`](../../test/Hardening.Specs/Mocks/MockRegressionSpecs.cs).
- A campaign issue gives the command that replays the whole run, as [Triage](hardening.md#triage) describes.

Each defect the exercise found is fixed, and a case in the pull request run covers it:

| Step | Defect | Regression |
|---|---|---|
| 3 | A `Does` callback that wrote the argument of a recursive member changed which mock the member returned | A row in `MockRegressionSpecs` and a behaviour case |
| 3 | An `Arg.Is` predicate that threw escaped the mock call | The misuse set of the step 3 cases |
| 3 | A misuse failure left queued matchers, a pending check and the last call in place | The misuse set checks the next operation after each misuse failure |
| 3 | The mock registry named a type obsolete as an error | The shape consumers of `test/Hardening.Specs` |
| 3 | The test registration wrote an extern-aliased `GenerateMock` argument with `global::` | The shape consumers and VGT0006 |
| 3 | The test registration wrote extern-aliased parameter types with `global::` | The shape consumers |
| 4 | MTP dropped every result published after a cancellation, so a cleanup failure after it was lost | The cancelled-cleanup cases of `test/Hardening.Lifecycle.Specs` |
| 5 | A tier 3 copy declared a local of the wrong type for a part that calls a member another generator adds | `WillRewriteContractSpecs.Every_runner_call_binds_and_runs` |

## Packed consumers and the compatibility matrix

`eng/pack-and-consume.sh` packs the four packages and runs the packed consumers on Linux x64. It publishes `samples/PackageConsumer`, `samples/VerifyAot` and `samples/MocksAot` with Native AOT and runs them, and it checks the version mismatch diagnostic. It passed in the replay. The [release candidate matrix](hardening.md#the-release-candidate-matrix-as-built) passed on macOS arm64 and Windows x64 on the JIT at `259570d`. `ci.yml` now runs it on each push to `main` and each pull request, so each commit's gate needs both platforms.

## Targeted mutations

All six [targeted mutations](hardening.md#targeted-mutations-as-built) were detected in the run of 2026-11-21 at `4eca35f`. No mutation survived, so none needs an explanation. The scheduled campaign runs them every week, and a mutation that no longer matches its catalog entry becomes an issue for the triage owner.

## Open defects

Two kinds of defect block completion: a change to normal C# evaluation, and a failure lost in setup or cleanup. No open issue reports either.

| Issue | Defect | Accepted | Owner | Review by | Skipped case |
|---|---|---|---|---|---|
| [#112](https://github.com/ipjohnson/VerificationGlue/issues/112) | A plain string as `Verify`'s second argument binds to the condition's text, so the failure shows the message in place of the condition and the message is lost | By the user on 2026-11-21 | `ipjohnson` | 2026-12-31 | `OpenDefectSpecs.A_plain_string_message_keeps_the_condition_text_and_shows_the_message` in `test/VerificationGlue.Tests.Specs` |

The skipped case is in no inventory entry's evidence, so it never counts as passing. It runs on xunit v3, because `[VerifyTest]` has no way to skip a case. Without its `Skip`, it fails with `Verify failed: width is wrong`.

The other open issues are outside the declared contracts. `#139` is about the package README logo on nuget.org. `#113`, `#114` and `#115` ask for richer messages that no contract promises.

## Exclusions

- The [excluded pairs](hardening.md#excluded-pairs) of the matrix: Native AOT on macOS arm64 and Windows x64, and Native AOT with a framework host.
- The [non-goals](hardening.md#non-goals): every C# program, class mocks, `[Row]` data rows as a mock feature, a line coverage target, NSubstitute compatibility beyond the bounded comparison, new performance budgets, and features merged after the baseline commit.
- An earlier .NET version, .NET Framework and an IDE's debugger are outside the [supported matrix](hardening.md#supported-matrix).

## Unverified areas

- The 12 pairwise combinations listed under [Combinations and higher-order scenarios](#combinations-and-higher-order-scenarios).
- The xunit v3 runs of the five conformance suites write no run report. Their assemblies have the names of the VerificationGlue.Tests runs, so the report cannot keep the two apart. They still fail the `build` job when a case fails.
- The specs and samples on macOS arm64 and Windows x64 count only as one outcome per platform.
- A consumer whose sources have CRLF line endings on Windows. The specs build the repository's LF sources.
- The shape sweep calls the model builder and the emitter directly, not the public generator. The shape consumers cover the public generator for the shapes `SHAPE-001` lists.
- The campaign's larger seeded runs and controlled concurrency cases run every week, not on each commit. A failure there reaches the owner as an issue, not as a failed pull request.

## Budgets and limits

| Budget | Limit | Where |
|---|---|---|
| `hardening` job | 10 minutes | `timeout-minutes` in `ci.yml` |
| Step 2 cases | 120 s | `STEP2_BUDGET_SECONDS` |
| Step 3 cases | 120 s | `STEP3_BUDGET_SECONDS` |
| Step 4 cases | 60 s | `STEP4_BUDGET_SECONDS` |
| Step 5 cases | 30 s | `STEP5_BUDGET_SECONDS` |
| Case budget in all | 5.5 minutes | The user approved 5 minutes at the step 1 gate and kept the 30 s of step 5 |
| Tier 2 cold build | 160 s added at 100,000 calls, and 8.5 times the build without the generator | `test/ColdBuild/budgets.json` |
| Tier 3 cold build | 120 s added at 100,000 calls, and 7.0 times the build without the generator | `test/ColdBuild/budgets.json` |
| Mock generator, cold run | 2,500 ms | `test/Generator.Benchmarks/mock-budgets.json` |
| Mock generator, run after an unrelated edit | 1,000 ms | `test/Generator.Benchmarks/mock-budgets.json` |
| Mock generator, compile of the generated code | 4,500 ms | `test/Generator.Benchmarks/mock-budgets.json` |

A step over its budget still runs every case, then fails and reports the overrun.

| Campaign run | Cases | Limit |
|---|---|---|
| `expressions` | 3,000 | 30 minutes |
| `mock-sequences` | 100,000 | 15 minutes |
| `mock-invariants` | 20,000 | 15 minutes |
| `mutations` | Every mutation | 60 minutes |

The campaign job has a timeout of 150 minutes, and its artifacts are kept for 30 days. A local run of the whole campaign on 2026-11-21 took 724 s and passed. A run that reaches its limit stops, reports the overrun and counts as incomplete.
