using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using BPUtil;
using BPUtil.SimpleHttp;
using MasterServer.Database;
using SHRDLib;

namespace MasterServer
{
	public class WebSocketProxy
	{
		/// <summary>
		/// A dictionary of connections currently waiting to be proxied.
		/// </summary>
		private static ConcurrentDictionary<string, WaitingClient> pendingProxyConnections = new ConcurrentDictionary<string, WaitingClient>();

		/// <summary>
		/// Handles a request from a Web Client who is requesting a proxied web socket connection to a specific Host Service.
		/// </summary>
		/// <param name="p">The HttpProcessor handling the request.</param>
		public static void HandleWebSocketClientProxyRequest(HttpProcessor p)
		{
			#region Perform Validation
			// Path format: "/WebSocketClientProxy/1/SESSION". More path segments, if given, are ignored (the last path segment is typically used as a display name in browser developer tools).
			string[] parts = p.request_url.LocalPath.Split('/').Skip(1).ToArray();
			int computerId; // The computer ID the client wishes to connect to.
			if (parts.Length < 3 || parts[0] != "WebSocketClientProxy" || !int.TryParse(parts[1], out computerId))
			{
				p.writeFailure("400 Bad Request");
				return;
			}
			string sid = parts[2]; // The session ID of the client's session.
			#endregion
			#region Verify Permission
			ServerSession session = SessionManager.GetSession(sid);
			if (session == null || session.Expired)
			{
				p.writeFailure("403 Forbidden");
				return;
			}
			Computer computer = ServiceWrapper.db.GetComputer(computerId);
			if (computer == null)
			{
				p.writeFailure("404 Not Found");
				return;
			}

			User user = session.GetUser();
			if (user == null)
			{
				p.writeFailure("403 Forbidden");
				return;
			}

			// If we get here, we have an active authenticated session.
			if (!user.IsAdmin)
			{
				// Admin users can access all computers.
				// This user is not an adminn, so we must check group membership.
				ComputerGroupMembership[] cgm = computer.GetGroupMemberships();
				if (cgm.Length == 0)
				{
					Logger.Info("Non-admin user " + user.ID + " (" + user.Name + ") attempted to access computer " + computer.ID + " (" + computer.Name + ") but computer has no group memberships.");
					p.writeFailure("403 Forbidden");
					return;
				}
				UserGroupMembership[] ugm = user.GetGroupMemberships();
				if (ugm.Length == 0)
				{
					Logger.Info("Non-admin user " + user.ID + " (" + user.Name + ") attempted to access computer " + computer.ID + " (" + computer.Name + ") but user has no group memberships.");
					p.writeFailure("403 Forbidden");
					return;
				}

				// The computer is accessible to this user if the computer and the user share at least one group membership.
				bool accessible = 0 < cgm.Select(m => m.GroupID).Intersect(ugm.Select(m => m.GroupID)).Count();
				if (!accessible)
				{
					Logger.Info("Non-admin user " + user.ID + " (" + user.Name + ") attempted to access computer " + computer.ID + " (" + computer.Name + ") without permission.");
					p.writeFailure("403 Forbidden");
					return;
				}
			}
			#endregion

			// Now that permission has been verified, find out of the specified computer is online.
			HostConnectHandle host = HostConnect.GetOnlineComputer(computer.ID);
			if (host == null)
			{
				p.writeFailure("504 Gateway Timeout", "Computer " + computer.ID + " is not online.");
				return;
			}

			// The computer is online.  Send a request to have the Host Service connect to this Master Server's web socket proxy service.
			string proxyKey = Util.GetRandomAlphaNumericString(64);
			WaitingClient waitingClient = null;
			try
			{
				waitingClient = new WaitingClient(p);
				pendingProxyConnections[proxyKey] = waitingClient;
				host.RequestWebSocketProxy(p.RemoteIPAddress, proxyKey);

				// Wait for the connection from the Host Service.
				if (!waitingClient.clientWaitHandle.WaitOne(10000))
				{
					p.writeFailure("504 Gateway Timeout", "Computer " + computer.ID + " did not respond in a timely manner.");
					return;
				}
				Stream hostStream = waitingClient.hostProcessor?.tcpStream;
				if (hostStream == null)
				{
					p.writeFailure("500 Internal Server Error");
					Logger.Debug("hostStream was null in WebSocketProxy handler");
					return;
				}

				// The Host Service has connected.  Remove the pending connection and clean up before starting to proxy data between the sockets.
				pendingProxyConnections.TryRemove(proxyKey, out WaitingClient ignored);
				proxyKey = null;
				waitingClient.Dispose();

				// Copy data from Host Service to Web Client
				p.responseWritten = true;
				p.tcpClient.NoDelay = true;
				Console.WriteLine("Client Proxy Initialized");
				CopyStreamUntilClosed(hostStream, p.tcpStream);
			}
			finally
			{
				// If anything went wrong initializing the proxy connection, we might not have cleaned up yet.
				if (proxyKey != null)
					pendingProxyConnections.TryRemove(proxyKey, out WaitingClient ignored);

				waitingClient?.Dispose();
			}
		}
		/// <summary>
		/// Handles a response from a Host Service who is responding to a web socket proxy request.
		/// </summary>
		/// <param name="p">The HttpProcessor handling the request.</param>
		public static void HandleWebSocketHostProxyResponse(HttpProcessor p)
		{
			#region Perform Validation
			// Path format: "/WebSocketHostProxy/PROXYKEY/SHRD" where "/SHRD" is the path expected by the host service's web socket server and may be any arbitrary path.
			string[] parts = p.request_url.LocalPath.Split('/').Skip(1).ToArray();
			int wsPort; // The port number the remote host service's web socket server believes that it listens on (needed for validation in some web socket servers).
			if (parts.Length < 4 || parts[0] != "WebSocketHostProxy" || !int.TryParse(parts[2], out wsPort))
			{
				p.writeFailure("400 Bad Request");
				return;
			}
			string proxyKey = parts[1]; // A proxy key which serves two purposes: 1) identifying the web socket connection request and 2) authenticating the host.
			string expectedPath = "/" + parts[3]; // The path expected by the host service's web socket server, because passing it along this way is better than hard coding it right here.
			#endregion

			// Permission is implicit because proxyKey is a 64-character random string that was shared over a previously-authenticated connection.
			if (!pendingProxyConnections.TryGetValue(proxyKey, out WaitingClient waitingClient))
			{
				p.writeFailure("404 Not Found");
				return;
			}

			// Reproduce the initial request that we've already read from the client's socket, with modifications.
			p.responseWritten = true;
			p.tcpClient.NoDelay = true;
			p.outputStream.WriteLineRN(waitingClient.clientProcessor.http_method + " " + expectedPath + " " + waitingClient.clientProcessor.http_protocol_versionstring);
			p.outputStream.WriteLineRN("Host: " + p.RemoteIPAddressStr + ":" + wsPort); // Produce a host header
			foreach (KeyValuePair<string, string> header in waitingClient.clientProcessor.httpHeadersRaw)
			{
				string keyLower = header.Key.ToLower();
				if (keyLower != "host")
					p.outputStream.WriteLineRN(header.Key + ": " + header.Value);
			}
			p.outputStream.WriteLineRN("");
			p.outputStream.Flush();

			// Save a reference to the Web Client's network stream and notify the Web Client's thread that we're ready to begin proxying data.
			Stream clientStream = waitingClient.clientProcessor.tcpStream;
			waitingClient.hostProcessor = p;
			waitingClient.clientWaitHandle.Set();

			// Copy data from Web Client to Host Service
			Console.WriteLine("Host Proxy Initialized");
			CopyStreamUntilClosed(clientStream, p.tcpStream);
		}
		private static void CopyStreamUntilClosed(Stream source, Stream target)
		{
			byte[] buf = new byte[16000];
			int read = 1;
			while (read > 0)
			{
				read = source.Read(buf, 0, buf.Length);
				if (read > 0)
					target.Write(buf, 0, read);
			}
		}

		private class WaitingClient : IDisposable
		{
			public HttpProcessor clientProcessor = null;
			public HttpProcessor hostProcessor = null;
			public EventWaitHandle clientWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
			public WaitingClient(HttpProcessor clientProcessor)
			{
				this.clientProcessor = clientProcessor;
			}

			#region IDisposable Support
			private bool disposedValue = false; // To detect redundant calls
			protected virtual void Dispose(bool disposing)
			{
				if (!disposedValue)
				{
					if (disposing)
						clientWaitHandle.Dispose();
					disposedValue = true;
				}
			}
			public void Dispose()
			{
				// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
				Dispose(true);
			}
			#endregion
		}
	}
}