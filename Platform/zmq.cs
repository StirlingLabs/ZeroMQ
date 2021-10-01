using System;
using System.Runtime.InteropServices;

namespace ZeroMQ.lib
{
	public static unsafe class zmq
	{
		private const CallingConvention CCCdecl = CallingConvention.Cdecl;

		// Use a const for the library name
		private const string LibraryName = "libzmq";

		// From zmq.h (v3):
		// typedef struct {unsigned char _ [32];} zmq_msg_t;
		private static readonly int sizeof_zmq_msg_t_v3 = 32;

		// From zmq.h (not v4, but v4.2 and later):
		// typedef struct zmq_msg_t {unsigned char _ [64];} zmq_msg_t;
		private static readonly int sizeof_zmq_msg_t_v4 = 64;

		public static readonly int sizeof_zmq_msg_t = sizeof_zmq_msg_t_v4;

		// The static constructor prepares static readonly fields
		static zmq()
		{
			version(out var major, out var minor, out var patch);
			LibraryVersion = new(major, minor, patch);

			if (major >= 4)
			{
				if (minor == 0)
					sizeof_zmq_msg_t = sizeof_zmq_msg_t_v3;

				return;
			}
			throw VersionNotSupported(null, ">= v3");
		}

		private static NotSupportedException VersionNotSupported(string methodName, string requiredVersion)
			=> new(
				$"{(methodName == null ? string.Empty : methodName + ": ")}libzmq version not supported. Required version {requiredVersion}");

		public static readonly Version LibraryVersion;

		// (2) Declare privately the extern entry point
		[DllImport(LibraryName, EntryPoint = "zmq_version", CallingConvention = CCCdecl)]
		public static extern void version(out int major, out int minor, out int patch);

		/* Deprecated. Use zmq_ctx_new instead.
		[DllImport(LibraryName, EntryPoint = "zmq_init", CallingConvention = CCCdecl)]
		public static extern IntPtr init(int io_threads); /**/

		[DllImport(LibraryName, EntryPoint = "zmq_ctx_new", CallingConvention = CCCdecl)]
		public static extern IntPtr ctx_new();

#if NET5_0_OR_GREATER
		[SuppressGCTransition]
#endif
		[DllImport(LibraryName, EntryPoint = "zmq_ctx_get", CallingConvention = CCCdecl)]
		public static extern int ctx_get(IntPtr context, int option);

#if NET5_0_OR_GREATER
		[SuppressGCTransition]
#endif
		[DllImport(LibraryName, EntryPoint = "zmq_ctx_set", CallingConvention = CCCdecl)]
		public static extern int ctx_set(IntPtr context, int option, int optval);

		[DllImport(LibraryName, EntryPoint = "zmq_ctx_shutdown", CallingConvention = CCCdecl)]
		public static extern int ctx_shutdown(IntPtr context);

		/* Deprecated. Use zmq_ctx_term instead.
		[DllImport(LibraryName, CallingConvention = CCCdecl)]
		public static extern Int32 term(IntPtr context); /**/

		/* Deprecated. Use zmq_ctx_term instead. */
		[DllImport(LibraryName, EntryPoint = "zmq_term", CallingConvention = CCCdecl)]
		public static extern int term(IntPtr context);

		[DllImport(LibraryName, EntryPoint = "zmq_ctx_term", CallingConvention = CCCdecl)]
		public static extern int ctx_term(IntPtr context);

		[DllImport(LibraryName, EntryPoint = "zmq_msg_init", CallingConvention = CCCdecl)]
		public static extern int msg_init(IntPtr msg);

		[DllImport(LibraryName, EntryPoint = "zmq_msg_init_size", CallingConvention = CCCdecl)]
		public static extern int msg_init_size(IntPtr msg, int size);

		[UnmanagedFunctionPointer(CCCdecl)]
		public delegate void FreeMessageDataDelegate(IntPtr data, IntPtr hint);

		[DllImport(LibraryName, EntryPoint = "zmq_msg_init_data", CallingConvention = CCCdecl)]
		public static extern int msg_init_data(IntPtr msg, IntPtr data, int size, FreeMessageDataDelegate ffn, IntPtr hint);

