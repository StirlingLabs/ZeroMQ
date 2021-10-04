namespace ZeroMQ
{
    public enum ZContextOption
    {
        IO_THREADS = 1,
        MAX_SOCKETS = 2,
        SOCKET_LIMIT = 3,
        THREAD_PRIORITY = 3,
        THREAD_SCHED_POLICY = 4,
        MSG_T_SIZE = 6,
        THREAD_AFFINITY_CPU_ADD = 7,
        THREAD_AFFINITY_CPU_REMOVE = 8,
        THREAD_NAME_PREFIX = 8,
        IPV6 = 42 // in zmq.h ZMQ_IPV6 is in the socket options section
    }
}
