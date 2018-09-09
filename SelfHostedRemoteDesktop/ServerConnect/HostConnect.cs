using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using SelfHostedRemoteDesktop.PerformanceData;
using SHRDLib;
using SHRDLib.NetCommand;

namespace SelfHostedRemoteDesktop.ServerConnect
{
	public class StateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// If not null, may contain an error message explaining the disconnection.
		/// </summary>
		public HostConnectResult result;
		/// <summary>
		/// The state of the HostConnect instance.
		/// </summary>
		public HostConnectClientState state;
		public StateChangedEventArgs(HostConnectResult result, HostConnectClientState state)
		{
			this.result = result;
			this.state = state;
		}
	}
	public enum HostConnectClientState
	{
		/// <summary>
		/// Connection is being established or authenticated.
		/// </summary>
		Connecting,
		/// <summary>
		/// Authentication is complete and this HostConnect instance is ready to send and receive commands.
		/// </summary>
		Connected,
		/// <summary>
		/// This instance is not connected to the server.
		/// </summary>
		Disconnected
	}
	/// <summary>
	/// HostConnect class for SHRD's Host Service. There should only be one of these active at a time.
	/// </summary>
	public class HostConnect
	{
		private static class ProtocolErrors
		{
			public const string AuthResponseCommand = "Authentication request must begin with Command.ClientAuthentication.";
			public const string HttpsNegotiationFailed = "HTTPS negotiation failed.";
			public const string AuthResponseTooLarge = "Authentication response was too large!";
			public const string AuthResponseSizeError = "Authentication response size calculation error!";
			public const string SecurityKeyLength = "Security key length greater than 255 bytes is not supported";
		}
		private Thread bgThread;
		private TcpClient tcpClient = null;
		private Stream stream = null;
		private object writeLock = new object();

		/// <summary>
		/// Initially Disconnected, state can be Connecting, Connected, Disconnected.
		/// </summary>
		public HostConnectClientState State { get; private set; } = HostConnectClientState.Disconnected;
		/// <summary>
		/// Called when the state of the HostConnect instance changes.  If the state changes to "Disconnected", it is the responsibility of the calling class to reconnect or not.
		/// </summary>
		public event EventHandler<StateChangedEventArgs> StateChanged = delegate { };

		public HostConnect()
		{
		}
		public void Connect()
		{
			Disconnect();
			bgThread = new Thread(BgThreadLoop);
			bgThread.Name = "HostConnect Thread";
			bgThread.IsBackground = true;
			bgThread.Start();
		}
		public void Disconnect()
		{
			if (bgThread != null)
			{
				Try.Swallow(bgThread.Abort);
				bgThread = null;
			}
			tcpClient = null;
			stream = null;
		}
		private void BgThreadLoop()
		{
			HostConnectResult result = null;
			try
			{
				result = FollowHostConnectProtocol();
				if (!string.IsNullOrEmpty(result.Error))
					Logger.Debug(result.Error);
			}
			catch (ThreadAbortException) { }
			catch (SocketException) { }
			catch (EndOfStreamException) { } // ordinary socket disconnect
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			finally
			{
				State = HostConnectClientState.Disconnected;
				StateChanged(this, new StateChangedEventArgs(result, State));
			}
		}

		private HostConnectResult FollowHostConnectProtocol()
		{
			State = HostConnectClientState.Connecting;
			StateChanged(this, new StateChangedEventArgs(null, State));

			Uri masterServerUri = new Uri(ServiceWrapper.settings.MasterServerAddress, UriKind.Absolute);

			// Ensure the certificate exists before we connect, because it can take a moment to create and we don't want the connection to time out.
			IdentityVerification.EnsureClientCertificateExists();

			tcpClient = new TcpClient();
			KeepAliveSender keepAlive = null;
			try
			{
				#region Get Connected
				tcpClient.NoDelay = true;
				tcpClient.ReceiveTimeout = 30000;
				tcpClient.SendTimeout = 30000;
				tcpClient.Connect(masterServerUri.DnsSafeHost, masterServerUri.Port);
				stream = tcpClient.GetStream();
				if (masterServerUri.Scheme == "https")
				{
					try
					{
						RemoteCertificateValidationCallback certCallback = null;
						if (!ServiceWrapper.settings.ValidateServerCertificate)
							certCallback = (sender, certificate, chain, sslPolicyErrors) => true;
						stream = new SslStream(stream, false, certCallback, null);
						((SslStream)stream).AuthenticateAsClient(masterServerUri.DnsSafeHost, null, System.Security.Authentication.SslProtocols.Tls12, ServiceWrapper.settings.ValidateServerCertificate);
					}
					catch (ThreadAbortException) { throw; }
					catch (SocketException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
						return new HostConnectResult(ProtocolErrors.HttpsNegotiationFailed);
					}
				}
				{
					// Create HTTP request.
					StringBuilder sb = new StringBuilder();
					sb.Append("POST ").Append(masterServerUri.PathAndQuery).Append("hostconnect HTTP/1.1\r\n");
					sb.Append("Host: ").Append(masterServerUri.Host).Append("\r\n");
					sb.Append("Content-Length: 0\r\n");
					sb.Append("\r\n");
					byte[] buf = ByteUtil.Utf8NoBOM.GetBytes(sb.ToString());
					stream.Write(buf, 0, buf.Length);
				}
				#endregion
				#region Authentication Protocol
				// Auth 0) Receive ClientAuthentication command code.
				Command command = (Command)ByteUtil.ReadNBytes(stream, 1)[0];
				if (command != Command.ClientAuthentication)
					return new HostConnectResult(ProtocolErrors.AuthResponseCommand);

				// Auth 1) Receive authentication challenge.  This is an array of 32 random bytes which the Host Service must sign with its private key.
				byte[] authChallenge = ByteUtil.ReadNBytes(stream, 32);

				// Auth 2) Build authentication reply.
				// Auth 2.1) Authentication type
				HostAuthenticationType authType = HostAuthenticationType.PermanentHost;

				// Auth 2.2) Encode security key
				byte[] securityKey = ByteUtil.Utf8NoBOM.GetBytes("NOT REAL");
				// TODO: Get the real security key from this exe's embedded settings.
				if (securityKey.Length > byte.MaxValue)
					return new HostConnectResult(ProtocolErrors.SecurityKeyLength);

				byte[] signature, publicKey;
				if (authType == HostAuthenticationType.PermanentHost)
				{
					// Auth 2.3) Create signature
					signature = IdentityVerification.SignAuthenticationChallenge(authChallenge);
					// Auth 2.4) Encode public key
					publicKey = ByteUtil.Utf8NoBOM.GetBytes(IdentityVerification.GetPublicKeyXML());
				}
				else
				{
					signature = new byte[0];
					publicKey = new byte[0];
				}
				// Auth 2.5) Encode computer name
				byte[] computerName = ByteUtil.Utf8NoBOM.GetBytes(Environment.MachineName);
				// Auth 2.6) Encode host version string
				byte[] appVersion = ByteUtil.Utf8NoBOM.GetBytes(AppVersion.VersionNumber);
				// Auth 2.7) Encode OS version string
				byte[] osVersion = ByteUtil.Utf8NoBOM.GetBytes(OSInfo.GetOSVersionInfo());

				// Build single buffer (not strictly necessary, but it helps us ensure we got the length calculated right).
				int calculatedLength = 1
					+ 1 + securityKey.Length
					+ 2 + signature.Length
					+ 2 + publicKey.Length
					+ 1 + computerName.Length
					+ 1 + appVersion.Length
					+ 1 + osVersion.Length;
				if (calculatedLength > ushort.MaxValue)
					return new HostConnectResult(ProtocolErrors.AuthResponseTooLarge);

				using (MemoryDataStream mds = new MemoryDataStream(calculatedLength))
				{
					mds.WriteByte((byte)authType);
					mds.WriteByte((byte)securityKey.Length);
					mds.Write(securityKey);
					mds.WriteUInt16((ushort)signature.Length);
					mds.Write(signature);
					mds.WriteUInt16((ushort)publicKey.Length);
					mds.Write(publicKey);
					mds.WriteByte((byte)computerName.Length);
					mds.Write(computerName);
					mds.WriteByte((byte)appVersion.Length);
					mds.Write(appVersion);
					mds.WriteByte((byte)osVersion.Length);
					mds.Write(osVersion);
					if (mds.Position != calculatedLength)
						return new HostConnectResult(ProtocolErrors.AuthResponseSizeError);
					mds.Seek(0, SeekOrigin.Begin);

					// Send authentication reply
					stream.WriteByte((byte)Command.ClientAuthentication);
					ByteUtil.WriteUInt16((ushort)calculatedLength, stream);
					mds.CopyTo(stream);
				}
				#endregion
				// This is the Host Service, which is responsible for sending a KeepAlive packet after 60 seconds of sending inactivity.  The Master Server will do the same on a 120 second interval.
				tcpClient.ReceiveTimeout = 135000; // 120 seconds + 15 seconds for bad network conditions.
				tcpClient.SendTimeout = 75000;

				State = HostConnectClientState.Connected;
				StateChanged(this, new StateChangedEventArgs(null, State));

				// Send KeepAlive packets every 60 seconds if no other packets have been sent.
				keepAlive = new KeepAliveSender("KeepAlive", 60000, SendKeepalive, (ignoredArg) => Disconnect());
				CommandLoop(tcpClient, stream);
			}
			finally
			{
				// Make local copies of these references so they can't become null after the null check.
				KeepAliveSender k = keepAlive;
				if (k != null)
					Try.Catch_RethrowThreadAbort(keepAlive.Stop);
				TcpClient c = tcpClient;
				if (c != null)
					Try.Catch_RethrowThreadAbort(c.Close);
			}
			return new HostConnectResult();
		}
		/// <summary>
		/// Listens for incoming commands in a loop.  This should be the only thread which reads from the [stream].
		/// </summary>
		private void CommandLoop(TcpClient tcpClient, Stream stream)
		{
			#region Command Loop
			while (tcpClient.Connected)
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
						Logger.Info("Received Command.KeepAlive");
						break;
					case Command.WebSocketConnectionRequest:
						Logger.Info("Received Command.WebSocketConnectionRequest");
						string proxyKey = ByteUtil.ReadUtf8_16(stream);
						string sourceIp = ByteUtil.ReadUtf8_16(stream);
						ServiceWrapper.BeginOutgoingWebSocketConnection(proxyKey, sourceIp);
						break;
					default:
						lock (writeLock)
						{
							stream.WriteByte((byte)Command.Error_CommandCodeUnknown);
						}
						break;
				}
			}
			#endregion
		}
		/// <summary>
		/// If it is time to send a keepalive packet, sends a keepalive packet.
		/// </summary>
		private void SendKeepalive(KeepAliveSender keepAlive)
		{
			ProtectedSend(() =>
			{
				if (keepAlive.IsTimeToKeepalive()) // Test timing after aquiring writeLock, to prevent unnecessary keepalive packets if another packet was sending while we were obtaining the lock.
				{
					keepAlive.NotifyPacketSending();
					Stream s = stream;
					if (s != null)
						s.WriteByte((byte)Command.KeepAlive);
				}
			});
		}
		/// <summary>
		/// Wrap in this all code which sends data on the socket. This ensures the "disconnected" flag is honored, that writeLock is obtained, and that disconnection is handled gracefully.
		/// </summary>
		/// <param name="action"></param>
		private void ProtectedSend(Action action)
		{
			if (State != HostConnectClientState.Connected)
				return;
			lock (writeLock)
			{
				try
				{
					action();
				}
				catch (SocketException) { Disconnect(); }
				catch (EndOfStreamException) { Disconnect(); }
			}
		}
	}
}
