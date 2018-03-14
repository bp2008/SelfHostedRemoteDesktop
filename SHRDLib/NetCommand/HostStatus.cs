using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace SHRDLib.NetCommand
{
	/// <summary>
	/// A small amount of status information for a Client Host, designed to be sent at regular intervals (1-5 minutes).
	/// </summary>
	public class HostStatus
	{
		/// <summary>
		/// [0-100] CPU Usage percent.
		/// </summary>
		public byte CPU = 255;
		/// <summary>
		/// [0-100] Memory usage percent.
		/// </summary>
		public byte MEM = 255;

		public HostStatus() { }
		public HostStatus(IDataStream s, int length)
		{
			CPU = (byte)s.ReadByte();
			MEM = (byte)s.ReadByte();
		}
	}
}
