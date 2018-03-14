using SelfHostedRemoteDesktop.NetCommand;

namespace SelfHostedRemoteDesktop.Streamer
{
	public class DesktopCaptureTask
	{
		public ImgFlags imgFlags;
		public byte jpegQuality;

		public DesktopCaptureTask(ImgFlags imgFlags, byte jpegQuality)
		{
			this.imgFlags = imgFlags;
			this.jpegQuality = jpegQuality;
		}
	}
}