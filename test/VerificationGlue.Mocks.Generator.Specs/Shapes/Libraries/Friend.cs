[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ShapeCase")]

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
