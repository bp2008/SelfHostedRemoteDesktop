using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.NetCommand
{
	[Flags]
	public enum ImgFlags : byte
	{
		/// <summary>
		/// Grayscale is a special case with value 0.  Do not use HasFlag with it; instead if none of the Color flags exist, Grayscale should be assumed.
		/// </summary>
		Grayscale = 0b0000_0000
		, Color420 = 0b0000_0001
		, Color440 = 0b0000_0010
		, Color444 = Color420 | Color440
		/// <summary>A full desktop refresh is requested, not differential</summary>
		, Refresh = 0b0000_0100
	}
}
