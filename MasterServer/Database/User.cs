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
	public class User
	{
		/// <summary>
		/// 
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		/// <summary>
		/// The unique user name, max length 64 characters.
		/// </summary>
		[MaxLength(64)]
		[Unique]
		[NotNull]
		[Collation("nocase")]
		public string Name { get; set; }

		/// <summary>
		/// An optional, non-unique name that may override the user name in client GUIs.  Admin GUIs should not render [DisplayName] without [Name] also being present.
		/// </summary>
		[MaxLength(64)]
		public string DisplayName { get; set; }

		/// <summary>
		/// A comment about the user that is only visible to administrators.
		/// </summary>
		[MaxLength(4096)]
		public string CommentByAdmin { get; set; }

		/// <summary>
		/// The email address of the user, to be used for notifications about failed login attempts, password recovery, etc.
		/// This is optional, not unique, and not a replacement for a user name.
		/// </summary>
		[MaxLength(128)]
		public string Email { get; set; }

		/// <summary>
		/// The password, salted, hashed, and hashed again.
		/// </summary>
		[NotNull]
		public byte[] PasswordHash { get; set; }

		/// <summary>
		/// A BCrypt salt value is uniquely generated for each user and using during password hashing on the client.
		/// The raw password never needs to be sent to the server.
		/// </summary>
		[NotNull]
		[MaxLength(80)]
		public string Salt { get; set; }

		/// <summary>
		/// If true, this user has full administrator privilege and can log in to the administrative interface.
		/// </summary>
		[NotNull]
		public bool IsAdmin { get; set; } = false;

		/// <summary>
		/// If true, the web administration interface cannot delete this user or change its IsAdmin flag.  A permanent user can still be renamed, because some people may prefer this for security reasons.
		/// </summary>
		[NotNull]
		public bool Permanent { get; set; } = false;

		public User()
		{
		}

		public User(string Name, string Password, string DisplayName, string Email, bool IsAdmin)
		{
			this.Name = Name;
			this.DisplayName = DisplayName;
			this.Email = Email;
			this.Salt = Util.BCryptSalt();
			this.PasswordHash = HashPassword(Password);
			this.IsAdmin = IsAdmin;
		}
		/// <summary>
		/// Sets the password for this user.
		/// </summary>
		/// <param name="password">The password to set for this user.</param>
		public void SetPassword(string password)
		{
			if (this.Salt == null)
				this.Salt = Util.BCryptSalt();
			this.PasswordHash = HashPassword(password);
		}
		private byte[] HashPassword(string password)
		{
			// Append the salt as a hex formatted string instead of as raw data.
			// This makes it easier to deal with in JavaScript.
			byte[] onceHashedPw = Hash.GetSHA512Bytes(Encoding.UTF8.GetBytes(Util.BCryptHash(password, Salt)));
			byte[] passwordHash = Hash.GetSHA512Bytes(onceHashedPw);
			return passwordHash;
		}

		/// <summary>
		/// Authentication protocol based on: 
		/// http://openwall.info/wiki/people/solar/algorithms/challenge-response-authentication
		/// 
		/// This authentication protocol is no replacement for proper HTTPS with an SSL certificate, 
		/// and exists mostly to protect user passwords in the event that they are used with an 
		/// unsecure connection.
		/// 
		/// IMPORTANT NOTES: 
		///		1) An attacker sniffing unencryted traffic will be able to hijack active sessions 
		///			quite easily.
		///		2) Setting a password (when creating or updating an account) is not 
		///			cryptographically secured unless you use HTTPS.  If an attacker sniffs this 
		///			traffic, the account is compromised.  Further, the password will be 
		///			discoverable by a relatively easy brute force attack.
		///		3) This authentication protocol grants protection against replay attacks.  An 
		///			attacker sniffing the login protocol will not be able to authenticate new 
		///			sessions, and should not be able to discover the hashed password.
		///		4) Passwords are always salted and hashed before being transmitted, making them 
		///			resistant to attacks with rainbow tables.
		/// 
		/// The way this works is, here on the server we only store the password after it has been 
		/// salted and hashed by two iterations of SHA512. This protects user passwords in the 
		/// event that the server's user database is stolen.
		/// The client can verify its identity by sending us the password after it has been salted 
		/// and hashed only one time, and we can authenticate the user by hashing a second time on 
		/// the server.
		/// 
		/// The trick is, the client needs to transmit the once-hashed password in such a way that 
		/// the server can read it, but someone intercepting traffic can not.
		/// To achieve this, we build a token "challengeHashed" which can be easily generated by 
		/// both the client and server, but not by an attacker intercepting traffic.  The client 
		/// XORs the once-hashed password with the challengeHashed token, and transmits this result.
		/// An attacker intercepting traffic can only see the result of XORing these two tokens, and 
		/// cannot reconstruct the original tokens.  However on the server we know the value of the 
		/// challengeHashed token so we can reverse the XOR operation to obtain the once-hashed
		/// password.  Essentially, challengeHashed is a single-use encryption key.
		/// 
		/// This method returns true if the response is validated and the user is authenticated.
		/// </summary>
		/// <param name="response">A response from the user.</param>
		/// <param name="challenge">The challenge token from which the user's response was created.</param>
		/// <returns>true if the user is authenticated successfully</returns>
		public bool AuthenticateUser(string response, byte[] challenge)
		{
			byte[] responseBytes = Hex.ToByteArray(response);
			byte[] challengeHashed = Hash.GetSHA512Bytes(PasswordHash, challenge);
			byte[] onceHashedPw = ByteUtil.XORByteArrays(challengeHashed, responseBytes);
			byte[] hashedAgain = Hash.GetSHA512Bytes(onceHashedPw);
			bool authenticationSuccess = Util.ArraysEqual(hashedAgain, PasswordHash);
			return authenticationSuccess;
		}

		/// <summary>
		/// Returns an array of UserGroupMembership to which this user belongs.
		/// </summary>
		/// <returns></returns>
		public UserGroupMembership[] GetGroupMemberships()
		{
			return ServiceWrapper.db.GetUserGroupMemberships(ID);
		}

		/// <summary>
		/// Returns an array of UserGroup to which this user belongs.
		/// </summary>
		/// <returns></returns>
		public UserGroup[] GetGroups()
		{
			return ServiceWrapper.db.GetUserGroups(ID);
		}
		///// <summary>
		///// Returns a UserSettings object indicating the effective settings/permissions for this user.
		///// </summary>
		///// <returns></returns>
		//public UserSettings GetUserSettings()
		//{
		//	UserSettings us = new UserSettings();
		//	UserGroup[] groups = ServiceWrapper.db.GetUserGroups(ID);
		//	foreach (UserGroup group in groups)
		//		us.AddAbstractSettings(ServiceWrapper.db.GetGroupSettings(group.ID));
		//	us.AddAbstractSettings(ServiceWrapper.db.GetUserSettings(ID));
		//	us.ApplyInheritanceRules();
		//	return us;
		//}
	}
	public class UserAndItsGroups
	{
		public User User;
		public List<UserGroup> Groups = new List<UserGroup>();
	}

	///// <summary>
	///// This object helps collect settings from multiple sources -- direct settings from the user itself and inherited settings from the user's group(s).
	///// </summary>
	//public class UserSettings
	//{
	//	public bool mayViewUsers = false;
	//	public bool mayEditUsers = false;
	//	public bool mayAccessComputers = false;
	//	public bool mayEditComputers = false;
	//	public bool mayViewGroups = false;
	//	public bool mayEditGroups = false;
	//	public bool mayViewStatusPage = false;

	//	internal void AddAbstractSettings(AbstractSetting[] settings)
	//	{
	//		foreach (AbstractSetting s in settings)
	//		{
	//			// These privileges are simple booleans which default to false.
	//			// If any privilege is granted to the user by any group or by the user itself, then the user has that privilege.
	//			if (s.SettingsID == SettingsKey.mayViewUsers) mayViewUsers |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayEditUsers) mayEditUsers |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayAccessComputers) mayAccessComputers |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayEditComputers) mayEditComputers |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayViewGroups) mayViewGroups |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayEditGroups) mayEditGroups |= s.Value == "1";
	//			else if (s.SettingsID == SettingsKey.mayViewStatusPage) mayViewStatusPage |= s.Value == "1";
	//		}
	//	}
	//	/// <summary>
	//	/// Call after adding all AbstractSetting arrays which apply to this user.
	//	/// This method applies additional inheritance rules. E.g. if permission "mayEditUsers" is granted, then "mayViewUsers" is automatically granted as well.
	//	/// </summary>
	//	internal void ApplyInheritanceRules()
	//	{
	//		if (mayEditUsers)
	//			mayViewUsers = true;
	//		if (mayEditComputers)
	//			mayAccessComputers = true;
	//		if (mayEditGroups)
	//			mayViewGroups = true;
	//	}
	//}
}
