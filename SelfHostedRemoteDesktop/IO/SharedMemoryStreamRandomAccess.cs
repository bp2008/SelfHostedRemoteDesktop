using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop
{
	/// <summary>
	/// A stream which can be shared with one other process.  This is a two-way stream (similar to a Network Stream), such that data you write to the stream can be read by the other process, and reading from the stream allows you to read what the other process has written.
	/// This class is not thread-safe. You should only access a SharedMemoryStream instance from one thread at a time.
	/// </summary>
	public class SharedMemoryStream : Stream, IDisposable
	{
		// TODO: Performance is severely lacking.  Try to make this class use less CPU time.
		private bool isDisposed = false;
		private MemoryMappedFile _mmf;
		private EventWaitHandle _ewh_read;
		private EventWaitHandle _ewh_write;
		private MemoryMappedViewAccessor viewAccessor;
		private ulong myBytesRead = 0;
		private ulong myBytesWritten = 0;
		private bool isOwnerProcess = false;
		private const long metadataStartsAt = 0;
		private long myIncomingStreamStartsAt;
		private long myOutgoingStreamStartsAt;
		/// <summary>
		/// The size of the send and receive buffers.  Memory usage of this object will be roughly 2x this number.
		/// </summary>
		private const int bufferSize = 4 * 1000;
		private const int metadataSize = 32;
		private const int memoryMappedFileSize = metadataSize + (2 * bufferSize);

		public override bool CanRead { get { return true; } }

		public override bool CanSeek { get { return false; } }

		public override bool CanWrite { get { return true; } }

		public override long Length { get { throw new NotSupportedException(); } }

		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

		/// <summary>
		/// Creates a new SharedMemory instance.
		/// </summary>
		/// <param name="isOwnerProcess">Pass true only if this is the process which should own the shared memory.  E.g. the one which starts first and lives longer than all other related processes.  The owner process must prepare the shared memory before spawning other processes which will use the shared memory, and ideally keep the shared memory alive until other processes are closed.</param>
		/// <param name="uniqueId">An ID which is unique to this application.</param>
		private SharedMemoryStream(bool isOwnerProcess, string uniqueId)
		{
			this.isOwnerProcess = isOwnerProcess;
			string mmf_uniqueId = "Global\\SHRD.SMS.MMF." + uniqueId;
			string ewh1_uniqueId = "Global\\SHRD.SMS.EWH1." + uniqueId;
			string ewh2_uniqueId = "Global\\SHRD.SMS.EWH2." + uniqueId;
			if (isOwnerProcess)
			{
				// Set up security objects
				SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
				SecurityIdentifier localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

				MemoryMappedFileSecurity mmf_security = new MemoryMappedFileSecurity();
				mmf_security.AddAccessRule(new AccessRule<MemoryMappedFileRights>(users, MemoryMappedFileRights.FullControl, AccessControlType.Allow));
				mmf_security.AddAccessRule(new AccessRule<MemoryMappedFileRights>(localSystem, MemoryMappedFileRights.FullControl, AccessControlType.Allow));

				var ewh_security = new EventWaitHandleSecurity();
				ewh_security.AddAccessRule(new EventWaitHandleAccessRule(users, EventWaitHandleRights.FullControl, AccessControlType.Allow));
				ewh_security.AddAccessRule(new EventWaitHandleAccessRule(localSystem, EventWaitHandleRights.FullControl, AccessControlType.Allow));

				// Set up Shared Memory
				_mmf = MemoryMappedFile.CreateOrOpen(mmf_uniqueId, memoryMappedFileSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.DelayAllocatePages, mmf_security, HandleInheritability.Inheritable);
				viewAccessor = _mmf.CreateViewAccessor(0, memoryMappedFileSize);

				// Set up EventWaitHandles
				bool createdNew;
				if (!EventWaitHandle.TryOpenExisting(ewh1_uniqueId, out _ewh_read))
					_ewh_read = new EventWaitHandle(false, EventResetMode.ManualReset, ewh1_uniqueId, out createdNew, ewh_security);
				_ewh_read.Set();
				if (!EventWaitHandle.TryOpenExisting(ewh2_uniqueId, out _ewh_write))
					_ewh_write = new EventWaitHandle(false, EventResetMode.ManualReset, ewh2_uniqueId, out createdNew, ewh_security);
				_ewh_write.Set();
				myIncomingStreamStartsAt = metadataSize;
				myOutgoingStreamStartsAt = metadataSize + bufferSize;
			}
			else
			{
				// Set up Shared Memory
				_mmf = MemoryMappedFile.OpenExisting(mmf_uniqueId, MemoryMappedFileRights.ReadWrite);
				viewAccessor = _mmf.CreateViewAccessor(0, memoryMappedFileSize);
				// This is the "slave process" so the read and write handles are reversed.
				_ewh_write = EventWaitHandle.OpenExisting(ewh1_uniqueId);
				_ewh_read = EventWaitHandle.OpenExisting(ewh2_uniqueId);
				myOutgoingStreamStartsAt = metadataSize;
				myIncomingStreamStartsAt = metadataSize + bufferSize;
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
		public static SharedMemoryStream OpenSharedMemoryStream(string uniqueId)
		{
			SharedMemoryStream sms = new SharedMemoryStream(false, uniqueId);
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
			Util.TryCatch(() => { viewAccessor?.Dispose(); });
			Util.TryCatch(() => { _mmf?.Dispose(); });
			Util.TryCatch(() => { _ewh_read?.Set(); });
			Util.TryCatch(() => { _ewh_read?.Dispose(); });
			Util.TryCatch(() => { _ewh_write?.Set(); });
			Util.TryCatch(() => { _ewh_write?.Dispose(); });
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
			WaitProgressivelyLonger waiter = new WaitProgressivelyLonger();
			do
			{
				ulong bytesWrittenByRemote = GetBytesWrittenByRemote();
				int bytesAvailableToRead = (int)(bytesWrittenByRemote - myBytesRead);
				int currentPositionInMyReadBuffer = (int)(myBytesRead % bufferSize);
				int availableBeforeEndOfBuffer = bufferSize - currentPositionInMyReadBuffer;
				int amountToRead = Math.Min(bytesAvailableToRead, Math.Min(count - totalAmountRead, availableBeforeEndOfBuffer));
				if (amountToRead > 0)
				{
					waiter.Reset();
					long actualReadPosition = myIncomingStreamStartsAt + currentPositionInMyReadBuffer;
					try
					{
						int amountRead = viewAccessor.ReadArray(actualReadPosition, buffer, offset + totalAmountRead, amountToRead);
						myBytesRead += (ulong)amountRead;
						totalAmountRead += amountRead;
						if (amountRead != amountToRead)
							throw new Exception("Read " + amountRead + " from viewAccessor, but meant to read " + amountToRead + ". Possible logic error.");
					}
					finally
					{
						SetBytesReadByMe();
					}
				}
				else
					waiter.Wait();
			}
			while (totalAmountRead < count);
			if (totalAmountRead != count)
				throw new Exception("Total amount read from viewAccessor (" + totalAmountRead + ") did not match count (" + count + "). Possible logic error.");
			return count;
		}

		/// <summary>
		/// Writes the specified amount of data to the outbound stream, not returning until all data has been placed in the underlying memory-mapped file. This method typically returns before the remote reader has finished reading the written data.
		/// </summary>
		/// <param name="buffer">The buffer containing data to write.</param>
		/// <param name="offset">The offset within the buffer at which to begin copying data.</param>
		/// <param name="count">The number of bytes to write.  Must be less than or equal to the length of the buffer minus the offset.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
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
			
			int totalAmountWritten = 0;
			WaitProgressivelyLonger waiter = new WaitProgressivelyLonger();
			do
			{
				ulong bytesReadByRemote = GetBytesReadByRemote();
				int bufferSpaceAlreadyConsumed = (int)(myBytesWritten - bytesReadByRemote);
				int bufferSpaceAvailable = bufferSize - bufferSpaceAlreadyConsumed;
				int currentPositionInMyWriteBuffer = (int)(myBytesWritten % bufferSize);
				int availableBeforeEndOfBuffer = bufferSize - currentPositionInMyWriteBuffer;
				int amountToWrite = Math.Min(bufferSpaceAvailable, Math.Min(count - totalAmountWritten, availableBeforeEndOfBuffer));
				if (amountToWrite > 0)
				{
					waiter.Reset();
					long actualWritePosition = myOutgoingStreamStartsAt + currentPositionInMyWriteBuffer;
					try
					{
						viewAccessor.WriteArray(actualWritePosition, buffer, offset + totalAmountWritten, amountToWrite);
						myBytesWritten += (ulong)amountToWrite;
						totalAmountWritten += amountToWrite;
					}
					finally
					{
						SetBytesWrittenByMe();
					}
				}
				else
					waiter.Wait();
			}
			while (totalAmountWritten < count);
			if (totalAmountWritten != count)
				throw new Exception("Total amount written to viewAccessor (" + totalAmountWritten + ") did not match count (" + count + "). Possible logic error.");
		}

		public override int ReadByte()
		{
			return base.ReadByte();
		}
		public override void WriteByte(byte value)
		{
			base.WriteByte(value);
		}

		#region Shared Metadata / Getting and Setting the number of bytes read and written by each process

		// Metadata section is 32 bytes long
		// [0-7] (uint) Owner Amount Read
		// [8-15] (uint) Owner Amount Written
		// [16-23] (uint) Slave Amount Read
		// [24-31] (uint) Slave Amount Written

		/// <summary>
		/// Gets the total number of bytes written by the remote side of the SharedMemoryStream.
		/// </summary>
		/// <returns></returns>
		private ulong GetBytesWrittenByRemote()
		{
			_ewh_read.WaitOne();
			try
			{
				return viewAccessor.ReadUInt64(isOwnerProcess ? 24 : 8);
			}
			finally
			{
				_ewh_read.Set();
			}
		}
		/// <summary>
		/// Sets the total number of bytes written by the local side of the SharedMemoryStream. Calling after writing data allows the remote side to access what you have written.
		/// </summary>
		private void SetBytesWrittenByMe()
		{
			_ewh_write.WaitOne();
			try
			{
				viewAccessor.Write(isOwnerProcess ? 8 : 24, myBytesWritten);
			}
			finally
			{
				_ewh_write.Set();
			}
		}
		/// <summary>
		/// Gets the total number of bytes read by the remote side of the SharedMemoryStream.
		/// </summary>
		/// <returns></returns>
		private ulong GetBytesReadByRemote()
		{
			_ewh_write.WaitOne();
			try
			{
				return viewAccessor.ReadUInt64(isOwnerProcess ? 16 : 0);
			}
			finally
			{
				_ewh_write.Set();
			}
		}

		/// <summary>
		/// Sets the total number of bytes read by the local side of the SharedMemoryStream. Calling after reading data allows the remote side to write more data.
		/// </summary>
		private void SetBytesReadByMe()
		{
			_ewh_read.WaitOne();
			try
			{
				viewAccessor.Write(isOwnerProcess ? 0 : 16, myBytesRead);
			}
			finally
			{
				_ewh_read.Set();
			}
		}
		#endregion

		#region Junk
		public override void Flush()
		{
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
}
