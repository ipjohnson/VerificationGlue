// case: mocks-shape-0005
// layer: shape
// origin: mocks.md, nullable annotations and tuple names
// expect: pass

using System;
using System.Collections.Generic;

namespace Shapes.Nullable;

public interface INullable
{
    string? Name { get; set; }
    List<string?>? Names(Dictionary<string, List<int?>?> map);
    int? Count(int?[]? counts, string?[][]? jagged, int[,]? grid, int[,][]? mixed);
    event EventHandler<string?>? Changed;
}

public interface ITuples
{
    (int Count, string? Name) Pair();
    (int, (string First, string Last) Person) Nested((int X, int Y)[] points);
    ValueTuple<int> Single();
    (int A, int B, int C, int D, int E, int F, int G, int H, int I) Long();
    Dictionary<(string Key, int Index), List<(bool Ok, string? Error)>> Map();
}

public interface IGeneric<T>
{
    T? Maybe(T? value);
    IEnumerable<T?> All();
}

public class MockTargets
{
    public IGeneric<string>? ClosedReference;
    public IGeneric<int>? ClosedValue;
    public IGeneric<string?>? ClosedNullable;
}
