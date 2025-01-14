﻿using System;
using System.Runtime.InteropServices;

namespace ZeroMQ.lib
{
    [StructLayout(LayoutKind.Sequential)]
    public struct zmq_pollitem_posix_t // : zmq_pollitem_i
    {
        private IntPtr socketPtr;
        private int fileDescriptor; // POSIX fd is an Int32
        private short events;
        private short readyEvents;

        public zmq_pollitem_posix_t(IntPtr socket, ZPollEventTypes pollEvents)
        {
            if (socket == default)
                throw new ArgumentException("Expected a valid socket handle.", nameof(socket));

            socketPtr = socket;
            fileDescriptor = 0;
            events = (short)pollEvents;
            readyEvents = (short)ZPollEventTypes.None;
        }

        public IntPtr SocketPtr
        {
            get => socketPtr;
            set => socketPtr = value;
        }

        public int FileDescriptor
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
