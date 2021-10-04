using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

// using System.Runtime.Remoting.Messaging;

namespace Examples
{
    internal static partial class Program
    {
        //  Round-trip demonstrator
        //  While this example runs in a single process, that is just to make
        //  it easier to start and stop the example. The client task signals to
        //  main when it's ready.

        private const string TcpClientEndpoint = "tcp://[::1]:5555";
        private const string TcpServerEndpoint = "tcp://[::1]:5556";

        private const string IpcClientEndpoint = "ipc://z-guided-example-client";
        private const string IpcServerEndpoint = "ipc://z-guided-example-server";

        private static int EndpointState = 0;

        private static string ClientEndpoint = EndpointState == 0
            ? TcpClientEndpoint
            : IpcClientEndpoint;
        private static string ServerEndpoint = EndpointState == 0
            ? TcpServerEndpoint
            : IpcServerEndpoint;

        private static bool wantsToRestart;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool WantsToRestart() => Volatile.Read(ref wantsToRestart);

        public static void Tripping(string[] args)
        {
            var canceller = new CancellationTokenSource();
            Console.CancelKeyPress += (s, ea) => {
                ea.Cancel = true;
                canceller.Cancel();
            };

            ZContext ctx;
            ZActor client;
            using (ctx = new())
            using (client = new(ctx, Tripping_ClientTask))
            {
                ConsoleKeyPressed += key => {
                    switch (key.Key)
                    {
                        case ConsoleKey.M:
                            ZMessage.IsCachingEnabled = !ZMessage.IsCachingEnabled;
                            Console.WriteLine($"[M] ZMessage's Object Caching: {ZMessage.IsCachingEnabled}");
                            break;
                        case ConsoleKey.N:
                            ZFrame.IsCachingEnabled = !ZFrame.IsCachingEnabled;
                            Console.WriteLine($"[N] ZFrame's Object Caching: {ZFrame.IsCachingEnabled}");
                            break;
                        case ConsoleKey.V:
                            Verbose = !Verbose;
                            Console.WriteLine($"[V] Verbose: {Verbose}");
                            break;
                        case ConsoleKey.Q:
                            Quiet = !Quiet;
                            Console.WriteLine($"[Q] Quiet: {Quiet}");
                            break;
                        /*case ConsoleKey.R:
                            Console.WriteLine("[R] Restarting...");
                            wantsToRestart = true;
                            Thread.Sleep(100);
                            ctx.Dispose();
                            client.Dispose();
                            Thread.Sleep(100);

                            switch (EndpointState)
                            {
                                case 0: {
                                    ClientEndpoint = IpcClientEndpoint;
                                    ServerEndpoint = IpcServerEndpoint;
                                    ++EndpointState;
                                    break;
                                }
                                case 1: {
                                    ClientEndpoint = TcpClientEndpoint;
                                    ServerEndpoint = TcpServerEndpoint;
                                    EndpointState = 0;
                                    break;
                                }
                                default: throw new NotImplementedException();
                            }

                            ctx = new();
                            client = new(ctx, Tripping_ClientTask);
                            client.Start();

                            Thread.Sleep(100);
                            wantsToRestart = false;
                            using (var signal2 = client.Frontend!.ReceiveFrame())
                            {
                                if (Verbose)
                                    signal2.ToString().Trace();
                            }
                            break;*/
                    }
                };
                new Thread(() => Tripping_WorkerTask(ctx)) { IsBackground = true }.Start();
                new Thread(() => Tripping_BrokerTask(ctx)) { IsBackground = true }.Start();
                client.Start();
                using var signal = client.Frontend!.ReceiveFrame();
                if (Verbose)
                    signal.ToString().Trace();
            }
        }


