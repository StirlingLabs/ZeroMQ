using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using ZeroMQ.lib;

namespace ZeroMQ
{
    public sealed partial class ZPoll
    {
        [PublicAPI]
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Event
        {
            internal readonly zmq.poller_event_t _event;

            internal Event(zmq.poller_event_t @event) => _event = @event;

            public ZSocket? Socket
                => ZSocket.Active.TryGetValue(_event.socket, out var wr)
                    && wr.TryGetTarget(out var s)
                        ? s
                        : throw new ObjectDisposedException(nameof(Socket));

            public ZPollEventTypes EventTypes => _event.events;
            public IntPtr UserData => _event.user_data;
        }
    }
}
