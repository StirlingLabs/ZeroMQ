﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using ZeroMQ;

namespace Examples
{
    public static class MdpCommon
    {
        public const int HEARTBEAT_LIVENESS = 3;

        public static readonly TimeSpan HEARTBEAT_DELAY = TimeSpan.FromMilliseconds(2500);
        public static readonly TimeSpan RECONNECT_DELAY = TimeSpan.FromMilliseconds(2500);

        public static readonly TimeSpan HEARTBEAT_INTERVAL = TimeSpan.FromMilliseconds(2500);
        public static readonly TimeSpan HEARTBEAT_EXPIRY =
            TimeSpan.FromMilliseconds(HEARTBEAT_INTERVAL.TotalMilliseconds * HEARTBEAT_LIVENESS);

        public static readonly string MDPW_WORKER = "MDPW01";
        public static readonly string MDPC_CLIENT = "MDPC01";

        //public static readonly string READY = "001";
        //public static readonly string REQUEST = "002";
        //public static readonly string REPLY = "003";
        //public static readonly string HEARTBEAT = "004";
        //public static readonly string DISCONNECT = "005";

        public enum MdpwCmd : byte
        {
            READY = 1,
            REQUEST = 2,
            REPLY = 3,
            HEARTBEAT = 4,
            DISCONNECT = 5
        }
    }

    public static class MdpExtensions
    {
        public static bool StrHexEq(this ZFrame zfrm, MdpCommon.MdpwCmd cmd)
            => zfrm.ToString().ToMdCmd().Equals(cmd);

        /// <summary>
        /// Parse hex value to MdpwCmd, if parsing fails, return 0
        /// </summary>
        /// <param name="hexval">hex string</param>
        /// <returns>MdpwCmd, return 0 if parsing failed</returns>
        public static MdpCommon.MdpwCmd ToMdCmd(this string hexval)
        {
            try
            {
                var cmd = (MdpCommon.MdpwCmd)byte.Parse(hexval, NumberStyles.AllowHexSpecifier);
                return cmd;
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        public static string ToHexString(this MdpCommon.MdpwCmd cmd)
            => cmd.ToString("X");

        // if you dont wanna see utc timeshift, remove zzz and use DateTime.UtcNow instead

        [StringFormatMethod("str")]
        [Conditional("TRACE")]
        public static void Trace(string? str)
        {
            if (str == null) return;
            if (Program.Quiet) return;
            var err = Console.Error;
            err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            err.Flush();
        }

        [StringFormatMethod("str")]
        [Conditional("TRACE")]
        public static void Trace(this string? str, params object[]? args)
        {
            if (str == null) return;
            if (Program.Quiet) return;
            var err = Console.Error;
            if (args == null)
                err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            else
            {
                err.Write($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - ");
                err.WriteLine(str, args);
            }
            err.Flush();
        }

        [Conditional("TRACE")]
        public static void Trace(FormattableString? str)
        {
            if (str == null) return;
            if (Program.Quiet) return;
            var err = Console.Error;
            err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            err.Flush();
        }

        public static void Info(FormattableString? str)
        {
            if (str == null) return;
            if (Program.Quiet) return;
            var err = Console.Error;
            err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            err.Flush();
        }

        [StringFormatMethod("str")]
        public static void Info(this string? str)
        {
            if (str == null) return;
            //if (Program.Quiet) return;
            var err = Console.Error;
            err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            err.Flush();
        }

        [StringFormatMethod("str")]
        public static void Info(this string? str, params object[]? args)
        {
            if (str == null) return;
            //if (Program.Quiet) return;
            var err = Console.Error;
            if (args == null)
                err.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - {str}");
            else
            {
                err.Write($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss:fffffff zzz}] - ");
                err.WriteLine(str, args);
            }
            err.Flush();
        }

        /// <summary>
        /// Based on zmsg_dump 
        /// https://github.com/imatix/zguide/blob/f94e8995a5e02d843434ace904a7afc48e266b3f/articles/src/multithreading/zmsg.c
        /// </summary>
        /// <param name="zmsg"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void DumpZmsg(this ZMessage? zmsg, string format = null, params object[] args)
        {
            if (Program.Quiet) return;
            if (!string.IsNullOrWhiteSpace(format))
                string.Format(format, args).Trace();
            using var dmsg = zmsg.Clone();
            foreach (var zfrm in dmsg)
                zfrm.DumpZfrm();
        }

        public static void DumpZfrm(this ZFrame zfrm, string format = null, params object[] args)
        {
            if (Program.Quiet) return;
            if (!string.IsNullOrWhiteSpace(format))
                string.Format(format, args).Trace();

            var data = zfrm.Read();
            var size = zfrm.Length;

            // Dump the message as text or binary
            var isText = true;
            for (var i = 0; i < size; i++)
            {
                if (data[i] < 32 || data[i] > 127)
                    isText = false;
            }
            var datastr = isText ? Encoding.UTF8.GetString(data) : data.ToHexString();
            Trace($"\tD: [{size,3:D3}]:{datastr}");
        }
    }
}
