﻿using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void EAgain(string[] args)
		{
			//
			// Shows how to provoke EAGAIN when reaching HWM
			//
			// Author: metadings
			//

			using (var context = new ZContext())
			using (var mailbox = new ZSocket(context, ZSocketType.DEALER))
			{
				mailbox.SendHighWatermark = 4;
				mailbox.SendTimeout = TimeSpan.Zero;
				mailbox.Linger = TimeSpan.Zero;
				mailbox.Connect("tcp://127.0.0.1:9876");

				for (var count = 0; count < 10; ++count)
				{
					Console.WriteLine("Sending {0}. message", count);

					using (var outgoing = ZFrame.Create($"message {count}"))
					{
						if (!mailbox.Send(outgoing, ZSocketFlags.DontWait, out var error))
						{
							if (error == ZError.EAGAIN)
							{
								Console.WriteLine(new ZException(error));
								break;

								/* Usually when reaching EAGAIN, I would do
								Thread.Sleep(1);
								continue; /**/
							}
							if (error == ZError.ETERM)
								return;	// Interrupted
							throw new ZException(error);
						}
					}
				}
			}
		}
	}
}