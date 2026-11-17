using System.Xml.Linq;

namespace VerificationGlue.Tests.Specs;

/// <summary>Runs every fixture test with <c>--report-trx</c> and reads the results by test name.</summary>
internal static class TrxReport
{
    public static Dictionary<string, TrxResult> Run()
    {
        var directory = Directory.CreateTempSubdirectory("vg-trx-");
        try
        {
            var result = Fixture.Run("--report-trx", "--report-trx-filename", "fixture.trx", "--results-directory", directory.FullName);
            Assert.True(result.ExitCode == 2, result.Output);

            XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var document = XDocument.Load(Path.Combine(directory.FullName, "fixture.trx"));
            var classes = document.Descendants(trx + "UnitTest").ToDictionary(
                test => test.Attribute("id")!.Value,
                test => test.Element(trx + "TestMethod")!.Attribute("className")!.Value);

            return document.Descendants(trx + "UnitTestResult").ToDictionary(
                test => test.Attribute("testName")!.Value,
                test => new TrxResult(
                    test.Attribute("outcome")!.Value,
                    classes[test.Attribute("testId")!.Value],
                    test.Descendants(trx + "Message").FirstOrDefault()?.Value ?? "",
                    test.Descendants(trx + "StackTrace").FirstOrDefault()?.Value ?? "",
                    test.Descendants(trx + "StdOut").FirstOrDefault()?.Value.Replace("\r\n", "\n") ?? ""));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

internal sealed record TrxResult(string Outcome, string ClassName, string Message, string StackTrace, string Output);
