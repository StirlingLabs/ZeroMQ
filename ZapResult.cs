using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace ZeroMQ
{
    [PublicAPI]
    public class ZapResult
    {
        private static readonly Regex RxSplitPascalCase = new("(?<=[a-z])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private ZapResult(int statusCode, string? statusText, bool identityIsNotSet)
        {
            switch (statusCode)
            {
                case < 200 or > 599: throw new ArgumentOutOfRangeException(nameof(statusCode));
                case >= 200 and <= 299: {
                    if (identityIsNotSet)
#pragma warning disable CA2208
                        throw new ArgumentException(
                            "On successful status code replies, identity must be valid.",
                            // ReSharper disable once NotResolvedInText
                            "identity");
#pragma warning restore CA2208
                    break;
                }
                default: {
                    if (!identityIsNotSet)
#pragma warning disable CA2208
                        throw new ArgumentException(
                            "On unsuccessful status code replies, identity must be blank.",
                            // ReSharper disable once NotResolvedInText
                            "identity");
                    break;
                }
            }

#pragma warning restore CA2208

            StatusCode = statusCode;

            if (statusText is not null)
                StatusText = statusText;
            else
            {
#if NET5_0_OR_GREATER
                var statusCodeName = Enum.GetName((HttpStatusCode)statusCode);
#else
                var statusCodeName = Enum.GetName(typeof(HttpStatusCode), (HttpStatusCode)statusCode);
#endif
                StatusText = statusCodeName is not null
                    ? RxSplitPascalCase.Replace(statusCodeName, " $1")
                    : "Unknown";
            }

        }

        public ZapResult(int statusCode, string? statusText = null, ReadOnlyMemory<byte> identity = default)
            : this(statusCode, statusText, identity.IsEmpty)
            => Identity = identity;
        public ZapResult(int statusCode, ReadOnlyMemory<byte> identity)
            : this(statusCode, null, identity) { }

        public ZapResult(int statusCode, string? statusText, string? identity)
            : this(statusCode, statusText, string.IsNullOrEmpty(identity))
            => Identity = string.IsNullOrEmpty(identity)
                ? Memory<byte>.Empty
                : EncodedStringCache.For(Encoding.UTF8, identity);

        public int StatusCode { get; set; }
        public string StatusText { get; set; }
        public ReadOnlyMemory<byte> Identity { get; set; }
        public ZMetadata Metadata { get; } = new();

        public static implicit operator ZapResult(int statusCode) => new(statusCode);
    }
}
