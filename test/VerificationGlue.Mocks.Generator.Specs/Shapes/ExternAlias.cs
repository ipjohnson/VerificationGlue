// case: mocks-shape-0007
// layer: shape
// origin: mocks.md, extern aliases
// expect: pass
// library: Libraries/VendorV1.cs alias V1
// library: Libraries/VendorV2.cs alias V2

extern alias V1;
extern alias V2;

namespace Shapes.ExternAlias;

public class Local
{
}

public class MockTargets
{
    public V1::Vendor.IGateway? First;
    public V2::Vendor.IGateway? Second;
    public V1::Vendor.IGateway<V2::Vendor.Payment>? Mixed;
    public V1::Vendor.IGateway<Local>? WithLocal;
    public System.Collections.Generic.IList<V2::Vendor.Payment>? BclOfAliased;
}
