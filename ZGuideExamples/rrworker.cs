﻿using System;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void RRWorker(string[] args)
		{
			//
			// Hello World worker
			// Connects REP socket to tcp://127.0.0.1:5560
			// Expects "Hello" from client, replies with "World"
			//
			// Author: metadings
			//

			if (args == null || args.Length < 2)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} RRWorker [Name] [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Name      Your Name");
				Console.WriteLine("    Endpoint  Where RRWorker should connect to.");
				Console.WriteLine("              Default is tcp://127.0.0.1:5560");
				Console.WriteLine();
				if (args.Length < 1)
					args = new[] { "World", "tcp://127.0.0.1:5560" };
				else
					args = new[] { args[0], "tcp://127.0.0.1:5560" };
			}

			var name = args[0];

			var endpoint = args[1];

			// Socket to talk to clients
			using (var context = new ZContext())
			using (var responder = new ZSocket(context, ZSocketType.REP))
			{
				responder.Connect(endpoint);

				while (true)
				{
					// Wait for next request from client
					using (var request = responder.ReceiveFrame())
					{
						Console.Write("{0} ", request.ReadString());

						// Do some 'work'
						Thread.Sleep(1);

						// Send reply back to client
						Console.WriteLine("{0}... ", name);
						responder.Send(ZFrame.Create(name));
					}
				}
			}
		}
	}
}