using System;
using System.Threading;
using Examples.MDCliApi;
using ZeroMQ;

// using System.Runtime.Remoting.Messaging;

namespace Examples
{
    //  Lets us build this source without creating a library
    static partial class Program
    {
        //  MMI echo query example
        public static void MMIEcho(string[] args)
        {
            var cancellor = new CancellationTokenSource();
            Console.CancelKeyPress += (s, ea) =>
            {
                ea.Cancel = true;
                cancellor.Cancel();
            };

            using (var session = new MajordomoClient("tcp://127.0.0.1:5555", Verbose))
            {
                var request  = new ZMessage();
                request.Add(new ZFrame("echo"));

                var reply = session.Send("mmi.service", request, cancellor);
                if (reply != null)
                {
                    var replycode = reply[0].ToString();
                    "Loopup echo service: {0}\n".DumpString(replycode);
                    reply.Dispose();
                }
                else
                    "E: no response from broker, make sure it's running\n".DumpString();
            }
        }
    }
}
