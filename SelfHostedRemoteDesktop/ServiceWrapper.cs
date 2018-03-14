using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using BPUtil;
using SelfHostedRemoteDesktop.Config;
using SHRDLib;

namespace SelfHostedRemoteDesktop
{
	public static class ServiceWrapper
	{
		public static readonly int service_pid = Process.GetCurrentProcess().Id;
		
		public static Settings settings;
		private static SHRDWebSocketServer webSocketServer;

		public static void Initialize()
		{
			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(BPUtil.Logger.httpLogger);

			settings = new Config.Settings();
			if (!settings.Load())
				settings.Save();
			
			webSocketServer = new SHRDWebSocketServer(8089);
		}
		public static void Start()
		{
			Logger.StartLoggingThreads();
			//httpServer.Start();
			webSocketServer.Start();
		}
		public static void Stop()
		{
			//Try.Catch(() => { httpServer?.Stop(); });
			Try.Catch(() => { webSocketServer?.Stop(); });
			Try.Catch(Logger.StopLoggingThreads);
		}
	}
}
