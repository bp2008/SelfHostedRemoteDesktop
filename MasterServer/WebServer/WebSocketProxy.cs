using System;
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
		/// Handles a request from a Web Client who is requesting a proxied web socket connection to a specific Host service.
		/// </summary>
		/// <param name="p"></param>
		public static void HandleWebSocketProxyRequest(HttpProcessor p)
		{
			#region Verify Permission
			string[] parts = p.request_url.Segments;
			if (parts.Length != 3)
			{
				p.writeFailure("400 Bad Request");
				return;
			}
			if (!int.TryParse(parts[1], out int computerId))
			{
				p.writeFailure("400 Bad Request");
				return;
			}
			string sid = parts[2];
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
					Logger.Info("Non-admin user " + user.ID + " (" + user.Name + ") attempted to access computer " + computer.ID + " (" + computer.Name + ") but computer has no group memberships");
					p.writeFailure("403 Forbidden");
					return;
				}
				UserGroupMembership[] ugm = user.GetGroupMemberships();
				if (ugm.Length == 0)
				{
					Logger.Info("Non-admin user " + user.ID + " (" + user.Name + ") attempted to access computer " + computer.ID + " (" + computer.Name + ") but user has no group memberships");
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
			// TODO: Send a request to have it connect to the web socket proxy service.
			string key = Util.GetRandomAlphaNumericString(64);
			host.RequestWebSocketProxy(key);

			// Wait for the connection from the Host Service

			Stream proxyStream = null; // TODO: Wait for the connection from the Host Service.

			// Begin proxying data to and from the host service.
			p.responseWritten = true;

			Thread parentThread = Thread.CurrentThread;
			Thread ResponseProxyThread = new Thread(() =>
			{
				try
				{
					CopyStreamUntilClosed(proxyStream, p.tcpStream);
				}
				catch (ThreadAbortException) { }
				catch (Exception ex)
				{
					if (ex.InnerException is ThreadAbortException)
						return;
					Logger.Debug(ex);
				}
			});
			ResponseProxyThread.IsBackground = true;
			ResponseProxyThread.Start();

			NetworkStream stream = p.tcpClient.GetStream();
			CopyStreamUntilClosed(proxyStream, stream);
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
	}
}