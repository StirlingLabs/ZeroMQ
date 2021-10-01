using System;
using System.Diagnostics;
using Examples.FLCliApi;
using ZeroMQ;

namespace Examples
{
	// Lets us build this source without creating a library

	static partial class Program
	{

		public static void FLClient3(string[] args)
		{
			//
			// Freelance client - Model 3
			// Uses FLCliApi.FreelanceClient class to encapsulate Freelance pattern
			//
			// Author: metadings
			//
			if (args == null || args.Length < 2)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} FLClient3 [Name] [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Name      Your Name");
				Console.WriteLine("    Endpoint  Where FLClient3 should connect to.");
				Console.WriteLine("              Default: tcp://127.0.0.1:5555");
				Console.WriteLine();
				if (args.Length < 1)
					args = new[] { "World", "tcp://127.0.0.1:5555" };
				else
					args = new[] { args[0], "tcp://127.0.0.1:5555" };
			}

			var name = args[0];

			// Create new freelance client object
			using (var client = new FreelanceClient())
			{
				// Connect to one or more endpoints
				for (var i = 0; i < args.Length - 1; ++i)
				{
					client.Connect(args[1]);
				}

				// Send a bunch of name resolution 'requests', measure time
				var stopwatch = new Stopwatch();
				stopwatch.Start();

				var requests = 0;
				while (requests++ < 100)
				{
					using (var request = new ZMessage())
					{
						request.Add(new ZFrame(name));

						using (var reply = client.Request(request))
						{

						}
					}
				}

				stopwatch.Stop();
				Console.WriteLine("Average round trip cost: {0} ms", stopwatch.ElapsedMilliseconds / requests);
			}
		}
	}
}