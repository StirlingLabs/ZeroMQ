using System;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		//
		// Suicidal Snail
		//
		// Author: metadings
		//

		static readonly TimeSpan SuiSnail_MAX_ALLOWED_DELAY = TimeSpan.FromMilliseconds(1000);

		static void SuiSnail_Subscriber(ZContext context, ZSocket backend, CancellationTokenSource canceller, object[] args)
		{
			// This is our subscriber. It connects to the publisher and subscribes
			// to everything. It sleeps for a short time between messages to
			// simulate doing too much work. If a message is more than one second
			// late, it croaks.

			using (var subscriber = new ZSocket(context, ZSocketType.SUB))
			{
				// Subscribe to everything
				subscriber.SubscribeAll();
				subscriber.Connect("tcp://127.0.0.1:5556");

				ZFrame incoming;
				var rnd = new Random();
				while (!canceller.IsCancellationRequested)
				{
					// Get and process messages
					if (null != (incoming = subscriber.ReceiveFrame(out var error)))
					{
						var terms = incoming.ReadString();
						Console.WriteLine(terms);
						var clock = DateTime.Parse(terms);

						// Suicide snail logic
						if (DateTime.UtcNow - clock > SuiSnail_MAX_ALLOWED_DELAY)
						{
							Console.WriteLine("E: subscriber cannot keep up, aborting");
							break;
						}

						// Work for 1 msec plus some random additional time
						Thread.Sleep(1 + rnd.Next(200));
					}
					else
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}
				}

				backend.Send(ZFrame.Create("gone and died"));
			}
		}

		static void SuiSnail_Publisher(ZContext context, ZSocket backend, CancellationTokenSource canceller, object[] args)
		{
			// This is our publisher task. It publishes a time-stamped message to its
			// PUB socket every millisecond:

			using (var publisher = new ZSocket(context, ZSocketType.PUB))
			{
				// Prepare publisher
				publisher.Bind("tcp://*:5556");

				ZFrame signal;
				while (!canceller.IsCancellationRequested)
				{
					// Send current clock (msecs) to subscribers
					if (!publisher.Send(ZFrame.Create(DateTime.UtcNow.ToString("s")), out var error))
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}

					if (null == (signal = backend.ReceiveFrame(ZSocketFlags.DontWait, out error)))
					{
						if (error == ZError.EAGAIN)
						{
							Thread.Sleep(1);	// wait 1 ms
							continue;
						}
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}

					// Suicide snail logic
					using (signal) break;
				}
			}
		}

		public static void SuiSnail(string[] args)
		{
			// The main task simply starts a client and a server, and then
			// waits for the client to signal that it has died:

			using (var context = new ZContext())
			using (var pubpipe = new ZActor(context, SuiSnail_Publisher))
			using (var subpipe = new ZActor(context, SuiSnail_Subscriber))
			{
				pubpipe.Start();
				subpipe.Start();

				subpipe.Frontend.ReceiveFrame();
				pubpipe.Frontend.Send(ZFrame.Create("break"));

				// wait for the Thread (you'll see how fast it is)
				pubpipe.Join(5000);
			}
		}
	}
}