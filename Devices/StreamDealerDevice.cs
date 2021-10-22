using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ZeroMQ.lib;

namespace ZeroMQ.Devices
{
    // using lib.sys;

    /// <summary>
    /// The Stream to Dealer is a Device for reading 
    /// and sending REPlies to TCP
    /// </summary>
    public class StreamDealerDevice : ZDevice
    {
        /// <summary>
        /// The frontend <see cref="ZSocketType"/> for a queue device.
        /// </summary>
        public static readonly ZSocketType FrontendType = ZSocketType.STREAM;

        /// <summary>
        /// The backend <see cref="ZSocketType"/> for a queue device.
        /// </summary>
        public static readonly ZSocketType BackendType = ZSocketType.DEALER;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamDealerDevice"/> class.
        /// </summary>
        public StreamDealerDevice() : this(ZContext.Current) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamDealerDevice"/> class.
        /// </summary>
        public StreamDealerDevice(ZContext context)
            : base(context, FrontendType, BackendType) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamDealerDevice"/> class.
        /// </summary>
        public StreamDealerDevice(string frontendBindAddr, string backendBindAddr)
            : this(ZContext.Current, frontendBindAddr, backendBindAddr) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamDealerDevice"/> class.
        /// </summary>
        public StreamDealerDevice(ZContext context, string frontendBindAddr, string backendBindAddr)
            : base(context, FrontendType, BackendType)
        {
            FrontendSetup.Bind(frontendBindAddr);
            BackendSetup.Bind(backendBindAddr);
        }

        /// <summary>
        /// Forwards requests from the frontend socket to the backend socket.
        /// </summary>
        protected override bool FrontendHandler(ZSocket sock, out ZMessage? message, out ZError? error)
        {
            error = default;
            message = null;

            // receiving scope
            // STREAM: get 2 frames, identity and body
            // IPAddress address = null;
            if (!ReceiveMsg(sock, out var incoming, out var address, out error) || incoming == null)
                return false;

            // sending scope
            // DEALER: forward
            using (incoming)
            {
                if (incoming[1].Length == 0)
                    return true; // Ignore the Empty one

                // Prepend empty delimiter between Identity frame and Data frame
                incoming.Insert(1, ZFrame.Create());

                // Prepend Peer-Address
                incoming.Insert(2, ZFrame.Create(address));

                if (!BackendSocket.Send(incoming, /* ZSocketFlags.DontWait, */ out error))
                    return false;

                incoming.Dispose();

                return true;
            }
        }

        static bool ReceiveMsg(ZSocket sock, out ZMessage? message, out string address, out ZError? error, bool _ = false)
        {
            message = ZMessage.Create();
            return ReceiveMsg(sock, ref message, out address, out error);
        }

        static bool ReceiveMsg(ZSocket sock, ref ZMessage? message, out string address, out ZError? error)
        {
            error = ZError.None;

            string? addressStr = null;

            // STREAM: read frames: identity, body

            // read the ip4 address from (ZFrame)frame.GetOption("Peer-Address")

            var receiveCount = 2;
            do
            {
                var frame = ZFrame.CreateEmpty();

                if (frame.IsDismissed) throw new InvalidOperationException("Message was dismissed.");

                if (!frame.Receive(sock, out error))
                {
                    address = "";
                    return false;
                }

                message ??= ZMessage.Create();
                message.Add(frame);

                if (receiveCount != 2)
                    continue;

                var metadata = frame.GetMetadata("Peer-Address", out error);
                if (null != (addressStr = zmq.ReadUtf8String(metadata)))
                    continue;

                // just ignore
                error = default;
                address = string.Empty;

            } while (--receiveCount > 0);

            address = addressStr ?? "";

            return true;
        }


        /// <summary>
        /// Forwards replies from the backend socket to the frontend socket.
        /// </summary>
        protected override bool BackendHandler(ZSocket sock, out ZMessage? message, out ZError? error)
        {
            error = default;
            message = null;

            // receiving scope
            // DEALER: normal movemsg
            if (!sock.ReceiveMessage(out var incoming, /* ZSocketFlags.DontWait */ ZSocketFlags.None, out error) || incoming is null)
                return false;

            using (incoming)
            {
                // STREAM: write frames: identity, body, identity, empty
                // Read identity
                var ic = (int)incoming[0].Length;
                var identityBytes = new byte[ic];
                incoming[0].Read(identityBytes, 0, ic);

                // Remove DEALER's delimiter
                incoming.RemoveAt(1);

                // Append Identity frame
                var identity0 = ZFrame.Create(identityBytes);
                incoming.Add(identity0);

                // Append STREAM's empty delimiter frame
                incoming.Add(ZFrame.Create());

                if (!SendMsg(FrontendSocket, incoming, out error))
                    return false;
            }

            return true;
        }

        static bool SendMsg(ZSocket sock, ZMessage? message, out ZError? error)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            error = ZError.None;

            foreach (var frame in message)
            {
                if (frame.IsDismissed) throw new InvalidOperationException("Message was dismissed.");

                if (!frame.Send(sock, out error))
                    return false;
            }

            message.Dispose();
            return true;
        }
    }
}
