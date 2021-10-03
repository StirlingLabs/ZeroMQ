using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ZeroMQ
{
    internal static class LinkedListExtensions
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<LinkedListNode<T>> Nodes<T>(this LinkedList<T> list, bool backward = false)
            => new LinkedListNodeEnumerable<T>(list, backward);

        public static int IndexOf<T>(this IEnumerable<LinkedListNode<T>> list, T item)
        {
            var i = 0;
            foreach (var listItem in list)
            {
                if (listItem.Equals(item))
                    return i;
                ++i;
            }
            return -1;
        }

        public static int LastIndexOf<T>(this IEnumerable<LinkedListNode<T>> list, T item)
        {
            if (list is LinkedListNodeEnumerable<T> e)
            {
                var index = 0;
                foreach (var listItem in e.List.Nodes(true))
                {
                    if (listItem.Equals(item))
                        return e.List.Count - index;
                    ++index;
                }
            }
            else
            {
                var count = 0;
                var found = -1;
                foreach (var listItem in list)
                {
                    if (listItem.Equals(item))
                        found = count;
                    ++count;
                }
                if (found >= 0)
                    return found;
            }
            return -1;
        }
    }
}
