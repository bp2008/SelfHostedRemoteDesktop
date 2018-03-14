using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using SelfHostedRemoteDesktop.NetCommand;
using SHRDLib;
using turbojpegCLI;

namespace SelfHostedRemoteDesktop
{
	public class FragmentedImage
	{
		public byte streamId = 0;
		/// <summary>
		/// If true, the frame is empty.  This is normal when using the DxgiOutputDuplicator and nothing changed on the screen before a timeout was reached.
		/// </summary>
		public bool EmptyFrame
		{
			get
			{
				return movedFragments == null || dirtyFragments == null || (movedFragments.Length == 0 && dirtyFragments.Length == 0);
			}
		}

		public MovedImageFragment[] movedFragments;
		public DirtyImageFragment[] dirtyFragments;
		/// <summary>
		/// Creates a FragmentedImage with empty movedFragments and dirtyFragments arrays.
		/// </summary>
		public FragmentedImage()
		{
			this.movedFragments = new MovedImageFragment[0];
			this.dirtyFragments = new DirtyImageFragment[0];
		}
		public FragmentedImage(MovedImageFragment[] movedFragments, DirtyImageFragment[] dirtyFragments)
		{
			this.movedFragments = movedFragments;
			this.dirtyFragments = dirtyFragments;
		}
		public FragmentedImage(IDataStream s)
		{
			streamId = (byte)s.ReadByte();

			List<MovedImageFragment> moveList = new List<MovedImageFragment>();
			List<DirtyImageFragment> dirtList = new List<DirtyImageFragment>();

			ushort moveFragCount = s.ReadUInt16();
			ushort dirtyFragCount = s.ReadUInt16();
			for (int i = 0; i < moveFragCount; i++)
				moveList.Add(new MovedImageFragment(s));
			for (int i = 0; i < dirtyFragCount; i++)
				dirtList.Add(new DirtyImageFragment(s));

			movedFragments = moveList.ToArray();
			dirtyFragments = dirtList.ToArray();
		}
		public void WriteToDataStream(IDataStream s, ref byte[] compressToBuffer, int jpegQuality = 80, turbojpegCLI.SubsamplingOption subsamp = turbojpegCLI.SubsamplingOption.SAMP_420)
		{
			if (movedFragments.Length > 65535)
				throw new Exception("FragmentedImage has too many movedFragments: " + movedFragments.Length);

			if (dirtyFragments.Length > 65535)
				throw new Exception("FragmentedImage has too many dirtyFragments: " + dirtyFragments.Length);

			s.WriteByte((byte)Command.GetScreenCapture); // Write command code

			s.WriteByte(streamId); // Write stream ID

			// Calculate buffer sizes

			s.WriteUInt16((ushort)movedFragments.Length); // Write number of fragments
			s.WriteUInt16((ushort)dirtyFragments.Length); // Write number of fragments

			if (movedFragments.Length == 0 && dirtyFragments.Length == 0)
				return;

			foreach (MovedImageFragment moveFrag in movedFragments)
				moveFrag.WriteToDataStream(s);

			if (dirtyFragments.Length > 0)
			{
				if (dirtyFragments[0].screenshot.BufferIsCompressed)
				{
					foreach (DirtyImageFragment dirtyFrag in dirtyFragments)
						dirtyFrag.WriteToDataStream(s, null, ref compressToBuffer);
				}
				else
				{
					using (turbojpegCLI.TJCompressor compressor = new turbojpegCLI.TJCompressor())
					{
						compressor.setSubsamp(subsamp);
						compressor.setJPEGQuality(jpegQuality);

						int requiredBufferSize = 0;
						foreach (DirtyImageFragment dirtyFrag in dirtyFragments)
						{
							int thisBufferSize = turbojpegCLI.TJ.bufSize(dirtyFrag.screenshot.Width, dirtyFrag.screenshot.Height, subsamp);
							requiredBufferSize = Math.Max(requiredBufferSize, thisBufferSize);
						}
						if (compressToBuffer == null || compressToBuffer.Length < requiredBufferSize)
							compressToBuffer = new byte[requiredBufferSize];

						foreach (DirtyImageFragment dirtyFrag in dirtyFragments)
							dirtyFrag.WriteToDataStream(s, compressor, ref compressToBuffer);
					}
				}

			}
		}
		/// <summary>
		/// The serialized form of this object should fit within the returned buffer size.
		/// </summary>
		/// <param name="jpegQuality"></param>
		/// <param name="subsamp"></param>
		/// <returns></returns>
		public int GetMaximumRequiredBufferSize(int jpegQuality = 80, turbojpegCLI.SubsamplingOption subsamp = SubsamplingOption.SAMP_420)
		{
			// Calculate maximum size of buffer required to hold the entire command and all its data, starting from the initial command code
			int maxCommandSize = 1 + 1 + 2 + 2 + (13 * movedFragments.Length) + (13 * dirtyFragments.Length);
			foreach (DirtyImageFragment dirtyFrag in dirtyFragments)
			{
				if (dirtyFrag.screenshot.BufferIsCompressed)
					maxCommandSize += dirtyFrag.screenshot.Buffer.Length;
				else
					maxCommandSize += turbojpegCLI.TJ.bufSize(dirtyFrag.screenshot.Width, dirtyFrag.screenshot.Height, subsamp);
			}
			return maxCommandSize;
		}
	}
	public abstract class ImageFragment
	{
		public Rectangle bounds;
	}
	public class DirtyImageFragment : ImageFragment
	{
		public Screenshot screenshot;

