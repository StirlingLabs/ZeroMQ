using System;
using System.Collections;
using System.Collections.Generic;

namespace ZeroMQ
{
    internal sealed class LinkedListNodeEnumerable<T> : IEnumerable<LinkedListNode<T>>, IEnumerable<T>
    {
        public readonly LinkedList<T> List;
        private readonly bool _backward;

        public LinkedListNodeEnumerable(LinkedList<T> list, bool backward)
        {
            List = list;
            _backward = backward;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => GetEnumerator() as IEnumerator<T>
                ?? throw new NotImplementedException();

        public IEnumerator<LinkedListNode<T>> GetEnumerator()
            => List.Count == 0
                ? LinkedListNodeEmptyEnumerator<T>.Instance
                : _backward
                    ? LinkedListNodeBackwardEnumerator<T>.Create(List)
                    : LinkedListNodeForwardEnumerator<T>.Create(List);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}