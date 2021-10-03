using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ZeroMQ.lib
{
    internal sealed partial class ZSafeHandle : IDisposable
    {
        private static readonly ConcurrentStack<ZSafeHandle> freeHandles = new();

        private delegate ZSafeHandle AllocStringNativeDelegate(string str, out nuint byteCount);

        private static readonly AllocStringNativeDelegate AllocStringNative = Ansi.AllocStringNative;

        public static ZSafeHandle Alloc(nuint size)
        {
            var handle = freeHandles.TryPop(out var h) ? h : new();
            handle._ptr = Marshal.AllocHGlobal((nint)size);
            handle._isAllocated = true;
            return handle;
        }

        public static ZSafeHandle AllocString(string str)
            => AllocString(str, out var byteCount);

        public static ZSafeHandle AllocString(string str, out nuint byteCount)
            => AllocStringNative(str, out byteCount);

        public static implicit operator IntPtr(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr?._ptr ?? default;

        public static unsafe explicit operator void*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (void*)dispoIntPtr._ptr;

        public static unsafe explicit operator byte*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (byte*)dispoIntPtr._ptr;

        public static unsafe explicit operator sbyte*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (sbyte*)dispoIntPtr._ptr;

        public static unsafe explicit operator short*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (short*)dispoIntPtr._ptr;

        public static unsafe explicit operator ushort*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (ushort*)dispoIntPtr._ptr;

        public static unsafe explicit operator char*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (char*)dispoIntPtr._ptr;

        public static unsafe explicit operator int*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (int*)dispoIntPtr._ptr;

        public static unsafe explicit operator uint*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (uint*)dispoIntPtr._ptr;

        public static unsafe explicit operator long*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (long*)dispoIntPtr._ptr;

        public static unsafe explicit operator ulong*(ZSafeHandle? dispoIntPtr)
            => dispoIntPtr == null ? null : (ulong*)dispoIntPtr._ptr;

        private bool _isAllocated;

        private IntPtr _ptr;

        public IntPtr Ptr => _ptr;

        ~ZSafeHandle()
            => Dispose(true);

        public void Dispose()
            => Dispose(false);

        void Dispose(bool final)
        {
            // TODO: instance ThreadStatic && do ( o == null ? return : ( lock(o, ms), check threadId, .. ) ) 
            var handle = _ptr;
            if (handle == default)
                return;

            if (_isAllocated)
            {
                Marshal.FreeHGlobal(handle);
                _isAllocated = false;
            }
            _ptr = default;

            if (!final)
            {
                    freeHandles.Push(this);
            }
        }
    }
}
