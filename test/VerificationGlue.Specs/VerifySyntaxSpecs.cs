using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerificationGlue.Specs;

// This project does not run the generator, so every call here gives a tier 1 message.
public class VerifySyntaxSpecs
{
    [Fact]
    public void Verify_passes_when_the_condition_holds()
    {
        var count = 3;

        Verify(count == 3);
    }

    [Fact]
    public void Verify_reports_the_source_text()
    {
        #region tier-1-message
        var count = 2;

        var failure = Assert.Throws<VerifyException>(() => Verify(count == 3));

        Assert.Equal("Verify failed: count == 3", failure.Message);
        #endregion
    }

    [Fact]
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

    [Fact]
    public void Throws_returns_the_exception()
    {
        var thrown = Throws<InvalidOperationException>(() => throw new InvalidOperationException("closed"));

        Assert.Equal("closed", thrown.Message);
    }

    [Fact]
    public void Throws_accepts_a_derived_exception()
    {
        Throws<ArgumentException>(() => throw new ArgumentNullException("name"));
    }

    [Fact]
    public void Throws_reports_no_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => Throws<InvalidOperationException>(() => { }));

        Assert.Equal("Throws failed: () => { }\n  expected InvalidOperationException\n  no exception was thrown", failure.Message);
    }

    [Fact]
    public void Throws_reports_a_different_exception()
    {
        var failure = Assert.Throws<VerifyException>(() => Throws<InvalidOperationException>(() => throw new FormatException("bad digit")));

        Assert.Equal(
            "Throws failed: () => throw new FormatException(\"bad digit\")\n  expected InvalidOperationException\n  it threw System.FormatException: bad digit",
            failure.Message);
        Assert.IsType<FormatException>(failure.InnerException);
    }

    [Fact]
    public async Task ThrowsAsync_returns_the_exception()
    {
        var thrown = await ThrowsAsync<TimeoutException>(async () =>
        {
            await Task.Yield();
            throw new TimeoutException("slow");
        });

        Assert.Equal("slow", thrown.Message);
    }

    [Fact]
    public async Task ThrowsAsync_reports_no_exception()
    {
        var failure = await Assert.ThrowsAsync<VerifyException>(() => ThrowsAsync<TimeoutException>(() => Task.CompletedTask));

        Assert.Equal("ThrowsAsync failed: () => Task.CompletedTask\n  expected TimeoutException\n  no exception was thrown", failure.Message);
    }
}
