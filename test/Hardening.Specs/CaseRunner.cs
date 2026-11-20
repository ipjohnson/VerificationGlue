using System.Text.RegularExpressions;
using VerificationGlue;

namespace Hardening.Specs;

/// <summary>What one run showed: its outcome, the probe's trace and final state, and the probe calls it skipped.</summary>
public sealed record Observation(string Outcome, IReadOnlyList<string> Trace, string State, IReadOnlyList<int> Skipped)
{
    public override string ToString() =>
        $"outcome {Outcome}, trace [{string.Join(", ", Trace)}], {State}, skipped [{string.Join(", ", Skipped)}]";
}

public enum Classification
{
    /// <summary>The form kept plain C# behavior under the tier it expects.</summary>
    Passed,

    /// <summary>The form kept plain C# behavior under the tier 2 fallback that a documented rule gives it.</summary>
    Fallback,

    Mismatch,
}

/// <summary>
/// The comparison of one form of one expression with the plain C# baseline. <see cref="MismatchKind"/>
/// is outcome, trace, state, skipped, tier or message, or null when they all match.
/// </summary>
public sealed record FormResult(
    ExpressionCase Case,
    Form Form,
    Expectation Expected,
    Tier? Ran,
    Observation Baseline,
    Observation Actual,
    string? Message,
    string? MismatchKind,
    string? Detail)
{
    public Classification Classification =>
        MismatchKind is not null ? Classification.Mismatch : Expected.Fallback is not null ? Classification.Fallback : Classification.Passed;
}

public static class CaseRunner
{
    /// <summary>
    /// Compiles <paramref name="cases"/> in one batch and compares every form of each with its baseline.
    /// <paramref name="assertedText"/> changes the expression inside the assertions only, which lets a
    /// spec check that the comparison finds a change to evaluation.
    /// </summary>
    public static async Task<IReadOnlyList<FormResult>> RunAsync(
        IReadOnlyList<ExpressionCase> cases,
        GeneratorMode mode = GeneratorMode.Rewrite,
        Func<string, string>? assertedText = null)
    {
        var source = new CaseSource(cases, assertedText);
        using var batch = CaseCompiler.Compile(source, mode);
        var results = new List<FormResult>();
        foreach (var expression in cases)
        {
            var baselines = new Dictionary<string, Observation>();
            foreach (var form in Forms.For(expression))
            {
                var shape = Forms.Baseline(form);
                if (!baselines.TryGetValue(shape, out var baseline))
                {
                    baseline = baselines[shape] = await RunBaselineAsync(batch, expression, shape);
                }

                results.Add(await RunFormAsync(batch, expression, form, baseline, assertedText?.Invoke(expression.Text) ?? expression.Text));
            }
        }

        return results;
    }

