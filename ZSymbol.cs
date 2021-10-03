using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ZeroMQ.lib;

namespace ZeroMQ
{
    /// <summary>
    /// TODO merge this with its sole subclass, ZError
    /// </summary>
    public class ZSymbol : IEquatable<ZSymbol>
    {
        public static bool operator ==(ZSymbol? left, ZSymbol? right)
            => Equals(left, right);

        public static bool operator !=(ZSymbol? left, ZSymbol? right)
            => !Equals(left, right);

        protected internal ZSymbol(int errno)
            => _num = errno;

        private int _num;
        public int Number => _num;

        public string Name => _zsymbolToName.TryGetValue(this, out var result) ? result : "<unknown>";

        public string? Text => Marshal.PtrToStringAnsi(zmq.strerror(_num));

        protected static int ResolveNumber<T>(string name)
        {
            var type = typeof(T);

            var codeType = type.GetNestedType("Code", BindingFlags.NonPublic);

            var symbolCodeField = codeType?.GetField(name);

            if (symbolCodeField == null)
                throw new ArgumentException(nameof(name), $"Missing symbol definition for {name}.");

            if (symbolCodeField.GetValue(null) is IConvertible convertible)
                return convertible.ToInt32(null);

            
            throw new ArgumentException(nameof(name), $"Invalid symbol definition for {name}.");
        }

        public static readonly ZSymbol? None = default!;

        static IDictionary<ZSymbol, string> _zsymbolToName = new Dictionary<ZSymbol, string>();

        public static IEnumerable<ZSymbol> Find(string symbol)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value == symbol).Select(x => x.Key);

        public static IEnumerable<ZSymbol> Find(string ns, int num)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value.StartsWith(ns) && s.Key._num == num).Select(x => x.Key);

        public bool Equals(ZSymbol? other)
            => _num == other?._num;

        public override bool Equals(object? obj)
            => Equals(this, obj);

        public new static bool Equals(object? a, object? b)
            => ReferenceEquals(a, b)
                || a is ZSymbol symbolA
                && b is ZSymbol symbolB
                && symbolA._num == symbolB._num;

        public override int GetHashCode()
            => Number.GetHashCode();

        public override string ToString()
            => Name + "(" + Number + "): " + Text;

        public static implicit operator int(ZSymbol errnum)
            => errnum.Number;
    }
}
