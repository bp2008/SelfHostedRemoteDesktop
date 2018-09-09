using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BPUtil;
using SelfHostedRemoteDesktop;
using SHRDLib.NetCommand;
using turbojpegCLI;

namespace SelfHostedRemoteDesktop.Streamer
{
	public static class StreamerStaticMain
	{
		private static volatile bool isExiting = false;
		private static StreamerArgs streamerArgs;
		private static SharedMemoryStream static_sm = null;
		private static AdvScreenCapture screenCapturer;
		private static DxgiOutputDuplicator dxgiDuplicator;
		private static InputEmulator inputEmulator;
		private static Thread thrMain;
		/// <summary>
		/// Desktop capturing with DxgiOutputDuplicator can regularly take up to several seconds (if the screen is static), so it must be done on its own thread.
		/// </summary>
		private static Thread thrDesktopCapture;
		private static ConcurrentQueue<DesktopCaptureTask> desktopCaptureTasks = new ConcurrentQueue<DesktopCaptureTask>();
		private static Thread thrMonitorService;
		private static Process service_process;
		private static DesktopInfo desktopInfo;
		private static Stopwatch compatibleDesktopCaptureModeClock = new Stopwatch();
		private static int timeToUseCompatibleDesktopCaptureMode = 5000;

		/// <summary>
		/// This is the main entry point for the SHRD streamer.
		/// </summary>
		/// <param name="args"></param>
		public static void Run(string[] args)
		{
			string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			Globals.InitializeProgram(exePath, "Self Hosted Remote Desktop", true);
			PrivateAccessor.SetStaticFieldValue(typeof(Globals), "errorFilePath", Globals.WritableDirectoryBase + "SHRD_Streamer_Log.txt");

			Logger.logType = LoggingMode.File;
			Logger.Info("SHRDStreamer Startup");

			Application.ThreadException += Application_ThreadException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			Application.ApplicationExit += Application_ApplicationExit;
			Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

			desktopInfo = DxgiOutputDuplicator.GetDesktopInfo();

			streamerArgs = new StreamerArgs(args);

			thrMain = new Thread(mainThreadRunner);
			thrMain.Name = "Streamer Main";
			thrMain.IsBackground = true;
			thrMain.Start();

			thrDesktopCapture = new Thread(desktopCaptureThreadRunner);
			thrDesktopCapture.SetApartmentState(ApartmentState.MTA);
			thrDesktopCapture.Name = "Desktop Capture";
			thrDesktopCapture.IsBackground = true;
			// thrDesktopCapture is started by thrMain after static_sm is set.

			if (streamerArgs.ServiceProcessId != null)
			{
				service_process = Process.GetProcessById(streamerArgs.ServiceProcessId.Value);
				thrMonitorService = new Thread(monitoringThreadRunner);
				thrMonitorService.Name = "Monitoring Service";
				thrMonitorService.IsBackground = true;
				thrMonitorService.Start();
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1());
			//Application.Run();
		}

