using System;
using System.Threading;
using Examples.MDWrkApi;
using ZeroMQ;

namespace Examples
{
	// Let us build this source without creating a library

	internal static partial class Program
	{
		//  Majordomo Protocol worker example
		//  Uses the mdwrk API to hide all MDP aspects
		public static void MDWorker(string[] args)
		{
			var cts = new CancellationTokenSource();
			Console.CancelKeyPress += (s, ea) =>
			{
				ea.Cancel = true;
				cts.Cancel();
			};

			using (var session = new MajordomoWorker("tcp://127.0.0.1:5555", "echo", Verbose))
			{
				ZMessage? reply = null;
				while (true)
				{
					var request = session.Recv(reply, cts);
					if (request == null)
						break; // worker was interrupted
					reply = request; // Echo is complex
				}
			}
		}
	}
}
