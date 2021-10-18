using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZeroMQ
{
    public sealed class EncodedStringCache
    {
        private static readonly ConcurrentDictionary<Encoding, EncodedStringCache> Shared = new();
        public static readonly Func<Encoding, EncodedStringCache> Factory = Create;

        private static EncodedStringCache Create(Encoding enc) => new(enc);

        public static EncodedStringCache For(Encoding enc) => Shared.GetOrAdd(enc, Factory);

        public static byte[] For(Encoding enc, string str) => For(enc).Get(str);

        private readonly Encoding _encoding;
        private readonly ConditionalWeakTable<string, byte[]>.CreateValueCallback _factory;
        private readonly ConditionalWeakTable<string, byte[]> _cwt = new();
        public readonly uint NullCharacterSize;
        private EncodedStringCache(Encoding encoding)
        {
            _encoding = encoding;
            _factory = Create;
            NullCharacterSize = (uint)_encoding.GetByteCount("\0");
        }

        public byte[] Get(string s) => _cwt.GetValue(s, _factory);

        private byte[] Create(string s)
        {

            // ReSharper disable once JoinDeclarationAndInitializer
            byte[] bytes;
#if NETSTANDARD2_0
            unsafe
            {
                var charCount = s.Length;
                fixed (char* pS = s)
                {
                    var byteCount = _encoding.GetByteCount(pS, charCount);
                    bytes = new byte[byteCount + NullCharacterSize];
                    fixed (byte* pBytes = bytes)
                        _encoding.GetBytes(pS, charCount, pBytes, byteCount);
                    Unsafe.InitBlockUnaligned(ref bytes[byteCount], 0, NullCharacterSize);
                }
            }
#else
            var byteCount = _encoding.GetByteCount(s);
            bytes = new byte[byteCount + NullCharacterSize];
            _encoding.GetBytes(s, bytes);
            //_encoding.GetBytes("\0", ((Span<byte>)bytes).Slice(byteCount));
            Unsafe.InitBlockUnaligned(ref bytes[byteCount], 0, NullCharacterSize);
#endif
            return bytes;
        }
    }
}
