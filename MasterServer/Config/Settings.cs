using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace MasterServer.Config
{
	public class Settings : SerializableObjectBase
	{
		public int httpPort = 8088;
		public bool devMode = false;

		public string GetWWWDirectoryBase()
		{
			if (devMode)
				return "../../../www/";
			else
				return Globals.ApplicationDirectoryBase + "www/";
		}
	}
}