		[DllImport(LibraryName, EntryPoint = "zmq_msg_send", CallingConvention = CCCdecl)]
		public static extern int msg_send(IntPtr msg, IntPtr socket, int flags);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_recv", CallingConvention = CCCdecl)]
		public static extern int msg_recv(IntPtr msg, IntPtr socket, int flags);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_close", CallingConvention = CCCdecl)]
		public static extern int msg_close(IntPtr msg);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_data", CallingConvention = CCCdecl)]
		public static extern IntPtr msg_data(IntPtr msg);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_size", CallingConvention = CCCdecl)]
		public static extern int msg_size(IntPtr msg);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_more", CallingConvention = CCCdecl)]
		public static extern int msg_more(IntPtr msg);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_gets", CallingConvention = CCCdecl)]
		public static extern IntPtr msg_gets(IntPtr msg, IntPtr property);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_get", CallingConvention = CCCdecl)]
		public static extern int msg_get(IntPtr msg, int property);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_set", CallingConvention = CCCdecl)]
		public static extern int msg_set(IntPtr msg, int property, int value);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_copy", CallingConvention = CCCdecl)]
		public static extern int msg_copy(IntPtr dest, IntPtr src);
		
		[DllImport(LibraryName, EntryPoint = "zmq_msg_move", CallingConvention = CCCdecl)]
		public static extern int msg_move(IntPtr dest, IntPtr src);

		[DllImport(LibraryName, EntryPoint = "zmq_socket", CallingConvention = CCCdecl)]
		public static extern IntPtr socket(IntPtr context, int type);
		
		[DllImport(LibraryName, EntryPoint = "zmq_close", CallingConvention = CCCdecl)]
		public static extern int close(IntPtr socket);

		[DllImport(LibraryName, EntryPoint = "zmq_getsockopt", CallingConvention = CCCdecl)]
		public static extern int getsockopt(IntPtr socket, int option_name, IntPtr option_value, IntPtr option_len);

		[DllImport(LibraryName, EntryPoint = "zmq_setsockopt", CallingConvention = CCCdecl)]
		public static extern int setsockopt(IntPtr socket, int option_name, IntPtr option_value, int option_len);

		[DllImport(LibraryName, EntryPoint = "zmq_bind", CallingConvention = CCCdecl)]
		public static extern int bind(IntPtr socket, IntPtr endpoint);

		[DllImport(LibraryName, EntryPoint = "zmq_unbind", CallingConvention = CCCdecl)]
		public static extern int unbind(IntPtr socket, IntPtr endpoint);

		[DllImport(LibraryName, EntryPoint = "zmq_connect", CallingConvention = CCCdecl)]
		public static extern int connect(IntPtr socket, IntPtr endpoint);

		[DllImport(LibraryName, EntryPoint = "zmq_disconnect", CallingConvention = CCCdecl)]
		public static extern int disconnect(IntPtr socket, IntPtr endpoint);

		[DllImport(LibraryName, EntryPoint = "zmq_poll", CallingConvention = CCCdecl)]
		public static extern int poll(void* items, int numItems, long timeout);

		[DllImport(LibraryName, EntryPoint = "zmq_send", CallingConvention = CCCdecl)]
		public static extern int send(IntPtr socket, IntPtr buf, int len, int flags);

		[DllImport(LibraryName, EntryPoint = "zmq_recv", CallingConvention = CCCdecl)]
		public static extern int recv(IntPtr socket, IntPtr buf, int len, int flags);


		[DllImport(LibraryName, EntryPoint = "zmq_has", CallingConvention = CCCdecl)]
		public static extern int has(IntPtr capability);

		[DllImport(LibraryName, EntryPoint = "zmq_socket_monitor", CallingConvention = CCCdecl)]
		public static extern int socket_monitor(IntPtr socket, IntPtr endpoint, int events);

		[DllImport(LibraryName, EntryPoint = "zmq_proxy", CallingConvention = CCCdecl)]
		public static extern int proxy(IntPtr frontend, IntPtr backend, IntPtr capture);
		
		[DllImport(LibraryName, EntryPoint = "zmq_proxy_steerable", CallingConvention = CCCdecl)]
		public static extern int proxy_steerable(IntPtr frontend, IntPtr backend, IntPtr capture, IntPtr control);

		[DllImport(LibraryName, EntryPoint = "zmq_curve_keypair", CallingConvention = CCCdecl)]
		public static extern int curve_keypair(IntPtr z85_public_key, IntPtr z85_secret_key);

		[DllImport(LibraryName, EntryPoint = "zmq_z85_encode", CallingConvention = CCCdecl)]
		public static extern IntPtr z85_encode(IntPtr dest, IntPtr data, int size);

		[DllImport(LibraryName, EntryPoint = "zmq_z85_decode", CallingConvention = CCCdecl)]
		public static extern IntPtr z85_decode(IntPtr dest, IntPtr data);

		[DllImport(LibraryName, EntryPoint = "zmq_errno", CallingConvention = CCCdecl)]
		public static extern int errno();

		[DllImport(LibraryName, EntryPoint = "zmq_strerror", CallingConvention = CCCdecl)]
		public static extern IntPtr strerror(int errnum);

	}
}