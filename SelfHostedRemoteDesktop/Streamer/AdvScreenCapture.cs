using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.NativeWin;
using SHRDLib;

namespace SelfHostedRemoteDesktop.Streamer
{
	/// <summary>
	/// A screen capture class capable of capturing even when no user is logged in.
	/// </summary>
	internal class AdvScreenCapture : CriticalFinalizerObject, IDisposable
	{
		private bool isDisposed = false;
		/// <summary>
		/// Handle for capturing the screen
		/// </summary>
		private AutoDisposeHandle deviceContext = null;
		/// <summary>
		/// This gets set = true whenever the display settings change, which forces us to reconfigure the capturer
		/// </summary>
		private bool displaySettingsChanged = true;
		/// <summary>
		/// Provides timing data for this class.
		/// </summary>
		private Stopwatch timing = new Stopwatch();
		/// <summary>
		/// The timing value after which AssociateCurrentThreadWithDefaultDesktop should be called.
		/// </summary>
		private long nextDesktopCheck = long.MinValue;
		/// <summary>
		/// The interval at which AssociateCurrentThreadWithDefaultDesktop should be called.
		/// </summary>
		private long desktopCheckInterval = 10000;
		public AdvScreenCapture()
		{
			timing.Start();
		}

		~AdvScreenCapture()
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
			Try.Catch(() => { deviceContext?.Dispose(); });
			timing.Stop();
			isDisposed = true;
		}
		public void DisplaySettingsChanged()
		{
			displaySettingsChanged = true;
		}
		/// <summary>
		/// Captures a screenshot. Remember, you should always use this class from the same thread.
		/// </summary>
		/// <returns></returns>
		public FragmentedImage Capture(SHRDLib.NetCommand.DesktopScreen screen)
		{
			if (screen == null)
				throw new NullReferenceException("screen cannot be null");
			long thisMs = timing.ElapsedMilliseconds;
			// Learn about the display configuration
			if (displaySettingsChanged)
			{
				displaySettingsChanged = false;
				ClearCaptureHandle();
				DesktopManager.ShouldReassociate = true;
			}
			if (DesktopManager.ShouldReassociate || thisMs > nextDesktopCheck)
			{
				DesktopManager.ShouldReassociate = false;
				nextDesktopCheck = thisMs + desktopCheckInterval;
				if (DesktopManager.AssociateCurrentThreadWithDefaultDesktop())
					ClearCaptureHandle(); // Desktop was changed.
			}
			// Capture
			Screenshot screenshot = CaptureScreenshot(screen.X, screen.Y, screen.Width, screen.Height);
			if (screenshot == null)
				return new FragmentedImage();
			FragmentedImage img = new FragmentedImage(new MovedImageFragment[0], new DirtyImageFragment[] { new DirtyImageFragment(screen.Y, screen.X + screen.Width, screen.Height + screen.Y, screen.X, screenshot) });
			return img;
		}

		#region Pulling Teeth (Screen Capture)
		/// <summary>
		/// Attempts to capture a screenshot of the current desktop.
		/// </summary>
		/// <returns></returns>
		private Screenshot CaptureScreenshot(int x, int y, int width, int height)
		{
			BasicEventTimer bet = new BasicEventTimer("0.000");
			bet.Start("Setup");
			if (!CreateCaptureHandleIfNecessary())
				return null;

			NativeMethods.BITMAPINFO bmpInfo = new NativeMethods.BITMAPINFO();
			using (AutoDisposeHandle bmp = AutoDisposeHandle.Create(NativeMethods.CreateCompatibleBitmap(deviceContext, 1, 1), h => NativeMethods.DeleteObject(h)))
			{
				bmpInfo.bmiHeader.biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
				if (0 == NativeMethods.GetDIBits(deviceContext, bmp, 0, 1, null, ref bmpInfo, NativeMethods.DIB_Color_Mode.DIB_RGB_COLORS))
					Win32Helper.ThrowLastWin32Error();
				bmpInfo.bmiHeader.biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFO));
				if (0 == NativeMethods.GetDIBits(deviceContext, bmp, 0, 1, null, ref bmpInfo, NativeMethods.DIB_Color_Mode.DIB_RGB_COLORS))
					Win32Helper.ThrowLastWin32Error();
			}
			bmpInfo.bmiHeader.biWidth = width;
			bmpInfo.bmiHeader.biHeight = -height;
			bmpInfo.bmiHeader.biSizeImage = 0;

			// Create memory device context.  When done, delete it.
			using (AutoDisposeHandle dc = AutoDisposeHandle.Create(NativeMethods.CreateCompatibleDC(IntPtr.Zero), h => NativeMethods.DeleteDC(h)))
			{
				if (dc == null)
					Win32Helper.ThrowLastWin32Error("Failed to create memory device context");

				// Create "DIB Section" (a Bitmap, more or less).  When done, delete it.
				IntPtr ppvBits = new IntPtr();
				using (AutoDisposeHandle newDib = AutoDisposeHandle.Create(NativeMethods.CreateDIBSection(dc, ref bmpInfo, 0U, out ppvBits, IntPtr.Zero, 0U), h => NativeMethods.DeleteObject(h)))
				//using (AutoDisposeHandle newDib = AutoDisposeHandle.Create(NativeMethods.CreateCompatibleBitmap(deviceContext, width, height), h => NativeMethods.DeleteObject(h)))
				{
					if (newDib == null)
						Win32Helper.ThrowLastWin32Error("Failed to create DIB Section");

					// Assign new DIB Section to our memory device context.  When done, put back the old DIB Section.
					using (AutoDisposeHandle oldDib = AutoDisposeHandle.Create(NativeMethods.SelectObject(dc, newDib), h => NativeMethods.SelectObject(dc, h)))
					{
						if (oldDib == null)
							Win32Helper.ThrowLastWin32Error("Failed to assign new DIB Section to memory device context");
						bet.Start("BitBlt");

						// Copy data from the screen to our memory device context
						if (!NativeMethods.BitBlt(dc, 0, 0, width, height, deviceContext, x, y, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt))
						{
							Logger.Debug("BitBlt failed with error code: " + Win32Helper.GetLastWin32Error());
							ClearCaptureHandle();
							DesktopManager.ShouldReassociate = true;
						}
						else
						{
							bet.Start("new Screenshot/Copy bits");
							Screenshot screenshot = new Screenshot(width, height, bmpInfo.bmiHeader.biBitCount);
							Marshal.Copy(ppvBits, screenshot.Buffer, 0, screenshot.Buffer.Length);
							bet.Stop();
							//Logger.Info(bet.ToString(Environment.NewLine));
							return screenshot;
						}
					}
				}
			}
			return null;
		}
		private void ClearCaptureHandle()
		{
			if (deviceContext != null)
				deviceContext.Dispose();
			deviceContext = null;
		}
		/// <summary>
		/// Attempts to create a capture handle if it does not already exist.  Returns false if the capture handle could not be created.
		/// </summary>
		private bool CreateCaptureHandleIfNecessary()
		{
			if (deviceContext == null)
				deviceContext = AutoDisposeHandle.Create(NativeMethods.GetDC(IntPtr.Zero), h => NativeMethods.ReleaseDC(IntPtr.Zero, h));
			return deviceContext != null;
		}
		#endregion
	}
}
