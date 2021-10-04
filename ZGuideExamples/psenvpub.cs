using System;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void PSEnvPub(string[] args)
		{
			//
			// Pubsub envelope publisher
			//
			// Author: metadings
			//

			// Prepare our context and publisher
			using (var context = new ZContext())
			using (var publisher = new ZSocket(context, ZSocketType.PUB))
			{
				publisher.Linger = TimeSpan.Zero;
				publisher.Bind("tcp://*:5563");

				var published = 0;
				while (true)
				{
					// Write two messages, each with an envelope and content

					using (var message = ZMessage.Create())
					{
						published++;
						message.Add(ZFrame.Create($"A {published}"));
						message.Add(ZFrame.Create(" We don't like to see this."));
						Thread.Sleep(1000);

						Console_WriteZMessage("Publishing ", message);
						publisher.Send(message);
					}

					using (var message = ZMessage.Create())
					{
						published++;
						message.Add(ZFrame.Create($"B {published}"));
						message.Add(ZFrame.Create(" We do like to see this."));
						Thread.Sleep(1000);

						Console_WriteZMessage("Publishing ", message);
						publisher.Send(message);
					}
				}
			}
		}
	}
}