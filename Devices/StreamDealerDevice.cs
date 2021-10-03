using System;
using System.Buffers;
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
                incoming.Insert(1, new());

                // Prepend Peer-Address
                incoming.Insert(2, new(address));

                if (!BackendSocket.Send(incoming, /* ZSocketFlags.DontWait, */ out error))
                    return false;

                incoming.Dismiss();

                return true;
            }
        }

        static bool ReceiveMsg(ZSocket sock, out ZMessage? message, out string address, out ZError? error, bool _ = false)
        {
            message = new ZMessage();
            return ReceiveMsg(sock, ref message, out address, out error);
        }

        static bool ReceiveMsg(ZSocket sock, ref ZMessage? message, out string address, out ZError? error)
        {
            error = ZError.None;
            // address = IPAddress.None;
            address = string.Empty;

            // STREAM: read frames: identity, body

            // read the ip4 address from (ZFrame)frame.GetOption("Peer-Address")

            var receiveCount = 2;
            do
            {
                var frame = ZFrame.CreateEmpty();

                while (-1 == zmq.msg_recv(frame.Ptr, sock.SocketPtr, ZSocketFlags.More))
                {
                    error = ZError.GetLastErr();

                    if (error != ZError.EINTR)
                    {
                        frame.Dispose();
                        return false;
                    }

                    error = default;
                }

                message ??= new();
                message.Add(frame);

                if (receiveCount != 2)
                    continue;

                if (default != (address = frame.GetOption("Peer-Address", out error)!))
                    continue;

                // just ignore
                error = default;
                address = string.Empty;

            } while (--receiveCount > 0);

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
                var identity0 = new ZFrame(identityBytes);
                incoming.Add(identity0);

                // Append STREAM's empty delimiter frame
                incoming.Add(new());

                if (!SendMsg(FrontendSocket, incoming, out error))
                    return false;
            }

            return true;
        }

        static bool SendMsg(ZSocket sock, ZMessage? msg, out ZError? error)
        {
            if (msg is null) throw new ArgumentNullException(nameof(msg));

            error = ZError.None;

            foreach (var frame in msg)
            {
                while (-1 == zmq.msg_send(frame.Ptr, sock.SocketPtr, ZSocketFlags.More))
                {
                    error = ZError.GetLastErr();

                    if (error != ZError.EINTR)
                        return false;
                    error = default;
                    /* if (error == ZError.EAGAIN)
                    {
                      error = default(ZError);
                      Thread.Sleep(1);
          
                      continue;
                    } */

                }
            }

            msg.Dismiss();
            return true;
        }
    }
}
