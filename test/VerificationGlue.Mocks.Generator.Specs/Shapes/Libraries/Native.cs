namespace Native;

public unsafe interface INative
{
    int Read(byte* buffer, int length);
}
