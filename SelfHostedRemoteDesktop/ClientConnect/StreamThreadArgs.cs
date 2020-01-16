using SHRDLib;
using SHRDLib.NetCommand;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SelfHostedRemoteDesktop.ClientConnect
{
	public class StreamThreadArgs
	{
		public AbortFlag abortFlag = new AbortFlag();
		public long myStreamNumber = 0;
		public long numSentFrames = 0;
		public long numAcknowledgedFrames = 0;
		public StreamType streamType;
		public byte displayIdx;

		public StreamThreadArgs(long myStreamNumber)
		{
			this.myStreamNumber = myStreamNumber;
		}
	}
}