		private static void Application_ApplicationExit(object sender, EventArgs e)
		{
			Logger.Info("Application_ApplicationExit event");
			isExiting = true;
			Try.Catch(() => { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged; });
			// It really is highly unlikely for thrMain to exit on its own, as it blocks trying to read from shared memory
			Try.Catch(() => { thrMain?.Join(25); });
			Try.Catch(() => { thrMain?.Abort(); });
			Try.Catch(() => { thrMonitorService?.Abort(); });
			Try.Catch(() => { thrDesktopCapture?.Abort(); });
			Try.Catch(() => { screenCapturer?.Dispose(); });
			Try.Catch(() => { dxgiDuplicator?.Dispose(); });
			//Try.Catch(() => { inputEmulator?.Dispose(); });
		}
		private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
		{
			try
			{
				screenCapturer?.DisplaySettingsChanged();
				Try.Catch(() => { dxgiDuplicator?.Dispose(); });
				dxgiDuplicator = new DxgiOutputDuplicator(0, 0);
				desktopInfo = DxgiOutputDuplicator.GetDesktopInfo();
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		/// <summary>
		/// Monitors the parent process (service_process) so this process can close itself if its parent closes.
		/// </summary>
		private static void monitoringThreadRunner()
		{
			if (service_process == null)
				return;
			try
			{
				while (!isExiting)
				{
					service_process.Refresh();
					if (service_process.HasExited || service_process.WaitForExit(1000))
					{
						Logger.Info("Detected service exit");
						break;
					}
				}
				if (!isExiting)
					RobustExit();
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		private static void mainThreadRunner()
		{
			try
			{
				screenCapturer = new AdvScreenCapture();
				dxgiDuplicator = new DxgiOutputDuplicator(0, 0);
				inputEmulator = new InputEmulator();
				Logger.Info("Application start shared memory id " + streamerArgs.SharedMemoryId);
				int ownerPID = streamerArgs.ServiceProcessId == null ? 0 : streamerArgs.ServiceProcessId.Value;
				using (SharedMemoryStream sm = SharedMemoryStream.OpenSharedMemoryStream(streamerArgs.SharedMemoryId, ownerPID))
				{
					try
					{
						static_sm = sm;
						thrDesktopCapture.Start();
						while (!isExiting)
						{
							Command commandCode = (Command)sm.ReadByte();
							// Handle
							switch (commandCode)
							{
								case Command.GetScreenCapture:
									ImgFlags imgFlags = (ImgFlags)sm.ReadByte();
									byte jpegQuality = (byte)sm.ReadByte();
									desktopCaptureTasks.Enqueue(new DesktopCaptureTask(imgFlags, jpegQuality));
									break;
								//case Command.CaptureCompressedDesktopImage:
								//	CaptureCompressedDesktopImage(sm);
								//	break;
								case Command.ReproduceUserInput:
									inputEmulator.EmulateInput(sm);
									break;
								case Command.GetDesktopInfo:
									lock (sm)
									{
										desktopInfo.WriteToDataStream(sm);
									}
									break;
								case Command.KeepAlive:
									break;
								default:
									Logger.Debug("Unsupported command code received: " + commandCode);
									lock (sm)
									{
										sm.WriteByte((byte)Command.Error_CommandCodeUnknown);
									}
									break;
							}
						}
					}
					finally
					{
						static_sm = null;
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (StreamDisconnectedException ex)
			{
				Logger.Info("Exiting because: " + ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				Logger.Info("Exiting due to main thread runner exception");
			}
			finally
			{
				Try.Catch(() => { dxgiDuplicator?.Dispose(); });
				Try.Catch(() => { screenCapturer?.Dispose(); });
				//Try.Catch(() => { inputEmulator?.Dispose(); });
				RobustExit();
			}
		}
		private static void desktopCaptureThreadRunner()
		{
			try
			{
				byte[] compressToBuffer = null;
				while (!isExiting && static_sm != null)
				{
					Thread.Sleep(1);
					DesktopCaptureTask task;
					while (!isExiting && static_sm != null && desktopCaptureTasks.TryDequeue(out task))
					{
						turbojpegCLI.SubsamplingOption subsamp = GetSubsamplingOptionFromImgFlags(task.imgFlags);
						FragmentedImage img = CaptureRawDesktopImage(task.imgFlags.HasFlag(ImgFlags.Refresh));
						SharedMemoryStream sm = static_sm;
						if (sm == null)
							break;
						lock (sm)
						{
							img.WriteToDataStream(static_sm, ref compressToBuffer, task.jpegQuality, subsamp);
						}
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (StreamDisconnectedException ex)
			{
				Logger.Info("Exiting because: " + ex.Message);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				Logger.Info("Exiting due to main thread runner exception");
			}
			finally
			{
				Try.Catch(() => { dxgiDuplicator?.Dispose(); });
				Try.Catch(() => { screenCapturer?.Dispose(); });
				//Try.Catch(() => { inputEmulator?.Dispose(); });
				RobustExit();
			}
		}

		/// <summary>
		/// Calls Application.Exit(), but leaves a background thread running which will kill the current process after a delay if Application.Exit() fails to do so in that time.
		/// </summary>
		/// <param name="msBeforeKill"></param>
		private static void RobustExit(int msBeforeKill = 1000)
		{
			Thread thrKillProc = new Thread(() =>
			{
				Try.Swallow(() => { Thread.Sleep(BPMath.Clamp(msBeforeKill, 0, 60000)); });
				Try.Swallow(() => { Process.GetCurrentProcess()?.Kill(); });
			});
			thrKillProc.Name = "Kill Process After 1 Second";
			thrKillProc.IsBackground = true;
			thrKillProc.Start();

			Application.Exit();
		}

		private static FragmentedImage CaptureRawDesktopImage(bool fullFrame)
		{

			if (DxgiOutputDuplicator.CurrentOSSupportsThisMethod)
			{
				bool proceedWithFastMethod = true;
				if (compatibleDesktopCaptureModeClock.IsRunning)
				{
					if (compatibleDesktopCaptureModeClock.ElapsedMilliseconds < timeToUseCompatibleDesktopCaptureMode)
						proceedWithFastMethod = false; // Not enough time has passed. Keep using compatible method.
					else
					{
						compatibleDesktopCaptureModeClock.Reset(); // time is up -- we can try fast mode again
						Logger.Info("Switching back to fast desktop capture mode");
					}
				}

				if (proceedWithFastMethod)
				{
					if (fullFrame)
						dxgiDuplicator.ResetOutputDuplicator();

					FragmentedImage imgFast = dxgiDuplicator.Capture();
					if (imgFast != null)
						return imgFast;
					// Most likely the console session is logging off, at the login screen, or just logged on and hasn't initialized the necessary directx parts yet.
					// Switch to the compatible capture mode for a while.
					compatibleDesktopCaptureModeClock.Start();
					Logger.Info("Switching to compatible desktop capture mode for next " + timeToUseCompatibleDesktopCaptureMode + " ms");
				}
			}
			// If we get here, we need to use a more-compatible capture method because we are probably at the login screen.
			FragmentedImage imgCompatible = screenCapturer.Capture(desktopInfo.GetScreen(0, 0));
			return imgCompatible;
		}

		private static SubsamplingOption GetSubsamplingOptionFromImgFlags(ImgFlags imgFlags)
		{
			if (imgFlags.HasFlag(ImgFlags.Color444))
				return turbojpegCLI.SubsamplingOption.SAMP_444;
			else if (imgFlags.HasFlag(ImgFlags.Color440))
				return turbojpegCLI.SubsamplingOption.SAMP_440;
			else if (imgFlags.HasFlag(ImgFlags.Color420))
				return turbojpegCLI.SubsamplingOption.SAMP_420;
			else
				return turbojpegCLI.SubsamplingOption.SAMP_GRAY;
		}

		//private static void CaptureCompressedDesktopImage(SharedMemoryStream sm)
		//{
		//	int method = sm.ReadByte();
		//	byte[] buf;
		//	if (method == 0)
		//		buf = ScreenCapture.GetScreenCap_DXGI();
		//	else if (method == 2)
		//		buf = ScreenCapture.GetScreenCap_2();
		//	else if (method == 3)
		//		buf = ScreenCapture.GetScreenCap_3();
		//	else
		//		buf = ScreenCapture.GetScreenCap_1();
		//	if (buf.Length == 0)
		//		UnknownError(sm, "CaptureCompressedDesktopImage");
		//	else
		//	{
		//		sm.WriteByte((byte)Command.CaptureCompressedDesktopImage); // Write command code
		//		sm.WriteInt32(buf.Length); // Write length of image
		//		sm.Write(buf, 0, buf.Length); // Write image
		//	}
		//}
		public static void UnknownError(SharedMemoryStream sm, string whoAmI)
		{
			Logger.Debug(whoAmI + " writing Error_UnknownError response");
			lock (sm)
			{
				sm.WriteByte((byte)Command.Error_Unspecified);
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			HandleUnhandledException((Exception)e.ExceptionObject, "Unhandled Exception");
		}

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			HandleUnhandledException(e.Exception, "Unhandled Thread Exception");
		}

		private static void HandleUnhandledException(Exception exception, string message)
		{
			Logger.Debug(exception, message);
		}
	}
}
