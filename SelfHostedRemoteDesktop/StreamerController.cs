using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using SHRDLib;
using SHRDLib.NetCommand;
using SHRDLib.Utilities;

namespace SelfHostedRemoteDesktop
{
	/// <summary>
	/// Manages the SelfHostedRDStreamer process, responsible for screen capturing and relaying remote keyboard/mouse input to the local console.
	/// </summary>
	public class StreamerController : CriticalFinalizerObject, IDisposable
	{
		private int this_process_pid = -1;
		private Process streamer_process = null;
		private static int numberOfTimesStreamerHasBeenStarted = -1;
		private int lastConsoleSessionId = -2;
		private bool isDisposed = false;

		/// <summary>
		/// A SharedMemoryStream which is shared with the streamer process.  Obtain a lock on syncObject before writing to this stream, and write your entire payload before releasing the lock.
		/// </summary>
		private SharedMemoryStream sm;

		/// <summary>
		/// An object which is locked during operations which require synchronization.
		/// </summary>
		private object syncObject = new object();
		/// <summary>
		/// A thread which handles all reading from the shared memory stream.
		/// </summary>
		private Thread readSharedMemoryThread = null;
		private AsyncLoadingObject<FragmentedImage> newFrame = new AsyncLoadingObject<FragmentedImage>();
		private AsyncLoadingObject<DesktopInfo> newDesktopInfo = new AsyncLoadingObject<DesktopInfo>();
		/// <summary>
		/// Event raised one time during the StreamerController's life, when the StreamerController is no longer usable.  This can happen for a number of reasons, such as:
		/// 1) The SharedMemoryStream was closed.
		/// 2) The Streamer process was closed.
		/// 3) The Streamer was attached to the Console session, but the Console session ID changed.
		/// 4) Dispose() was called on this StreamerController
		/// 5) etc...
		/// -----
		/// At this point, whatever is using the StreamerController should call dispose() on the StreamerController and cease using it, though it may create another StreamerController.
		/// </summary>
		public event EventHandler OnClose = delegate { };
		private object closeLock = new object();
		private bool closed = false;
		private void RaiseOnCloseEvent()
		{
			lock (closeLock)
			{
				if (closed)
					return;
				closed = true;
			}
			Try.Catch(() => { OnClose(this, new EventArgs()); });
		}

		public StreamerController(int this_process_pid)
		{
			this.this_process_pid = this_process_pid;
		}

		~StreamerController()
		{
			if (isDisposed)
				return;
			Dispose(false);
		}

