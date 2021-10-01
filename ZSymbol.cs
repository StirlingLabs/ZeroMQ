﻿// using System.Runtime.Remoting.Messaging;

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
    public class ZSymbol
    {
        protected internal ZSymbol(int errno)
            => _num = errno;

        private int _num;
        public int Number => _num;

        public string Name => _zsymbolToName.TryGetValue(this, out var result) ? result : "<unknown>";

        public string Text => Marshal.PtrToStringAnsi(zmq.strerror(_num));

        private static void PickupConstantSymbols<T>(ref IDictionary<ZSymbol, string> symbols)
            where T : ZSymbol
        {
            var type = typeof(T);

            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);

            var codeType = type.GetNestedType("Code", BindingFlags.NonPublic);

            // Pickup constant symbols
            foreach (var symbolField in fields.Where(f => typeof(ZSymbol).IsAssignableFrom(f.FieldType)))
            {
                var symbolCodeField = codeType?.GetField(symbolField.Name);

                if (symbolCodeField == null)
                    continue;

                var symbolNumber = (int)symbolCodeField.GetValue(null);

                var symbol = Activator.CreateInstance(
                    type,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new object[] { symbolNumber },
                    null);
                symbolField.SetValue(null, symbol);
                symbols.Add((ZSymbol)symbol, symbolCodeField.Name);
            }
        }

        public static readonly ZSymbol None = default;

        static ZSymbol()
        {
            // System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(zmq).TypeHandle);

            IDictionary<ZSymbol, string> symbols = new Dictionary<ZSymbol, string>();

            PickupConstantSymbols<ZError>(ref symbols);

            _zsymbolToName = symbols;
        }

        static IDictionary<ZSymbol, string> _zsymbolToName;

        public static IEnumerable<ZSymbol> Find(string symbol)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value == symbol).Select(x => x.Key);

        public static IEnumerable<ZSymbol> Find(string ns, int num)
            => _zsymbolToName
                .Where(s => s.Value != null && s.Value.StartsWith(ns) && s.Key._num == num).Select(x => x.Key);

        public override bool Equals(object obj)
            => Equals(this, obj);

        public new static bool Equals(object a, object b)
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
