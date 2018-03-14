using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SQLite;

namespace MasterServer.Database
{
	public class Computer
	{
		/// <summary>
		/// 
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// The computer display name, max length 128 characters.
		/// </summary>
		[MaxLength(128)]
		[Unique]
		[NotNull]
		[Collation("nocase")]
		public string Name { get; set; }

		/// <summary>
		/// The computer's public key for identity verification.
		/// </summary>
		[NotNull]
		public byte[] PublicKey { get; set; }

		/// <summary>
		/// The time at which this record was created, in milliseconds since the Unix Epoch.
		/// </summary>
		public long RecordCreated { get; set; }

		/// <summary>
		/// The OS name and version information.  E.g. "Windows 10 Pro v1703 b15063 (64 bit)"
		/// </summary>
		[MaxLength(128)]
		public string OS { get; set; }

		/// <summary>
		/// Version number of the Host Client application, the last time it connected.
		/// </summary>
		[MaxLength(16)]
		public string AppVersion { get; set; }

		/// <summary>
		/// Returns an array of ComputerGroupMembership to which this computer belongs.
		/// </summary>
		/// <returns></returns>
		public ComputerGroupMembership[] GetGroupMemberships()
		{
			return ServiceWrapper.db.GetComputerGroupMemberships(ID);
		}
	}
	public class ComputerAndItsGroups
	{
		public Computer Computer;
		public List<UserGroup> Groups = new List<UserGroup>();
	}
}
