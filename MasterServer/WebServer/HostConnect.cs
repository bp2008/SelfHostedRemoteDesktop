using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using BPUtil;
using BPUtil.SimpleHttp;
using MasterServer.Database;
using SHRDLib;
using SHRDLib.NetCommand;

namespace MasterServer
{
	public static class HostConnect
	{
		private static class ProtocolErrors
		{
			public const string AuthResponseLength = "Authentication response length must be greater than zero.";
			public const string SignatureLength = "Authentication response specified 0-length signature.";
			public const string SignatureVerificationFailed = "Signature verification failed.";
			public const string FailedToAddComputer = "Failed to add computer.";
			public const string AuthResponseCommand = "Authentication response must begin with Command.ClientAuthentication.";
		}
		/// <summary>
		/// A dictionary of currently connected Host Services.
		/// </summary>
		private static ConcurrentDictionary<int, HostConnectHandle> hosts = new ConcurrentDictionary<int, HostConnectHandle>();
		/// <summary>
		/// Implements the server-side part of Self Hosted Remote Desktop's HostConnect protocol.  Called by the web server when a remote Host Service POSTs to url /hostconnect
		/// </summary>
		/// <param name="p">The HttpProcessor instance.</param>
		public static HostConnectResult HandleHostService(HttpProcessor p)
		{
			Computer computer;
			#region Authentication Protocol
			{
				// Auth 0) Send ClientAuthentication command code.
				p.tcpStream.WriteByte((byte)Command.ClientAuthentication);

				// Auth 1) Send authentication challenge.  This is an array of 32 random bytes which the Host Service must sign with its private key.
				byte[] authChallenge = ByteUtil.GenerateRandomBytes(32);
				p.tcpStream.Write(authChallenge, 0, authChallenge.Length);

				// Auth 2) Receive authentication reply.
				Command receivedCommand = (Command)ByteUtil.ReadNBytes(p.tcpStream, 1)[0];
				if (receivedCommand != Command.ClientAuthentication)
					return new HostConnectResult(ProtocolErrors.AuthResponseCommand);

				ushort authResponseLength = ByteUtil.ReadUInt16(p.tcpStream);
				if (authResponseLength == 0)
					return new HostConnectResult(ProtocolErrors.AuthResponseLength);

				// Auth 2.1) Host Service is attempting authentication.
				// Read the signature.
				byte[] authResponse = ByteUtil.ReadNBytes(p.tcpStream, authResponseLength);
				int offset = 0;
				ushort signatureLength = ByteUtil.ReadUInt16(authResponse, offset);
				offset += 2;
				if (signatureLength == 0)
					return new HostConnectResult(ProtocolErrors.SignatureLength);
				byte[] signature = ByteUtil.SubArray(authResponse, offset, signatureLength);
				offset += signatureLength;

				// Auth 2.2) Read the computer ID this Host Service claims to be.
				int computerId = ByteUtil.ReadInt32(authResponse, offset);
				offset += 4;


				if (computerId == -1)
				{
					// Auth 2.2.1) This Host Service has no computer ID, so we can't associate it with a previously existing computer.
					// Because the Host Service sent a computer ID of -1, it will now be sending its computer name and public key.
					// A record for this machine will be created in the database, in the Uncategorized group.
					byte nameLength = authResponse[offset];
					offset++;
					string name = ByteUtil.ReadUtf8(authResponse, offset, nameLength);
					offset += nameLength;

					ushort publicKeyLength = ByteUtil.ReadUInt16(authResponse, offset);
					offset += 2;
					byte[] publicKey = ByteUtil.SubArray(authResponse, offset, publicKeyLength);
					offset += publicKeyLength;

					computer = new Computer();
					computer.Name = name;
					computer.PublicKey = publicKey;
				}
				else
				{
					// Auth 2.2.2) Load the specified computer ID from the database.
					computer = ServiceWrapper.db.GetComputer(computerId);
					if (computer == null) // Authentication failures all use the same response so an attacker can't tell if the computer ID was valid or not.
						return new HostConnectResult(ProtocolErrors.SignatureVerificationFailed);
				}
				// Auth 2.3) Read the Host version and OS version strings.
				byte hostVersionLength = authResponse[offset];
				offset++;
				computer.AppVersion = ByteUtil.ReadUtf8(authResponse, offset, hostVersionLength);
				offset += hostVersionLength;

				byte osVersionLength = authResponse[offset];
				offset++;
				computer.OS = ByteUtil.ReadUtf8(authResponse, offset, osVersionLength);
				offset += osVersionLength;

				// Auth 2.4) Signature Verification
				if (!IdentityVerification.VerifySignature(authChallenge, computer.PublicKey, signature))
					return new HostConnectResult(ProtocolErrors.SignatureVerificationFailed);

				if (computerId == -1)
				{
					// Auth 2.5) Add this new Computer to the database.
					try
					{
						ServiceWrapper.db.AddComputer(computer);
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
						return new HostConnectResult(ProtocolErrors.FailedToAddComputer);
					}
				}
			}
			#endregion

			// A 75 second receive timeout allows for keepalive packets every 60 seconds to be received, and disconnections should always be detected within 75 seconds.
			p.tcpClient.ReceiveTimeout = 75000;
			p.tcpClient.SendTimeout = 10000;

			// Create a HostConnectHandle for this computer to take over responsibility for the connection.
			HostConnectHandle handle = new HostConnectHandle(computer.ID, p, p.tcpStream);
			hosts.AddOrUpdate(computer.ID, handle, (id, existing) =>
			{
				// We have a handle for this host already, so just disconnect the old one.  It probably just hasn't timed out yet.
				existing.Disconnect();
				return handle;
			});
			try
			{
				handle.ListenLoop();
			}
			finally
			{
				if (hosts.TryRemove(computer.ID, out HostConnectHandle existing))
					existing.Disconnect();
			}

			return new HostConnectResult();
		}
		/// <summary>
		/// If the specified computer is currently connected to this Master Server, returns its HostConnectHandle.  Otherwise, returns null. 
		/// </summary>
		/// <param name="computerId">The ID of the computer.</param>
		/// <returns></returns>
		public static HostConnectHandle GetOnlineComputer(int computerId)
		{
			if (hosts.TryGetValue(computerId, out HostConnectHandle host))
				return host;
			return null;
		}
	}
	public class HostConnectResult
	{
		public string Error = null;
		/// <summary>
		/// Creates a HostConnectResult indicating a normal exit.
		/// </summary>
		public HostConnectResult() { }
		/// <summary>
		/// Creates a HostConnectResult indicating that an error occurred.
		/// </summary>
		/// <param name="error">The error message.</param>
		public HostConnectResult(string error) { this.Error = error; }
	}
	/// <summary>
	/// Provides access to a remote Host Service.
	/// </summary>
	public class HostConnectHandle
	{
		public readonly int ComputerID;
		private Stream stream;
		private HttpProcessor p;
		/// <summary>
		/// Lock this object before writing to [stream] or p.rawOutputStream or any other stream instance inheriting from these.
		/// Do not attempt to read from the network stream except in the same thread as ListenLoop is running in.
		/// </summary>
		private object writeLock = new object();
		public HostConnectHandle() { }
		public HostConnectHandle(int computerId, HttpProcessor p, Stream stream)
		{
			this.ComputerID = computerId;
			this.p = p;
			this.stream = stream;
		}
		/// <summary>
		/// Listens for incoming traffic.  This should be the only thread which reads from the [stream].
		/// </summary>
		internal void ListenLoop()
		{
			#region Command Loop
			while (p.tcpClient.Connected)
			{
				Command command = (Command)ByteUtil.ReadNBytes(stream, 1)[0];
				switch (command)
				{
					case Command.HostStatus:
						{
							int hostStatusLength = ByteUtil.ReadInt32(stream);
							HostStatus hostStatus = new HostStatus(ByteUtil.ReadNBytesToDataStream(stream, hostStatusLength), hostStatusLength);

							break;
						}
					case Command.KeepAlive:
						break;
					default:
						lock (writeLock)
						{
							p.tcpStream.WriteByte((byte)Command.Error_CommandCodeUnknown);
						}
						break;
				}
			}
			#endregion
		}
		public void RequestWebSocketProxy(string strProxyKey)
		{
			lock (writeLock)
			{
				byte[] proxyKey = ByteUtil.Utf8NoBOM.GetBytes(strProxyKey);
				p.tcpStream.WriteByte((byte)Command.WebSocketConnectionRequest);
				ByteUtil.WriteUInt16((ushort)proxyKey.Length, p.tcpStream);
				p.tcpStream.Write(proxyKey, 0, proxyKey.Length);
			}
		}
		/// <summary>
		/// Causes Master Server to disconnect from the host.
		/// </summary>
		public void Disconnect()
		{
			try
			{
				// TODO: Implement this as necessary.
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
	}
}