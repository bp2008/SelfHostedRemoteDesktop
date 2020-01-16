using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using BPUtil;
using SelfHostedRemoteDesktop.ClientConnect;
using SelfHostedRemoteDesktop.Config;
using SelfHostedRemoteDesktop.ServerConnect;
using SHRDLib;

namespace SelfHostedRemoteDesktop
{
	public static class ServiceWrapper
	{
		public static readonly int service_pid = Process.GetCurrentProcess().Id;

		public static Settings settings;
		public static HostConnect hostConnect;
		private static bool isStopped = true;
		private static WaitProgressivelyLonger reconnectTimer = WaitProgressivelyLonger.Exponential(60000, 2, 5000);

		private static object webSocketConnectionsLock = new object();
		/// <summary>
		/// A dictionary of proxyKey to <see cref="SHRDWebSocketClientHandler"/>.
		/// </summary>
		private static Dictionary<string, SHRDWebSocketClientHandler> webSocketConnections = new Dictionary<string, SHRDWebSocketClientHandler>();

		public static void Initialize()
		{
			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(BPUtil.Logger.httpLogger);

			settings = new Config.Settings();
			if (!settings.Load())
				settings.Save();


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
			hostConnect.Connect();
		}
		public static void Stop()
		{
			isStopped = true;
			//Try.Catch(() => { httpServer?.Stop(); });
			Try.Catch(Logger.StopLoggingThreads);
			Try.Catch(() => { hostConnect?.Disconnect(); });
			lock (webSocketConnectionsLock)
			{
				foreach (SHRDWebSocketClientHandler clientHandler in webSocketConnections.Values)
					clientHandler.CloseSocket();
				webSocketConnections.Clear();
			}
		}

		public static void BeginOutgoingWebSocketConnection(string proxyKey, string sourceIp)
		{
			Logger.Info("Accepting web socket connection request from " + sourceIp);
			SHRDWebSocketClientHandler clientHandler = new SHRDWebSocketClientHandler();
			lock (webSocketConnectionsLock)
			{
				if (webSocketConnections.ContainsKey(proxyKey))
					throw new Exception("The proxy key \"" + proxyKey + "\" is already in use.");

				webSocketConnections.Add(proxyKey, clientHandler);
			}

			clientHandler.OnClose += (sender, e) =>
			{
				lock (webSocketConnectionsLock)
				{
					if (webSocketConnections.ContainsKey(proxyKey))
						webSocketConnections.Remove(proxyKey);
				}
			};
			clientHandler.BeginProxiedConnection(proxyKey);
		}
	}
}
