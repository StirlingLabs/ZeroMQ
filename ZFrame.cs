using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// A single part message, sent or received via a <see cref="ZSocket"/>.
    /// </summary>
    public sealed partial class ZFrame : ICloneable, IDisposable
    {
        public static readonly nuint MinimumFrameSize = zmq.sizeof_zmq_msg_t;

        public const nuint DefaultFrameSize = 4096;

        private readonly object _lock = new();
        private int _stateFlags;
        private nuint _length;
        private nuint _position;
        private IntPtr _msg;


        private static bool _isCachingEnabled = false;

        public static bool IsCachingEnabled
        {
            get => _isCachingEnabled;
            set {
                var currentlyEnabled = _isCachingEnabled;

                if (value == currentlyEnabled)
                    return;

                if (currentlyEnabled && !value)
                {
                    _isCachingEnabled = false;
#if NETSTANDARD2_0
                    Thread.Sleep(1);
#else
                    Thread.Yield();
                    Interlocked.MemoryBarrierProcessWide();
#endif
                    FlushObjectCaches();
                }

                _isCachingEnabled = value;
            }
        }

        public static void FlushObjectCaches() { }


        public static ZFrame FromStream(Stream stream, nint i, nuint l)
        {
            stream.Position = i;
            if (l == 0) return Create();

            var frame = Create(l);
            var bytesPool = ArrayPool<byte>.Shared;
            var buf = bytesPool.Rent(65535);
            try
            {
                int bufLen;
                nint current = -1;
                do
                {
                    ++current;
                    var remaining = Math.Min(Math.Max(0, (nint)l - current * buf.Length), buf.Length);
                    if (remaining < 1) break;

                    bufLen = stream.Read(buf, 0, checked((int)remaining));
                    frame.Write(buf, 0, bufLen);

                } while (bufLen > 0);
            }
            finally
            {
                bytesPool.Return(buf);
            }

            frame.Position = 0;
            return frame;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame CopyFrom(ZFrame frame)
            => frame.Clone();

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create()
            => CreateEmpty();

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ZFrame(IntPtr msg)
            => _msg = msg;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(nuint size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            //ZFrame r = TryReclaimFrame(out var f) ? f : new();
            var r = new ZFrame(zmq.AllocNative(zmq.sizeof_zmq_msg_t));
            r.CreateNative(size);
            Debug.Assert(r._msg != default, "Missing message after CreateNative N");
            r._length = size;
            r._position = 0;
            return r;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(byte[] data)
            => Create(data, 0, (nuint)data.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(byte[] data, nuint offset, nuint length)
        {
            if (length == 0) return CreateEmpty();
            //ZFrame r = TryReclaimFrame(out var f) ? f : new();
            ZFrame r = new(zmq.AllocNative(zmq.sizeof_zmq_msg_t));
            r.CreateNative(length);
            Debug.Assert(r._msg != default, "Missing message after CreateNative M");
            r._length = length;
            r._position = 0;
            r.Write(data, (int)offset, (int)length);
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(string str, Encoding encoding)
        {
            //ZFrame r = TryReclaimFrame(out var f) ? f : new();
            ZFrame r = new(zmq.AllocNative(zmq.sizeof_zmq_msg_t));
            r._length = 0;
            r._position = 0;
            r.WriteStringNative(str, encoding, true);
            Debug.Assert(r._msg != default, "Missing message after WriteStringNative");
            return r;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(string str)
            => Create(str, ZContext.Encoding);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ZFrame Create<T>(T value)
            where T : unmanaged
        {
            var size = (nuint)sizeof(T);
            //ZFrame r = TryReclaimFrame(out var f) ? f : new();
            ZFrame r = new(zmq.AllocNative(zmq.sizeof_zmq_msg_t));
            r.CreateNative(size);
            Debug.Assert(r._msg != default, "Missing message after CreateNative G");
            r._length = size;
            r._position = 0;
            r.WritePrimitive(value);
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame CreateEmpty()
        {
            //ZFrame r = TryReclaimFrame(out var f) ? f : new();
            ZFrame r = new(zmq.AllocNative(zmq.sizeof_zmq_msg_t));
            r.CreateEmptyNative();
            Debug.Assert(r._msg != default, "Missing message after CreateEmptyNative");
            r._length = 0;
            r._position = 0;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreateEmptyNative()
        {
            lock (_lock)
            {
                Debug.Assert(_msg != default, "CreateEmptyNative; _msg is missing");
                Debug.Assert(!IsUnavailable, "CreateEmptyNative; Should not be unavailable");

                if (0 == zmq.msg_init(_msg))
                    return;

                var error = ZError.GetLastError();
                throw new ZException(error, "zmq_msg_init");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreateNative(nuint size)
        {
            lock (_lock)
            {
                Debug.Assert(_msg != default, "CreateNative; _msg is missing");
                Debug.Assert(!IsUnavailable, "CreateNative; Should not be unavailable");
                while (-1 == zmq.msg_init_size(_msg, size))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM)
                        throw new OutOfMemoryException("zmq_msg_init_size");

                    throw new ZException(error, "zmq_msg_init_size");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreatePinned(byte[] data, nuint offset, nuint length)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var size = (nuint)data.Length;
            if (size == 0) throw new ArgumentException("Array must not be empty.", nameof(data));
            Debug.Assert(_msg != default, "CreatePinned(byte[]); _msg is missing");
            CreatePinned(GCHandle.Alloc(data, GCHandleType.Pinned), offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CreatePinned(GCHandle pin, nuint offset, nuint length)
        {
            Debug.Assert(!IsUnavailable, "CreatePinned; Should not be unavailable");

            if (pin.Target is not byte[] bytes)
                throw new InvalidOperationException(
                    $"GCHandle must point to a simple byte array, but was {pin.Target?.GetType().AssemblyQualifiedName ?? "null"} (allocated: {pin.IsAllocated} @ 0x{pin.AddrOfPinnedObject():X8})");

            //Console.Error.WriteLine($"[{CallStackHelpers.GetCallStackDepth()}] ZFrame.CreatePinned(GCH 0x{(IntPtr)pin:X8}, {size})");

#if NET5_0_OR_GREATER
            var pBytes = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(bytes)) + (nint)offset;
#else
            var pBytes = (IntPtr)Unsafe.AsPointer(ref bytes[0]);
#endif
            lock (_lock)
            {
                Debug.Assert(_msg != default, "CreatePinned; _msg is missing");
                Debug.Assert(!IsUnavailable, "CreatePinned; Should not be unavailable");

#if NET5_0_OR_GREATER
                while (-1 == zmq.msg_init_data(_msg, pBytes, length, &PinnedGcHandleFreeCallback, GCHandle.ToIntPtr(pin)))
#else
                while (-1 == zmq.msg_init_data(_msg, pBytes, length, PinnedGcHandleFreeCallback, GCHandle.ToIntPtr(pin)))
#endif
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM)
                        throw new OutOfMemoryException("zmq_msg_init_data");

                    throw new ZException(error, "zmq_msg_init_data");
                }
            }

        }

#if NET6_0_OR_GREATER
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "ZeroMQ.ZFrame.PinnedGcHandleFreeCallback")]
#elif NET5_0_OR_GREATER
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "ZeroMQ.ZFrame.PinnedGcHandleFreeCallback")]
#endif
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PinnedGcHandleFreeCallback(IntPtr dataAddr, IntPtr pinPtr)
            => GCHandle.FromIntPtr(pinPtr).Free();

#if TRACE
        private struct CallerInfo
        {
            public int MetadataToken;
            public string? FilePath;
            public int? LineNumber;

            public override string ToString()
            {
                var mdToken = MetadataToken;
                switch (mdToken)
                {
                    case 0:
                        return $"(Metadata missing) {FilePath}:{LineNumber}";
                    case -1:
                        // Caller provided metadata as absolute
                        return $"{FilePath}:{LineNumber}";
                    default:
                        try
                        {
                            var sym = typeof(ZFrame).Module.ResolveMethod(mdToken);
                            var t = sym?.ReflectedType;
                            return sym is not null
                                // ReSharper disable once ConstantNullCoalescingCondition
                                ? $"{{MDToken 0x{(uint)mdToken:X8}}} {t?.FullName ?? "???"}.{sym.Name ?? "???"} @ {FilePath}:{LineNumber}"
                                : $"{{MDToken 0x{(uint)mdToken:X8}}} ??? @ {FilePath}:{LineNumber}";
                        }
                        catch
                        {
                            return $"{{MDToken 0x{(uint)mdToken:X8}}} ??? @ {FilePath}:{LineNumber}";
                        }
                }
            }

            public static implicit operator string(CallerInfo ci) => ci.ToString();
        }
#endif

#if TRACE
        private CallerInfo? DisposeCaller;

        void IDisposable.Dispose()
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
#endif
        {
            lock (_lock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

#if TRACE
                DisposeCaller = new()
                {
                    MetadataToken = 0,
                    FilePath = "IDispose implementation",
                    LineNumber = -1
                };
#endif

                /*try
                {*/
                Close();
                /*}
                catch
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                }*/

                if (!TryRecycleFrame())
                {
                    // oh well, try to help catch any use-after-free
                    _stateFlags = -1;
                    _position = (nuint)ulong.MaxValue;
                    _length = (nuint)ulong.MaxValue;
                }
            }
        }

#if TRACE
        public void Dispose(
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNum = default)
        {
            lock (_lock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                if (filePath is not null)
                    DisposeCaller = new()
                    {
                        MetadataToken = -1,
                        FilePath = filePath,
                        LineNumber = lineNum
                    };
                else
                {
                    var sf = new StackFrame(1, true);
                    DisposeCaller = new()
                    {
                        MetadataToken = sf.GetMethod()?.MetadataToken ?? 0,
                        FilePath = sf.GetFileName(),
                        LineNumber = sf.GetFileLineNumber()
                    };
                }

                try
                {
                    Close();
                }
                catch
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                }

                if (!TryRecycleFrame())
                {
                    // oh well, try to help catch any use-after-free
                    _stateFlags = -1;
                    _position = (nuint)ulong.MaxValue;
                    _length = (nuint)ulong.MaxValue;
                }
            }
        }
#endif

#if TRACE
        private CallerInfo? DismissCaller;

        public void Dismiss(
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNum = default)
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dismiss()
#endif
        {
            lock (_lock)
            {

                if (IsDismissed) return;
                IsDismissed = true;
#if TRACE
                if (filePath is not null)
                    DismissCaller = new()
                    {
                        MetadataToken = -1,
                        FilePath = filePath,
                        LineNumber = lineNum
                    };
                else
                {
                    var sf = new StackFrame(1, true);
                    DismissCaller = new()
                    {
                        MetadataToken = sf.GetMethod()?.MetadataToken ?? 0,
                        FilePath = sf.GetFileName(),
                        LineNumber = sf.GetFileLineNumber()
                    };
                }
#endif

                Close();

                /*
                if (!TryRecycleMsg())
                    _msg_handle?.Dispose();

                _msg_handle = null;*/

                zmq.FreeNative(Interlocked.Exchange(ref _msg, default));
            }
        }


        /*
        [MustUseReturnValue]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryRecycleMsg()
            => IsCachingEnabled
                && _msg != default
                && FreeMsgs.TryAdd(_msg);*/

        [MustUseReturnValue]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryRecycleFrame()
        {
            /*
            if (!IsCachingEnabled) return false;
            lock (_lock)
            {
                Debug.Assert(IsClosed, "must be closed");
                Debug.Assert(IsDismissed, "must be dismissed");
                Debug.Assert(IsDisposed, "must be disposed");
                Debug.Assert(_msg_handle == null, "_msg_handle must be freed");
                return FreeFrames.TryAdd(this);
            }
            */
            return false;
        }

        public bool CanRead => true;

        public bool CanSeek => true;

        public bool CanTimeout => false;

        public bool CanWrite => true;

        public long Length
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)GetLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetLength()
        {
            UnavailableCheck();

            lock (_lock)
            {
                ExceptionDispatchInfo edi = null!;
                for (var attempts = 0; attempts <= 3; ++attempts)
                {
                    try
                    {
                        _length = zmq.msg_size(_msg);
                        if (_length >= 0)
                            return _length;
                    }
                    catch (Exception ex)
                    {
                        edi = ExceptionDispatchInfo.Capture(ex);
                    }
#if NETSTANDARD2_0
                    Thread.Sleep(1);
#else
                    Thread.Yield();
                    Interlocked.MemoryBarrierProcessWide();
#endif
                }
                edi.Throw();
                return 0;
            }
        }

        public void SetLength(long _)
            => throw new NotSupportedException();

        public nuint Position
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetPosition();

            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Seek(value, SeekOrigin.Begin);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetPosition()
            => _position;


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr MsgPtr()
            => _msg;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr DataPtr()
        {
            UnavailableCheck();

            lock (_lock)
            {
                ExceptionDispatchInfo edi = null!;
                for (var attempts = 0; attempts <= 3; ++attempts)
                {
                    try
                    {
                        var ptr = zmq.msg_data(_msg);
                        if (ptr != default) return ptr;
                    }
                    catch (Exception ex)
                    {
                        edi = ExceptionDispatchInfo.Capture(ex);
                    }
#if NETSTANDARD2_0
                    Thread.Sleep(1);
#else
                    Thread.Yield();
                    Interlocked.MemoryBarrierProcessWide();
#endif
                }
                edi.Throw();
                return default;
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Data()
            => (byte*)DataPtr();

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref byte DataRef()
            => ref Unsafe.AsRef<byte>((void*)DataPtr());

        public nuint Seek(nuint offset, SeekOrigin origin)
        {
            var length = GetLength();

            var pos = origin switch
            {
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => length + offset,
                _ => offset
            };

            if (pos < 0 || pos > length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return _position = pos;
        }

        public unsafe byte[] Read()
        {
            var attempts = 0;
            for (;;)
            {
                lock (_lock)
                {
                    // NOTE: DataRef should be called before GetLength
                    ref var dataRef = ref DataRef();

#if NETSTANDARD2_0 || !NET5_0_OR_GREATER
                    if (Unsafe.AsPointer(ref dataRef) == default)
                    {
                        if (++attempts >= 3) return Array.Empty<byte>();
                        Thread.Sleep(1);
                        continue;
                    }
#else
                    if (Unsafe.IsNullRef(ref dataRef))
                    {
                        if (++attempts >= 3) return Array.Empty<byte>();
                        Thread.Yield();
                        Interlocked.MemoryBarrierProcessWide();
                        continue;
                    }
#endif

                    var length = GetLength();

                    var buffer = new byte[length];

                    var remaining = (nint)length - (nint)_position;

                    if (remaining <= 0) return Array.Empty<byte>();

                    Unsafe.CopyBlockUnaligned(
                        ref buffer[0],
                        ref Unsafe.Add(ref dataRef, (nint)_position),
                        (uint)remaining);

                    _position += (nuint)remaining;

                    return buffer;
                }
            }

        }

        public byte[]? Read(int count)
        {
            var remaining = Math.Min(count, checked((int)Math.Max(0, GetLength() - _position)));

            switch (remaining)
            {
                case 0: return Array.Empty<byte>();
                case < 0: return null;
            }

            var bytes = new byte[remaining];

            Read(bytes, 0, remaining);

            return bytes;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(byte[] buffer, int offset, int count)
            => Read(new Span<byte>(buffer, offset, count));

#if NETSTANDARD2_0
        public unsafe int Read(Span<byte> buffer)
#else
        public unsafe int Read(Span<byte> buffer)
#endif
        {
            var attempts = 0;
            for (;;)
            {
                lock (_lock)
                {
                    var count = (nuint)buffer.Length;

                    // NOTE: DataRef should be called before GetLength
                    ref var dataRef = ref DataRef();

#if NETSTANDARD2_0 || !NET5_0_OR_GREATER
                    if (Unsafe.AsPointer(ref dataRef) == default)
                    {
                        if (++attempts >= 3) return 0;
                        Thread.Sleep(1);
                        continue;
                    }
#else
                    if (Unsafe.IsNullRef(ref dataRef))
                    {
                        if (++attempts >= 3) return 0;
                        Thread.Yield();
                        Interlocked.MemoryBarrierProcessWide();
                        continue;
                    }
#endif

                    var length = GetLength();

                    if (_position + count > length)
                    {
                        count = length - _position;
                        if (count <= 0) return 0;
                    }

                    Unsafe.CopyBlockUnaligned(
                        ref buffer.GetPinnableReference(),
                        ref Unsafe.Add(ref dataRef, (nint)_position),
                        (uint)count);

                    _position += count;
                    return (int)count;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int ReadByte()
            => _position + 1 > GetLength() ? -1 : *(Data() + _position++);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte ReadAsByte()
            => _position + 1 > GetLength() ? default : *(Data() + _position++);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe short ReadInt16()
        {
            if (_position + 2 > GetLength()) return default;
            var v = *(short*)(Data() + _position);
            _position += 2;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
            => (ushort)ReadInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar()
            => (char)ReadInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int ReadInt32()
        {
            if (_position + 4 > GetLength()) return default;
            var v = *(int*)(Data() + _position);
            _position += 4;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
            => (char)ReadInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe long ReadInt64()
        {
            if (_position + 8 > GetLength()) return default;
            var v = *(long*)(Data() + _position);
            _position += 8;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
            => (ulong)ReadInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T Read<T>() where T : unmanaged
        {
            var size = (nuint)sizeof(T);
            if (_position + size > GetLength()) return default;
            var v = *(T*)(Data() + _position);
            _position += size;
            return v;
        }

        [MustUseReturnValue]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryRead<T>(out T item) where T : unmanaged
        {
            var size = (nuint)sizeof(T);
            if (_position + size > GetLength())
            {
#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
                item = default;
#else
                Unsafe.SkipInit(out item);
#endif
                return false;
            }
            item = *(T*)(Data() + _position);
            _position += size;
            return true;
        }

        [MustUseReturnValue]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryRead<T>(ref T[] buffer) where T : unmanaged
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            var count = (uint)buffer.Length;
            var itemSize = (uint)sizeof(T);
            var size = itemSize * count;
            if (_position + size > GetLength())
            {
                return false;
            }
            Unsafe.CopyBlockUnaligned(
#if !NET5_0_OR_GREATER
                ref Unsafe.As<T, byte>(ref buffer[0]),
#else
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(buffer)),
#endif
                ref DataRef(),
                size
            );
            _position += size;
            return true;
        }

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString()
            => ReadString(ZContext.Encoding);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadString(out string? value)
            => TryReadString(ZContext.Encoding, out value);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(Encoding encoding)
            => ReadString( /* byteCount */ GetLength() - _position, encoding);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadString(Encoding encoding, out string? value)
            => TryReadString( /* byteCount */ GetLength() - _position, encoding, out value);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(nuint length)
            => ReadString( /* byteCount */ length, ZContext.Encoding);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadString(nuint length, out string? value)
            => TryReadString( /* byteCount */ length, ZContext.Encoding, out value);

        [MustUseReturnValue]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(nuint byteCount, Encoding encoding)
            => TryReadString(byteCount, encoding, out var str) ? str : null;

        [MustUseReturnValue]
        public bool TryReadString(nuint byteCount, Encoding encoding, out string? value)
        {

            unsafe
            {
                byte* bytes;

                for (;;)
                {
                    bytes = Data();
                    if (bytes != default) break;
#if NETSTANDARD2_0
                    Thread.Sleep(1);
#else
                    Thread.Yield();
                    Interlocked.MemoryBarrierProcessWide();
#endif
                }

                var remaining = checked((int)Math.Min(byteCount, Math.Max(0, GetLength() - _position)));

                if (remaining == 0)
                {
                    value = string.Empty;
                    return true;
                }

                if (remaining < 0)
                {
                    value = null;
                    return false;
                }

                bytes += _position;

                //var dec = encoding.GetDecoder();
                //var charCount = dec.GetCharCount(bytes, remaining, false);
                var charCount = encoding.GetCharCount(bytes, remaining);

                if (charCount == 0)
                {
                    value = string.Empty;
                    return true;
                }

                //var resultChars = new char[charCount];
                var charPool = ArrayPool<char>.Shared;
                var resultChars = charPool.Rent(charCount); // new char[charCount];
                try
                {
                    fixed (char* chars = resultChars)
                    {
                        //charCount = dec.GetChars(bytes, remaining, chars, charCount, true);
                        charCount = encoding.GetChars(bytes, remaining, chars, charCount);

                        _position += (nuint)encoding.GetByteCount(chars, charCount);

                        if (chars[charCount - 1] != '\0' && chars[charCount] == '\0')
                            _position += 1;

                        if (charCount == 0)
                            value = string.Empty;

                        else
                        {
#if NETSTANDARD2_0
                            string s = new(chars, 0, charCount);
#else
                            string s = string.Create(charCount, (p: (IntPtr)chars, charCount), (charSpan, o) => {
                                var pChars = (char*)o.p;
                                fixed (char* pCharSpan = charSpan)
                                    Unsafe.CopyBlockUnaligned(pCharSpan, pChars, (uint)(o.charCount * 2));
                            });
#endif
                            value = string.IsInterned(s) ?? s;
                        }

                        return true;
                    }
                }
                finally
                {
                    charPool.Return(resultChars);
                }
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadLine()
            => ReadLine(GetLength() - _position, ZContext.Encoding);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadLine(Encoding encoding)
            => ReadLine(GetLength() - _position, encoding);

        public string? ReadLine(nuint byteCount, Encoding encoding)
        {
            var remaining = checked((int)Math.Min(byteCount, Math.Max(0, GetLength() - _position)));

            if (remaining == 0)
                return string.Empty;

            if (remaining < 0)
                return null;

            unsafe
            {
                var bytes = Data() + _position;

                //var dec = encoding.GetDecoder();
                //var charCount = dec.GetCharCount(bytes, remaining, false);
                var charCount = encoding.GetCharCount(bytes, remaining);
                if (charCount == 0) return string.Empty;

                var charPool = ArrayPool<char>.Shared;
                var resultChars = charPool.Rent(charCount); // new char[charCount];
                try
                {
                    fixed (char* chars = resultChars)
                    {
                        //charCount = dec.GetChars(bytes, remaining, chars, charCount, true);
                        charCount = encoding.GetChars(bytes, remaining, chars, charCount);

                        int i = -1, z = 0;
                        while (i < charCount)
                        {
                            ++i;

                            if (chars[i] == '\n')
                            {
                                charCount = i;
                                ++z;

                                if (i - 1 > -1 && chars[i - 1] == '\r')
                                {
                                    --charCount;
                                    ++z;
                                }

                                break;
                            }

                            if (chars[i] != '\0')
                                continue;

                            charCount = i;
                            ++z;

                            break;
                        }

                        //var enc = encoding.GetEncoder();
                        //_position += (nuint)enc.GetByteCount(chars, charCount + z, true);
                        _position += (nuint)encoding.GetByteCount(chars, charCount + z);

                        if (charCount == 0)
                            return string.Empty;
                        else
                        {
                            string s = new(chars, 0, charCount);
                            return string.IsInterned(s) ?? s;
                        }
                    }
                }
                finally
                {
                    charPool.Return(resultChars);
                }
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] buffer, int offset, int count)
            => Write(new ReadOnlySpan<byte>(buffer, offset, count));

#if NETSTANDARD2_0
        public void Write(ReadOnlySpan<byte> buffer)
#else
        public void Write(ReadOnlySpan<byte> buffer)
#endif
        {
            var count = checked((uint)buffer.Length);

            if (_position + count > GetLength())
                throw new InvalidOperationException();

            Unsafe.CopyBlockUnaligned(
                ref DataRef(),
                ref Unsafe.AsRef(buffer.GetPinnableReference()),
                count);

            _position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(byte value)
        {
            if (_position + 1 > GetLength()) throw new InvalidOperationException();
            *(Data() + _position++) = value;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
            => Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(short value)
        {
            if (_position + 2 > GetLength()) throw new InvalidOperationException();
            *(short*)(Data() + _position) = value;
            _position += 2;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort value)
            => Write((short)value);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(char value)
            => Write((short)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(int value)
        {
            if (_position + 4 > GetLength()) throw new InvalidOperationException();
            *(int*)(Data() + _position) = value;
            _position += 4;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value)
            => Write((int)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(long value)
        {
            if (_position + 8 > GetLength()) throw new InvalidOperationException();
            *(long*)(Data() + _position) = value;
            _position += 8;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ulong value)
            => Write((long)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WritePrimitive<T>(T value) where T : unmanaged
        {
            var size = (uint)sizeof(T);
            if (_position + size > GetLength()) throw new InvalidOperationException("Element size exceeds length.");
            *(T*)(Data() + _position) = value;
            _position += size;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string str)
            => Write(str, ZContext.Encoding);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string str, Encoding encoding)
            => WriteStringNative(str, encoding, false);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLine(string str)
            => WriteLine(str, ZContext.Encoding);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLine(string str, Encoding encoding)
            => WriteStringNative($"{str}\r\n", encoding, false);

        internal unsafe void WriteStringNative(string str, Encoding encoding, bool create)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));

            UnavailableCheck();

            if (str == string.Empty)
            {
                if (!create)
                    return;

                CreateNative(0);
                _length = 0;
                _position = 0;
                return;
            }

            var interned = string.IsInterned(str);
            if (interned != null) str = interned;

            if (create)
            {
                //Console.Error.WriteLine($"[{CallStackHelpers.GetCallStackDepth()}] WriteStringNative({str}, {encoding.EncodingName}, {create})");
                var bytes = EncodedStringCache.For(encoding, str);
                var size = (nuint)bytes.LongLength - 1;
                CreatePinned(bytes, 0, size);
                _length = size;
                _position = _length;
            }
            else
            {
                //Console.Error.WriteLine($"[{CallStackHelpers.GetCallStackDepth()}] Writing {str}");
                var byteCount = checked((uint)encoding.GetByteCount(str));
                if (_position + byteCount > GetLength())
                    // fail if frame is too small
                    throw new InvalidOperationException();

                var charCount = str.Length;
                fixed (char* strP = str)
                    byteCount = (uint)encoding.GetBytes(strP, charCount, Data() + _position, (int)byteCount);

                _position += byteCount;
            }
        }

        public void Flush()
            => throw new NotSupportedException();

        private ZFrameStateFlags StateFlags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
#if !NET5_0_OR_GREATER
                return (ZFrameStateFlags)Interlocked.Add(ref _stateFlags, 0);
#else
                return (ZFrameStateFlags)Interlocked.Or(ref _stateFlags, 0);
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStateFlags(ZFrameStateFlags flags, bool set)
        {
#if !NET5_0_OR_GREATER
            if (set)
            {
                var mask = (int)flags;
                for (;;)
                {
                    var seen = Volatile.Read(ref _stateFlags);
                    var orig = Interlocked.CompareExchange(ref _stateFlags, seen | mask, seen);
                    if (seen == orig) break;
                }
            }
            else
            {
                var mask = ~ (int)flags;
                for (;;)
                {
                    var seen = Volatile.Read(ref _stateFlags);
                    var orig = Interlocked.CompareExchange(ref _stateFlags, seen & mask, seen);
                    if (seen == orig) break;
                }
            }
#else
            if (set) Interlocked.Or(ref _stateFlags, (int)flags);
            else Interlocked.And(ref _stateFlags, ~(int)flags);
#endif
        }

        public bool IsClosed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (StateFlags & ZFrameStateFlags.IsClosed) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => UpdateStateFlags(ZFrameStateFlags.IsClosed, value);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (StateFlags & ZFrameStateFlags.IsDisposed) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => UpdateStateFlags(ZFrameStateFlags.IsDisposed, value);
        }

        public bool IsDismissed
        {
            get => (StateFlags & ZFrameStateFlags.IsDismissed) != 0;
            private set => UpdateStateFlags(ZFrameStateFlags.IsDismissed, value);
        }

        public bool IsUnavailable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (StateFlags & ZFrameStateFlags.IsUnavailable) != 0;
        }

#if TRACE
        private CallerInfo? CloseCaller;

        public void Close(
            [CallerMemberName] string? memberName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNum = default)
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Close()
#endif
        {
            lock (_lock)
            {
                if (IsClosed) return;
                IsClosed = true;
#if TRACE
                if (filePath is not null)
                    CloseCaller = new()
                    {
                        MetadataToken = -1,
                        FilePath = filePath,
                        LineNumber = lineNum
                    };
                else
                {
                    var sf = new StackFrame(1, true);
                    CloseCaller = new()
                    {
                        MetadataToken = sf.GetMethod()?.MetadataToken ?? 0,
                        FilePath = sf.GetFileName(),
                        LineNumber = sf.GetFileLineNumber()
                    };
                }
#endif

                Debug.Assert(_msg != default, "Closing unallocated message?");
                while (-1 == zmq.msg_close(_msg))
                {
                    var error = ZError.GetLastError();

                    if (error == default)
                        throw new ZException(error, "zmq_msg_close failed, missing error?");

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.EFAULT)
                        // Ignore: Invalid message.
                        break;

                    throw new ZException(error, "zmq_msg_close");
                }

                // Go unallocating the HGlobal
                Dismiss();
            }
        }


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnavailableCheck()
        {
            if (!IsUnavailable) return;
            lock (_lock)
            {
                StringBuilder sb = new();

                sb.AppendFormat($"Frame State: {StateFlags}.\n");
#if TRACE
                if (DisposeCaller is not null)
                    sb.Append("Disposed by ").AppendLine(DisposeCaller);
#endif
#if TRACE
                if (DismissCaller is not null)
                    sb.Append("Dismissed by ").AppendLine(DismissCaller);
#endif
#if TRACE
                if (CloseCaller is not null)
                    sb.Append("Closed by ").AppendLine(CloseCaller);
#endif
                throw new ObjectDisposedException("ZFrame", sb.ToString());
            }
        }

        public void CopyZeroTo(ZFrame other)
        {
            lock (_lock)
            lock (other._lock)
            {
                UnavailableCheck();

                // zmq.msg_copy(dest, src)

                Debug.Assert(_msg != default, "CopyZeroTo; _msg is missing");
                while (-1 == zmq.msg_copy(other._msg, _msg))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.EFAULT)
                    {
                        // Invalid message. 
                    }

                    throw new ZException(error, "zmq_msg_copy");
                }
            }
        }

        public void MoveZeroTo(ZFrame other)
        {
            lock (_lock)
            lock (other._lock)
            {
                UnavailableCheck();

                // zmq.msg_copy(dest, src)

                Debug.Assert(_msg != default, "MoveZeroTo; _msg is missing");
                while (-1 == zmq.msg_move(other._msg, _msg))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.EFAULT)
                    {
                        // Invalid message. 
                    }

                    throw new ZException(error, "zmq_msg_move");
                }
            }

            // When move, msg_close this _framePtr
            Close();
        }

        public int GetOption(ZFrameOption property)
        {
            int result;
            return -1 == (result = GetOption(property, out var error))
                ? throw new ZException(error)
                : result;
        }

        public int GetOption(ZFrameOption property, out ZError? error)
        {
#if TRACE
            UnavailableCheck();
#endif
            error = ZError.ENOENT;
            if (IsUnavailable) return -1;

            error = ZError.None;

            lock (_lock)
            {
                int result;
                Debug.Assert(_msg != default, "GetOption; _msg is missing");
                if (-1 != (result = zmq.msg_get(_msg, (int)property)))
                    return result;
            }

            error = ZError.GetLastError();
            return -1;
        }

        public string? GetOption(string property)
        {
            string? result;
            if (null != (result = GetOption(property, out var error)))
                return result;

            return error != ZError.None
                ? throw new ZException(error)
                : result;
        }

        public unsafe string? GetOption(string property, out ZError? error)
        {
#if TRACE
            UnavailableCheck();
#endif
            error = ZError.ENOENT;
            if (IsDismissed || IsClosed || IsDisposed) return null;

            error = ZError.None;

            var propertyBytes = EncodedStringCache.For(Encoding.UTF8, property);

            IntPtr resultPtr;
            Debug.Assert(_msg != default, "GetOption; _msg is missing");
            lock (_lock)
            {
                fixed (void* pPropertyStr = propertyBytes)
                    resultPtr = zmq.msg_gets(_msg, (IntPtr)pPropertyStr);
            }

            if (default == resultPtr)
            {
                error = ZError.GetLastError();
                return null;
            }
            var result = Marshal.PtrToStringAnsi(resultPtr);
            return result;
        }

        #region ICloneable implementation

        object ICloneable.Clone()
            => Clone();

        public ZFrame Clone()
        {
            UnavailableCheck();

            var frame = CreateEmpty();
            CopyZeroTo(frame);
            return frame;
        }

        #endregion

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => ToString(ZContext.Encoding) ?? "";

        public string? ToString(Encoding encoding)
        {
            lock (_lock)
            {
                if (Length < 0)
                    return null;

                var old = _position;
                Seek(0, SeekOrigin.Begin);
                var result = ReadString(encoding);
                Seek(old, SeekOrigin.Begin);
                if (result is not null)
                    return string.IsInterned(result) ?? result;
                return null;
            }
        }

        internal bool Receive(ZSocket sock, ZSocketFlags flags, out ZError? error)
        {
            lock (_lock)
            lock (sock._lock)
            {
                UnavailableCheck();

                Debug.Assert(_msg != default, "Receive; _msg is missing");

                int bytes;
                while (-1 == (bytes = zmq.msg_recv(_msg, sock.SocketPtr, flags)))
                {
                    error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM
                        || error == ZError.EFSM
                        || error == ZError.ENOTSUP
                        || error == ZError.ENOTSOCK
                        || error == ZError.EFAULT
                        || error == ZError.EFSM)
                        throw new ZException(error);

                    return false;
                }

#if NETSTANDARD2_0
                Thread.Sleep(1);
#else
                Interlocked.MemoryBarrierProcessWide();
#endif
                // might need to remove this check, it's just to catch weirdness in testing
                Debug.Assert(bytes >= 0, "Receive expected bytes >= 0");

#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
                error = default;
#else
            Unsafe.SkipInit(out error);
#endif
                return true;
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Receive(ZSocket sock, out ZError? error, bool more = true)
            => Receive(sock, more ? ZSocketFlags.More : default, out error);

        internal bool Send(ZSocket sock, ZSocketFlags flags, out ZError? error)
        {
            lock (_lock)
            lock (sock._lock)
            {
                UnavailableCheck();

                Debug.Assert(_msg != default, "Send; _msg is missing");

                int bytes;

                while (-1 == (bytes = zmq.msg_send(_msg, sock.SocketPtr, flags)))
                {
                    //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-SEND_MSG_DONE");
                    //Console.Error.Flush();
                    error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM
                        || error == ZError.EFSM
                        || error == ZError.ENOTSUP
                        || error == ZError.ENOTSOCK
                        || error == ZError.EFAULT
                        || error == ZError.EFSM)
                        throw new ZException(error);

                    return false;
                }

                // might need to remove this check, it's just to catch weirdness in testing
                Debug.Assert(bytes >= 0, "Send expected bytes >= 0");

#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
                error = default;
#else
            Unsafe.SkipInit(out error);
#endif
                return true;
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Send(ZSocket sock, out ZError? error, bool more = true)
            => Send(sock, more ? ZSocketFlags.More : default, out error);

        public bool TryGetRoutingId(out uint id)
        {
            UnavailableCheck();
            lock (_lock)
            {
                id = zmq.routing_id(_msg);

                return id != 0;
            }
        }

        public bool TrySetRoutingId(uint id)
        {
            UnavailableCheck();
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), "Routing IDs must be > 0.");
            lock (_lock)
            {
                while (-1 == zmq.msg_set_routing_id(_msg, id))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.EINTR)
                        continue;

                    throw new ZException(error);
                }
                return true;
            }
        }
    }
}
