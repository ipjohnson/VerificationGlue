using Hardening.Specs.Mocks;
using VerificationGlue;
using VerificationGlue.Mocks;
using static VerificationGlue.VerifySyntax;

namespace Hardening.Specs.Interactions;

/// <summary>
/// MIX-001 of docs/design/hardening.md: an awaited mock result inside a tier 3 assertion with
/// <c>&amp;&amp;</c>, <c>||</c> and <c>?:</c>. Each mock call records its stack, so a case also
/// checks that the call came from a <c>__Verified</c> copy.
/// </summary>
[Trait("Step", "5")]
public partial class AwaitedMockResultSpecs
{
    [VerifyTest]
    public async Task A_passing_assertion_calls_the_awaited_member_once_and_skips_the_other_branch()
    {
        var stacks = new List<string>();
        var service = Recording(stacks);

        Verify(await service.LoadAsync(1) == 5 || await service.LoadAsync(2) == 0);
        Verify(!(await service.LoadAsync(3) == 1 && await service.PeekAsync(1) == 0));
        Verify(await service.PeekAsync(2) == 0 ? await service.LoadAsync(4) == 0 : await service.LoadAsync(5) == 9);

        Verify(service.Calls().LoadAsync(1) == 1);
        Verify(service.Calls().LoadAsync(2) == 0);
        Verify(service.Calls().LoadAsync(3) == 1);
        Verify(service.Calls().PeekAsync(1) == 0);
        Verify(service.Calls().PeekAsync(2) == 1);
        Verify(service.Calls().LoadAsync(4) == 1);
        Verify(service.Calls().LoadAsync(5) == 0);
        Verify(stacks.Count == 1 && stacks[0].Contains("__Verified", StringComparison.Ordinal));
    }

    [VerifyTest]
    public async Task A_failing_assertion_reports_the_awaited_value_without_calling_the_member_again()
    {
        var stacks = new List<string>();
        var service = Recording(stacks);

        var failure = await ThrowsAsync<VerifyException>(() => VerifyLoads(service));

        Verify(service.Calls().LoadAsync(1) == 1);
        Verify(service.Calls().LoadAsync(2) == 0);
        Verify(stacks.Count == 1 && stacks[0].Contains("__Verified", StringComparison.Ordinal));
        Verify(failure.Message.StartsWith("Verify failed: await service.LoadAsync(1) == 4 && await service.LoadAsync(2) == 4\n", StringComparison.Ordinal));
        Verify(Value(failure.Message, "await service.LoadAsync(1) == 4") == "false");
        Verify(Value(failure.Message, "await service.LoadAsync(1)") == "5");
        Verify(Value(failure.Message, "await service.LoadAsync(2) == 4") == "<not evaluated>");
        Verify(Value(failure.Message, "await service.LoadAsync(2)") == "<not evaluated>");
    }

    [Verify]
    private static async Task VerifyLoads(IService service)
    {
        Verify(await service.LoadAsync(1) == 4 && await service.LoadAsync(2) == 4);
    }

    /// <summary>A mock whose <c>LoadAsync(1)</c> gives 5 and records the stack of each call to it.</summary>
    private static IService Recording(List<string> stacks)
    {
        var service = Mock.Of<IService>();
        service.LoadAsync(1).Returns(_ =>
        {
            stacks.Add(Environment.StackTrace);
            return Task.FromResult(5);
        });
        return service;
    }

    /// <summary>The value a tier 3 message shows for a part of the condition, from its line <c>  part → value</c>.</summary>
    private static string? Value(string message, string part) => message
        .Split('\n')
        .Select(line => line.Split(" → ", 2))
        .Where(pieces => pieces.Length == 2 && pieces[0].Trim() == part)
        .Select(pieces => pieces[1])
        .FirstOrDefault();
}
