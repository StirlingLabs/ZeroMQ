using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZeroMQ
{
    /// <summary>
    /// This class is a port of zcert.c provided in CZMQ, http://czmq.zeromq.org.
    /// 
    /// The ZCert class provides a way to create and work with security
    /// certificates for the ZMQ CURVE mechanism. A certificate contains a
    /// public + secret key pair, plus metadata. It can be used as a
    /// temporary object in memory, or persisted to disk. On disk, a
    /// certificate is stored as two files. One is public and contains only
    /// the public key.The second is secret and contains both keys. The
    /// two have the same filename, with the secret file adding "_secret".
    /// To exchange certificates, send the public file via some secure route.
    /// Certificates are not signed but are text files that can be verified by
    /// eye.
    /// </summary>
    /// <remark>
    /// Certificates are stored in the ZPL (ZMQ RFC 4) format. They have two
    /// sections, "metadata" and "curve". The first contains a list of 'name =
    /// value' pairs, one per line. Values may be enclosed in quotes. The curve
    /// section has a 'public-key = keyvalue' and, for secret certificates, a
    /// 'secret-key = keyvalue' line.The keyvalue is a Z85-encoded CURVE key.
    /// </remark>
    public class ZCert : ICloneable
    {
        /// <summary>
        /// Public key Z85 decoded. Byte array of length 32.
        /// </summary>
        public byte[] PublicKey
        {
            get => _publicKey;
            private set => _publicKey = value;
        }

        /// <summary>
        /// Secret key Z85 decoded. Byte array of length 32.
        /// </summary>
        public byte[] SecretKey
        {
            get => _secretKey;
            private set => _secretKey = value;
        }

        /// <summary>
        /// Public key as a string with length 40.  
        /// This is the public key encoded with Z85.
        /// </summary>
        public string PublicTxt
        {
            get => new(_publicTxt);
            private set {
                _publicTxt = value.ToCharArray();
                _publicKey = Z85.DecodeBytes(value, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Secret key as a string with length 40.
        /// This is the secret key encoded with Z85.
        /// </summary>
        public string SecretTxt
        {
            get => new(_secretTxt);
            private set {
                _secretTxt = value.ToCharArray();
                _secretKey = Z85.DecodeBytes(value, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Meta data key value pairs.
        /// </summary>
        private Dictionary<string, string> metadata = new();

        private char[] _publicTxt = new char[40];
        private char[] _secretTxt = new char[40];

        private byte[] _publicKey = new byte[32];
        private byte[] _secretKey = new byte[32];
        private static readonly char[] ZeroKey = "0000000000000000000000000000000000000000".ToCharArray();

        /// <summary>
        /// Create a valid certificate with a random secret/public key pair.
        /// </summary>
        public ZCert()
        {
            Z85.CurveKeypair(out var publicKey, out var secretKey);

            if (!Z85.TryDecode(publicKey, _publicKey))
                throw new("Unable to decode generated public key.");
            if (!Z85.TryDecode(secretKey, _secretKey))
                throw new("Unable to decode generated secret key.");

            var pk = Z85.Encode(_publicKey);
            if (pk == default || pk.Array is null) throw new("Failed to Z85 encode generated public key.");
            _publicTxt = Encoding.UTF8.GetString(pk.Array, pk.Offset, pk.Count).ToCharArray();

            var sk = Z85.Encode(_secretKey);
            if (sk == default || sk.Array is null) throw new("Failed to Z85 encode generated secret key.");
            _secretTxt = Encoding.UTF8.GetString(sk.Array, sk.Offset, sk.Count).ToCharArray();
        }

        /// <summary>
        /// Create a certificate from the given public and secret key.
        /// </summary>
        /// <param name="publicKey">Public key of certificate. This byte array must have the length 32.</param>
        /// <param name="secretKey">Private key of certificate. This byte array must have the length 32.</param>
        /// <exception cref="InvalidOperationException">Exception thrown if the length of the public or secret key is incorrect.</exception>
        public ZCert(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> secretKey)
        {
            if (publicKey is not { Length: 32 })
                throw new ArgumentException("public key length must be of length 32", nameof(publicKey));
            if (secretKey is not { Length: 32 })
                throw new ArgumentException("secret key length must be of length 32", nameof(secretKey));

            publicKey.CopyTo(_publicKey);
            secretKey.CopyTo(_secretKey);

            var pk = Z85.Encode(publicKey);
            if (pk == default || pk.Array is null) throw new ArgumentException("Failed to Z85 encode public key.", nameof(publicKey));
            _publicTxt = Encoding.UTF8.GetString(pk.Array, pk.Offset, pk.Count).ToCharArray();

            var sk = Z85.Encode(secretKey);
            if (sk == default || sk.Array is null) throw new ArgumentException("Failed to Z85 encode secret key.", nameof(secretKey));
            _secretTxt = Encoding.UTF8.GetString(sk.Array, sk.Offset, sk.Count).ToCharArray();
        }

        /// <summary>
        /// Create a certificate from the given public and secret key.
        /// </summary>
        /// <param name="publicKey">Public key of certificate. This byte array must have the length 32.</param>
        /// <exception cref="InvalidOperationException">Exception thrown if the length of the public or secret key is incorrect.</exception>
        public ZCert(ReadOnlySpan<byte> publicKey)
        {
            if (publicKey is not { Length: 32 })
                throw new ArgumentException("public key length must be of length 32", nameof(publicKey));

            publicKey.CopyTo(_publicKey);

            var pk = Z85.Encode(publicKey);
            if (pk == default || pk.Array is null) throw new ArgumentException("Failed to Z85 encode public key.", nameof(publicKey));
            _publicTxt = Encoding.UTF8.GetString(pk.Array, pk.Offset, pk.Count).ToCharArray();

            _secretTxt = ZeroKey;
        }

        /// <summary>
        /// Create a certificate from the given public and secret key.
        /// </summary>
        /// <param name="publicTxt">Public key of certificate. This string must have 40 characters.</param>
        /// <param name="secretTxt">Private key of certificate. This string must have 40 characters.</param>
        /// <exception cref="InvalidOperationException">Exception thrown if the length of the public or secret key is incorrect.</exception>
        public ZCert(string publicTxt, string secretTxt)
        {
            if (publicTxt is not { Length: 40 })
                throw new ArgumentException("public text length must be of length 40.", nameof(publicTxt));
            if (secretTxt is not { Length: 40 })
                throw new ArgumentException("secret text length must be of length 40.", nameof(secretTxt));

            PublicTxt = publicTxt;
            SecretTxt = secretTxt;

            if (!Z85.TryDecode(publicTxt, Encoding.UTF8, _publicKey))
                throw new ArgumentException("Failed to Z85 decode public key.", nameof(publicTxt));
            if (!Z85.TryDecode(secretTxt, Encoding.UTF8, _secretKey))
                throw new ArgumentException("Failed to Z85 decode secret key.", nameof(secretTxt));

        }

        /// <summary>
        /// Set meta data name value pair on the certificate.
        /// </summary>
        /// <param name="name">Name of meta data.</param>
        /// <param name="value">Value of meta data.</param>
        public void SetMeta(string name, string value)
            => metadata[name] = value;

        /// <summary>
        /// Remove a meta data from the certificate.
        /// </summary>
        /// <param name="name"></param>
        public void UnsetMeta(string name)
        {
            if (metadata.ContainsKey(name))
                metadata.Remove(name);
        }

        /// <summary>
        /// Get the value of this meta data. An empty string is returned if the meta data isn't found.
        /// </summary>
        /// <param name="name">Name of meta data</param>
        /// <returns></returns>
        public string this[string name]
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => metadata.TryGetValue(name, out var value) ? value : "";
        }

        public Dictionary<string, string> MetaData => metadata.ToDictionary(entry => entry.Key, entry => entry.Value);

        /// <summary>
        /// Apply certificate to the given socket. This will set the CurveSecretKey equal to this certificate secret key and
        /// the CurvePublicKey equal to this certificate public key.
        /// </summary>
        /// <param name="socket">Socket to set curve certificate on.</param>
        public void Apply(ZSocket socket)
        {
            socket.CurveSecretKey = SecretKey;
            socket.CurvePublicKey = PublicKey;
        }

        /// <summary>
        /// Duplicate this certificate by doing a deep clone.
        /// </summary>
        /// <returns>A copy of the given certificate.</returns>
        public ZCert Clone()
            => new(
                    (byte[])PublicKey.Clone(),
                    (byte[])SecretKey.Clone())
                { metadata = new(metadata) };

        object ICloneable.Clone() => Clone();

        /// <summary>
        /// Compare to certificate. Return true if public and private keys are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Return true if public and private keys are equal.</returns>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
            => obj is ZCert cert && Equals(cert);

        /// <summary>
        /// Compare to certificate. Return true if public and private keys are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Return true if public and private keys are equal.</returns>
        public bool Equals(ZCert? obj)
        {
            if (obj == null)
                return false;
            return obj.SecretTxt == SecretTxt && obj.PublicTxt == PublicTxt;
        }

        /// <summary>
        /// Return hash code of public key.
        /// </summary>
        /// <returns>Hash code of public key.</returns>
        public override int GetHashCode()
            => PublicTxt.GetHashCode();


        private delegate void LineRead(string line, ZCert? cert);

        /// <summary>
        /// Load a certificate from file. This will first try to open the secret file by append _secret to the
        /// file name (filename + "_secret"). If the secret file isn't found only the public key is loaded and the secret key will contain 32 zeros.
        /// </summary>
        /// <param name="filename">Filename (excluding the "_secret" ending).</param>
        /// <returns>Return the loaded certificate. OBS! null is returned if the file isn't found.</returns>
        public static ZCert? Load(string filename)
        {
            var cert = new ZCert();
            //  Try first to load secret certificate, which has both keys
            //  Then fallback to loading public certificate
            var filenameSecret = filename + "_secret";
            Queue<string> lines;
            if (File.Exists(filenameSecret))
            {
                lines = new(File.ReadAllLines(filenameSecret).ToList());
            }
            else if (File.Exists(filename))
            {
                lines = new(File.ReadAllLines(filename).ToList());
            }
            else
            {
                return null;
            }
            LineRead? reader = null;
            while (lines.Count > 0)
            {
                var line = lines.Dequeue();
                if (line.TrimStart().StartsWith("#"))
                    continue;
                if (line.TrimStart().StartsWith("metadata"))
                {
                    reader = (str, c) => {
                        var metadata = Split(str);
                        if (metadata.Length != 2)
                            return;
                        if (c is null) throw new InvalidOperationException($"Missing Certificate for {metadata}");
                        c.SetMeta(metadata[0].Trim(), metadata[1].Trim('"', ' ', '\t'));
                    };
                }
                if (line.TrimStart().StartsWith("curve"))
                {
                    reader = (str, c) => {
                        var key = Split(str);
                        if (key.Length != 2)
                            return;
                        if (key[0].Trim() == "public-key")
                        {
                            var pubKey = key[1].Trim('"', ' ', '\t');
                            if (c is null) throw new InvalidOperationException($"Missing Certificate for PK {pubKey}");
                            c.PublicTxt = pubKey;
                        }
                        if (key[0].Trim() == "secret-key")
                        {
                            if (c is null) throw new InvalidOperationException("Missing Certificate");
                            c.SecretTxt = key[1].Trim('"', ' ', '\t');
                        }
                    };
                }
                reader?.Invoke(line, cert);
            }
            return cert;
        }

        private static string[] Split(string str)
        {
            var splitIndex = str.IndexOf('"');
            var metadata = Array.Empty<string>();
            if (splitIndex > 2)
            {
                metadata = new string[2] { str.Substring(0, splitIndex - 2).Trim(), str.Substring(splitIndex).Trim() };
            }

            return metadata;
        }

        private List<string> GetMetadataAll(string filename, IEnumerable<string> headers)
        {
            var lines = new List<string>();
            lines.AddRange(headers);
            lines.Add("");
            lines.Add("metadata");
            foreach (var pair in metadata)
            {
                lines.Add("    " + pair.Key + " = \"" + pair.Value + "\"");
            }
            return lines;
        }

        /// <summary>
        /// Save the public key and meta data in this certificate to file.
        /// Also save the public and secret key with meta data to file with name given by (filename + "_secret").
        /// </summary>
        /// <param name="filename"></param>
        public void Save(string filename)
        {
            SavePublic(filename);
            SaveSecret(filename + "_secret");
        }


        /// <summary>
        /// Save the public key and meta data in this certificate to file.
        /// </summary>
        /// <param name="filename"></param>
        public void SavePublic(string filename)
        {
            var lines = GetMetadataAll(filename,
                new List<string>
                {
                    "#   ****  Generated on " + DateTime.Now.ToLongDateString(),
                    "#   ZeroMQ CURVE Public Certificate",
                    "#   Exchange securely, or use a secure mechanism to verify the contents",
                    "#   of this file after exchange. Store public certificates in your home",
                    "#   directory, in the .curve subdirectory.",
                });
            lines.Add("curve");
            lines.Add("    public-key = \"" + PublicTxt + "\"");
            File.WriteAllLines(filename, lines.ToArray());
        }

        /// <summary>
        /// Save the public and secret key with meta data to file.
        /// </summary>
        /// <param name="filename"></param>
        public void SaveSecret(string filename)
        {
            var lines = GetMetadataAll(filename,
                new List<string>
                {
                    "#   ****  Generated on " + DateTime.Now.ToLongDateString(),
                    "#   ZeroMQ CURVE **Secret** Certificate",
                    "#   DO NOT PROVIDE THIS FILE TO OTHER USERS nor change its permissions."
                });
            lines.Add("curve");
            lines.Add("    public-key = \"" + PublicTxt + "\"");
            lines.Add("    secret-key = \"" + SecretTxt + "\"");
            File.WriteAllLines(filename, lines.ToArray());
        }
    }
}
