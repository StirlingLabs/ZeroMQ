using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void FLServer1(string[] args)
		{
			//
			// Freelance server - Model 1
			// Trivial echo service
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} FLServer1 [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Endpoint  Where FLServer1 should bind on.");
				Console.WriteLine("              Default is tcp://127.0.0.1:7780");
				Console.WriteLine();
				args = new[] { "tcp://127.0.0.1:7780" };
			}

			using (var context = new ZContext())
			using (var server = new ZSocket(context, ZSocketType.REP)) 
			{
				server.Bind(args[0]);

				Console.WriteLine("I: echo service is ready at {0}", args[0]);

				ZMessage? message;
				while (true)
				{
					if (null != (message = server.ReceiveMessage(out var error)))
						using (message)
							server.Send(message);
					else
					{
						if (error == ZError.ETERM)
							return;	// Interrupted
						throw new ZException(error);
					}
				}
			}
		}
	}
}