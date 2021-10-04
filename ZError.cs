using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    public sealed class ZError : IEquatable<ZError>, IComparable<ZError>
    {
        public static readonly ZError?
            None = null; // null

        public static readonly ZError
            EPERM = new(nameof(EPERM)),
            ENOENT = new(nameof(ENOENT)),
            ESRCH = new(nameof(ESRCH)),
            EINTR = new(nameof(EINTR)),
            EIO = new(nameof(EIO)),
            ENXIO = new(nameof(ENXIO)),
            E2BIG = new(nameof(E2BIG)),
            ENOEXEC = new(nameof(ENOEXEC)),
            EBADF = new(nameof(EBADF)),
            ECHILD = new(nameof(ECHILD)),
            EAGAIN = new(nameof(EAGAIN)),
            ENOMEM = new(nameof(ENOMEM)),
            EACCES = new(nameof(EACCES)),
            EFAULT = new(nameof(EFAULT)),
            ENOTBLK = new(nameof(ENOTBLK)),
            EBUSY = new(nameof(EBUSY)),
            EEXIST = new(nameof(EEXIST)),
            EXDEV = new(nameof(EXDEV)),
            ENODEV = new(nameof(ENODEV)),
            ENOTDIR = new(nameof(ENOTDIR)),
            EISDIR = new(nameof(EISDIR)),
            EINVAL = new(nameof(EINVAL)),
            ENFILE = new(nameof(ENFILE)),
            EMFILE = new(nameof(EMFILE)),
            ENOTTY = new(nameof(ENOTTY)),
            ETXTBSY = new(nameof(ETXTBSY)),
            EFBIG = new(nameof(EFBIG)),
            ENOSPC = new(nameof(ENOSPC)),
            ESPIPE = new(nameof(ESPIPE)),
            EROFS = new(nameof(EROFS)),
            EMLINK = new(nameof(EMLINK)),
            EPIPE = new(nameof(EPIPE)),
            EDOM = new(nameof(EDOM)),
            ERANGE, // 34
            ENOTSUP = new(nameof(ENOTSUP)),
            EPROTONOSUPPORT = new(nameof(EPROTONOSUPPORT)),
            ENOBUFS = new(nameof(ENOBUFS)),
            ENETDOWN = new(nameof(ENETDOWN)),
            EADDRINUSE = new(nameof(EADDRINUSE)),
            EADDRNOTAVAIL = new(nameof(EADDRNOTAVAIL)),
            ECONNREFUSED = new(nameof(ECONNREFUSED)),
            EINPROGRESS = new(nameof(EINPROGRESS)),
            ENOTSOCK = new(nameof(ENOTSOCK)),
            EMSGSIZE = new(nameof(EMSGSIZE)),
            // as of here are differences to nanomsg
            EAFNOSUPPORT = new(nameof(EAFNOSUPPORT)),
            ENETUNREACH = new(nameof(ENETUNREACH)),
            ECONNABORTED = new(nameof(ECONNABORTED)),
            ECONNRESET = new(nameof(ECONNRESET)),
            ENOTCONN = new(nameof(ENOTCONN)),
            ETIMEDOUT = new(nameof(ETIMEDOUT)),
            EHOSTUNREACH = new(nameof(EHOSTUNREACH)),
            ENETRESET = new(nameof(ENETRESET)),
            /*  Native ZeroMQ error codes. */
            EFSM = new(nameof(EFSM)),
            ENOCOMPATPROTO = new(nameof(ENOCOMPATPROTO)),
            ETERM = new(nameof(ETERM)),
            EMTHREAD = new(nameof(EMTHREAD));


        static readonly ConcurrentDictionary<int, ZError> NumToError = new();

        private ZError(int errno, string name)
        {
            Number = errno;
            _lazyText = new(() => StrErrorInternal(Number));
            Name = name;
        }

        private ZError(string s) : this(ZErrorCode.ReverseLookup(s), s) { }


        private string StrErrorInternal(int num)
        {
            var pStr = zmq.strerror(num);
            return (pStr == default ? null : Marshal.PtrToStringAnsi(pStr))!;
        }

        public int Number { get; }

        public string? Name { get; }

        private readonly Lazy<string?> _lazyText;
        public string? Text => _lazyText.Value;

        public static bool operator ==(ZError? left, ZError? right)
            => Equals(left, right);

        public static bool operator !=(ZError? left, ZError? right)
            => !Equals(left, right);

        public bool Equals(ZError? other)
            => Number == other?.Number;

        public int CompareTo(ZError? other)
            => Number.CompareTo(other?.Number);

        public override bool Equals(object? obj)
            => Equals(this, obj);

        public new static bool Equals(object? a, object? b)
            => ReferenceEquals(a, b)
                || a is ZError symbolA
                && b is ZError symbolB
                && symbolA.Number == symbolB.Number;

        public override int GetHashCode()
            => Number.GetHashCode();

        public override string ToString()
            => Name + " (" + Number + "): " + Text;

        public static implicit operator int(ZError error)
            => error.Number;

        public static ZError? GetLastError()
            => FromNumber(zmq.errno());

        public static ZError? FromNumber(int num)
            => NumToError.GetOrAdd(num, n => new(n, "UnknownErrorCode_" + n));
    }

    [UsedImplicitly]
    internal static class ZErrorCode
    {
        internal static int ReverseLookup(string name)
        {
            var value = -1;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                value = Enum.TryParse<MacOSX>(name, out var eMacOsx) ? (int)eMacOsx : -1;

            if (value != -1)
                return value;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                value = Enum.TryParse<Posix>(name, out var ePosix) ? (int)ePosix : -1;

            if (value != -1)
                return value;

            value = Enum.TryParse<Common>(name, out var eCommon) ? (int)eCommon : -1;

            return value;
        }


        internal const int HAUSNUMERO = 156384712;

        internal enum Common
        {
            EPERM = 1,
            ENOENT = 2,
            ESRCH = 3,
            EINTR = 4,
            EIO = 5,
            ENXIO = 6,
            E2BIG = 7,
            ENOEXEC = 8,
            EBADF = 9,
            ECHILD = 10,
            EAGAIN = 11,
            ENOMEM = 12,
            EACCES = 13,
            EFAULT = 14,
            ENOTBLK = 15,
            EBUSY = 16,
            EEXIST = 17,
            EXDEV = 18,
            ENODEV = 19,
            ENOTDIR = 20,
            EISDIR = 21,
            EINVAL = 22,
            ENFILE = 23,
            EMFILE = 24,
            ENOTTY = 25,
            ETXTBSY = 26,
            EFBIG = 27,
            ENOSPC = 28,
            ESPIPE = 29,
            EROFS = 30,
            EMLINK = 31,
            EPIPE = 32,
            EDOM = 33,
            ERANGE = 34, // 34
            ENOTSUP = 129,
            EPROTONOSUPPORT = 135,
            ENOBUFS = 119,
            ENETDOWN = 116,
            EADDRINUSE = 100,
            EADDRNOTAVAIL = 101,
            ECONNREFUSED = 107,
            EINPROGRESS = 112,
            ENOTSOCK = 128,
            EMSGSIZE = 115,
            // as of here are differences to nanomsg
            EAFNOSUPPORT = 102,
            ENETUNREACH = 118,
            ECONNABORTED = 106,
            ECONNRESET = 108,
            ENOTCONN = 126,
            ETIMEDOUT = 138,
            EHOSTUNREACH = 110,
            ENETRESET = 117,
            /*  Native ZeroMQ error codes. */
            EFSM = HAUSNUMERO + 51,
            ENOCOMPATPROTO = HAUSNUMERO + 52,
            ETERM = HAUSNUMERO + 53,
            EMTHREAD = HAUSNUMERO + 54
        }

        internal enum Posix
        {
            // ENOTSUP = HAUSNUMERO + 1,
            EPROTONOSUPPORT = 93,
            ENOBUFS = 105,
            ENETDOWN = 100,
            EADDRINUSE = 98,
            EADDRNOTAVAIL = 99,
            ECONNREFUSED = 111,
            EINPROGRESS = 115,
            ENOTSOCK = 88,
            EMSGSIZE = 90,
            EAFNOSUPPORT = 97,
            ENETUNREACH = 101,
            ECONNABORTED = 103,
            ECONNRESET = 104,
            ENOTCONN = 107,
            ETIMEDOUT = 110,
            EHOSTUNREACH = 113,
            ENETRESET = 102
        }

        internal enum MacOSX
        {
            EAGAIN = 35,
            EINPROGRESS = 36,
            ENOTSOCK = 38,
            EMSGSIZE = 40,
            EPROTONOSUPPORT = 43,
            EAFNOSUPPORT = 47,
            EADDRINUSE = 48,
            EADDRNOTAVAIL = 49,
            ENETDOWN = 50,
            ENETUNREACH = 51,
            ENETRESET = 52,
            ECONNABORTED = 53,
            ECONNRESET = 54,
            ENOBUFS = 55,
            ENOTCONN = 57,
            ETIMEDOUT = 60,
            EHOSTUNREACH = 65
        }
    }
}
