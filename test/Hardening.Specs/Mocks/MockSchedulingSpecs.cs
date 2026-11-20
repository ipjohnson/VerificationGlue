namespace Hardening.Specs.Mocks;

/// <summary>
/// The concurrency cases of step 3. The interpreter runs another flow's statement at a chosen point
/// of a statement with semaphores, not with timing, so each case runs one ordering every time.
/// </summary>
[Trait("Step", "3")]
public class MockSchedulingSpecs
{
    private static readonly Response Five = new(ResponseKind.Value, ["5"]);

    [VerifyTest]
    public async Task Every_legal_interleaving_of_a_statement_pair_matches_the_reference_model()
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

    [VerifyTest]
    public async Task A_call_from_another_flow_before_the_checked_call_is_recorded_and_counted()
    {
        var check = new Check(Get(ArgSpec.Exact("5"), ArgSpec.Exact("5")), 1, 1);
        var sequence = new Sequence("Checked", [new Interleave(check, Point.BeforeCall, false, new Invoke(Get(ArgSpec.Exact("5"), ArgSpec.Exact("5")))), new Snapshot(0, false)]);

        var observations = await Interpreter.RunAsync(sequence);

        Assert.Equal(["check passes", "other flow: 0", "calls [Get(5, 5)]"], observations);
    }

    [VerifyTest]
    public async Task A_call_from_another_flow_between_a_setup_call_and_Returns_is_not_answered_by_that_setup()
    {
        var site = Get(ArgSpec.Any, ArgSpec.Exact("5"));
        foreach (var awaits in new[] { false, true })
        {
            var sequence = new Sequence("Setup", [
                new Interleave(new SetupNs(site, Five), Point.BeforeReturns, awaits, new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("5")))),
                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("5"))),
                new Snapshot(0, false),
            ]);

            var observations = await Interpreter.RunAsync(sequence);

            Assert.Equal(["set up", "other flow: 0", "5", "calls [Get(1, 5), Get(1, 5)]"], observations);
        }
    }

    /// <summary>
    /// Each statement A in this flow with each statement B of another flow, at each point where CD-04
    /// makes the ordering legal, on the same mock and on another one. A call before and after lets the
    /// checks count something and shows the state each ordering leaves.
    /// </summary>
    private static IEnumerable<Sequence> Interleavings()
    {
        Op[] firsts =
        [
            new SetupNs(Get(ArgSpec.Any, ArgSpec.Exact("2")), Five),
            new SetupNs(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")), new Response(ResponseKind.Sequence, ["6", "7"])),
            new SetupNs(Get(ArgSpec.Exact("1"), new ArgSpec(ArgKind.Pred, null, Pred.Even)), new Response(ResponseKind.Throws, [], 1)),
            new Check(Get(ArgSpec.Exact("1"), ArgSpec.Any), 1, int.MaxValue),
            new Check(Get(ArgSpec.Any, ArgSpec.Exact("2")), 0, 0),
            new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2")), 2, 2),
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
                new SetupTyped(Get(ArgSpec.Any, ArgSpec.Exact("2"), mock), new Response(ResponseKind.Value, ["9"])),
                new When(Get(ArgSpec.Exact("1"), ArgSpec.Any, mock), new Callback(2)),
                new Check(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"), mock), 1, int.MaxValue),
                new CountCalls(Get(ArgSpec.Any, ArgSpec.Exact("2"), mock)),
            ];
            foreach (var first in firsts)
            {
                foreach (var other in others)
                {
                    foreach (var (at, awaits) in new[] { (Point.BeforeCall, false), (Point.BeforeReturns, false), (Point.BeforeReturns, true) })
                    {
                        var legal = at == Point.BeforeCall || (first is SetupNs && other is not (Check or CountCalls));
                        if (legal)
                        {
                            yield return new Sequence($"Interleaving_{index++}", [
                                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
                                new Interleave(first, at, awaits, other),
                                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"))),
                                new Invoke(Get(ArgSpec.Exact("3"), ArgSpec.Exact("2"))),
                                new Invoke(Get(ArgSpec.Exact("1"), ArgSpec.Exact("2"), 1)),
                                new Snapshot(0, false),
                                new Snapshot(1, false),
                            ]);
                        }
                    }
                }
            }
        }
    }

    private static Site Get(ArgSpec a, ArgSpec b, int mock = 0) => new(Member.Get2, mock, [a, b]);
}
