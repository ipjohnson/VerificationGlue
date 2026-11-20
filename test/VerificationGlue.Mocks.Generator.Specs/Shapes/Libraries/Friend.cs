[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ShapeCase")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Hardening.Specs")]

namespace Friend;

internal interface IFriendOnly
{
    int Count();
}

public interface IPartlyInternal
{
    int Visible();
    internal int Hidden();
}

public interface IInternalDefault
{
    int Visible();
    internal int Hidden() => 0;
}
