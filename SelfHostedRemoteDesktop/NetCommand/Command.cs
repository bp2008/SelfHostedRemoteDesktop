using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.NetCommand
{
	/// <summary>
	/// All the possible command codes.
	/// These command codes are shared for all communication in Self Hosted Remote Desktop.
	/// Self Hosted Remote Desktop has 4 parts:
	///
	/// 1) The Master Server.  This server acts performs computer and user management, and is a gateway for tunneling of connections.  It is recommended to be accessed via a domain name with a valid certificate from a trusted certificate authority.
	/// 2) The Host Service.  The host service is the reliable, always-on service running on each computer that is to be remotely accessed.  The Host Service maintains an active connection to the Master Server using the /hostconnect endpoint. 
	/// 3) The Streamer.  This is a secondary process started by the Host Service, whose purpose is to capture the desktop video and relay user-input to the desktop.
	/// 4) The Web Client.  This is the web application which connects to various Host Services, typically by tunneling through the Master Server.  It renders the remote desktop.
	/// 
	/// All interprocess communication must begin with one of these command codes, so the receiving end knows what to expect.
	///
	/// "Success" responses typically begin with an echo of the command code being handled.  Therefore, how to handle a particular command code depends on the application that is receiving it and where it is being received from. For instance, the service can receive the command code "GetDesktopInfo" from a client (indicating a request for desktop info) or from the Streamer process (indicating a payload of desktop info).
	/// 
	/// Communication between the service process and the streamer process is asynchronous.  Either process may send a message to the other process at any time.  Synchronous communication, while conceptually simpler, is not sufficient for this application because not all requests can be completed in a timely manner. The same applies to communcation between the service and the client via web socket.
	/// </summary>
	public enum Command : byte
	{
		/// <summary>
		/// Starts or restarts streaming, beginning with an "iframe". The following data specifies some streaming options such as the stream format and which display to capture.  The service will respond with a single byte ID of the stream, to be used in future GetScreenCapture and AcknowledgeFrame commands.  This command is only used between the web client and the host service.
		/// </summary>
		StartStreaming = 0,
		/// <summary>
		/// Stops streaming.  This command is only used between the web client and the host service.
		/// </summary>
		StopStreaming = 1,
		/// <summary>
		/// The web client sends this command to the host service to acknowledge receipt of a frame.  There is a one-byte argument to identify the stream.
		/// </summary>
		AcknowledgeFrame = 2,
		/// <summary>
		/// The following data describes a keyboard or mouse event. No response is expected from this command.
		/// </summary>
		ReproduceUserInput = 3,
		/// <summary>
		/// Requests information about the current desktop indexes, sizes, and coordinates.
		/// </summary>
		GetDesktopInfo = 4,
		/// <summary>
		/// The following data specifies the streaming settings that should be set. These settings can be changed without restarting the stream.  No response is expected.
		/// </summary>
		SetStreamSettings = 5,
		/// <summary>
		/// Requests the streaming settings that are currently set.
		/// </summary>
		GetStreamSettings = 6,
		/// <summary>
		/// Requests a screen capture.  The Host Service does not accept this command.  The Streamer does.
		/// </summary>
		GetScreenCapture = 10,
		/// <summary>
		/// The Master Server uses this to request that a Host Client authenticate itself by signing a random array of bytes.
		/// </summary>
		ClientAuthentication = 200,
		/// <summary>
		/// This standalone command has no meaning and is used only to keep a connection alive.  All parts of Self Hosted Remote Desktop must accept this command without error.
		/// </summary>
		KeepAlive = 240,
		/// <summary>
		/// If any part of a command is unrecognized.
		/// </summary>
		Error_SyntaxError = 253,
		/// <summary>
		/// If a received command code is unknown to the streamer process.
		/// </summary>
		Error_CommandCodeUnknown = 254,
		/// <summary>
		/// If an unknown error occurs.
		/// </summary>
		Error_Unspecified = 255
	}
}
