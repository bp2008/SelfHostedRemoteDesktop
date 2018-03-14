using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace SelfHostedRemoteDesktop.Config
{
	public class Settings : SerializableObjectBase
	{
		public int httpPort = 8088;
		public int webSocketPort = 8089; // TODO: Make the web server share the same port as the web socket server.
		public string wwwDirectoryOverride = ""; // TODO: This is only needed for development; move this value elsewhere.

		public string GetWWWDirectoryBase()
		{
			string dir = wwwDirectoryOverride;
			if (string.IsNullOrWhiteSpace(dir))
				dir = Globals.ApplicationDirectoryBase + "www/";
			return dir;
		}
	}
}
