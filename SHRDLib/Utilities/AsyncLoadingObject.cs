using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SHRDLib.Utilities
{
	/// <summary>
	/// Helper object for StreamerController.  An AsyncLoadingObject instance should only be used to load one object at a time, or else responses can be mixed up.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class AsyncLoadingObject<T>
	{
		private T produced;
		private EventWaitHandle ewh = new EventWaitHandle(false, EventResetMode.AutoReset);
		private int canceledConsumes = 0;
		private object syncLock = new object();
		public AsyncLoadingObject()
		{
		}
		/// <summary>
		/// Called by the producer when an object is ready.
		/// </summary>
		/// <param name="obj"></param>
		public void Produce(T obj)
		{
			lock (syncLock)
			{
				if (canceledConsumes > 0)
				{
					canceledConsumes--;
					return;
				}
				produced = obj;
				ewh.Set();
			}
		}
		/// <summary>
		/// Waits for the producer to produce an object.  Returns true if the producer produces an object, or false if the AbortFlag was set before an object was produced.
		/// If aborted, then the object, when it loads, will be discarded.  This way, the AsyncLoadingObject instance can be reused without going out of sync.
		/// </summary>
		/// <param name="flag">An AbortFlag to observe.</param>
		/// <param name="obj">The produced object, valid only if the return value from this method is true.</param>
		/// <param name="timeout">The amount of time in milliseconds to wait between checking the abort flag.  Very low values may increase CPU usage due to overhead of the internal EventWaitHandle.</param>
		/// <returns></returns>
		public bool Consume(AbortFlag flag, out T obj, int timeout = 500)
		{
			bool gotObject = false;
			while (!flag.abort && (gotObject = ewh.WaitOne(timeout)) == false) ;
			if (gotObject)
			{
				lock (syncLock)
				{
					obj = produced;
					produced = default(T);
				}
				return true;
			}
			else
			{
				//We gave up on waiting for it, but API expectations guarantee that it will arrive eventually.
				// Incrementing canceledConsumes causes the next Produced object to be discarded without signaling the EventWaitHandle.  
				lock (syncLock)
				{
					canceledConsumes++;
				}
				obj = default(T);
				return false;
			}
		}
	}
}
