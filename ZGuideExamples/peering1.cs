using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		//
		// Broker peering simulation (part 1)
		// Prototypes the state flow
		//
		// Author: metadings
		//

		public static void Peering1(string[] args)
		{
			// First argument is this broker's name
			// Other arguments are our peers' names
			//
			if (args == null || args.Length < 2)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: {0} Peering1 World Receiver0", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine("       {0} Peering1 Receiver0 World", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				return;
			}
			var self = args[0];
			Console.WriteLine("I: preparing broker as {0}", self);

			using (var context = new ZContext())
			using (var backend = new ZSocket(context, ZSocketType.PUB))
			using (var frontend = new ZSocket(context, ZSocketType.SUB))
			{
				// Bind backend to endpoint
				backend.Bind("tcp://127.0.0.1:" + Peering1_GetPort(self));

				// Connect frontend to all peers
				frontend.SubscribeAll();
				for (var i = 1; i < args.Length; ++i)
				{
					var peer = args[i];
					Console.WriteLine("I: connecting to state backend at {0}", peer);
					frontend.Connect("tcp://127.0.0.1:" + Peering1_GetPort(peer));
				}

				// The main loop sends out status messages to peers, and collects
				// status messages back from peers. The zmq_poll timeout defines
				// our own heartbeat:

				var poll = ZPollItem.CreateReceiver();
				var rnd = new Random();

				while (true)
				{
					// Poll for activity, or 1 second timeout
					if (!frontend.PollIn(poll, out var incoming, out var error, TimeSpan.FromSeconds(1)))
					{
						if (error == ZError.EAGAIN)
						{
							using (var output = ZMessage.Create())
							{
								output.Add(ZFrame.Create(self));

								var outputNumber = ZFrame.Create(4);
								outputNumber.Write(rnd.Next(10));
								output.Add(outputNumber);

								backend.Send(output);
							}

							continue;
						}
						if (error == ZError.ETERM)
							return;

						throw new ZException(error);
					}
					using (incoming)
					{
						var peer_name = incoming[0].ReadString();
						var available = incoming[1].ReadInt32();
						Console.WriteLine("{0} - {1} workers free", peer_name, available);
					}
				}
			}
		}

		static short Peering1_GetPort(string name)
		{
			var hash = (short)name[0];
			if (hash < 1024)
				hash += 1024;
			return hash;
		}
	}
}