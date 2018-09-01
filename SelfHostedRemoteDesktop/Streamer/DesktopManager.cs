using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.NativeWin;

namespace SelfHostedRemoteDesktop.Streamer
{
	public static class DesktopManager
	{
		/// <summary>
		/// [default: true] A convenience variable, not used within this class, for helping external code decide whether or not to call AssociateCurrentThreadWithDefaultDesktop().
		/// </summary>
		public static bool ShouldReassociate = true;
		/// <summary>
		/// If the current thread's desktop is not the desktop that receives input, then change it. Returns true if the desktop was changed.
		/// </summary>
		public static bool AssociateCurrentThreadWithDefaultDesktop()
		{
			using (AutoDisposeHandle inputDesktop = GetInputDesktop())
			{
				string inputDesktopName = GetDesktopName(inputDesktop);
				string currentThreadDesktopName = GetDesktopName(GetThreadDesktop());
				if (currentThreadDesktopName != inputDesktopName)
				{
					if (NativeMethods.SetThreadDesktop(inputDesktop))
						return true;
					Win32Helper.ThrowLastWin32Error("Unable to set thread desktop");
				}
			}
			return false;
		}
		/// <summary>
		/// Retrieves a handle to the desktop assigned to the current thread.  This handle does not need to be closed.
		/// </summary>
		/// <returns></returns>
		private static AutoDisposeHandle GetThreadDesktop()
		{
			// The value returned by GetThreadDesktop does not need to be closed, so we pass null for the onRelease method.
			return AutoDisposeHandle.Create(NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId()), null);
		}
		/// <summary>
		/// Gets a handle to the desktop that receives user input.
		/// </summary>
		/// <returns></returns>
		private static AutoDisposeHandle GetInputDesktop()
		{
			return AutoDisposeHandle.Create(NativeMethods.OpenInputDesktop(0, false, (uint)NativeMethods.ACCESS_MASK.DESKTOP_ALL_ACCESS), h => NativeMethods.CloseDesktop(h));
		}

		private static string GetDesktopName(IntPtr desktopHandle)
		{
			byte[] desktopNameBuf = new byte[64];
			uint nameLength;
			if (!NativeMethods.GetUserObjectInformation(desktopHandle, NativeMethods.UserObjectInformation.NAME, desktopNameBuf, (uint)desktopNameBuf.Length, out nameLength))
			{
				desktopNameBuf = new byte[nameLength];
				if (!NativeMethods.GetUserObjectInformation(desktopHandle, NativeMethods.UserObjectInformation.NAME, desktopNameBuf, (uint)desktopNameBuf.Length, out nameLength))
					throw new Exception("Unable to get desktop name");
			}
			return Encoding.ASCII.GetString(desktopNameBuf, 0, (int)Math.Min(nameLength, desktopNameBuf.Length));
		}
	}
}
