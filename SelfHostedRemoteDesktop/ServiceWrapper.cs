using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using BPUtil;
using SelfHostedRemoteDesktop.Config;
using SelfHostedRemoteDesktop.ServerConnect;
using SHRDLib;

namespace SelfHostedRemoteDesktop
{
	public static class ServiceWrapper
	{
		public static readonly int service_pid = Process.GetCurrentProcess().Id;

		public static Settings settings;
		private static SHRDWebSocketServer webSocketServer;
		public static HostConnect hostConnect;
		private static bool isStopped = true;
		private static WaitProgressivelyLonger reconnectTimer = WaitProgressivelyLonger.Exponential(60000, 2, 5000);

		public static void Initialize()
		{
			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(BPUtil.Logger.httpLogger);

			settings = new Config.Settings();
			if (!settings.Load())
				settings.Save();

			webSocketServer = new SHRDWebSocketServer(8089);

			hostConnect = new HostConnect();
			hostConnect.StateChanged += HostConnect_StateChanged;
		}

		private static void HostConnect_StateChanged(object sender, StateChangedEventArgs e)
		{
			Logger.Info("HostConnect_StateChanged: " + e.state);
			if (e.state == HostConnectClientState.Disconnected && !isStopped)
			{
				int timeout = reconnectTimer.GetNextTimeout();
				Logger.Info("HostConnect disconnected. Automatic reconnection in " + (timeout / 1000) + " seconds.");
				SetTimeout.OnBackground(hostConnect.Connect, timeout);
			}
			else if (e.state == HostConnectClientState.Connected)
			{
				reconnectTimer.Reset();
			}
		}

		public static void Start()
		{
			isStopped = false;
			Logger.StartLoggingThreads();
			//httpServer.Start();
			webSocketServer.Start();
			hostConnect.Connect();
		}
		public static void Stop()
		{
			isStopped = true;
			//Try.Catch(() => { httpServer?.Stop(); });
			Try.Catch(() => { webSocketServer?.Stop(); });
			Try.Catch(Logger.StopLoggingThreads);
			Try.Catch(() => { hostConnect?.Disconnect(); });
		}
	}
}