        static void Tripping_ClientTask(ZContext ctx, ZSocket pipe, CancellationTokenSource canceller, object[] args)
        {
            while (WantsToRestart())
                Thread.Yield();

            using var client = new ZSocket(ctx, ZSocketType.DEALER)
            {
                IPv4Only = false,
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true
            };

            client.Connect(ClientEndpoint);
            //client.Connect("tcp://127.0.0.1:5555");
            Info("Setting up test...");
            Thread.Sleep(100);
            var wantsToExit = false;

            void OnConsoleOnCancelKeyPress(object _, ConsoleCancelEventArgs a)
            {
                a.Cancel = true;
                Volatile.Write(ref wantsToExit, true);
                Info("Ending test...");

                Console.CancelKeyPress -= OnConsoleOnCancelKeyPress;
            }

            Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool WantsToExit() => Volatile.Read(ref wantsToExit) || WantsToRestart();

            long requestsTotal = 0;
            long requestsTotalTicks = 0;
            long asyncRequestsTotal = 0;
            long asyncRequestsTotalTicks = 0;

            static long Gcd(long a, long b)
            {
                while (b > 0)
                {
                    var rem = a % b;
                    a = b;
                    b = rem;
                }
                return a;
            }

            do
            {
                long requests;
                var sw = Stopwatch.StartNew();
                sw.Restart();
                var maxTimeSpent = TimeSpan.FromSeconds(1);
                for (requests = 0; sw.Elapsed <= maxTimeSpent && !WantsToExit(); requests++)
                {
                    using var outgoing = ZFrame.Create("hello");
                    //Trace($"Created hello frame 0x{outgoing.MsgPtr():X8}");
                    client.Send(outgoing);
                    //Trace($"Sent hello frame 0x{outgoing.MsgPtr():X8}");
                    using var reply = client.ReceiveFrame();
                    //Trace($"Got reply frame 0x{reply.MsgPtr():X8}");
                    if (Verbose)
                        string.Format(reply.ToString()).Trace();
                    //Trace($"Disposing hello frame 0x{outgoing.MsgPtr():X8} and reply frame 0x{reply.MsgPtr():X8}");
                }
                sw.Stop();
                requestsTotal += requests;
                requestsTotalTicks += sw.ElapsedTicks;
                var gcd = Gcd(requestsTotal, requestsTotalTicks);
                requestsTotal /= gcd;
                requestsTotalTicks /= gcd;
                Info(
                    $"Synchronous round-trips: {requests} in {sw.ElapsedMilliseconds} ms => {(double)requests / sw.ElapsedTicks * Stopwatch.Frequency:F0} trips per second (avg. {(double)requestsTotal / requestsTotalTicks * Stopwatch.Frequency:F0} tps)");
                sw.Restart();
                requests = 0;
                var requestsRecvd = 0;
                const long outstandingRequestsThreshold = 1000;
                var flushInterval = TimeSpan.FromMilliseconds(1);
                var asyncMaxTimeSpent = maxTimeSpent - new TimeSpan(flushInterval.Ticks / 2);
                {
                    void AsyncSendThenRecv()
                    {
                        var cts = new CancellationTokenSource(1000);

                        for (;;)
                        {
                            // ReSharper disable once MethodSupportsCancellation
                            //var sending = Task.Run(() => {
                            TimeSpan lastBreak = default;
                            for (;;)
                            {
                                using var outgoing = ZFrame.Create("hello");
                                try
                                {
                                    client.SendFrame(outgoing);
                                }
                                catch (ZException ex)
                                {
                                    Info($"Will try to re-send #{requests} later");
                                    if (ex.Error == ZError.EAGAIN)
                                        Thread.Sleep(1);
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Info($"Failed to send #{requests}");
                                    ex.GetType().AssemblyQualifiedName.Info();
                                    ex.Message.Info();
                                    ex.StackTrace.Info();
                                }
                                ++requests;

                                if (cts.IsCancellationRequested || WantsToExit())
                                    return;

                                //if (requests % 50000 != 0)
                                //    continue;
                                if (sw.Elapsed - lastBreak <= flushInterval)
                                    continue;
                                lastBreak = sw.Elapsed;

                                // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
                                if (requests - Volatile.Read(ref requestsRecvd) > outstandingRequestsThreshold)
                                    break;
                            }

                            //});

                            // ReSharper disable once MethodSupportsCancellation
                            //var receiving = Task.Run(() => {
                            for (; requestsRecvd < requests; requestsRecvd++)
                            {
                                try
                                {
                                    using var reply = client.ReceiveFrame();
                                    if (Verbose)
                                        string.Format(reply.ToString()).Trace();
                                }
                                catch (ZException ex)
                                {
                                    if (ex.Error == ZError.EAGAIN)
                                    {
                                        Info($"Retrying receive #{requests}");
                                        requestsRecvd--;
                                        Thread.Sleep(1);
                                    }
                                    else
                                        throw;
                                }
                                catch (Exception ex)
                                {
                                    Info($"Failed to receive #{requests}");
                                    ex.GetType().AssemblyQualifiedName.Info();
                                    ex.Message.Info();
                                    ex.StackTrace.Info();
                                }
                            }
                            //});

                            if (cts.IsCancellationRequested)
                                return;
                        }
                    }

                    AsyncSendThenRecv();
                }
                sw.Stop();
                asyncRequestsTotal += requests;
                asyncRequestsTotalTicks += sw.ElapsedTicks;
                gcd = Gcd(asyncRequestsTotal, asyncRequestsTotalTicks);
                asyncRequestsTotal /= gcd;
                asyncRequestsTotalTicks /= gcd;
                Info(
                    $"Asynchronous round-trips: {requests} in {sw.ElapsedMilliseconds} ms => {(double)requests / sw.ElapsedTicks * Stopwatch.Frequency:F0} trips per second (avg. {(double)asyncRequestsTotal / asyncRequestsTotalTicks * Stopwatch.Frequency:F0} tps)");
            } while (!WantsToExit());
            using (var outgoing = ZFrame.Create("done"))
                pipe.SendFrame(outgoing);
        }

        //  .split worker task
        //  Here is the worker task. All it does is receive a message, and
        //  bounce it back the way it came:
        static void Tripping_WorkerTask(ZContext ctx)
        {
            do
            {
                while (WantsToRestart())
                    Thread.Yield();

                using var worker = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true
                };

                worker.Connect(ServerEndpoint);
                //worker.Connect("tcp://127.0.0.1:5556");

                while (!WantsToRestart())
                {
                    using var msg = worker.ReceiveMessage(out var error);
                    if (error == null && worker.Send(msg, out error))
                        continue;
                    // errorhandling, context terminated or sth else
                    if (error?.Equals(ZError.ETERM) ?? false)
                        return; // Interrupted
                    throw new ZException(error);
                }
            } while (WantsToRestart());
        }

        //  .split broker task
        //  Here is the broker task. It uses the {{zmq_proxy}} function to switch
        //  messages between frontend and backend:
        static void Tripping_BrokerTask(ZContext ctx)
        {
            do
            {
                while (WantsToRestart())
                    Thread.Yield();

                using var frontend = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true
                };
                using var backend = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true
                };

                frontend.Bind(ClientEndpoint);
                backend.Bind(ServerEndpoint);
                //frontend.Bind("tcp://127.0.0.1:5555");
                //backend.Bind("tcp://127.0.0.1:5556");

                if (ZContext.Proxy(frontend, backend, out var error))
                    continue;

                if (!Equals(error, ZError.ETERM))
                    throw new ZException(error);

            } while (WantsToRestart());
        }
    }
}
