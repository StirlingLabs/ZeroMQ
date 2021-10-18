using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    public enum ZPollState : short
    {
        Ready = 0,
        Waiting = 1,
        Disposed = 2
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Any runtime that does custom thread management (e.g. MS SQL Server)
    /// will need to use <see cref="Thread.BeginThreadAffinity"/>.
    /// </remarks>
    [PublicAPI]
    public sealed partial class ZPoll : IDisposable, IDictionary<ZSocket, ZPollEventTypes>, IEquatable<ZPoll>
    {
        private readonly object _lock = new();

        private IntPtr _poller;

        private ZPollEventTypes _events;

        private ZPollState _state;

        private int _registered;

        private static readonly long StopwatchMillisecondFrequency = Stopwatch.Frequency / 1000;
        private static readonly long LessThanStopwatchMillisecondFrequency = StopwatchMillisecondFrequency - 1;

        private IntPtr Poll
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.CompareExchange(ref _poller, default, default);
        }

        public ZPoll()
        {
            _poller = zmq.poller_new();
            if (_poller == default)
                throw new ZException(ZError.GetLastError());
        }

        public bool Add(ZSocket socket, ZPollEventTypes eventTypes, IntPtr userData = default)
        {
            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

                Interlocked.Increment(ref _registered);
                while (-1 == zmq.poller_add(_poller, socket.SocketPtr, userData, eventTypes))
                {
                    Interlocked.Decrement(ref _registered);

                    var error = ZError.GetLastError();

                    if (error == ZError.None)
                        return false;

                    if (error != ZError.EINTR)
                        throw new ZException(error);

                    Interlocked.Increment(ref _registered);
                }

                return true;
            }
        }

        public bool Modify(ZSocket socket, ZPollEventTypes eventTypes)
        {
            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

                while (-1 == zmq.poller_modify(_poller, socket.SocketPtr, eventTypes))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.None)
                        return false;

                    if (error == ZError.EINTR) continue;

                    throw new ZException(error);
                }

                return true;
            }
        }

        void IDictionary<ZSocket, ZPollEventTypes>.Add(ZSocket key, ZPollEventTypes value)
            => throw new NotImplementedException();

        bool IDictionary<ZSocket, ZPollEventTypes>.ContainsKey(ZSocket key)
            => throw new NotImplementedException();

        bool IDictionary<ZSocket, ZPollEventTypes>.Remove(ZSocket key)
            => throw new NotImplementedException();

        bool IDictionary<ZSocket, ZPollEventTypes>.TryGetValue(ZSocket key, out ZPollEventTypes value)
            => throw new NotImplementedException();

        ZPollEventTypes IDictionary<ZSocket, ZPollEventTypes>.this[ZSocket key]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        ICollection<ZSocket> IDictionary<ZSocket, ZPollEventTypes>.Keys => throw new NotImplementedException();

        ICollection<ZPollEventTypes> IDictionary<ZSocket, ZPollEventTypes>.Values => throw new NotImplementedException();

        public bool Remove(ZSocket socket)
        {
            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

                while (-1 == zmq.poller_remove(_poller, socket.SocketPtr))
                {
                    var error = ZError.GetLastError();

                    if (error == ZError.None)
                        return false;

                    if (error == ZError.EINTR)
                        continue;

                    throw new ZException(error);
                }

                Interlocked.Decrement(ref _registered);

                return true;
            }
        }

        public unsafe bool WaitForEvent(out Event @event, long timeout = Timeout.Infinite)
        {
            @event = default;

            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

                var ts = Stopwatch.GetTimestamp();

                fixed (void* pEvent = &@event)
                {
                    while (-1 == zmq.poller_wait(_poller, (IntPtr)pEvent, timeout))
                    {
                        var error = ZError.GetLastError();

                        // timeout reached, no events
                        if (error == ZError.EAGAIN)
                            return false;

                        if (error != ZError.EINTR)
                            throw new ZException(error);

                        if (timeout == Timeout.Infinite)
                            continue;

                        timeout -= (Stopwatch.GetTimestamp() - ts
                                + LessThanStopwatchMillisecondFrequency)
                            / StopwatchMillisecondFrequency;

                        if (timeout > 0)
                            continue;

                        return false;
                    }
                }

                return true;
            }
        }

        public unsafe bool WaitForEvents(ref ArraySegment<Event> events, long timeout = Timeout.Infinite)
        {

            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

#if NET5_0_OR_GREATER
                var registered = Interlocked.Or(ref _registered, 0);
#else
                var registered = Interlocked.Add(ref _registered, 0);
#endif
                var eventsArray = events.Array;

                if (eventsArray is null || eventsArray.Length < registered)
                    eventsArray = new Event[registered];

                var ts = Stopwatch.GetTimestamp();

                int count;

                fixed (void* pEvent = eventsArray)
                {
                    while (-1 == (count = zmq.poller_wait_all(_poller, (IntPtr)pEvent, registered, timeout)))
                    {
                        var error = ZError.GetLastError();

                        // timeout reached, no events
                        if (error == ZError.EAGAIN)
                            return false;

                        if (error != ZError.EINTR)
                            throw new ZException(error);

                        if (timeout == Timeout.Infinite)
                            continue;

                        timeout -= (Stopwatch.GetTimestamp() - ts
                                + LessThanStopwatchMillisecondFrequency)
                            / StopwatchMillisecondFrequency;

                        if (timeout > 0)
                            continue;

                        return default;
                    }
                }

                events = new(eventsArray, 0, count);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool TryWait(out Event @event, long timeout = 1)
        {
            lock (_lock)
            {
                if (_poller == default)
                    throw new ObjectDisposedException(nameof(ZPoll));

                bool success;
                fixed (void* p = &@event._event)
                    success = -1 != zmq.poller_wait(_poller, (IntPtr)p, timeout);
                return success;
            }
        }

        public IEnumerable<Event> EventLoop(bool yielding = true, CancellationToken cancellationToken = default)
        {

            for (;;)
            {

                if (TryWait(out var @event))
                {
                    yield return @event;
                    continue;
                }

                var error = ZError.GetLastError();

                if (error != null && error != ZError.EAGAIN && error != ZError.EINTR)
                    throw new ZException(error);

                if (cancellationToken.IsCancellationRequested)
                    yield break;

                // interrupted or timeout reached, no events
                if (yielding)
                    Thread.Yield();
            }
        }

        public async IAsyncEnumerable<Event> EventLoopAsync(bool yielding = true,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (;;)
            {
                if (TryWait(out var @event))
                {
                    yield return @event;
                    continue;
                }

                var error = ZError.GetLastError();

                if (error != null && error != ZError.EAGAIN && error != ZError.EINTR)
                    throw new ZException(error);

                if (cancellationToken.IsCancellationRequested)
                    yield break;

                // interrupted or timeout reached, no events
                if (yielding)
                    await Task.Yield();
            }
        }

        public unsafe void Dispose()
        {
            lock (_lock)
            {
                var poll = Interlocked.Exchange(ref _poller, default);

                if (poll == default) return;

                if (-1 == zmq.poller_destroy(&poll))
                    throw new ZException(ZError.GetLastError());

                Debug.Assert(poll == default);
            }
        }

        void ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.Add(KeyValuePair<ZSocket, ZPollEventTypes> item)
            => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.Remove(KeyValuePair<ZSocket, ZPollEventTypes> item)
            => Remove(item.Key);

        int ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.Count
        {
            get {
#if NET5_0_OR_GREATER
                return Interlocked.Or(ref _registered, 0);
#else
                return Interlocked.Add(ref _registered, 0);
#endif
            }
        }

        bool ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.IsReadOnly
            => false;

        IEnumerator<KeyValuePair<ZSocket, ZPollEventTypes>> IEnumerable<KeyValuePair<ZSocket, ZPollEventTypes>>.GetEnumerator()
            => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator()
            => throw new NotSupportedException();
        void ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.Clear()
            => throw new NotSupportedException();
        bool ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.Contains(KeyValuePair<ZSocket, ZPollEventTypes> item)
            => throw new NotSupportedException();
        void ICollection<KeyValuePair<ZSocket, ZPollEventTypes>>.CopyTo(KeyValuePair<ZSocket, ZPollEventTypes>[] array, int arrayIndex)
            => throw new NotSupportedException();


        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        public bool Equals(ZPoll? other)
            => other is not null
                && (ReferenceEquals(this, other)
                    || _poller == other._poller);

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
            => ReferenceEquals(this, obj)
                || obj is ZPoll other && Equals(other);

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        public override int GetHashCode()
            => _poller.GetHashCode();

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ZPoll? left, ZPoll? right)
            => left is not null && left.Equals(right)
                || left is null && right is null;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ZPoll? left, ZPoll? right)
            => !(left == right);
    }
}
