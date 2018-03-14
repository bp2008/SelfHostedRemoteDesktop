using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using SelfHostedRemoteDesktop.Native;
using SHRDLib;

namespace SelfHostedRemoteDesktop
{
	public class ProcessHelper
	{
		/// <summary>
		/// Attempts to start the specified interactive process. Returns the process ID of the executed program, or -1 if there was a problem. May also throw an exception.
		/// </summary>
		/// <param name="executablePath"></param>
		/// <param name="commandLine"></param>
		/// <param name="workingDirectory"></param>
		/// <returns></returns>
		public static int ExecuteInteractive(string executablePath, string commandLine, string workingDirectory)
		{
			int consoleSessionID = GetConsoleSessionId();
			if (consoleSessionID == -1)
				return -1; // No session currently attached to the console.

			// In order to be able to open a process when no user is logged in, we will use an
			// existing process from the active console session as a sort of template.

			// First, we will try explorer.exe
			int templateProcessId = -1;// Process.GetProcessesByName("explorer").FirstOrDefault(p =>
									   //{
									   //	return p.SessionId == consoleSessionID;
									   //})?.Id ?? -1;
									   //if (templateProcessId != -1)
									   //	Logger.Info("Will impersonate explorer.exe for session " + consoleSessionID);

			// Next, we will try winlogon.exe, though it is said a process impersonating winlogin is killed by the system after about 10 minutes.
			if (templateProcessId == -1)
			{
				templateProcessId = Process.GetProcessesByName("winlogon").FirstOrDefault(p =>
				{
					return p.SessionId == consoleSessionID;
				})?.Id ?? -1;
				if (templateProcessId != -1)
					Logger.Info("Will impersonate winlogon.exe for session " + consoleSessionID);
			}

			// If that fails, try any process running as local System.
			if (templateProcessId == -1)
			{
				templateProcessId = Process.GetProcesses().FirstOrDefault(p => p.SessionId == consoleSessionID && UserIsMatch(p.Id, WellKnownSidType.LocalSystemSid))?.Id ?? -1;
				if (templateProcessId != -1)
					Logger.Info("Will impersonate an arbitrary local system process for session " + consoleSessionID);
			}

			if (templateProcessId == -1)
				return -1; // No process could be found to use as a template.

			// Open Process
			using (AutoDisposeHandle cloneableProcHandle = OpenProcess(templateProcessId))
			{
				// Get token from process
				using (AutoDisposeHandle originalToken = OpenProcessToken(cloneableProcHandle))
				{
					if (originalToken == null)
						return -1;

					// Clone the token
					using (AutoDisposeHandle duplicatedToken = DuplicateTokenEx(originalToken))
					{
						if (duplicatedToken == null)
							return -1;

						// Try to start process
						return CreateProcessAsUser(executablePath, commandLine, workingDirectory, duplicatedToken);
					}
				}
			}
		}

		/// <summary>
		/// Returns the current active console session Id, or -1 if there is no session currently attached to the physical console.
		/// </summary>
		/// <returns></returns>
		public static int GetConsoleSessionId()
		{
			return NativeMethods.WTSGetActiveConsoleSessionId();
		}

		public static Process GetProcByID(int id)
		{
			if (id == -1)
				return null;
			// Process.GetProcessById(id) throws an exception if the process can't be found.
			Process[] processlist = Process.GetProcesses();
			return processlist.FirstOrDefault(pr => pr.Id == id);
		}
		private static bool UserIsMatch(int pid, WellKnownSidType type)
		{
			try
			{
				using (AutoDisposeHandle processHandle = OpenProcess(pid))
				{
					using (AutoDisposeHandle processToken = OpenProcessToken(processHandle))
					{
						using (WindowsIdentity identity = new WindowsIdentity(processToken))
							return identity.User.IsWellKnown(type);
					}
				}
			}
			catch
			{
				return false;
			}
		}

		private static AutoDisposeHandle OpenProcess(int templateProcessId)
		{
			return AutoDisposeHandle.Create(NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.All, false, templateProcessId), h => NativeMethods.CloseHandle(h));
		}
		private static AutoDisposeHandle OpenProcessToken(IntPtr processHandle)
		{
			IntPtr handle;
			if (NativeMethods.OpenProcessToken(processHandle, (uint)TokenAccessLevels.MaximumAllowed, out handle))
				return AutoDisposeHandle.Create(handle, h => NativeMethods.CloseHandle(h));
			return null;
		}
		private static AutoDisposeHandle DuplicateTokenEx(IntPtr originalToken)
		{
			IntPtr handle;
			if (NativeMethods.DuplicateTokenEx(originalToken, (uint)TokenAccessLevels.MaximumAllowed, IntPtr.Zero, NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, NativeMethods.TOKEN_TYPE.TokenPrimary, out handle))
				return AutoDisposeHandle.Create(handle, h => NativeMethods.CloseHandle(h));
			return null;
		}
		private static AutoDisposeHandle CreateEnvironmentBlock(IntPtr userToken)
		{
			IntPtr handle;
			if (NativeMethods.CreateEnvironmentBlock(out handle, userToken, false))
				return AutoDisposeHandle.Create(handle, h => NativeMethods.DestroyEnvironmentBlock(h));
			return null;
		}
		private static int CreateProcessAsUser(string executablePath, string commandLine, string workingDirectory, IntPtr userToken)
		{
			using (AutoDisposeHandle environmentVariables = CreateEnvironmentBlock(userToken))
			{
				if (environmentVariables == null)
					return -1;

				NativeMethods.STARTUPINFO startupInformation = new NativeMethods.STARTUPINFO();
				startupInformation.length = Marshal.SizeOf(typeof(NativeMethods.STARTUPINFO));
				startupInformation.desktop = "Winsta0\\Default";
				startupInformation.showWindow = (short)NativeMethods.WindowShowStyle.ShowNoActivate;
				NativeMethods.PROCESS_INFORMATION processInformation = new NativeMethods.PROCESS_INFORMATION();
				try
				{
					bool result = NativeMethods.CreateProcessAsUser
					(
						userToken,
						executablePath,
						commandLine,
						IntPtr.Zero,
						IntPtr.Zero,
						false,
						(uint)(NativeMethods.CreateProcessFlags.DETACHED_PROCESS | NativeMethods.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT),
						environmentVariables,
						workingDirectory,
						ref startupInformation,
						ref processInformation
					);
					if (!result)
						Win32Helper.ThrowLastWin32Error("Unable to start streamer process");
					return processInformation.processID;
				}
				finally
				{
					if (processInformation.processHandle != IntPtr.Zero)
					{
						try
						{
							NativeMethods.CloseHandle(processInformation.processHandle);
						}
						catch { }
					}
					if (processInformation.threadHandle != IntPtr.Zero)
					{
						try
						{
							NativeMethods.CloseHandle(processInformation.threadHandle);
						}
						catch { }
					}
				}
			}
		}
	}
}
