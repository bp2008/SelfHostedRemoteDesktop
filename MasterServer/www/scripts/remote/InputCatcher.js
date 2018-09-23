export default function InputCatcher(canvas)
{
	var self = this;
	var initialized = false;
	var $videoFrame = $("#videoFrame");

	var Initialize = function ()
	{
		if (initialized)
			return;
		initialized = true;

		document.addEventListener('keydown', onKeyDown);
		document.addEventListener('keyup', onKeyUp);
		document.addEventListener('keypress', swallowEvent);
		canvas.addEventListener('mousemove', onMouseMove);
		canvas.addEventListener('mousedown', onMouseDown);
		canvas.addEventListener('mouseup', onMouseUp);
		canvas.addEventListener('click', swallowEvent);
		canvas.addEventListener('contextmenu', swallowEvent);
		canvas.addEventListener('mousewheel', onMouseWheel);
	};
	this.Dispose = function ()
	{

		document.removeEventListener('keydown', onKeyDown);
		document.removeEventListener('keyup', onKeyUp);
		document.removeEventListener('keypress', swallowEvent);
		canvas.removeEventListener('mousemove', onMouseMove);
		canvas.removeEventListener('mousedown', onMouseDown);
		canvas.removeEventListener('mouseup', onMouseUp);
		canvas.removeEventListener('click', swallowEvent);
		canvas.removeEventListener('contextmenu', swallowEvent);
		canvas.removeEventListener('mousewheel', onMouseWheel);
	};
	var onKeyDown = function (e)
	{
		e.preventDefault();
		var keyInfo = AnalyzeKeyEvent(e);
		webSocketStreamer.reproduceKeyAction(true, keyInfo.windowsCode, keyInfo.modifiers);
		return false;
	};
	var onKeyUp = function (e)
	{
		e.preventDefault();
		var keyInfo = AnalyzeKeyEvent(e);
		webSocketStreamer.reproduceKeyAction(false, keyInfo.windowsCode, keyInfo.modifiers);
		return false;
	};
	var AnalyzeKeyEvent = function (e)
	{
		var result = { windowsCode: JsKeycodeToWinKeycode(e.keyCode), modifiers: 0 };
		if (e.ctrlKey)
			result.modifiers += ModifierKeys.Ctrl;
		if (e.shiftKey)
			result.modifiers += ModifierKeys.Shift;
		if (e.altKey)
			result.modifiers += ModifierKeys.Alt;
		if (e.metaKey)
			result.modifiers += ModifierKeys.Windows;
		return result;
	};
	var swallowEvent = function (e)
	{
		e.stopPropagation();
		e.preventDefault();
	};
	var onMouseMove = function (e)
	{
		var offset = $videoFrame.offset();
		var x = e.pageX - offset.left;
		var y = e.pageY - offset.top;

		if (webSocketStreamer.currentDesktopInfo && webSocketStreamer.currentDesktopInfo.screens.length > 0)
		{
			var screen = webSocketStreamer.currentDesktopInfo.screens[0];
			x = x - screen.X;
			x = (x / (screen.Width - 1)) * 65535;
			y = y - screen.Y;
			y = (y / (screen.Height - 1)) * 65535;
		}
		else
			return;
		webSocketStreamer.reproduceMouseMoveAction(x, y);
	};
	var onMouseDown = function (e)
	{
		e.preventDefault();
		var winButton = jsMouseButtonToWindowsMouseButton(e.which);
		webSocketStreamer.reproduceMouseButtonAction(true, winButton);
		return false;
	};
	var onMouseUp = function (e)
	{
		e.preventDefault();
		var winButton = jsMouseButtonToWindowsMouseButton(e.which);
		webSocketStreamer.reproduceMouseButtonAction(false, winButton);
		return false;
	};
	var jsMouseButtonToWindowsMouseButton = function (jsButton)
	{
		switch (jsButton)
		{
			case 1: return MouseButton.Left;
			case 2: return MouseButton.Middle;
			case 3: return MouseButton.Right;
			default: return MouseButton.None;
		}
	};
	var onMouseWheel = function (e)
	{
		e.preventDefault();
		console.log("Mouse wheel (x, y, f)", e.deltaX, e.deltaY, e.deltaFactor);
		if (e.deltaY > 0)
			webSocketStreamer.reproduceMouseWheelAction(0, 1);
		else if (e.deltaY < 0)
			webSocketStreamer.reproduceMouseWheelAction(0, -1);
	};

	Initialize();
}

const MouseButton =
{
	Left: 0
	, Right: 1
	, Middle: 2
	, Back: 3
	, Forward: 4
	, None: 255
};
const ModifierKeys =
{
	None: 0
	, LeftCtrl: 1
	, RightCtrl: 1 << 1
	, LeftShift: 1 << 2
	, RightShift: 1 << 3
	, LeftAlt: 1 << 4
	, RightAlt: 1 << 5
	, LeftWindows: 1 << 6 // Currently Disabled as web browsers don't report this modifier state
	, RightWindows: 1 << 7 // ^^
	, CapsLock: 1 << 8 // ^^
	, NumLock: 1 << 9 // ^^
	, ScrollLock: 1 << 10 // ^^
};
ModifierKeys.Ctrl = ModifierKeys.LeftCtrl | ModifierKeys.RightCtrl;
ModifierKeys.Shift = ModifierKeys.LeftShift | ModifierKeys.RightShift;
ModifierKeys.Alt = ModifierKeys.LeftAlt | ModifierKeys.RightAlt;
ModifierKeys.Windows = ModifierKeys.LeftWindows | ModifierKeys.RightWindows;