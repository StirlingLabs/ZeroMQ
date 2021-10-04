using System;
using System.Threading;
using Examples.MDCliApi2;
using ZeroMQ;

namespace Examples
{
    // Let us build this source without creating a library
    static partial class Program
    {
        //  Majordomo Protocol client example
        //  Uses the mdcli API to hide all MDP aspects
        public static void MDClient2(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, ea) =>
            {
                ea.Cancel = true;
                cts.Cancel();
            };

            using (var session = new MajordomoClient("tcp://127.0.0.1:5555", Verbose))
            {
                int count;
                for (count = 0; count < 100000 && !cts.IsCancellationRequested; count++)
                {
                    using var request = ZMessage.Create();
                    request.Prepend(ZFrame.Create("Hello world"));
                    session.Send("echo", request, cts);
                }
                for (count = 0; count < 100000 && !cts.IsCancellationRequested; count++)
                {
                    using (var reply = session.Recv(cts))
                    {
                        if (reply == null)
                            break; // Interrupt or failure
                    }
                }
                Console.WriteLine("{0} replies received\n", count);
            }
        }
    }
}
