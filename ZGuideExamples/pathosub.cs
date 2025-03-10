﻿using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void PathoSub(string[] args)
		{
			//
			// Pathological subscriber
			// Subscribes to one random topic and prints received messages
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
			{
				Console.WriteLine();
				Console.WriteLine("Usage: ./{0} PathoSub [Endpoint]", AppDomain.CurrentDomain.FriendlyName);
				Console.WriteLine();
				Console.WriteLine("    Endpoint  Where PathoSub should connect to.");
				Console.WriteLine("              Default is tcp://127.0.0.1:5556");
				Console.WriteLine();
				args = new[] { "tcp://127.0.0.1:5556" };
			}

			using (var context = new ZContext())
			using (var subscriber = new ZSocket(context, ZSocketType.SUB))
			{
				subscriber.Connect(args[0]);

				var rnd = new Random();
				var subscription = $"{rnd.Next(1000):D3}";
				subscriber.Subscribe(subscription);

				ZMessage? msg;
				while (true)
				{
					if (null == (msg = subscriber.ReceiveMessage(out var error)))
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}
					using (msg)
					{
						if (msg[0].ReadString() != subscription)
							throw new InvalidOperationException();
						Console.WriteLine(msg[1].ReadString());
					}
				}
			}
		}
	}
}