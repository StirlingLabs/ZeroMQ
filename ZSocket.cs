using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// Sends and receives messages, single frames and byte frames across ZeroMQ.
    /// </summary>
    [PublicAPI]
    public class ZSocket : IDisposable
    {
        public static readonly ConcurrentDictionary<IntPtr, WeakReference<ZSocket>> Active = new();

        /// <summary>
        /// Create a <see cref="ZSocket"/> instance.
        /// </summary>
        /// <returns><see cref="ZSocket"/></returns>
        public static ZSocket Create(ZContext context, ZSocketType socketType)
            => new(context, socketType);

        /// <summary>
        /// Create a <see cref="ZSocket"/> instance.
        /// </summary>
        /// <returns><see cref="ZSocket"/></returns>
        public static ZSocket? Create(ZContext context, ZSocketType socketType, out ZError? error)
        {
            var socket = new ZSocket(context, socketType, out error);
            return error == default ? socket : default;
        }

        private ZContext _context;

        private IntPtr _socketPtr;

        private ZSocketType _socketType;
        public readonly object _lock = new();

        public string? Name { get; set; }

        /// <summary>
        /// Create a <see cref="ZSocket"/> instance.
        /// You are using ZContext.Current!
        /// </summary>
        /// <returns><see cref="ZSocket"/></returns>
        public ZSocket(ZSocketType socketType) : this(ZContext.Current, socketType) { }

        /// <summary>
        /// Create a <see cref="ZSocket"/> instance.
        /// </summary>
        /// <returns><see cref="ZSocket"/></returns>
        public ZSocket(ZContext context, ZSocketType socketType)
        {
            _context = context;
            _socketType = socketType;

            if (!Initialize(out var error))
                throw new ZException(error);
        }
        /// <summary>
        /// Create a <see cref="ZSocket"/> instance.
        /// </summary>
        /// <returns><see cref="ZSocket"/></returns>
        private ZSocket(ZContext context, ZSocketType socketType, out ZError? error)
        {
            _context = context;
            _socketType = socketType;

            Initialize(out error);
        }

        [Obsolete("Do not use.", true)]
        private ZSocket()
            => throw new NotSupportedException();

        protected bool Initialize(out ZError? error)
        {
            error = default;

            if (default != (_socketPtr = zmq.socket(_context.ContextPtr, (int)_socketType)))
            {
                Active.TryAdd(_socketPtr, new(this));
                return true;
            }

            error = ZError.GetLastError();
            return false;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ZSocket"/> class.
        /// </summary>
        ~ZSocket()
            => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ZSocket"/>, and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (_lock)
            {
                Active.TryRemove(_socketPtr, out _);
                //if (disposing)
                Close(out _);
            }
        }

        /// <summary>
        /// Close the current socket.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Close()
        {
            if (!Close(out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Close the current socket.
        /// </summary>
        public bool Close(out ZError? error)
        {
            lock (_lock)
            {
                error = ZError.None;
                var ptr = Interlocked.Exchange(ref _socketPtr, default);
                if (ptr == default) return true;

                if (-1 != zmq.close(ptr))
                    return true;

                error = ZError.GetLastError();
                return false;
            }
        }

        public ZContext Context => _context;

        public IntPtr SocketPtr => _socketPtr;

        /// <summary>
        /// Gets the <see cref="ZeroMQ.ZSocketType"/> value for the current socket.
        /// </summary>
        public ZSocketType SocketType => _socketType;

        /// <summary>
        /// Bind the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public void Bind(string endpoint)
        {
            if (!Bind(endpoint, out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Bind the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public unsafe bool Bind(string endpoint, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = default;

                if (string.IsNullOrWhiteSpace(endpoint))
                    throw new ArgumentException("IsNullOrWhiteSpace", nameof(endpoint));

                fixed (void* pEncodingBytes = EncodedStringCache.For(Encoding.UTF8, endpoint))
                    if (-1 != zmq.bind(_socketPtr, (IntPtr)pEncodingBytes))
                        return true;

                error = ZError.GetLastError();
                return false;
            }
        }

        /// <summary>
        /// Unbind the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public void Unbind(string endpoint)
        {
            if (!Unbind(endpoint, out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Unbind the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public unsafe bool Unbind(string endpoint, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = default;

                if (string.IsNullOrWhiteSpace(endpoint))
                    throw new ArgumentException("IsNullOrWhiteSpace", nameof(endpoint));

                fixed (void* pEncodingBytes = EncodedStringCache.For(Encoding.UTF8, endpoint))
                    if (-1 != zmq.unbind(_socketPtr, (IntPtr)pEncodingBytes))
                        return true;

                error = ZError.GetLastError();
                return false;
            }
        }

        /// <summary>
        /// Connect the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public void Connect(string endpoint)
        {
            if (!Connect(endpoint, out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Connect the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public unsafe bool Connect(string endpoint, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = default;

                if (string.IsNullOrWhiteSpace(endpoint))
                    throw new ArgumentException("IsNullOrWhiteSpace", nameof(endpoint));

                fixed (void* pEncodingBytes = EncodedStringCache.For(Encoding.UTF8, endpoint))
                    if (-1 != zmq.connect(_socketPtr, (IntPtr)pEncodingBytes))
                        return true;

                error = ZError.GetLastError();
                return false;
            }
        }

        /// <summary>
        /// Disconnect the specified endpoint.
        /// </summary>
        public void Disconnect(string endpoint)
        {
            if (!Disconnect(endpoint, out var error))
                throw new ZException(error);
        }

        /// <summary>
        /// Disconnect the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        public unsafe bool Disconnect(string endpoint, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = default;

                if (string.IsNullOrWhiteSpace(endpoint))
                    throw new ArgumentException("IsNullOrWhiteSpace", nameof(endpoint));

                fixed (void* pEncodingBytes = EncodedStringCache.For(Encoding.UTF8, endpoint))
                    if (-1 != zmq.disconnect(_socketPtr, (IntPtr)pEncodingBytes))
                        return true;

                error = ZError.GetLastError();
                return false;
            }
        }

        /// <summary>
        /// Receives HARD bytes into a new byte[n]. Please don't use ReceiveBytes, use instead ReceiveFrame.
        /// </summary>
        public int ReceiveBytes(Span<byte> buffer, nuint offset, nuint count)
        {
            int length;
            if (-1 == (length = ReceiveBytes(buffer, offset, count, ZSocketFlags.None, out var error)))
                throw new ZException(error);
            return length;
        }

        /// <summary>
        /// Receives HARD bytes into a new byte[n]. Please don't use ReceiveBytes, use instead ReceiveFrame.
        /// </summary>
        public unsafe int ReceiveBytes(Span<byte> buffer, nuint offset, nuint count, ZSocketFlags flags, out ZError? error)
        {

            // int zmq_recv(void* socket, void* buf, size_t len, int flags);

            lock (_lock)
            {
                EnsureNotDisposed();

                error = ZError.None;

                fixed (byte* pBuffer = buffer)
                {
                    var pOffset = pBuffer + offset;

                    int length;

                    //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-RECV");
                    //Console.Error.Flush();
                    while (-1 == (length = zmq.recv(SocketPtr, (IntPtr)pOffset, count, flags)))
                    {
                        //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-RECV_DONE");
                        //Console.Error.Flush();
                        error = ZError.GetLastError();

                        if (error != ZError.EINTR
                            || error == ZError.ETERM
                            || error == ZError.EAGAIN)
                            break;

                        if (error == ZError.ENOMEM
                            || error == ZError.EFSM
                            || error == ZError.ENOTSUP
                            || error == ZError.EFSM)
                            throw new ZException(error);

                        error = default;
                    }

                    return length;
                }
            }
        }

        /// <summary>
        /// Sends HARD bytes from a byte[n]. Please don't use SendBytes, use instead SendFrame.
        /// </summary>
        public bool SendBytes(ReadOnlySpan<byte> buffer, nuint offset, nuint count)
            => !SendBytes(buffer, offset, count, ZSocketFlags.None, out var error)
                ? throw new ZException(error)
                : true;

        /// <summary>
        /// Sends HARD bytes from a byte[n]. Please don't use SendBytes, use instead SendFrame.
        /// </summary>
        public unsafe bool SendBytes(ReadOnlySpan<byte> buffer, nuint offset, nuint count, ZSocketFlags flags, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = ZError.None;

                // int zmq_send (void *socket, void *buf, size_t len, int flags);

                fixed (byte* pBuffer = buffer)
                {
                    var pOffset = pBuffer + offset;

                    //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-SEND");
                    //Console.Error.Flush();
                    while (-1 == zmq.send(SocketPtr, (IntPtr)pOffset, count, flags))
                    {
                        //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-SEND_DONE");
                        //Console.Error.Flush();
                        error = ZError.GetLastError();

                        if (error != ZError.EINTR)
                            return false;

                        error = default;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Sends HARD bytes from a byte[n]. Please don't use SendBytes, use instead SendFrame.
        /// </summary>
        public bool Send(ReadOnlySpan<byte> buffer, nuint offset, nuint count)
            => SendBytes(buffer, offset, count); // just Send*
        /// <summary>
        /// Sends HARD bytes from a byte[n]. Please don't use SendBytes, use instead SendFrame.
        /// </summary>
        public bool Send(ReadOnlySpan<byte> buffer, nuint offset, nuint count, ZSocketFlags flags, out ZError? error)
            => SendBytes(buffer, offset, count, flags, out error); // just Send*


        [MustUseReturnValue]
        public ZMessage? ReceiveMessage()
            => ReceiveMessage(ZSocketFlags.None);


        [MustUseReturnValue]
        public ZMessage? ReceiveMessage(out ZError? error)
            => ReceiveMessage(ZSocketFlags.None, out error);


        [MustUseReturnValue]
        public ZMessage? ReceiveMessage(ZSocketFlags flags)
        {
            var message = ReceiveMessage(flags, out var error);
            if (error != ZError.None)
                throw new ZException(error);
            return message;
        }

        [MustUseReturnValue]
        public ZMessage? ReceiveMessage(ZSocketFlags flags, out ZError? error)
        {
            ZMessage? message = null;
            ReceiveMessage(ref message, flags, out error);
            return message;
        }

        public bool ReceiveMessage(ref ZMessage? message, out ZError? error)
            => ReceiveMessage(ref message, ZSocketFlags.None, out error);

        public bool ReceiveMessage(out ZMessage? message, ZSocketFlags flags, out ZError? error, bool _ = false)
        {
            message = null;
            return ReceiveMessage(ref message, flags, out error);
        }

        public bool ReceiveMessage(ref ZMessage? message, ZSocketFlags flags, out ZError? error)
        {
            EnsureNotDisposed();

            var count = int.MaxValue;
            return ReceiveFrames(ref count, ref message, flags, out error);
        }

        public ZFrame? ReceiveFrame()
        {
            var frame = ReceiveFrame(out var error);
            if (error != ZError.None)
                throw new ZException(error);
            return frame;
        }

        public ZFrame? ReceiveFrame(ZSocketFlags flags)
        {
            var frame = ReceiveFrame(flags, out var error);

            if (error == ZError.None)
                return frame;

            if ((flags & ZSocketFlags.DontWait) != 0 && error == ZError.EAGAIN)
                return frame;

            throw new ZException(error);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ZFrame? ReceiveFrame(out ZError? error)
            => ReceiveFrame(ZSocketFlags.None, out error);

        public ZFrame? ReceiveFrame(ZSocketFlags flags, out ZError? error)
        {
            flags &= ~ZSocketFlags.More;

            var frame = ZFrame.CreateEmpty();

            return frame.Receive(this, flags, out error)
                ? frame
                : null;

        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<ZFrame>? ReceiveFrames(int framesToReceive)
            => ReceiveFrames(framesToReceive, ZSocketFlags.None);

        public IEnumerable<ZFrame>? ReceiveFrames(int framesToReceive, ZSocketFlags flags)
        {
            var frames = ReceiveFrames(framesToReceive, flags, out var error);
            return error != ZError.None
                ? throw new ZException(error)
                : frames;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<ZFrame>? ReceiveFrames(int framesToReceive, out ZError? error)
            => ReceiveFrames(framesToReceive, ZSocketFlags.None, out error);

        public IEnumerable<ZFrame>? ReceiveFrames(int framesToReceive, ZSocketFlags flags, out ZError? error)
        {
            LinkedList<ZFrame>? frames = null;

            if (!ReceiveFrames(ref framesToReceive, ref frames, flags, out error))
                return null;

            return frames != null
                ? LinkedListNodeForwardEnumerator<ZFrame>.Create(frames)
                : null;
        }

        public bool ReceiveFrames(ref int framesToReceive, ref LinkedList<ZFrame>? frames, ZSocketFlags flags, out ZError? error)
        {
            EnsureNotDisposed();

            error = default;
            flags |= ZSocketFlags.More;

            do
            {
                var frame = ZFrame.CreateEmpty();

                if (framesToReceive == 1)
                    flags &= ~ZSocketFlags.More;

                if (!frame.Receive(this, flags, out error))
                    return false;

                frames ??= new();

                frames.AddLast(frame);

            } while (--framesToReceive > 0 && ReceiveMore);

            return true;
        }

        public bool ReceiveFrames(ref int framesToReceive, ref ZMessage? frames, ZSocketFlags flags, out ZError? error)
        {
            lock (_lock)
            {
                EnsureNotDisposed();

                error = default;
                flags |= ZSocketFlags.More;

                do
                {
                    var frame = ZFrame.CreateEmpty();

                    if (framesToReceive == 1)
                        flags &= ~ZSocketFlags.More;

                    if (!frame.Receive(this, flags, out error))
                        return false;

                    frames ??= ZMessage.Create();

                    frames.Add(frame);

                } while (--framesToReceive > 0 && ReceiveMore);

                return true;
            }
        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(ZMessage? msg)
            => SendMessage(msg); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(ZMessage? msg, out ZError? error)
            => SendMessage(msg, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(ZMessage? msg, ZSocketFlags flags)
            => SendMessage(msg, flags); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(ZMessage? msg, ZSocketFlags flags, out ZError? error)
            => SendMessage(msg, flags, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(IEnumerable<ZFrame> frames)
            => SendFrames(frames); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(IEnumerable<ZFrame> frames, out ZError? error)
            => SendFrames(frames, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(IEnumerable<ZFrame> frames, ZSocketFlags flags)
            => SendFrames(frames, flags); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(IEnumerable<ZFrame> frames, ZSocketFlags flags, out ZError? error)
            => SendFrames(frames, flags, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(IEnumerable<ZFrame> frames, ref int sent, ZSocketFlags flags, out ZError? error)
            => SendFrames(frames, ref sent, flags, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(ZFrame frame)
            => SendFrame(frame); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(ZFrame msg, out ZError? error)
            => SendFrame(msg, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendMore(ZFrame frame)
            => SendFrameMore(frame); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendMore(ZFrame msg, out ZError? error)
            => SendFrameMore(msg, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendMore(ZFrame frame, ZSocketFlags flags)
            => SendFrameMore(frame, flags); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendMore(ZFrame msg, ZSocketFlags flags, out ZError? error)
            => SendFrameMore(msg, flags, out error); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Send(ZFrame frame, ZSocketFlags flags)
            => SendFrame(frame, flags); // just Send*

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Send(ZFrame frame, ZSocketFlags flags, out ZError? error)
            => SendFrame(frame, flags, out error); // just Send*


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendMessage(ZMessage msg)
            => SendMessage(msg, ZSocketFlags.None);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendMessage(ZMessage msg, out ZError? error)
            => SendMessage(msg, ZSocketFlags.None, out error);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendMessage(ZMessage msg, ZSocketFlags flags)
        {
            if (!SendMessage(msg, flags, out var error))
                throw new ZException(error);
        }

        public virtual bool SendMessage(ZMessage msg, ZSocketFlags flags, out ZError? error)
        {
            if (msg is null) throw new ArgumentNullException(nameof(msg));

            error = ZError.None;
            var more = (flags & ZSocketFlags.More) == ZSocketFlags.More;
            flags |= ZSocketFlags.More;

            for (int i = 0, l = msg.Count; i < l; ++i)
            {
                var frame = msg.RemoveAt(i, false);

                if (frame == null) continue;

                if (i == l - 1 && !more)
                    flags &= ~ZSocketFlags.More;

                if (!SendFrame(frame, flags, out error))
                    return false;
            }
            return true;

        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrames(IEnumerable<ZFrame> frames)
            => SendFrames(frames, ZSocketFlags.None);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendFrames(IEnumerable<ZFrame> frames, out ZError? error)
            => SendFrames(frames, ZSocketFlags.None, out error);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrames(IEnumerable<ZFrame> frames, ZSocketFlags flags)
        {
            var sent = 0;
            if (!SendFrames(frames, ref sent, flags, out var error))
                throw new ZException(error);
        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendFrames(IEnumerable<ZFrame> frames, ZSocketFlags flags, out ZError? error)
        {
            var sent = 0;
            return SendFrames(frames, ref sent, flags, out error);
        }

        public virtual bool SendFrames(IEnumerable<ZFrame> frames, ref int sent, ZSocketFlags flags, out ZError? error)
        {
            EnsureNotDisposed();

            error = ZError.None;

            var more = (flags & ZSocketFlags.More) == ZSocketFlags.More;
            flags |= ZSocketFlags.More;

            var framesIsList = frames is IList<ZFrame>;
            var _frames = frames.ToArray();

            for (int i = 0, l = _frames.Length; i < l; ++i)
            {
                var frame = _frames[i];

                if (i == l - 1 && !more)
                    flags &= ~ZSocketFlags.More;

                if (!SendFrame(frame, flags, out error))
                    return false;

                if (framesIsList)
                    ((IList<ZFrame>)frames).Remove(frame);

                ++sent;
            }

            return true;
        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrame(ZFrame frame)
            => SendFrame(frame, ZSocketFlags.None);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendFrame(ZFrame msg, out ZError? error)
            => SendFrame(msg, ZSocketFlags.None, out error);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrameMore(ZFrame frame)
            => SendFrame(frame, ZSocketFlags.More);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendFrameMore(ZFrame msg, out ZError? error)
            => SendFrame(msg, ZSocketFlags.More, out error);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrameMore(ZFrame frame, ZSocketFlags flags)
            => SendFrame(frame, flags | ZSocketFlags.More);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool SendFrameMore(ZFrame msg, ZSocketFlags flags, out ZError? error)
            => SendFrame(msg, flags | ZSocketFlags.More, out error);


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SendFrame(ZFrame frame, ZSocketFlags flags)
        {
            if (!SendFrame(frame, flags, out var error))
                throw new ZException(error);
        }

        public virtual bool SendFrame(ZFrame frame, ZSocketFlags flags, out ZError? error)
        {
            EnsureNotDisposed();

            if (!frame.Send(this, flags, out error))
                return false;

            //frame.Dismiss();
            return true;
        }

        public bool Forward(ZSocket destination, out ZMessage? message, out ZError? error)
        {
            EnsureNotDisposed();

            error = default;
            message = null; // message is always null

            var more = false;

            using var frame = ZFrame.CreateEmpty();

            if (frame.IsDismissed) throw new InvalidOperationException("Message was dismissed.");

            do
            {
                if (!frame.Receive(this, ZSocketFlags.None, out error))
                    return false;

                // will have to receive more?
                more = ReceiveMore;

                // sending scope
                if (!frame.Send(this, out error, more))
                    return false;

            } while (more);

            return true;
        }

        private unsafe bool GetOption(ZSocketOption option, IntPtr optionValue, ref nuint optionLength)
        {
            EnsureNotDisposed();

            while (-1 == zmq.getsockopt(_socketPtr, (int)option, optionValue, (nuint*)Unsafe.AsPointer(ref optionLength)))
            {
                var error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    throw new ZException(error);
            }

            return true;
        }

        private unsafe bool GetOption<T>(ZSocketOption option, Span<T> optionValue, ref nuint optionLength)
            where T : unmanaged
        {
            EnsureNotDisposed();

            /*
            Debug.Assert(optionValue.Length * sizeof(T) == (int)optionLength,
                $"Expected same option value size {optionValue.Length * sizeof(T)} and length specified {optionLength}");

            if (option is not ZSocketOption.IMMEDIATE and not ZSocketOption.RCVMORE)
                Console.WriteLine($"Trying to get {option} size {optionLength}");
            */

            fixed (void* pOptionValue = optionValue)
            {
                while (-1 == zmq.getsockopt(_socketPtr, (int)option, (IntPtr)pOptionValue, (nuint*)Unsafe.AsPointer(ref optionLength)))
                {
                    var error = ZError.GetLastError();

                    if (error != ZError.EINTR)
                        throw new ZException(error);

                }
            }

            /*
#if NET5_0_OR_GREATER
            if (option is not ZSocketOption.IMMEDIATE and not ZSocketOption.RCVMORE)
                Console.WriteLine($"Got {option} size {optionLength}: 0x{Convert.ToHexString(MemoryMarshal.AsBytes(optionValue))}");
#endif
            */

            return true;
        }

        // From options.hpp: unsigned char identity [256];
        private const nuint MaxBinaryOptionSize = 256;

        public unsafe bool GetOption(ZSocketOption option, byte[]? value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var optionLength = (nuint)value.Length;

            fixed (byte* pValue = value)
            {
                if (!GetOption(option, (IntPtr)pValue, ref optionLength))
                    return false;
            }

            return true;
        }

        public unsafe bool GetOption(ZSocketOption option, Span<byte> value)
        {
            if (value == default) throw new ArgumentNullException(nameof(value));

            var optionLength = (nuint)value.Length;

            fixed (byte* pValue = value)
            {
                if (!GetOption(option, (IntPtr)pValue, ref optionLength))
                    return false;
            }

            return true;
        }

        public unsafe bool GetOption(ZSocketOption option, out byte[] value, ref nuint size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

            value = new byte[size];
            fixed (byte* pValue = value)
            {
                if (!GetOption(option, (IntPtr)pValue, ref size))
                    return false;
            }

            return true;
        }

        public byte[]? GetOptionBytes(ZSocketOption option, nuint size = MaxBinaryOptionSize)
            => GetOption(option, out var result, ref size)
                ? result
                : null;

#if NETSTANDARD2_0
        public bool GetOption(ZSocketOption option, out string? value)
#else
        public bool GetOption(ZSocketOption option, [NotNullWhen(true)] out string? value)
#endif
        {
            var optionLength = MaxBinaryOptionSize;

            if (!GetOption(option, out var optionValue, ref optionLength))
            {
                value = null;
                return false;
            }

            // remove null padding
            if (optionLength > 0 && optionValue[optionLength - 1] == 0)
                optionLength--;

            value = Encoding.UTF8.GetString(optionValue, 0, (int)optionLength);

            return true;
        }

        public string? GetOptionString(ZSocketOption option)
            => GetOption(option, out string? result)
                ? result
                : null;

        public unsafe bool GetOption<T>(ZSocketOption option, out T value)
            where T : unmanaged
        {
#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
            value = default;
#else
            Unsafe.SkipInit(out item);
#endif

            var optionLength = (nuint)sizeof(T);

#if NETSTANDARD2_0
            fixed (T* pValue = &value)
            {
                var optionValueSpan = new Span<T>(pValue, 1);
                return GetOption(option, optionValueSpan, ref optionLength);
            }
#else
            var optionValueSpan = MemoryMarshal.CreateSpan(ref value, 1);
            return GetOption(option, optionValueSpan, ref optionLength);
#endif

        }

        public T GetOption<T>(ZSocketOption option, T @default = default) where T : unmanaged
            => GetOption<T>(option, out var value) ? value : @default;

        public int GetOptionInt32(ZSocketOption option)
            => GetOption(option, out int result)
                ? result
                : default;

        public bool GetOption(ZSocketOption option, out uint value)
        {
            var result = GetOption(option, out int resultValue);
            value = (uint)resultValue;
            return result;
        }

        public uint GetOptionUInt32(ZSocketOption option)
            => GetOption(option, out uint result)
                ? result
                : default;

        public long GetOptionInt64(ZSocketOption option)
            => GetOption(option, out long result)
                ? result
                : default;

        public bool GetOption(ZSocketOption option, out ulong value)
        {
            var result = GetOption(option, out long resultValue);
            value = (ulong)resultValue;
            return result;
        }

        public ulong GetOptionUInt64(ZSocketOption option)
            => GetOption(option, out ulong result)
                ? result
                : default;


        private bool SetOption(ZSocketOption option, IntPtr optionValue, nuint optionLength)
        {
            EnsureNotDisposed();

            while (-1 == zmq.setsockopt(_socketPtr, (int)option, optionValue, optionLength))
            {
                var error = ZError.GetLastError();

                if (error != ZError.EINTR)
                    return false;
            }

            return true;
        }

        public bool SetOptionNull(ZSocketOption option)
            => SetOption(option, default, 0);

        public unsafe bool SetOption(ZSocketOption option, Span<byte> value)
        {
            if (value == default) return SetOptionNull(option);

            var optionLength = (nuint)value.Length;

            fixed (byte* pValue = value)
                return SetOption(option, (IntPtr)pValue, optionLength);
        }

        public unsafe bool SetOption(ZSocketOption option, string? value)
        {
            if (value == null) return SetOptionNull(option);

            var valueBytes = EncodedStringCache.For(Encoding.UTF8, value);
            fixed (void* pValue = valueBytes)
                return SetOption(option, (IntPtr)pValue, (nuint)valueBytes.Length);
        }

        public unsafe bool SetOption<T>(ZSocketOption option, T value)
            where T : unmanaged
        {
            var optionLength = (nuint)sizeof(T);

            return SetOption(option, (IntPtr)(&value), optionLength);
        }

        /// <summary>
        /// Subscribe to all messages.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        public void SubscribeAll()
            => Subscribe(Array.Empty<byte>());

        /// <summary>
        /// Subscribe to messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        public virtual void Subscribe(byte[] prefix)
            => SetOption(ZSocketOption.SUBSCRIBE, prefix);

        /// <summary>
        /// Subscribe to messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        public virtual void Subscribe(string prefix)
            => SetOption(ZSocketOption.SUBSCRIBE, ZContext.Encoding.GetBytes(prefix));

        /// <summary>
        /// Unsubscribe from all messages.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        public void UnsubscribeAll()
            => Unsubscribe(Array.Empty<byte>());

        /// <summary>
        /// Unsubscribe from messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        public virtual void Unsubscribe(byte[] prefix)
            => SetOption(ZSocketOption.UNSUBSCRIBE, prefix);

        /// <summary>
        /// Unsubscribe from messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        public virtual void Unsubscribe(string prefix)
            => SetOption(ZSocketOption.UNSUBSCRIBE, ZContext.Encoding.GetBytes(prefix));

        /// <summary>
        /// Gets a value indicating whether the multi-part message currently being read has more message parts to follow.
        /// </summary>
        public bool ReceiveMore => GetOptionInt32(ZSocketOption.RCVMORE) == 1;

        public string? LastEndpoint => GetOptionString(ZSocketOption.LAST_ENDPOINT);

        /// <summary>
        /// Gets or sets the I/O thread affinity for newly created connections on this socket.
        /// </summary>
        public ulong Affinity
        {
            get => GetOptionUInt64(ZSocketOption.AFFINITY);
            set => SetOption(ZSocketOption.AFFINITY, value);
        }

        /// <summary>
        /// Gets or sets the maximum length of the queue of outstanding peer connections. (Default = 100 connections).
        /// </summary>
        public int Backlog
        {
            get => GetOptionInt32(ZSocketOption.BACKLOG);
            set => SetOption(ZSocketOption.BACKLOG, value);
        }

        public byte[]? ConnectRID
        {
            get => GetOptionBytes(ZSocketOption.CONNECT_RID);
            set => SetOption(ZSocketOption.CONNECT_RID, value);
        }

        public bool Conflate
        {
            get => GetOptionInt32(ZSocketOption.CONFLATE) == 1;
            set => SetOption(ZSocketOption.CONFLATE, value ? 1 : 0);
        }

        public const int BinaryKeySize = 32;

        public byte[]? CurvePublicKey
        {
            get => GetOptionBytes(ZSocketOption.CURVE_PUBLICKEY, BinaryKeySize);
            set => SetOption(ZSocketOption.CURVE_PUBLICKEY, value);
        }

        public byte[]? CurveSecretKey
        {
            get => GetOptionBytes(ZSocketOption.CURVE_SECRETKEY, BinaryKeySize);
            set => SetOption(ZSocketOption.CURVE_SECRETKEY, value);
        }

        public bool CurveServer
        {
            get => GetOptionInt32(ZSocketOption.CURVE_SERVER) == 1;
            set => SetOption(ZSocketOption.CURVE_SERVER, value ? 1 : 0);
        }

        public byte[]? CurveServerKey
        {
            get => GetOptionBytes(ZSocketOption.CURVE_SERVERKEY, BinaryKeySize);
            set => SetOption(ZSocketOption.CURVE_SERVERKEY, value);
        }

        public bool GSSAPIPlainText
        {
            get => GetOptionInt32(ZSocketOption.GSSAPI_PLAINTEXT) == 1;
            set => SetOption(ZSocketOption.GSSAPI_PLAINTEXT, value ? 1 : 0);
        }

        public string? GSSAPIPrincipal
        {
            get => GetOptionString(ZSocketOption.GSSAPI_PRINCIPAL);
            set => SetOption(ZSocketOption.GSSAPI_PRINCIPAL, value);
        }

        public bool GSSAPIServer
        {
            get => GetOptionInt32(ZSocketOption.GSSAPI_SERVER) == 1;
            set => SetOption(ZSocketOption.GSSAPI_SERVER, value ? 1 : 0);
        }

        public string? GSSAPIServicePrincipal
        {
            get => GetOptionString(ZSocketOption.GSSAPI_SERVICE_PRINCIPAL);
            set => SetOption(ZSocketOption.GSSAPI_SERVICE_PRINCIPAL, value);
        }

        public int HandshakeInterval
        {
            get => GetOptionInt32(ZSocketOption.HANDSHAKE_IVL);
            set => SetOption(ZSocketOption.HANDSHAKE_IVL, value);
        }

        /// <summary>
        /// Gets or sets the Identity.
        /// </summary>
        /// <value>Identity as byte[]</value>
        public byte[]? Identity
        {
            get => GetOptionBytes(ZSocketOption.IDENTITY);
            set => SetOption(ZSocketOption.IDENTITY, value);
        }

        /// <summary>
        /// Gets or sets the Identity.
        /// Note: The string contains chars like \0 (null terminator,
        /// which are NOT printed (in Console.WriteLine)!
        /// </summary>
        /// <value>Identity as string</value>
        public string? IdentityString
        {
            get => Identity == null ? null : ZContext.Encoding.GetString(Identity);
            set => Identity = value == null ? null : ZContext.Encoding.GetBytes(value);
        }

        public bool Immediate
        {
            get => GetOptionInt32(ZSocketOption.IMMEDIATE) == 1;
            set => SetOption(ZSocketOption.IMMEDIATE, value ? 1 : 0);
        }

        public bool IPv6
        {
            get => GetOptionInt32(ZSocketOption.IPV6) == 1;
            set => SetOption(ZSocketOption.IPV6, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the linger period for socket shutdown. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
        /// </summary>
        public TimeSpan Linger
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.LINGER));
            set => SetOption(ZSocketOption.LINGER, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the maximum size for inbound messages (bytes). (Default = -1, no limit).
        /// </summary>
        public long MaxMessageSize
        {
            get => GetOptionInt64(ZSocketOption.MAX_MSG_SIZE);
            set => SetOption(ZSocketOption.MAX_MSG_SIZE, value);
        }

        /// <summary>
        /// Gets or sets the time-to-live field in every multicast packet sent from this socket (network hops). (Default = 1 hop).
        /// </summary>
        public int MulticastHops
        {
            get => GetOptionInt32(ZSocketOption.MULTICAST_HOPS);
            set => SetOption(ZSocketOption.MULTICAST_HOPS, value);
        }

        public string? PlainPassword
        {
            get => GetOptionString(ZSocketOption.PLAIN_PASSWORD);
            set => SetOption(ZSocketOption.PLAIN_PASSWORD, value);
        }

        public bool PlainServer
        {
            get => GetOptionInt32(ZSocketOption.PLAIN_SERVER) == 1;
            set => SetOption(ZSocketOption.PLAIN_SERVER, value ? 1 : 0);
        }

        public string? PlainUserName
        {
            get => GetOptionString(ZSocketOption.PLAIN_USERNAME);
            set => SetOption(ZSocketOption.PLAIN_USERNAME, value);
        }

        public bool ProbeRouter
        {
            get => GetOptionInt32(ZSocketOption.PROBE_ROUTER) == 1;
            set => SetOption(ZSocketOption.PROBE_ROUTER, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the maximum send or receive data rate for multicast transports (kbps). (Default = 100 kbps).
        /// </summary>
        public int MulticastRate
        {
            get => GetOptionInt32(ZSocketOption.RATE);
            set => SetOption(ZSocketOption.RATE, value);
        }

        /// <summary>
        /// Gets or sets the underlying kernel receive buffer size for the current socket (bytes). (Default = 0, OS default).
        /// </summary>
        public int ReceiveBufferSize
        {
            get => GetOptionInt32(ZSocketOption.RCVBUF);
            set => SetOption(ZSocketOption.RCVBUF, value);
        }

        /// <summary>
        /// Gets or sets the high water mark for inbound messages (number of messages). (Default = 0, no limit).
        /// </summary>
        public int ReceiveHighWatermark
        {
            get => GetOptionInt32(ZSocketOption.RCVHWM);
            set => SetOption(ZSocketOption.RCVHWM, value);
        }

        /// <summary>
        /// Gets or sets the timeout for receive operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RCVTIMEO));
            set => SetOption(ZSocketOption.RCVTIMEO, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the initial reconnection interval. (Default = 100 milliseconds).
        /// </summary>
        public TimeSpan ReconnectInterval
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECONNECT_IVL));
            set => SetOption(ZSocketOption.RECONNECT_IVL, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the maximum reconnection interval. (Default = 0, only use <see cref="ReconnectInterval"/>).
        /// </summary>
        public TimeSpan ReconnectIntervalMax
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECONNECT_IVL_MAX));
            set => SetOption(ZSocketOption.RECONNECT_IVL_MAX, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the recovery interval for multicast transports. (Default = 10 seconds).
        /// </summary>
        public TimeSpan MulticastRecoveryInterval
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECOVERY_IVL));
            set => SetOption(ZSocketOption.RECOVERY_IVL, (int)value.TotalMilliseconds);
        }

        public bool RequestCorrelate
        {
            get => GetOptionInt32(ZSocketOption.REQ_CORRELATE) == 1;
            set => SetOption(ZSocketOption.REQ_CORRELATE, value ? 1 : 0);
        }

        public bool RequestRelaxed
        {
            get => GetOptionInt32(ZSocketOption.REQ_RELAXED) == 1;
            set => SetOption(ZSocketOption.REQ_RELAXED, value ? 1 : 0);
        }

        public bool RouterHandover
        {
            get => GetOptionInt32(ZSocketOption.ROUTER_HANDOVER) == 1;
            set => SetOption(ZSocketOption.ROUTER_HANDOVER, value ? 1 : 0);
        }

        public RouterMandatory RouterMandatory
        {
            get => (RouterMandatory)GetOptionInt32(ZSocketOption.ROUTER_MANDATORY);
            set => SetOption(ZSocketOption.ROUTER_MANDATORY, (int)value);
        }

        public bool RouterRaw
        {
            get => GetOptionInt32(ZSocketOption.ROUTER_RAW) == 1;
            set => SetOption(ZSocketOption.ROUTER_RAW, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the underlying kernel transmit buffer size for the current socket (bytes). (Default = 0, OS default).
        /// </summary>
        public int SendBufferSize
        {
            get => GetOptionInt32(ZSocketOption.SNDBUF);
            set => SetOption(ZSocketOption.SNDBUF, value);
        }

        /// <summary>
        /// Gets or sets the high water mark for outbound messages (number of messages). (Default = 0, no limit).
        /// </summary>
        public int SendHighWatermark
        {
            get => GetOptionInt32(ZSocketOption.SNDHWM);
            set => SetOption(ZSocketOption.SNDHWM, value);
        }

        /// <summary>
        /// Gets or sets the timeout for send operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.SNDTIMEO));
            set => SetOption(ZSocketOption.SNDTIMEO, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the override value for the SO_KEEPALIVE TCP socket option. (where supported by OS). (Default = -1, OS default).
        /// </summary>
        public TcpKeepaliveBehaviour TcpKeepAlive
        {
            get => (TcpKeepaliveBehaviour)GetOptionInt32(ZSocketOption.TCP_KEEPALIVE);
            set => SetOption(ZSocketOption.TCP_KEEPALIVE, (int)value);
        }

        /// <summary>
        /// Gets or sets the override value for the 'TCP_KEEPCNT' socket option (where supported by OS). (Default = -1, OS default).
        /// The default value of '-1' means to skip any overrides and leave it to OS default.
        /// </summary>
        public int TcpKeepAliveCount
        {
            get => GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_CNT);
            set => SetOption(ZSocketOption.TCP_KEEPALIVE_CNT, value);
        }

        /// <summary>
        /// Gets or sets the override value for the TCP_KEEPCNT (or TCP_KEEPALIVE on some OS). (Default = -1, OS default).
        /// </summary>
        public int TcpKeepAliveIdle
        {
            get => GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_IDLE);
            set => SetOption(ZSocketOption.TCP_KEEPALIVE_IDLE, value);
        }

        /// <summary>
        /// Gets or sets the override value for the TCP_KEEPINTVL socket option (where supported by OS). (Default = -1, OS default).
        /// </summary>
        public int TcpKeepAliveInterval
        {
            get => GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_INTVL);
            set => SetOption(ZSocketOption.TCP_KEEPALIVE_INTVL, value);
        }

        public int TypeOfService
        {
            get => GetOptionInt32(ZSocketOption.TOS);
            set => SetOption(ZSocketOption.TOS, value);
        }

        public bool XPubVerbose
        {
            get => GetOptionInt32(ZSocketOption.XPUB_VERBOSE) == 1;
            set => SetOption(ZSocketOption.XPUB_VERBOSE, value ? 1 : 0);
        }

        public string? ZapDomain
        {
            get => GetOptionString(ZSocketOption.ZAP_DOMAIN);
            set => SetOption(ZSocketOption.ZAP_DOMAIN, value);
        }

        public bool ZapEnforceDomain
        {
            get => GetOptionInt32(ZSocketOption.ZAP_ENFORCE_DOMAIN) == 1;
            set => SetOption(ZSocketOption.ZAP_ENFORCE_DOMAIN, value ? 1 : 0);
        }

        /// <summary>
        /// Add a filter that will be applied for each new TCP transport connection on a listening socket.
        /// Example: "127.0.0.1", "mail.ru/24", "::1", "::1/128", "3ffe:1::", "3ffe:1::/56"
        /// </summary>
        /// <seealso cref="ClearTcpAcceptFilter"/>
        /// <remarks>
        /// If no filters are applied, then TCP transport allows connections from any IP.
        /// If at least one filter is applied then new connection source IP should be matched.
        /// </remarks>
        /// <param name="filter">IPV6 or IPV4 CIDR filter.</param>
        public void AddTcpAcceptFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                throw new ArgumentNullException(nameof(filter));

            SetOption(ZSocketOption.TCP_ACCEPT_FILTER, filter);
        }

        /// <summary>
        /// Reset all TCP filters assigned by <see cref="AddTcpAcceptFilter"/>
        /// and allow TCP transport to accept connections from any IP.
        /// </summary>
        public void ClearTcpAcceptFilter()
            => SetOption(ZSocketOption.TCP_ACCEPT_FILTER, (string)null);

        public bool IPv4Only
        {
            get => GetOptionInt32(ZSocketOption.IPV4_ONLY) == 1;
            set => SetOption(ZSocketOption.IPV4_ONLY, value ? 1 : 0);
        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotDisposed()
        {
            if (_socketPtr == default)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Gets or sets the underlying kernel transmit buffer size for the current socket (bytes). (Default = 0, OS default).
        /// </summary>
        public ZSocketType Type
        {
            get => GetOption<ZSocketType>(ZSocketOption.TYPE);
            set => SetOption(ZSocketOption.TYPE, value);
        }

        public override string ToString()
            => Name ?? LastEndpoint ?? nameof(ZSocket);
    }
}
