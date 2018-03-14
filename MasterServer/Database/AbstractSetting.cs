using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace MasterServer.Database
{
	public class AbstractSetting
	{
		/// <summary>
		/// The unique ID of this setting.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// The ID of the UserGroup, User, or Computer for which this setting applies.
		/// </summary>
		[NotNull]
		public int ForeignKey { get; set; }

		/// <summary>
		/// The type of foreign object (UserGroup, User, or Computer) which the ForeignKey is.
		/// </summary>
		[NotNull]
		public ForeignKeyType ForeignKeyType { get; set; }

		/// <summary>
		/// The ID of the setting, which maps to a SettingsKey enum value.
		/// </summary>
		[NotNull]
		public SettingsKey SettingsID { get; set; }

		/// <summary>
		/// The value of the setting.
		/// </summary>
		[NotNull]
		public string Value { get; set; }

		public AbstractSetting()
		{
		}
		public AbstractSetting(SettingsKey SettingsID, string Value)
		{
			this.SettingsID = SettingsID;
			this.Value = Value;
		}
		public AbstractSetting(SettingsKey SettingsID, bool Value)
		{
			this.SettingsID = SettingsID;
			this.Value = Value ? "1" : "0";
		}
	}
	public enum SettingsKey
	{
		mayViewUsers = 0
	   , mayEditUsers = 1
	   , mayAccessComputers = 2
	   , mayEditComputers = 3
	   , mayViewGroups = 4
	   , mayEditGroups = 5
	   , mayViewStatusPage = 6
	}
	public enum ForeignKeyType
	{
		UserGroup = 0
	   , User = 1
	   , Computer = 2
	}
}
