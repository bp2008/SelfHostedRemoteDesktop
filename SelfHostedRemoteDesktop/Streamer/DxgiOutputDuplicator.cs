using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SelfHostedRemoteDesktop;
using SelfHostedRemoteDesktop.Native;
using SHRDLib.NetCommand;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SHRDLib;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace SelfHostedRemoteDesktop.Streamer
{

	public class DxgiOutputDuplicator : IDisposable
	{
		public static readonly bool CurrentOSSupportsThisMethod = Environment.OSVersion.Version >= new Version(6, 2);
		private Factory1 factory;
		private Adapter1 adapter;
		private Output output;
		private Device device;
		private Output1 screen;
		private RawRectangle bounds;
		private OutputDuplication duplicatedOutput;
		private Texture2D screenTexture;
		private ArrayBufferHelper<RawRectangle> rawRectHelper = new ArrayBufferHelper<RawRectangle>();
		private ArrayBufferHelper<OutputDuplicateMoveRectangle> moveRectHelper = new ArrayBufferHelper<OutputDuplicateMoveRectangle>();
		//private Surface1 _renderSurface;

		public DxgiOutputDuplicator(int adapterIndex, int displayIndex)
		{
			if (!CurrentOSSupportsThisMethod)
				throw new Exception("This method is not supported by the current OS");
			Configure(adapterIndex, displayIndex);
		}
		#region Dispose/Cleanup
		~DxgiOutputDuplicator()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			DestroyOutputDuplicator();
			screenTexture?.Dispose();
			screen?.Dispose();
			output?.Dispose();
			device?.Dispose();
			adapter?.Dispose();
			factory?.Dispose();
		}
		private void DestroyOutputDuplicator()
		{
			duplicatedOutput?.Dispose();
			duplicatedOutput = null;
		}
		private void DestroySlowerMethodParts()
		{
			//if (_hdc != IntPtr.Zero)
			//{
			//	NativeMethods.ReleaseDC(_desktopWindow, _hdc);
			//	NativeMethods.DeleteDC(_hdc);
			//	_hdc = IntPtr.Zero;
			//	_desktopWindow = IntPtr.Zero;
			//}
			//_renderSurface?.Dispose();
			//_renderSurface = null;
		}
		#endregion

		public static DesktopInfo GetDesktopInfo()
		{
			List<DesktopScreen> listOfScreens = new List<DesktopScreen>();
			using (Factory1 factory = new Factory1())
			{
				Adapter1[] adapters = factory.Adapters1;
				try
				{
					for (int adapterIdx = 0; adapterIdx < adapters.Length; adapterIdx++)
					{
						Adapter1 adapter = adapters[adapterIdx];
						Output[] outputs = adapter.Outputs;
						try
						{
							for (int outputIdx = 0; outputIdx < outputs.Length; outputIdx++)
								using (Output1 screen = outputs[outputIdx].QueryInterface<Output1>())
								{
									string adapterName = string.Join(" ", adapter.Description1.Description.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
									string outputName = screen.Description.DeviceName.Trim();
									listOfScreens.Add(new DesktopScreen((byte)adapterIdx, (byte)outputIdx, adapterName, outputName
										, (short)screen.Description.DesktopBounds.Left
										, (short)screen.Description.DesktopBounds.Top
										, (ushort)(screen.Description.DesktopBounds.Right - screen.Description.DesktopBounds.Left)
										, (ushort)(screen.Description.DesktopBounds.Bottom - screen.Description.DesktopBounds.Top)));
								}
						}
						finally
						{
							foreach (Output output in outputs)
								output.Dispose();
						}
					}
				}
				finally
				{
					foreach (Adapter1 adapter in adapters)
						adapter.Dispose();
				}
			}
			return new DesktopInfo(listOfScreens.ToArray());
		}

		/// <summary>
		/// (Re)configures the class to a fresh state.
		/// </summary>
		private void Configure(int adapterIndex, int displayIndex)
		{
			Dispose(false);
			factory = new Factory1();
			adapter = factory.GetAdapter1(adapterIndex);
			device = new Device(adapter);
			output = adapter.GetOutput(displayIndex);
			screen = output.QueryInterface<Output1>();
			bounds = screen.Description.DesktopBounds;

			Texture2DDescription textureDesc = new Texture2DDescription
			{
				CpuAccessFlags = CpuAccessFlags.Read,
				BindFlags = BindFlags.None,
				Format = Format.B8G8R8A8_UNorm,
				Width = bounds.Right - bounds.Left,
				Height = bounds.Bottom - bounds.Top,
				OptionFlags = ResourceOptionFlags.None,
				MipLevels = 1,
				ArraySize = 1,
				SampleDescription = { Count = 1, Quality = 0 },
				Usage = ResourceUsage.Staging
			};
			screenTexture = new Texture2D(device, textureDesc);
			ResetOutputDuplicator();
		}

		/// <summary>
		/// Destroys and re-recreates the duplicatedOutput object, then returns true if successful. This causes the next captured frame to cover the whole desktop.
		/// </summary>
		/// <returns></returns>
		public bool ResetOutputDuplicator()
		{
			DestroyOutputDuplicator();
			Try.Swallow(() => { duplicatedOutput = screen.DuplicateOutput(device); });
			return duplicatedOutput != null;
		}


		public FragmentedImage Capture()
		{
			if (duplicatedOutput == null && !ResetOutputDuplicator())
				return null;
			OutputDuplicateFrameInformation frameInfo;
			SharpDX.DXGI.Resource screenResource = null;
			bool success = false;
			try
			{
				int frameDisposeFailures = 0;
				while (true)
				{
					try
					{
						screenResource = null;
						duplicatedOutput.AcquireNextFrame(10000, out frameInfo, out screenResource);
						if (frameInfo.AccumulatedFrames > 0)
						{
							if (screenResource == null)
							{
								Logger.Debug("screenResource was null in DxgiOutputDuplicator");
								return null;
							}

							// Copy the texture so we can access the pixel data of the copy
							using (Texture2D screenTexture2D = screenResource.QueryInterface<Texture2D>())
								device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);


							// Learn which rectangles moved
							OutputDuplicateMoveRectangle[] moveRects = GetMoveRectangles();

							// Learn which rectangles were made dirty
							RawRectangle[] dirtyRects = GetDirtyRectangles();

							FragmentedImage img = new FragmentedImage(new MovedImageFragment[moveRects.Length], new DirtyImageFragment[dirtyRects.Length]);
							int i = 0;
							foreach (OutputDuplicateMoveRectangle moveRect in moveRects)
							{
								img.movedFragments[i++] = new MovedImageFragment(
									moveRect.DestinationRect.Top
									, moveRect.DestinationRect.Right
									, moveRect.DestinationRect.Bottom
									, moveRect.DestinationRect.Left
									, moveRect.SourcePoint.X
									, moveRect.SourcePoint.Y);
							}

							// Get the desktop capture pixel data
							i = 0;
							DataBox mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);
							foreach (RawRectangle dirtyRect in dirtyRects)
							{
								Screenshot screenshot = new Screenshot(dirtyRect.Right - dirtyRect.Left, dirtyRect.Bottom - dirtyRect.Top, 32);
								IntPtr source = mapSource.DataPointer;
								source += dirtyRect.Top * mapSource.RowPitch; // Offset source to the correct row
								source += dirtyRect.Left * 4; // Offset source to the correct column
								int destOffset = 0;
								for (int y = dirtyRect.Top; y < dirtyRect.Bottom; y++)
								{
									Marshal.Copy(source, screenshot.Buffer, destOffset, screenshot.Stride);
									source += mapSource.RowPitch;
									destOffset += screenshot.Stride;
								}

								img.dirtyFragments[i++] = new DirtyImageFragment(
									dirtyRect.Top
									, dirtyRect.Right
									, dirtyRect.Bottom
									, dirtyRect.Left
									, screenshot);
							}
							device.ImmediateContext.UnmapSubresource(screenTexture, 0);
							//DebugDrawRects(screenshot, moveRects, dirtyRects);

							success = true;
							return img;
						}
					}
					catch (SharpDXException e)
					{
						if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
							throw e;
					}
					finally
					{
						if (screenResource != null)
						{
							try
							{
								screenResource.Dispose();
								duplicatedOutput.ReleaseFrame();
							}
							catch
							{
								if (++frameDisposeFailures > 2)
									throw;
								ResetOutputDuplicator();
							}
						}
					}
				}
			}
			finally
			{
				if (!success)
					DestroyOutputDuplicator();
			}
			// TODO: Delete cursor drawing stuff.
			//{
			//	IntPtr dc = _renderSurface.GetDC(new RawBool(true));
			//	NativeMethods.SelectObject(_hdc, dc);
			//	NativeMethods.BitBlt(dc, 0, 0, bounds.Right - bounds.Left
			//		, bounds.Bottom - bounds.Top, _hdc, bounds.Left
			//		, bounds.Top, System.Drawing.CopyPixelOperation.SourceCopy);
			//	NativeMethods.CURSORINFO pci;
			//	pci.cbSize = NativeMethods.SizeOfCursorInfo;
			//	if (NativeMethods.GetCursorInfo(out pci) && pci.flags > 0)
			//		NativeMethods.DrawIcon(dc, pci.ptScreenPos.X, pci.ptScreenPos.Y, pci.hCursor);
			//	_renderSurface.ReleaseDC();
			//	return null;
			//}
		}

		private void DebugDrawRects(Screenshot screenshot, OutputDuplicateMoveRectangle[] moveRects, RawRectangle[] dirtyRects)
		{
			foreach (RawRectangle rect in dirtyRects)
			{
				DebugDrawRectOnScreenshot(screenshot, rect, 255, 0, 0);
			}
			foreach (OutputDuplicateMoveRectangle rect in moveRects)
			{
				DebugDrawRectOnScreenshot(screenshot, rect.DestinationRect, 0, 0, 255);
			}
		}

		private void DebugDrawRectOnScreenshot(Screenshot screenshot, RawRectangle rect, byte r, byte g, byte b)
		{
			// Draw top and bottom lines
			for (int y = rect.Top; y < rect.Bottom; y++)
			{
				int offset = y * screenshot.Stride;
				if (y == rect.Top || y + 1 == rect.Bottom)
				{
					for (int x = rect.Left; x < (rect.Right - 1); x++)
					{
						int offset2 = x * 4;
						screenshot.Buffer[offset + offset2] = b;
						screenshot.Buffer[offset + offset2 + 1] = g;
						screenshot.Buffer[offset + offset2 + 2] = r;
					}
				}
				else {
					int offset2 = rect.Left * 4;
					screenshot.Buffer[offset + offset2] = b;
					screenshot.Buffer[offset + offset2 + 1] = g;
					screenshot.Buffer[offset + offset2 + 2] = r;
					offset2 = (rect.Right - 1) * 4;
					screenshot.Buffer[offset + offset2] = b;
					screenshot.Buffer[offset + offset2 + 1] = g;
					screenshot.Buffer[offset + offset2 + 2] = r;
				}
			}
		}

		public OutputDuplicateMoveRectangle[] GetMoveRectangles()
		{
			OutputDuplicateMoveRectangle[] moveRectBuffer = moveRectHelper.GetExistingBuffer();
			// First, try with our existing buffer. It may fail and make us increase the buffer size.
			int requiredMoveRectSize = -1;
			try
			{
				duplicatedOutput.GetFrameMoveRects(moveRectHelper.bufferSizeBytes, moveRectBuffer, out requiredMoveRectSize);
			}
			catch (SharpDXException e)
			{
				if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.MoreData.Result.Code)
					throw e;
			}
			if (requiredMoveRectSize == -1)
				throw new Exception("GetFrameMoveRects did not tell us the required buffer size");

			if (requiredMoveRectSize > moveRectHelper.bufferSizeBytes)
			{
				if (requiredMoveRectSize % moveRectHelper.itemSize != 0)
					throw new Exception("requiredMoveRectSize " + requiredMoveRectSize + " is not divisible by " + moveRectHelper.itemSize);

				// Increase buffer size and try again
				moveRectBuffer = moveRectHelper.GetLargerBuffer(requiredMoveRectSize / moveRectHelper.itemSize);
				duplicatedOutput.GetFrameMoveRects(moveRectHelper.bufferSizeBytes, moveRectBuffer, out requiredMoveRectSize);

				if (requiredMoveRectSize % moveRectHelper.itemSize != 0)
					throw new Exception("requiredMoveRectSize " + requiredMoveRectSize + " is not divisible by " + moveRectHelper.itemSize);
			}
			int numMoveRects = requiredMoveRectSize / moveRectHelper.itemSize;
			OutputDuplicateMoveRectangle[] retVal = new OutputDuplicateMoveRectangle[numMoveRects];
			for (int i = 0; i < numMoveRects; i++)
			{
				retVal[i] = moveRectBuffer[i];
				//Logger.Info("MoveRect [" + moveRectBuffer[i].SourcePoint.X
				//	+ "," + +moveRectBuffer[i].SourcePoint.Y + "] -> ["
				//	+ moveRectBuffer[i].DestinationRect.Left + ","
				//	+ moveRectBuffer[i].DestinationRect.Top + ","
				//	+ moveRectBuffer[i].DestinationRect.Right + ","
				//	+ moveRectBuffer[i].DestinationRect.Bottom + "]");
			}
			return retVal;
		}
		public RawRectangle[] GetDirtyRectangles()
		{
			RawRectangle[] rawRectBuffer = rawRectHelper.GetExistingBuffer();
			// First, try with our existing buffer. It may fail and make us increase the buffer size.
			int requiredRawRectSize = -1;
			try
			{
				duplicatedOutput.GetFrameDirtyRects(rawRectHelper.bufferSizeBytes, rawRectBuffer, out requiredRawRectSize);
			}
			catch (SharpDXException e)
			{
				if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.MoreData.Result.Code)
					throw e;
			}
			if (requiredRawRectSize == -1)
				throw new Exception("GetFrameDirtyRects did not tell us the required buffer size");

			if (requiredRawRectSize > rawRectHelper.bufferSizeBytes)
			{
				if (requiredRawRectSize % rawRectHelper.itemSize != 0)
					throw new Exception("requiredRawRectSize " + requiredRawRectSize + " is not divisible by " + rawRectHelper.itemSize);

				// Increase buffer size and try again
				rawRectBuffer = rawRectHelper.GetLargerBuffer(requiredRawRectSize / rawRectHelper.itemSize);
				duplicatedOutput.GetFrameDirtyRects(rawRectHelper.bufferSizeBytes, rawRectBuffer, out requiredRawRectSize);

				if (requiredRawRectSize % rawRectHelper.itemSize != 0)
					throw new Exception("requiredRawRectSize " + requiredRawRectSize + " is not divisible by " + rawRectHelper.itemSize);
			}
			int numDirtyRects = requiredRawRectSize / rawRectHelper.itemSize;
			RawRectangle[] retVal = new RawRectangle[numDirtyRects];
			for (int i = 0; i < numDirtyRects; i++)
			{
				retVal[i] = rawRectBuffer[i];
				//Logger.Info("DirtyRect ["
				//	+ rawRectBuffer[i].Left + ","
				//	+ rawRectBuffer[i].Top + ","
				//	+ rawRectBuffer[i].Right + ","
				//	+ rawRectBuffer[i].Bottom + "]");
			}
			return retVal;
		}
	}
	public class ArrayBufferHelper<T>
	{
		private T[] buffer;
		public readonly int itemSize;
		public int bufferSizeBytes { get; private set; }
		public ArrayBufferHelper()
		{
			bufferSizeBytes = itemSize = Marshal.SizeOf(typeof(T));
			buffer = new T[1];
		}
		public T[] GetExistingBuffer()
		{
			return buffer;
		}
		public T[] GetLargerBuffer(int size)
		{
			buffer = new T[size];
			bufferSizeBytes = size * itemSize;
			return buffer;
		}
	}
}
