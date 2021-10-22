using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace Examples
{
    internal static partial class Program
    {
        private static int _lastIssuedFreeEphemeralTcpPort = -1;
        private static int GetFreeEphemeralTcpPort()
        {
            bool IsFree(int realPort)
            {
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] listeners = properties.GetActiveTcpListeners();
                int[] openPorts = listeners.Select(item => item.Port).ToArray<int>();
                return openPorts.All(openPort => openPort != realPort);
            }

            const int ephemeralRangeSize = 16384;
            const int ephemeralRangeStart = 49152;

            var port = (_lastIssuedFreeEphemeralTcpPort + 1) % ephemeralRangeSize;

            while (!IsFree(ephemeralRangeStart + port))
                port = (port + 1) % ephemeralRangeSize;

            _lastIssuedFreeEphemeralTcpPort = port;

            return ephemeralRangeStart + port;
        }
        public static Task Receive(ZSocket socket, SemaphoreSlim handoffSignal)
        {
            ZFrame? frame;
            try
            {
                // ReSharper disable once AccessToDisposedClosure
                frame = socket.ReceiveFrame(ZSocketFlags.DontWait);
            }
            finally
            {
                handoffSignal.Release();
            }

            if (frame == null)
                Debug.Fail("No message frame received.");

            using (frame)
            {
                if (frame.ReadString() != "hello")
                    Debug.Fail("Did not receive the expected \"hello\" message.");
            }

            return Task.CompletedTask;
        }

        private static long serverToClientSent;
        private static long serverFromClientReceived;
        private static long clientToServerSent;
        private static long clientFromServerReceived;

        private static string traceServerState;

        public static void ThreadedClientServer(string[] args)
        {
            using var zap = new ZapClient();

            zap.AuthenticatePlain = (
                version, sequence,
                domain, address, identity,
                name, password
            ) => {
                return name switch
                {
                    "alice" when password != "" => new(200, null, "alice") { Metadata = { { "Example", "Metadata" } } },
                    "bob" when password != "" => new(200, null, "bob") { Metadata = { { "Example", "Metadata" } } },
                    "charlie" when password != "" => new(200, null, "charlie") { Metadata = { { "Example", "Metadata" } } },
                    "dave" when password != "" => new(200, null, "dave") { Metadata = { { "Example", "Metadata" } } },
                    "eve" when password != "" => new(200, null, "eve") { Metadata = { { "Example", "Metadata" } } },
                    _ => new(400)
                };
            };

            var helloMsgBytes = new Memory<byte>(Encoding.UTF8.GetBytes("hello"));
            var howdyMsgBytes = new Memory<byte>(Encoding.UTF8.GetBytes("howdy"));

            var timeoutMs = args.Length >= 1 && int.TryParse(args[0], out var timeout) ? timeout : 5000;
            var cts = new CancellationTokenSource(timeoutMs);
            Info($"Starting up ThreadedClientServer, running for {timeoutMs}ms.");
            cts.Token.Register(() => {
                Info(
                    $"Shutting down ThreadedClientServer. {clientToServerSent} C→S, {clientFromServerReceived} C←S, {serverToClientSent} S→C, {serverFromClientReceived} S←C.");
            });

            new Thread(() => {
                var lastUpdate = Stopwatch.GetTimestamp();
                do
                {
                    var now = Stopwatch.GetTimestamp();
                    if (now - lastUpdate > Stopwatch.Frequency)
                    {
                        Info(
                            $"ThreadedClientServer: {clientToServerSent} C→S, {clientFromServerReceived} C←S, {serverToClientSent} S→C, {serverFromClientReceived} S←C.");
                        lastUpdate = now;
                    }
                    Thread.Sleep(10);
                } while (!cts.IsCancellationRequested);
            }) { IsBackground = true }.Start();

            var freePort = GetFreeEphemeralTcpPort();

            var serverThread = new Thread(() => {
                //using var serverCtx = new ZContext();
                using var serverSocket = new ZSocket(ZSocketType.SERVER)
                {
                    Name = "Server",
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true,
                    Backlog = 100,
                    ReceiveHighWatermark = 400_000,
                    SendHighWatermark = 400_000,
                    ReceiveBufferSize = 400_000 * 256,
                    SendBufferSize = 400_000 * 256,
                    ZapDomain = "Server",
                    ZapEnforceDomain = true,
                    PlainServer = true
                };

                serverSocket.Bind($"tcp://[::1]:{freePort}");

                using var serverPoll = new ZPoll
                {
                    { serverSocket, ZPollEventTypes.In }
                };

                using var handoffSignal = new SemaphoreSlim(0, 1);
                foreach (var _ in serverPoll.EventLoop(true, cts.Token))
                {
                    if (cts.IsCancellationRequested)
                    {
                        Info("Server received shutdown signal. (1)");
                        break;
                    }

#if NETSTANDARD2_0
                    if (!ThreadPool.QueueUserWorkItem(x => {
                        var o = ((
                            ZSocket serverSocket,
                            SemaphoreSlim handoffSignal,
                            CancellationToken ct
                            ))x;
#else
                    if (!ThreadPool.QueueUserWorkItem(o => {
#endif
                        ZFrame? req;
                        try
                        {
                            // ReSharper disable once AccessToDisposedClosure
#if TRACE
                            traceServerState = "Receive Request";
#endif
                            req = o.serverSocket.ReceiveFrame(ZSocketFlags.DontWait);
                        }
                        finally
                        {
#if TRACE
                            traceServerState = "Handoff Release";
#endif
                            o.handoffSignal.Release();
                            //o.handoffSignal.Dispose();
                        }
                        Interlocked.Increment(ref serverFromClientReceived);

                        if (req is null)
                            Debug.Fail("No request frame received.");

                        if (o.ct.IsCancellationRequested)
                        {
                            Info("Server received shutdown signal. (2)");
                            return;
                        }

                        uint rid;

#if TRACE
                        traceServerState = "Process Request";
#endif
                        using (req)
                        {
                            var str = req!.ReadString();
                            if (str != "hello")
                                Debug.Fail("Did not receive the expected \"hello\" request.");

                            if (!req.TryGetRoutingId(out rid))
                                Debug.Fail("Did not retrieve the routing ID for request.");

                            ZError error;
                            //var routingId = req.GetMetadata("Routing-Id", out error); // does not work
                            //var identity = req.GetMetadata("Identity", out error); // does not work
                            var socketType = req.GetMetadata("Socket-Type", out error); // works, "CLIENT"
                            var userId = req.GetMetadata("User-Id", out error); // works, "alice"
                            var peerAddress = req.GetMetadata("Peer-Address", out error); // works "::1"
                            var exampleMetadata = req.GetMetadata("Example", out error); // works
                        }

#if TRACE
                        traceServerState = "Create Response";
#endif
                        var rsp = ZFrame.Create(howdyMsgBytes);
                        if (!rsp.TrySetRoutingId(rid))
                            Debug.Fail("Did not set the routing ID for response.");

#if TRACE
                        traceServerState = "Send Response";
#endif
                        o.serverSocket.SendFrame(rsp);
                        Interlocked.Increment(ref serverToClientSent);
#if NETSTANDARD2_0
                    }, (serverSocket, handoffSignal, ct: cts.Token)))
#else
                    }, (serverSocket, handoffSignal, ct: cts.Token), true))
#endif
                        Debug.Fail("Was not able to queue worker.");

                    try
                    {
                        handoffSignal.Wait(cts.Token);
                    }
                    catch (OperationCanceledException oce) when (oce.CancellationToken == cts.Token && cts.Token.IsCancellationRequested)
                    {
                        // ok
                    }
                }

            }) { Name = "Server", Priority = ThreadPriority.AboveNormal };

            serverThread.Start();

            void ClientLoop(object? o)
            {
                var clientNumber = (int)o!;
                //using var clientCtx = new ZContext();
                using var clientSocket = new ZSocket(ZSocketType.CLIENT)
                {
                    Name = "Client",
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true,
                    Backlog = 100,
                    ReceiveHighWatermark = 400_000,
                    SendHighWatermark = 400_000,
                    ReceiveBufferSize = 400_000 * 256,
                    SendBufferSize = 400_000 * 256,
                    PlainUserName = clientNumber switch
                    {
                        0 => "alice",
                        1 => "bob",
                        2 => "charlie",
                        3 => "dave",
                        4 => "eve",
                        _ => throw new NotImplementedException()
                    },
                    PlainPassword = "testing"
                };

                clientSocket.Connect($"tcp://[::1]:{freePort}");

                for (;;)
                {
                    try
                    {
                        if (cts.IsCancellationRequested)
                        {
                            Info("Client received shutdown signal. (1)");
                            return;
                        }
                        using (var req = ZFrame.Create(helloMsgBytes))
                        {

                            clientSocket.SendFrame(req);
                            Interlocked.Increment(ref clientToServerSent);
                        }

                        if (cts.IsCancellationRequested)
                        {
                            Info("Client received shutdown signal. (2)");
                            return;
                        }

                        ZFrame? rsp;

                        for (;;)
                        {
                            rsp = clientSocket.ReceiveFrame(out var error);
                            if (error is null)
                                break;

                            if (error == ZError.EAGAIN && cts.IsCancellationRequested)
                                return;

                            Debug.Fail($"Received unexpected error {error}");

                        }
                        using (rsp)
                        {
                            if (rsp is null)
                                Debug.Fail("Did not receive the expected response.");

                            if (rsp!.ReadString() != "howdy")
                                Debug.Fail("Did not receive the expected \"howdy\" response.");

                            Interlocked.Increment(ref clientFromServerReceived);
                        }
                    }
                    catch (ZException ex) when (ex.Error == ZError.EAGAIN && cts.IsCancellationRequested)
                    {
                        // ok
                    }
                    catch (Exception ex)
                    {
                        Info($"Exception in client loop, {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }

            const int clientCount = 5;
            var clientThreads = new Thread[clientCount];
            for (var i = 0; i < clientCount; ++i)
            {
                var clientThread = new Thread(ClientLoop);
                clientThreads[i] = clientThread;
                clientThread.Start(i);
            }

            while (!cts.IsCancellationRequested)
                Thread.Sleep(1);

            Info(
                $"Finished. {clientToServerSent} C→S, {clientFromServerReceived} C←S, {serverToClientSent} S→C, {serverFromClientReceived} S←C.");

            foreach (var clientThread in clientThreads)
                clientThread.Join(100);
            serverThread.Join(100);

        }
    }
}
