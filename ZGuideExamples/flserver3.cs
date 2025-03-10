﻿using System;
using ZeroMQ;

namespace Examples
{
	internal static partial class Program
	{
		public static void FLServer3(string[] args)
		{
			//
			// Freelance server - Model 3
			// Uses an ROUTER/ROUTER socket but just one thread
			//
			// Author: metadings
			//

			// Prepare server socket with predictable identity
			var bind_endpoint = "tcp://*:5555";
			var connect_endpoint = "tcp://127.0.0.1:5555";

			using (var context = new ZContext())
			using (var server = new ZSocket(context, ZSocketType.ROUTER))
			{
				Console.CancelKeyPress += (s, ea) =>
				{
					ea.Cancel = true;
					context.Shutdown();
				};

				server.IdentityString = connect_endpoint;
				server.Bind(bind_endpoint);
				Console.WriteLine("I: service is ready as {0}", bind_endpoint);

				ZError? error;
				ZMessage? request;
				while (true)
				{
					if (null == (request = server.ReceiveMessage(out error)))
					{
						if (error == ZError.ETERM)
							break;	// Interrupted
						throw new ZException(error);
					}
					using (var response = ZMessage.Create())
					{
						ZFrame identity;

						using (request)
						{
							if (Verbose) Console_WriteZMessage("Receiving", request);

							// Frame 0: identity of client
							// Frame 1: PING, or client control frame
							// Frame 2: request body

							identity = request.Pop();

							var control = request.Pop();
							var controlMessage = control.ReadString();

							if (controlMessage == "PING")
							{
								control.Dispose();
								response.Add(ZFrame.Create("PONG"));
							}
							else
							{
								response.Add(control);
								response.Add(ZFrame.Create("OK"));
							}
						}

						response.Prepend(identity);

						if (Verbose) Console_WriteZMessage("Sending  ", response);
						if (!server.Send(response, out error))
						{
							if (error == ZError.ETERM)
								break;	// Interrupted
							throw new ZException(error);
						}
					}
				}
				if (error == ZError.ETERM)
					Console.WriteLine("W: interrupted");
			}
		}

	}
}