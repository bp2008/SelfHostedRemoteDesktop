import * as Util from 'appRoot/scripts/Util.js';
import JpegFragmentedVideoRenderer from 'appRoot/scripts/remote/JpegFragmentedVideoRenderer.js';

export default function DesktopVideoRenderer()
{
	var self = this;
	var renderModule = new JpegFragmentedVideoRenderer();
	//this.VisibilityChanged = function (isVisible)
	//{
	//	if (isVisible && !isPlaying)
	//		self.Play();
	//	else if (!isVisible && isPlaying)
	//		self.Stop();
	//};
	this.NewFrame = function (frame)
	{
		renderModule.NewFrame(frame);
	};
}