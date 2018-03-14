using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SQLite;

namespace MasterServer.Database
{
	public class ComputerGroupMembership
	{
		/// <summary>
		/// A unique identifier for this membership record.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// A group ID.
		/// </summary>
		[NotNull]
		public int GroupID { get; set; }

		/// <summary>
		/// The computer ID of a computer belonging to this group.
		/// </summary>
		[NotNull]
		public int ComputerID { get; set; }

		public ComputerGroupMembership()
		{
		}
		public ComputerGroupMembership(int GroupID, int ComputerID)
		{
			this.GroupID = GroupID;
			this.ComputerID = ComputerID;
		}
	}
}
