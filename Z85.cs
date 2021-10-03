using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using ZeroMQ.lib;

namespace ZeroMQ
{
    public static class Z85
    {
        public static unsafe void CurveKeypair(out ArraySegment<byte> publicKey, out ArraySegment<byte> secretKey)
        {
            const int destLen = 40;

            var publicKeyData = new byte[destLen + 1];
            var secretKeyData = new byte[destLen + 1];

            fixed (byte* pPublicKey = publicKeyData)
            fixed (byte* pSecretKey = secretKeyData)
            {
                if (0 != zmq.curve_keypair((IntPtr)pPublicKey, (IntPtr)pSecretKey))
                    throw new InvalidOperationException("Failed to create key pair.", new ZException(ZError.GetLastErr()));
            }

            publicKey = new(publicKeyData, 0, destLen);
            secretKey = new(secretKeyData, 0, destLen);
        }

        public static ArraySegment<byte> Encode(ReadOnlySpan<byte> decoded)
        {
            var dataLen = (uint)decoded.Length;
            if (dataLen % 4 > 0)
                throw new InvalidOperationException("decoded.Length must be divisible by 4");

            var destLen = (uint)(decoded.Length * 1.25);

            // the buffer dest must be one byte larger than destLen to accomodate the null termination character

            var bytes = new byte[destLen + 1];

            if (TryEncode(decoded, bytes))
                return new(bytes, 0, bytes.Length - 1);

            throw new InvalidOperationException("Encoding failed.", new ZException(ZError.GetLastErr()));
        }

        public static unsafe bool TryEncode(ReadOnlySpan<byte> decoded, Span<byte> encoded)
        {
            var dataLen = (uint)decoded.Length;
            if (dataLen % 4 > 0)
                throw new InvalidOperationException("decoded.Length must be divisible by 4");

            var destLen = (uint)(decoded.Length * 1.25);

            // the buffer dest must be one byte larger than destLen to accomodate the null termination character

            var encodedLen = destLen + 1;
            if (encoded.Length < encodedLen)
                throw new InvalidOperationException("encoded.Length must be at least 1.25 * decoded.Length + 1");

            fixed (byte* pDecoded = decoded)
            fixed (byte* pBytes = encoded)
            {
                if (zmq.z85_encode((IntPtr)pBytes, (IntPtr)pDecoded, dataLen) == (IntPtr)pBytes)
                    return true;
            }
            return false;
        }

        public static ArraySegment<byte> ToZ85Encoded(this byte[] decoded)
            => Encode(decoded);

        public static ArraySegment<byte> ToZ85Encoded(this Memory<byte> decoded)
            => Encode(decoded.Span);

        public static ArraySegment<byte> ToZ85Encoded(this Span<byte> decoded)
            => Encode(decoded);

        public static ArraySegment<byte> ToZ85Encoded(this ReadOnlyMemory<byte> decoded)
            => Encode(decoded.Span);

        public static ArraySegment<byte> ToZ85Encoded(this ReadOnlySpan<byte> decoded)
            => Encode(decoded);

        public static string? ToZ85Encoded(this string decoded)
            => Encode(decoded, ZContext.Encoding);

        public static string? ToZ85Encoded(this string decoded, Encoding encoding)
            => Encode(decoded, encoding);

        public static ArraySegment<byte> ToZ85EncodedBytes(this string decoded)
            => EncodeBytes(decoded, ZContext.Encoding);

        public static ArraySegment<byte> ToZ85EncodedBytes(this string decoded, Encoding encoding)
            => EncodeBytes(decoded, encoding);

        public static string? Encode(string str)
            => Encode(str, ZContext.Encoding);

        public static string? Encode(string? str, Encoding encoding)
        {
            if (encoding is null) throw new ArgumentNullException(nameof(encoding));
            if (str is null) return null;
            var encoded = EncodeBytes(str, encoding);
            return encoded.Array is null
                ? null
                : encoding.GetString(encoded.Array, encoded.Offset, encoded.Count);
        }

        public static ArraySegment<byte> EncodeBytes(string? str)
            => str is null ? default : EncodeBytes(str, ZContext.Encoding);

        public static ArraySegment<byte> EncodeBytes(string? str, Encoding encoding)
        {
            if (str is null) return default;
            var bytes = encoding.GetBytes(str);
            return Encode(bytes);
        }


