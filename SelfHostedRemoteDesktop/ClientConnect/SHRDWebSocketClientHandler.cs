using BPUtil;
using BPUtil.SimpleHttp.WebSockets;
using SHRDLib.NetCommand;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.ClientConnect
{
	/// <summary>
	/// Handles an incoming WebSocket connection from a client.
	/// </summary>
	public class SHRDWebSocketClientHandler
	{
		private const string myPath = "/SHRD"; // TODO: After Host Service gets its own web server, move this path to where it actually belongs.
		private WebSocket socket;

		private static long idCounter = 0;
		private long ID = Interlocked.Increment(ref idCounter);

		private ImgFlags imgFlags = ImgFlags.Color420;
		private byte jpegQuality = 25;
		private byte maxFPS = 15;
		private byte maxUnacknowledgedFrames = 3;
		private Thread streamingThread = null;
		private long activeStreamNumber = 0;
		private StreamThreadArgs streamThreadArgs = new StreamThreadArgs(-1);
		private StreamerController streamerController;
		private bool calledClose = false;
		private object closeLock = new object();
		/// <summary>
		/// An event raised when the connection is closed or when the connection fails to establish.
		/// </summary>
		public event EventHandler OnClose = delegate { };

		public SHRDWebSocketClientHandler()
		{
			Console.WriteLine(ID + " SHRDWebSocketClientHandler constructor");
			streamerController = new StreamerController(ServiceWrapper.service_pid);
			streamerController.OnClose += StreamerController_OnClose;
		}
		/// <summary>
		/// Instructs the socket to close as soon as possible, or to prevent opening in the first place.
		/// </summary>
		public void CloseSocket()
		{
			if (calledClose)
				return;
			lock (closeLock)
			{
				if (calledClose)
					return;
				calledClose = true;
			}
			if (socket != null)
				socket.Close();
		}
		private void StreamerController_OnClose(object sender, EventArgs e)
		{
			streamThreadArgs.abortFlag.abort = true;
			CloseSocket();
		}
		private void onWebSocketClose(WebSocketCloseFrame frame)
		{
			OnClose(this, new EventArgs());
			StopStreaming();
			Try.Catch(() => { streamerController?.Dispose(); streamerController = null; });
		}

		#region Message Handling
		private void onWebSocketMessageReceived(WebSocketFrame frame)
		{
			if (frame.GetType() != typeof(WebSocketBinaryFrame))
			{
				CloseSocket();
				return;
			}
			WebSocketBinaryFrame bf = frame as WebSocketBinaryFrame;
			byte[] buf = bf.Data;

			Console.WriteLine(ID + " OnMessage(" + buf.Length + ")");
			if (buf.Length == 0)
			{
				CloseSocket();
				return;
			}

			Command cmd = (Command)buf[0];
			Console.WriteLine(ID + " OnMessage: " + cmd);
			try
			{
				switch (cmd)
				{
					case Command.StartStreaming:
						if (buf.Length < 3)
						{
							SyntaxError(buf[0], buf);
							break;
						}
						StopStreaming();
						Console.WriteLine(ID + " Command.StartStreaming " + (byte)Interlocked.Read(ref activeStreamNumber));
						socket.Send(new byte[] { buf[0], (byte)Interlocked.Read(ref activeStreamNumber) });
						StartStreaming(buf);
						break;
					case Command.StopStreaming:
						Console.WriteLine(ID + " Command.StopStreaming");
						StopStreaming();
						break;
					case Command.AcknowledgeFrame:
						if (buf.Length < 2)
						{
							SyntaxError(buf[0], buf);
							break;
						}
						AcknowledgeFrame(buf[1]);
						break;
					case Command.ReproduceUserInput:
						if (buf.Length < 2)
						{
							SyntaxError(buf[0], buf);
							break;
						}
						InputType inputType = (InputType)buf[1];
						if (inputType == InputType.KeyDown || inputType == InputType.KeyUp)
						{
							if (buf.Length < 10)
							{
								SyntaxError(buf[0], buf, inputType);
								break;
							}
							int keyCode = ByteUtil.ReadInt32(buf, 2);
							ModifierKeys modifiers = (ModifierKeys)ByteUtil.ReadUInt32(buf, 6);
							streamerController.DoKeyboardInput(keyCode, modifiers, inputType == InputType.KeyUp);
						}
						else if (inputType == InputType.MouseMove)
						{
							if (buf.Length < 10)
							{
								SyntaxError(buf[0], buf, inputType);
								break;
							}
							float x = ByteUtil.ReadFloat(buf, 2);
							float y = ByteUtil.ReadFloat(buf, 6);
							streamerController.DoMouseMove(x, y);
						}
						else if (inputType == InputType.MouseButtonDown || inputType == InputType.MouseButtonUp)
						{
							if (buf.Length < 3)
							{
								SyntaxError(buf[0], buf, inputType);
								break;
							}
							MouseButton button = (MouseButton)buf[2];
							streamerController.DoMouseButton(button, inputType == InputType.MouseButtonUp);
						}
						else if (inputType == InputType.MouseWheel)
						{
							if (buf.Length < 6)
							{
								SyntaxError(buf[0], buf, inputType);
								break;
							}
							short deltaX = ByteUtil.ReadInt16(buf, 2);
							short deltaY = ByteUtil.ReadInt16(buf, 4);
							streamerController.DoMouseWheel(deltaX, deltaY);
						}
						break;
					case Command.GetDesktopInfo:
						DesktopInfo desktopInfo = streamerController.GetDesktopInfo();
						using (MemoryDataStream mds = new MemoryDataStream())
						{
							desktopInfo.WriteToDataStream(mds);
							socket.Send(mds.ToArray());
						}
						break;
					case Command.SetStreamSettings:
						if (buf.Length < 5)
						{
							SyntaxError(buf[0], buf);
							break;
						}

						imgFlags = (ImgFlags)buf[1];
						jpegQuality = BPMath.Clamp<byte>(buf[2], 1, 100);
						maxFPS = BPMath.Clamp<byte>(buf[3], 1, byte.MaxValue);
						maxUnacknowledgedFrames = BPMath.Clamp<byte>(buf[4], 1, byte.MaxValue);
						break;
					case Command.GetStreamSettings:
						byte[] response = new byte[4];
						response[0] = buf[0];
						response[1] = (byte)imgFlags;
						response[2] = jpegQuality;
						response[3] = maxFPS;
						socket.Send(response);
						break;
					case Command.KeepAlive:
						break;
					default:
						// CloseSocket();
						socket.Send(new byte[] { (byte)Command.Error_CommandCodeUnknown });
						break;
				}
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex, "WebSocketServer");
			}
		}

		private void SyntaxError(byte cmd, byte[] buf, string extra = "")
		{
			string bufText = buf.Length < 20 ? (" [" + string.Join(", ", buf) + "]") : " [buffer length: " + buf.Length + "]";
			Logger.Debug("Malformed Command." + (Command)cmd + extra + bufText);
			socket.Send(new byte[] { (byte)Command.Error_SyntaxError });
		}
		private void SyntaxError(byte cmd, byte[] buf, InputType inputType)
		{
			SyntaxError(cmd, buf, "/" + inputType.ToString());
		}

		private void AcknowledgeFrame(byte streamId)
		{
			if ((byte)Interlocked.Read(ref activeStreamNumber) == streamId)
				Interlocked.Increment(ref streamThreadArgs.numAcknowledgedFrames);
		}
		#endregion

		#region Video Streaming
		private void StopStreaming(int waitMs = 10000)
		{
			streamThreadArgs.abortFlag.abort = true;
			if (streamingThread != null)
			{
				Interlocked.Increment(ref activeStreamNumber);
				if (waitMs < 1 || !streamingThread.Join(BPMath.Clamp(waitMs, 1000, 60000)))
					streamingThread.Abort();
				streamingThread = null;
			}
		}
		private void StartStreaming(byte[] buf)
		{
			streamThreadArgs = new StreamThreadArgs(Interlocked.Read(ref activeStreamNumber));
			streamThreadArgs.streamType = (StreamType)buf[1]; // TODO: Use streamType
			streamThreadArgs.displayIdx = buf[2]; // TODO: Use displayIdx

			streamingThread = new Thread(streamLoop);
			streamingThread.Name = "WebSocket Streaming";
			streamingThread.IsBackground = true;
			streamingThread.Start(streamThreadArgs);
		}

		private void streamLoop(object objArgs)
		{
			try
			{
				// streamLoop itself isn't in the stack trace from what I've seen! So I've nested the logic into another method to help with debugging.
				streamLoop_inner(objArgs);
			}
			catch (ThreadAbortException)
			{
				Logger.Debug("WebSocket Streaming thread had to be aborted. Closing the web socket because it could be in a bad state.");
				CloseSocket();
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		private void streamLoop_inner(object objArgs)
		{
			Stopwatch frameTimer = new Stopwatch();
			frameTimer.Start();
			long nextFrameStart = 0;
			StreamThreadArgs args = (StreamThreadArgs)objArgs;
			while (!args.abortFlag.abort)
			{
				try
				{
					int sleepTime = (int)(nextFrameStart - frameTimer.ElapsedMilliseconds);
					while (sleepTime > 0 || (Interlocked.Read(ref args.numSentFrames) >= Interlocked.Read(ref args.numAcknowledgedFrames) + maxUnacknowledgedFrames))
					{
						if (args.abortFlag.abort)
							return;
						Thread.Sleep(BPMath.Clamp(sleepTime, 1, 10));
						sleepTime = (int)(nextFrameStart - frameTimer.ElapsedMilliseconds);
					}
					if (args.abortFlag.abort)
						return;

					nextFrameStart = frameTimer.ElapsedMilliseconds + (1000 / maxFPS);

					if (streamerController == null)
						return;
					FragmentedImage fragmentedImage = streamerController.GetRawDesktopCapture(imgFlags, jpegQuality, args.abortFlag);
					if (args.abortFlag.abort)
						return;
					if (fragmentedImage == null)
						fragmentedImage = new FragmentedImage();
					fragmentedImage.streamId = (byte)args.myStreamNumber;
					using (MemoryDataStream mds = new MemoryDataStream(fragmentedImage.GetMaximumRequiredBufferSize()))
					{
						byte[] compressionBuffer = null;
						fragmentedImage.WriteToDataStream(mds, ref compressionBuffer);
						Interlocked.Increment(ref args.numSentFrames);
						socket.Send(mds.ToArray());
					}
				}
				catch (ThreadAbortException) { throw; }
				catch (Exception ex)
				{
					Logger.Debug(ex);
				}
			}
		}
		#endregion

		#region Establish Proxy Connection
		/// <summary>
		/// Opens a connection to the Master Server using the specified connectionKey and then creates a WebSocket (server) instance from it.
		/// </summary>
		/// <param name="connectionKey">A unique key allowing this host to connect to its end of a proxy connection provided by the master server.</param>
		public void BeginProxiedConnection(string connectionKey)
		{
			lock (closeLock)
			{
				if (calledClose)
				{
					OnClose(this, new EventArgs());
					return;
				}
				TcpClient tcpc = new TcpClient();
				tcpc.BeginConnect("localhost", 8088, onTcpcConnect, new { tcpc, connectionKey });
			}
		}
		private void onTcpcConnect(IAsyncResult ar)
		{
			lock (closeLock)
			{
				if (ar.IsCompleted)
				{
					dynamic args = (dynamic)ar.AsyncState;
					TcpClient tcpc = (TcpClient)args.tcpc;
					tcpc.EndConnect(ar);

					if (calledClose)
					{
						Try.Swallow(() => { tcpc.Close(); });
						OnClose(this, new EventArgs());
						return;
					}

					tcpc.NoDelay = true;
					string connectionKey = (string)args.connectionKey;

					// Send to the Master Server an HTTP request that will be transformed into a web socket proxy.
					// After the connection key, append the port number, then append this service's path (e.g. connectionKey + "/80/SHRD")
					// This could have been hard-coded in the Master Server, but it seems more flexible if we send it here.
					// For example, at some point in the future, a host service might want to customize or randomize its web socket listening endpoint.
					// NOTE: At this time, the host service does not actually have a listening endpoint. Only these outgoing proxied web socket server connections are supported.
					string proxyPath = "/WebSocketHostProxy/" + connectionKey + "/" + 80 + myPath;
					Console.WriteLine("GET " + proxyPath + " HTTP/1.1");
					byte[] buf = Encoding.UTF8.GetBytes("GET " + proxyPath + " HTTP/1.1\r\n\r\n");
					tcpc.GetStream().Write(buf, 0, buf.Length);
					Console.WriteLine("WebSocket proxy connection established.  Handing-off to WebSocket server.");
					socket = new WebSocket(tcpc);
					socket.CompleteWebSocketHandshake(myPath);
					socket.StartReading(onWebSocketMessageReceived, onWebSocketClose);
				}
				else
				{
					Logger.Debug("SHRDWebSocketClientHandler.onTcpcConnect with IsCompleted == false");
					OnClose(this, new EventArgs());
				}
			}
		}
		#endregion
	}
}
