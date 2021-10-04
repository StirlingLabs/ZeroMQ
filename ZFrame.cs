using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// A single part message, sent or received via a <see cref="ZSocket"/>.
    /// </summary>
    public sealed partial class ZFrame : ICloneable
    {
        public static readonly nuint MinimumFrameSize = zmq.sizeof_zmq_msg_t;

        public const nuint DefaultFrameSize = 4096;

        private readonly object _lock = new();
        private int _stateFlags;
        private nuint _length;
        private nuint _position;
        private ZSafeHandle? _msg_handle;

        private IntPtr _msg
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _msg_handle;
        }

        private static readonly IProducerConsumerCollection<ZSafeHandle> FreeMsgs
            = new ConcurrentQueue<ZSafeHandle>();

        public static void FlushObjectCaches()
        {
            while (FreeMsgs.TryTake(out _)) {}
        }

        private ZFrame()
            => _msg_handle
                = FreeMsgs.TryTake(out var m)
                    ? m.ClearMemory()
                    : ZSafeHandle.Alloc(zmq.sizeof_zmq_msg_t);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create()
        {
            ZFrame r = new();
            r.CreateNative(0);
            Debug.Assert(r._msg != default, "Missing message after CreateNative 0");
            r._length = 0;
            r._position = 0;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(nuint size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            ZFrame r = new();
            r.CreateNative(size);
            Debug.Assert(r._msg != default, "Missing message after CreateNative N");
            r._length = size;
            r._position = 0;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(byte[] data)
        {
            var size = (nuint)data.Length;
            if (size == 0) return CreateEmpty();
            ZFrame r = new();
            r.CreatePinned(data);
            Debug.Assert(r._msg != default, "Missing message after CreatePinned");
            r._length = size;
            r._position = 0;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(byte[] data, nuint offset, nuint count)
        {
            if (count == 0) return CreateEmpty();
            ZFrame r = new();
            r.CreateNative(count);
            Debug.Assert(r._msg != default, "Missing message after CreateNative M");
            r._length = count;
            r._position = 0;
            r.Write(data, (int)offset, (int)count);
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZFrame Create(string str, Encoding encoding)
        {
            ZFrame r = new();
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
            ZFrame r = new();
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
            ZFrame r = new();
            r.CreateEmptyNative();
            Debug.Assert(r._msg != default, "Missing message after CreateEmptyNative");
            r._length = 0;
            r._position = 0;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreateEmptyNative()
        {
            Debug.Assert(!IsUnavailable, "CreateEmptyNative; Should not be unavailable");

            lock (_lock)
            {
                while (-1 == zmq.msg_init(_msg))
                {
                    var error = ZError.GetLastErr();

                    if (error == ZError.EINTR)
                        continue;

                    throw new ZException(error, "zmq_msg_init");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreateNative(nuint size)
        {
            Debug.Assert(!IsUnavailable, "CreateNative; Should not be unavailable");

            lock (_lock)
            {
                Debug.Assert(_msg != default);
                while (-1 == zmq.msg_init_size(_msg, size))
                {
                    var error = ZError.GetLastErr();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM)
                        throw new OutOfMemoryException("zmq_msg_init_size");

                    throw new ZException(error, "zmq_msg_init_size");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreatePinned(byte[] data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var size = (nuint)data.Length;
            if (size == 0) throw new ArgumentException("Array must not be empty.", nameof(data));
            CreatePinned(GCHandle.Alloc(data, GCHandleType.Pinned), size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CreatePinned(GCHandle pin, nuint size)
        {
            Debug.Assert(!IsUnavailable, "CreatePinned; Should not be unavailable");

            if (pin.Target is not byte[] bytes)
                throw new InvalidOperationException(
                    $"GCHandle must point to a simple byte array, but was {pin.Target?.GetType().AssemblyQualifiedName ?? "null"} (allocated: {pin.IsAllocated} @ 0x{pin.AddrOfPinnedObject():X8})");

            //Console.Error.WriteLine($"[{CallStackHelpers.GetCallStackDepth()}] ZFrame.CreatePinned(GCH 0x{(IntPtr)pin:X8}, {size})");

#if NET5_0_OR_GREATER
            var pBytes = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(bytes));
#else
            var pBytes = (IntPtr)Unsafe.AsPointer(ref bytes[0]);
#endif
            lock (_lock)
            {
                Debug.Assert(_msg != default);

#if NET5_0_OR_GREATER
                while (-1 == zmq.msg_init_data(_msg, pBytes, size, &PinnedGcHandleFreeCallback, GCHandle.ToIntPtr(pin)))
#else
                while (-1 == zmq.msg_init_data(_msg, pBytes, size, PinnedGcHandleFreeCallback, GCHandle.ToIntPtr(pin)))
#endif
                {
                    var error = ZError.GetLastErr();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.ENOMEM)
                        throw new OutOfMemoryException("zmq_msg_init_data");

                    throw new ZException(error, "zmq_msg_init_data");
                }
            }

        }

#if NET5_0_OR_GREATER
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PinnedGcHandleFreeCallback(IntPtr dataAddr, IntPtr pinPtr)
        {
            //Console.Error.WriteLine($"[{CallStackHelpers.GetCallStackDepth()}] ZFrame.PinnedGcHandleFreeCallbackDelegate({dataAddr:X8}, GCH 0x{pinPtr:X8})");
            var handle = GCHandle.FromIntPtr(pinPtr);
            handle.Free();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            lock (_lock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                Close();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Dismiss()
        {
            lock (_lock)
            {
                Close();

                if (IsDismissed) return;
                IsDismissed = true;

                if (_msg_handle == null) return;
                FreeMsgs.TryAdd(_msg_handle);
            }
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
            if (IsDismissed || IsClosed || IsDisposed)
                return _length = 0;

            lock (_lock)
                return _length = zmq.msg_size(_msg);
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
            if (IsDismissed || IsClosed || IsDisposed)
                return default;

            lock (_lock)
                return zmq.msg_data(_msg);
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
            var pos = origin switch
            {
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => GetLength() + offset,
                _ => offset
            };

            if (pos < 0 || pos > GetLength())
                throw new ArgumentOutOfRangeException(nameof(offset));

            return _position = pos;
        }

        public byte[]? Read()
        {
            var remaining = checked((int)Math.Max(0, GetLength() - _position));

            if (remaining == 0)
                return Array.Empty<byte>();

            var bytes = new byte[remaining];

            Read(bytes, 0, remaining);

            return Read(remaining);
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
        public int Read(Span<byte> buffer)
#else
        public int Read(Span<byte> buffer)
#endif
        {
            var count = (nuint)buffer.Length;

            var length = GetLength();

            if (_position + count > length)
            {
                count = length - _position;
                if (count <= 0) return 0;
            }

            Unsafe.CopyBlockUnaligned(
                ref buffer.GetPinnableReference(),
                ref Unsafe.Add(ref DataRef(), (nint)_position),
                (uint)count);

            _position += count;
            return (int)count;
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

            unsafe
            {
                var bytes = Data() + _position;

                var dec = encoding.GetDecoder();
                var charCount = dec.GetCharCount(bytes, remaining, false);

                if (charCount == 0)
                {
                    value = string.Empty;
                    return true;
                }

                var resultChars = new char[charCount];
                fixed (char* chars = resultChars)
                {
                    charCount = dec.GetChars(bytes, remaining, chars, charCount, true);

                    int i = -1, z = 0;
                    while (i < charCount)
                    {
                        ++i;

                        if (chars[i] != '\0')
                            continue;

                        charCount = i;
                        ++z;

                        break;
                    }

                    var enc = encoding.GetEncoder();
                    _position += (nuint)enc.GetByteCount(chars, charCount + z, true);

                    if (charCount == 0)
                        value = string.Empty;
                    else
                    {
                        string s = new(chars, 0, charCount);
                        value = string.IsInterned(s) ?? s;
                    }

                    return true;
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

                var dec = encoding.GetDecoder();
                var charCount = dec.GetCharCount(bytes, remaining, false);
                if (charCount == 0) return string.Empty;

                var resultChars = new char[charCount];
                fixed (char* chars = resultChars)
                {
                    charCount = dec.GetChars(bytes, remaining, chars, charCount, true);

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

                    var enc = encoding.GetEncoder();
                    _position += (nuint)enc.GetByteCount(chars, charCount + z, true);

                    return charCount == 0
                        ? string.Empty
                        : new(chars, 0, charCount);
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
                var cache = EncodedStringCaches.GetOrAdd(encoding, EncodedStringCache.Factory);

                var bytes = cache.Get(str);

                var size = (nuint)bytes.LongLength;
                CreatePinned(bytes);
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
                var enc = encoding.GetEncoder();
                fixed (char* strP = str)
                    byteCount = (uint)enc.GetBytes(strP, charCount, Data() + _position, (int)byteCount, true);

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

        public void Close()
        {
            lock (_lock)
            {
                if (IsClosed) return;
                IsClosed = true;

                Debug.Assert(_msg != default, "Closing unallocated message?");
                while (-1 == zmq.msg_close(_msg))
                {
                    var error = ZError.GetLastErr();

                    if (error == ZError.EINTR)
                        continue;

                    if (error == ZError.EFAULT)
                        // Ignore: Invalid message.
                        break;

                    if (error == default)
                        return;

                    throw new ZException(error, "zmq_msg_close");
                }

                // Go unallocating the HGlobal
                Dismiss();
            }
        }

        public void CopyZeroTo(ZFrame other)
        {
            if (IsUnavailable)
            {
                if (IsDisposed) throw new ObjectDisposedException("Frame was disposed.");
                if (IsClosed) throw new ObjectDisposedException("Frame was closed.");
                if (IsDismissed) throw new ObjectDisposedException("Frame was dismissed.");
            }

            // zmq.msg_copy(dest, src)

            lock (_lock)
            lock (other._lock)
            {
                Debug.Assert(_msg != default);
                while (-1 == zmq.msg_copy(other._msg, _msg))
                {
                    var error = ZError.GetLastErr();

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
            if (IsUnavailable)
            {
                if (IsDisposed) throw new ObjectDisposedException("Frame was disposed.");
                if (IsClosed) throw new ObjectDisposedException("Frame was closed.");
                if (IsDismissed) throw new ObjectDisposedException("Frame was dismissed.");
            }

            // zmq.msg_copy(dest, src)

            lock (_lock)
            lock (other._lock)
            {
                Debug.Assert(_msg != default);
                while (-1 == zmq.msg_move(other._msg, _msg))
                {
                    var error = ZError.GetLastErr();

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
            error = ZError.ENOENT;
            if (IsDismissed || IsClosed || IsDisposed) return -1;

            error = ZError.None;

            lock (_lock)
            {
                int result;
                Debug.Assert(_msg != default);
                if (-1 != (result = zmq.msg_get(_msg, (int)property)))
                    return result;
            }

            error = ZError.GetLastErr();
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

        public string? GetOption(string property, out ZError? error)
        {
            error = ZError.ENOENT;
            if (IsDismissed || IsClosed || IsDisposed) return null;

            error = ZError.None;

            using var propertyPtr = ZSafeHandle.AllocString(property);

            IntPtr resultPtr;
            Debug.Assert(_msg != default);
            lock (_lock) resultPtr = zmq.msg_gets(_msg, propertyPtr);

            if (default == resultPtr)
            {
                error = ZError.GetLastErr();
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
            if (IsUnavailable)
            {
                if (IsDisposed) throw new ObjectDisposedException("Frame was disposed.");
                if (IsClosed) throw new ObjectDisposedException("Frame was closed.");
                if (IsDismissed) throw new ObjectDisposedException("Frame was dismissed.");
            }

            var frame = CreateEmpty();
            CopyZeroTo(frame);
            return frame;
        }

        #endregion

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => ToString(ZContext.Encoding);

        public string ToString(Encoding encoding)
        {
            if (Length <= -1)
                return GetType()?.FullName ?? nameof(ZFrame);

            var old = _position;
            Seek(0, SeekOrigin.Begin);
            var result = ReadString(encoding);
            Seek(old, SeekOrigin.Begin);
            return result ?? nameof(ZFrame);
        }

        internal bool Receive(ZSocket sock, ZSocketFlags flags, out ZError? error)
        {
            if (IsUnavailable)
            {
                if (IsDisposed) throw new ObjectDisposedException("Frame was disposed.");
                if (IsClosed) throw new ObjectDisposedException("Frame was closed.");
                if (IsDismissed) throw new ObjectDisposedException("Frame was dismissed.");
            }

            Debug.Assert(_msg != default);
            //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-RECV_MSG");
            //Console.Error.Flush();
            while (-1 == zmq.msg_recv(_msg, sock.SocketPtr, flags))
            {
                //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-RECV_MSG_DONE");
                //Console.Error.Flush();
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                    continue;

                if (error == ZError.ENOMEM
                    || error == ZError.EFSM
                    || error == ZError.ENOTSUP
                    || error == ZError.ENOTSOCK
                    || error == ZError.EFAULT
                    || error == ZError.EFSM)
                    throw new ZException(error);

                if (error != ZError.EAGAIN)
                    return false;

                // might be out of memory / system resources
                if ((flags & ZSocketFlags.DontWait) == 0)
                    throw new ZException(error, "Resource temporarily unavailable.");

                Console.Error.WriteLine("EAGAIN");
                Console.Error.Flush();
                Thread.Yield();
            }

#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
            error = default;
#else
            Unsafe.SkipInit(out error);
#endif
            return true;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Receive(ZSocket sock, out ZError? error, bool more = true)
            => Receive(sock, more ? ZSocketFlags.More : default, out error);

        internal bool Send(ZSocket sock, ZSocketFlags flags, out ZError? error)
        {
            if (IsUnavailable)
            {
                if (IsDisposed) throw new ObjectDisposedException("Frame was disposed.");
                if (IsClosed) throw new ObjectDisposedException("Frame was closed.");
                if (IsDismissed) throw new ObjectDisposedException("Frame was dismissed.");
            }

            Debug.Assert(_msg != default);
            //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-SEND_MSG");
            //Console.Error.Flush();
            while (-1 == zmq.msg_send(_msg, sock.SocketPtr, flags))
            {
                //Console.Error.WriteLine($"{Thread.CurrentThread.ManagedThreadId}-SEND_MSG_DONE");
                //Console.Error.Flush();
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                    continue;

                if (error == ZError.ENOMEM
                    || error == ZError.EFSM
                    || error == ZError.ENOTSUP
                    || error == ZError.ENOTSOCK
                    || error == ZError.EFAULT
                    || error == ZError.EFSM)
                    throw new ZException(error);

                if (error != ZError.EAGAIN)
                    return false;

                // might be out of memory / system resources
                if ((flags & ZSocketFlags.DontWait) == 0)
                    throw new ZException(error, "Resource temporarily unavailable.");

                Console.Error.WriteLine("EAGAIN");
                Console.Error.Flush();
                Thread.Yield();
            }

#if !NET5_0_OR_GREATER || !NETSTANDARD2_1_OR_GREATER
            error = default;
#else
            Unsafe.SkipInit(out error);
#endif
            return true;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Send(ZSocket sock, out ZError? error, bool more = true)
            => Send(sock, more ? ZSocketFlags.More : default, out error);
    }
}
