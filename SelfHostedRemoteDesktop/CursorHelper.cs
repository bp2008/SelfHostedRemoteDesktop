using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SelfHostedRemoteDesktop.NetCommand;

namespace SelfHostedRemoteDesktop
{
	public static class CursorHelper
	{
		private static List<Cursor> allCursors = new List<Cursor>();
		private static ConcurrentDictionary<Cursor, byte[]> cursorPngs = null;
		static CursorHelper()
		{
			Thread thrCursorLoader = new Thread(() =>
			{
				addCursor(Cursors.AppStarting);
				addCursor(Cursors.Arrow);
				addCursor(Cursors.Cross);
				addCursor(Cursors.Default);
				addCursor(Cursors.Hand);
				addCursor(Cursors.Help);
				addCursor(Cursors.HSplit);
				addCursor(Cursors.IBeam);
				addCursor(Cursors.No);
				addCursor(Cursors.NoMove2D);
				addCursor(Cursors.NoMoveHoriz);
				addCursor(Cursors.NoMoveVert);
				addCursor(Cursors.PanEast);
				addCursor(Cursors.PanNE);
				addCursor(Cursors.PanNorth);
				addCursor(Cursors.PanNW);
				addCursor(Cursors.PanSE);
				addCursor(Cursors.PanSouth);
				addCursor(Cursors.PanSW);
				addCursor(Cursors.PanWest);
				addCursor(Cursors.SizeAll);
				addCursor(Cursors.SizeNESW);
				addCursor(Cursors.SizeNS);
				addCursor(Cursors.SizeNWSE);
				addCursor(Cursors.SizeWE);
				addCursor(Cursors.UpArrow);
				addCursor(Cursors.VSplit);
				addCursor(Cursors.WaitCursor);
				allCursors.Sort(new Comparison<Cursor>((c1, c2) =>
				{
					return c1.Handle.ToInt32() - c2.Handle.ToInt32();
				}));
				RenderAllCursors();
			});
			thrCursorLoader.Name = "Cursor Loading Thread";
			thrCursorLoader.IsBackground = true;
			thrCursorLoader.Start();
		}

		private static void addCursor(Cursor cursor)
		{
			allCursors.Add(cursor);
		}

		public static void RenderAllCursors()
		{
			ConcurrentDictionary<Cursor, byte[]> dict = new ConcurrentDictionary<Cursor, byte[]>();
			try
			{
				for (int i = 0; i < allCursors.Count; i++)
				{
					Cursor c = allCursors[i];
					int width, height;
					byte[] data = DrawCursor(c, out width, out height);
					if (data != null)
						dict[c] = data;
					else
					{
						using (Bitmap bmp = new Bitmap(c.Size.Width, c.Size.Height, PixelFormat.Format32bppArgb))
						{
							using (Graphics gBitmap = Graphics.FromImage(bmp))
							{
								c.Draw(gBitmap, new Rectangle(0, 0, c.Size.Width, c.Size.Height));
								using (MemoryStream ms = new MemoryStream())
								{
									bmp.Save(ms, ImageFormat.Png);

								}
							}
						}
					}
				}
			}
			finally
			{
				cursorPngs = dict;
			}
		}
		public static void SaveAllCursorImages()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			while (cursorPngs == null && sw.ElapsedMilliseconds < 5000)
				Thread.Sleep(1);
			if (cursorPngs == null)
				throw new Exception("Cursor images could not be created in a timely manner.");
			Directory.CreateDirectory("cursors");
			for (int i = 0; i < allCursors.Count; i++)
			{
				Cursor c = allCursors[i];
				byte[] data = cursorPngs[c];
				File.WriteAllBytes("cursors/cur_" + i + ".png", data);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICONINFO
		{
			public bool fIcon;
			public int xHotspot;
			public int yHotspot;
			public IntPtr hbmMask;
			public IntPtr hbmColor;
		}

		[DllImport("user32")]
		private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO pIconInfo);

		[DllImport("user32.dll")]
		private static extern IntPtr LoadCursorFromFile(string lpFileName);

		[DllImport("gdi32.dll", SetLastError = true)]
		private static extern bool DeleteObject(IntPtr hObject);

