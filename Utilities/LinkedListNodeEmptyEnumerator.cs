using System;
using System.Collections;
using System.Collections.Generic;

namespace ZeroMQ
{
    internal sealed class LinkedListNodeEmptyEnumerator<T> : IEnumerator<LinkedListNode<T>>, IEnumerator<T>
    {
        public static readonly LinkedListNodeEmptyEnumerator<T> Instance = new();

        private LinkedListNodeEmptyEnumerator() { }

        public bool MoveNext()
            => false;

        public void Reset() { }

        T IEnumerator<T>.Current => throw new ObjectDisposedException(nameof(LinkedListNodeEmptyEnumerator<T>));

        public LinkedListNode<T> Current => throw new ObjectDisposedException(nameof(LinkedListNodeEmptyEnumerator<T>));

        object? IEnumerator.Current => Current;

        public void Dispose() { }
    }
}