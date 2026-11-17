// case: mocks-shape-0003
// layer: shape
// origin: mocks.md, in, ref readonly, params and optional parameters
// expect: VGM0003

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shapes.Parameters;

public enum Mode { Basic, Scientific }

public interface IParameters
{
    void In(in DateTime value);
    void RefReadonly(ref readonly Guid value);
    void Ref(ref int value);
    bool Out(string text, out int value);
    void OutGeneric<T>(out T value, ref T other);
    int Params(params int[] values);
    int ParamsCollection(params IEnumerable<int> values);
    int ParamsSpan(params ReadOnlySpan<int> values);
    void Optional(int count = 3, string? name = null, Mode mode = Mode.Scientific, decimal amount = 1.5m, double ratio = double.NaN);
    void Caller([CallerMemberName] string member = "", [CallerLineNumber] int line = 0);
    void ScopedRef(scoped ref int value);
    void ScopedIn(scoped in long value);
    int this[in int index, params string[] keys] { get; set; }
}

public class MockTargets
{
    public IParameters? Parameters;
}
