using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SQLite;

namespace MasterServer.Database
{
	public class UserGroup
	{
		/// <summary>
		/// 
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// The group display name, max length 64 characters.
		/// </summary>
		[MaxLength(64)]
		[Unique]
		[NotNull]
		[Collation("nocase")]
		public string Name { get; set; }
		
		/// <summary>
		/// If true, the group cannot be renamed or deleted from the web administration interface.
		/// </summary>
		[NotNull]
		public bool Permanent { get; set; } = false;

		public UserGroup()
		{
		}

		public UserGroup(string Name)
		{
			this.Name = Name;
		}
	}
}
