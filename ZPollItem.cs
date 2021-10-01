namespace ZeroMQ
{
	public class ZPollItem
	{
		public ZPoll Events;

		public ZPoll ReadyEvents;

		public delegate bool ReceiveDelegate(ZSocket socket, out ZMessage message, out ZError error);

		public ReceiveDelegate ReceiveMessage;

		public static bool DefaultReceiveMessage(ZSocket socket, out ZMessage message, out ZError error)
		{
			message = null;
			return socket.ReceiveMessage(ref message, out error);
		}

		public delegate bool SendDelegate(ZSocket socket, ZMessage message, out ZError error);

		public SendDelegate SendMessage;

		public static bool DefaultSendMessage(ZSocket socket, ZMessage message, out ZError error)
			=> socket.Send(message, out error);

		protected ZPollItem(ZPoll events)
			=> Events = events;

		public static ZPollItem Create(ReceiveDelegate receiveMessage)
			=> Create(receiveMessage, null);

		public static ZPollItem CreateSender(SendDelegate sendMessage)
			=> Create(null, sendMessage);

		public static ZPollItem Create(ReceiveDelegate receiveMessage, SendDelegate sendMessage)
		{
			var pollItem = new ZPollItem((receiveMessage != null ? ZPoll.In : ZPoll.None) | (sendMessage != null ? ZPoll.Out : ZPoll.None));
			pollItem.ReceiveMessage = receiveMessage;
			pollItem.SendMessage = sendMessage;
			return pollItem;
		}

		public static ZPollItem CreateReceiver()
			=> Create(DefaultReceiveMessage, null);

		public static ZPollItem CreateSender()
			=> Create(null, DefaultSendMessage);

		public static ZPollItem CreateReceiverSender()
			=> Create(DefaultReceiveMessage, DefaultSendMessage);
	}
}