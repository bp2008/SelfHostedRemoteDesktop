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

namespace MasterServer
{
	public class WebServer : HttpServer
	{
		private static bool enableCaching = false;
		//private static ObjectPool<byte[]> decompressedScreenshotBufferPool = new ObjectPool<byte[]>(() => null, 2);
		//private static NetworkStream webSocketProxyStream1 = null;
		//private static NetworkStream webSocketProxyStream2 = null;
		public WebServer(int port, int httpsPort = -1, X509Certificate2 cert = null) : base(port, httpsPort, cert)
		{
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			p.tcpClient.NoDelay = true;
			string pageLower = p.requestedPage.ToLower();
			if (pageLower == "json")
			{
				p.writeFailure("405 Method Not Allowed", "json API requests must use the POST method");
			}
			else if (pageLower == "admin/")
			{
				p.writeRedirect("../admin");
			}
			else if (pageLower == "admin" || pageLower.StartsWith("admin/"))
			{
				Admin.AdminPage.HandleRequest(p);
			}
			else if (p.requestedPage == "")
			{
				p.writeRedirect("test.html");
			}
			else if (pageLower == "admin")
			{
				p.writeRedirect("admin.html");
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
			else if (p.requestedPage.StartsWith("WebSocketProxy/"))
			{
				WebSocketProxy.HandleWebSocketProxyRequest(p);
			}
			//else if (p.requestedPage == "WebSocketProxy")
			//{
			//	//p.writeWebSocketProxy();
			//	p.responseWritten = true;
			//	bool isProxiedWebSocketServer = false;
			//	NetworkStream otherStream;
			//	if (webSocketProxyStream1 == null)
			//	{
			//		isProxiedWebSocketServer = true;
			//		webSocketProxyStream1 = p.tcpClient.GetStream();
			//		while (webSocketProxyStream2 == null)
			//			Thread.Sleep(1);
			//		otherStream = webSocketProxyStream2;
			//	}
			//	else if (webSocketProxyStream2 == null)
			//	{
			//		webSocketProxyStream2 = p.tcpClient.GetStream();
			//		while (webSocketProxyStream1 == null)
			//			Thread.Sleep(1);
			//		otherStream = webSocketProxyStream1;
			//		StringBuilder sb = new StringBuilder();
			//		sb.Append(p.http_method + " /SHRD " + p.http_protocol_versionstring + "\r\n");
			//		foreach (KeyValuePair<string, string> kvp in p.httpHeadersRaw)
			//		{
			//			if (kvp.Key.ToLower() == "host")
			//				sb.Append(kvp.Key + ": 192.168.0.120:8089\r\n");
			//			else
			//				sb.Append(kvp.Key + ": " + kvp.Value + "\r\n");
			//		}
			//		sb.Append("\r\n");
			//		byte[] buf = Encoding.UTF8.GetBytes(sb.ToString());
			//		otherStream.Write(buf, 0, buf.Length);
			//		otherStream.Flush();
			//	}
			//	else
			//		return;

			//	byte[] buffer = new byte[64 * 1024];
			//	int read;
			//	do
			//	{
			//		read = otherStream.Read(buffer, 0, buffer.Length);
			//		if (read > 0)
			//			p.rawOutputStream.Write(buffer, 0, read);
			//		else
			//			p.rawOutputStream.Flush();
			//	}
			//	while (read > 0);
			//}
			else
			{
				#region www
				DirectoryInfo WWWDirectory = new DirectoryInfo(ServiceWrapper.settings.GetWWWDirectoryBase());
				string wwwDirectoryBase = WWWDirectory.FullName.Replace('\\', '/').TrimEnd('/') + '/';
				FileInfo fi = new FileInfo(wwwDirectoryBase + p.requestedPage);
				string targetFilePath = fi.FullName.Replace('\\', '/');
				if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
				{
					p.writeFailure("400 Bad Request");
					return;
				}
				if (!fi.Exists)
				{
					p.writeFailure();
					return;
				}

				if ((fi.Extension == ".html" || fi.Extension == ".htm") && fi.Length < 256000)
				{
					string html = File.ReadAllText(fi.FullName);
					try
					{
						html = html.Replace("%REMOTEIP%", p.RemoteIPAddress);
						//html = html.Replace("%SYSTEM_NAME%", ServiceWrapper.settings.systemName);
						html = html.Replace("%APP_VERSION%", AppVersion.VersionNumber);
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

		//private byte[] GetByteArrayOfSize(int requiredBufferSize)
		//{
		//	byte[] buf;
		//	int attempts = 0;
		//	do
		//	{
		//		buf = decompressedScreenshotBufferPool.GetObject(() => new byte[requiredBufferSize]);
		//	}
		//	while (buf.Length != requiredBufferSize && ++attempts < 5);
		//	if (buf.Length != requiredBufferSize)
		//		buf = new byte[requiredBufferSize];
		//	return buf;
		//}

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			string pageLower = p.requestedPage.ToLower();
			if (pageLower == "json")
			{
				JSONAPI.HandleRequest(p, inputData.ReadToEnd());
			}
			else if (pageLower == "admin" || pageLower.StartsWith("admin/"))
			{
				Admin.AdminPage.HandleRequest(p);
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
