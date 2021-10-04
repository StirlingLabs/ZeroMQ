using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void FLServer2(string[] args)
		{
			//
			// Freelance server - Model 2
			// Does some work, replies OK, with message sequencing
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} FLServer2 [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Endpoint  Where FLServer2 should bind on.");
				Console.WriteLine("              Default is tcp://127.0.0.1:7781");
				Console.WriteLine();
				args = new[] { "tcp://127.0.0.1:7781" };
			}

			var endpoint = args[0];

			using (var context = new ZContext())
			using (var server = new ZSocket(context, ZSocketType.REP))
			{
				server.Bind(endpoint);
				Console.WriteLine("I: server is ready as {0}", endpoint);

				ZError? error;
				ZMessage? incoming;

				while (true)
				{
					if (null == (incoming = server.ReceiveMessage(out error)))
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}

					ZFrame identity;
					using (incoming)
					{
						// Fail nastily if run against wrong client
						if (incoming.Count < 2)
							throw new InvalidOperationException();

						identity = incoming.RemoveAt(0, false);
					}
					using (identity)
					using (var outgoing = ZMessage.Create())
					{
						outgoing.Add(identity);
						outgoing.Add(ZFrame.Create("OK"));

						if (!server.Send(outgoing, out error))
						{
							if (error == ZError.ETERM)
								break;	// Interrupted
							throw new ZException(error);
						}
					}
				}

				if (error == ZError.ETERM)
					Console.WriteLine("W: interrupted");
			}
		}
	}
}