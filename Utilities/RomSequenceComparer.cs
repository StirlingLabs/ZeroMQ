using System;
using System.Collections.Generic;

namespace ZeroMQ
{
    internal class RomSequenceComparer : IComparer<ReadOnlyMemory<byte>>, IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public static readonly RomSequenceComparer Instance = new();

        private RomSequenceComparer() { }

        public int Compare(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
            => x.Span.SequenceCompareTo(y.Span);

        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
            => x.Length == y.Length && x.Span.SequenceEqual(y.Span);

        public int GetHashCode(ReadOnlyMemory<byte> obj)
            => obj.GetHashCode();
    }
}
