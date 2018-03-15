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
		/// <summary>
		/// The web address of the master server.
		/// </summary>
		public string MasterServerAddress = "http://192.168.0.154:8088/";
		/// <summary>
		/// If true, the server must present a verifiable certificate (e.g. not self-signed).
		/// </summary>
		public bool ValidateServerCertificate = false;

		///// <summary>
		///// Gets the web address of the master server, running a simple validation pass to solve issues like a missing forward slash at the end of the string.
		///// </summary>
		///// <returns></returns>
		//public string GetCleanMasterServerAddress()
		//{
		//	if (Uri.TryCreate(MasterServerAddress, UriKind.Absolute, out Uri uri))
		//		return uri.ToString();
		//	return MasterServerAddress;
		//}
		//public int httpPort = 8088;
		//public int webSocketPort = 8089; // TODO: Make the web server share the same port as the web socket server.
		//public string wwwDirectoryOverride = ""; // TODO: This is only needed for development; move this value elsewhere.

		//public string GetWWWDirectoryBase()
		//{
		//	string dir = wwwDirectoryOverride;
		//	if (string.IsNullOrWhiteSpace(dir))
		//		dir = Globals.ApplicationDirectoryBase + "www/";
		//	return dir;
		//}
	}
}
