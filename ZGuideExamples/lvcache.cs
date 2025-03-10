﻿using System;
using System.Collections.Generic;
using System.Linq;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void LVCache(string[] args)
		{
			//
			// Last value cache
			// Uses XPUB subscription messages to re-send data
			//
			// Author: metadings
			//

			using (var context = new ZContext())
			using (var frontend = new ZSocket(context, ZSocketType.SUB))
			using (var backend = new ZSocket(context, ZSocketType.XPUB))
			{
				// Subscribe to every single topic from publisher
				frontend.Bind("tcp://*:5557");
				frontend.SubscribeAll();

				backend.Bind("tcp://*:5558");

				// Store last instance of each topic in a cache
				var cache = new HashSet<LVCacheItem>();

				// We route topic updates from frontend to backend, and
				// we handle subscriptions by sending whatever we cached,
				// if anything:
				var p = ZPollItem.CreateReceiver();
				while (true)
				{
					// Any new topic data we cache and then forward
					if (frontend.PollIn(p, out var msg, out var error, TimeSpan.FromMilliseconds(1)))
						using (msg)
						{
							var topic = msg[0].ReadString();
							var current = msg[1].ReadString();

							var previous = cache.FirstOrDefault(item => topic == item.Topic);
							if (previous != null)
								cache.Remove(previous);
							cache.Add(new()
								{ Topic = topic, Current = current });

							backend.Send(msg);
						}
					else
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						if (error != ZError.EAGAIN)
							throw new ZException(error);
					}

					// When we get a new subscription, we pull data from the cache:
					if (backend.PollIn(p, out msg, out error, TimeSpan.FromMilliseconds(1)))
						using (msg)
						{
							// Event is one byte 0=unsub or 1=sub, followed by topic
							var subscribe = msg[0].ReadAsByte();
							if (subscribe == 0x01)
							{
								var topic = msg[0].ReadString();
								var previous = cache.FirstOrDefault(item => topic == item.Topic);
								if (previous != null)
								{
									Console.WriteLine("Sending cached topic {0}", topic);
									backend.SendMore(ZFrame.Create(previous.Topic));
									backend.Send(ZFrame.Create(previous.Current));
								}
								else
									Console.WriteLine("Failed to send cached topic {0}!", topic);
							}
						}
					else
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						if (error != ZError.EAGAIN)
							throw new ZException(error);
					}
				}
			}
		}

		class LVCacheItem 
		{
			public string Topic;
			public string Current;

			public override int GetHashCode()
				=> Topic.GetHashCode();
		}
	}
}