namespace Stranger;

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
