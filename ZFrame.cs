using System;
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
        public static ZFrame FromStream(Stream stream, long i, int l)
        {
            stream.Position = i;
            if (l == 0) return new();

            var frame = Create(l);
            var buf = new byte[65535];
            int bufLen, remaining, current = -1;
            do
            {
                ++current;
                remaining = Math.Min(Math.Max(0, l - current * buf.Length), buf.Length);
                if (remaining < 1) break;

                bufLen = stream.Read(buf, 0, remaining);
                frame.Write(buf, 0, bufLen);

            } while (bufLen > 0);

            frame.Position = 0;
            return frame;
        }

        public static ZFrame CopyFrom(ZFrame frame)
            => frame.Clone();

        public static ZFrame Create(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            return new(CreateNative(size), size);
        }

        public static ZFrame CreateEmpty()
            => new(CreateEmptyNative(), -1);

        public const int DefaultFrameSize = 4096;

        public static readonly int MinimumFrameSize = zmq.sizeof_zmq_msg_t;

        private DispoIntPtr framePtr;

        private int length;

        private int position;

        // private ZeroMQ.lib.FreeMessageDelegate _freePtr;

        public ZFrame(byte[] buffer)
            : this(CreateNative(buffer.Length), buffer.Length)
            => Write(buffer, 0, buffer.Length);

        public ZFrame(byte[] buffer, int offset, int count)
            : this(CreateNative(count), count)
            => Write(buffer, offset, count);

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

        internal ZFrame(DispoIntPtr frameIntPtr, int size)
        {
            framePtr = frameIntPtr;
            length = size;
            position = 0;
        }

        ~ZFrame()
            => Dispose(false);

        internal static DispoIntPtr CreateEmptyNative()
        {
            var msg = DispoIntPtr.Alloc(zmq.sizeof_zmq_msg_t);

            ZError error;
            while (-1 == zmq.msg_init(msg))
            {
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                {
                    error = default;
                    continue;
                }

                msg.Dispose();

                throw new ZException(error, "zmq_msg_init");
            }

            return msg;
        }

        internal static DispoIntPtr CreateNative(int size)
        {
            var msg = DispoIntPtr.Alloc(zmq.sizeof_zmq_msg_t);

            ZError error;
            while (-1 == zmq.msg_init_size(msg, size))
            {
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                {
                    error = default;
                    continue;
                }

                msg.Dispose();

                if (error == ZError.ENOMEM)
                {
                    throw new OutOfMemoryException("zmq_msg_init_size");
                }
                throw new ZException(error, "zmq_msg_init_size");
            }
            return msg;
        }

        /* internal static DispoIntPtr Alloc(IntPtr data, int size) 
        {
          var msg = DispoIntPtr.Alloc(zmq.sizeof_zmq_msg_t);
    
          ZError error;
          while (-1 == zmq.msg_init_data(msg, data, size, /* msg_free_delegate null, /* hint default(IntPtr))) {
            error = ZError.GetLastError();
    
            if (error == ZError.EINTR) {
              continue;
            }
    
            msg.Dispose();
    
            if (error == ZError.ENOMEM) {
              throw new OutOfMemoryException ("zmq_msg_init_size");
            }
            throw new ZException (error, "zmq_msg_init_size");
          }
          return msg;
        } */

        protected override void Dispose(bool disposing)
        {
            if (framePtr != null)
            {
                if (framePtr.Ptr != default)
                {
                    Close();
                }
            }
            GC.SuppressFinalize(this);
            base.Dispose(disposing);
        }

        public void Dismiss()
        {
            if (framePtr != null)
            {
                framePtr.Dispose();
                framePtr = null;
            }
            GC.SuppressFinalize(this);
        }

        public bool IsDismissed => framePtr == null;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanTimeout => false;

        public override bool CanWrite => true;

        private void EnsureCapacity()
        {
            if (framePtr != default(IntPtr))
                length = zmq.msg_size(framePtr);
            else
                length = -1;
        }

        public override long Length
        {
            get {
                EnsureCapacity();
                return length;
            }
        }

        public override void SetLength(long length)
            => throw new NotSupportedException();

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public IntPtr Ptr => framePtr;

        public IntPtr DataPtr()
        {
            if (framePtr == default(IntPtr))
                return default;

            return zmq.msg_data(framePtr);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var pos = origin switch
            {
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => Length + offset,
                _ => offset
            };

            if (pos < 0 || pos > Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            position = (int)pos;
            return pos;
        }

        public byte[] Read()
        {
            var remaining = Math.Max(0, (int)(Length - position));
            return Read(remaining);
        }

        public byte[] Read(int count)
        {
            var remaining = Math.Min(count, Math.Max(0, (int)(Length - position)));

            if (remaining == 0)
                return Array.Empty<byte>();

            if (remaining < 0)
                return null;

            var bytes = new byte[remaining];
            /* int bytesLength = */
            Read(bytes, 0, remaining);
            return bytes;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = Math.Min(count, Math.Max(0, (int)(Length - position)));

            if (remaining == 0)
                return 0;

            if (remaining < 0)
                return -1;
            Marshal.Copy(DataPtr() + position, buffer, offset, remaining);

            position += remaining;
            return remaining;
        }

        public override int ReadByte()
        {
            if (position + 1 > Length)
                return -1;

            int byt = Marshal.ReadByte(DataPtr() + position);
            ++position;
            return byt;
        }

        public byte ReadAsByte()
        {
            if (position + 1 > Length)
                return default;

            var byt = Marshal.ReadByte(DataPtr() + position);
            ++position;
            return byt;
        }

        public short ReadInt16()
        {
            var bytes = new byte[2];
            var len = Read(bytes, 0, 2);

            if (len < 2)
                return default;

            return BitConverter.ToInt16(bytes, 0);
        }

        public ushort ReadUInt16()
        {
            var bytes = new byte[2];
            var len = Read(bytes, 0, 2);

            if (len < 2)
                return default;

            return BitConverter.ToUInt16(bytes, 0);
        }

        public char ReadChar()
        {
            var bytes = new byte[2];
            var len = Read(bytes, 0, 2);

            if (len < 2)
                return default;

            return BitConverter.ToChar(bytes, 0);
        }

        public int ReadInt32()
        {
            var bytes = new byte[4];
            var len = Read(bytes, 0, 4);

            if (len < 4)
                return default;

            return BitConverter.ToInt32(bytes, 0);
        }

        public uint ReadUInt32()
        {
            var bytes = new byte[4];
            var len = Read(bytes, 0, 4);

            if (len < 4)
                return default;

            return BitConverter.ToUInt32(bytes, 0);
        }

        public long ReadInt64()
        {
            var bytes = new byte[8];
            var len = Read(bytes, 0, 8);

            if (len < 8)
                return default;

            return BitConverter.ToInt64(bytes, 0);
        }

        public ulong ReadUInt64()
        {
            var bytes = new byte[8];
            var len = Read(bytes, 0, 8);

            if (len < 8)
                return default;

            return BitConverter.ToUInt64(bytes, 0);
        }

        public string ReadString()
            => ReadString(ZContext.Encoding);

        public string ReadString(Encoding encoding)
            => ReadString( /* byteCount */ (int)Length - position, encoding);

        public string ReadString(int length)
            => ReadString( /* byteCount */ length, ZContext.Encoding);

        public string ReadString(int byteCount, Encoding encoding)
        {
            var remaining = Math.Min(byteCount, Math.Max(0, (int)Length - position));

            if (remaining == 0)
                return string.Empty;

            if (remaining < 0)
                return null;

            unsafe
            {
                var bytes = (byte*)(DataPtr() + position);

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
                    position += enc.GetByteCount(chars, charCount + z, true);

                    return charCount == 0
                        ? string.Empty
                        : new(chars, 0, charCount);
                }
            }
        }

        public string ReadLine()
            => ReadLine((int)Length - position, ZContext.Encoding);

        public string ReadLine(Encoding encoding)
            => ReadLine((int)Length - position, encoding);

        public string ReadLine(int byteCount, Encoding encoding)
        {
            var remaining = Math.Min(byteCount, Math.Max(0, (int)Length - position));

            if (remaining == 0)
                return string.Empty;

            if (remaining < 0)
                return null;

            unsafe
            {
                var bytes = (byte*)(DataPtr() + position);

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
                    position += enc.GetByteCount(chars, charCount + z, true);

                    return charCount == 0
                        ? string.Empty
                        : new(chars, 0, charCount);
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (position + count > Length) throw new InvalidOperationException();
            Marshal.Copy(buffer, offset, DataPtr() + position, count);
            position += count;
        }

        public void Write(byte value)
        {
            if (position + 1 > Length) throw new InvalidOperationException();
            Marshal.WriteByte(DataPtr() + position, value);
            ++position;
        }

        public override void WriteByte(byte value)
            => Write(value);

        public void Write(short value)
        {
            if (position + 2 > Length) throw new InvalidOperationException();
            Marshal.WriteInt16(DataPtr() + position, value);
            position += 2;
        }

        public void Write(ushort value)
            => Write((short)value);

        public void Write(char value)
            => Write((short)value);

        public void Write(int value)
        {
            if (position + 4 > Length) throw new InvalidOperationException();
            Marshal.WriteInt32(DataPtr() + position, value);
            position += 4;
        }

        public void Write(uint value)
            => Write((int)value);

        public void Write(long value)
        {
            if (position + 8 > Length) throw new InvalidOperationException();
            Marshal.WriteInt64(DataPtr() + position, value);
            position += 8;
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

                framePtr = CreateNative(0);
                length = 0;
                position = 0;
                return;
            }

            var charCount = str.Length;
            var enc = encoding.GetEncoder();

            fixed (char* strP = str)
            {
                var byteCount = enc.GetByteCount(strP, charCount, false);

                if (create)
                {
                    framePtr = CreateNative(byteCount);
                    length = byteCount;
                    position = 0;
                }
                else if (position + byteCount > Length)
                    // fail if frame is too small
                    throw new InvalidOperationException();

                byteCount = enc.GetBytes(strP, charCount, (byte*)(DataPtr() + position), byteCount, true);
                position += byteCount;
            }
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override void Close()
        {
            if (framePtr == null)
                return;

            if (framePtr.Ptr == default)
            {
                Dismiss();
                return;
            }

            ZError error;
            while (-1 == zmq.msg_close(framePtr))
            {
                error = ZError.GetLastErr();

                if (error == ZError.EINTR)
                {
                    error = default;
                    continue;
                }
                if (error == ZError.EFAULT)
                {
                    // Ignore: Invalid message.
                    break;
                }
                return;
            }

            // Go unallocating the HGlobal
            Dismiss();
        }

        public void CopyZeroTo(ZFrame other)
        {
            // zmq.msg_copy(dest, src)
            ZError error;
            while (-1 == zmq.msg_copy(other.framePtr, framePtr))
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
            ZError error;
            while (-1 == zmq.msg_move(other.framePtr, framePtr))
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

        public int GetOption(ZFrameOption property, out ZError error)
        {
            error = ZError.None;

            int result;
            if (-1 != (result = zmq.msg_get(framePtr, (int)property)))
                return result;

            error = ZError.GetLastErr();
            return -1;
        }

        public string GetOption(string property)
        {
            string result;
            if (null != (result = GetOption(property, out var error)))
                return result;

            return error != ZError.None
                ? throw new ZException(error)
                : result;
        }

        public string GetOption(string property, out ZError error)
        {
            error = ZError.None;

            string result = null;
            using (var propertyPtr = DispoIntPtr.AllocString(property))
            {
                IntPtr resultPtr;
                if (default == (resultPtr = zmq.msg_gets(framePtr, propertyPtr)))
                {
                    error = ZError.GetLastErr();
                    return null;
                }
                result = Marshal.PtrToStringAnsi(resultPtr);
            }
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

            long old = position;
            Position = 0;
            var retur = ReadString(encoding);
            Position = old;
            return retur;
        }
    }
}
