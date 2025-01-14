﻿using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void WUServer(string[] args)
		{
			//
			// Weather update server
			// Binds PUB socket to tcp://*:5556
			// Publishes random weather updates
			//
			// Author: metadings
			//

			// Prepare our context and publisher
			using (var context = new ZContext())
			using (var publisher = new ZSocket(context, ZSocketType.PUB))
			{
				var address = "tcp://*:5556";
				Console.WriteLine("I: Publisher.Bind'ing on {0}", address);
				publisher.Bind(address);

				/* foreach (IPAddress localAddress in WUProxy_GetPublicIPs())
				{
					var epgmAddress = string.Format("epgm://{0};239.192.1.1:8100", localAddress);
					Console.WriteLine("I: Publisher.Bind'ing on {0}...", epgmAddress);
					publisher.Bind(epgmAddress);
				} */

				// Initialize random number generator
				var rnd = new Random();

				while (true)
				{
					// Get values that will fool the boss
					var zipcode = rnd.Next(99999);
					var temperature = rnd.Next(-55, +45);

					// Send message to all subscribers
					var update = $"{zipcode:D5} {temperature}";
					using (var updateFrame = ZFrame.Create(update))
						publisher.Send(updateFrame);
				}
			}
		}
	}
}