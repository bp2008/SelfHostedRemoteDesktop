using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;

namespace SHRDLib
{
	/// <summary>
	/// Runs a background thread with the purpose of sending keepalive packets to prevent a socket from disconnecting due to inactivity.
	/// </summary>
	public class KeepAliveSender : IDisposable
	{
		private bool stopped = false;

		private Action<KeepAliveSender> keepalive;
		private Action<KeepAliveSender> onStop;

		private long keepaliveIntervalMs;
		private long nextKeepaliveMs;

		private Thread keepAliveThread;

		private Stopwatch keepAliveTimer;

		/// <summary>
		/// Runs a background thread with the purpose of sending keepalive packets to prevent a socket from disconnecting due to inactivity.
		/// </summary>
		/// <param name="threadName">A name for the background thread so it is identifiable in debugging tools.</param>
		/// <param name="keepaliveIntervalMs">The interval for sending keepalive packets.  Should be significantly shorter than the socket timeout.</param>
		/// <param name="keepalive">A function to call when it is time to send a keepalive packet.</param>
		/// <param name="onStop">A function to call when this KeepAliveSender stops.  [onStop] may be called more than once if Stop is called more than once.</param>
		public KeepAliveSender(string threadName, int keepaliveIntervalMs, Action<KeepAliveSender> keepalive, Action<KeepAliveSender> onStop)
		{
			this.keepalive = keepalive;
			this.onStop = onStop;

			this.keepaliveIntervalMs = keepaliveIntervalMs;
			nextKeepaliveMs = keepaliveIntervalMs;

			keepAliveTimer = new Stopwatch();
			keepAliveTimer.Start();

			keepAliveThread = new Thread(KeepAliveLoop);
			keepAliveThread.IsBackground = true;
			keepAliveThread.Name = threadName;
			keepAliveThread.Start();
		}
		#region IDisposable Support
		private bool disposedValue = false;
		/// <summary>
		/// Stops this KeepAliveSender instance if it is not already stopped.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					Stop();
				}
				disposedValue = true;
			}
		}
		/// <summary>
		/// Calls Stop()
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion
		/// <summary>
		/// Returns true if a keepalive needs to be sent now.
		/// </summary>
		/// <returns></returns>
		public bool IsTimeToKeepalive()
		{
			return keepAliveTimer.ElapsedMilliseconds >= nextKeepaliveMs;
		}
		/// <summary>
		/// Informs this instance that a keepalive packet will not be necessary for an interval.
		/// </summary>
		public void NotifyPacketSending()
		{
			nextKeepaliveMs = keepAliveTimer.ElapsedMilliseconds + keepaliveIntervalMs;
		}
		/// <summary>
		/// If this instance has not already stopped, stops the background thread and causes [onStop] to be called.
		/// </summary>
		public void Stop()
		{
			if (stopped)
				return;
			stopped = true;
			Try.Catch_RethrowThreadAbort(keepAliveThread.Abort);
			try
			{
				onStop(this);
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex, "Exception thrown when calling onStop action");
			}
		}
		private void KeepAliveLoop()
		{
			try
			{
				// Send KeepAlive packets every 120 seconds if no other packets have been sent.
				while (!stopped)
				{
					if (IsTimeToKeepalive())
						try
						{
							keepalive(this);
						}
						catch (ThreadAbortException) { throw; }
						catch (Exception ex)
						{
							Logger.Debug(ex, "Exception thrown when calling keepalive action");
						}
					Thread.Sleep(1000);
				}
			}
			catch (ThreadAbortException) { }
			catch (EndOfStreamException) { } // ordinary socket disconnect
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			finally
			{
				Stop();
			}
		}
	}
}
