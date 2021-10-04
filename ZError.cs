using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroMQ.lib;

namespace ZeroMQ
{
    public sealed class ZError
    {
        public static bool operator ==(ZError? left, ZError? right)
            => Equals(left, right);

        public static bool operator !=(ZError? left, ZError? right)
            => !Equals(left, right);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ZError(string str)
            : this(ResolveNumber<ZError>(str))
            => Debug.Assert(str != "EAGAIN" || Number == 11);

        private ZError(int errno)
            => _num = errno;

        private int _num;
        public int Number => _num;

        public string Name => _zsymbolToName.TryGetValue(this, out var result) ? result : "<unknown>";

        public string? Text
        {
            get {
                var pStr = zmq.strerror(_num);
                return pStr == default ? null : Marshal.PtrToStringAnsi(pStr);
            }
        }

        private static int ResolveNumber<T>(string name)
        {
            var type = typeof(T);

            var codeType = type.GetNestedType("Code",
                BindingFlags.Public | BindingFlags.NonPublic);

            var symbolCodeField = codeType?.GetField(name);

            if (symbolCodeField == null)
                throw new ArgumentException(nameof(name),
                    $"Missing symbol declaration for {name}.");

            var value = symbolCodeField.GetValue(null);

            return value switch
            {
                int i => i,
                IConvertible convertible => convertible.ToInt32(null),
                null => throw new ArgumentException(nameof(name), $"Missing symbol definition for {name}."),
                _ => throw new ArgumentException(nameof(name), $"Invalid symbol definition for {name}.")
            };

        }

        static IDictionary<ZError, string> _zsymbolToName = new Dictionary<ZError, string>();

        public static IEnumerable<ZError> Find(string symbol)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value == symbol).Select(x => x.Key);

        public static IEnumerable<ZError> Find(string ns, int num)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value.StartsWith(ns) && s.Key._num == num).Select(x => x.Key);

        public bool Equals(ZError? other)
            => _num == other?._num;

        public override bool Equals(object? obj)
            => Equals(this, obj);

        public new static bool Equals(object? a, object? b)
            => ReferenceEquals(a, b)
                || a is ZError symbolA
                && b is ZError symbolB
                && symbolA._num == symbolB._num;

        public override int GetHashCode()
            => Number.GetHashCode();

        public override string ToString()
            => Name + "(" + Number + "): " + Text;

        public static implicit operator int(ZError errnum)
            => errnum.Number;

        private static class Code
        {
            private const int HAUSNUMERO = 156384712;

            // TODO: find a way to make this independent of the Windows SDK version that libzmq was built against
            // TODO: are all of these actually used by libzmq?
            // these values are the Windows error codes as defined by the Windows 10 SDK when _CRT_NO_POSIX_ERROR_CODES is not defined
            public static readonly int
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
                EMTHREAD = HAUSNUMERO + 54;

            private static class Posix
            {
                // source: http://www.virtsync.com/c-error-codes-include-errno

                public static readonly int
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
                    ENETRESET = 102;
            }

            private static class MacOSX
            {
                public static readonly int
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
                    EHOSTUNREACH = 65;
            }
        }

        public static ZError? GetLastErr()
        {
            var errno = zmq.errno();

            return FromErrno(errno);
        }

        public static ZError? FromErrno(int num)
        {
            // TODO: this can be made more efficient
            var symbol = Find("E", num)
                .FirstOrDefault();

            if (symbol != null)
                return symbol;

            // unexpected error
            return new(num);
        }

        public static readonly ZError? None = null; // null

        public static readonly ZError
            // DEFAULT = new ZmqError(0),
            EPERM = new(nameof(EPERM)),
            ENOENT = new(nameof(ENOENT)),
            ESRCH = new(nameof(ESRCH)),
            EINTR = new(nameof(EINTR)),
            EIO = new(nameof(EIO)),
            ENXIO = new(nameof(ENXIO)),
            E2BIG = new(nameof(E2BIG)),
            ENOEXEC = new(nameof(ENOEXEC)),
            EBADF = new(nameof(EBADF)),
            ECHILD = new(nameof(ECHILD)) // = HAUSNUMERO + 54
            ;
        public static readonly ZError
            // DEFAULT = new ZmqError(0),
            EAGAIN = new(nameof(EAGAIN)) // = HAUSNUMERO + 54
            ;
        public static readonly ZError
            // DEFAULT = new ZmqError(0),
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
            ERANGE = new(nameof(ERANGE)), // 34
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
            ENOCOMPATPROTO = new(nameof(ENOCOMPATPROTO)) // = HAUSNUMERO + 54
            ;
        public static readonly ZError
            // DEFAULT = new ZmqError(0),
            ETERM = new(nameof(ETERM)) // = HAUSNUMERO + 54
            ;
        public static readonly ZError
            // DEFAULT = new ZmqError(0),
            EMTHREAD = new(nameof(EMTHREAD)) // = HAUSNUMERO + 54
            ;
    }
}
