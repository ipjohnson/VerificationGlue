// case: mocks-shape-0017
// layer: shape
// origin: mocks-shape-sweep.md triage, a type nested in a generic type with extern-aliased type arguments, CD-10
// expect: VGM0001
// library: Libraries/VendorV1.cs alias V1
// library: Libraries/VendorV2.cs alias V2

extern alias V1;
extern alias V2;

namespace Shapes.ExternAliasNested;

public class Local
{
}

// Refused: a nested type's type arguments come from an extern-aliased assembly.
public interface IReadsNested
{
    V1::Vendor.Ledger<V2::Vendor.Payment>.Line Read();
}

// Refused: the same, through the type argument of a BCL type.
public interface IEnumeratesAliased
{
    System.Collections.Generic.List<V2::Vendor.Payment>.Enumerator Each();
}

public class MockTargets
{
    // Refused: the mocked type itself.
    public V1::Vendor.Ledger<V2::Vendor.Payment>.IEntry? Entry;

    // Mocked: the outer type is aliased, but its type argument is not.
    public V1::Vendor.Ledger<Local>.IEntry? LocalEntry;
}
