using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using SHRDLib;

namespace SelfHostedRemoteDesktop
{
	/// <summary>
	/// A stream which can be shared with one other process.  This is a two-way stream (similar to a Network Stream), such that data you write to the stream can be read by the other process, and reading from the stream allows you to read what the other process has written.
	/// This class is not thread-safe. You should only access a SharedMemoryStream instance from one thread at a time.
	/// </summary>
	public class SharedMemoryStream : Stream, IDisposable, IDataStream
	{
		private bool isDisposed = false;
		private NamedPipeServerStream pipeServer;
		private PipeStream pipe;
		private bool isOwnerProcess = false;
		/// <summary>
		/// (if 0, no action) The process ID of the process at the other end of the SharedMemoryStream. While waiting for a connection, this PID will be monitored and if it is ever found to not be running, the stream connection will be aborted.
		/// </summary>
		public int otherProcessPid = 0;
		/// <summary>
		/// The unique ID of this shared memory stream.
		/// </summary>
		public readonly string uniqueId;
		/// <summary>
		/// The size of the send and receive buffers.
		/// </summary>
		private const int bufferSize = 4 * 1000 * 1000;

		public override bool CanRead { get { return true; } }

		public override bool CanSeek { get { return false; } }

		public override bool CanWrite { get { return true; } }

		public override long Length { get { throw new NotSupportedException(); } }

		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

		/// <summary>
		/// Creates a new SharedMemory instance.
		/// </summary>
		/// <param name="isOwnerProcess">Pass true only if this is the process which should own the shared memory.  E.g. the one which starts first and lives longer than all other related processes.  The owner process must prepare the shared memory before spawning other processes which will use the shared memory, and ideally keep the shared memory alive until other processes are closed.</param>
		/// <param name="uniqueId">An ID which hasn't been used before by this application instance.</param>
		/// <param name="otherProcessPid">The process ID of the other process. Pass it if you know it already, otherwise pass 0 and set the [otherProcessPid] field later.</param>
		private SharedMemoryStream(bool isOwnerProcess, string uniqueId, int otherProcessPid = 0)
		{
			this.uniqueId = uniqueId;
			this.isOwnerProcess = isOwnerProcess;
			this.otherProcessPid = otherProcessPid;
			string pipe_uniqueId = "Global\\SHRD.SMS.PIPE." + uniqueId;
			if (isOwnerProcess)
			{
				// Set up security objects
				SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
				SecurityIdentifier localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

				PipeSecurity pipe_security = new PipeSecurity();
				pipe_security.AddAccessRule(new PipeAccessRule(users, PipeAccessRights.FullControl, AccessControlType.Allow));
				pipe_security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));

