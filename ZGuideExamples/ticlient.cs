using System;
using System.Linq;
using System.Threading;
using Examples.MDCliApi;
using ZeroMQ;

// using System.Runtime.Remoting.Messaging;

namespace Examples
{
	//  Titanic client example
	//  Implements client side of http://rfc.zeromq.org/spec:9

	//  Lets us build this source without creating a library
	static partial class Program
	{


		public static void TIClient(string[] args)
		{
			var canceller = new CancellationTokenSource();
			Console.CancelKeyPress += (s, ea) =>
			{
				ea.Cancel = true;
				canceller.Cancel();
			};

			using (var session = new MajordomoClient("tcp://127.0.0.1:5555", Verbose))
			{
				//  1. Send 'echo' request to Titanic
				var request = ZMessage.Create();
				request.Add(new("echo"));
				request.Add(new("Hello World"));

				var uuid = Guid.Empty; 
				using (var reply = TIClient_ServiceCall(session, "titanic.request", request, canceller))
				{
					if (reply != null)
					{
						uuid = Guid.Parse(reply.PopString());
						$"I: request UUID {uuid}".DumpString();
					}
				}

				// 2. Wait until we get a reply
				while (!canceller.IsCancellationRequested)
				{
					Thread.Sleep(100);
					request.Dispose();
					request = ZMessage.Create();
					request.Add(new(uuid.ToString()));
					var reply = TIClient_ServiceCall(session, "titanic.reply", request, canceller);
					if (reply != null)
					{
						var replystring = reply.Last().ToString();
						$"Reply: {replystring}\n".DumpString();
						reply.Dispose();

						// 3. Close Request
						request.Dispose();
						request = ZMessage.Create();
						request.Add(new(uuid.ToString()));
						reply = TIClient_ServiceCall(session, "titanic.close", request, canceller);
						reply.Dispose();
						break;
					}
					"I: no reply yet, trying again...\n".DumpString();
					Thread.Sleep(5000); // try again in 5 seconds
				}
			}
		}

		//  Calls a TSP service
		//  Returns response if successful (status code 200 OK), else NULL
		static ZMessage? TIClient_ServiceCall (MajordomoClient session, string service, ZMessage request, CancellationTokenSource cts)
		{
			using (var reply = session.Send(service, request, cts))
			{
				if (reply != null)
				{
					var status = reply.PopString();
					if (status.Equals("200"))
						return reply.Clone();
					if (status.Equals("400"))
					{
						"E: client fatal error, aborting".DumpString();
						cts.Cancel();
					}
					else if (status.Equals("500"))
					{
						"E: server fatal error, aborting".DumpString();
						cts.Cancel();
					}
				}
				else
					cts.Cancel();   // Interrupted or failed
			}
			return null; 
		}
	}
}
