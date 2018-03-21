using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHRDLib.NetCommand
{
	public enum HostAuthenticationType : byte
	{
		/// <summary>
		/// Permanent hosts must provide their public key and sign the authentication challenge.
		/// </summary>
		PermanentHost,
		/// <summary>
		/// Temporary hosts do not need a public key, and do not need to sign the authentication challenge.
		/// </summary>
		TemporaryHost
	}
}