    private static async Task<Observation> RunBaselineAsync(CompiledBatch batch, ExpressionCase expression, string shape)
    {
        var probe = new Probe();
        Exception? thrown = null;
        try
        {
            await Invoke(batch.Baseline(expression.Name, shape, expression.IsAsync), probe);
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        return Observe(expression, probe, Outcome(thrown, probe.Failures));
    }

    private static async Task<FormResult> RunFormAsync(CompiledBatch batch, ExpressionCase expression, Form form, Observation baseline, string asserted)
    {
        var probe = new Probe();
        var check = Forms.IsCheck(form);
        var scope = check ? VerifyScope.Begin() : null;
        var failures = 0;
        Exception? thrown = null;
        string? message = null;
        try
        {
            await Invoke(batch.Form(expression.Name, form, expression.IsAsync), probe);
        }
        catch (VerifyException exception) when (!check)
        {
            failures = 1;
            message = exception.Message;
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        if (scope is not null)
        {
            var recorded = scope.Failures;
            try
            {
                scope.Dispose();
            }
            catch (VerifyException)
            {
            }

            failures = recorded.Count;
            message = recorded.Count > 0 ? recorded[0].Message : null;
        }

        var actual = Observe(expression, probe, Outcome(thrown, failures));
        var expected = Forms.Expect(form, expression);
        var site = batch.Source.Site(expression.Name, form);
        var ran = TierOf(probe.Evaluator, batch.IsIntercepted(site));
        var (kind, detail) = Compare(baseline, actual, expected, ran, message, check, asserted, site.Line);
        if (kind == "tier")
        {
            detail += $", from the first probe call in {probe.Evaluator?.Type}.{probe.Evaluator?.Method}";
        }

        return new FormResult(expression, form, expected, ran, baseline, actual, message, kind, detail);
    }

    private static (string?, string?) Compare(Observation baseline, Observation actual, Expectation expected, Tier? ran, string? message, bool check, string asserted, int line)
    {
        if (actual.Outcome != baseline.Outcome)
        {
            return ("outcome", $"plain C# gives {baseline.Outcome}, the assertion gives {actual.Outcome}");
        }

        if (!actual.Trace.SequenceEqual(baseline.Trace))
        {
            return ("trace", $"plain C# records [{string.Join(", ", baseline.Trace)}], the assertion records [{string.Join(", ", actual.Trace)}]");
        }

        if (actual.State != baseline.State)
        {
            return ("state", $"plain C# ends with {baseline.State}, the assertion ends with {actual.State}");
        }

        if (!actual.Skipped.SequenceEqual(baseline.Skipped))
        {
            return ("skipped", $"plain C# skips [{string.Join(", ", baseline.Skipped)}], the assertion skips [{string.Join(", ", actual.Skipped)}]");
        }

        if (ran != expected.Tier)
        {
            return ("tier", $"expected {Forms.Describe(expected.Tier)}, ran under {(ran is { } tier ? Forms.Describe(tier) : "no known tier")}");
        }

        if (message is not null && MessageProblem(message, check, asserted, ran.Value, line) is { } problem)
        {
            return ("message", problem);
        }

        return (null, null);
    }

    private static string? MessageProblem(string message, bool check, string asserted, Tier ran, int line)
    {
        var lines = message.Split('\n');
        var first = $"{(check ? "Check" : "Verify")} failed: {asserted}";
        if (lines[0] != first)
        {
            return $"the first line is '{lines[0]}', not '{first}'";
        }

        var location = $"  at Cases.cs:{line}";
        var shown = lines.Any(text => text.Contains(" → ", StringComparison.Ordinal)) ? Tier.Tier3 : lines[^1].StartsWith("  at ", StringComparison.Ordinal) ? Tier.Tier2 : Tier.Tier1;
        var ranShape = ran == Tier.Tier3Lambda ? Tier.Tier3 : ran;
        if (shown != ranShape)
        {
            return $"the message has the shape of {Forms.Describe(shown)}, but {Forms.Describe(ran)} ran: {message}";
        }

        return ran != Tier.Tier1 && lines[^1] != location ? $"the last line is '{lines[^1]}', not '{location}'" : null;
    }

    /// <summary>
    /// The tier that ran, from the frame that made the first probe call. A lambda check runs a copy of
    /// the body in the file class <c>VerifyLambdas0</c>. A tier 3 copy is a method named
    /// <c>{Name}__Verified</c>, and a lambda or local function inside it gives tier 2 messages. Elsewhere
    /// the call gives tier 2 when an interceptor replaced it, and tier 1 when none did.
    /// </summary>
    public static Tier? TierOf((string Type, string Method)? evaluator, bool intercepted)
    {
        if (evaluator is not var (type, method))
        {
            return null;
        }

        if (Regex.IsMatch(type, @"VerifyLambdas\d+$"))
        {
            return Tier.Tier3Lambda;
        }

        var name = method;
        if (method == "MoveNext")
        {
            var nested = type[(type.LastIndexOf('+') + 1)..];
            var stateMachine = Regex.Match(nested, @"^<(\w+)>d__\d+$");
            name = stateMachine.Success ? stateMachine.Groups[1].Value : nested;
        }

        if (Regex.IsMatch(name, @"^\w+__Verified$"))
        {
            return Tier.Tier3;
        }

        return name.Contains("__Verified", StringComparison.Ordinal) || intercepted ? Tier.Tier2 : Tier.Tier1;
    }

    private static Observation Observe(ExpressionCase expression, Probe probe, string outcome)
    {
        var ran = probe.Trace.Select(item => Regex.Match(item, @"(\d+)$")).Where(match => match.Success).Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
        return new Observation(outcome, [.. probe.Trace], probe.FinalState, [.. expression.Ids.Where(id => !ran.Contains(id))]);
    }

    private static Task Invoke(Delegate run, Probe probe)
    {
        if (run is Func<Probe, Task> async)
        {
            return async(probe);
        }

        ((Action<Probe>)run)(probe);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The outcome in the same words for both sides: passed, failed, failed twice, or the exception,
    /// with the number of false results before it.
    /// </summary>
    private static string Outcome(Exception? thrown, int failures)
    {
        var outcome = (thrown, failures) switch
        {
            (null, 0) => "passed",
            (null, 1) => "failed",
            (null, _) => $"failed {failures} times",
            _ => $"throws {thrown.GetType().FullName}: {thrown.Message}",
        };

        return thrown is not null && failures > 0 ? $"{outcome} after {failures} false results" : outcome;
    }
}
