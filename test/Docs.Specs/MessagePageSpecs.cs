using System.Text.RegularExpressions;
using Messages;
using VerificationGlue;

namespace Docs.Specs;

/// <summary>
/// <c>docs/guide/messages.md</c> shows the real output of <c>samples/Messages</c>. Each message on
/// the page sits in a block marked with the name of its case in <see cref="Gallery"/>. Run with
/// <c>VERIFICATIONGLUE_ACCEPT_MESSAGES=1</c> to write the fresh messages into the page.
/// </summary>
public partial class MessagePageSpecs
{
    private const string AcceptVariable = "VERIFICATIONGLUE_ACCEPT_MESSAGES";

    private static readonly string Page = Path.Combine(RepositoryRoot.Path, "docs", "guide", "messages.md");

    [Fact]
    public void Every_case_has_one_block_and_every_block_has_a_case()
    {
        var blocks = Block().Matches(Read()).Select(match => match.Groups["name"].Value).ToList();

        Assert.Equal(Gallery.Cases.Select(@case => @case.Name).Order(), blocks.Order());
    }

    [Fact]
    public void Every_message_on_the_page_matches_a_fresh_run()
    {
        var page = Read();
        var messages = Gallery.Cases.ToDictionary(@case => @case.Name, @case => Message(@case.Run));
        var fresh = Block().Replace(page, match => messages.TryGetValue(match.Groups["name"].Value, out var message)
            ? $"<!-- message: {match.Groups["name"].Value} -->\n```text\n{message}\n```\n<!-- endMessage -->"
            : match.Value);

        if (fresh == page)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
        {
            File.WriteAllText(Page, fresh);
            return;
        }

        var stale = Block().Matches(page)
            .Where(match => messages.TryGetValue(match.Groups["name"].Value, out var message) && message != match.Groups["body"].Value)
            .Select(match => $"'{match.Groups["name"].Value}' shows:\n{match.Groups["body"].Value}\nA fresh run gives:\n{messages[match.Groups["name"].Value]}");
        Assert.Fail($"docs/guide/messages.md differs from a fresh run. Rerun with {AcceptVariable}=1 to update it.\n\n{string.Join("\n\n", stale)}");
    }

    private static string Read() => File.ReadAllText(Page).Replace("\r\n", "\n");

    private static string Message(Action run)
    {
        try
        {
            run();
        }
        catch (VerifyException failure)
        {
            return failure.Message;
        }

        throw new InvalidOperationException("The case passed. Each case in samples/Messages must fail.");
    }

    [GeneratedRegex("<!-- message: (?<name>[a-z0-9-]+) -->\n```text\n(?<body>.*?)\n```\n<!-- endMessage -->", RegexOptions.Singleline)]
    private static partial Regex Block();
}
