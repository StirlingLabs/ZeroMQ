using System;
using ZeroMQ;

namespace Examples
{
	internal partial class Program
	{
		public static void Z85Encode(string[] args)
		{
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
				args = new[] { "Hello World!" };

			var txt = args[0];
			var encoded = Z85.Encode(txt);
			Console.WriteLine("{0}: {1}", txt, encoded);
		}

		public static void Z85Decode(string[] args)
		{
			//
			// Author: metadings
			//

			if (args == null || args.Length < 1)
				args = new[] { "nm=QNzY&b1A+]nf" };

			var txt = args[0];
			var decoded = Z85.Decode(txt);
			Console.WriteLine("{0}: {1}", txt, decoded);
		}
	}
}

