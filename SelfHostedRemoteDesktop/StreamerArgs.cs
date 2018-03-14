using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop
{
	public class StreamerArgs
	{
		public int? ServiceProcessId = null;
		public string SharedMemoryId = null;
		public StreamerArgs(int? ServiceProcessId = null, string SharedMemoryId = null)
		{
			this.ServiceProcessId = ServiceProcessId;
			this.SharedMemoryId = SharedMemoryId;
		}
		public StreamerArgs(string[] args)
		{
			if (args.Length == 0 || args[0] != "streamer")
				throw new ArgumentException("First argument must be \"streamer\" in order to use this StreamerArgs constructor.");
			for (int i = 1; i < args.Length; i++)
			{
				bool nextArgExists = i + 1 < args.Length;
				if (args[i] == "-spid" && nextArgExists)
					ServiceProcessId = TryParseInt(args[i+1]);
				else if (args[i] == "-smid" && nextArgExists)
					SharedMemoryId = args[i + 1];
			}
		}
		/// <summary>
		/// Returns the args formatted to go on a command line.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			List<string> args = new List<string>();
			args.Add("streamer");
			if (ServiceProcessId != null)
				args.Add("-spid " + ServiceProcessId);
			if (SharedMemoryId != null)
				args.Add("-smid " + EncodeParameterArgument(SharedMemoryId));
			return string.Join(" ", args);
		}

		/// <summary>
		/// Attempts to parse an int from the specified string.
		/// </summary>
		/// <param name="str">The string containing an int.</param>
		/// <returns>The parsed int, or null</returns>
		private static int? TryParseInt(string str)
		{
			int num;
			if (int.TryParse(str, out num))
				return num;
			return null;
		}
		/// <summary>
		/// Encodes an argument for passing into a program on the command line.
		/// From http://stackoverflow.com/a/12364234/814569
		/// </summary>
		/// <param name="original">The value that should be received by the program</param>
		/// <returns>The value which needs to be passed to the program for the original value to come through</returns>
		public static string EncodeParameterArgument(string original)
		{
			if (string.IsNullOrEmpty(original))
				return original;
			string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
			value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"", RegexOptions.Singleline);
			return value;
		}
	}
}
