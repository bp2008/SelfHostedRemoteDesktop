using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.SimpleHttp;
using SHRDLib;

namespace MasterServer.Admin
{
	public static class AdminPage
	{
		//private static ConcurrentDictionary<string, string> pageNameToFileNameMap = CreatePageNameToFileNameMap();

		//private static ConcurrentDictionary<string, string> CreatePageNameToFileNameMap()
		//{
		//	ConcurrentDictionary<string, string> dict = new ConcurrentDictionary<string, string>();
		//	dict["login"] = "admin/login.html";
		//	dict["login"] = "admin/login.html";
		//}

		public static void HandleRequest(HttpProcessor p)
		{
			string pageLower = p.requestedPage.ToLower();

			if (pageLower == "admin")
			{
				// This is like a "master page". Other pages such as login.html and status.html are partial pages which get loaded into the master page.
				WritePage(p, "admin/admin.html");
			}
			else if (pageLower == "admin/login")
			{
				WritePage(p, "admin/login.html");
			}
			else
			{
				// Beyond this point, pages require an active admin session.
				// The pages can only load correctly when POSTed because we look in POST parameters for the session string.
				ServerSession session = SessionManager.GetSession(p.GetPostParam("session"));
				if (session == null || !session.IsAdminValid)
				{
					p.writeFailure("403 Forbidden", "<div>Session invalid or expired.</div>"
						+ "<div><a href=\"admin#page=login\">Click here to re-enter.</a></div>");
					return;
				}
				if (pageLower == "admin/status")
				{
					WritePage(p, "admin/status.html");
				}
				else if (pageLower == "admin/users")
				{
					WritePage(p, "admin/users.html");
				}
				else if (pageLower == "admin/groups")
				{
					WritePage(p, "admin/groups.html");
				}
				else if (pageLower == "admin/computers")
				{
					WritePage(p, "admin/computers.html");
				}
				else if (pageLower == "admin/logout")
				{
					SessionManager.RemoveSession(session.sid);
					p.writeFailure("403 Logged Out", "Your session has been logged out.");
				}
			}
		}
		private static void WritePage(HttpProcessor p, string relativePath)
		{
			string pageFullPath = ServiceWrapper.settings.GetWWWDirectoryBase() + relativePath;
			FileInfo fi = new FileInfo(pageFullPath);
			if (fi.Exists)
			{
				string html = File.ReadAllText(fi.FullName);
				try
				{
					html = html.Replace("%REMOTEIP%", p.RemoteIPAddressStr);
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
				p.writeFailure();
			}
		}
	}
}
