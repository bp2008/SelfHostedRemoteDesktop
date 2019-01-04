using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using BPUtil;
using MasterServer.Config;
using MasterServer.Database;

namespace MasterServer
{
	public static class ServiceWrapper
	{
		public static DB db;
		public static Settings settings;
		private static WebServer httpServer;
		//private static SHRDWebSocketServer webSocketServer;

		public static void Initialize()
		{
			Globals.Initialize(System.Reflection.Assembly.GetExecutingAssembly().Location);
			if (Environment.UserInteractive)
			{
				Logger.logType = LoggingMode.Console | LoggingMode.File;
				Logger.Info("User-interactive environment detected. Logging to console is enabled.");
			}
			else
				Logger.logType = LoggingMode.File;
			
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(Logger.httpLogger);

			settings = new Settings();
			settings.Load();
			settings.SaveIfNoExist();

			db = new DB();
			httpServer = new WebServer(8088);
			//webSocketServer = new SHRDWebSocketServer(8089);
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
		public static void Start()
		{
			Logger.StartLoggingThreads();
			httpServer.Start();
			//webSocketServer.Start();
		}
		public static void Stop()
		{
			Try.Catch(() => { httpServer?.Stop(); });
			//Try.Catch(() => { webSocketServer?.Stop(); });
			Try.Catch(Logger.StopLoggingThreads);
			Try.Catch(() => { db?.Dispose(); });
		}
	}
}