		public void Dispose()
		{
			if (isDisposed)
				return;
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			RaiseOnCloseEvent();
			Kill();
			isDisposed = true;
		}
		#region Public interface
		/// <summary>
		/// Lock for the GetRawDesktopCapture method to ensure that only one frame can be processing at a time.
		/// </summary>
		private object lock_GetRawDesktopCapture = new object();
		/// <summary>
		/// Returns a buffer containing serialized ImageFragments.
		/// May return null in case of error.
		/// This method blocks until a frame is received from the Streamer process.
		/// </summary>
		/// <returns></returns>
		public FragmentedImage GetRawDesktopCapture(ImgFlags imgFlags, byte jpegQuality, AbortFlag abortFlag)
		{
			try
			{
				lock (lock_GetRawDesktopCapture)
				{
					lock (syncObject)
					{
						if (!Maintain())
							return new FragmentedImage();
						sm.WriteByte((byte)Command.GetScreenCapture);
						sm.WriteByte((byte)imgFlags);
						sm.WriteByte(jpegQuality);
					}
					FragmentedImage img;
					if (newFrame.Consume(abortFlag, out img))
						return img;
					else
						return null;
				}
			}
			catch (ThreadAbortException) { throw; }
			catch (StreamDisconnectedException ex)
			{
				Logger.Info(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			return new FragmentedImage();
		}
		//public ImageFragment[] GetRawDesktopCapture2()
		//{
		//	return null;
		//lock (syncObject)
		//{
		//	if (!Maintain())
		//		return null;
		//	sm.WriteByte((byte)Command.CaptureRawDesktopImage);
		//	Command responseCode = (Command)sm.ReadByte();
		//	if (responseCode != Command.CaptureRawDesktopImage)
		//		throw new Exception("Invalid response code from streamer.");
		//	int maxBufferSize = sm.ReadInt32();
		//	byte[] buf = new byte[];
		//	ushort fragCount = sm.ReadUInt16();
		//	for (int i = 0; i < fragCount; i++)
		//	{
		//	}
		//	int width = sm.ReadInt32();
		//	int height = sm.ReadInt32();
		//	int bitsPerPixel = sm.ReadInt32();
		//	int imgLength = sm.ReadInt32();
		//	if (imgLength <= 0)
		//		return null;
		//	else
		//	{
		//		Screenshot screenshot = new Screenshot(width, height, bitsPerPixel);
		//		if (imgLength != screenshot.Buffer.Length)
		//			throw new Exception("Unexpected image length from streamer");
		//		sm.Read(screenshot.Buffer, 0, screenshot.Buffer.Length);
		//		return null;
		//	}
		//}
		//}
		//public byte[] GetCompressedDesktopCapture(byte method)
		//{
		//	lock (syncObject)
		//	{
		//		if (!Maintain())
		//			return null;
		//		sm.WriteByte((byte)Command.CaptureCompressedDesktopImage);
		//		sm.WriteByte(method);
		//		Command responseCode = (Command)sm.ReadByte();
		//		if (responseCode != Command.CaptureCompressedDesktopImage)
		//			throw new Exception("Invalid response code from streamer.");
		//		int imgLength = sm.ReadInt32();
		//		if (imgLength <= 0)
		//			return null;
		//		else
		//			return Util.ReadNBytes(sm, imgLength);
		//	}
		//}

		public void DoKeyboardInput(int keyCode, ModifierKeys modifiers, bool isUpCommand)
		{
			try
			{
				lock (syncObject)
				{
					if (!Maintain())
						return;
					sm.WriteByte((byte)Command.ReproduceUserInput);
					sm.WriteByte(isUpCommand ? (byte)InputType.KeyUp : (byte)InputType.KeyDown);
					sm.WriteInt32(keyCode);
					sm.WriteUInt32((uint)modifiers);
					//Console.WriteLine("Completed key " + keyCode + " " + (isUpCommand ? "up" : "down"));
				}
			}
			catch (StreamDisconnectedException ex)
			{
				Logger.Debug(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		public void DoMouseMove(float x, float y)
		{
			try
			{
				lock (syncObject)
				{
					if (!Maintain())
						return;
					sm.WriteByte((byte)Command.ReproduceUserInput);
					sm.WriteByte((byte)InputType.MouseMove);
					sm.WriteFloat(x);
					sm.WriteFloat(y);
					//Logger.Info("Mouse " + x + ", " + y);
				}
			}
			catch (StreamDisconnectedException ex)
			{
				Logger.Debug(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		public void DoMouseButton(MouseButton button, bool isUpCommand)
		{
			try
			{
				lock (syncObject)
				{
					if (!Maintain())
						return;
					sm.WriteByte((byte)Command.ReproduceUserInput);
					sm.WriteByte(isUpCommand ? (byte)InputType.MouseButtonUp : (byte)InputType.MouseButtonDown);
					sm.WriteByte((byte)button);
				}
			}
			catch (StreamDisconnectedException ex)
			{
				Logger.Debug(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		public void DoMouseWheel(short deltaX, short deltaY)
		{
			try
			{
				lock (syncObject)
				{
					if (!Maintain())
						return;
					sm.WriteByte((byte)Command.ReproduceUserInput);
					sm.WriteByte((byte)InputType.MouseWheel);
					sm.WriteInt16(deltaX);
					sm.WriteInt16(deltaY);
					Logger.Info("Mouse wheel " + deltaX + ", " + deltaY);
				}
			}
			catch (StreamDisconnectedException ex)
			{
				Logger.Debug(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}

		/// <summary>
		/// Lock for the GetDesktopInfo method to ensure that only one DesktopInfo request can be processing at a time.
		/// </summary>
		private object lock_GetDesktopInfo = new object();
		public DesktopInfo GetDesktopInfo()
		{
			try
			{
				lock (lock_GetDesktopInfo)
				{
					lock (syncObject)
					{
						if (!Maintain())
							return new DesktopInfo();
						sm.WriteByte((byte)Command.GetDesktopInfo);
					}
					DesktopInfo info;
					if (newDesktopInfo.Consume(new AbortFlag(), out info))
						return info;
					else
						return null;
				}
			}
			catch (StreamDisconnectedException ex)
			{
				Logger.Debug(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			return new DesktopInfo();
		}

		/// <summary>
		/// A method intended only for use in early development. This should be deleted eventually.
		/// </summary>
		/// <returns></returns>
		public bool IsRunning()
		{
			lock (syncObject)
			{
				if (streamer_process != null)
				{
					int consoleSessionId = ProcessHelper.GetConsoleSessionId();
					if (streamer_process.SessionId == consoleSessionId)
					{
						streamer_process.Refresh();
						if (!streamer_process.HasExited)
							return true;
					}
				}
				return false;
			}
		}
		#endregion
		#region Private helpers
		/// <summary>
		/// Checks the status of the streamer process and starts it if necessary. Returns true if the streamer process is active.  Returns false if the streamer process is not active, indicating that other requests will fail.
		/// </summary>
		private bool Maintain()
		{
			try
			{
				// TODO: ProcessHelper.GetConsoleSessionId() does not always return the session ID of the current visible.
				// Example: Hyper-V virtual machines.  Try connecting to SHRD while there is no active Virtual Machine 
				// Connection window, then open a Virtual Machine Connection window and it will run under a different 
				// session ID than SHRD.  The only apparent fix is to log out the VM session and then the VM connection 
				// begins using the same session as SHRD.
				// We perhaps need a way to manually connect to a specific session.  I envision a GUI where you can see a 
				// list of sessions and the processes running under them, to help you choose which session to connect to.
				int consoleSessionId = ProcessHelper.GetConsoleSessionId();
				if (consoleSessionId != lastConsoleSessionId)
				{
					if (lastConsoleSessionId != -2)
					{
						Logger.Info("Detected change in console session ID from " + lastConsoleSessionId + " to " + consoleSessionId + ". This StreamerController will now close.");
						Kill();
						return false;
					}
					else
						lastConsoleSessionId = consoleSessionId;
				}
				if (consoleSessionId == -1)
				{
					Logger.Info("consoleSessionId -1 (no session attached to physical console)");
					Kill();
					return false;
				}
				if (streamer_process != null)
				{
					streamer_process.Refresh();
					if (streamer_process.HasExited)
					{
						ClearProcessReference();
						RaiseOnCloseEvent();
						return false;
					}
					else if (streamer_process.SessionId == consoleSessionId)
						return true;
					else
					{
						Logger.Info("streamer_process.SessionId (" + streamer_process.SessionId + ") != consoleSessionId (" + consoleSessionId + ")");
						Kill(); // Session Id is no longer current
						return false;
					}
				}

				// If we reach this point, we need to start the streamer process.
				string sharedMemUniqueId = "Stream" + Interlocked.Increment(ref numberOfTimesStreamerHasBeenStarted);
				sm = SharedMemoryStream.CreateSharedMemoryStream(sharedMemUniqueId);
				StartSharedMemoryReadingThread(sharedMemUniqueId);

				string path = Globals.ApplicationDirectoryBase;
				StreamerArgs args = new StreamerArgs(this_process_pid, sharedMemUniqueId);
				if (Environment.UserInteractive)
				{
					ProcessStartInfo psi = new ProcessStartInfo(path + Globals.ExecutableNameWithExtension, args.ToString());
					psi.UseShellExecute = false;
					psi.WorkingDirectory = path;
					streamer_process = Process.Start(psi);
					sm.otherProcessPid = streamer_process.Id;
				}
				else
				{
					// TODO: Add a method for enumerating and choosing the Windows Session ID to open the process with.
					int streamer_pid = sm.otherProcessPid = ProcessHelper.ExecuteInteractive(path + Globals.ExecutableNameWithExtension, args.ToString(), path);
					streamer_process = ProcessHelper.GetProcByID(streamer_pid);
				}
				Logger.Info("Started streamer process (PID: " + streamer_process.Id + ", Shared Memory ID: " + sharedMemUniqueId + ")");
				return true;
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				return false;
			}
		}

		/// <summary>
		/// Attempts to kill the streamer process and returns true if the streamer process is closed (even if it wasn't running to start with.  If this method returns false, assume the streamer process is unkillable.
		/// </summary>
		/// <returns></returns>
		private bool Kill()
		{
			Logger.Info("Killing Streamer Process" + (streamer_process == null ? "" : " (PID: " + streamer_process.Id + ")"));
			RaiseOnCloseEvent();

			sm = null;

			if (streamer_process == null)
				return true;

			streamer_process.Refresh();
			if (streamer_process.HasExited)
			{
				ClearProcessReference();
				return true;
			}
			try
			{
				streamer_process.CloseMainWindow();
				if (!streamer_process.WaitForExit(1000)) // Wait up to one second for exit. It SHOULD be much, much faster than this.
					streamer_process.Kill();
				return true;
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Unable to kill " + Globals.ExecutableNameWithExtension + " process.");
				return false;
			}
		}
		private void ClearProcessReference()
		{
			Logger.Info("Found exited streamer_process " + streamer_process.Id + ". Cleaning up.");
			Try.Catch(() => { streamer_process?.Close(); });
			streamer_process = null;
		}
		/// <summary>
		/// End any existing streamer processes.  This should only happen once, when the Service process starts.
		/// </summary>
		private void EndOrphanedStreamerProcesses()
		{
			// TODO: End orphaned streamer processes when the Service process starts.
			foreach (Process p in Process.GetProcessesByName(Globals.ExecutableNameWithoutExtension))
			{
				try
				{
					if (GetCommandLine(p.Id).StartsWith("streamer "))
						p.Kill();
				}
				catch (Exception ex)
				{
					Logger.Debug(ex, "Found orphaned " + Globals.ExecutableNameWithExtension + " process, unable to kill.");
				}
			}
		}
		private static string GetCommandLine(int processId)
		{
			var commandLine = new StringBuilder();
			using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + processId))
			{
				foreach (var @object in searcher.Get())
				{
					commandLine.Append(@object["CommandLine"]);
					commandLine.Append(" ");
				}
			}

			return commandLine.ToString();
		}
		#endregion
		#region Reading the Shared Memory Stream
		private void StartSharedMemoryReadingThread(string sharedMemUniqueId)
		{
			Try.Swallow(() => { readSharedMemoryThread?.Abort(); });
			readSharedMemoryThread = new Thread(readSharedMemory);
			readSharedMemoryThread.IsBackground = true;
			readSharedMemoryThread.Name = "Read Shared Memory " + sharedMemUniqueId;
			readSharedMemoryThread.Start(sm);
		}
		private void readSharedMemory(object args)
		{
			SharedMemoryStream mySm = (SharedMemoryStream)args;
			try
			{
				while (sm == mySm)
				{
					try
					{
						byte cmdByte = (byte)sm.ReadByte();
						Command cmd = (Command)cmdByte;
						// Handle message from Streamer process.
						// TODO: Refactor the responses to use a generic wrapper class, e.g. 
						// AsyncLoadingObject<FragmentedImage> newFrame = ...;
						// newFrame.Loaded(img);
						// if (newFrame.Wait(abortFlag, out ...))
						//	return ...;
						switch (cmd)
						{
							case Command.GetScreenCapture:
								FragmentedImage img = new FragmentedImage(sm);
								newFrame.Produce(img);
								break;
							case Command.GetDesktopInfo:
								DesktopInfo di = new DesktopInfo(sm);
								newDesktopInfo.Produce(di);
								break;
							case Command.Error_SyntaxError:
							case Command.Error_CommandCodeUnknown:
							case Command.Error_Unspecified:
							case Command.StartStreaming:
							case Command.StopStreaming:
							case Command.AcknowledgeFrame:
							case Command.ReproduceUserInput:
							case Command.SetStreamSettings:
							case Command.GetStreamSettings:
							default:
								Logger.Info("Received unexpected byte from Streamer process: " + cmd);
								// TODO: End the Streamer process now and start a new one, because we have just entered an undefined state.
								break;
						}
					}
					catch (ThreadAbortException) { throw; }
					catch (StreamDisconnectedException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
					}
				}

			}
			catch (ThreadAbortException) { }
			catch (StreamDisconnectedException)
			{
				Logger.Info("Read Shared Memory thread detected stream close (" + mySm.uniqueId + ")");
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			finally
			{
				Try.Catch(() => { mySm?.Dispose(); });
				RaiseOnCloseEvent();
			}
		}
		#endregion
	}
}
