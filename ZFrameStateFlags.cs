using System;

namespace ZeroMQ
{
    [Flags]
    internal enum ZFrameStateFlags
    {
        IsDisposed = 1 << 0,
        IsDismissed = 1 << 1,
        IsClosed = 1 << 2,
        IsUnavailable = IsDisposed | IsDismissed | IsClosed,
    }
}
