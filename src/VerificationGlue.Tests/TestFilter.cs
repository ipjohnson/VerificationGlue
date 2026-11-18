using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace VerificationGlue.Tests;

/// <summary>
/// The expression that <c>--vg-filter</c> takes. It is clauses separated by <c>|</c>, and a test matches
/// when any clause matches. A clause is terms separated by <c>&amp;</c>, and matches when every term matches.
/// A term is <c>name:</c>, <c>trait:</c> or <c>group:</c> and a pattern, with <c>!</c> in front to negate it.
/// In a pattern, <c>*</c> matches any run of characters. Matching is case-sensitive.
/// </summary>
internal sealed class TestFilter
{
    public const string OptionName = "vg-filter";

    private readonly Term[][] _clauses;

    private TestFilter(Term[][] clauses) => _clauses = clauses;

    /// <summary>Parses <paramref name="expression"/>, or returns null and says why in <paramref name="error"/>.</summary>
    public static TestFilter? TryParse(string expression, out string error)
    {
        var clauses = new List<Term[]>();
        foreach (var clause in expression.Split('|'))
        {
            var terms = new List<Term>();
            foreach (var text in clause.Split('&'))
            {
                if (Term.TryParse(text.Trim(), out var term, out error))
                {
                    terms.Add(term!);
                }
                else
                {
                    error = $"The --{OptionName} value '{expression}' is not valid: {error}";
                    return null;
                }
            }

            clauses.Add([.. terms]);
        }

        error = "";
        return new TestFilter([.. clauses]);
    }

    public bool Matches(TestDescriptor test) => _clauses.Any(clause => clause.All(term => term.Matches(test)));

    private sealed class Term(string kind, string pattern, string? traitValue, bool negated)
    {
        public static bool TryParse(string text, out Term? term, out string error)
        {
            term = null;
            var negated = text.StartsWith('!');
            var body = negated ? text[1..].TrimStart() : text;
            if (body.Length == 0)
            {
                error = "a term is empty. Write terms such as name:Checkout*, trait:Category=Slow or group:database, joined by & or |.";
                return false;
            }

            var colon = body.IndexOf(':');
            var kind = colon < 0 ? body : body[..colon];
            if (colon < 0 || kind is not ("name" or "trait" or "group"))
            {
                error = $"the term '{text}' does not start with name:, trait: or group:.";
                return false;
            }

            var pattern = body[(colon + 1)..].Trim();
            string? traitValue = null;
            if (kind == "trait")
            {
                var equals = pattern.IndexOf('=');
                if (equals >= 0)
                {
                    traitValue = pattern[(equals + 1)..].Trim();
                    pattern = pattern[..equals].Trim();
                }
            }

            if (pattern.Length == 0)
            {
                error = $"the term '{text}' has no {(kind == "trait" ? "trait name" : kind)} after '{kind}:'.";
                return false;
            }

            term = new Term(kind, pattern, traitValue, negated);
            error = "";
            return true;
        }

        public bool Matches(TestDescriptor test) => negated != kind switch
        {
            "name" => Wildcard(pattern, test.MethodName) || Wildcard(pattern, test.FullTypeName + "." + test.MethodName) || Wildcard(pattern, test.DisplayName),
            "group" => Wildcard(pattern, test.ScheduleGroup),
            _ => test.Traits.Any(trait => Wildcard(pattern, trait.Key) && (traitValue is null || Wildcard(traitValue, trait.Value))),
        };
    }

    /// <summary>True when <paramref name="text"/> matches <paramref name="pattern"/>, where <c>*</c> matches any run of characters.</summary>
    internal static bool Wildcard(string pattern, string text)
    {
        var parts = pattern.Split('*');
        if (parts.Length == 1)
        {
            return string.Equals(pattern, text, StringComparison.Ordinal);
        }

        if (!text.StartsWith(parts[0], StringComparison.Ordinal))
        {
            return false;
        }

        var position = parts[0].Length;
        for (var index = 1; index < parts.Length - 1; index++)
        {
            var found = text.IndexOf(parts[index], position, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            position = found + parts[index].Length;
        }

        var last = parts[^1];
        return text.Length - position >= last.Length && text.EndsWith(last, StringComparison.Ordinal);
    }
}

/// <summary>Registers <c>--vg-filter</c> and rejects an expression that does not parse, before any test runs.</summary>
internal sealed class TestFilterOption : ICommandLineOptionsProvider
{
    public string Uid => "VerificationGlue.Tests.Filter";

    public string Version => "1.0.0";

    public string DisplayName => "VerificationGlue.Tests filter";

    public string Description => "Selects tests by name, trait or schedule group.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(
            TestFilter.OptionName,
            "Selects tests by name, trait or schedule group, such as 'name:Checkout*', 'trait:Category=Slow' or 'group:database & !trait:Category=Flaky'. " +
            "& joins terms that must all match, | joins alternatives, ! negates a term, and * matches any characters.",
            ArgumentArity.ExactlyOne,
            isHidden: false),
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) =>
        TestFilter.TryParse(arguments[0], out var error) is null ? ValidationResult.InvalidTask(error) : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => ValidationResult.ValidTask;
}
