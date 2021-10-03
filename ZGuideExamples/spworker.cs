using System;
using System.Text;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	static partial class Program
	{
		public static void SPWorker(string[] args)
		{
			//
			// Simple Pirate worker
			// Connects REQ socket to tcp://127.0.0.1:5556
			// Implements worker part of load-balancing
			//
			// Author: metadings
			//

			var rnd = new Random();
			if (args == null || args.Length < 1)
				args = new[] { "World" + rnd.Next() };
			var name = args[0];

			using (var context = new ZContext())
			using (var worker = new ZSocket(context, ZSocketType.REQ))
			{
				worker.Identity = Encoding.UTF8.GetBytes(name);
				worker.Connect("tcp://127.0.0.1:5556");

				Console.WriteLine("I: ({0}) worker ready", name);

				using (var outgoing = new ZFrame("READY"))
					worker.Send(outgoing);

				var cycles = 0;
				ZMessage? incoming;

				while (true)
				{
					if (null == (incoming = worker.ReceiveMessage(out var error)))
					{
						if (error == ZError.ETERM)
							return;

						throw new ZException(error);
					}
					using (incoming)
					{
						// Simulate various problems, after a few cycles
						cycles++;

						if (cycles > 3 && rnd.Next(5) == 0)
						{
							Console.WriteLine("I: ({0}) simulating a crash", name);
							return;
						}
						if (cycles > 3 && rnd.Next(5) == 0)
						{
							Console.WriteLine("I: ({0}) simulating CPU overload", name);
							Thread.Sleep(500);
						}

						Console.WriteLine("I: ({0}) normal reply", name);

						Thread.Sleep(1); // Do some heavy work

						// Send message back
						worker.Send(incoming);
					}
				}

			}
		}
	}
}