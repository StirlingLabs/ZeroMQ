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
		{
			int byteCount;
			return AllocString(str, out byteCount);
		}

		public static DispoIntPtr AllocString(string str, out int byteCount)
			=> AllocStringNative(str, out byteCount);

		public static implicit operator IntPtr(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? IntPtr.Zero : dispoIntPtr._ptr;

		unsafe public static explicit operator void*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (void*)dispoIntPtr._ptr;

		unsafe public static explicit operator byte*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (byte*)dispoIntPtr._ptr;

		unsafe public static explicit operator sbyte*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (sbyte*)dispoIntPtr._ptr;

		unsafe public static explicit operator short*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (short*)dispoIntPtr._ptr;

		unsafe public static explicit operator ushort*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (ushort*)dispoIntPtr._ptr;

		unsafe public static explicit operator char*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (char*)dispoIntPtr._ptr;

		unsafe public static explicit operator int*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (int*)dispoIntPtr._ptr;

		unsafe public static explicit operator uint*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (uint*)dispoIntPtr._ptr;

		unsafe public static explicit operator long*(DispoIntPtr dispoIntPtr)
			=> dispoIntPtr == null ? null : (long*)dispoIntPtr._ptr;

		unsafe public static explicit operator ulong*(DispoIntPtr dispoIntPtr)
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
			if (handle != IntPtr.Zero)
			{
				if (isAllocated)
				{
					Marshal.FreeHGlobal(handle);
					isAllocated = false;
				}
				_ptr = IntPtr.Zero;
			}
		}

		/* public void ReAlloc(long size) {
			_ptr = Marshal.ReAllocHGlobal(_ptr, new IntPtr(size));
		} */
	}
}