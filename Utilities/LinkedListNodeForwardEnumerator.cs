using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZeroMQ
{
    internal sealed class LinkedListNodeForwardEnumerator<T> : IEnumerator<LinkedListNode<T>>, IEnumerator<T>, IEnumerable<T>
    {
        public static readonly ConcurrentStack<LinkedListNodeForwardEnumerator<T>> Recycled = new();

        private LinkedList<T>? List;
        private LinkedListNode<T>? Node;

        private LinkedListNodeForwardEnumerator(LinkedList<T> list)
            => List = list;

        public static LinkedListNodeForwardEnumerator<T> Create(LinkedList<T> list)
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
                node = list.First;

                if (node is null)
                    return false;

                var nodeList = node.List;

                if (nodeList != list)
                {
                    List = null;
                    return false;
                }

                Node = node;
                return true;
            }
            else
            {
                var nodeList = node.List;

                if (nodeList != list)
                {
                    List = null;
                    return false;
                }

                var next = node.Next;

                if (next is null)
                    return false;

                nodeList = next.List;

                if (nodeList != list)
                {
                    List = null;
                    return false;
                }

                Node = next;
                return true;
            }
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

        public IEnumerator<T> GetEnumerator()
            => this;

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
