using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroMQ.lib
{
    internal sealed partial class ZSafeHandle : IDisposable
    {
        public static class Ansi
        {
            internal static ZSafeHandle AllocStringNative(string str, out nuint byteCount)
            {
                // use built in allocation
                var p = new ZSafeHandle
                {
                    _ptr = Marshal.StringToHGlobalAnsi(str),
                    _isAllocated = true
                };

                byteCount = (uint)Encoding.Default.GetByteCount(str);

                return p;
            }
        }
    }
}
