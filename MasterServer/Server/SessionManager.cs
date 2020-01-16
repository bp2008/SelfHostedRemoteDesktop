using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using BPUtil;

namespace MasterServer
{
	public static class SessionManager
	{
		/// <summary>
		/// The maximum amount of time, in milliseconds, that a session should remain active if no requests are received using it.
		/// </summary>
		public const long SessionMaxIdleTime = 10 * 60 * 1000; // 10 minutes
		/// <summary>
		/// The maximum amount of time, in milliseconds, that an UNAUTHENTICATED session should remain active.
		/// </summary>
		public const long SessionMaxUnauthenticatedTime = 15 * 1000; // 15 seconds

		/// <summary>
		/// Gets the current time in milliseconds since the Unix Epoch (1970/1/1 midnight UTC).
		/// </summary>
		public static long CurrentTime { get { return TimeUtil.GetTimeInMsSinceEpoch(); } }

		/// <summary>
		/// The CurrentTime value at the moment SessionManager was created.
		/// </summary>
		private static long StartTime = CurrentTime;

		/// <summary>
		/// Number of milliseconds to wait between maintenance loops.
		/// </summary>
		private const long MaintenanceInterval = 60000;

		/// <summary>
		/// The next maintenance should not occur before this time value (CurrentTime).
		/// </summary>
		private static long NextMaintenance = CurrentTime + MaintenanceInterval;

		private static ConcurrentDictionary<string, ServerSession> activeSessions = new ConcurrentDictionary<string, ServerSession>();

		/// <summary>
		/// Attempts to return the specified session, if it is in the active sessions map and has not expired.  If the specified session is expired or not found, returns null.
		/// </summary>
		/// <param name="sid">The session's Session ID string.</param>
		/// <param name="touch">If false, the session won't be touched.  Pass false if you are looking at an existing session without owning it.</param>
		/// <returns></returns>
		public static ServerSession GetSession(string sid, bool touch = true)
		{
			Maintain();
			activeSessions.TryGetValue(sid, out ServerSession session);
			if (session != null && session.Expired)
			{
				RemoveSession(sid);
				session = null;
			}
			else if (session != null && touch)
				session.TouchNow();
			return session;
		}
		/// <summary>
		/// Adds a session to the active sessions map.
		/// </summary>
		/// <param name="session"></param>
		public static void AddSession(ServerSession session)
		{
			Maintain();
			activeSessions.TryAdd(session.sid, session);
		}

		/// <summary>
		/// Attempts to remove and return the specified session, logging it out.  If the specified session is not found, returns null.
		/// </summary>
		/// <param name="sid">The session's Session ID string.</param>
		/// <returns></returns>
		public static ServerSession RemoveSession(string sid)
		{
			activeSessions.TryRemove(sid, out ServerSession session);
			return session;
		}

		/// <summary>
		/// Runs session maintenance if necessary.  It becomes necessary every 1 minute.
		/// Traditionally this sort of logic would run on a background thread, but I figure we don't need the overhead of that.
		/// </summary>
		private static void Maintain()
		{
			if (CurrentTime > NextMaintenance)
			{
				NextMaintenance = CurrentTime + MaintenanceInterval;
				try
				{
					foreach (ServerSession session in activeSessions.Values)
					{
						if (session.Expired)
							RemoveSession(session.sid);
					}
				}
				catch (ThreadAbortException) { throw; }
				catch (Exception ex)
				{
					Logger.Debug(ex);
				}
			}
		}
	}
}