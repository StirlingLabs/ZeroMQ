using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// Creates <see cref="ZSocket"/> instances within a process boundary.
    /// </summary>
    public sealed class ZContext : IDisposable
    {
        static Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Gets and protected sets the default Encoding.
        /// Note: Do not set the Encoding after ZContext.Create.
        /// </summary>
        public static Encoding Encoding => _encoding;

        private static readonly object SyncObject = new();

        private static ZContext _current;

        public static ZContext Current
        {
            get {
                if (_current == null)
                {
                    // INFO: This is the ZContext who is the one, running this program.
                    lock (SyncObject)
                    {
                        if (_current == null)
                        {
                            _current = new();
                        }
                    }
                }
                return _current;
            }
        }

        /// <summary>
        /// Create a <see cref="ZContext"/> instance.
        /// </summary>
        /// <returns><see cref="ZContext"/></returns>
        public ZContext()
        {
            _contextPtr = zmq.ctx_new();

            if (_contextPtr == default)
                throw new InvalidProgramException("zmq_ctx_new");
        }

        /// <summary>
        /// Create a <see cref="ZContext"/> instance.
        /// </summary>
        /// <returns><see cref="ZContext"/></returns>
        public static ZContext Create()
            => new();

        ~ZContext()
            => Dispose(false);

        private IntPtr _contextPtr;

        /// <summary>
        /// Gets a handle to the native ZeroMQ context.
        /// </summary>
        public IntPtr ContextPtr => _contextPtr;

        public static bool Has(string capability)
        {
            using (var capabilityPtr = ZSafeHandle.AllocString(capability))
            {
                if (0 < zmq.has(capabilityPtr))
                {
                    return true;
                }
            }
            return false;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Proxy(ZSocket frontend, ZSocket backend)
            => Proxy(frontend, backend, null);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Proxy(ZSocket frontend, ZSocket backend, out ZError? error)
            => Proxy(frontend, backend, null, out error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Proxy(ZSocket frontend, ZSocket backend, ZSocket? capture)
        {
            if (Proxy(frontend, backend, capture, out var error))
                return;

            if (error == ZError.ETERM)
                return; // Interrupted

            throw new ZException(error);
        }

        public static bool Proxy(ZSocket frontend, ZSocket backend, ZSocket? capture, out ZError? error)
        {
            error = ZError.None;

            while (-1 == zmq.proxy(frontend.SocketPtr, backend.SocketPtr, capture?.SocketPtr ?? default))
            {
                error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    return false;

                if (frontend.SocketPtr == default)
                    throw new ObjectDisposedException(nameof(frontend));

                if (backend.SocketPtr == default)
                    throw new ObjectDisposedException(nameof(frontend));
            }

            return true;
        }

        public static void ProxySteerable(ZSocket frontend, ZSocket backend, ZSocket control)
            => ProxySteerable(frontend, backend, null, control);

        public static bool ProxySteerable(ZSocket frontend, ZSocket backend, ZSocket control, out ZError? error)
            => ProxySteerable(frontend, backend, null, control, out error);

        public static void ProxySteerable(ZSocket frontend, ZSocket backend, ZSocket? capture, ZSocket control)
        {
            if (ProxySteerable(frontend, backend, capture, control, out var error))
                return;

            if (error == ZError.ETERM)
                return; // Interrupted

            throw new ZException(error);
        }

        public static bool ProxySteerable(ZSocket frontend, ZSocket backend, ZSocket? capture, ZSocket control, out ZError? error)
        {
            error = ZError.None;

            while (-1 == zmq.proxy_steerable(frontend.SocketPtr, backend.SocketPtr, capture?.SocketPtr ?? default,
                control?.SocketPtr ?? default))
            {
                error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    return false;

                error = default;
            }
            return true;
        }

        public void SetOption(ZContextOption option, int optionValue)
        {
            EnsureNotDisposed();

            var rc = zmq.ctx_set(_contextPtr, option, optionValue);
            if (rc != -1)
                return;

            var error = ZError.GetLastError();

            if (error == ZError.EINVAL)
                throw new ArgumentOutOfRangeException($"The requested option optionName \"{option}\" is invalid.");

            throw new ZException(error);
        }

        public int GetOption(ZContextOption option)
        {
            EnsureNotDisposed();

            var rc = zmq.ctx_get(_contextPtr, option);
            if (rc != -1)
                return rc;
            var error = ZError.GetLastError();

            if (error == ZError.EINVAL)
                throw new ArgumentOutOfRangeException($"The requested option optionName \"{option}\" is invalid.");

            throw new ZException(error);
        }

        /// <summary>
        /// Gets or sets the size of the thread pool for the current context (default = 1).
        /// </summary>
        public int ThreadPoolSize
        {
            get => GetOption(ZContextOption.IO_THREADS);
            set => SetOption(ZContextOption.IO_THREADS, value);
        }

        /// <summary>
        /// Gets or sets the maximum number of sockets for the current context (default = 1024).
        /// </summary>
        public int MaxSockets
        {
            get => GetOption(ZContextOption.MAX_SOCKETS);
            set => SetOption(ZContextOption.MAX_SOCKETS, value);
        }

        /// <summary>
        /// Gets or sets the supported socket protocol(s) when using TCP transports. (Default = <see cref="ProtocolType.Ipv4Only"/>).
        /// </summary>
        public bool IPv6Enabled
        {
            get => GetOption(ZContextOption.IPV6) == 1;
            set => SetOption(ZContextOption.IPV6, value ? 1 : 0);
        }

        /// <summary>
        /// Shutdown the ZeroMQ context.
        /// </summary>
        public void Shutdown()
        {
            if (!Shutdown(out var error))
            {
                throw new ZException(error);
            }
        }

        /// <summary>
        /// Shutdown the ZeroMQ context.
        /// </summary>
        public bool Shutdown(out ZError? error)
        {
            error = default;

            if (_contextPtr == default)
                return true;

            while (-1 == zmq.ctx_shutdown(_contextPtr))
            {
                error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    return false;

                // Maybe ZError.EFAULT

                error = default;
            }

            // don't _contextPtr = default(IntPtr);
            return true;
        }

        /// <summary>
        /// Terminate the ZeroMQ context.
        /// </summary>
        public void Terminate()
        {
            if (!Terminate(out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Terminate the ZeroMQ context.
        /// </summary>
        public bool Terminate(out ZError? error)
        {
            error = ZError.None;
            if (_contextPtr == default)
                return true;

            while (-1 == zmq.ctx_term(_contextPtr))
            {
                error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    return false;

                // Maybe ZError.EFAULT

                error = default;
            }

            _contextPtr = default;
            return true;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
                Terminate(out var error);
        }

        private void EnsureNotDisposed()
        {
            if (_contextPtr == default)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
