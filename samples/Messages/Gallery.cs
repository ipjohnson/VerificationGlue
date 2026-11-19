namespace Messages;

/// <summary>
/// Every case in <c>docs/guide/messages.md</c> and <c>docs/guide/verify-equivalent.md</c>, by the name its block uses. Each case fails on
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
        ("tier2-single", ValueMessages.SingleItem),
        ("tier2-single-match", ValueMessages.SingleMatch),
        ("tier2-is", ValueMessages.IsType),
        ("tier2-not-null-value", ValueMessages.NotNullValue),
        ("tier2-equivalent", EquivalentMessages.Members),
        ("tier2-equivalent-maps", EquivalentMessages.Maps),
        ("equivalent-items", EquivalentMessages.Items),
        ("equivalent-counts", EquivalentMessages.Counts),
        ("equivalent-dictionary", EquivalentMessages.Dictionary),
        ("equivalent-set", EquivalentMessages.Set),
        ("equivalent-options", EquivalentMessages.Options),
        ("tier3-values", Tier3Messages.Values),
        ("tier3-skipped", Tier3Messages.Skipped),
        ("tier3-because", Tier3Messages.Because),
        ("tier3-pattern", Tier3Messages.Pattern),
        ("tier3-tolerance", ToleranceMessages.Tolerance),
        ("tier3-string-lines", StringMessages.Lines),
        ("tier3-string-index", StringMessages.SingleLine),
        ("tier3-sequence-equal", CollectionMessages.Sequence),
        ("tier3-set-equals", CollectionMessages.Set),
        ("tier3-count", CollectionMessages.Counted),
        ("tier3-all", CollectionMessages.Every),
        ("tier3-any", CollectionMessages.Some),
        ("tier3-contains", CollectionMessages.Contained),
        ("lambda-values", LambdaMessages.Values),
        ("lambda-collection", LambdaMessages.Collection),
        ("lambda-lazy", LambdaMessages.Lazy),
        ("lambda-sequence", LambdaMessages.Sequence),
        ("lambda-members", LambdaMessages.Members),
        ("lambda-fallback", LambdaMessages.Fallback),
        ("check-tier2", CheckMessages.Tier2),
        ("check-tier3", CheckMessages.Tier3),
    ];
}
