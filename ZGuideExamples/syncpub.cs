﻿using System;
using ZeroMQ;

namespace Examples
{
	static partial class Program
	{
		const int SyncPub_SubscribersExpected = 3;	// We wait for 3 subscribers

		public static void SyncPub(string[] args)
		{
			//
			// Synchronized publisher
			//
			// Author: metadings
			//

			// Socket to talk to clients and
			// Socket to receive signals
			using (var context = new ZContext())
			using (var publisher = new ZSocket(context, ZSocketType.PUB))
			using (var syncservice = new ZSocket(context, ZSocketType.REP))
			{
				publisher.SendHighWatermark = 1100000;
				publisher.Bind("tcp://*:5561");

				syncservice.Bind("tcp://*:5562");

				// Get synchronization from subscribers
				var subscribers = SyncPub_SubscribersExpected;
				do
				{
					Console.WriteLine("Waiting for {0} subscriber" + (subscribers > 1 ? "s" : string.Empty) + "...", subscribers);

					// - wait for synchronization request
					syncservice.ReceiveFrame();

					// - send synchronization reply
					syncservice.Send(ZFrame.Create());
				} 
				while (--subscribers > 0);

				// Now broadcast exactly 20 updates followed by END
				Console.WriteLine("Broadcasting messages:");
				for (var i = 0; i < 20; ++i)
				{
					Console.WriteLine("Sending {0}...", i);
					publisher.Send(ZFrame.Create(i));
				}
				publisher.Send(ZFrame.Create("END"));
			}
		}
	}
}