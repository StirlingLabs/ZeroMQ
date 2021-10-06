using System;
using System.Collections.Concurrent;
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

        private static double _asyncFlushIntervalMs = 1;

        private const int MessageHighWatermark = 400_000;
        private const int MessageBufferSize = MessageHighWatermark * 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool WantsToRestart() => Volatile.Read(ref wantsToRestart);

        private static ConcurrentDictionary<int, WeakReference<ZSocket>> Sockets
            = new();

        private static void ForEachSocket(Action<ZSocket> action)
        {

            foreach (var kv in Sockets)
            {
                var wr = kv.Value;
                if (!wr.TryGetTarget(out var sock))
                {
                    Sockets.TryRemove(kv.Key, out _);
                    continue;
                }
                action(sock);
            }
        }

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
                            Info($"[M] ZMessage's Object Caching: {ZMessage.IsCachingEnabled}");
                            break;
                        case ConsoleKey.N:
                            ZFrame.IsCachingEnabled = !ZFrame.IsCachingEnabled;
                            Info($"[N] ZFrame's Object Caching: {ZFrame.IsCachingEnabled}");
                            break;
                        case ConsoleKey.V:
                            Verbose = !Verbose;
                            Info($"[V] Verbose: {Verbose}");
                            break;
                        case ConsoleKey.Q:
                            Quiet = !Quiet;
                            Info($"[Q] Quiet: {Quiet}");
                            break;
                        case ConsoleKey.UpArrow: {
                            var v = _asyncFlushIntervalMs = Math.Max(_asyncFlushIntervalMs + 1, 1);
                            Info($"Async Flush Interval Multiplier /\\: {v}");
                            break;
                        }
                        case ConsoleKey.DownArrow: {
                            var v = _asyncFlushIntervalMs = Math.Max(_asyncFlushIntervalMs - 1, 1);
                            Info($"Async Flush Interval Multiplier \\/: {v}");
                            break;
                        }

                        /*
                        case ConsoleKey.UpArrow: {
                            ForEachSocket(sock => {
                                var v = sock.SendHighWatermark = Math.Max(sock.SendHighWatermark + 1000, 0);
                                Info($"{sock} SendHighWatermark /\\: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.DownArrow: {
                            ForEachSocket(sock => {
                                var v = sock.SendHighWatermark = Math.Max(sock.SendHighWatermark - 1000, 0);
                                Info($"{sock} SendHighWatermark \\/: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.RightArrow: {
                            ForEachSocket(sock => {
                                var v = sock.ReceiveHighWatermark = Math.Max(sock.ReceiveHighWatermark + 1000, 0);
                                Info($"{sock} ReceiveHighWatermark /\\: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.LeftArrow: {
                            ForEachSocket(sock => {
                                var v = sock.ReceiveHighWatermark = Math.Max(sock.ReceiveHighWatermark - 1000, 0);
                                Info($"{sock} ReceiveHighWatermark \\/: {v}");
                            });
                            break;
                        }
                        */
                        case ConsoleKey.Home: {
                            ForEachSocket(sock => {
                                var v = sock.SendBufferSize = Math.Max(sock.SendBufferSize + 256 * 1024, MessageBufferSize);
                                Info($"{sock} SendBufferSize /\\: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.End: {
                            // NOTE: reducing buffer size while running is *DANGEROUS*
                            ForEachSocket(sock => {
                                var v = sock.SendBufferSize = Math.Max(sock.SendBufferSize + 256 * 1024, MessageBufferSize);
                                Info($"{sock} SendBufferSize \\/: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.PageUp: {
                            ForEachSocket(sock => {
                                var v = sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize + 256 * 1024, MessageBufferSize);
                                Info($"{sock} ReceiveBufferSize /\\: {v}");
                            });
                            break;
                        }
                        case ConsoleKey.PageDown: {
                            // NOTE: reducing buffer size while running is *DANGEROUS*
                            ForEachSocket(sock => {
                                var v = sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize - 256 * 1024, MessageBufferSize);
                                Info($"{sock} ReceiveBufferSize \\/: {v}");
                            });
                            break;
                        }
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
                Trace($"Signal: {signal}");
            }
        }


        static void Tripping_ClientTask(ZContext ctx, ZSocket pipe, CancellationTokenSource canceller, object[] args)
        {
            Thread.BeginThreadAffinity();

            while (WantsToRestart())
                Thread.Yield();
            using var client = new ZSocket(ctx, ZSocketType.DEALER)
            {
                Name = "Client",
                IPv4Only = false,
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true,
                SendHighWatermark = MessageHighWatermark,
                SendBufferSize = MessageBufferSize,
                ReceiveHighWatermark = MessageHighWatermark,
                ReceiveBufferSize = MessageBufferSize
            };

            Sockets.TryAdd(client.GetHashCode(), new(client));

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
                    ZFrame? outgoing = null;
                    try
                    {
                        outgoing = ZFrame.Create("hello");
                        //Trace($"Created hello frame 0x{outgoing.MsgPtr():X8}");
                        try
                        {
                            for (;;)
                            {
                                if (client.Send(outgoing, out var error))
                                    break;

                                if (error != ZError.EAGAIN)
                                    throw new ZException(error);

                                Trace($"Retrying sync send message #{requests}");
                                Thread.Yield();
                            }
                        }
                        catch (ZException ex)
                        {
                            Info($"Message sync send #{requests} failed: {ex.Error}");
                            Thread.Yield();
                            --requests;
                            continue;
                        }
                        //Trace($"Sent hello frame 0x{outgoing.MsgPtr():X8}");

                        var retryCount = 0;
                        ClientSyncRetryReceive:
                        try
                        {
                            //using var reply = client.ReceiveFrame();
                            var reply = client.ReceiveFrame();
                            try
                            {
                                //Trace($"Got reply frame 0x{reply.MsgPtr():X8}");
                                if (reply == null)
                                {
                                    if (sw.Elapsed > maxTimeSpent || WantsToExit())
                                    {
                                        Info($"Message sync receive #{requests} aborted");
                                        break;
                                    }
                                    if (retryCount++ < 1000)
                                        goto ClientSyncRetryReceive;
                                    Info($"Message sync receive #{requests} skipped (retried x{retryCount})");
                                }
                                else
                                {
                                    var s = reply?.ToString();
                                    if (s != "hello")
                                        Info($"Corrupt message #{requests}: {s}");
                                    //Trace($"Disposing hello frame 0x{outgoing.MsgPtr():X8} and reply frame 0x{reply.MsgPtr():X8}");
                                }
                            }
                            finally
                            {
                                reply?.Dispose();
                            }
                        }
                        catch (ZException ex)
                        {
                            if (ex.Error == ZError.EAGAIN)
                            {
                                Thread.Yield();
                                if (sw.Elapsed > maxTimeSpent || WantsToExit())
                                    break;
                                goto ClientSyncRetryReceive;
                            }
                            Info($"Message sync receive #{requests} failed: {ex.Error}");
                        }
                    }
                    finally
                    {
                        outgoing?.Dispose();
                    }
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
                const long outstandingRequestsThreshold = 0;
                var flushInterval = TimeSpan.FromMilliseconds(_asyncFlushIntervalMs);
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
                                ZFrame? outgoing = null;
                                try
                                {
                                    outgoing = ZFrame.Create("hello");
                                    try
                                    {

                                        for (;;)
                                        {
                                            //var success = client.SendFrame(outgoing, out var error);
                                            var success = client.SendFrame(outgoing, ZSocketFlags.DontWait, out var error);
                                            
                                            if (success) break;

                                            if (error != ZError.EAGAIN)
                                                throw new ZException(error);

                                            Trace($"Retrying async send message #{requests}");
                                            Thread.Yield();
                                        }
                                    }
                                    catch (ZException ex)
                                    {
                                        if (ex.Error != ZError.EAGAIN)
                                        {
                                            Info($"Failed to async send #{requests}");
                                            ex.GetType().AssemblyQualifiedName.Info();
                                            ex.Message.Info();
                                            ex.StackTrace.Info();
                                        }
                                        else
                                        {
                                            Info($"Will retry to async send #{requests}: {ex.Error}");
                                            Thread.Yield();
                                            --requests;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Info($"Failed to async send #{requests}");
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
                                finally
                                {
                                    outgoing?.Dispose();
                                }
                            }

                            //});

                            // ReSharper disable once MethodSupportsCancellation
                            //var receiving = Task.Run(() => {
                            for (; requestsRecvd < requests; requestsRecvd++)
                            {
                                var retryCount = 0;
                                ClientAsyncRetryReceive:
                                ZFrame? reply = null;
                                try
                                {
                                    reply = client.ReceiveFrame();
                                    //Trace($"Got reply frame 0x{reply.MsgPtr():X8}");
                                    if (reply == null)
                                    {
                                        if (retryCount++ < 10)
                                            goto ClientAsyncRetryReceive;
                                        Environment.FailFast($"Message async receive #{requests} failed");
                                        return;
                                    }
                                    var s = reply.ToString();
                                    if (s != "hello")
                                        Info($"Corrupt message #{requests}: {s}");
                                }
                                catch (ZException ex)
                                {
                                    if (ex.Error == ZError.EAGAIN)
                                    {
                                        Info($"Retrying async receive #{requests}: {ex.Error}");
                                        //requestsRecvd--;
                                        Thread.Yield();
                                        if (retryCount++ < 10)
                                            goto ClientAsyncRetryReceive;
                                        Environment.FailFast($"Message async receive #{requests} failed");
                                        return;
                                    }
                                    else
                                        throw;
                                }
                                catch (Exception ex)
                                {
                                    Info($"Failed to async receive #{requests}");
                                    ex.GetType().AssemblyQualifiedName.Info();
                                    ex.Message.Info();
                                    ex.StackTrace.Info();
                                    requestsRecvd--;
                                }
                                finally
                                {
                                    reply?.Dispose();
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
                if (sw.ElapsedMilliseconds > 1100)
                {
                    var v = _asyncFlushIntervalMs = Math.Max(_asyncFlushIntervalMs - 1, 1);
                    Info($"*** AUTO *** Async Flush Interval Multiplier \\/: {v}");
                }
            } while (!WantsToExit());
            {
                ZFrame? outgoing = null;
                try
                {
                    outgoing = ZFrame.Create("done");
                    pipe.SendFrame(outgoing);
                }
                finally
                {
                    outgoing?.Dispose();
                }
            }
        }

        //  .split worker task
        //  Here is the worker task. All it does is receive a message, and
        //  bounce it back the way it came:
        static void Tripping_WorkerTask(ZContext ctx)
        {
            Thread.BeginThreadAffinity();

            do
            {
                while (WantsToRestart())
                    Thread.Yield();

                using var worker = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    Name = "Worker",
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true,
                    SendHighWatermark = MessageHighWatermark,
                    SendBufferSize = MessageBufferSize,
                    ReceiveHighWatermark = MessageHighWatermark,
                    ReceiveBufferSize = MessageBufferSize
                };

                Sockets.TryAdd(worker.GetHashCode(), new(worker));

                worker.Connect(ServerEndpoint);
                //worker.Connect("tcp://127.0.0.1:5556");

                while (!WantsToRestart())
                {
                    using var msg = worker.ReceiveMessage(out var error);
                    if (error is null)
                    {
                        if (msg is not null)
                            for (;;)
                            {
                                try
                                {
                                    worker.Send(msg, out error);
                                }
                                catch (ZException ex) when (ex.Error == ZError.EAGAIN)
                                {
                                    Info($"Worker Will retry to forward message: {ex.Error}");
                                    Thread.Yield();
                                    continue;
                                }
                                break;
                            }
                        else
                            Thread.Yield(); // or you can spin here

                        continue;
                    }

                    // errorhandling, context terminated or sth else
                    if (error == ZError.ETERM)
                        return; // Interrupted

                    // lacking system resources?
                    if (error == ZError.EAGAIN)
                        continue;

                    throw new ZException(error);
                }
            } while (WantsToRestart());
        }

        //  .split broker task
        //  Here is the broker task. It uses the {{zmq_proxy}} function to switch
        //  messages between frontend and backend:
        static void Tripping_BrokerTask(ZContext ctx)
        {
            Thread.BeginThreadAffinity();

            do
            {
                while (WantsToRestart())
                    Thread.Yield();

                using var frontend = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    Name = "Broker Frontend",
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true,
                    SendHighWatermark = MessageHighWatermark,
                    SendBufferSize = MessageBufferSize,
                    ReceiveHighWatermark = MessageHighWatermark,
                    ReceiveBufferSize = MessageBufferSize
                };

                Sockets.TryAdd(frontend.GetHashCode(), new(frontend));

                using var backend = new ZSocket(ctx, ZSocketType.DEALER)
                {
                    Name = "Broker Backend",
                    IPv4Only = false,
                    SendTimeout = TimeSpan.FromSeconds(1),
                    ReceiveTimeout = TimeSpan.FromSeconds(1),
                    Immediate = true,
                    SendHighWatermark = MessageHighWatermark,
                    SendBufferSize = MessageBufferSize,
                    ReceiveHighWatermark = MessageHighWatermark,
                    ReceiveBufferSize = MessageBufferSize
                };

                Sockets.TryAdd(backend.GetHashCode(), new(backend));

                frontend.Bind(ClientEndpoint);
                backend.Bind(ServerEndpoint);
                //frontend.Bind("tcp://127.0.0.1:5555");
                //backend.Bind("tcp://127.0.0.1:5556");

                ContinueProxy:
                if (ZContext.Proxy(frontend, backend, out var error))
                    continue;

                // lacking system resources?
                if (error == ZError.EAGAIN)
                {
                    Thread.Yield();
                    goto ContinueProxy;
                }

                if (error != ZError.ETERM)
                    throw new ZException(error);

            } while (WantsToRestart());
        }
    }
}
