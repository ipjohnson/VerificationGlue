// case: mocks-shape-0002
// layer: shape
// origin: mocks.md, generic constraints including default, notnull and allows ref struct
// expect: VGM0003

using System;
using System.Collections.Generic;

namespace Shapes.Constraints;

public interface IConstrained<TClass, TStruct, TNotNull, TNew, TAllows>
    where TClass : class?
    where TStruct : struct
    where TNotNull : notnull
    where TNew : IComparable<TNew>, new()
    where TAllows : allows ref struct
{
    TClass? Class(TClass? value);
    TStruct? Struct(TStruct? value);
    TNotNull NotNull(TNotNull value);
    TNew Create();
    TAllows Passthrough();
}

public interface IGenericMethods
{
    // An unconstrained T? in an explicit implementation needs 'where T : default'.
    T? Find<T>(T? value);
    T? FindClass<T>(T? value) where T : class;
    T? FindStruct<T>(T? value) where T : struct;
    T FindBase<T>(T? value) where T : Exception;
    void Unmanaged<T>(T value) where T : unmanaged;
    void NotNull<T>(T value) where T : notnull;
    void Enum<T>(T value) where T : struct, System.Enum;
    void Delegate<T>(T value) where T : System.Delegate;
    TResult Related<TSource, TResult>(TSource source) where TResult : TSource, new();
    IEnumerable<T> Many<T>(IEnumerable<T> values) where T : IEquatable<T>;

    // A method type parameter that allows ref structs cannot be boxed, so this member throws.
    void Span<T>(T value) where T : allows ref struct;
}

public class MockTargets
{
    public IConstrained<string, int, Guid, int, object>? Closed;
    public IGenericMethods? Methods;
}

public interface IOpen<T> where T : class, IDisposable, new()
{
    T Get();
}

public interface IOpenAllowsRefStruct<T> where T : allows ref struct
{
    T Get();
    void Set(T value);
}
