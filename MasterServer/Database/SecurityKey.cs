using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SHRDLib;
using SQLite;

namespace MasterServer.Database
{
	/// <summary>
	/// Contains a security key string and an associated permission level.  This is used by the HostConnect Authentication protocol.
	/// </summary>
	public class SecurityKey
	{
		/// <summary>
		/// A unique identifier for this security key.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// The security key string.
		/// </summary>
		[NotNull]
		[Unique]
		[MaxLength(64)]
		public string KeyString { get; set; }

		/// <summary>
		/// The permission level of this security key string.
		/// </summary>
		[NotNull]
		private byte permission { get; set; }

		[SQLite.Ignore]
		public SecurityKeyPermission Permission
		{
			get { return (SecurityKeyPermission)permission; }
			set { permission = (byte)value; }
		}

		public SecurityKey()
		{
		}
		public SecurityKey(string KeyString, SecurityKeyPermission Permission)
		{
			this.KeyString = KeyString;
			this.Permission = Permission;
		}
		/// <summary>
		/// Generates a new security key instance with the specified permission level and a new, randomized key string that is 64 characters long.
		/// </summary>
		/// <param name="Permission">The permission level to assign to the new security key.</param>
		/// <returns></returns>
		public static SecurityKey GenerateNew(SecurityKeyPermission Permission)
		{
			return new SecurityKey(Util.GetRandomAlphaNumericString(64), Permission);
		}
	}
	public enum SecurityKeyPermission : byte
	{
		/// <summary>
		/// The Security Key is disabled.  Authentication using this key should be rejected.
		/// </summary>
		Disabled = 0,
		/// <summary>
		/// The Security Key is valid only for temporary host connections.
		/// </summary>
		TemporaryHost = 1,
		/// <summary>
		/// The Security Key is valid for temporary or permanent host connections.
		/// </summary>
		PermanentHost = 2
	}
}
