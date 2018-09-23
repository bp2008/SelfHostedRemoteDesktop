export default function FullscreenController(onFullscreenChange)
{
	let self = this;
	let fullscreen_supported = ((document.documentElement.requestFullscreen || document.documentElement.msRequestFullscreen || document.documentElement.mozRequestFullScreen || document.documentElement.webkitRequestFullscreen) && (document.exitFullscreen || document.msExitFullscreen || document.mozCancelFullScreen || document.webkitExitFullscreen)) ? true : false;

	let fsEvents = "webkitfullscreenchange mozfullscreenchange fullscreenchange MSFullscreenChange";

	Util.AddEvents(document, fsEvents, fullscreenChange);

	this.Dispose = function ()
	{
		Util.RemoveEvents(document, fsEvents, fullscreenChange);
	};

	function fullscreenChange(e)
	{
		if (typeof onFullscreenChange === "function")
			onFullscreenChange(e);
	}

	this.toggleFullScreen = function ()
	{
		if (fullscreen_supported)
		{
			if (!self.isFullScreen())
				requestFullScreen();
			else
				exitFullScreen();
		}
		else
		{
			BI_Hotkey_MaximizeVideoArea();
		}
	};
	this.isFullScreen = function ()
	{
		return (document.fullscreenElement || document.mozFullScreenElement || document.webkitFullscreenElement || document.msFullscreenElement) ? true : false;
	};
	let requestFullScreen = function ()
	{
		if (document.documentElement.requestFullscreen)
			document.documentElement.requestFullscreen();
		else if (document.documentElement.msRequestFullscreen)
			document.documentElement.msRequestFullscreen();
		else if (document.documentElement.mozRequestFullScreen)
			document.documentElement.mozRequestFullScreen();
		else if (document.documentElement.webkitRequestFullscreen)
			document.documentElement.webkitRequestFullscreen(Element.ALLOW_KEYBOARD_INPUT);
	};
	let exitFullScreen = function ()
	{
		if (document.exitFullscreen)
			document.exitFullscreen();
		else if (document.msExitFullscreen)
			document.msExitFullscreen();
		else if (document.mozCancelFullScreen)
			document.mozCancelFullScreen();
		else if (document.webkitExitFullscreen)
			document.webkitExitFullscreen();
	};
}