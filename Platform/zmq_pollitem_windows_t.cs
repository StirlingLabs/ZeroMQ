using System;
using System.Runtime.InteropServices;

namespace ZeroMQ.lib
{
	[StructLayout(LayoutKind.Sequential)]
	public struct zmq_pollitem_windows_t // : zmq_pollitem_i
	{
		private IntPtr socketPtr;
		private IntPtr fileDescriptor; // Windows is an size_t
		private short events;
		private short readyEvents;

		public zmq_pollitem_windows_t(IntPtr socket, ZPoll pollEvents)
		{
			if (socket == default)
			{
				throw new ArgumentException("Expected a valid socket handle.", nameof(socket));
			}

			socketPtr = socket;
			fileDescriptor = default;
			events = (short)pollEvents;
			readyEvents = 0;
		}

		public IntPtr SocketPtr
		{
			get => socketPtr;
			set => socketPtr = value;
		}

		public IntPtr FileDescriptor
		{
			get => fileDescriptor;
			set => fileDescriptor = value;
		}

		public short Events
		{
			get => events;
			set => events = value;
		}

		public short ReadyEvents
		{
			get => readyEvents;
			set => readyEvents = value;
		}
	}
}