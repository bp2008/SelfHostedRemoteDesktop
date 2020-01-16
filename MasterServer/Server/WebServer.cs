using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using BPUtil.SimpleHttp;
using BPUtil;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Net.Sockets;
using SHRDLib;
using System.Net;

namespace MasterServer
{
	public class WebServer : HttpServer
	{
		private static bool enableCaching = false; // TODO: Enable caching
		private WebpackProxy webpackProxy = null;
		public WebServer(int port, int httpsPort = -1, X509Certificate2 cert = null) : base(port, httpsPort, cert)
		{
			if (ServiceWrapper.settings.devMode)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine("Starting web server in dev mode. Webpack Proxy is enabled.");
				Console.ResetColor();
				webpackProxy = new WebpackProxy(9000, Globals.ApplicationDirectoryBase + "../../../");
			}
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			BasicEventTimer bet = new BasicEventTimer();
			bet.Start("GET " + p.requestedPage);
			try
			{
				p.tcpClient.NoDelay = true;
				string pageLower = p.requestedPage.ToLower();
				if (pageLower == "json")
				{
					p.writeFailure("405 Method Not Allowed", "json API requests must use the POST method");
				}
				else if (p.requestedPage.StartsWith("WebSocketClientProxy/"))
				{
					WebSocketProxy.HandleWebSocketClientProxyRequest(p);
				}
				else if (p.requestedPage.StartsWith("WebSocketHostProxy/"))
				{
					WebSocketProxy.HandleWebSocketHostProxyResponse(p);
				}
				//else if (p.requestedPage == "windowskeycodes")
				//{
				//	p.writeSuccess();
				//	p.outputStream.Write("<table><thead><tr><th>KeyCode</th><th>Name</th></tr></thead><tbody>");
				//	HashSet<int> addedKeyCodes = new HashSet<int>();
				//	foreach (int keyCode in ((IEnumerable<int>)Enum.GetValues(typeof(System.Windows.Forms.Keys))))
				//	{
				//		p.outputStream.Write("<tr><td>" + keyCode + "</td><td>" + (System.Windows.Forms.Keys)keyCode + "</td></tr>");
				//	}
				//	p.outputStream.Write("</tbody></table>");
				//}
				else
				{
					#region www
					DirectoryInfo WWWDirectory = new DirectoryInfo(ServiceWrapper.settings.GetWWWDirectoryBase());
					string wwwDirectoryBase = WWWDirectory.FullName.Replace('\\', '/').TrimEnd('/') + '/';

					FileInfo fi = null;
					if (p.requestedPage == "")
						fi = GetDefaultFile(wwwDirectoryBase);
					else
					{
						try
						{
							fi = new FileInfo(wwwDirectoryBase + p.requestedPage);
						}
						catch
						{
							fi = GetDefaultFile(wwwDirectoryBase);
						}
					}
					string targetFilePath = fi.FullName.Replace('\\', '/');
					if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
					{
						p.writeFailure("400 Bad Request");
						return;
					}
					if (webpackProxy != null)
					{
						// Handle hot module reload provided by webpack dev server.
						switch (fi.Extension.ToLower())
						{
							case ".js":
							case ".map":
							case ".css":
							case ".json":
								bet.Start("Proxy Start");
								webpackProxy.Proxy(p);
								bet.Start("Proxy End");
								return;
						}
					}
					if (!fi.Exists)
					{
						fi = GetDefaultFile(wwwDirectoryBase);
						if (!fi.Exists)
						{
							p.writeFailure();
							return;
						}
					}

					if ((fi.Extension == ".html" || fi.Extension == ".htm") && fi.Length < 256000)
					{
						bet.Start("Write HTML");
						string html = File.ReadAllText(fi.FullName);
						try
						{
							//html = html.Replace("%%REMOTEIP%%", p.RemoteIPAddressStr);
							html = html.Replace("%%SYSTEM_NAME%%", "SHRD");
							html = html.Replace("%%APP_VERSION%%", AppVersion.VersionNumber);
							html = html.Replace("%%APPPATH%%", "/");
						}
						catch (Exception ex)
						{
							Logger.Debug(ex);
						}
						p.writeSuccess(Mime.GetMimeType(fi.Extension));
						p.outputStream.Write(html);
						p.outputStream.Flush();
					}
					else
					{
						bet.Start("Write Response");
						if (fi.LastWriteTimeUtc.ToString("R") == p.GetHeaderValue("if-modified-since"))
						{
							p.writeSuccess(Mime.GetMimeType(fi.Extension), -1, "304 Not Modified");
							return;
						}
						using (FileStream fs = fi.OpenRead())
						{
							p.writeSuccess(Mime.GetMimeType(fi.Extension), fi.Length, additionalHeaders: GetCacheLastModifiedHeaders(TimeSpan.FromHours(1), fi.LastWriteTimeUtc));
							p.outputStream.Flush();
							fs.CopyTo(p.tcpStream);
							p.tcpStream.Flush();
						}
					}
					#endregion
				}
			}
			finally
			{
				bet.Stop();
				//Logger.Info(bet.ToString("\r\n"));
			}
		}

		private FileInfo GetDefaultFile(string wwwDirectoryBase)
		{
			return new FileInfo(wwwDirectoryBase + "Default.html");
		}

		private List<KeyValuePair<string, string>> GetCacheEtagHeaders(TimeSpan maxAge, string etag)
		{
			List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
			if (enableCaching)
			{
				additionalHeaders.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=" + (long)maxAge.TotalSeconds + ", public"));
				additionalHeaders.Add(new KeyValuePair<string, string>("ETag", etag));
			}
			return additionalHeaders;
		}
		private List<KeyValuePair<string, string>> GetCacheLastModifiedHeaders(TimeSpan maxAge, DateTime lastModifiedUTC)
		{
			List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
			if (enableCaching)
			{
				additionalHeaders.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=" + (long)maxAge.TotalSeconds + ", public"));
				additionalHeaders.Add(new KeyValuePair<string, string>("Last-Modified", lastModifiedUTC.ToString("R")));
			}
			return additionalHeaders;
		}

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			string pageLower = p.requestedPage.ToLower();
			p.tcpClient.NoDelay = true;
			if (pageLower == "json")
			{
				JSONAPI.HandleRequest(p, inputData.ReadToEnd());
			}
			else if (pageLower == "hostconnect")
			{
				HostConnectResult result = HostConnect.HandleHostService(p);
				if (result.Error != null)
					Console.WriteLine(result.Error);
			}
		}
		protected override void stopServer()
		{
		}
	}
}
