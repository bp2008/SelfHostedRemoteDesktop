using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.SimpleHttp;

namespace MasterServer
{
	public class WebpackProxy
	{
		int webpackPort;
		object webpackStartLock = new object();
		bool hasTriedRecovery = false;

		public WebpackProxy(int webpackPort)
		{
			this.webpackPort = webpackPort;
		}
		public void Proxy(HttpProcessor p)
		{
			Exception ex = TryProxy(p);
			if (ex == null)
				return;
			if (!hasTriedRecovery)
			{
				lock (webpackStartLock)
				{
					if (!hasTriedRecovery)
					{
						if (TestTcpPortBind(webpackPort))
						{
							Console.Error.WriteLine("Webpack's TCP port " + webpackPort + " is closed. Trying to start webpack.");
							if (TryStartWebpack())
							{
								Stopwatch sw = new Stopwatch();
								sw.Start();
								while (sw.ElapsedMilliseconds < 10000 && !TestTcpPortOpen(webpackPort))
									Thread.Sleep(100);
							}
						}
						else
							Console.Error.WriteLine("Webpack's TCP port " + webpackPort + " is open, suggesting that webpack may be running.");
						hasTriedRecovery = true;
					}
				}
			}
			ex = TryProxy(p);
			if (ex == null)
				return;
			Logger.Debug(ex, "Failed to proxy \"" + p.request_url.AbsolutePath + "\" to webpack.");
		}
		private Exception TryProxy(HttpProcessor p)
		{
			try
			{
				p.ProxyTo("http://" + IPAddress.Loopback.ToString() + ":" + webpackPort + p.request_url.AbsolutePath, singleRequestOnly: true);
				return null;
			}
			catch (Exception ex) { return ex; }
		}
		private bool TryStartWebpack()
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/C \"npm start\"");
				psi.UseShellExecute = true;
				psi.WorkingDirectory = new DirectoryInfo(Globals.ApplicationDirectoryBase + "../../../").FullName;
				Process npm = Process.Start(psi);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Failed to start webpack (cmd.exe /C \"npm start\")");
				return false;
			}
		}
		private bool TestTcpPortBind(int port)
		{
			TcpListener tcpListener = null;
			try
			{
				tcpListener = new TcpListener(IPAddress.Loopback, port);
				tcpListener.Start();
				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				if (tcpListener != null)
					Try.Swallow(tcpListener.Stop);
			}
		}
		private bool TestTcpPortOpen(int port)
		{
			TcpClient client = null;
			try
			{
				client = new TcpClient();
				client.SendTimeout = client.ReceiveTimeout = 1000;
				client.Connect(IPAddress.Loopback, port);
				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				if (client != null)
					Try.Swallow(client.Close);
			}
		}
	}
}
