using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace ZeroMQ
{
    /// <summary>
    /// A single or multi-part message, sent or received via a <see cref="ZSocket"/>.
    /// </summary>
    public class ZMessage : IList<ZFrame>, ICloneable, IDisposable
    {
        private static readonly ConcurrentStack<ZMessage> FreeMessages = new();
        private static readonly ConcurrentStack<LinkedList<ZFrame>> FreeLists = new();
        private static readonly ConcurrentStack<LinkedListNode<ZFrame>> FreeNodes = new();

        public static void FlushObjectCaches()
        {
            FreeMessages.Clear();
            FreeLists.Clear();
            FreeNodes.Clear();
        }

        private readonly object _lock = new();
        private LinkedList<ZFrame>? _frames;

        private ZMessage() => Reset();

        private void Reset()
        {
            lock (_lock)
                _frames = FreeLists.TryPop(out var list) ? list : new();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZMessage"/> class.
        /// Creates an empty message.
        /// </summary>
        public static ZMessage Create()
        {
            if (!FreeMessages.TryPop(out var zm))
                return new();

            zm.Reset();
            return zm;

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZMessage"/> class.
        /// Creates a message that contains the given <see cref="ZFrame"/> objects.
        /// </summary>
        /// <param name="frames">A collection of <see cref="ZFrame"/> objects to be stored by this <see cref="ZMessage"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="frames"/> is null.</exception>
        public static ZMessage Create(IEnumerable<ZFrame> frames)
        {
            if (frames is null) throw new ArgumentNullException(nameof(frames));
            var zm = Create();
            zm.AddRange(frames);
            return zm;
        }

        ~ZMessage()
            => Dispose(true);

        // ReSharper disable once InconsistentlySynchronizedField
        private LinkedList<ZFrame> FramesOrBust
            => _frames ?? throw new ObjectDisposedException(nameof(ZMessage));

        public void Dispose()
            => Dispose(false);

        protected virtual void Dispose(bool final)
        {

            lock (_lock)
            {
                if (_frames != null)
                {
                    foreach (var node in _frames.Nodes())
                    {
                        var frame = node.Value;
                        frame.Dispose();

                        _frames.Remove(node);
                        FreeNodes.Push(node);
                    }
                    _frames.Clear();
                    FreeLists.Push(_frames);
                }
                _frames = null;
            }

            if (final) return;

            FreeMessages.Push(this);
        }

        public void Dismiss()
        {
            lock (_lock)
            {
                if (_frames != null)
                {
                    foreach (var node in _frames.Nodes())
                    {
                        var frame = node.Value;
                        frame.Dismiss();

                        _frames.Remove(node);
                        FreeNodes.Push(node);
                    }
                    _frames.Clear();
                    FreeLists.Push(_frames);
                }
                _frames = null;
            }
        }

        public void ReplaceAt(int index, ZFrame replacement)
            => ReplaceAt(index, replacement, true);

        public ZFrame? ReplaceAt(int index, ZFrame replacement, bool dispose)
        {
            lock (_lock)
            {
                if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

                var node = GetFrameNode(index);

                if (node == null) throw new ArgumentOutOfRangeException(nameof(index));

                ZFrame old = node.Value;
                node.Value = replacement;

                if (!dispose)
                    return old;

                old.Dispose();
                return null;
            }
        }

        #region IList implementation

        public int IndexOf(ZFrame item)
        {
            lock (_lock)
            {
                if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));
                return _frames.Nodes().IndexOf(item);
            }
        }

        public void Prepend(ZFrame item)
            => Insert(0, item);

        public void Insert(int index, ZFrame item)
        {
            LinkedListNode<ZFrame>? newNode;
            lock (_lock)
            {
                if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

                var frameCount = _frames.Count;

                if (index == 0)
                {
                    if (FreeNodes.TryPop(out newNode))
                    {
                        newNode.Value = item;
                        _frames.AddFirst(newNode);
                        return;
                    }
                    _frames.AddFirst(item);
                    return;
                }
                if (index == frameCount)
                {

                    if (FreeNodes.TryPop(out newNode))
                    {
                        newNode.Value = item;
                        _frames.AddLast(newNode);
                        return;
                    }

                    _frames.AddLast(item);
                    return;
                }

                var node = GetFrameNode(index);

                if (node == null) throw new ArgumentOutOfRangeException(nameof(index));

                if (FreeNodes.TryPop(out newNode))
                {
                    newNode.Value = item;
                    _frames.AddAfter(node, newNode);
                }

                newNode = new(item);
                _frames.AddAfter(node, newNode);
            }
        }

        /// <summary>
        /// Removes ZFrames. Note: Disposes the ZFrame.
        /// </summary>
        /// <returns>The <see cref="ZeroMQ.ZFrame"/>.</returns>
        public void RemoveAt(int index)
            // ReSharper disable once MustUseReturnValue
            => RemoveAt(index, true);

        /// <summary>
        /// Removes ZFrames.
        /// </summary>
        /// <returns>The <see cref="ZeroMQ.ZFrame"/>.</returns>
        /// <param name="dispose">If set to <c>false</c>, do not dispose the ZFrame.</param>
        [MustUseReturnValue]
        [ContractAnnotation("dispose:true => null")]
        public ZFrame? RemoveAt(int index, bool dispose)
        {
            lock (_lock)
            {
                if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

                var node = GetFrameNode(index);

                if (node == null) throw new ArgumentOutOfRangeException(nameof(index));

                var frame = node.Value;

                _frames.Remove(node);
                FreeNodes.Push(node);

                if (!dispose)
                    return frame;

                frame.Dispose();
                return null;
            }
        }

        [MustUseReturnValue]
        public ZFrame? Pop()
        {
            var result = RemoveAt(0, false);
            if (result == null)
                return null;

            result.Position = 0; // TODO maybe remove this here again, see https://github.com/zeromq/clrzmq4/issues/110
            return result;
        }

        [MustUseReturnValue]
        public int PopBytes(byte[] buffer, int offset, int count)
        {
            using var frame = Pop();
            return frame?.Read(buffer, offset, count) ?? 0;
        }

        [MustUseReturnValue]
        public bool TryPopByte(out byte value)
        {
            using var frame = Pop();
            if (frame == null)
            {
                value = default;
                return false;
            }

            var result = frame.ReadByte();
            if (result is >= 0 and <= 0xFF)
            {
                value = (byte)result;
                return true;
            }

            value = default;
            return false;
        }

        [MustUseReturnValue]
        public bool TryPop<T>(out T value) where T : unmanaged
        {
            using var frame = Pop();

            if (frame is not null)
                return frame.TryRead(out value);

            value = default;
            return false;

        }

        [MustUseReturnValue]
        public string? PopString()
            => PopString(ZContext.Encoding);

        [MustUseReturnValue]
        public string? PopString(Encoding encoding)
        {
            using var frame = Pop();
            return frame?.ReadString((nuint)frame.Length, encoding);
        }

        [MustUseReturnValue]
        public string? PopString(nuint bytesCount, Encoding encoding)
        {
            using var frame = Pop();
            return frame?.ReadString(bytesCount, encoding);
        }

        public void Wrap(ZFrame frame)
        {
            lock (_lock)
            {
                if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

                if (FreeNodes.TryPop(out var node))
                {
                    node.Value = new();
                    _frames.AddFirst(node);
                }
                else
                    _frames.AddFirst(new ZFrame());

                if (FreeNodes.TryPop(out node))
                {
                    node.Value = frame;
                    _frames.AddFirst(node);
                }
                else
                    _frames.AddFirst(frame);
            }
        }

        [MustUseReturnValue]
        public ZFrame? Unwrap()
        {
            var frame = RemoveAt(0, false);

            if (Count > 0 && this[0].Length == 0)
                RemoveAt(0);

            return frame;
        }

        public ZFrame this[int index]
        {
            [DebuggerStepThrough]
            get => IndexGetFrameNodeOrBust(index).Value;

            [DebuggerStepThrough]
            set => IndexGetFrameNodeOrBust(index).Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LinkedListNode<ZFrame> IndexGetFrameNodeOrBust(int index)
            => GetFrameNode(index) ?? throw new IndexOutOfRangeException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LinkedListNode<ZFrame> ArgumentGetFrameNodeOrBust(int index, [InvokerParameterName] string? paramName)
            => GetFrameNode(index) ?? throw new ArgumentOutOfRangeException(paramName);

        [MustUseReturnValue]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LinkedListNode<ZFrame>? GetFrameNode(int index)
        {
            lock (_lock)
            {
                var frameCount = _frames!.Count;
                if (frameCount == 0 || index < 0 || index > frameCount)
                    return null;
                LinkedListNode<ZFrame> node;
                if (index == 0)
                    node = _frames.First!;
                else if (index == frameCount - 1)
                    node = _frames.Last!;
                else if (index <= frameCount >> 1)
                    node = _frames.Nodes()
                        .Skip(index).First();
                else
                    node = _frames.Nodes(true)
                        .Skip(frameCount - index).First();
                return node;
            }
        }

        #endregion

        #region ICollection implementation

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ZFrame item)
            => Add(item);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendRange(IEnumerable<ZFrame> items)
            => AddRange(items);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ZFrame item)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

            lock (_lock)
            {
                {
                    if (FreeNodes.TryPop(out var node))
                    {
                        node.Value = item;
                        _frames.AddLast(node);
                        return;
                    }
                }

                _frames.AddLast(item);
            }
        }

        public void AddRange(IEnumerable<ZFrame> items)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));
            lock (_lock)
            {
                if (FreeNodes.Count > 0)
                    foreach (var item in items)
                    {
                        if (FreeNodes.TryPop(out var node))
                        {
                            node.Value = item;
                            _frames.AddLast(node);
                        }
                        else
                            _frames.AddLast(item);
                    }
                else
                    foreach (var item in items)
                        _frames.AddLast(item);
            }
        }

        public void Clear()
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));
            lock (_lock)
            {
                foreach (var node in _frames.Nodes())
                {
                    node.Value.Dispose();

                    _frames.Remove(node);
                    FreeNodes.Push(node);
                }
                _frames.Clear();
            }
        }

        [MustUseReturnValue]
        public bool Contains(ZFrame item)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));
            lock (_lock)
                return _frames.Contains(item);
        }

        void ICollection<ZFrame>.CopyTo(ZFrame[] array, int arrayIndex)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

            lock (_lock)
            {
                int i = 0, count = Count;
                foreach (var frame in this)
                    array[arrayIndex + i++] = frame.Clone();
            }
        }

        public void AddTo(ICollection<ZFrame> collection)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

            lock (_lock)
            {
                foreach (var frame in this)
                    collection.Add(frame.Clone());
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ZFrame item)
            => null == Remove(item, true);

        public ZFrame? Remove(ZFrame item, bool dispose)
        {
            if (_frames == null) throw new ObjectDisposedException(nameof(ZMessage));

            lock (_lock)
            {
                var node = _frames.Find(item);
                if (node is null)
                    return null;

                {
                    node.Value = null!;

                    _frames.Remove(node);
                    FreeNodes.Push(node);
                }

                if (!dispose)
                    return item;

                item.Dispose();
                return null;
            }
        }

        public int Count
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                lock (_lock)
                    return FramesOrBust.Count;
            }
        }

        bool ICollection<ZFrame>.IsReadOnly => false;

        #endregion

        #region IEnumerable implementation

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<ZFrame> GetEnumerator()
        {
            lock (_lock)
            {
                return ((IEnumerable<ZFrame>)FramesOrBust.Nodes())
                    .GetEnumerator();
                //return FramesOrBust.GetEnumerator();
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        #endregion

        #region ICloneable implementation

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MustUseReturnValue]
        object ICloneable.Clone()
            => Clone();

        [MustUseReturnValue]
        public ZMessage Clone()
        {
            lock (_lock)
            {
                var message = Create();
                foreach (var frame in this)
                    message.Add(frame.Clone());
                return message;
            }
        }

        #endregion
    }
}
