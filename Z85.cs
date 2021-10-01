using System;
using System.Runtime.InteropServices;
using System.Text;
using ZeroMQ.lib;

namespace ZeroMQ
{
	public static class Z85
	{
		public static void CurveKeypair(out byte[] publicKey, out byte[] secretKey)
		{
			const int destLen = 40;
			using (var publicKeyData = DispoIntPtr.Alloc(destLen + 1))
			using (var secretKeyData = DispoIntPtr.Alloc(destLen + 1))
			{
				if (0 != zmq.curve_keypair(publicKeyData, secretKeyData))
				{
					throw new InvalidOperationException();
				}
				
				publicKey = new byte[destLen];
				Marshal.Copy(publicKeyData, publicKey, 0, destLen);
				
				secretKey = new byte[destLen];
				Marshal.Copy(secretKeyData, secretKey, 0, destLen);
			}
		}

		public static byte[] Encode(byte[] decoded)
		{
			var dataLen = decoded.Length;
			if (dataLen % 4 > 0)
			{
				throw new InvalidOperationException("decoded.Length must be divisible by 4");
			}
			var destLen = (Int32)(decoded.Length * 1.25);

			var data = GCHandle.Alloc(decoded, GCHandleType.Pinned);

            // the buffer dest must be one byte larger than destLen to accomodate the null termination character
			using (var dest = DispoIntPtr.Alloc(destLen + 1))
			{
                zmq.z85_encode(dest, data.AddrOfPinnedObject(), dataLen);
				data.Free();

				var bytes = new byte[destLen];
				Marshal.Copy(dest, bytes, 0, destLen);
				return bytes;
			}
		}

		public static byte[] ToZ85Encoded(this byte[] decoded)
			=> Encode(decoded);

		public static string ToZ85Encoded(this string decoded)
			=> Encode(decoded, ZContext.Encoding);

		public static string ToZ85Encoded(this string decoded, Encoding encoding)
			=> Encode(decoded, encoding);

		public static byte[] ToZ85EncodedBytes(this string decoded)
			=> EncodeBytes(decoded, ZContext.Encoding);

		public static byte[] ToZ85EncodedBytes(this string decoded, Encoding encoding)
			=> EncodeBytes(decoded, encoding);

		public static string Encode(string strg)
			=> Encode(strg, ZContext.Encoding);

		public static string Encode(string strg, Encoding encoding)
		{
			var encoded = EncodeBytes(strg, encoding);
			return encoding.GetString(encoded);
		}

		public static byte[] EncodeBytes(string strg)
			=> EncodeBytes(strg, ZContext.Encoding);

		public static byte[] EncodeBytes(string strg, Encoding encoding)
		{
			var bytes = encoding.GetBytes(strg);
			return Encode(bytes);
		}


		public static byte[] Decode(byte[] encoded)
		{
			var dataLen = encoded.Length;
			if (dataLen % 5 > 0)
			{
				throw new InvalidOperationException("encoded.Length must be divisible by 5");
			}
			var destLen = (Int32)(encoded.Length * .8);

			var data = GCHandle.Alloc(encoded, GCHandleType.Pinned);

			using (var dest = DispoIntPtr.Alloc(destLen))
			{
                zmq.z85_decode(dest, data.AddrOfPinnedObject());
				data.Free();

				var decoded = new byte[destLen];

				Marshal.Copy(dest, decoded, 0, decoded.Length);

				return decoded;
			}
		}

		public static byte[] ToZ85Decoded(this byte[] encoded)
			=> Decode(encoded);

		public static string ToZ85Decoded(this string encoded)
			=> Decode(encoded, ZContext.Encoding);

		public static string ToZ85Decoded(this string encoded, Encoding encoding)
			=> Decode(encoded, encoding);

		public static byte[] ToZ85DecodedBytes(this string encoded)
			=> DecodeBytes(encoded, ZContext.Encoding);

		public static byte[] ToZ85DecodedBytes(this string encoded, Encoding encoding)
			=> DecodeBytes(encoded, encoding);

		public static string Decode(string strg)
			=> Decode(strg, ZContext.Encoding);

		public static string Decode(string strg, Encoding encoding)
		{
			var encoded = DecodeBytes(strg, encoding);
			return encoding.GetString(encoded);
		}
		
		public static byte[] DecodeBytes(string strg, Encoding encoding)
		{
			var bytes = encoding.GetBytes(strg);
			return Decode(bytes);
		}
	}
}

