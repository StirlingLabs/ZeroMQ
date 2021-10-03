using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// A single part message, sent or received via a <see cref="ZSocket"/>.
    /// </summary>
    public sealed class ZFrame : Stream, ICloneable
    {
        public static ZFrame FromStream(Stream stream, nint i, nuint l)
        {
            stream.Position = i;
            if (l == 0) return new();

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

        public static ZFrame CopyFrom(ZFrame frame)
            => frame.Clone();

        public static ZFrame Create(nuint size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            return new(CreateNative(size), size);
        }

        public static ZFrame CreateEmpty()
            => new(CreateEmptyNative(), 0);

        public const nuint DefaultFrameSize = 4096;

        public static readonly nuint MinimumFrameSize = zmq.sizeof_zmq_msg_t;

        private ZSafeHandle? _framePtr;

        private nuint _length;

        private nuint _position;

        // private ZeroMQ.lib.FreeMessageDelegate _freePtr;

        public ZFrame(byte[] buffer)
            : this(CreateNative((nuint)buffer.Length), (nuint)buffer.Length)
            => Write(buffer, 0, buffer.Length);

        public ZFrame(byte[] buffer, nuint offset, nuint count)
            : this(CreateNative(count), count)
            => Write(buffer, (int)offset, (int)count);

        public ZFrame(byte value)
            : this(CreateNative(1), 1)
            => Write(value);

        public ZFrame(short value)
            : this(CreateNative(2), 2)
            => Write(value);

        public ZFrame(ushort value)
            : this(CreateNative(2), 2)
            => Write(value);

        public ZFrame(char value)
            : this(CreateNative(2), 2)
            => Write(value);

        public ZFrame(int value)
            : this(CreateNative(4), 4)
            => Write(value);

        public ZFrame(uint value)
            : this(CreateNative(4), 4)
            => Write(value);

        public ZFrame(long value)
            : this(CreateNative(8), 8)
            => Write(value);

        public ZFrame(ulong value)
            : this(CreateNative(8), 8)
            => Write(value);

        public ZFrame(string str)
            : this(str, ZContext.Encoding) { }

        public ZFrame(string str, Encoding encoding)
            => WriteStringNative(str, encoding, true);

        public ZFrame()
            : this(CreateNative(0), 0) { }

        /* protected ZFrame(IntPtr data, int size)
          : this(Alloc(data, size), size)
        { } */

        internal ZFrame(ZSafeHandle frameIntPtr, nuint size)
        {
            _framePtr = frameIntPtr;
            _length = size;
            _position = 0;
        }

        ~ZFrame()
            => Dispose(false);

        internal static ZSafeHandle CreateEmptyNative()
        {
            var msg = ZSafeHandle.Alloc(zmq.sizeof_zmq_msg_t);

            while (-1 == zmq.msg_init(msg))
            {
                var error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                    continue;

                msg.Dispose();

                throw new ZException(error, "zmq_msg_init");
            }

            return msg;
        }

        internal static ZSafeHandle CreateNative(nuint size)
        {
            var msg = ZSafeHandle.Alloc(zmq.sizeof_zmq_msg_t);

            while (-1 == zmq.msg_init_size(msg, size))
            {
                var error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                    continue;

                msg.Dispose();

                if (error == ZError.ENOMEM)
                    throw new OutOfMemoryException("zmq_msg_init_size");

                throw new ZException(error, "zmq_msg_init_size");
            }
            return msg;
        }

        protected override void Dispose(bool disposing)
        {
            if (_framePtr != null)
                if (_framePtr.Ptr != default)
                    Close();
            GC.SuppressFinalize(this);
            base.Dispose(disposing);
        }

        public void Dismiss()
        {
            if (_framePtr != null)
            {
                _framePtr.Dispose();
                _framePtr = null;
            }
            GC.SuppressFinalize(this);
        }

        public bool IsDismissed => _framePtr == null;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanTimeout => false;

        public override bool CanWrite => true;

        public override long Length
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)GetLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetLength()
            => _length = _framePtr is not null && _framePtr != default(IntPtr) ? zmq.msg_size(_framePtr) : 0;

        public override void SetLength(long _)
            => throw new NotSupportedException();

        public override long Position
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)GetPosition();

            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Seek(value, SeekOrigin.Begin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetPosition()
            => _position;

        public IntPtr Ptr
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _framePtr ?? default(IntPtr);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr DataPtr()
        {
            var p = Ptr;
            return p == default
                ? default
                : zmq.msg_data(p);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Data()
            => (byte*)DataPtr();

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref byte DataRef()
            => ref Unsafe.AsRef<byte>((void*)DataPtr());

        public override long Seek(long offset, SeekOrigin origin)
            => (long)Seek((nuint)offset, origin);

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
        public override int Read(byte[] buffer, int offset, int count)
            => Read(new Span<byte>(buffer, offset, count));

#if NETSTANDARD2_0
        public int Read(Span<byte> buffer)
#else
        public override int Read(Span<byte> buffer)
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

        public override unsafe int ReadByte()
            => _position + 1 > GetLength() ? -1 : *(Data() + _position++);

        public unsafe byte ReadAsByte()
            => _position + 1 > GetLength() ? default : *(Data() + _position++);

        public unsafe short ReadInt16()
        {
            if (_position + 2 > GetLength()) return default;
            var v = *(short*)(Data() + _position);
            _position += 2;
            return v;
        }

        public ushort ReadUInt16()
            => (ushort)ReadInt16();

        public char ReadChar()
            => (char)ReadInt16();

        public unsafe int ReadInt32()
        {
            if (_position + 4 > GetLength()) return default;
            var v = *(int*)(Data() + _position);
            _position += 4;
            return v;
        }

        public uint ReadUInt32()
            => (char)ReadInt32();

        public unsafe long ReadInt64()
        {
            if (_position + 8 > GetLength()) return default;
            var v = *(long*)(Data() + _position);
            _position += 8;
            return v;
        }

        public ulong ReadUInt64()
            => (ulong)ReadInt64();

        public unsafe T Read<T>() where T : unmanaged
        {
            var size = (nuint)sizeof(T);
            if (_position + size > GetLength()) return default;
            var v = *(T*)(Data() + _position);
            _position += size;
            return v;
        }

        public unsafe bool TryRead<T>(out T item) where T : unmanaged
        {
            var size = (nuint)sizeof(T);
            if (_position + size > GetLength())
            {
#if !NET5_0_OR_GREATER
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

        public unsafe bool TryRead<T>(ref T[] buffer) where T : unmanaged
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            var count = (uint)buffer.Length;
            var itemSize = (uint)sizeof(T);
            var size = itemSize * count;
            if (_position + size > GetLength())
            {
#if !NET5_0_OR_GREATER
                buffer = default;
#else
                Unsafe.SkipInit(out buffer);
#endif
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

        public string? ReadString()
            => ReadString(ZContext.Encoding);

        public string? ReadString(Encoding encoding)
            => ReadString( /* byteCount */ GetLength() - _position, encoding);

        public string? ReadString(nuint length)
            => ReadString( /* byteCount */ length, ZContext.Encoding);

        public string? ReadString(nuint byteCount, Encoding encoding)
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

                if (charCount == 0)
                    return string.Empty;

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

                    return charCount == 0
                        ? string.Empty
                        : new(chars, 0, charCount);
                }
            }
        }

        public string? ReadLine()
            => ReadLine(GetLength() - _position, ZContext.Encoding);

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
        public override void Write(byte[] buffer, int offset, int count)
            => Write(new ReadOnlySpan<byte>(buffer, offset, count));

#if NETSTANDARD2_0
        public void Write(ReadOnlySpan<byte> buffer)
#else
        public override void Write(ReadOnlySpan<byte> buffer)
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

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(byte value)
        {
            if (_position + 1 > GetLength()) throw new InvalidOperationException();
            *(Data() + _position++) = value;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte value)
            => Write(value);

        [DebuggerStepThrough]
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

        [DebuggerStepThrough]
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

        [DebuggerStepThrough]
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

                _framePtr = CreateNative(0);
                _length = 0;
                _position = 0;
                return;
            }

            var charCount = str.Length;
            var enc = encoding.GetEncoder();

            fixed (char* strP = str)
            {
                var byteCount = checked((uint)enc.GetByteCount(strP, charCount, false));

                if (create)
                {
                    _framePtr = CreateNative(byteCount);
                    _length = byteCount;
                    _position = 0;
                }
                else if (_position + byteCount > GetLength())
                    // fail if frame is too small
                    throw new InvalidOperationException();

                byteCount = (uint)enc.GetBytes(strP, charCount, Data() + _position, (int)byteCount, true);
                _position += byteCount;
            }
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override void Close()
        {
            if (_framePtr == null)
                return;

            if (_framePtr.Ptr == default)
            {
                Dismiss();
                return;
            }

            while (-1 == zmq.msg_close(_framePtr))
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

        public void CopyZeroTo(ZFrame other)
        {
            // zmq.msg_copy(dest, src)
            ZError? error;
            while (-1 == zmq.msg_copy(other._framePtr, _framePtr))
            {
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                {
                    error = default;
                    continue;
                }
                if (error == ZError.EFAULT)
                {
                    // Invalid message. 
                }
                throw new ZException(error, "zmq_msg_copy");
            }
        }

        public void MoveZeroTo(ZFrame other)
        {
            // zmq.msg_copy(dest, src)
            ZError? error;
            while (-1 == zmq.msg_move(other._framePtr, _framePtr))
            {
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                {
                    error = default;
                    continue;
                }
                if (error == ZError.EFAULT)
                {
                    // Invalid message. 
                }
                throw new ZException(error, "zmq_msg_move");
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
            error = ZError.None;

            int result;
            if (-1 != (result = zmq.msg_get(_framePtr, (int)property)))
                return result;

            error = ZError.GetLastErr();
            return -1;
        }

        public string? GetOption(string property)
        {
            string result;
            if (null != (result = GetOption(property, out var error)))
                return result;

            return error != ZError.None
                ? throw new ZException(error)
                : result;
        }

        public string? GetOption(string property, out ZError? error)
        {
            error = ZError.None;

            string? result = null;
            using var propertyPtr = ZSafeHandle.AllocString(property);
            IntPtr resultPtr;
            if (default == (resultPtr = zmq.msg_gets(_framePtr, propertyPtr)))
            {
                error = ZError.GetLastErr();
                return null;
            }
            result = Marshal.PtrToStringAnsi(resultPtr);
            return result;
        }

        #region ICloneable implementation

        object ICloneable.Clone()
            => Clone();

        public ZFrame Clone()
        {
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
                return GetType().FullName;

            var old = _position;
            Seek(0, SeekOrigin.Begin);
            var result = ReadString(encoding);
            Seek(old, SeekOrigin.Begin);
            return result;
        }
    }
}
