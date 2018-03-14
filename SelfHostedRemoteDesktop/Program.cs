using System;

namespace SelfHostedRemoteDesktop
{
	static class Program
	{
		static Program()
		{
			CosturaUtility.Initialize();
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			if (args.Length > 0 && args[0] == "streamer")
				Streamer.StreamerStaticMain.Run(args);
			else
				StaticMain.Run(args);
		}
	}
}
