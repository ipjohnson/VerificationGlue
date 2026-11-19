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
    public void Verify_of_a_lambda_passes_when_it_returns_true()
    {
        var count = 3;

        Verify(() => count == 3);
    }

    [VerifyTest]
    public void Verify_of_a_lambda_reports_the_lambdas_body()
    {
        var count = 2;

        var failure = Assert.Throws<VerifyException>(() => Verify(() => count == 3));

        Assert.Equal("Verify failed: count == 3", failure.Message);
    }

    [VerifyTest]
    public void Check_of_a_lambda_records_a_failure_in_the_scope()
    {
        var count = 2;
        var scope = VerifyScope.Begin();

        Check(() => count == 3);

        var failure = Assert.Throws<VerifyException>(scope.Dispose);
        Assert.Equal("1 check failed\n\nCheck failed: count == 3", failure.Message);
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

    [VerifyTest]
    public void Single_returns_the_only_item()
    {
        List<string> names = ["a"];

        Assert.Equal("a", Single(names));
    }

    [VerifyTest]
    public void Single_reports_no_items()
    {
        List<int> numbers = [];

        var failure = Assert.Throws<VerifyException>(() => Single(numbers));

        Assert.Equal("Single failed: numbers\n  expected one item, but found 0", failure.Message);
    }

    [VerifyTest]
    public void Single_lists_several_items()
    {
        List<string> names = ["a", "b"];

        var failure = Assert.Throws<VerifyException>(() => Single(names));

        Assert.Equal("Single failed: names\n  expected one item, but found 2: [\"a\", \"b\"]", failure.Message);
    }

    [VerifyTest]
    public void Single_lists_the_first_ten_items()
    {
        var failure = Assert.Throws<VerifyException>(() => Single(Enumerable.Range(1, 12)));

        Assert.Equal("Single failed: Enumerable.Range(1, 12)\n  expected one item, but found 12: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, …]", failure.Message);
    }

    [VerifyTest]
    public void Single_reports_null_items()
    {
        List<int>? numbers = null;

        var failure = Assert.Throws<VerifyException>(() => Single(numbers));

        Assert.Equal("Single failed: numbers\n  expected one item, but the items were null", failure.Message);
    }

    [VerifyTest]
    public void Single_with_a_predicate_returns_the_only_match()
    {
        List<int> numbers = [1, 2, 3];

        Assert.Equal(2, Single(numbers, n => n % 2 == 0));
    }

    [VerifyTest]
    public void Single_with_a_predicate_reports_no_match()
    {
        List<int> numbers = [1, 3];

        var failure = Assert.Throws<VerifyException>(() => Single(numbers, n => n % 2 == 0));

        Assert.Equal("Single failed: numbers, n => n % 2 == 0\n  expected one match, but found 0", failure.Message);
    }

    [VerifyTest]
    public void Single_with_a_predicate_lists_several_matches()
    {
        List<int> numbers = [1, 2, 3, 4];

        var failure = Assert.Throws<VerifyException>(() => Single(numbers, n => n % 2 == 0));

        Assert.Equal("Single failed: numbers, n => n % 2 == 0\n  expected one match, but found 2: [2, 4]", failure.Message);
    }

    [VerifyTest]
    public void Single_with_a_predicate_lists_the_first_ten_matches()
    {
        var failure = Assert.Throws<VerifyException>(() => Single(Enumerable.Range(1, 30), n => n % 2 == 0));

        Assert.Equal(
            "Single failed: Enumerable.Range(1, 30), n => n % 2 == 0\n  expected one match, but found 15: [2, 4, 6, 8, 10, 12, 14, 16, 18, 20, …]",
            failure.Message);
    }

    [VerifyTest]
    public void Single_with_a_predicate_reports_null_items()
    {
        List<int>? numbers = null;

        var failure = Assert.Throws<VerifyException>(() => Single(numbers, n => n > 0));

        Assert.Equal("Single failed: numbers, n => n > 0\n  expected one match, but the items were null", failure.Message);
    }

    [VerifyTest]
    public void Is_returns_the_value_as_the_type()
    {
        object error = new ArgumentNullException("name");

        Exception typed = Is<ArgumentException>(error);

        Assert.Same(error, typed);
    }

    [VerifyTest]
    public void Is_names_the_actual_type()
    {
        object value = new List<int>();

        var failure = Assert.Throws<VerifyException>(() => Is<Dictionary<string, int>>(value));

        Assert.Equal("Is failed: value\n  expected Dictionary<String, Int32>, but it was List<Int32>", failure.Message);
    }

    [VerifyTest]
    public void Is_reports_null()
    {
        object? value = null;

        var failure = Assert.Throws<VerifyException>(() => Is<string>(value));

        Assert.Equal("Is failed: value\n  expected String, but it was null", failure.Message);
    }

    [VerifyTest]
    public void NotNull_returns_a_reference_typed_non_null()
    {
        string? name = "a";

        var found = NotNull(name);

        // Both lines compile without a nullable warning, which is an error in this repository.
        Assert.Equal(1, found.Length);
        Assert.Equal(1, name.Length);
    }

    [VerifyTest]
    public void NotNull_reports_a_null_reference()
    {
        string? name = null;

        var failure = Assert.Throws<VerifyException>(() => NotNull(name));

        Assert.Equal("NotNull failed: name", failure.Message);
    }

    [VerifyTest]
    public void NotNull_returns_the_value_of_a_nullable_value_type()
    {
        int? count = 3;

        int found = NotNull(count);

        Assert.Equal(3, found);
        Assert.Equal(3, count.Value);
    }

    [VerifyTest]
    public void NotNull_reports_a_nullable_value_type_without_a_value()
    {
        int? count = null;

        var failure = Assert.Throws<VerifyException>(() => NotNull(count));

        Assert.Equal("NotNull failed: count", failure.Message);
    }
}