				// Set up Shared Memory
				pipeServer = new NamedPipeServerStream(pipe_uniqueId, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, bufferSize, bufferSize, pipe_security);
				pipe = pipeServer;
				pipeServer.BeginWaitForConnection(endWaitForConnection, null);
			}
			else
			{
				// Set up Shared Memory
				NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", pipe_uniqueId, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
				pipe = pipeClient;
				pipeClient.Connect(10000);
			}
		}

		private void endWaitForConnection(IAsyncResult ar)
		{
			try
			{
				if (ar.IsCompleted)
					pipeServer.EndWaitForConnection(ar);
			}
			catch (ObjectDisposedException ex)
			{
				if (ex.Message == "Cannot access a closed pipe.")
					return;
				throw;
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}

		/// <summary>
		/// Creates a SharedMemoryStream with the specified ID. Requires the process to be elevated.
		/// Only one process besides this one may open the SharedMemoryStream. If more than one additional process opens the stream, the behavior is undefined.
		/// </summary>
		/// <param name="uniqueId">An ID which is unique to this application.</param>
		/// <returns></returns>
		public static SharedMemoryStream CreateSharedMemoryStream(string uniqueId)
		{
			SharedMemoryStream sms = new SharedMemoryStream(true, uniqueId);
			return sms;
		}
		/// <summary>
		/// Opens an existing SharedMemoryStream created by another process.
		/// </summary>
		/// <param name="uniqueId">An ID which is unique to this application.</param>
		/// <returns></returns>
		public static SharedMemoryStream OpenSharedMemoryStream(string uniqueId, int ownerProcessId)
		{
			SharedMemoryStream sms = new SharedMemoryStream(false, uniqueId, ownerProcessId);
			return sms;
		}

		~SharedMemoryStream()
		{
			if (isDisposed)
				return;
			Dispose(false);
		}

		public new void Dispose()
		{
			if (isDisposed)
				return;
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected override void Dispose(bool disposing)
		{
			Try.Swallow(() => { pipe?.WaitForPipeDrain(); });
			Try.Catch(() => { pipe?.Dispose(); });
			Try.Catch(() => { pipeServer?.Dispose(); });
			isDisposed = true;
		}

		/// <summary>
		/// Reads the specified amount of data from the incoming stream, not returning until all the requested data is read.
		/// </summary>
		/// <param name="buffer">The buffer to store data in as it is read.</param>
		/// <param name="offset">The offset in the buffer to begin storing data.</param>
		/// <param name="count">The number of bytes to read.  Must be less than or equal to the length of the buffer minus the offset.</param>
		/// <returns>The number of bytes read. This will always be equal to count.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			WaitUntilConnected();
			if (count == 0)
				return 0;
			if (buffer == null)
				throw new ArgumentNullException("buffer");
			if (buffer.Length == 0)
				throw new ArgumentException("buffer must have size larger than 0", "buffer");
			if (count < 0 || count > buffer.Length - offset)
				throw new ArgumentOutOfRangeException("count", "Value " + count + " is out of range");
			if (offset < 0 || offset >= buffer.Length)
				throw new ArgumentOutOfRangeException("offset", "Value " + offset + " is out of range");

			int totalAmountRead = 0;
			do
			{
				int read = pipe.Read(buffer, totalAmountRead, count - totalAmountRead);
				totalAmountRead += read;
			}
			while (totalAmountRead < count);
			if (totalAmountRead != count)
				throw new Exception("Total amount read from pipe (" + totalAmountRead + ") did not match count (" + count + "). Possible logic error.");
			return count;
		}

		/// <summary>
		/// Writes the specified amount of data to the outbound stream.
		/// </summary>
		/// <param name="buffer">The buffer containing data to write.</param>
		/// <param name="offset">The offset within the buffer at which to begin copying data.</param>
		/// <param name="count">The number of bytes to write.  Must be less than or equal to the length of the buffer minus the offset.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			try
			{
				WaitUntilConnected();
				if (count == 0)
					return;
				if (buffer == null)
					throw new ArgumentNullException("buffer");
				if (buffer.Length == 0)
					throw new ArgumentException("buffer must have size larger than 0", "buffer");
				if (count < 0 || count > buffer.Length - offset)
					throw new ArgumentOutOfRangeException("count", "Value " + count + " is out of range");
				if (offset < 0 || offset >= buffer.Length)
					throw new ArgumentOutOfRangeException("offset", "Value " + offset + " is out of range");
				if (count > buffer.Length - offset)
					throw new ArgumentOutOfRangeException("count", count, "value exceeds the size of the buffer minus the offset");

				pipe.Write(buffer, 0, count);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}

		/// <summary>
		/// Writes the data to the outbound stream.
		/// </summary>
		/// <param name="buffer">The buffer containing data to write.</param>
		public void Write(byte[] buffer)
		{
			try
			{
				WaitUntilConnected();
				if (buffer == null)
					throw new ArgumentNullException("buffer");
				if (buffer.Length == 0)
					return;

				pipe.Write(buffer, 0, buffer.Length);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}

		public override int ReadByte()
		{
			try
			{
				WaitUntilConnected();
				int returnValue = pipe.ReadByte();
				if (returnValue == -1)
				{
					pipe.Dispose();
					throw new StreamDisconnectedException();
				}
				return returnValue;
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public override void WriteByte(byte value)
		{
			try
			{
				WaitUntilConnected();
				pipe.WriteByte(value);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteInt16(short num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(num)), 0, 2);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteUInt16(ushort num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)num)), 0, 2);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteInt32(int num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(num)), 0, 4);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteUInt32(uint num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes((uint)IPAddress.HostToNetworkOrder((int)num)), 0, 4);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteInt64(long num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(num)), 0, 8);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteUInt64(ulong num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(BitConverter.GetBytes((ulong)IPAddress.HostToNetworkOrder((long)num)), 0, 8);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteFloat(float num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(ByteUtil.NetworkToHostOrder(BitConverter.GetBytes(num)), 0, 4);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public void WriteDouble(double num)
		{
			try
			{
				WaitUntilConnected();
				pipe.Write(ByteUtil.NetworkToHostOrder(BitConverter.GetBytes(num)), 0, 8);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		public short ReadInt16()
		{
			return BitConverter.ToInt16(ReadNBytesFromNetworkOrder(2), 0);
		}
		public ushort ReadUInt16()
		{
			return BitConverter.ToUInt16(ReadNBytesFromNetworkOrder(2), 0);
		}
		public int ReadInt32()
		{
			return BitConverter.ToInt32(ReadNBytesFromNetworkOrder(4), 0);
		}
		public uint ReadUInt32()
		{
			return BitConverter.ToUInt32(ReadNBytesFromNetworkOrder(4), 0);
		}
		public long ReadInt64()
		{
			return BitConverter.ToInt64(ReadNBytesFromNetworkOrder(8), 0);
		}
		public ulong ReadUInt64()
		{
			return BitConverter.ToUInt64(ReadNBytesFromNetworkOrder(8), 0);
		}
		public float ReadFloat()
		{
			return BitConverter.ToSingle(ReadNBytesFromNetworkOrder(4), 0);
		}
		public double ReadDouble()
		{
			return BitConverter.ToDouble(ReadNBytesFromNetworkOrder(8), 0);
		}
		public string ReadUtf8(int lengthBytes)
		{
			return ByteUtil.ReadUtf8(ReadNBytes(lengthBytes));
		}
		/// <summary>
		/// Reads a specific number of bytes from the stream, returning a byte array.  Ordinary stream.Read operations are not guaranteed to read all the requested bytes.
		/// </summary>
		/// <param name="length">The number of bytes to read.</param>
		/// <returns></returns>
		public byte[] ReadNBytes(int length)
		{
			try
			{
				WaitUntilConnected();
				return ByteUtil.ReadNBytes(pipe, length);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}
		/// <summary>
		/// Reads a specific number of bytes from the stream and performs NetworkToHostOrder on the resulting byte array before returning it.
		/// </summary>
		/// <param name="length">The number of bytes to read.</param>
		/// <returns></returns>
		public byte[] ReadNBytesFromNetworkOrder(int length)
		{
			try
			{
				WaitUntilConnected();
				return ByteUtil.ReadNBytesFromNetworkOrder(pipe, length);
			}
			catch (IOException ex)
			{
				if (ex.Message == "Pipe is broken.")
					throw new StreamDisconnectedException();
				throw;
			}
		}

		private void WaitUntilConnected()
		{
			while (!pipe.IsConnected)
			{
				if (otherProcessPid != 0)
				{
					bool exited = false;
					try
					{
						Process p = Process.GetProcessById(otherProcessPid);
						exited = p == null || p.HasExited;
					}
					catch
					{
						exited = true;
					}
					if (exited)
						throw new StreamDisconnectedException("The other process has exited");
				}
				Thread.Sleep(10);
			}
		}

		#region Junk
		public override void Flush()
		{
			WaitUntilConnected();
			pipe.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}
		#endregion
	}
	public class StreamDisconnectedException : Exception
	{
		public StreamDisconnectedException() : base("The SharedMemoryStream has been disconnected.")
		{
		}

		public StreamDisconnectedException(string message) : base(message)
		{
		}
	}
}
