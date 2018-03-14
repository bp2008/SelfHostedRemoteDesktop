using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SHRDLib;

namespace SHRDLib.NetCommand
{
	public class DesktopInfo
	{
		public DesktopScreen[] screens { get; private set; }
		public IntRectangle outerBounds { get; private set; }
		public DesktopInfo()
		{
			this.screens = new DesktopScreen[0];
		}
		public DesktopInfo(DesktopScreen[] screens)
		{
			this.screens = screens;
		}
		public DesktopInfo(IDataStream s)
		{
			int count = s.ReadByte();
			List<DesktopScreen> listOfScreens = new List<DesktopScreen>();
			for (int i = 0; i < count; i++)
			{
				listOfScreens.Add(new DesktopScreen(s));
			}
			screens = listOfScreens.ToArray();
		}
		public void WriteToDataStream(IDataStream s)
		{
			s.WriteByte((byte)Command.GetDesktopInfo);
			if (screens.Length > 255)
				throw new Exception("Number of screens may not be greater than 255");
			s.WriteByte((byte)screens.Length);
			foreach (DesktopScreen screen in screens)
				screen.WriteToDataStream(s);
		}

		public DesktopScreen GetScreen(int adapterIndex, int outputIndex)
		{
			return screens.FirstOrDefault(s => s.adapterIndex == adapterIndex && s.outputIndex == outputIndex);
		}
	}

	public class IntRectangle
	{
		public int X;
		public int Y;
		public int Width;
		public int Height;
		public IntRectangle() { }
		public IntRectangle(int X, int Y, int Width, int Height)
		{
			this.X = X;
			this.Y = Y;
			this.Width = Width;
			this.Height = Height;
		}
	}

	public class DesktopScreen
	{
		public byte adapterIndex;
		public byte outputIndex;
		public string adapterName;
		public string outputName;
		public short X;
		public short Y;
		public ushort Width;
		public ushort Height;
		public DesktopScreen(byte adapterIndex, byte outputIndex, string adapterName, string outputName, short X, short Y, ushort Width, ushort Height)
		{
			this.adapterIndex = adapterIndex;
			this.outputIndex = outputIndex;
			this.adapterName = adapterName;
			this.outputName = outputName;
			this.X = X;
			this.Y = Y;
			this.Width = Width;
			this.Height = Height;
		}

		public DesktopScreen(IDataStream s)
		{
			adapterIndex = (byte)s.ReadByte();
			outputIndex = (byte)s.ReadByte();

			ushort strLength = s.ReadUInt16();
			adapterName = Encoding.UTF8.GetString(ByteUtil.ReadNBytes(s, strLength));

			strLength = s.ReadUInt16();
			outputName = Encoding.UTF8.GetString(ByteUtil.ReadNBytes(s, strLength));

			X = s.ReadInt16();
			Y = s.ReadInt16();
			Width = s.ReadUInt16();
			Height = s.ReadUInt16();
		}

		internal void WriteToDataStream(IDataStream s)
		{
			s.WriteByte(adapterIndex);
			s.WriteByte(outputIndex);

			byte[] strData = Encoding.UTF8.GetBytes(adapterName);
			s.WriteUInt16((ushort)strData.Length);
			s.Write(strData, 0, strData.Length);

			strData = Encoding.UTF8.GetBytes(outputName);
			s.WriteUInt16((ushort)strData.Length);
			s.Write(strData, 0, strData.Length);

			s.WriteInt16(X);
			s.WriteInt16(Y);
			s.WriteUInt16(Width);
			s.WriteUInt16(Height);
		}
	}
}
