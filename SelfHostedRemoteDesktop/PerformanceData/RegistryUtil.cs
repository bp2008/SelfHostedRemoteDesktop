﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SelfHostedRemoteDesktop.PerformanceData
{
	public static class RegistryUtil
	{
		/// <summary>
		/// Set = true to read entries written by 32 bit programs on 64 bit Windows.
		/// 
		/// On 32 bit Windows, this setting has no effect.
		/// </summary>
		public static bool Force32BitRegistryAccess = false;

		/// <summary>
		/// Gets HKEY_LOCAL_MACHINE in either the 32 or 64 bit view depending on RegistryUtil configuration and OS version.
		/// </summary>
		/// <returns></returns>
		public static RegistryKey HKLM
		{
			get
			{
				if (!Force32BitRegistryAccess && Environment.Is64BitOperatingSystem)
					return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
				else
					return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
			}
		}
		/// <summary>
		/// Gets HKEY_CURRENT_USER in either the 32 or 64 bit view depending on RegistryUtil configuration and OS version.
		/// </summary>
		/// <returns></returns>
		public static RegistryKey HKCU
		{
			get
			{
				if (!Force32BitRegistryAccess && Environment.Is64BitOperatingSystem)
					return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
				else
					return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
			}
		}

		/// <summary>
		/// Returns the requested RegistryKey or null if the key does not exist.
		/// </summary>
		/// <param name="path">A path relative to HKEY_LOCAL_MACHINE.  E.g. "SOFTWARE\\Microsoft"</param>
		/// <returns></returns>
		public static RegistryKey GetHKLMKey(string path)
		{
			return HKLM.OpenSubKey(path);
		}
		/// <summary>
		/// Returns the requested RegistryKey or null if the key does not exist.
		/// </summary>
		/// <param name="path">A path relative to HKEY_LOCAL_MACHINE.  E.g. "SOFTWARE\\Microsoft"</param>
		/// <returns></returns>
		public static RegistryKey GetHKCUKey(string path)
		{
			return HKCU.OpenSubKey(path);
		}

		public static T GetHKLMValue<T>(string path, string key, T defaultValue)
		{
			object value = HKLM.OpenSubKey(path)?.GetValue(key);
			if (value == null)
				return defaultValue;
			return (T)value;
		}
		public static string GetStringValue(RegistryKey key, string name)
		{
			object obj = key.GetValue(name);
			if (obj == null)
				return "";
			return obj.ToString();
		}
		public static int GetIntValue(RegistryKey key, string name, int defaultValue)
		{
			object obj = key.GetValue(name);
			if (obj == null)
				return defaultValue;
			if (typeof(int).IsAssignableFrom(obj.GetType()))
				return (int)obj;
			int val;
			if (int.TryParse(obj.ToString(), out val))
				return val;
			return defaultValue;
		}
	}
}
