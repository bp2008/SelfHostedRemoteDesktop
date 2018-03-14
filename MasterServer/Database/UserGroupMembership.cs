using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SQLite;

namespace MasterServer.Database
{
	public class UserGroupMembership
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
		/// The user ID of a user belonging to this group.
		/// </summary>
		[NotNull]
		public int UserID { get; set; }

		public UserGroupMembership()
		{
		}
		public UserGroupMembership(int GroupID, int UserID)
		{
			this.GroupID = GroupID;
			this.UserID = UserID;
		}
	}
}