		private static byte[] DrawCursor(Cursor cur, out int width, out int height)
		{
			width = 0;
			height = 0;

			ICONINFO ii;
			GetIconInfo(cur.Handle, out ii);
			try
			{
				if (ii.hbmColor != IntPtr.Zero)
					return MakePngFromHbm(ii.hbmColor, out width, out height);
				else if (ii.hbmMask != IntPtr.Zero)
					return MakePngFromHbm(ii.hbmMask, out width, out height);
				else
					return null;
			}
			finally
			{
				if (ii.hbmColor != IntPtr.Zero)
					DeleteObject(ii.hbmColor);
				if (ii.hbmMask != IntPtr.Zero)
					DeleteObject(ii.hbmMask);
			}
		}

		private static byte[] MakePngFromHbm(IntPtr hbm, out int width, out int height)
		{
			using (Bitmap bmp = Image.FromHbitmap(hbm))
			{
				width = bmp.Width;
				height = bmp.Height;

				if (bmp.PixelFormat == PixelFormat.Format1bppIndexed)
				{
					//return TryHandle1BppFormat(bmp);
					using (Bitmap dstBitmap = bmp.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppArgb))
					{
						using (MemoryStream ms = new MemoryStream())
						{
							dstBitmap.Save(ms, ImageFormat.Png);
							return ms.ToArray();
						}
					}
				}
				else
				{
					BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);
					using (Bitmap dstBitmap = new Bitmap(width, height, bmData.Stride, PixelFormat.Format32bppArgb, bmData.Scan0))
					{
						bmp.UnlockBits(bmData);
						using (MemoryStream ms = new MemoryStream())
						{
							dstBitmap.Save(ms, ImageFormat.Png);
							return ms.ToArray();
						}
					}
				}
			}
		}

		/// <summary>
		/// Apparently the "Format1bppIndexed" format is not intuitive, because this method doesn't work.
		/// </summary>
		/// <param name="bmp"></param>
		/// <returns></returns>
		private static byte[] TryHandle1BppFormat(Bitmap bmp)
		{
			BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);
			byte[] argb;
			try
			{
				byte[] data = new byte[Math.Abs(bmData.Stride) * bmData.Height];
				Marshal.Copy(bmData.Scan0, data, 0, data.Length);
				argb = new byte[4 * bmData.Width * bmData.Height];
				StringBuilder sb = new StringBuilder();
				for (int i = 0, n_s = 0; i < data.Length; i++, n_s += 32)
				{
					for (int bitOffset = 7, n = n_s; bitOffset >= 0; bitOffset--, n += 4)
					{
						if ((n / 4) % bmp.Width == 0)
							sb.AppendLine();
						sb.Append(GetBit(bitOffset, data[i]) ? 'X' : ' ');
						argb[n] = argb[n + 1] = argb[n + 2] = (byte)(GetBit(bitOffset, data[i]) ? 0 : 255);
						argb[n + 3] = 255;
					}
				}
				Console.WriteLine(sb.ToString());
			}
			finally
			{
				bmp.UnlockBits(bmData);
			}
			using (Bitmap dstBitmap = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb))
			{
				bmData = dstBitmap.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, dstBitmap.PixelFormat);
				try
				{
					if (bmData.Stride != 4 * bmp.Width)
						throw new Exception("Bitmap stride (" + bmData.Stride + ") was not as expected (" + 4 * bmp.Width + ")");
					{
						Marshal.Copy(argb, 0, bmData.Scan0, argb.Length);
						using (MemoryStream ms = new MemoryStream())
						{
							dstBitmap.Save(ms, ImageFormat.Png);
							return ms.ToArray();
						}
					}
				}
				finally
				{
					dstBitmap.UnlockBits(bmData);
				}
			}
		}

		private static bool GetBit(int bitOffset, byte b)
		{
			if (bitOffset == 0)
				return (b & 0b0000_0001) > 0;
			else if (bitOffset == 1)
				return (b & 0b0000_0010) > 0;
			else if (bitOffset == 2)
				return (b & 0b0000_0100) > 0;
			else if (bitOffset == 3)
				return (b & 0b0000_1000) > 0;
			else if (bitOffset == 4)
				return (b & 0b0001_0000) > 0;
			else if (bitOffset == 5)
				return (b & 0b0010_0000) > 0;
			else if (bitOffset == 6)
				return (b & 0b0100_0000) > 0;
			else
				return (b & 0b1000_0000) > 0;
		}
	}
}
