using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using static ZeroMQ.lib.zmq;

namespace ZeroMQ.lib
{
    [PublicAPI]
    public static class zmq_msg_group
    {
#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_set_group", CallingConvention = Cdecl)]
        public static extern int msg_set_group(IntPtr msg, IntPtr group);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_group", CallingConvention = Cdecl)]
        public static extern IntPtr msg_group(IntPtr msg);
    }

#if DEBUG
    [PublicAPI]
    public static class zmq_test_dummy
    {
        [DllImport(LibName, EntryPoint = "__zmq_test_dummy_does_not_exist__", CallingConvention = Cdecl)]
        public static extern void m();
    }
#endif
}
