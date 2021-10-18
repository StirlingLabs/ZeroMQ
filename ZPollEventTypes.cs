using System;

namespace ZeroMQ
{
    [Flags]
    public enum ZPollEventTypes : short
    {
        None = 0,
        In = 1,
        Out = 2,
        Error = 4,
        Pri = 8
    }
}
