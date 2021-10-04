using System;
using ZeroMQ;

namespace Examples
{
	static partial class Program
	{
		public static void RRClient(string[] args)
		{
			//
			// Hello World client
			// Connects REQ socket to tcp://127.0.0.1:5559
			// Sends "Hello" to server, expects "World" back
			//
			// Author: metadings
			//

			// Socket to talk to server
			using (var context = new ZContext())
			using (var requester = new ZSocket(context, ZSocketType.REQ))
			{
				requester.Connect("tcp://127.0.0.1:5559");

				for (var n = 0; n < 10; ++n)
				{
					requester.Send(ZFrame.Create("Hello"));

					using (var reply = requester.ReceiveFrame())
						Console.WriteLine("Hello {0}!", reply.ReadString());
				}
			}
		}
	}
}