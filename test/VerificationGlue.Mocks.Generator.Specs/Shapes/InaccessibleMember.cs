// case: mocks-shape-0009
// layer: shape
// origin: sweep, Microsoft.EntityFrameworkCore.Migrations.IMigrationsDatabaseLock has an abstract member another assembly cannot see
// expect: VGM0001
// library: Libraries/Stranger.cs

namespace Shapes.InaccessibleMember;

public class MockTargets
{
    // Refused: the abstract internal member cannot be implemented outside its assembly.
    public Stranger.IPartlyInternal? PartlyInternal;

    // Mocked: the internal member has a default body, so the mock skips it.
    public Stranger.IInternalDefault? InternalDefault;
}
