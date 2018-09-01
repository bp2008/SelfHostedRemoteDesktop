using System;
using System.Linq;
using System.Collections.Generic;
using BPUtil;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using MasterServer.Database;
using SHRDLib;

namespace MasterServer
{
	public static class JSONAPI
	{
		private const string jsonType = "application/json";
		public static void HandleRequest(HttpProcessor p, string jsonStr)
		{
			object response = null;
			try
			{
				dynamic requestObj = JsonConvert.DeserializeObject(jsonStr);
				ServerSession session = GetSession(requestObj);
				string cmd = Try.Get(() => (string)requestObj.cmd);
				if (cmd == "login")
				{
					if (session == null)
					{
						session = ServerSession.CreateUnauthenticated();
						SessionManager.AddSession(session);
					}
					// Get current challenge and generate a new one so the previous one can not be used again, preventing replay attacks.
					byte[] currentChallenge = session.authChallenge;
					session.authChallenge = ByteUtil.GenerateRandomBytes(32);
					// Get user
					string userName = Try.Get(() => (string)requestObj.user);
					if (string.IsNullOrEmpty(userName))
					{
						response = new ResultFailWithReason(session, "missing parameter: \"user\"");
						return;
					}
					User user = ServiceWrapper.db.GetUser(userName);

					string responseToken = Try.Get(() => (string)requestObj.response);
					if (string.IsNullOrEmpty(responseToken))
					{
						// No response token was provided. This is step 1 of authentication, where the client requests information necessary to build the response token.
						string salt;
						if (user != null)
							salt = user.Salt;
						else
						{
							salt = Util.GenerateFakeUserSalt(userName);
							Logger.Info("Fake salt \"" + salt + "\" created for user name \"" + userName + "\". Remote IP: " + p.RemoteIPAddressStr);
						}

						response = new ResultLoginChallengeAndSalt(session, salt);
					}
					else
					{
						bool authenticated = false;
						// If the user hasn't gotten a challenge token yet, currentChallenge will be blank.
						if (currentChallenge != null && currentChallenge.Length > 0)
						{
							if (user != null)
								authenticated = user.AuthenticateUser(responseToken, currentChallenge);
						}
						if (authenticated)
						{
							session.userId = user.ID;
							response = new ResultLoginSuccess(session);
						}
						else
							response = new ResultFailWithReason(session, "authentication rejected");
					}
					return;
				}
				else
				{
					if (session == null || !session.IsAuthValid)
					{
						response = new ResultFailNoSession(session);
						return;
					}
					///////////////////////////////////////////////////////////////////////////////////////////////////
					///////////////////////////////////////////////////////////////////////////////////////////////////
					// Commands after this point require an authenticated session, but not administrator permission. //
					///////////////////////////////////////////////////////////////////////////////////////////////////
					///////////////////////////////////////////////////////////////////////////////////////////////////
					User user = session.GetUser();
					if (user == null)
					{
						Logger.Debug("Session has user ID " + session.userId + " but GetUser returned null");
						response = new ResultFailWithReason(session, "session corrupted");
						return;
					}
					bool handled = true;
					switch (cmd)
					{
						case "getComputers":
							UserGroup[] userGroups = user.GetGroups();
							GetComputerGroupsResult result = new GetComputerGroupsResult();
							result.Groups = userGroups.Select(g => new GroupOfComputers(g, ServiceWrapper.db.GetComputersInGroup(g.ID))).ToArray();
							response = result;
							break;
						case "logout":
							SessionManager.RemoveSession(session.sid);
							response = new ResultSuccess();
							break;
						default:
							handled = false;
							break;
					}
					if (handled)
						return;

					if (!user.IsAdmin)
					{
						response = new ResultFailInsufficientPrivileges();
						return;
					}
					/////////////////////////////////////////////////////////////////
					/////////////////////////////////////////////////////////////////
					// Commands after this point require administrator permission. //
					/////////////////////////////////////////////////////////////////
					/////////////////////////////////////////////////////////////////
					switch (cmd)
					{
						#region admin/getComputers
						case "admin/getComputers":
							{
								ResultComputersAndTheirGroups result = new ResultComputersAndTheirGroups();

								ComputerAndItsGroups[] allComputers = ServiceWrapper.db.GetAllComputersAndTheirGroups();
								result.Computers = allComputers.Select(c => new AdminResultComputer(c)).ToArray();

								response = result;
								break;
							}
						#endregion
						#region admin/getUsers
						case "admin/getUsers":
							{
								ResultAllUsers result = new ResultAllUsers();

								UserAndItsGroups[] allUsers = ServiceWrapper.db.GetAllUsersAndTheirGroups();
								result.Users = allUsers.Select(u => new AdminResultUser(u)).ToArray();

								response = result;
								break;
							}
						#endregion
						default:
							{
								response = new ResultFailWithReason(session, "unrecognized command");
								break;
							}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				response = new ResultFail() { error = "An unexpected error occurred." };
			}
			finally
			{
				if (response == null)
					response = new ResultFail() { error = "Application Error: A response was not generated, so this response was generated as a fallback." };
				p.writeSuccess(jsonType);
				p.outputStream.Write(JsonConvert.SerializeObject(response));
			}
		}

		private static ServerSession GetSession(dynamic requestObj)
		{
			string sid = Try.Get(() => (string)requestObj.session);
			if (string.IsNullOrEmpty(sid))
				return null;
			return SessionManager.GetSession(sid);
		}
		private class ResultFail
		{
			public bool success = false;
			public string error;
		}
		private class ResultSuccess
		{
			public bool success = true;
		}
		private class ResultFailNoSession : ResultFail
		{
			public ResultFailNoSession(ServerSession session)
			{
				if (session == null)
					error = "missing session";
				else
					error = "invalid session";
			}
		}
		private class ResultFailInsufficientPrivileges : ResultFail
		{
			public ResultFailInsufficientPrivileges()
			{
				error = "insufficient privileges";
			}
		}
		private class ResultFailWithReason : ResultFail
		{
			public string session;
			/// <summary>
			/// Represents a failure response, providing a custom failure reason string.
			/// </summary>
			/// <param name="session">The user's session.  May not be null.</param>
			/// <param name="error">The reason for the failure.</param>
			public ResultFailWithReason(ServerSession session, string reason)
			{
				this.error = reason;
				this.session = session.sid;
			}
		}
		private class ResultLoginChallengeAndSalt : ResultFail
		{
			public string challenge;
			public string session;
			public string salt;
			public ResultLoginChallengeAndSalt(ServerSession session, string salt)
			{
				error = "login challenge";
				this.challenge = Hex.ToHex(session.authChallenge);
				this.session = session.sid;
				this.salt = salt;
			}
		}
		private class ResultLoginSuccess : ResultSuccess
		{
			public string session;
			public bool admin;
			public ResultLoginSuccess(ServerSession session)
			{
				this.session = session.sid;
				this.admin = session.IsAdminValid;
			}
		}
		private class GetComputerGroupsResult : ResultSuccess
		{
			public GroupOfComputers[] Groups;
		}
		private class GroupOfComputers
		{
			public int ID;
			public string Name;
			public ResultComputer[] Computers;
			public GroupOfComputers() { }
			public GroupOfComputers(UserGroup group, List<Computer> computers)
			{
				ID = group.ID;
				Name = group.Name;
				Computers = computers.Select(c => new ResultComputer(c)).ToArray();
			}
		}
		private class ResultComputer
		{
			public int ID;
			public string Name;
			public long LastDisconnect;
			public long Uptime = -1;
			/// <summary>
			/// 
			/// </summary>
			/// <param name="c">A ComputerAndItsGroups object to copy data from.</param>
			/// <param name="allowedGroupIds">If non-null, this collection of group IDs will be used to filter the computer's group list.  Pass in a collection of the user's group IDs.</param>
			public ResultComputer(Computer c)
			{
				ID = c.ID;
				Name = c.Name;
				LastDisconnect = c.LastDisconnect;
				HostConnectHandle handle = HostConnect.GetOnlineComputer(ID);
				if (handle != null)
				{
					Uptime = TimeUtil.GetTimeInMsSinceEpoch(handle.ConnectTime);
				}
			}
		}
		/// <summary>
		/// Represents a set of computers and the UserGroups they belong to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultComputersAndTheirGroups : ResultSuccess
		{
			public ResultComputerAndItsGroups[] Computers;
			public ResultComputersAndTheirGroups()
			{
			}
			public ResultComputersAndTheirGroups(ResultComputerAndItsGroups[] Computers)
			{
				this.Computers = Computers;
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents a computer and the UserGroups it belongs to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultComputerAndItsGroups
		{
			public int ID;
			public string Name;
			public ResultGroupSummary[] Groups;
			public long LastDisconnect;
			public long Uptime = -1;
			/// <summary>
			/// 
			/// </summary>
			/// <param name="c">A ComputerAndItsGroups object to copy data from.</param>
			/// <param name="allowedGroupIds">If non-null, this collection of group IDs will be used to filter the computer's group list.  Pass in a collection of the user's group IDs.</param>
			public ResultComputerAndItsGroups(ComputerAndItsGroups c, IEnumerable<int> allowedGroupIds)
			{
				ID = c.Computer.ID;
				Name = c.Computer.Name;
				Groups = c.Groups
					.Where(g => allowedGroupIds == null || allowedGroupIds.Contains(g.ID))
					.Select(g => new ResultGroupSummary(g))
					.ToArray();
				LastDisconnect = c.Computer.LastDisconnect;
				HostConnectHandle handle = HostConnect.GetOnlineComputer(ID);
				if (handle != null)
				{
					Uptime = TimeUtil.GetTimeInMsSinceEpoch(handle.ConnectTime);
				}
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents a computer and all of the UserGroups it belongs to, exposing only the information needed by a client application.
		/// </summary>
		private class AdminResultComputer : ResultComputerAndItsGroups
		{
			public string CommentByAdmin;
			public AdminResultComputer(ComputerAndItsGroups c) : base(c, null)
			{
				CommentByAdmin = c.Computer.CommentByAdmin;
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents UserGroup, exposing only the information needed by a client application.
		/// </summary>
		private class ResultGroupSummary
		{
			public int ID;
			public string Name;
			public ResultGroupSummary(UserGroup g)
			{
				ID = g.ID;
				Name = g.Name;
			}
		}
		/// <summary>
		/// Represents a set of users and the UserGroups they belong to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultAllUsers : ResultSuccess
		{
			public ResultUser[] Users;
			public ResultAllUsers()
			{
			}
			public ResultAllUsers(ResultUser[] Users)
			{
				this.Users = Users;
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents a user and the UserGroups it belongs to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultUser
		{
			public int ID;
			public string Name;
			public string DisplayName;
			public string Email;
			public bool IsAdmin;
			public ResultGroupSummary[] Groups;
			public ResultUser(UserAndItsGroups c)
			{
				ID = c.User.ID;
				Name = c.User.Name;
				DisplayName = c.User.DisplayName;
				Email = c.User.Email;
				IsAdmin = c.User.IsAdmin;
				Groups = c.Groups.Select(g => new ResultGroupSummary(g)).ToArray();
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents a user and the UserGroups it belongs to, exposing only the information needed by an admin application.
		/// </summary>
		private class AdminResultUser : ResultUser
		{
			public string CommentByAdmin;
			public AdminResultUser(UserAndItsGroups c) : base(c)
			{
				CommentByAdmin = c.User.CommentByAdmin;
			}
		}
	}
}