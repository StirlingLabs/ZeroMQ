using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ZeroMQ;
using ZeroMQ.lib;

#if !DEBUG
#warning ConcurrentUdpPeerToPeer DOES NOT WORK, NOT FINISHED
#endif

namespace Examples
{
    internal static partial class Program
    {
        private static long peer1ToPeer2Sent;
        private static long peer1FromPeer2Received;
        private static long peer2ToPeer1Sent;
        private static long peer2FromPeer1Received;
        public static unsafe void ConcurrentUdpPeerToPeer()
        {
            Info($"Starting up ConcurrentUdpPeerToPeer, running for 5000ms.");
            Info("DOES NOT WORK, NOT FINISHED");
            var port1 = GetFreeEphemeralTcpPort();
            var port2 = GetFreeEphemeralTcpPort();
            var endpoint1 = "udp://127.0.0.1:" + port1;
            var endpoint2 = "udp://127.0.0.1:" + port2;

            CancellationTokenSource ctsPeer1 = new();
            var peer1 = new Thread(() => {
                BlockingCollection<(string Endpoint, ZFrame Frame)> outbound = new();
                BlockingCollection<ZFrame> inbound = new();

                var radioThread = new Thread(() => {
                    using var radio = new ZSocket(ZSocketType.RADIO)
                    {
                        IPv4Only = false,
                        Name = "Peer #1 Radio",
                        Immediate = true,
                        Backlog = 100,
                        ReceiveHighWatermark = 400_000,
                        SendHighWatermark = 400_000,
                        ReceiveBufferSize = 400_000 * 256,
                        SendBufferSize = 400_000 * 256,
                    };
                    //radio.SetOption(ZSocketOption.BINDTODEVICE, endpoint1);
                    foreach (var msg in outbound.GetConsumingEnumerable())
                    {
                        if (msg.Endpoint == null)
                            throw new NotImplementedException("Missing message endpoint to send");
                        if (msg.Frame == null)
                            throw new NotImplementedException("Missing message frame to send");

                        radio.Connect(msg.Endpoint);
                        radio.SendFrame(msg.Frame, out var error);
                        Interlocked.Increment(ref peer1ToPeer2Sent);
                        if (error != ZError.None)
                            throw new NotImplementedException("Unexpected error", new ZException(error));
                        radio.Disconnect(msg.Endpoint);
                    }
                })
                {
                    Name = "Peer #1 Radio"
                };

                var dishThread = new Thread(() => {
                    using var dish = new ZSocket(ZSocketType.DISH)
                    {
                        IPv4Only = false,
                        Name = "Peer #1 Dish",
                        Immediate = true,
                        Backlog = 100,
                        ReceiveHighWatermark = 400_000,
                        SendHighWatermark = 400_000,
                        ReceiveBufferSize = 400_000 * 256,
                        SendBufferSize = 400_000 * 256,
                    };
                    dish.Bind(endpoint1);
                    while (!ctsPeer1.IsCancellationRequested)
                    {
                        var msg = dish.ReceiveFrame(ZSocketFlags.DontWait, out var error);
                        if (error == ZError.EAGAIN)
                        {
                            Thread.Yield();
                            continue;
                        }
                        if (msg == null)
                            throw new NotImplementedException("Unexpected error", new ZException(error));

                        Interlocked.Increment(ref peer1FromPeer2Received);
                        inbound.Add(msg, ctsPeer1.Token);
                    }
                })
                {
                    Name = "Peer #1 Dish"
                };

                ctsPeer1.Token.Register(() => {
                    inbound.CompleteAdding();
                    outbound.CompleteAdding();
                });

                radioThread.Start();
                dishThread.Start();

                foreach (var msg in inbound.GetConsumingEnumerable())
                {
                    var b = (byte*)msg.DataPtr();
                    var l = zmq.GetByteStringLength(b, 255);
                    var endpoint = new string((sbyte*)b, 0, l, Encoding.UTF8);

                    var reply = ZFrame.Create("ok");
                    outbound.Add((endpoint, reply), ctsPeer1.Token);
                }

            })
            {
                Name = "Peer #1"
            };

            CancellationTokenSource ctsPeer2 = new();
            var peer2 = new Thread(() => {
                BlockingCollection<(string Endpoint, ZFrame Frame)> outbound = new();
                BlockingCollection<ZFrame> inbound = new();

                var radioThread = new Thread(() => {
                    using var radio = new ZSocket(ZSocketType.RADIO)
                    {
                        IPv4Only = false,
                        Name = "Peer #2 Radio",
                        Immediate = true,
                        Backlog = 100,
                        ReceiveHighWatermark = 400_000,
                        SendHighWatermark = 400_000,
                        ReceiveBufferSize = 400_000 * 256,
                        SendBufferSize = 400_000 * 256,

                    };
                    //radio.SetOption(ZSocketOption.BINDTODEVICE, endpoint2);
                    foreach (var msg in outbound.GetConsumingEnumerable())
                    {
                        if (msg.Endpoint == null)
                            throw new NotImplementedException("Missing message endpoint to send");
                        if (msg.Frame == null)
                            throw new NotImplementedException("Missing message frame to send");

                        radio.Connect(msg.Endpoint);
                        radio.SendFrame(msg.Frame, out var error);
                        Interlocked.Increment(ref peer2ToPeer1Sent);
                        if (error != ZError.None)
                            throw new NotImplementedException("Unexpected error", new ZException(error));
                        radio.Disconnect(msg.Endpoint);
                    }
                })
                {
                    Name = "Peer #2 Radio"
                };

                var dishThread = new Thread(() => {
                    using var dish = new ZSocket(ZSocketType.DISH)
                    {
                        IPv4Only = false,
                        Name = "Peer #2 Dish",
                        Immediate = true,
                        Backlog = 100,
                        ReceiveHighWatermark = 400_000,
                        SendHighWatermark = 400_000,
                        ReceiveBufferSize = 400_000 * 256,
                        SendBufferSize = 400_000 * 256,
                    };
                    dish.Bind(endpoint2);
                    while (!ctsPeer2.IsCancellationRequested)
                    {
                        var msg = dish.ReceiveFrame(ZSocketFlags.DontWait, out var error);
                        if (error == ZError.EAGAIN)
                        {
                            Thread.Yield();
                            continue;
                        }
                        if (msg == null)
                            throw new NotImplementedException("Unexpected error", new ZException(error));

                        Interlocked.Increment(ref peer2FromPeer1Received);
                        inbound.Add(msg, ctsPeer2.Token);
                    }
                })
                {
                    Name = "Peer #2 Dish"
                };

                ctsPeer2.Token.Register(() => {
                    inbound.CompleteAdding();
                    outbound.CompleteAdding();
                });

                radioThread.Start();
                dishThread.Start();

                outbound.Add((endpoint1, ZFrame.Create("hello")), ctsPeer2.Token);

                foreach (var msg in inbound.GetConsumingEnumerable())
                {
                    var b = (byte*)msg.DataPtr();
                    var l = zmq.GetByteStringLength(b, 255);
                    var endpoint = new string((sbyte*)b, 0, l, Encoding.UTF8);

                    var s = msg.ReadString();

                    Debug.Assert(s == "ok");

                    var newMsg = ZFrame.Create("hello");
                    outbound.Add((endpoint, newMsg), ctsPeer1.Token);
                }

            })
            {
                Name = "Peer #2"
            };

            new Thread(() => {
                var lastUpdate = Stopwatch.GetTimestamp();
                do
                {
                    var now = Stopwatch.GetTimestamp();
                    if (now - lastUpdate > Stopwatch.Frequency)
                    {
                        Info(
                            $"ConcurrentUdpPeerToPeer: {peer1ToPeer2Sent} 1→2, {peer1FromPeer2Received} 1←2, {peer2ToPeer1Sent} 2→1, {peer2FromPeer1Received} 2←1.");
                        lastUpdate = now;
                    }
                    Thread.Sleep(10);
                } while (!ctsPeer1.IsCancellationRequested || !ctsPeer2.IsCancellationRequested);
            }) { IsBackground = true }.Start();

            peer1.Start();
            peer2.Start();

            ctsPeer1.CancelAfter(5000);
            ctsPeer2.CancelAfter(5000);

            peer1.Join();
            peer2.Join();

            Info($"Finished. {peer1ToPeer2Sent} 1→2, {peer1FromPeer2Received} 1←2, {peer2ToPeer1Sent} 2→1, {peer2FromPeer1Received} 2←1.");

        }
    }
}
