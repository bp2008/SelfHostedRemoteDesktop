using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHRDLib
{
	public class HostConnectResult
	{
		public string Error = null;
		/// <summary>
		/// Creates a HostConnectResult indicating a normal exit.
		/// </summary>
		public HostConnectResult() { }
		/// <summary>
		/// Creates a HostConnectResult indicating that an error occurred.
		/// </summary>
		/// <param name="error">The error message.</param>
		public HostConnectResult(string error) { this.Error = error; }
	}
}
