using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using BPUtil;
using BPUtil.Forms;

namespace SelfHostedRemoteDesktop
{
	public static class StaticMain
	{
		public static void Run(string[] args)
		{
			string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			Globals.InitializeProgram(exePath, "Self Hosted Remote Desktop", true);
			PrivateAccessor.SetStaticFieldValue(typeof(Globals), "errorFilePath", Globals.WritableDirectoryBase + "SHRD_Log.txt");

			FileInfo fiExe = new FileInfo(exePath);
			Environment.CurrentDirectory = fiExe.Directory.FullName;

			System.Windows.Forms.Application.ThreadException += Application_ThreadException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

			//byte[] buf = new byte[4];
			//ByteUtil.WriteFloat(5, buf, 0);
			//float f = ByteUtil.ReadFloat(buf, 0);
			//Logger.Info(f.ToString());

			if (Environment.UserInteractive)
			{
				bool cmd = args.Length > 0 && args[0] == "cmd";
				if (cmd || Debugger.IsAttached)
				{
					ConsoleAppHelper.AllocateConsole();
					Logger.logType = LoggingMode.Console | LoggingMode.File;
					Logger.Info("Console environment detected. Logging to console is enabled.");
					ServiceWrapper.Initialize();
					ServiceWrapper.Start();
					do
						Console.WriteLine("Type \"exit\" to close");
					while (Console.ReadLine().ToLower() != "exit");
					ServiceWrapper.Stop();
					return;
				}
				else
					Logger.logType = LoggingMode.File;

				string Title = "SelfHostedRemoteDesktop " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " Service Manager";
				string ServiceName = "SelfHostedRemoteDesktop";
				ButtonDefinition btnStartCmdTest = new ButtonDefinition("Test w/console", btnStartCmdTest_Click);
				ButtonDefinition[] customButtons = new ButtonDefinition[] { btnStartCmdTest };

				System.Windows.Forms.Application.Run(new ServiceManager(Title, ServiceName, customButtons));
			}
			else
			{
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[]
				{
				new SelfHostedRemoteDesktopSvc()
				};
				ServiceBase.Run(ServicesToRun);
			}
		}

		private static void btnStartCmdTest_Click(object sender, EventArgs e)
		{
			Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "cmd");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			HandleUnhandledException((Exception)e.ExceptionObject, "Unhandled Exception");
		}

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			HandleUnhandledException(e.Exception, "Unhandled Thread Exception");
		}

		private static void HandleUnhandledException(Exception exception, string message)
		{
			Logger.Debug(exception, message);
		}
	}
}
