using System.Runtime.InteropServices;

namespace ZeroMQ.Monitoring
{
	using System;
	using lib;

	/// <summary>
	/// Provides data for <see cref="ZMonitor.Connected"/>, <see cref="ZMonitor.Listening"/>, <see cref="ZMonitor.Accepted"/>, <see cref="ZMonitor.Closed"/> and <see cref="ZMonitor.Disconnected"/> events.
	/// </summary>
	public class ZMonitorFileDescriptorEventArgs : ZMonitorEventArgs
	{
		internal ZMonitorFileDescriptorEventArgs(ZMonitor monitor, ZMonitorEventData data)
			: base(monitor, data)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				this.FileDescriptor_Windows = new IntPtr(data.EventValue);
			}
			else
			{
				this.FileDescriptor_Posix = data.EventValue;
			}
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