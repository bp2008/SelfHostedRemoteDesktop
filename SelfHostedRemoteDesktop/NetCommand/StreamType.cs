using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.NetCommand
{
	/// <summary>
	/// All the possible stream type codes.
	/// </summary>
	public enum StreamType
	{
		/// <summary>
		/// Fragmented JPEG streaming.
		/// </summary>
		JPEG = 0,
		/// <summary>
		/// H.264 streaming.
		/// </summary>
		H264 = 1
	}
}
