using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class ZCert
    {
        /// <summary>
        /// Public key Z85 decoded. Byte array of length 32.
        /// </summary>
        public byte[] PublicKey
        {
            get => publicKey;
            private set => publicKey = value;
        }

        /// <summary>
        /// Secret key Z85 decoded. Byte array of length 32.
        /// </summary>
        public byte[] SecretKey
        {
            get => secretKey;
            private set => secretKey = value;
        }

        /// <summary>
        /// Public key as a string with length 40.  
        /// This is the public key encoded with Z85.
        /// </summary>
        public string PublicTxt
        {
            get => new(publicTxt);
            private set {
                publicTxt = value.ToCharArray();
                publicKey = Z85.DecodeBytes(value, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Secret key as a string with length 40.
        /// This is the secret key encoded with Z85.
        /// </summary>
        public string SecretTxt
        {
            get => new(secretTxt);
            private set {
                secretTxt = value.ToCharArray();
                secretKey = Z85.DecodeBytes(value, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Meta data key value pairs.
        /// </summary>
        private Dictionary<string, string> metadata = new();

        private char[] publicTxt = new char[40];
        private char[] secretTxt = new char[40];
        private byte[] publicKey = new byte[32];
        private byte[] secretKey = new byte[32];

        /// <summary>
        /// Create a valid certificate with a random secret/public key pair.
        /// </summary>
        public ZCert()
        {
            Z85.CurveKeypair(out var publictxt, out var secrettxt);
            publicKey = Z85.Decode(publictxt);
            secretKey = Z85.Decode(secrettxt);

            publicTxt = Encoding.UTF8.GetString(Z85.Encode(publicKey)).ToCharArray();
            secretTxt = Encoding.UTF8.GetString(Z85.Encode(secretKey)).ToCharArray();

            var e = Z85.Encode(publicTxt.Select(c => (byte)c).ToArray());
        }

        /// <summary>
        /// Create a certificate from the given public and secret key.
        /// </summary>
        /// <param name="publicKey">Public key of certificate. This byte array must have the length 32.</param>
        /// <param name="secretKey">Private key of certificate. This byte array must have the length 32.</param>
        /// <exception cref="InvalidOperationException">Exception thrown if the length of the public or secret key is incorrect.</exception>
        public ZCert(byte[] publicKey, byte[] secretKey)
        {
            if (publicKey == null || publicKey.Length != 32)
            {
                throw new InvalidOperationException("public key length must be of length 32");
            }
            if (secretKey == null || secretKey.Length != 32)
            {
                throw new InvalidOperationException("secret key length must be of length 32");
            }
            Array.Copy(publicKey, this.publicKey, 32);
            Array.Copy(secretKey, this.secretKey, 32);

            publicTxt = Encoding.UTF8.GetString(Z85.Encode(publicKey)).ToCharArray();
            secretTxt = Encoding.UTF8.GetString(Z85.Encode(secretKey)).ToCharArray();
        }

        /// <summary>
        /// Create a certificate from the given public and secret key.
        /// </summary>
        /// <param name="publicTxt">Public key of certificate. This string must have 40 characters.</param>
        /// <param name="secretTxt">Private key of certificate. This string must have 40 characters.</param>
        /// <exception cref="InvalidOperationException">Exception thrown if the length of the public or secret key is incorrect.</exception>
        public ZCert(string publicTxt, string secretTxt)
        {
            if (publicTxt == null || publicTxt.Length != 40)
            {
                throw new InvalidOperationException("public text length must be of length 40.");
            }
            if (secretTxt == null || secretTxt.Length != 40)
            {
                throw new InvalidOperationException("secret text length must be of length 40.");
            }

            PublicTxt = publicTxt;
            SecretTxt = secretTxt;

            publicKey = PublicTxt.ToZ85DecodedBytes();
            secretKey = SecretTxt.ToZ85DecodedBytes();

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
            get {
                if (metadata.ContainsKey(name))
                {
                    return metadata[name];
                }
                return "";
            }
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
        /// <param name="cert">Certificate to deep clone. Public and private keys must not be null.</param>
        /// <returns>A copy of the given certificate.</returns>
        public static ZCert Dup(ZCert cert)
        {
            if (cert == null)
                return null;
            return new((
                    byte[])cert.PublicKey.Clone(),
                cert.SecretKey != null ? (byte[])cert.SecretKey.Clone() : new byte[32])
            {
                metadata = new(cert.metadata)
            };
        }

        /// <summary>
        /// Compare to certificate. Return true if public and private keys are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Return true if public and private keys are equal.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ZCert)
            {
                return Equals(obj as ZCert);
            }
            return false;
        }

        /// <summary>
        /// Compare to certificate. Return true if public and private keys are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Return true if public and private keys are equal.</returns>
        public bool Equals(ZCert obj)
        {
            if (obj == null)
            {
                return false;
            }
            return obj.SecretTxt == SecretTxt && obj.PublicTxt == PublicTxt;
        }

        /// <summary>
        /// Return hash code of public key.
        /// </summary>
        /// <returns>Hash code of public key.</returns>
        public override int GetHashCode()
            => PublicTxt != null ? PublicTxt.GetHashCode() : 0;


        private delegate void LineRead(string line, ZCert cert);

        /// <summary>
        /// Load a certificate from file. This will first try to open the secret file by append _secret to the
        /// file name (filename + "_secret"). If the secret file isn't found only the public key is loaded and the secret key will contain 32 zeros.
        /// </summary>
        /// <param name="filename">Filename (excluding the "_secret" ending).</param>
        /// <returns>Return the loaded certificate. OBS! null is returned if the file isn't found.</returns>
        public static ZCert Load(string filename)
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
            LineRead reader = null;
            while (lines.Count > 0)
            {
                var line = lines.Dequeue();
                if (line.TrimStart().StartsWith("#"))
                    continue;
                if (line.TrimStart().StartsWith("metadata"))
                {
                    reader = (str, c) => {
                        var metadata = Split(str);
                        if (metadata.Length == 2)
                        {
                            c.SetMeta(metadata[0].Trim(), metadata[1].Trim('"', ' ', '\t'));
                        }
                    };
                }
                if (line.TrimStart().StartsWith("curve"))
                {
                    reader = (str, c) => {
                        var key = Split(str);
                        if (key.Length == 2)
                        {
                            if (key[0].Trim() == "public-key")
                                c.PublicTxt = key[1].Trim('"', ' ', '\t');
                            if (key[0].Trim() == "secret-key")
                                c.SecretTxt = key[1].Trim('"', ' ', '\t');
                        }
                    };
                }
                if (reader != null)
                {
                    reader(line, cert);
                }
            }
            return cert;
        }

        private static string[] Split(string str)
        {
            var splitindex = str.IndexOf('"');
            var metadata = Array.Empty<string>();
            if (splitindex > 2)
            {
                metadata = new string[2] { str.Substring(0, splitindex - 2).Trim(), str.Substring(splitindex).Trim() };
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
