using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace VerificationGlue;

/// <summary>
/// Reads one variable that a <c>Verify(() =&gt; ...)</c> lambda captured, from the delegate's target.
/// The generated check of each call site holds one per captured variable.
/// </summary>
/// <remarks>
/// The compiler stores a captured local or parameter in a field of the same name on a closure
/// object. The closure of an inner scope reaches the one of an outer scope through a field whose
/// name starts with <c>CS$&lt;&gt;8__locals</c>, and a captured <c>this</c> is the field
/// <c>&lt;&gt;4__this</c>. These names are compiler details. When one is not found the read throws,
/// and the generated check runs the delegate for a message without values. The path to the field
/// is found on the first read and kept while closures of the same type arrive.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CapturedVariable
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ThisField = "<>4__this";
    private const string OuterScopePrefix = "CS$<>8__locals";

    private readonly string _field;
    private Path? _path;

    /// <summary>Creates a reader for the field that holds the variable.</summary>
    /// <param name="field">The field name, which is the variable's name for a local or a parameter.</param>
    public CapturedVariable(string field)
    {
        _field = field;
    }

    /// <summary>A reader for the captured <c>this</c>.</summary>
    /// <returns>The reader.</returns>
    public static CapturedVariable This() => new(ThisField);

    /// <summary>Reads the variable from <paramref name="closure"/>.</summary>
    /// <typeparam name="T">The variable's type.</typeparam>
    /// <param name="closure">The delegate's target.</param>
    /// <returns>The variable's value.</returns>
    /// <exception cref="MissingFieldException">The closure holds no field of that name.</exception>
    public T Read<T>(object? closure)
    {
        if (closure is null)
        {
            throw new MissingFieldException("the lambda has no closure");
        }

        // A lambda that captures only this compiles to an instance method, so its target is the instance.
        if (_field == ThisField && closure is T self)
        {
            return self;
        }

        var path = _path;
        if (path is null || path.ClosureType != closure.GetType())
        {
            _path = path = Find(closure);
        }

        var scope = closure;
        foreach (var outer in path.Outer)
        {
            scope = outer.GetValue(scope) ?? throw new MissingFieldException("an outer scope of the closure is null");
        }

        return (T)path.Field.GetValue(scope)!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The generated check keeps the closure types with DynamicDependency. A field the trimmer removed throws, and the check falls back to running the delegate.")]
    private Path Find(object closure)
    {
        var outer = new List<FieldInfo>();
        object? scope = closure;
        while (scope is not null)
        {
            var type = scope.GetType();
            if (type.GetField(_field, InstanceFields) is { } field)
            {
                return new Path(closure.GetType(), outer.ToArray(), field);
            }

            FieldInfo? next = null;
            foreach (var candidate in type.GetFields(InstanceFields))
            {
                if (candidate.Name.StartsWith(OuterScopePrefix, StringComparison.Ordinal))
                {
                    next = candidate;
                    break;
                }
            }

            if (next is null)
            {
                break;
            }

            outer.Add(next);
            scope = next.GetValue(scope);
        }

        throw new MissingFieldException("the closure holds no field named " + _field);
    }

    private sealed class Path
    {
        public Path(Type closureType, FieldInfo[] outer, FieldInfo field)
        {
            ClosureType = closureType;
            Outer = outer;
            Field = field;
        }

        public Type ClosureType { get; }

        public FieldInfo[] Outer { get; }

        public FieldInfo Field { get; }
    }
}
