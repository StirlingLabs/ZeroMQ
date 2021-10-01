﻿namespace ZeroMQ
{
    public enum ZSocketOption
    {
        AFFINITY = 4,
        IDENTITY = 5,
        SUBSCRIBE = 6,
        UNSUBSCRIBE = 7,
        RATE = 8,
        RECOVERY_IVL = 9,
        SNDBUF = 11,
        RCVBUF = 12,
        RCVMORE = 13,
        FD = 14,
        EVENTS = 15,
        TYPE = 16,
        LINGER = 17,
        RECONNECT_IVL = 18,
        BACKLOG = 19,
        RECONNECT_IVL_MAX = 21,
        MAX_MSG_SIZE = 22,
        SNDHWM = 23,
        RCVHWM = 24,
        MULTICAST_HOPS = 25,
        RCVTIMEO = 27,
        SNDTIMEO = 28,
        LAST_ENDPOINT = 32,
        ROUTER_MANDATORY = 33,
        TCP_KEEPALIVE = 34,
        TCP_KEEPALIVE_CNT = 35,
        TCP_KEEPALIVE_IDLE = 36,
        TCP_KEEPALIVE_INTVL = 37,
        IMMEDIATE = 39,
        XPUB_VERBOSE = 40,
        ROUTER_RAW = 41,
        IPV6 = 42,
        MECHANISM = 43,
        PLAIN_SERVER = 44,
        PLAIN_USERNAME = 45,
        PLAIN_PASSWORD = 46,
        CURVE_SERVER = 47,
        CURVE_PUBLICKEY = 48,
        CURVE_SECRETKEY = 49,
        CURVE_SERVERKEY = 50,
        PROBE_ROUTER = 51,
        REQ_CORRELATE = 52,
        REQ_RELAXED = 53,
        CONFLATE = 54,
        ZAP_DOMAIN = 55,
        ROUTER_HANDOVER = 56,
        TOS = 57,
        CONNECT_RID = 61,
        GSSAPI_SERVER = 62,
        GSSAPI_PRINCIPAL = 63,
        GSSAPI_SERVICE_PRINCIPAL = 64,
        GSSAPI_PLAINTEXT = 65,
        HANDSHAKE_IVL = 66,
        IDENTITY_FD = 67,
        SOCKS_PROXY = 68,
        XPUB_NODROP = 69,
        BLOCKY = 70,
        XPUB_MANUAL = 71,
        XPUB_WELCOME_MSG = 72,
        STREAM_NOTIFY = 73,
        INVERT_MATCHING = 74,
        HEARTBEAT_IVL = 75,
        HEARTBEAT_TTL = 76,
        HEARTBEAT_TIMEOUT = 77,
        XPUB_VERBOSE_UNSUBSCRIBE = 78,
        CONNECT_TIMEOUT = 79,
        TCP_RETRANSMIT_TIMEOUT = 80,
        THREAD_SAFE = 81,

        /* Deprecated options and aliases */
        TCP_ACCEPT_FILTER = 38,
        IPC_FILTER_PID = 58,
        IPC_FILTER_UID = 59,
        IPC_FILTER_GID = 60,
        IPV4_ONLY = 31,
        DELAY_ATTACH_ON_CONNECT = 39, // IMMEDIATE,
        NOBLOCK = 1, // DONTWAIT,
        FAIL_UNROUTABLE = 33, // ROUTER_MANDATORY,
        ROUTER_BEHAVIOR = 33, // ROUTER_MANDATORY,
    }
}
