using System.Xml.Linq;

namespace Hardening.Reports;

// The results in a TRX file that Microsoft.Testing.Extensions.TrxReport writes. A test with rows has a
// result for each row, and each result names the method.
public sealed record TrxFile(string Outcome, IReadOnlyList<TrxResult> Results)
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static TrxFile Read(string path)
    {
        var document = XDocument.Load(path);
        var methods = document.Descendants(Trx + "UnitTest").ToDictionary(
            test => test.Attribute("id")!.Value,
            test => test.Element(Trx + "TestMethod")!);
        var results = document.Descendants(Trx + "UnitTestResult")
            .Select(result =>
            {
                var method = methods[result.Attribute("testId")!.Value];
                return new TrxResult(
                    method.Attribute("className")!.Value.Replace('+', '.'),
                    method.Attribute("name")!.Value,
                    result.Attribute("testName")!.Value,
                    Normalize(result.Attribute("outcome")?.Value),
                    result.Element(Trx + "Output")?.Element(Trx + "ErrorInfo")?.Element(Trx + "Message")?.Value)
                {
                    Assembly = method.Attribute("codeBase")?.Value is { Length: > 0 } codeBase
                        ? Path.GetFileNameWithoutExtension(codeBase.Replace('\\', '/').Split('/')[^1])
                        : null,
                };
            })
            .ToList();
        var outcome = document.Descendants(Trx + "ResultSummary").FirstOrDefault()?.Attribute("outcome")?.Value ?? "Unknown";
        return new TrxFile(outcome, results);
    }

    public bool Aborted => Outcome is "Aborted" or "Timeout" or "Unknown";

    private static string Normalize(string? outcome) => outcome switch
    {
        "Passed" => TestOutcome.Passed,
        "Failed" or "Error" or "Timeout" or "Aborted" => TestOutcome.Failed,
        _ => TestOutcome.Skipped,
    };
}

// ClassName is the full name of the class, with nested types joined by dots. Message is the failure
// message of a result that failed.
public sealed record TrxResult(string ClassName, string Method, string Name, string Outcome, string? Message = null)
{
    // The test assembly that ran the result, such as VerifyInXunit, or null when the file does not say.
    public string? Assembly { get; init; }

    // Whether the result is of Type.Method, where Type may be nested and has no namespace.
    public bool Is(string type, string method) =>
        Method == method && (ClassName == type || ClassName.EndsWith("." + type, StringComparison.Ordinal));
}

public static class TestOutcome
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string NotRun = "not run";
}
