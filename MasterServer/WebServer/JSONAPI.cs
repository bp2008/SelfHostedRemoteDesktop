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
		private const string mimeType = "application/json";
		public static void HandleRequest(HttpProcessor p, string jsonStr)
		{
			dynamic requestObj = JsonConvert.DeserializeObject(jsonStr);
			ServerSession session = GetSession(requestObj);
			string cmd = "";
			Try.Swallow(() => { cmd = requestObj.cmd; });
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
				string userName = "";
				Try.Swallow(() => { userName = requestObj.user; });
				if (string.IsNullOrEmpty(userName))
				{
					p.writeSuccess(mimeType);
					p.outputStream.Write(JsonConvert.SerializeObject(new ResultFailWithReason(session, "missing parameter: \"user\"")));
					return;
				}
				User user = ServiceWrapper.db.GetUser(userName);

				string responseToken = "";
				Try.Swallow(() => { responseToken = requestObj.response; });
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

					p.writeSuccess(mimeType);
					p.outputStream.Write(JsonConvert.SerializeObject(new ResultLoginChallengeAndSalt(session, salt)));
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
					p.writeSuccess(mimeType);
					if (authenticated)
					{
						session.userId = user.ID;
						p.outputStream.Write(JsonConvert.SerializeObject(new ResultLoginSuccess(session)));
					}
					else
						p.outputStream.Write(JsonConvert.SerializeObject(new ResultFailWithReason(session, "authentication rejected")));
				}
				return;
			}
			else
			{
				if (session == null || !session.IsAuthValid)
				{
					p.writeSuccess(mimeType);
					p.outputStream.Write(JsonConvert.SerializeObject(new ResultFailNoSession(session)));
					return;
				}
				// Commands after this point require an authenticated session, but not administrator permission.
				User user = session.GetUser();
				if (!user.IsAdmin)
				{
					p.writeFailure("403 Forbidden");
					return;
				}
				// Commands after this point require administrator permission.
				switch (cmd)
				{
					#region admin/getMainMenu
					case "admin/getMainMenu":
						{
							//UserSettings settings = session.GetUser().GetUserSettings();

							ResultAdminMainMenu resultMainMenu = new ResultAdminMainMenu();

							List<AdminMainMenuItem> menuItems = new List<AdminMainMenuItem>();
							menuItems.Add(new AdminMainMenuItem("Status", "status"));
							menuItems.Add(new AdminMainMenuItem("Computers", "computers"));
							menuItems.Add(new AdminMainMenuItem("Users", "users"));
							menuItems.Add(new AdminMainMenuItem("Log out", "logout"));
							resultMainMenu.menuItems = menuItems.ToArray();

							p.writeSuccess(mimeType);
							p.outputStream.Write(JsonConvert.SerializeObject(resultMainMenu));
							break;
						}
					#endregion
					#region admin/getComputers
					case "admin/getComputers":
						{
							ResultAllComputers result = new ResultAllComputers();

							ComputerAndItsGroups[] allComputers = ServiceWrapper.db.GetAllComputersAndTheirGroups();
							result.Computers = allComputers.Select(c => new ResultComputer(c)).ToArray();

							p.writeSuccess(mimeType);
							p.outputStream.Write(JsonConvert.SerializeObject(result));
							break;
						}
					#endregion
					#region admin/getUsers
					case "admin/getUsers":
						{
							ResultAllUsers result = new ResultAllUsers();

							UserAndItsGroups[] allUsers = ServiceWrapper.db.GetAllUsersAndTheirGroups();
							result.Users = allUsers.Select(u => new ResultUser(u)).ToArray();

							p.writeSuccess(mimeType);
							p.outputStream.Write(JsonConvert.SerializeObject(result));
							break;
						}
					#endregion
					default:
						{
							p.writeSuccess(mimeType);
							p.outputStream.Write(JsonConvert.SerializeObject(new ResultFailWithReason(session, "unrecognized command")));
							break;
						}
				}
			}
		}

		private static ServerSession GetSession(dynamic requestObj)
		{
			string sid = "";
			Try.Swallow(() => { sid = requestObj.session; });
			if (string.IsNullOrEmpty(sid))
				return null;
			return SessionManager.GetSession(sid);
		}
		private class ResultFail
		{
			public string result = "fail";
			public string reason;
		}
		private class ResultSuccess
		{
			public string result = "success";
		}
		private class ResultFailNoSession : ResultFail
		{
			public ResultFailNoSession(ServerSession session)
			{
				if (session == null)
					reason = "missing session";
				else
					reason = "invalid session";
			}
		}
		private class ResultFailInsufficientPrivileges : ResultFail
		{
			public ResultFailInsufficientPrivileges()
			{
				reason = "insufficient privileges";
			}
		}
		private class ResultFailWithReason : ResultFail
		{
			public string session;
			/// <summary>
			/// Represense a failure response, providing a custom failure reason string.
			/// </summary>
			/// <param name="session">The user's session.  May not be null.</param>
			/// <param name="reason">The reason for the failure.</param>
			public ResultFailWithReason(ServerSession session, string reason)
			{
				this.reason = reason;
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
				reason = "login challenge";
				this.challenge = Hex.ToHex(session.authChallenge);
				this.session = session.sid;
				this.salt = salt;
			}
		}
		private class ResultLoginSuccess : ResultSuccess
		{
			public string session;
			public ResultLoginSuccess(ServerSession session)
			{
				this.session = session.sid;
			}
		}
		private class ResultAdminMainMenu : ResultSuccess
		{
			public AdminMainMenuItem[] menuItems;
			public ResultAdminMainMenu()
			{
			}
		}
		private class AdminMainMenuItem : ResultSuccess
		{
			public string DisplayNameHtml;
			public string PageNameInternal;
			public AdminMainMenuItem(string DisplayNameHtml, string PageNameInternal)
			{
				this.DisplayNameHtml = DisplayNameHtml;
				this.PageNameInternal = PageNameInternal;
			}
		}
		/// <summary>
		/// Represents a set of computers and the UserGroups they belong to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultAllComputers : ResultSuccess
		{
			public ResultComputer[] Computers;
			public ResultAllComputers()
			{
			}
			public ResultAllComputers(ResultComputer[] Computers)
			{
				this.Computers = Computers;
			}
		}
		/// <summary>
		/// [Not usable as a standalone API response] Represents a computer and the UserGroups it belongs to, exposing only the information needed by a client application.
		/// </summary>
		private class ResultComputer
		{
			public int ID;
			public string Name;
			public ResultGroupSummary[] Groups;
			public long LastDisconnect;
			public long Uptime = -1;
			public ResultComputer(ComputerAndItsGroups c)
			{
				ID = c.Computer.ID;
				Name = c.Computer.Name;
				Groups = c.Groups.Select(g => new ResultGroupSummary(g)).ToArray();
				LastDisconnect = c.Computer.LastDisconnect;
				HostConnectHandle handle = HostConnect.GetOnlineComputer(ID);
				if (handle != null)
				{
					Uptime = TimeUtil.GetTimeInMsSinceEpoch(handle.ConnectTime);
				}
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
	}
}