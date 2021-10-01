using System;
using System.Runtime.InteropServices;

namespace ZeroMQ.Monitoring
{
    /// <summary>
    /// Provides data for <see cref="ZMonitor.Connected"/>, <see cref="ZMonitor.Listening"/>, <see cref="ZMonitor.Accepted"/>, <see cref="ZMonitor.Closed"/> and <see cref="ZMonitor.Disconnected"/> events.
    /// </summary>
    public class ZMonitorFileDescriptorEventArgs : ZMonitorEventArgs
    {
        internal ZMonitorFileDescriptorEventArgs(ZMonitor monitor, ZMonitorEventData data)
            : base(monitor, data)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                FileDescriptor_Windows = new(data.EventValue);
            else
                FileDescriptor_Posix = data.EventValue;
        }

        /// <summary>
        /// Gets the monitor descriptor (Posix)
        /// </summary>
        public int FileDescriptor_Posix { get; private set; }

        /// <summary>
        /// Gets the monitor descriptor (Windows)
        /// </summary>
        public IntPtr FileDescriptor_Windows { get; private set; }
    }
}
