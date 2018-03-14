using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop
{
	public class Screenshot : IDisposable
	{
		private bool isDisposed = false;
		//private static ObjectPool<byte[]> bufferPool = new ObjectPool<byte[]>(() => null, 2);
		public int Width { get; private set; }
		public int Height { get; private set; }
		public int BitsPerPixel { get; private set; }
		public int Stride { get; private set; }
		public byte[] Buffer;
		public bool BufferIsCompressed = false;
		/// <summary>
		/// Create an empty Screenshot instance with a buffer of size Height * Stride.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="bitsPerPixel"></param>
		public Screenshot(int width, int height, int bitsPerPixel, byte[] buffer = null, int strideOverride = 0, bool bufferIsCompressed = false)
		{
			Width = width;
			Height = height;
			this.BufferIsCompressed = bufferIsCompressed;
			if (bufferIsCompressed)
			{
				Buffer = buffer;
			}
			else
			{
				BitsPerPixel = bitsPerPixel;
				if (bitsPerPixel != 24 && bitsPerPixel != 32)
					throw new Exception("Screenshot BitsPerPixel " + bitsPerPixel + " is unsupported");
				if (strideOverride > 0)
					Stride = strideOverride;
				else
				{
					Stride = width * bitsPerPixel;  // bits per row
					Stride += 31;                   // round up to next 32-bit boundary
					Stride /= 32;                   // DWORDs per row (1 DWORD = 4 bytes = 32 bits)
					Stride *= 4;                    // bytes per row
				}
				int requiredBufferSize = Height * Stride;
				//if (buffer != null && buffer.Length == requiredBufferSize)
				//	Buffer = buffer;
				//else
				//{
				//	int attempts = 0;
				//	do
				//	{
				//		Buffer = bufferPool.GetObject(() => new byte[requiredBufferSize]);
				//	}
				//	while (Buffer.Length != requiredBufferSize && ++attempts < 5);
				//	if (Buffer.Length != requiredBufferSize)
				Buffer = new byte[requiredBufferSize];
				//}
			}
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
			//bufferPool.PutObject(Buffer);
			Buffer = null;
			isDisposed = true;
		}
	}
}
