using System;

namespace ZeroMQ.Monitoring
{
	public struct ZMonitorEventData
	{
		public ZMonitorEvents Event;
		public Int32 EventValue;
		public String Address;
	}
}