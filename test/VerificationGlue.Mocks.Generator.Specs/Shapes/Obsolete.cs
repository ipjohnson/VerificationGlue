// case: mocks-shape-0016
// layer: shape
// origin: sweep, System.Xml.IApplicationResourceStreamResolver is [Obsolete] with error: true
// expect: pass

using System;

namespace Shapes.Obsolete;

[Obsolete("Use INew.", error: true)]
public interface IOld
{
    int Value();
}

[Obsolete("Use Current.", error: true)]
public class Retired
{
}

// Only an obsolete type can name Retired. This one is a warning, so the interface itself compiles.
[Obsolete("Moving to Current.")]
public interface IUsesRetired
{
    Retired Get();
}

public interface IObsoleteMember
{
    [Obsolete("Use Run.", error: true)]
    void Walk();

    void Run();
}
