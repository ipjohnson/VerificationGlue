using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerificationGlue.Specs;

// This project does not run the generator, so every call here gives a tier 1 message.
public class VerifyScopeSpecs
{
    [VerifyTest]
    public void Check_failures_are_reported_together_when_the_scope_ends()
    {
        var count = 2;
        var name = "a";

        var scope = VerifyScope.Begin();
        Check(count == 3);
        Check(name == "a");
        Check(name.Length > 1);
        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.Equal("2 checks failed\n\nCheck failed: count == 3\n\nCheck failed: name.Length > 1", failure.Message);
    }

    [VerifyTest]
    public void Check_lets_the_code_after_it_run()
    {
        var reached = false;

        var scope = VerifyScope.Begin();
        Check(false);
        reached = true;

        Assert.Throws<VerifyException>(scope.Dispose);
        Assert.True(reached);
    }

    [VerifyTest]
    public Task A_scope_with_no_failures_ends_quietly() => OutsideTheRunnersScope(() =>
    {
        using (VerifyScope.Begin())
        {
            Check(1 + 1 == 2);
        }

        Assert.Null(VerifyScope.Current);
    });

    [VerifyTest]
    public Task Check_with_no_scope_throws_like_Verify() => OutsideTheRunnersScope(() =>
    {
        var failure = Assert.Throws<VerifyException>(() => Check(1 > 2));

        Assert.Equal("Check failed: 1 > 2", failure.Message);
    });

    [VerifyTest]
    public void CheckEquivalent_records_its_failure_in_the_scope_and_continues()
    {
        var actual = new Point(1, 2);
        var reached = false;

        var scope = VerifyScope.Begin();
        CheckEquivalent(actual, new Point(1, 3));
        CheckEquivalent(actual, new Point(1, 2));
        reached = true;
        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.True(reached);
        Assert.Equal(
            "1 check failed\n\n" +
            "CheckEquivalent failed: actual is not equivalent to new Point(1, 3)\n" +
            "  expected Point { X = 1, Y = 3 }, found Point { X = 1, Y = 2 }\n" +
            "  compared with Equals, because the VerificationGlue generator did not run",
            failure.Message);
    }

    [VerifyTest]
    public Task CheckEquivalent_with_no_scope_throws_like_VerifyEquivalent() => OutsideTheRunnersScope(() =>
    {
        var actual = new Point(1, 2);

        var failure = Assert.Throws<VerifyException>(() => CheckEquivalent(actual, new Point(1, 3), options => options));

        Assert.StartsWith("CheckEquivalent failed: actual is not equivalent to new Point(1, 3)\n", failure.Message);
    });

    [VerifyTest]
    public void An_inner_scope_reports_its_own_failures_and_restores_the_outer_scope()
    {
        var outer = VerifyScope.Begin();
        var inner = VerifyScope.Begin();
        Check(false);

        var failure = Assert.Throws<VerifyException>(inner.Dispose);

        Assert.Same(outer, VerifyScope.Current);
        Assert.Equal("1 check failed\n\nCheck failed: false", failure.Message);
        Assert.Empty(outer.Failures);
        outer.Dispose();
    }

    [VerifyTest]
    public async Task A_check_in_an_awaited_method_reports_to_the_scope()
    {
        var scope = VerifyScope.Begin();
        await CheckLater(0);

        Assert.Single(scope.Failures);
        Assert.Throws<VerifyException>(scope.Dispose);
    }

    [VerifyTest]
    public void Ending_a_scope_twice_reports_once()
    {
        var scope = VerifyScope.Begin();
        Check(false);
        Assert.Throws<VerifyException>(scope.Dispose);

        scope.Dispose();
    }

    [VerifyTest]
    public void Verify_still_throws_inside_a_scope()
    {
        using var scope = VerifyScope.Begin();

        Assert.Throws<VerifyException>(() => Verify(false));
        Assert.Empty(scope.Failures);
    }

    [VerifyTest]
    public void Single_Is_and_NotNull_throw_inside_a_scope_and_record_nothing()
    {
        List<int> numbers = [];
        object value = "text";
        string? name = null;
        int? count = null;

        using var scope = VerifyScope.Begin();

        Assert.Throws<VerifyException>(() => Single(numbers));
        Assert.Throws<VerifyException>(() => Single(numbers, n => n > 0));
        Assert.Throws<VerifyException>(() => Is<int>(value));
        Assert.Throws<VerifyException>(() => NotNull(name));
        Assert.Throws<VerifyException>(() => NotNull(count));
        Assert.Empty(scope.Failures);
    }

    // VerificationGlue.Tests runs each test inside a VerifyScope. The body runs without the
    // execution context, so it starts with no scope.
    private static Task OutsideTheRunnersScope(Action body)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(body);
        }
    }

    private static async Task CheckLater(int value)
    {
        await Task.Yield();
        Check(value > 0);
    }

    private sealed record Point(int X, int Y);
}
