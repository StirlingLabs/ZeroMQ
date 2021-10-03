using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZeroMQ
{
    internal sealed class LinkedListNodeBackwardEnumerator<T> : IEnumerator<LinkedListNode<T>>, IEnumerator<T>
    {
        public static readonly ConcurrentStack<LinkedListNodeBackwardEnumerator<T>> Recycled = new();

        private LinkedList<T>? List;
        private LinkedListNode<T>? Node;

        private LinkedListNodeBackwardEnumerator(LinkedList<T> list)
            => List = list;

        public static LinkedListNodeBackwardEnumerator<T> Create(LinkedList<T> list)
        {
            if (Recycled.TryPop(out var r))
            {
                r.List = list;
                return r;
            }

            return new(list);
        }

        public bool MoveNext()
        {
            var node = Node;

            var list = List;

            if (list is null)
                throw new ObjectDisposedException("List was null.");

            if (node is null)
            {
                node = list.Last;

                if (node is null)
                    return false;

                if (node.List != list)
                {
                    List = null;
                    return false;
                    //throw new ObjectDisposedException("Different list.");
                }

                Node = node;
                return true;
            }

            if (node.List != list)
            {
                List = null;
                return false;
                //throw new ObjectDisposedException("Different list.");
            }

            var next = node.Previous;

            if (next is null)
                return false;

            Node = next;
            return true;
        }

        public void Reset()
        {
            Node = null;
            if (List is null) throw new ObjectDisposedException("List was null during reset.");
        }

        private LinkedListNode<T> NodeOrBust
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Node ?? throw new ObjectDisposedException("Node was null.");
        }

        T IEnumerator<T>.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NodeOrBust.Value;
        }

        public LinkedListNode<T> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NodeOrBust;
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Current;
        }

        public void Dispose()
        {
            Node = null;
            List = null!;
            Recycled.Push(this);
        }
    }
}
