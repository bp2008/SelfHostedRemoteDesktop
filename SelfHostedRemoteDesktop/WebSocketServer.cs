//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using BPUtil;
//using SHRDLib;
//using SHRDLib.NetCommand;
//using WebSocketSharp;
//using WebSocketSharp.Server;
//using Logger = BPUtil.Logger;

//namespace SelfHostedRemoteDesktop
//{
//	public class SHRDWebSocketServer
//	{
//		private const string myPath = "/SHRD";
//		WebSocketServer srv;
//		public SHRDWebSocketServer(int port)
//		{
//			srv = new WebSocketServer(port, false);
//			srv.Log.Output = (LogData data, string path) =>
//			{
//				if (data.Level >= LogLevel.Error)
//					Logger.Debug(data.Message);
//				else
//					Logger.Info(data.Message);
//			};
//			srv.AddWebSocketService<SHRDWebSocketBehavior>(myPath);
//			Logger.Info("WebSocket Service Added on port " + port + ": " + myPath);
//		}

//		/// <summary>
//		/// Opens a connection to the Master Server and creates the most basic possible 
//		/// </summary>
//		/// <param name="connectionKey"></param>
//		public void BeginProxiedConnection(string connectionKey)
//		{
//			TcpClient tcpc = new TcpClient();
//			tcpc.BeginConnect("localhost", 8088, onTcpcConnect, new { tcpc, connectionKey });
//		}

//		private void onTcpcConnect(IAsyncResult ar)
//		{
//			if (ar.IsCompleted)
//			{
//				dynamic args = (dynamic)ar.AsyncState;
//				TcpClient tcpc = (TcpClient)args.tcpc;
//				tcpc.NoDelay = true;
//				string connectionKey = (string)args.connectionKey;

//				// Send to the Master Server an HTTP request that will be transformed into a web socket proxy.
//				// After the connection key, append the port number, then append this service's path (e.g. connectionKey + "/80/SHRD")
//				// This could have been hard-coded in the Master Server, but it seems more flexible if we send it here.
//				// For example, at some point in the future, a host service might want to customize or randomize its web socket listening endpoint.
//				Console.WriteLine("GET /WebSocketHostProxy/" + connectionKey + "/" + srv.Port + myPath + " HTTP/1.1");
//				byte[] buf = Encoding.UTF8.GetBytes("GET /WebSocketHostProxy/" + connectionKey + "/" + srv.Port + myPath + " HTTP/1.1\r\n\r\n");
//				tcpc.GetStream().Write(buf, 0, buf.Length);
//				Console.WriteLine("srv.AcceptTcpClient");
//				srv.AcceptTcpClient(tcpc);
//			}
//			else
//				Logger.Debug("SHRDWebSocketServer.onTcpcConnect with IsCompleted == false");
//		}

//		public void Start()
//		{
//			srv.Start();
//		}
//		public void Stop()
//		{
//			Try.Catch(() => { srv.Stop(); });
//		}
//		private class SHRDWebSocketBehavior : WebSocketBehavior
//		{


//		}
//	}
//}
