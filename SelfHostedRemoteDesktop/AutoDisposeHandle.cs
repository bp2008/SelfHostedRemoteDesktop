using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop
{
	/// <summary>
	/// Manages a native OS handle and automatically calls a configurable "release" function on the handle when it is no longer in use, or when the Dispose method is called.
	/// </summary>
	public class AutoDisposeHandle : CriticalFinalizerObject, IDisposable
	{
		private IntPtr NativeHandle;

		private Action<IntPtr> onRelease;

		private bool isDisposed = false;
		
		private AutoDisposeHandle(IntPtr handle, Action<IntPtr> onRelease)
		{
			this.NativeHandle = handle;
			this.onRelease = onRelease;
		}
		~AutoDisposeHandle()
		{
			if (isDisposed)
				return;
			Dispose(false);
		}

		public void Dispose()
		{
			if (isDisposed)
				return;
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (onRelease != null)
				onRelease(NativeHandle);
			isDisposed = true;
		}

		public static implicit operator IntPtr(AutoDisposeHandle h)
		{
			if (h == null)
				return IntPtr.Zero;
			return h.NativeHandle;
		}

		public static bool operator ==(AutoDisposeHandle h1, AutoDisposeHandle h2)
		{
			return Equals(h1, h2);
		}

		public static bool operator !=(AutoDisposeHandle h1, AutoDisposeHandle h2)
		{
			return !Equals(h1, h2);
		}

		public override bool Equals(object h2)
		{
			if (h2 == null)
				return false;
			return ((AutoDisposeHandle)h2).NativeHandle == NativeHandle;
		}

		public override int GetHashCode()
		{
			return NativeHandle.GetHashCode();
		}

		/// <summary>
		/// Creates a handle. Will return null if [handle] is IntPtr.Zero.
		/// </summary>
		/// <param name="handle">The handle to manage.</param>
		/// <param name="onRelease">This method is called automatically, and the handle is passed in, when this instance is no longer in use or when the Dispose method is called.</param>
		/// <returns></returns>
		public static AutoDisposeHandle Create(IntPtr handle, Action<IntPtr> onRelease)
		{
			if (handle == IntPtr.Zero)
				return null;
			return new AutoDisposeHandle(handle, onRelease);
		}
	}
}
