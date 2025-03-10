﻿using System;
using System.Text;
using System.Threading;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		static void AsyncSrv_Client(ZContext context, int i) 
		{
			//
			// Asynchronous client-to-server (DEALER to ROUTER)
			//
			// While this example runs in a single process, that is to make
			// it easier to start and stop the example. Each task has its own
			// context and conceptually acts as a separate process.
			//
			// Author: metadings
			//

			// This is our client task
			// It connects to the server, and then sends a request once per second
			// It collects responses as they arrive, and it prints them out. We will
			// run several client tasks in parallel, each with a different random ID.

			using (var client = new ZSocket(context, ZSocketType.DEALER))
			{
				// Set identity to make tracing easier
				client.Identity = Encoding.UTF8.GetBytes("CLIENT" + i);
				// Connect
				client.Connect("tcp://127.0.0.1:5570");

				ZError? error;
				var poll = ZPollItem.CreateReceiver();

				var requests = 0;
				while (true)
				{
					// Tick once per second, pulling in arriving messages
					for (var centitick = 0; centitick < 100; ++centitick)
					{
						if (!client.PollIn(poll, out var incoming, out error, TimeSpan.FromMilliseconds(10)))
						{
							if (error == ZError.EAGAIN)
							{
								Thread.Sleep(1);
								continue;
							}
							if (error == ZError.ETERM)
								return;	// Interrupted
							throw new ZException(error);
						}
						using (incoming)
						{
							var messageText = incoming[0].ReadString();
							Console.WriteLine("[CLIENT{0}] {1}", i, messageText);
						}
					}
					using var outgoing = ZMessage.Create();
					outgoing.Add(ZFrame.Create(client.Identity));
					outgoing.Add(ZFrame.Create("request " + ++requests));

					if (client.Send(outgoing, out error))
						continue;
					if (error == ZError.ETERM)
						return;	// Interrupted
					throw new ZException(error);
				}
			}
		}

		static void AsyncSrv_ServerTask(ZContext context) 
		{
			// This is our server task.
			// It uses the multithreaded server model to deal requests out to a pool
			// of workers and route replies back to clients. One worker can handle
			// one request at a time but one client can talk to multiple workers at
			// once.

			using (var frontend = new ZSocket(context, ZSocketType.ROUTER))
			using (var backend = new ZSocket(context, ZSocketType.DEALER))
			{
				// Frontend socket talks to clients over TCP
				frontend.Bind("tcp://*:5570");
				// Backend socket talks to workers over inproc
				backend.Bind("inproc://backend");

				// Launch pool of worker threads, precise number is not critical
				for (var i = 0; i < 5; ++i)
				{
					var j = i; new Thread(() => AsyncSrv_ServerWorker(context, j)).Start();
				}

				// Connect backend to frontend via a proxy
				if (!ZContext.Proxy(frontend, backend, out var error))
				{
					if (error == ZError.ETERM)
						return;	// Interrupted
					throw new ZException(error);
				}
			}
		}

		static void AsyncSrv_ServerWorker(ZContext context, int i) 
		{
			// Each worker task works on one request at a time and sends a random number
			// of replies back, with random delays between replies:

			using (var worker = new ZSocket(context, ZSocketType.DEALER))
			{
				worker.Connect("inproc://backend");

				ZMessage? request;
				var rnd = new Random();

				while (true)
				{
					if (null == (request = worker.ReceiveMessage(out var error)))
					{
						if (error == ZError.ETERM)
							return;	// Interrupted
						throw new ZException(error);
					}
					using (request)
					{
						// The DEALER socket gives us the reply envelope and message
						var identity = request[1].ReadString();
						var content = request[2].ReadString();

						// Send 0..4 replies back
						var replies = rnd.Next(5);
						for (var reply = 0; reply < replies; ++reply)
						{
							// Sleep for some fraction of a second
							Thread.Sleep(rnd.Next(1000) + 1);

							using (var response = ZMessage.Create())
							{
								response.Add(ZFrame.Create(identity));
								response.Add(ZFrame.Create(content));

								if (!worker.Send(response, out error))
								{
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

		public static void AsyncSrv(string[] args)
		{
			// The main thread simply starts several clients and a server, and then
			// waits for the server to finish.

			using (var context = new ZContext())
			{
				for (var i = 0; i < 5; ++i)
				{
					var j = i; new Thread(() => AsyncSrv_Client(context, j)).Start();
				}
				new Thread(() => AsyncSrv_ServerTask(context)).Start();

				// Run for 5 seconds then quit
				Thread.Sleep(5 * 1000);
			}
		}
	}
}