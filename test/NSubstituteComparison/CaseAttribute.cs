namespace NSubstituteComparison;

/// <summary>
/// Marks a static method in <c>Cases/</c> as a comparison case. The method takes an
/// <see cref="Observations"/> and returns <c>void</c> or <see cref="Task"/>. Its class is its area,
/// and the class lives in a file of the same name.
/// </summary>
/// <param name="title">What the case does, as a heading on the differences page.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CaseAttribute(string title) : Attribute
{
    public string Title { get; } = title;
}

/// <summary>
/// Records that the case behaves differently under VerificationGlue.Mocks on purpose. The
/// comparison fails when such a case behaves the same, and the differences page shows the reason.
/// </summary>
/// <param name="reason">Why VerificationGlue.Mocks behaves differently, in one or two sentences.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DifferenceAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}

/// <summary>The title of an area on the differences page, on the case class.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AreaAttribute(string title, int order) : Attribute
{
    public string Title { get; } = title;

    public int Order { get; } = order;
}
