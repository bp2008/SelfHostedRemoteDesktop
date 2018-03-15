using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.PerformanceData
{
	public static class OSInfo
	{
		public static string GetOSVersionInfo()
		{
			StringBuilder sb = new StringBuilder();
			string prodName = RegistryUtil.GetHKLMValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "Unknown");
			sb.Append(prodName);

			string release = RegistryUtil.GetHKLMValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "");
			if (string.IsNullOrWhiteSpace(release))
				sb.Append(" v" + RegistryUtil.GetHKLMValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentVersion", "Unknown"));
			else
			{
				sb.Append(" v" + release);
				string build = RegistryUtil.GetHKLMValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", "");
				if (string.IsNullOrWhiteSpace(build))
					build = RegistryUtil.GetHKLMValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", "");
				if (!string.IsNullOrWhiteSpace(build))
					sb.Append(" b" + build);
			}
			if (Environment.Is64BitOperatingSystem)
				sb.Append(" (64 bit)");
			else
				sb.Append(" (32 bit)");
			return sb.ToString();
		}
	}
}
