// case: mocks-shape-0008
// layer: shape
// origin: mocks.md, internal types in another assembly that grants InternalsVisibleTo
// expect: pass
// library: Libraries/Friend.cs

namespace Shapes.InternalsVisibleTo;

public class MockTargets
{
    internal Friend.IFriendOnly? FriendOnly = null;
    public Friend.IPartlyInternal? PartlyInternal;
    public Friend.IInternalDefault? InternalDefault;
}
