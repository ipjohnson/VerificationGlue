using Hardening.Specs.Mocks;

namespace Hardening.Specs.Interactions;

/// <summary>
/// MIX-005 of docs/design/hardening.md: a child task whose check or setup is pending while another
/// flow calls the mock, with the step 3 interpreter's semaphores choosing the point. Each sequence
/// runs on the reference model and on the mocks. The orderings are the legal ones of <c>CD-04</c>:
/// the child starts between statements (rule 4), and the other flow runs a whole statement before the
/// child's mock call or between a setup's mock call and <c>Returns</c> (rule 3).
/// </summary>
[Trait("Step", "5")]
public class ChildTaskSchedulingSpecs
{
    [VerifyTest]
    public async Task A_check_pending_in_a_child_task_counts_a_call_another_flow_makes_before_it()
    {
        var sequence = new Sequence("Pending", [
            new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
            new ChildTask([new Interleave(new Check(Get(ArgSpec.Exact("1"), ArgSpec.Any), 2, 2), Point.BeforeCall, false, new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))))]),
            new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")), 2, 2),
            new Snapshot(0, false),
        ]);

        var result = await MockCampaign.RunAsync(sequence);

        Assert.True(result.Passed, result.Detail);
        Assert.Equal(["0", "check passes", "other flow: 0", "check passes", "calls [Get(1, 2), Get(1, 2)]"], result.Actual);
    }

    [VerifyTest]
    public async Task A_call_another_flow_makes_during_a_setup_in_a_child_task_is_recorded_and_not_answered_by_it()
    {
        foreach (var awaits in new[] { false, true })
        {
            var sequence = new Sequence("Setup", [
                new ChildTask([new Interleave(new SetupNs(Get(ArgSpec.Any, ArgSpec.Exact("2")), Five), Point.BeforeReturns, awaits, new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))))]),
                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
                new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")), 2, 2),
                new Snapshot(0, false),
            ]);

            var result = await MockCampaign.RunAsync(sequence);

            Assert.True(result.Passed, result.Detail);
            Assert.Equal(["set up", "other flow: 0", "5", "check passes", "calls [Get(1, 2), Get(1, 2)]"], result.Actual);
        }
    }

    [VerifyTest]
    public async Task Every_legal_interleaving_inside_a_child_task_matches_the_reference_model()
    {
        var results = new List<SequenceResult>();
        foreach (var sequence in Interleavings())
        {
            results.Add(await MockCampaign.RunAsync(sequence));
        }

        Assert.Equal(168, results.Count);
        var failures = results.Where(result => !result.Passed).Select(result => $"{result.Detail}\n{result.Sequence}").ToList();
        Assert.True(failures.Count == 0, $"{failures.Count} of {results.Count} interleavings differ from the model:\n\n" + string.Join("\n\n", failures.Take(5)));
    }

    private static readonly Response Five = new(ResponseKind.Value, ["5"]);

    /// <summary>
    /// Each pending statement A in a child task, with each statement B of another flow at each legal
    /// point, on the same mock and on the other one. The child is the whole task, a task inside a
    /// task, or one with calls and an await around A. The parent then calls, checks and lists the
    /// calls, which shows what the child and the other flow left behind.
    /// </summary>
    private static IEnumerable<Sequence> Interleavings()
    {
        Op[] pending =
        [
            new Check(Get(ArgSpec.Exact("1"), ArgSpec.Any), 1, int.MaxValue),
            new Check(Get(ArgSpec.Any, ArgSpec.Exact("2")), 0, 0),
            new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")), 2, 2),
            new SetupNs(Get(ArgSpec.Any, ArgSpec.Exact("2")), Five),
        ];
        Func<Op, Op>[] children =
        [
            interleave => new ChildTask([interleave]),
            interleave => new ChildTask([new ChildTask([interleave])]),
            interleave => new ChildTask([new Invoke(Get(ArgSpec.Exact("3"), ArgSpec.Exact("2"))), new Yield(), interleave, new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")))]),
        ];
        var index = 0;
        foreach (var mock in new[] { 0, 1 })
        {
            Op[] others =
            [
                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"), mock)),
                new Invoke(Get(ArgSpec.Exact("3"), ArgSpec.Exact("2"), mock)),
                new SetupNs(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"), mock), new Response(ResponseKind.Value, ["8"])),
                new SetupNs(Get(ArgSpec.Any, ArgSpec.Any, mock), new Response(ResponseKind.Sequence, ["3", "4"])),
                new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"), mock), 1, int.MaxValue),
            ];
            foreach (var child in children)
            {
                foreach (var first in pending)
                {
                    foreach (var other in others)
                    {
                        foreach (var (at, awaits) in new[] { (Point.BeforeCall, false), (Point.BeforeReturns, false), (Point.BeforeReturns, true) })
                        {
                            var legal = at == Point.BeforeCall || (first is SetupNs && other is not Check);
                            if (legal)
                            {
                                yield return new Sequence($"ChildInterleaving_{index++}", [
                                    new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
                                    child(new Interleave(first, at, awaits, other)),
                                    new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
                                    new Invoke(Get(ArgSpec.Exact("3"), ArgSpec.Exact("2"), 1)),
                                    new Check(Get(ArgSpec.Exact("1"), ArgSpec.Any), 1, int.MaxValue),
                                    new Snapshot(0, false),
                                    new Snapshot(1, false),
                                ]);
                            }
                        }
                    }
                }
            }
        }
    }

    private static Site Get(ArgSpec a, ArgSpec b, int mock = 0) => new(Member.Get2, mock, [a, b]);
}
