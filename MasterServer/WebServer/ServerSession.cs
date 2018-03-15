using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MasterServer.Database;
using SHRDLib;

namespace MasterServer
{
	/// <summary>
	/// Represents a session between the client and server, and persists state information.
	/// </summary>
	public class ServerSession
	{
		/// <summary>
		/// Session ID string.
		/// </summary>
		public string sid;

		/// <summary>
		/// A nonce used in authentication so the client does not have to send the password in plain text.
		/// </summary>
		public byte[] authChallenge;

		/// <summary>
		/// The ID of the authenticated user, or null until authentication is successful.
		/// </summary>
		public int? userId = null;

		/// <summary>
		/// Contains SessionManager.CurrentTime from the time this session was last touched by a user.  This value controls idle timeouts.
		/// </summary>
		private long lastTouched = 0;

		/// <summary>
		/// Returns true if the session has expired.
		/// </summary>
		public bool Expired
		{
			get
			{
				if (userId == null)
					return SessionManager.CurrentTime > lastTouched + SessionManager.SessionMaxUnauthenticatedTime;
				else
					return SessionManager.CurrentTime > lastTouched + SessionManager.SessionMaxIdleTime;
			}
		}
		/// <summary>
		/// Returns true if the session has been authenticated.
		/// </summary>
		private bool IsAuthenticated { get { return userId != null; } }

		/// <summary>
		/// Returns true if the session has been authenticated and is not expired.
		/// </summary>
		public bool IsAuthValid { get { return IsAuthenticated && !Expired; } }

		/// <summary>
		/// Returns true if the session has been authenticated, is not expired, and has admin privileges.
		/// </summary>
		public bool IsAdminValid { get { return IsAuthValid && GetUser().IsAdmin; } }

		/// <summary>
		/// Loads the user data from the database and returns a new instance of the User class.
		/// </summary>
		/// <returns></returns>
		public User GetUser()
		{
			if (userId == null)
				return null;
			return ServiceWrapper.db.GetUser(userId.Value);
		}
		/// <summary>
		/// Loads an array of UserGroup which this session's user belongs to.
		/// </summary>
		/// <returns></returns>
		public UserGroup[] GetUserGroups()
		{
			if (userId == null)
				return new UserGroup[0];
			return ServiceWrapper.db.GetUserGroups(userId.Value);
		}

		private ServerSession()
		{
			lastTouched = SessionManager.CurrentTime;
		}

		internal static ServerSession CreateUnauthenticated()
		{
			ServerSession session = new ServerSession();
			// 16 characters, each character having 62 possible values, yields (62 ^ 16 =) 47672401706823533450263330816 possible session strings.
			session.sid = Util.GetRandomAlphaNumericString(16);
			session.TouchNow();
			return session;
		}

		/// <summary>
		/// Call when the session is created or used by a user.  Prevents idle-logoff for a time.
		/// Has no effect on unauthenticated or expired sessions.
		/// </summary>
		public void TouchNow()
		{
			if (userId != null && !Expired)
				lastTouched = SessionManager.CurrentTime;
		}
	}
}