		/// <summary>
		/// Reads a DirtyImageFragment from the stream, assuming it is already in compressed format.
		/// </summary>
		/// <param name="s"></param>
		public DirtyImageFragment(IDataStream s)
		{
			bounds.X = s.ReadInt16();
			bounds.Y = s.ReadInt16();
			bounds.Width = s.ReadUInt16();
			bounds.Height = s.ReadUInt16();
			int imgLength = s.ReadInt32();
			screenshot = new Screenshot(bounds.Width, bounds.Height, 32, new byte[imgLength], bufferIsCompressed: true);
			s.Read(screenshot.Buffer, 0, imgLength);
		}

		/// <summary>
		/// Creates a DirtyImageFragment with manual specifications.
		/// </summary>
		/// <param name="top"></param>
		/// <param name="right"></param>
		/// <param name="bottom"></param>
		/// <param name="left"></param>
		/// <param name="screenshot"></param>
		public DirtyImageFragment(int top, int right, int bottom, int left, Screenshot screenshot)
		{
			bounds.X = left;
			bounds.Y = top;
			bounds.Width = right - left;
			bounds.Height = bottom - top;
			this.screenshot = screenshot;
		}

		/// <summary>
		/// Writes the DirtyImageFragment to the data stream in compressed format.
		/// </summary>
		/// <param name="s">The stream to write the frame to.</param>
		/// <param name="compressor">A TJCompressor instance that is preconfigured with quality and subsampling options.  Can be null if the image is already compressed.</param>
		/// <param name="compressToBuffer">The buffer to compress to, not used if the image is already compressed.</param>
		public void WriteToDataStream(IDataStream s, TJCompressor compressor, ref byte[] compressToBuffer)
		{
			s.WriteInt16((short)bounds.X);
			s.WriteInt16((short)bounds.Y);
			s.WriteUInt16((ushort)bounds.Width);
			s.WriteUInt16((ushort)bounds.Height);

			if (screenshot.BufferIsCompressed)
			{
				s.WriteInt32(screenshot.Buffer.Length); // Write length of image
				s.Write(screenshot.Buffer, 0, screenshot.Buffer.Length); // Write image
			}
			else
			{
				turbojpegCLI.PixelFormat pixelFormat = screenshot.BitsPerPixel == 24 ? turbojpegCLI.PixelFormat.BGR : turbojpegCLI.PixelFormat.BGRX;
				compressor.setSourceImage(screenshot.Buffer, 0, 0, screenshot.Width, screenshot.Stride, screenshot.Height, pixelFormat);
				compressor.compress(ref compressToBuffer, turbojpegCLI.Flag.NONE);
				int compressedSize = compressor.getCompressedSize();
				s.WriteInt32(compressedSize); // Write length of image
				s.Write(compressToBuffer, 0, compressedSize); // Write image
			}
		}
	}
	public class MovedImageFragment : ImageFragment
	{
		public Point source;

		public MovedImageFragment(IDataStream s)
		{
			bounds.X = s.ReadInt16();
			bounds.Y = s.ReadInt16();
			bounds.Width = s.ReadUInt16();
			bounds.Height = s.ReadUInt16();
			source.X = s.ReadInt16();
			source.Y = s.ReadInt16();
		}
		/// <summary>
		/// Writes the MovedImageFragment to the data stream.
		/// </summary>
		/// <param name="s"></param>
		public void WriteToDataStream(IDataStream s)
		{
			s.WriteInt16((short)bounds.X);
			s.WriteInt16((short)bounds.Y);
			s.WriteUInt16((ushort)bounds.Width);
			s.WriteUInt16((ushort)bounds.Height);
			s.WriteInt16((short)source.X);
			s.WriteInt16((short)source.Y);
		}

		public MovedImageFragment(int top, int right, int bottom, int left, int x, int y)
		{
			bounds.X = left;
			bounds.Y = top;
			bounds.Width = right - left;
			bounds.Height = bottom - top;
			source.X = x;
			source.Y = y;
		}
	}
}
