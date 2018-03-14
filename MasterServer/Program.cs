using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace MasterServer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			if (Environment.UserInteractive)
			{
				ConsoleAppHelper.AllocateConsole();
				Console.WriteLine("Master Server - Self Hosted Remote Desktop");
				Console.WriteLine("User-interactive environment detected.  Running as a console application.");
				try
				{
					ServiceWrapper.Initialize();
					ServiceWrapper.Start();
					Console.WriteLine("Type \"exit\" to close or \"web\" to open local web server.");
					string line;
					do
					{
						line = Console.ReadLine().ToLower();
						if (line == "web")
						{
							Console.WriteLine("Launching http://127.0.0.1:8088/admin");
							Process.Start("http://127.0.0.1:8088/admin");
						}
					}
					while (line != "exit");
					Console.WriteLine("Stopping...");
				}
				finally
				{
					ServiceWrapper.Stop();
				}
			}
			else
			{
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[]
				{
					new MasterServerSvc()
				};
				ServiceBase.Run(ServicesToRun);
			}
		}
	}
}
