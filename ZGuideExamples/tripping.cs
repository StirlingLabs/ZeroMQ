using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

// using System.Runtime.Remoting.Messaging;

namespace Examples
{
    static partial class Program
    {
        //  Round-trip demonstrator
        //  While this example runs in a single process, that is just to make
        //  it easier to start and stop the example. The client task signals to
        //  main when it's ready.
        public static void Tripping(string[] args)
        {
            var canceller = new CancellationTokenSource();
            Console.CancelKeyPress += (s, ea) => {
                ea.Cancel = true;
                canceller.Cancel();
            };

            using var ctx = new ZContext();
            using var client = new ZActor(ctx, Tripping_ClientTask);
            new Thread(() => Tripping_WorkerTask(ctx)).Start();
            new Thread(() => Tripping_BrokerTask(ctx)).Start();
            client.Start();
            using var signal = client.Frontend.ReceiveFrame();
            if (Verbose)
                signal.ToString().DumpString();
        }


        static void Tripping_ClientTask(ZContext ctx, ZSocket pipe, CancellationTokenSource canceller, object[] args)
        {
            using var client = new ZSocket(ctx, ZSocketType.DEALER)
            {
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true
            };

            client.Connect("tcp://127.0.0.1:5555");
            "Setting up test...".DumpString();
            Thread.Sleep(100);
            var wantsToExit = false;

            void OnConsoleOnCancelKeyPress(object _, ConsoleCancelEventArgs a)
            {
                a.Cancel = true;
                Volatile.Write(ref wantsToExit, true);
                "Ending test...".DumpString();

                Console.CancelKeyPress -= OnConsoleOnCancelKeyPress;
            }

            Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool WantsToExit() => Volatile.Read(ref wantsToExit);

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
                for (requests = 0; sw.Elapsed <= maxTimeSpent; requests++)
                {
                    using var outgoing = ZFrame.Create("hello");
                    //$"Created hello frame 0x{outgoing.MsgPtr():X8}".DumpString();
                    client.Send(outgoing);
                    //$"Sent hello frame 0x{outgoing.MsgPtr():X8}".DumpString();
                    using var reply = client.ReceiveFrame();
                    //$"Got reply frame 0x{reply.MsgPtr():X8}".DumpString();
                    if (Verbose)
                        string.Format(reply.ToString()).DumpString();
                    //$"Disposing hello frame 0x{outgoing.MsgPtr():X8} and reply frame 0x{reply.MsgPtr():X8}".DumpString();
                }
                sw.Stop();
                requestsTotal += requests;
                requestsTotalTicks += sw.ElapsedTicks;
                var gcd = Gcd(requestsTotal, requestsTotalTicks);
                requestsTotal /= gcd;
                requestsTotalTicks /= gcd;
                $"Synchronous round-trips: {requests} in {sw.ElapsedMilliseconds} ms => {(double)requests / sw.ElapsedTicks * Stopwatch.Frequency:F0} trips per second (avg. {(double)requestsTotal / requestsTotalTicks * Stopwatch.Frequency:F0} tps)"
                    .DumpString();
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
                                    $"Will try to re-send #{requests} later".DumpString();
                                    if (ex.Error == ZError.EAGAIN)
                                        Thread.Sleep(1);
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    $"Failed to send #{requests}".DumpString();
                                    ex.GetType().AssemblyQualifiedName.DumpString();
                                    ex.Message.DumpString();
                                    ex.StackTrace.DumpString();
                                }
                                ++requests;

                                if (cts.IsCancellationRequested)
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
                                        string.Format(reply.ToString()).DumpString();
                                }
                                catch (ZException ex)
                                {
                                    $"Retrying receive #{requests}".DumpString();
                                    requestsRecvd--;
                                    if (ex.Error == ZError.EAGAIN)
                                        Thread.Sleep(1);
                                }
                                catch (Exception ex)
                                {
                                    $"Failed to receive #{requests}".DumpString();
                                    ex.GetType().AssemblyQualifiedName.DumpString();
                                    ex.Message.DumpString();
                                    ex.StackTrace.DumpString();
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
                $"Asynchronous round-trips: {requests} in {sw.ElapsedMilliseconds} ms => {(double)requests / sw.ElapsedTicks * Stopwatch.Frequency:F0} trips per second (avg. {(double)asyncRequestsTotal / asyncRequestsTotalTicks * Stopwatch.Frequency:F0} tps)"
                    .DumpString();
            } while (!WantsToExit());
            using (var outgoing = ZFrame.Create("done"))
                pipe.SendFrame(outgoing);
        }

        //  .split worker task
        //  Here is the worker task. All it does is receive a message, and
        //  bounce it back the way it came:
        static void Tripping_WorkerTask(ZContext ctx)
        {
            using var worker = new ZSocket(ctx, ZSocketType.DEALER)
            {
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true
            };

            worker.Connect("tcp://127.0.0.1:5556");

            while (true)
            {
                using var msg = worker.ReceiveMessage(out var error);
                if (error == null && worker.Send(msg, out error))
                    continue;
                // errorhandling, context terminated or sth else
                if (error.Equals(ZError.ETERM))
                    return; // Interrupted
                throw new ZException(error);
            }
        }

        //  .split broker task
        //  Here is the broker task. It uses the {{zmq_proxy}} function to switch
        //  messages between frontend and backend:
        static void Tripping_BrokerTask(ZContext ctx)
        {
            using var frontend = new ZSocket(ctx, ZSocketType.DEALER)
            {
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true
            };
            using var backend = new ZSocket(ctx, ZSocketType.DEALER)
            {
                SendTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                Immediate = true
            };

            frontend.Bind("tcp://*:5555");
            backend.Bind("tcp://*:5556");

            if (ZContext.Proxy(frontend, backend, out var error))
                return;

            if (Equals(error, ZError.ETERM))
                return; // Interrupted

            throw new ZException(error);
        }
    }
}
