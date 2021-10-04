using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void MSPoller(string[] args)
		{
			//
			// Reading from multiple sockets
			// This version uses zmq_poll()
			//
			// Author: metadings
			//

			using (var context = new ZContext())
			using (var receiver = new ZSocket(context, ZSocketType.PULL))
			using (var subscriber = new ZSocket(context, ZSocketType.SUB))
			{
				// Connect to task ventilator
				receiver.Connect("tcp://127.0.0.1:5557");

				// Connect to weather server
				subscriber.Connect("tcp://127.0.0.1:5556");
				subscriber.SetOption(ZSocketOption.SUBSCRIBE, "10001 ");

				var sockets = new[] { receiver, subscriber };
				var polls = new[] { ZPollItem.CreateReceiver(), ZPollItem.CreateReceiver() };

				// Process messages from both sockets
				while (true)
				{
					if (sockets.PollIn(polls, out var msg, out var error, TimeSpan.FromMilliseconds(64)))
					{
						if (msg[0] != null)
						{
							// Process task
						}
						if (msg[1] != null)
						{
							// Process weather update
						}
					}
					else
					{
						if (error == ZError.ETERM)
							return;	// Interrupted
						if (error != ZError.EAGAIN)
							throw new ZException(error);
					}
				}
			}
		}
	}
}