        public static byte[] Decode(ReadOnlySpan<byte> encoded)
        {
            var dataLen = encoded.Length;

            if (dataLen == 0) return Array.Empty<byte>();

            if (dataLen % 5 > 0)
                throw new InvalidOperationException("encoded.Length must be divisible by 5");

            var destLen = (int)(encoded.Length * .8);

            var decoded = new byte[destLen];

            if (TryDecode(encoded, decoded))
                return decoded;

            throw new InvalidOperationException("Decoding failed.", new ZException(ZError.GetLastErr()));
        }


        public static unsafe bool TryDecode(ReadOnlySpan<byte> encoded, Span<byte> decoded)
        {
            var dataLen = encoded.Length;

            if (dataLen == 0) return true;

            if (dataLen % 5 > 0)
                throw new InvalidOperationException("encoded.Length must be divisible by 5");

            var destLen = (int)(encoded.Length * .8);

            if (decoded.Length < destLen)
                throw new InvalidOperationException("decoded.Length must be at least encoded.Length * 0.8");

            fixed (byte* pDecoded = decoded)
            fixed (byte* pEncoded = encoded)
            {
                if (zmq.z85_decode((IntPtr)pDecoded, (IntPtr)pEncoded) == (IntPtr)pDecoded)
                    return true;
            }
            return false;
        }

        public static bool TryDecode(string? encoded, Encoding enc, Span<byte> decoded)
        {
            if (encoded == null) return true;
            var bytePool = ArrayPool<byte>.Shared;
            var strLen = encoded.Length;
            var bytes = bytePool.Rent(enc.GetMaxByteCount(strLen));
            try
            {
                var l = enc.GetBytes(encoded, 0, strLen, bytes, 0);
                return TryDecode(new(bytes, 0, l), decoded);
            }
            finally
            {
                bytePool.Return(bytes);
            }
        }

        public static unsafe bool TryDecode(string? encoded, Encoding enc, out string? decoded)
        {
            if (encoded == null)
            {
                decoded = null;
                return true;
            }

            var bytePool = ArrayPool<byte>.Shared;
            var strLen = encoded.Length;
            var decodedByteLen = (int)(strLen * .8);
            var decodedBytes = bytePool.Rent(decodedByteLen);
            var bytes = bytePool.Rent(enc.GetMaxByteCount(strLen));
            try
            {
                var l = enc.GetBytes(encoded, 0, strLen, bytes, 0);
                if (!TryDecode(new(bytes, 0, l), decodedBytes))
                {
                    decoded = null;
                    return false;
                }

                fixed (byte* pDecodedBytes = decodedBytes)
                    decoded = new((sbyte*)pDecodedBytes, 0, decodedBytes.Length, enc);
                return true;
            }
            finally
            {
                bytePool.Return(bytes);
                bytePool.Return(decodedBytes);
            }
        }

        public static string? Decode(string? str, Encoding encoding)
            => TryDecode(str, encoding, out var decoded) ? decoded : null;

        public static byte[]? DecodeBytes(string? str, Encoding encoding)
        {
            if (encoding is null) throw new ArgumentNullException(nameof(encoding));
            if (str is null) return null;
            var bytes = encoding.GetBytes(str);
            return Decode(bytes);
        }

        public static byte[] ToZ85Decoded(this byte[] encoded)
            => Decode(encoded);

        public static byte[] ToZ85Decoded(this Span<byte> encoded)
            => Decode(encoded);

        public static byte[] ToZ85Decoded(this Memory<byte> encoded)
            => Decode(encoded.Span);

        public static byte[] ToZ85Decoded(this ReadOnlySpan<byte> encoded)
            => Decode(encoded);

        public static byte[] ToZ85Decoded(this ReadOnlyMemory<byte> encoded)
            => Decode(encoded.Span);

        public static string? ToZ85Decoded(this string encoded)
            => Decode(encoded, ZContext.Encoding);

        public static string? ToZ85Decoded(this string encoded, Encoding encoding)
            => Decode(encoded, encoding);

        public static byte[]? ToZ85DecodedBytes(this string encoded)
            => DecodeBytes(encoded, ZContext.Encoding);

        public static byte[]? ToZ85DecodedBytes(this string encoded, Encoding encoding)
            => DecodeBytes(encoded, encoding);

        public static string? Decode(string? str)
            => str is null
                ? null
                : TryDecode(str, ZContext.Encoding, out var decoded)
                    ? decoded
                    : null;
    }
}
