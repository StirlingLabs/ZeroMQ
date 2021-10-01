using System;
using System.Runtime.InteropServices;

namespace ZeroMQ.lib
{
    internal sealed partial class DispoIntPtr : IDisposable
    {
        private delegate DispoIntPtr AllocStringNativeDelegate(string str, out int byteCount);

        private static readonly AllocStringNativeDelegate AllocStringNative = Ansi.AllocStringNative;

        /* static DispoIntPtr() {
          // Platform.SetupPlatformImplementation(typeof(DispoIntPtr));
        } */

        public static DispoIntPtr Alloc(int size)
        {
            var dispPtr = new DispoIntPtr();
            dispPtr._ptr = Marshal.AllocHGlobal(size);
            dispPtr.isAllocated = true;
            return dispPtr;
        }

        public static DispoIntPtr AllocString(string str)
            => AllocString(str, out var byteCount);

        public static DispoIntPtr AllocString(string str, out int byteCount)
            => AllocStringNative(str, out byteCount);

        public static implicit operator IntPtr(DispoIntPtr dispoIntPtr)
            => dispoIntPtr?._ptr ?? default;

        public static unsafe explicit operator void*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (void*)dispoIntPtr._ptr;

        public static unsafe explicit operator byte*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (byte*)dispoIntPtr._ptr;

        public static unsafe explicit operator sbyte*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (sbyte*)dispoIntPtr._ptr;

        public static unsafe explicit operator short*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (short*)dispoIntPtr._ptr;

        public static unsafe explicit operator ushort*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (ushort*)dispoIntPtr._ptr;

        public static unsafe explicit operator char*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (char*)dispoIntPtr._ptr;

        public static unsafe explicit operator int*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (int*)dispoIntPtr._ptr;

        public static unsafe explicit operator uint*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (uint*)dispoIntPtr._ptr;

        public static unsafe explicit operator long*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (long*)dispoIntPtr._ptr;

        public static unsafe explicit operator ulong*(DispoIntPtr dispoIntPtr)
            => dispoIntPtr == null ? null : (ulong*)dispoIntPtr._ptr;

        private bool isAllocated;

        private IntPtr _ptr;

        public IntPtr Ptr => _ptr;

        ~DispoIntPtr()
            => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            // TODO: instance ThreadStatic && do ( o == null ? return : ( lock(o, ms), check threadId, .. ) ) 
            var handle = _ptr;
            if (handle == default)
                return;

            if (isAllocated)
            {
                Marshal.FreeHGlobal(handle);
                isAllocated = false;
            }
            _ptr = default;
        }

        /* public void ReAlloc(long size) {
          _ptr = Marshal.ReAllocHGlobal(_ptr, new IntPtr(size));
        } */
    }
}
