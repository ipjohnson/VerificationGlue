using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace VerificationGlue.Tests.DependencyInjection.Generator;

/// <summary>A source location as plain values, so a model that holds one compares by value.</summary>
internal sealed record LocationModel(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationModel From(Location location) =>
        new(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>
/// The <c>T</c> of one <c>[ServiceCollection&lt;T&gt;]</c>. <see cref="Problem"/> is the reason for VGD0001,
/// or null when <see cref="TypeName"/> has a <c>Configure</c> method the generator can call or is a
/// DependencyModules module it can create.
/// </summary>
/// <param name="TypeName">The type, written with <c>global::</c>.</param>
/// <param name="DisplayName">The type as the message names it.</param>
/// <param name="TakesContext">True when <c>Configure</c> takes a second parameter of <c>TestContainerContext</c>.</param>
/// <param name="IsModule">True when <c>T</c> has no such <c>Configure</c> method and the container adds <c>new T()</c> as a module.</param>
/// <param name="Problem">The reason for VGD0001, or null.</param>
/// <param name="Location">The attribute.</param>
internal sealed record SourceModel(string TypeName, string DisplayName, bool TakesContext, bool IsModule, string? Problem, LocationModel Location);
