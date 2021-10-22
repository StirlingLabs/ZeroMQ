using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroMQ
{
    public sealed class ZMetadata : IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>
    {
        private readonly Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _data
            = new(RomSequenceComparer.Instance);

        public IEnumerator<KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>> GetEnumerator()
            => _data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)_data).GetEnumerator();

        public ReadOnlyMemory<byte> ValidateKey(ReadOnlyMemory<byte> key, string argName)
        {
            if (key.Length > 255)
                throw new ArgumentException("Must be less than 255 bytes.", argName);

            if (key.Length == 0)
                throw new ArgumentException("Must be at least 255 bytes.", argName);

            // trim trailing null bytes
            while (key.Span[key.Length - 1] == 0)
                key = key.Slice(0, key.Length - 1);

            for (var i = 0; i < key.Span.Length; i++)
            {
                var b = key.Span[i];
                // name-char = ALPHA | DIGIT | "-" | "_" | "." | "+"
                if (b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z'
                    or >= (byte)'0' and <= (byte)'9'
                    or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'+'
                ) continue;

                throw new ArgumentException($"Character at index {i} is outside of acceptable character set for ZAP metadata name.", argName);
            }
            return key;
        }

        public ReadOnlyMemory<byte> ValidateValue(ReadOnlyMemory<byte> value, bool cutLastNullByte, string argName)
        {
            if (value.Length >= 255)
                throw new ArgumentException("ZAP values may be at most 255 bytes.", argName);

            if (cutLastNullByte)
            {
                if (value.Span[value.Length - 1] != 0)
                    throw new ArgumentException("ZAP values should be null terminated.", argName);
                value = value.Slice(0, value.Length - 1);
            }
            else
            {
                ref var p = ref Unsafe.AsRef(value.Span.GetPinnableReference());
                if (Unsafe.AddByteOffset(ref p, (IntPtr)value.Length) != 0)
                    throw new ArgumentException("ZAP values should be null terminated.", argName);

            }

            return value;
        }


        public void Add(string key, ReadOnlyMemory<byte> value)
            => _data.Add(
                ValidateKey(EncodedStringCache.For(Encoding.UTF8, key), nameof(key)),
                ValidateValue(value, false, nameof(value)));

        public void Add(ReadOnlyMemory<byte> key, string value)
            => _data.Add(
                ValidateKey(key, nameof(key)),
                ValidateValue(EncodedStringCache.For(Encoding.UTF8, value), true, nameof(value)));

        public void Add(string key, string value)
            => _data.Add(
                ValidateKey(EncodedStringCache.For(Encoding.UTF8, key), nameof(key)),
                ValidateValue(new(EncodedStringCache.For(Encoding.UTF8, value)), true, nameof(value)));

        public void Add(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)
            => _data.Add(
                ValidateKey(key, nameof(key)),
                ValidateValue(value, false, nameof(value)));

        public void Add(KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> item)
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data)
                .Add(new(ValidateKey(item.Key, "item.Key"), item.Value));

        public bool Remove(ReadOnlyMemory<byte> key)
            => _data.Remove(key);

        public bool Remove(KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> item)
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data).Remove(item);

        public void Clear()
            => _data.Clear();

        public bool Contains(KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> item)
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data).Contains(item);

        public void CopyTo(KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>[] array, int arrayIndex)
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data).CopyTo(array, arrayIndex);

        public int Count => _data.Count;

        public bool IsReadOnly
            => ((ICollection<KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>)_data).IsReadOnly;

        public bool ContainsKey(ReadOnlyMemory<byte> key)
            => _data.ContainsKey(key);

        public bool TryGetValue(ReadOnlyMemory<byte> key, out ReadOnlyMemory<byte> value)
            => _data.TryGetValue(key, out value);

        public ReadOnlyMemory<byte> this[ReadOnlyMemory<byte> key]
        {
            get => _data[key];
            set => _data[key] = value;
        }

        public ICollection<ReadOnlyMemory<byte>> Keys
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data).Keys;

        public ICollection<ReadOnlyMemory<byte>> Values
            => ((IDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)_data).Values;

        internal byte[] Encode()
        {
            var allocSize = GetEncodedSize();

            if (allocSize == 0)
                return Array.Empty<byte>();

            var data = new byte[allocSize];

            Encode(data);

            return data;
        }

        internal int GetEncodedSize()
        {
            var allocSize = 0;
            foreach (var kvp in this)
                allocSize += 1 + 4 + kvp.Key.Length + kvp.Value.Length;
            return allocSize;
        }

        internal void Encode(Span<byte> data)
        {
            var offset = 0;
            ref var refData = ref data[0];

            foreach (var kvp in this)
            {
                var keyLength = kvp.Key.Length;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref refData, offset), (byte)keyLength);
                offset += 1;
                Unsafe.CopyBlock(ref Unsafe.Add(ref refData, offset), ref Unsafe.AsRef(kvp.Key.Span.GetPinnableReference()), (uint)keyLength);
                offset += keyLength;

                var valueLength = kvp.Value.Length;
                var valueLengthBe = BitConverter.IsLittleEndian
                    ? BinaryPrimitives.ReverseEndianness(valueLength)
                    : valueLength;

                Unsafe.WriteUnaligned(ref Unsafe.Add(ref refData, offset), valueLengthBe);
                offset += 4;
                Unsafe.CopyBlock(ref Unsafe.Add(ref refData, offset), ref Unsafe.AsRef(kvp.Value.Span.GetPinnableReference()),
                    (uint)valueLength);
                offset += valueLength;
            }
        }
    }
}
