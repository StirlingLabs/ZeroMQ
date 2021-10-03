using System;
using System.Diagnostics;
using System.Threading;

namespace ZeroMQ
{
    public abstract class ZThread : IDisposable
    {
        public CancellationTokenSource? Canceller { get; protected set; }

        protected Thread? Thread;

        protected bool Disposed;

        /// <summary>
        /// Finalizes an instance of the <see cref="ZThread"/> class.
        /// </summary>
        ~ZThread()
            => Dispose(false);

        /// <summary>
        /// Gets a value indicating whether the device loop is running.
        /// </summary>
        public bool IsCancellationRequested => Canceller?.IsCancellationRequested ?? false;

        public virtual void Start()
        {
            var canceller = new CancellationTokenSource();
            Start(canceller);
        }

        /// <summary>
        /// Start the device in the current thread.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The <see cref="ZThread"/> has already been disposed.</exception>
        public virtual void Start(CancellationTokenSource canceller)
        {
            EnsureNotDisposed();

            Canceller = canceller;

            Thread ??= new(Run);

            Thread.Start();
        }

        /// <summary>
        /// Blocks the calling thread until the device terminates.
        /// </summary>
        public virtual void Join()
        {
            EnsureNotDisposed();

            if (Thread == null) return;
            Thread.Join();
        }

        /// <summary>
        /// Blocks the calling thread until the device terminates.
        /// </summary>
        public virtual bool Join(int ms)
        {
            EnsureNotDisposed();

            if (Thread == null) return false;
            return Thread.Join(ms);
        }

        /// <summary>
        /// Blocks the calling thread until the device terminates or the specified time elapses.
        /// </summary>
        /// <param name="timeout">
        /// A <see cref="TimeSpan"/> set to the amount of time to wait for the device to terminate.
        /// </param>
        /// <returns>
        /// true if the device terminated; false if the device has not terminated after
        /// the amount of time specified by <paramref name="timeout"/> has elapsed.
        /// </returns>
        public virtual bool Join(TimeSpan timeout)
        {
            EnsureNotDisposed();

            if (Thread == null) return false;
            return Thread.Join(timeout);
        }

        /// <summary>
        /// Stop the device in such a way that it can be restarted.
        /// </summary>
        public virtual void Stop()
        {
            EnsureNotDisposed();

            if (Thread != null)
            {
                Debug.Assert(Canceller != null);
                Canceller.Cancel();
            }
        }

        /// <summary>
        /// Stop the device and safely terminate the underlying sockets.
        /// </summary>
        public virtual void Close()
        {
            EnsureNotDisposed();

            Stop();

            if (Thread == null) return;
            Thread.Join();
        }

        /// <summary>
        /// Releases all resources used by the current instance, including the frontend and backend sockets.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Run();

        /// <summary>
        /// Stops the device and releases the underlying sockets. Optionally disposes of managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing) Close();
            Disposed = true;
        }

        protected void EnsureNotDisposed()
        {
            if (Disposed) throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
