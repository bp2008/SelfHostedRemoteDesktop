using System;
using System.Collections.Generic;
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

namespace SelfHostedRemoteDesktop.HostConnect
{
	/// <summary>
	/// HostConnect class for SHRD's Host Service.
	/// </summary>
	public static class HostConnect
	{
		private static class ProtocolErrors
		{
			public const string AuthResponseCommand = "Authentication request must begin with Command.ClientAuthentication.";
			public const string HttpsNegotiationFailed = "HTTPS negotiation failed.";
			public const string AuthResponseTooLarge = "Authentication response was too large!";
			public const string AuthResponseSizeError = "Authentication response size calculation error!";
			public const string SecurityKeyLength = "Security key length greater than 255 bytes is not supported";
		}
		public static HostConnectResult Connect()
		{
			Uri masterServerUri = new Uri(ServiceWrapper.settings.MasterServerAddress, UriKind.Absolute);

			// Ensure the certificate exists before we connect, because it can take a moment to create and we don't want the connection to time out.
			IdentityVerification.EnsureClientCertificateExists();

			TcpClient tcpClient = new TcpClient();
			try
			{
				#region Get Connected
				tcpClient.ReceiveTimeout = 30000;
				tcpClient.SendTimeout = 30000;
				tcpClient.Connect(masterServerUri.DnsSafeHost, masterServerUri.Port);
				Stream tcpStream = tcpClient.GetStream();
				if (masterServerUri.Scheme == "https")
				{
					try
					{
						RemoteCertificateValidationCallback certCallback = null;
						if (!ServiceWrapper.settings.ValidateServerCertificate)
							certCallback = (sender, certificate, chain, sslPolicyErrors) => true;
						tcpStream = new SslStream(tcpStream, false, certCallback, null);
						((SslStream)tcpStream).AuthenticateAsClient(masterServerUri.DnsSafeHost, null, System.Security.Authentication.SslProtocols.Tls12, ServiceWrapper.settings.ValidateServerCertificate);
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
					tcpStream.Write(buf, 0, buf.Length);
				}
				#endregion
				#region Authentication Protocol
				// Auth 0) Receive ClientAuthentication command code.
				Command command = (Command)tcpStream.ReadByte();
				if (command != Command.ClientAuthentication)
					return new HostConnectResult(ProtocolErrors.AuthResponseCommand);

				// Auth 1) Receive authentication challenge.  This is an array of 32 random bytes which the Host Service must sign with its private key.
				byte[] authChallenge = ByteUtil.ReadNBytes(tcpStream, 32);

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
					tcpStream.WriteByte((byte)Command.ClientAuthentication);
					ByteUtil.WriteUInt16((ushort)calculatedLength, tcpStream);
					mds.CopyTo(tcpStream);
				}
				#endregion
				// This is the Host Service, which is responsible for sending a KeepAlive packet after 60 seconds of sending inactivity.  The Master Server will do the same on a 120 second interval.
				tcpClient.ReceiveTimeout = 135000; // 120 seconds + 15 seconds for bad network conditions.
				tcpClient.SendTimeout = 75000;

				// TODO: Listen for commands in a loop.
				// TODO: Send KeepAlive packets every 60 seconds if no other packets have been sent.  Use the SetTimeout library in BPUtil.
			}
			finally
			{
				Try.Catch_RethrowThreadAbort(() => tcpClient.Close());
			}
			return new HostConnectResult();
		}
	}
}
