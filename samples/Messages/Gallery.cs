namespace Messages;

/// <summary>
/// Every case in <c>docs/guide/messages.md</c>, by the name its block uses. Each case fails on
/// purpose. <c>test/Docs.Specs/MessagePageSpecs.cs</c> runs them and compares the messages.
/// </summary>
public static class Gallery
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("tier2-equal", Tier2Messages.Equal),
        ("tier2-not-null", Tier2Messages.NotNull),
        ("tier2-relational", Tier2Messages.Relational),
        ("tier2-bool-call", Tier2Messages.BoolCall),
        ("tier2-pattern", Tier2Messages.Pattern),
        ("tier2-with-message", Tier2Messages.WithMessage),
        ("tier2-throws", Tier2Messages.Throws),
        ("tier2-throws-exactly", Tier2Messages.ThrowsExactly),
        ("tier3-values", Tier3Messages.Values),
        ("tier3-skipped", Tier3Messages.Skipped),
        ("tier3-because", Tier3Messages.Because),
        ("tier3-pattern", Tier3Messages.Pattern),
        ("check-tier2", CheckMessages.Tier2),
        ("check-tier3", CheckMessages.Tier3),
    ];
}
