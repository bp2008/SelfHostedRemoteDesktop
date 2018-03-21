using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BPUtil;
using BPUtil.SimpleHttp;
using MasterServer.Database;
using SHRDLib;
using SHRDLib.NetCommand;

namespace MasterServer
{
	/// <summary>
	/// HostConnect class for SHRD's Master Server.
	/// </summary>
	public static class HostConnect
	{
		private static class ProtocolErrors
		{
			public const string AuthResponseLength = "Authentication response length must be greater than zero.";
			public const string SignatureLength = "Authentication response specified 0-length signature.";
			public const string SecurityKeyLength = "Authentication response specified 0-length security key.";
			public const string PublicKeyLength = "Authentication response specified 0-length public key.";
			public const string NameLength = "Authentication response specified 0-length computer name.";
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
			p.tcpClient.ReceiveTimeout = 30000;
			p.tcpClient.SendTimeout = 30000;
			Computer computer;
			#region Authentication Protocol
			{
				// Auth 0) Send ClientAuthentication command code.
				p.tcpStream.WriteByte((byte)Command.ClientAuthentication);

				// Auth 1) Send authentication challenge.  This is an array of 32 random bytes which the Host Service must sign with its private key.
				byte[] authChallenge = ByteUtil.GenerateRandomBytes(32);
				p.tcpStream.Write(authChallenge, 0, authChallenge.Length);

				// Auth 2) Receive authentication reply.  This comes in as one big block so that data can be added to the end of the block in the future without breaking existing implementations.
				Command receivedCommand = (Command)ByteUtil.ReadNBytes(p.tcpStream, 1)[0];
				if (receivedCommand != Command.ClientAuthentication)
					return new HostConnectResult(ProtocolErrors.AuthResponseCommand);

				ushort authResponseLength = ByteUtil.ReadUInt16(p.tcpStream);
				if (authResponseLength == 0)
					return new HostConnectResult(ProtocolErrors.AuthResponseLength);
				using (MemoryDataStream authResponse = new MemoryDataStream(p.tcpStream, authResponseLength))
				{
					// Auth 2.1) Read authentication type.
					HostAuthenticationType authType = (HostAuthenticationType)authResponse.ReadByte();

					// Auth 2.2) Read the security key that was created when the host download was provisioned.
					int securityKeyLength = authResponse.ReadByte();
					if (securityKeyLength == 0)
						return new HostConnectResult(ProtocolErrors.SecurityKeyLength);
					string securityKey = authResponse.ReadUtf8(securityKeyLength);


					// Auth 2.3) Read the signature.
					ushort signatureLength = authResponse.ReadUInt16();
					if (signatureLength == 0 && authType == HostAuthenticationType.PermanentHost)
						return new HostConnectResult(ProtocolErrors.SignatureLength);
					byte[] signature = authResponse.ReadNBytes(signatureLength);

					// Auth 2.4) Read the public key. This is used to identify and authenticate the computer.
					ushort publicKeyLength = authResponse.ReadUInt16();
					if (publicKeyLength == 0 && authType == HostAuthenticationType.PermanentHost)
						return new HostConnectResult(ProtocolErrors.PublicKeyLength);
					string publicKey = authResponse.ReadUtf8(publicKeyLength);

					// Auth 2.5) Read the computer name.
					int nameLength = authResponse.ReadByte();
					if (nameLength == 0)
						return new HostConnectResult(ProtocolErrors.NameLength);
					string name = authResponse.ReadUtf8(nameLength);

					// Get computer from database
					computer = ServiceWrapper.db.GetComputerByPublicKey(publicKey);
					bool computerIsNew = computer == null;
					if (computerIsNew)
					{
						computer = new Computer();
						computer.PublicKey = publicKey;
						computer.Name = name; // Only set the name if this is the first time we've seen the computer.  Future name changes will only happen in the SHRD administration interface.
					}

					// Auth 2.6) Read the Host version string.
					int hostVersionLength = authResponse.ReadByte();
					computer.AppVersion = authResponse.ReadUtf8(hostVersionLength);

					// Auth 2.7) Read the OS version string.
					int osVersionLength = authResponse.ReadByte();
					computer.OS = authResponse.ReadUtf8(osVersionLength);

					// Signature Verification
					if (authType == HostAuthenticationType.PermanentHost &&
						!IdentityVerification.VerifySignature(authChallenge, computer.PublicKey, signature))
						return new HostConnectResult(ProtocolErrors.SignatureVerificationFailed);

					// Add or update this Computer in the database.
					try
					{
						if (computerIsNew)
							ServiceWrapper.db.AddComputer(computer);
						else
							ServiceWrapper.db.UpdateComputer(computer);
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

			// This is the Master Server, which is responsible for sending a KeepAlive packet after 120 seconds of sending inactivity.  The Host Service will do the same on a 60 second interval.
			// A 75 second timeout means that disconnections should always be detected within 75 seconds of connection loss.  I don't know how long the underlying TCP stacks will wait, so this provides some measure of a guarantee that we don't wait excessively long.
			p.tcpClient.ReceiveTimeout = 75000; // 60 seconds + 15 seconds for bad network conditions.
			p.tcpClient.SendTimeout = 75000;

			// TODO: Send KeepAlive packets every 120 seconds if no other packets have been sent.

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
	/// <summary>
	/// Provides access to a remote Host Service.
	/// </summary>
	public class HostConnectHandle
	{
		/// <summary>
		/// The computer ID of the computer represented by this HostConnectHandle.
		/// </summary>
		public readonly int ComputerID;
		/// <summary>
		/// The DateTime.UtcNow value at the moment host authentication passed and this HostConnectHandle was created.
		/// </summary>
		public readonly DateTime ConnectTime;
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
			this.ConnectTime = DateTime.UtcNow;
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
							HostStatus hostStatus = new HostStatus(stream, hostStatusLength);

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
		public void RequestWebSocketProxy(IPAddress sourceIp, string strProxyKey)
		{
			lock (writeLock)
			{
				p.tcpStream.WriteByte((byte)Command.WebSocketConnectionRequest);

				// Write proxy key as string.  This is all the Host Service actually needs to be able to initiate the proxied web socket connection.
				byte[] proxyKey = ByteUtil.Utf8NoBOM.GetBytes(strProxyKey);
				ByteUtil.WriteUInt16((ushort)proxyKey.Length, p.tcpStream);
				p.tcpStream.Write(proxyKey, 0, proxyKey.Length);

				// Write the IP address as a string.  A Host Service could find this useful for logging or filtering purposes.
				byte[] ipData = ByteUtil.Utf8NoBOM.GetBytes(sourceIp.ToString());
				ByteUtil.WriteUInt16((ushort)ipData.Length, p.tcpStream);
				p.tcpStream.Write(ipData, 0, ipData.Length);
			}
		}
		/// <summary>
		/// Causes Master Server to disconnect from the host.
		/// </summary>
		public void Disconnect()
		{
			// TODO: Implement this as necessary.
			try
			{
				ServiceWrapper.db.UpdateComputerLastDisconnectTime(ComputerID);
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
	}
}