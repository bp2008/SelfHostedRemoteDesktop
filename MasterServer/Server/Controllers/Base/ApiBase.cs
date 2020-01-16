using BPUtil.MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer.Server.Controllers.Base
{
	abstract class ApiBase : Controller
	{
		/// <summary>
		/// Enforces the requirement that the user have an active session.
		/// </summary>
		/// <returns>True if the controller is usable in the current request context.  False if the controller must not be used.</returns>
		public override bool OnAuthorization()
		{
			return true;
		}
	}
}
