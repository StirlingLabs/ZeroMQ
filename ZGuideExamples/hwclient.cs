using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void HWClient(string[] args)
		{
			//
			// Hello World client
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} HWClient [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Endpoint  Where HWClient should connect to.");
				Console.WriteLine("              Default is tcp://127.0.0.1:5555");
				Console.WriteLine();
				args = new[] { "tcp://127.0.0.1:5555" };
			}

			var endpoint = args[0];

			// Create
			using (var context = new ZContext())
			using (var requester = new ZSocket(context, ZSocketType.REQ))
			{
				// Connect
				requester.Connect(endpoint);

				for (var n = 0; n < 10; ++n)
				{
					var requestText = "Hello";
					Console.Write("Sending {0}...", requestText);

					// Send
					requester.Send(ZFrame.Create(requestText));

					// Receive
					using (var reply = requester.ReceiveFrame())
						Console.WriteLine(" Received: {0} {1}!", requestText, reply.ReadString());
				}
			}
		}
	}
}