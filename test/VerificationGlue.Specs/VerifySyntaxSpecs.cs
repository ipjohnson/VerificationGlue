using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerificationGlue.Specs;

// This project does not run the generator, so every call here gives a tier 1 message.
public class VerifySyntaxSpecs
{
    [VerifyTest]
    public void Verify_passes_when_the_condition_holds()
    {
        var count = 3;

        Verify(count == 3);
    }

    [VerifyTest]
    public void Verify_reports_the_source_text()
    {
        #region tier-1-message
        var count = 2;

        var failure = Assert.Throws<VerifyException>(() => Verify(count == 3));

        Assert.Equal("Verify failed: count == 3", failure.Message);
        #endregion
    }

    [VerifyTest]
    public void Message_holes_run_only_on_failure()
    {
        var runs = 0;
        string Hole()
        {
            runs++;
            return "hole";
        }

        Verify(true, $"{Hole()}");
        Assert.Equal(0, runs);

        var failure = Assert.Throws<VerifyException>(() => Verify(runs == 1, $"runs is {runs}, {Hole()} {3.5:F2}"));

        Assert.Equal(1, runs);
        Assert.Equal("Verify failed: runs == 1\n  runs is 0, hole 3.50", failure.Message);
    }

    [VerifyTest]
    public void Throws_returns_the_exception()
    {
        var thrown = Throws<InvalidOperationException>(() => throw new InvalidOperationException("closed"));

        Assert.Equal("closed", thrown.Message);
    }

    [VerifyTest]
    public void Throws_accepts_a_derived_exception()
    {
        Throws<ArgumentException>(() => throw new ArgumentNullException("name"));
    }

    [VerifyTest]
    public void Throws_reports_no_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => Throws<InvalidOperationException>(() => { }));

        Assert.Equal("Throws failed: () => { }\n  expected InvalidOperationException\n  no exception was thrown", failure.Message);
    }

    [VerifyTest]
    public void Throws_reports_a_different_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => Throws<InvalidOperationException>(() => throw new FormatException("bad digit")));

        Assert.Equal(
            "Throws failed: () => throw new FormatException(\"bad digit\")\n  expected InvalidOperationException\n  it threw System.FormatException: bad digit",
            failure.Message);
        Assert.IsType<FormatException>(failure.InnerException);
    }

    [VerifyTest]
    public async Task ThrowsAsync_returns_the_exception()
    {
        var thrown = await ThrowsAsync<TimeoutException>(async () =>
        {
            await Task.Yield();
            throw new TimeoutException("slow");
        });

        Assert.Equal("slow", thrown.Message);
    }

    [VerifyTest]
    public async Task ThrowsAsync_reports_no_exception()
    {
        var failure = await Assert.ThrowsAsync<VerifyException>(() => ThrowsAsync<TimeoutException>(() => Task.CompletedTask));

        Assert.Equal("ThrowsAsync failed: () => Task.CompletedTask\n  expected TimeoutException\n  no exception was thrown", failure.Message);
    }

    [VerifyTest]
    public void ThrowsExactly_returns_the_exact_exception()
    {
        var thrown = ThrowsExactly<ArgumentException>(() => throw new ArgumentException("bad"));

        Assert.Equal("bad", thrown.Message);
    }

    [VerifyTest]
    public void ThrowsExactly_names_a_derived_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException("name")));

        Assert.Equal(
            "ThrowsExactly failed: () => throw new ArgumentNullException(\"name\")\n  expected exactly ArgumentException, but ArgumentNullException was thrown",
            failure.Message);
    }

    [VerifyTest]
    public void ThrowsExactly_reports_an_unrelated_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => ThrowsExactly<InvalidOperationException>(() => throw new FormatException("bad digit")));

        Assert.Equal(
            "ThrowsExactly failed: () => throw new FormatException(\"bad digit\")\n  expected InvalidOperationException\n  it threw System.FormatException: bad digit",
            failure.Message);
    }

    [VerifyTest]
    public void ThrowsExactly_reports_no_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => ThrowsExactly<InvalidOperationException>(() => { }));

        Assert.Equal("ThrowsExactly failed: () => { }\n  expected InvalidOperationException\n  no exception was thrown", failure.Message);
    }

    [VerifyTest]
    public async Task ThrowsExactlyAsync_returns_the_exact_exception()
    {
        var thrown = await ThrowsExactlyAsync<TimeoutException>(() => Task.FromException(new TimeoutException("slow")));

        Assert.Equal("slow", thrown.Message);
    }

    [VerifyTest]
    public async Task ThrowsExactlyAsync_names_a_derived_exception()
    {
        var failure = await Assert.ThrowsAsync<VerifyException>(() => ThrowsExactlyAsync<ArgumentException>(() => Task.FromException(new ArgumentNullException("name"))));

        Assert.Equal(
            "ThrowsExactlyAsync failed: () => Task.FromException(new ArgumentNullException(\"name\"))\n  expected exactly ArgumentException, but ArgumentNullException was thrown",
            failure.Message);
    }

    [VerifyTest]
    public async Task ThrowsExactlyAsync_reports_an_unrelated_exception()
    {
        var failure = await Assert.ThrowsAsync<VerifyException>(() => ThrowsExactlyAsync<TimeoutException>(() => Task.FromException(new FormatException("bad digit"))));

        Assert.Equal(
            "ThrowsExactlyAsync failed: () => Task.FromException(new FormatException(\"bad digit\"))\n  expected TimeoutException\n  it threw System.FormatException: bad digit",
            failure.Message);
    }

    [VerifyTest]
    public async Task ThrowsExactlyAsync_reports_no_exception()
    {
        var failure = await Assert.ThrowsAsync<VerifyException>(() => ThrowsExactlyAsync<TimeoutException>(() => Task.CompletedTask));

        Assert.Equal("ThrowsExactlyAsync failed: () => Task.CompletedTask\n  expected TimeoutException\n  no exception was thrown", failure.Message);
    }
}
