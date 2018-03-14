using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.Native
{
	public static class Win32Helper
	{
		public static void ThrowLastWin32Error(string message = null)
		{
			int error = Marshal.GetLastWin32Error();
			if (message != null)
				throw new Win32Exception(error, message + " - Windows Error Code: " + error);
			else
				throw new Win32Exception(error, "Windows Error Code: " + error);
		}

		public static int GetLastWin32Error()
		{
			return Marshal.GetLastWin32Error();
		}
	}
}
