using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZeroMQ
{
    public sealed partial class ZFrame
    {
        private static readonly ConcurrentDictionary<Encoding, EncodedStringCache> EncodedStringCaches = new();

        private sealed class EncodedStringCache
        {
            private readonly Encoding _encoding;

            private readonly ConditionalWeakTable<string, byte[]>.CreateValueCallback _factory;

            private readonly ConditionalWeakTable<string, byte[]> _cwt = new();
            private EncodedStringCache(Encoding encoding)
            {
                _encoding = encoding;
                _factory = Create;
            }

            public static EncodedStringCache Create(Encoding enc) => new(enc);

            public static readonly Func<Encoding, EncodedStringCache> Factory = Create;

            public byte[] Get(string s) => _cwt.GetValue(s, _factory);

            private byte[] Create(string s)
            {
                {
                    var enc = _encoding.GetEncoder();

                    // ReSharper disable once JoinDeclarationAndInitializer
                    byte[] bytes;
#if NETSTANDARD2_0
                    unsafe
                    {
                        var charCount = s.Length;
                        fixed (char* pS = s)
                        {
                            var byteCount = enc.GetByteCount(pS, charCount, true);
                            bytes = new byte[byteCount];
                            fixed (byte* pBytes = bytes)
                                enc.GetBytes(pS, charCount, pBytes, byteCount, true);
                        }
                    }
#else
                    var byteCount = enc.GetByteCount(s, true);
                    bytes = new byte[byteCount];
                    enc.GetBytes(s, bytes, true);
#endif
                    return bytes;
                }
            }
        }
    }
}
