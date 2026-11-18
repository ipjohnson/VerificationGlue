using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace VerificationGlue.Tests.AspNetCore.Generator;

/// <summary>A source location as plain values, so a model that holds one compares by value.</summary>
internal sealed record LocationModel(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationModel From(Location location) =>
        new(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>
/// A class marked <c>[WebAppEntry]</c>. <see cref="Problem"/> is the reason for VGA0002, or null when
/// the generator can add to the class.
/// </summary>
internal sealed record EntryClassModel(
    string? Namespace,
    string Name,
    string AssemblyName,
    string? Problem,
    LocationModel ClassLocation,
    LocationModel AttributeLocation);

internal enum EntryReturn
{
    Void,
    Int,
    Task,
    TaskOfInt,
}

/// <summary>
/// The entry point of an app assembly. <see cref="TypeName"/> is the full metadata name of its type,
/// as <c>UnsafeAccessorType</c> takes it.
/// </summary>
internal sealed record EntryPointModel(string TypeName, string MethodName, EntryReturn Return, bool HasArgs);

/// <summary>
/// What the generator found in a referenced assembly that a <c>[WebAppEntry]</c> names. Either
/// <see cref="EntryPoint"/> is set, or <see cref="Problem"/> says why not for VGA0001.
/// </summary>
internal sealed record AppModel(string AssemblyName, EntryPointModel? EntryPoint, string? Problem);

/// <summary>The names of the referenced assemblies, for the VGA0001 message of an assembly that is not one of them.</summary>
internal sealed record ReferencedAssemblies(EquatableArray<string> Names);
