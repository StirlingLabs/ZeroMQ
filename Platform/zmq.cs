using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;

namespace ZeroMQ.lib
{
    [PublicAPI]
    [SuppressMessage("Interoperability", "CA1401", Justification = "Intentional")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static unsafe class zmq
    {
        // Use a const for the library name
        private const string LibName = "libzmq";

        private const CallingConvention Cdecl = CallingConvention.Cdecl;

        // From zmq.h (v3):
        // typedef struct {unsigned char _ [32];} zmq_msg_t;
        internal const int sizeof_zmq_msg_t_v3 = 32;

        [StructLayout(LayoutKind.Sequential)]
        public struct msg_t_v3
        {
            private ulong a, b, c, d;
        }

        // From zmq.h (not v4, but v4.2 and later):
        // typedef struct zmq_msg_t {unsigned char _ [64];} zmq_msg_t;
        internal const int sizeof_zmq_msg_t_v4 = 64;

        [StructLayout(LayoutKind.Sequential)]
        public struct msg_t_v4
        {
            private msg_t_v3 v3;
            private ulong a, b, c, d;
        }

        public static readonly nuint sizeof_zmq_msg_t = sizeof_zmq_msg_t_v4;

        // The static constructor prepares static readonly fields
        static zmq()
        {
            version(out var major, out var minor, out var patch);
            LibraryVersion = new(major, minor, patch);

            if (major < 4)
                throw new NotSupportedException($"libzmq version {LibraryVersion} not supported. Required version >= v4.0.");

            if (minor == 0)
                sizeof_zmq_msg_t = sizeof_zmq_msg_t_v3;
        }

        private static NotSupportedException VersionNotSupported(string requiredVersion)
            => new($"libzmq version not supported. Required version {requiredVersion}");

        public static readonly Version LibraryVersion;

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_version", CallingConvention = Cdecl)]
        public static extern void version(out int major, out int minor, out int patch);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_ctx_new", CallingConvention = Cdecl)]
        public static extern IntPtr ctx_new();

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_ctx_get", CallingConvention = Cdecl)]
        public static extern int ctx_get(IntPtr context, int option);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_ctx_set", CallingConvention = Cdecl)]
        public static extern int ctx_set(IntPtr context, int option, int optVal);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_ctx_shutdown", CallingConvention = Cdecl)]
        public static extern int ctx_shutdown(IntPtr context);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_ctx_term", CallingConvention = Cdecl)]
        public static extern int ctx_term(IntPtr context);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_init", CallingConvention = Cdecl)]
        public static extern int msg_init(IntPtr msg);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_init_size", CallingConvention = Cdecl)]
        public static extern int msg_init_size(IntPtr msg, nuint size);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif

#if NET5_0_OR_GREATER
            [DllImport(LibName, EntryPoint = "zmq_msg_init_data", CallingConvention = Cdecl)]
            public static extern int msg_init_data(IntPtr msg, IntPtr data, nuint size,
                    delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> ffn, IntPtr hint);
#else
        [UnmanagedFunctionPointer(Cdecl)]
        public delegate void free_fn(IntPtr data, IntPtr hint);

        [DllImport(LibName, EntryPoint = "zmq_msg_init_data", CallingConvention = Cdecl)]
        public static extern int msg_init_data(IntPtr msg, IntPtr data, nuint size,
            free_fn ffn, IntPtr hint);
#endif

#if NET5_0_OR_GREATER
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int msg_send(IntPtr msg, IntPtr socket, ZSocketFlags flags)
            => (flags & ZSocketFlags.DontWait) != 0
                ? msg_send_non_blocking(msg, socket, flags)
                : msg_send_blocking(msg, socket, flags);

        [DllImport(LibName, EntryPoint = "zmq_msg_send", CallingConvention = Cdecl)]
        private static extern int msg_send_blocking(IntPtr msg, IntPtr socket, ZSocketFlags flags);

        [SuppressGCTransition]
        [DllImport(LibName, EntryPoint = "zmq_msg_send", CallingConvention = Cdecl)]
        private static extern int msg_send_non_blocking(IntPtr msg, IntPtr socket, ZSocketFlags flags);
#else
        [DllImport(LibName, EntryPoint = "zmq_msg_send", CallingConvention = Cdecl)]
        public static extern int msg_send(IntPtr msg, IntPtr socket, ZSocketFlags flags);
#endif

#if NET5_0_OR_GREATER
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int msg_recv(IntPtr msg, IntPtr socket, ZSocketFlags flags)
            => (flags & ZSocketFlags.DontWait) != 0
                ? msg_recv_non_blocking(msg, socket, flags)
                : msg_recv_blocking(msg, socket, flags);

        [DllImport(LibName, EntryPoint = "zmq_msg_recv", CallingConvention = Cdecl)]
        private static extern int msg_recv_blocking(IntPtr msg, IntPtr socket, ZSocketFlags flags);

        [SuppressGCTransition]
        [DllImport(LibName, EntryPoint = "zmq_msg_recv", CallingConvention = Cdecl)]
        private static extern int msg_recv_non_blocking(IntPtr msg, IntPtr socket, ZSocketFlags flags);
#else
        [DllImport(LibName, EntryPoint = "zmq_msg_recv", CallingConvention = Cdecl)]
        public static extern int msg_recv(IntPtr msg, IntPtr socket, ZSocketFlags flags);
#endif


#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_close", CallingConvention = Cdecl)]
        public static extern int msg_close(IntPtr msg);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_data", CallingConvention = Cdecl)]
        public static extern IntPtr msg_data(IntPtr msg);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_size", CallingConvention = Cdecl)]
        public static extern nuint msg_size(IntPtr msg);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_more", CallingConvention = Cdecl)]
        public static extern int msg_more(IntPtr msg);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_gets", CallingConvention = Cdecl)]
        public static extern IntPtr msg_gets(IntPtr msg, IntPtr property);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_get", CallingConvention = Cdecl)]
        public static extern int msg_get(IntPtr msg, int property);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_set", CallingConvention = Cdecl)]
        public static extern int msg_set(IntPtr msg, int property, int value);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_copy", CallingConvention = Cdecl)]
        public static extern int msg_copy(IntPtr dest, IntPtr src);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_msg_move", CallingConvention = Cdecl)]
        public static extern int msg_move(IntPtr dest, IntPtr src);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_socket", CallingConvention = Cdecl)]
        public static extern IntPtr socket(IntPtr context, int type);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_close", CallingConvention = Cdecl)]
        public static extern int close(IntPtr socket);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_getsockopt", CallingConvention = Cdecl)]
        public static extern int getsockopt(IntPtr socket, int optionName, IntPtr optionValue, ref nuint optionLen);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_setsockopt", CallingConvention = Cdecl)]
        public static extern int setsockopt(IntPtr socket, int optionName, IntPtr optionValue, nuint optionLen);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_bind", CallingConvention = Cdecl)]
        public static extern int bind(IntPtr socket, IntPtr endpoint);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_unbind", CallingConvention = Cdecl)]
        public static extern int unbind(IntPtr socket, IntPtr endpoint);

        [DllImport(LibName, EntryPoint = "zmq_connect", CallingConvention = Cdecl)]
        public static extern int connect(IntPtr socket, IntPtr endpoint);

        [DllImport(LibName, EntryPoint = "zmq_disconnect", CallingConvention = Cdecl)]
        public static extern int disconnect(IntPtr socket, IntPtr endpoint);

        [DllImport(LibName, EntryPoint = "zmq_poll", CallingConvention = Cdecl)]
        public static extern int poll(void* items, int numItems, long timeout);

#if NET5_0_OR_GREATER
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int send(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags)
            => (flags & ZSocketFlags.DontWait) != 0
                ? send_non_blocking(socket, buf, len, flags)
                : send_blocking(socket, buf, len, flags);

        [DllImport(LibName, EntryPoint = "zmq_send", CallingConvention = Cdecl)]
        private static extern int send_blocking(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);

        [SuppressGCTransition]
        [DllImport(LibName, EntryPoint = "zmq_send", CallingConvention = Cdecl)]
        private static extern int send_non_blocking(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);
#else
        [DllImport(LibName, EntryPoint = "zmq_send", CallingConvention = Cdecl)]
        public static extern int send(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);
#endif

#if NET5_0_OR_GREATER
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int recv(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags)
            => (flags & ZSocketFlags.DontWait) != 0
                ? recv_non_blocking(socket, buf, len, flags)
                : recv_blocking(socket, buf, len, flags);

        [DllImport(LibName, EntryPoint = "zmq_recv", CallingConvention = Cdecl)]
        private static extern int recv_blocking(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);

        [SuppressGCTransition]
        [DllImport(LibName, EntryPoint = "zmq_recv", CallingConvention = Cdecl)]
        private static extern int recv_non_blocking(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);
#else
        [DllImport(LibName, EntryPoint = "zmq_recv", CallingConvention = Cdecl)]
        public static extern int recv(IntPtr socket, IntPtr buf, nuint len, ZSocketFlags flags);
#endif


#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_has", CallingConvention = Cdecl)]
        public static extern int has(IntPtr capability);

        [DllImport(LibName, EntryPoint = "zmq_socket_monitor", CallingConvention = Cdecl)]
        public static extern int socket_monitor(IntPtr socket, IntPtr endpoint, int events);

        [DllImport(LibName, EntryPoint = "zmq_proxy", CallingConvention = Cdecl)]
        public static extern int proxy(IntPtr frontend, IntPtr backend, IntPtr capture);

        [DllImport(LibName, EntryPoint = "zmq_proxy_steerable", CallingConvention = Cdecl)]
        public static extern int proxy_steerable(IntPtr frontend, IntPtr backend, IntPtr capture, IntPtr control);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_curve_keypair", CallingConvention = Cdecl)]
        public static extern int curve_keypair(IntPtr z85PublicKey, IntPtr z85SecretKey);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_z85_encode", CallingConvention = Cdecl)]
        public static extern IntPtr z85_encode(IntPtr dest, IntPtr data, nuint size);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_z85_decode", CallingConvention = Cdecl)]
        public static extern IntPtr z85_decode(IntPtr dest, IntPtr data);

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_errno", CallingConvention = Cdecl)]
        public static extern int errno();

#if NET5_0_OR_GREATER
        [SuppressGCTransition]
#endif
        [DllImport(LibName, EntryPoint = "zmq_strerror", CallingConvention = Cdecl)]
        public static extern IntPtr strerror(int errNum);
    }
}
