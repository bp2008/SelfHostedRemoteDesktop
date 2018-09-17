import * as Util from 'appRoot/scripts/Util.js';
import JpegFragmentedVideoPlayer from 'appRoot/scripts/JpegFragmentedVideoPlayer.js';

export default function DesktopVideoPlayer()
{
	var self = this;
	var playerModule = new JpegFragmentedVideoPlayer();
	var isPlaying = false;
	this.VisibilityChanged = function (isVisible)
	{
		if (isVisible && !isPlaying)
			self.Play();
		else if (!isVisible && isPlaying)
			self.Stop();
	};
	this.SocketOpen = function ()
	{
		self.Play();
	};
	this.SocketClose = function ()
	{
		isPlaying = false;
		playerModule.ConnectionLost();
		toaster.Warning("WebSocket closed.  Attempting to reconnect.");
		webSocketStreamer = new WebSocketStreamer();
	};
	this.Play = function ()
	{
		if (documentIsHidden())
			return;
		isPlaying = true;
		playerModule.Play();
	};
	this.Stop = function ()
	{
		isPlaying = false;
		playerModule.Stop();
	};
	this.NewFrame = function (buf)
	{
		playerModule.NewFrame(buf);
	};
}