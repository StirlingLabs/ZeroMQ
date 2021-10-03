using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace ZeroMQ
{
    public delegate void ZAction0(ZSocket backend, CancellationTokenSource? canceller, object[] args);

    public delegate void ZAction(ZContext context, ZSocket backend, CancellationTokenSource? canceller, object[] args);

    public class ZActor : ZThread
    {
        private static long InProcEndpointCounter;

        public ZContext Context { get; protected set; }

        public string Endpoint { get; protected set; }

        public ZAction? Action { get; protected set; }

        public ZAction0? Action0 { get; protected set; }

        public object[] Arguments { get; protected set; }

        public ZSocket? Backend { get; protected set; }

        public ZSocket? Frontend { get; protected set; }

        public ZActor(ZContext context, ZAction action, params object[] args)
            : this(context, default!, action, args)
            => Endpoint = GeneratePrivateInProcEndpoint();

        private string GeneratePrivateInProcEndpoint()
        {
            string ep;
#if NETSTANDARD2_0
            var bytesPool = ArrayPool<byte>.Shared;
            byte[] rnd = bytesPool.Rent(8);
            try
            {
                using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(rnd);
                ep = $"inproc://{Interlocked.Increment(ref InProcEndpointCounter):X16}{MemoryMarshal.Read<long>(rnd):X16}";
            }
            finally
            {
                bytesPool.Return(rnd);
            }
#else
            Span<byte> rnd = stackalloc byte[8];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(rnd);
            ep = $"inproc://{Interlocked.Increment(ref InProcEndpointCounter):X16}{MemoryMarshal.Read<long>(rnd):X16}";
#endif
            return ep;
        }

        private ZActor(ZContext context, string endpoint, ZAction action, params object[] args)
        {
            Context = context;

            Endpoint = endpoint;
            Action = action;
            Arguments = args;
        }

        /// <summary>
        /// You are using ZContext.Current!
        /// </summary>
        public ZActor(ZAction0 action, params object[] args)
            : this(default!, action, args)
            => Endpoint = GeneratePrivateInProcEndpoint();

        /// <summary>
        /// You are using ZContext.Current!
        /// </summary>
        public ZActor(string endpoint, ZAction0 action, params object[] args)
        {
            Context = ZContext.Current;

            Endpoint = endpoint;
            Action0 = action;
            Arguments = args;
        }

        protected override void Run()
        {
            using (Backend = ZSocket.Create(Context, ZSocketType.PAIR))
            {
                Backend.Bind(Endpoint);

                Action0?.Invoke(Backend, Canceller, Arguments);

                Action?.Invoke(Context, Backend, Canceller, Arguments);
            }
        }

        public override void Start()
        {
            base.Start();

            if (Frontend != null)
                return;

            Frontend = ZSocket.Create(Context, ZSocketType.PAIR);
            Frontend.Connect(Endpoint);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;

            if (Frontend != null)
            {
                Frontend.Dispose();
                Frontend = null;
            }

            if (Backend != null)
            {
                Backend.Dispose();
                Backend = null;
            }
        }
    }
}
