using System;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void HWServer(string[] args)
		{
			//
			// Hello World server
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} HWServer [Name]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Name   Your name. Default: World");
				Console.WriteLine();
				args = new[] { "World" };
			}

			var name = args[0];

			// Create
			using (var context = new ZContext())
			using (var responder = new ZSocket(context, ZSocketType.REP))
			{
				// Bind
				responder.Bind("tcp://*:5555");

				while (true)
				{
					// Receive
					using (var request = responder.ReceiveFrame())
					{
						Console.WriteLine("Received {0}", request.ReadString());

						// Do some work
						Thread.Sleep(1);

						// Send
						responder.Send(ZFrame.Create(name));
					}
				}
			}
		}
	}
}