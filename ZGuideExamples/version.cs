using System;
using ZeroMQ.lib;

namespace Examples
{
	internal static partial class Program
	{
		public static void Version(string[] args)
		{
			//
			// Report 0MQ version
			//
			// Author: metadings
			//

			// Console.WriteLine(zmq.Version);

			zmq.version(out var major, out var minor, out var patch);
			Console.WriteLine("{0}.{1}.{2}", major, minor, patch);
		}
	}
}