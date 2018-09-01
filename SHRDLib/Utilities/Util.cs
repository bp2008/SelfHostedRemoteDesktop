using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using BCrypt.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace SHRDLib
{
	public static class Util
	{
		public static string BCryptSalt()
		{
			return BCrypt.Net.BCrypt.GenerateSalt(10);
		}
		/// <summary>
		/// Usually, it is the client app which does this, however we need to be able to perform this for the initial 
		/// "admin" account or if we decide to assign a random password for a user during password recovery.
		/// </summary>
		/// <param name="source">The string to hash.</param>
		/// <param name="salt">The BCrypt salt (this can't be just any string, it needs to have a specific format).</param>
		public static string BCryptHash(string source, string salt)
		{
			return BCrypt.Net.BCrypt.HashPassword(source, salt);
		}
		public static bool ArraysEqual<T>(T[] a, T[] b)
		{
			if (a == null && b == null)
				return true;
			else if (a == null || b == null || a.Length != b.Length)
				return false;
			else
			{
				for (int i = 0; i < a.Length; i++)
					if (!a[i].Equals(b[i]))
						return false;
			}
			return true;
		}

		/// <summary>
		/// Returns a BCrypt salt string based on the hash of the user name salted with a "secret" string.
		/// This string has a persistent value. E.g. given the same user name (case insensitive), it will always return the same salt string.
		/// </summary>
		/// <param name="userName">The user name of the requested user to create a fake persistent salt value for.</param>
		/// <returns></returns>
		public static string GenerateFakeUserSalt(string userName)
		{
			return BCrypt.Net.BCrypt.GenerateFakeSalt(10, Hash.GetSHA1Bytes(userName.ToLower() + "TODO: Make this secret changeable and randomly generated, but persisted on disk"));
		}

		/// <summary>
		/// Gets a random character from the ranges 0-9, A-Z, a-z. There are 62 possible characters this method will return.
		/// </summary>
		/// <returns></returns>
		public static char GetRandomAlphaNumericChar()
		{
			int i = SecureRandom.Next(62);
			if (i < 10)
				return (char)(48 + i);
			if (i < 36)
				return (char)(65 + (i - 10));
			return (char)(97 + (i - 36));
		}
		/// <summary>
		/// Gets a string of random characters from the ranges 0-9, A-Z, a-z. There are 62 possible characters this method will return.
		/// </summary>
		/// <returns></returns>
		public static string GetRandomAlphaNumericString(ushort length)
		{
			StringBuilder sb = new StringBuilder(length);
			for (int i = 0; i < length; i++)
				sb.Append(GetRandomAlphaNumericChar());
			return sb.ToString();
		}
		private static Regex rxNameEndsInNumber = new Regex("-(\\d+)$", RegexOptions.Compiled);
		/// <summary>
		/// Designed to be used along with the [AttemptUntilTrue] method.  Removes a previously-added "-#" tag and adds a new one to the name, ensuring the name does not exceed the allowed length.  If tryNumber is 1, the string is returned unaltered.  If tryNumber is 2, any previously-added "-#" tag is not removed.
		/// The idea is that you get these results:
		/// call(1) => "Name"
		/// call(2) => "Name-2
		/// call(3) => "Name-3"
		/// call(4) => "Name-4"
		/// 
		/// Due to the special [tryNumber] 2 behavior, it is even safe for the original name to end in "-#".
		/// 
		/// call(1) => "Name-5"
		/// call(2) => "Name-5-2
		/// call(3) => "Name-5-3"
		/// call(4) => "Name-5-4"
		/// 
		/// It is safe to use multiple-digit numbers for tryNumber.
		/// </summary>
		/// <param name="name">A string that is the name of something.</param>
		/// <param name="tryNumber">The number to append to the end of the string in an attempt to make it unique.</param>
		/// <param name="maxLength">The maximum length the string can be.  Characters may be trimmed from the end of the string if necessary before appending a hyphen and [tryNumber].</param>
		/// <returns></returns>
		public static string MakeNameUnique(string name, uint tryNumber, int maxLength)
		{
			string strTryNumber = "-" + tryNumber.ToString();
			if (tryNumber > 2)
			{
				Match m = rxNameEndsInNumber.Match(name);
				if (m.Success)
					name = name.Remove(name.Length - m.Length);
			}
			if (name.Length + strTryNumber.Length > maxLength)
				name = name.Remove(maxLength - strTryNumber.Length);
			return name + "-" + tryNumber;
		}

		/// <summary>
		/// Attempts an action repeatedly until any of these is true:
		/// A) The provided function returns true.
		/// B) The function has been attempted [maxAttempts] times.
		/// C) [minAttempts] has been reached and [timeLimitMs] has been reached.
		/// Returns true if the attempted function returns true.  Returns false if this method ends because it gave up.
		/// </summary>
		/// <param name="minAttempts">The minimum number of failed attempts to allow.</param>
		/// <param name="maxAttempts">The maximum number of failed attempts to allow.</param>
		/// <param name="timeLimitMs">Ceases attempts before the maximum attempt limit is reached if the minimum attempt limit is reached and this many milliseconds have passed since we started attempts.</param>
		/// <param name="func">The function to call.  A single integer argument is passed in, starting at 1 the first time and increasing by 1 each call up to but not exceeding [maxAttempts].  This function should return true when it is successful in its action.</param>
		/// <param name="waitBetweenAttempts">If greater than -1, the thread will sleep this many milliseconds between attempts.  It will not sleep before the first attempt or after the last attempt. Sleeping for 0 milliseconds should be equivalent to Thread.Yield().</param>
		public static bool AttemptUntilTrue(int minAttempts, int maxAttempts, int timeLimitMs, Func<int, bool> func, int waitBetweenAttempts = -1)
		{
			int attempt = 1;
			Stopwatch sw = Stopwatch.StartNew();
			while (attempt <= maxAttempts // Test Condition B
				&& !(attempt > minAttempts && sw.ElapsedMilliseconds >= timeLimitMs)) // Test Condition C
			{
				if (waitBetweenAttempts > -1 && attempt > 1)
					Thread.Sleep(waitBetweenAttempts);
				if (func(attempt))
					return true; // Condition A
				attempt++;
			}
			return false;
		}
	}
}
