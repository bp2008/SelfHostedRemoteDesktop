/// <reference path="jquery-3.1.1.js" />
/// <reference path="jquery.ba-bbq.min.js" />
/// <reference path="toastr.min.js" />
/// <reference path="shared.js" />
"use strict";
///////////////////////////////////////////////////////////////
// Initialization /////////////////////////////////////////////
///////////////////////////////////////////////////////////////
var player = null;
var webSocketStreamer = null;
var mainMenu = null;
var inputCatcher = null;
$(function ()
{
	var compatResponse = CompatibilityTest();
	if (compatResponse)
	{
		alert("Your browser failed a compatibility check and this page will not load.\n\n" + compatResponse);
		return;
	}
	clientSettings = GetNamespaceLocalStorage(clientId);

	mainMenu = new MainMenu();

	player = new DesktopVideoPlayer();

	webSocketStreamer = new WebSocketStreamer();

	inputCatcher = new InputCatcher();

	SHRD_CustomEvent.AddListener("visibilityChange", visibilityChanged); // TODO: The browser does not yet call this custom event when visibility changes.

	$(window).resize(resized);
	resized();

});
///////////////////////////////////////////////////////////////
// Visibility Change Handler //////////////////////////////////
///////////////////////////////////////////////////////////////
function visibilityChanged(isVisible)
{
	player.VisibilityChanged(isVisible);
}
///////////////////////////////////////////////////////////////
// Window Resized /////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function resized()
{
	var wndW = $(window).width();
	var wndH = $(window).height();

	var $layout_top = $("#layout_top");
	var $layout_main = $("#layout_main");
	var $videoFrame = $("#videoFrame");

	var ltH = $layout_top.height();

	$layout_main.css("width", wndW + "px");
	$layout_main.css("height", (wndH - ltH) + "px");

	$layout_main.css("width", wndW + "px");
	$layout_main.css("height", (wndH - ltH) + "px");
}
///////////////////////////////////////////////////////////////
// CompatibilityTest //////////////////////////////////////////
///////////////////////////////////////////////////////////////
function CompatibilityTest()
{
	if (typeof WebSocket !== "function")
		return "Your browser does not support web sockets.";
	if (typeof Storage !== "function")
		return "Your browser does not support Local Storage.";
	if (typeof localStorage !== "object")
		return "Unable to access Local Storage.  Maybe it is disabled in your browser?";
	return null;
}
///////////////////////////////////////////////////////////////
// Input Catching /////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function InputCatcher()
{
	var self = this;
	var initialized = false;
	var $videoFrame = $("#videoFrame");
	var $videoCanvas = $("#myCanvas");

	var MouseButton =
		{
			Left: 0
			, Right: 1
			, Middle: 2
			, Back: 3
			, Forward: 4
			, None: 255
		};
	var ModifierKeys =
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

	var Initialize = function ()
	{
		if (initialized)
			return;
		initialized = true;

		var $document = $(document);
		$document.on('keydown', onKeyDown);
		$document.on('keyup', onKeyUp);
		$document.on('keypress', swallowEvent);
		$videoCanvas.on('mousemove', onMouseMove);
		$videoCanvas.on('mousedown', onMouseDown);
		$videoCanvas.on('mouseup', onMouseUp);
		$videoCanvas.on('click', swallowEvent);
		$videoCanvas.on('contextmenu', swallowEvent);
		$videoCanvas.on('mousewheel', onMouseWheel);
	}
	var onKeyDown = function (e)
	{
		e.preventDefault();
		var keyInfo = AnalyzeKeyEvent(e);
		webSocketStreamer.reproduceKeyAction(true, keyInfo.windowsCode, keyInfo.modifiers);
		return false;
	}
	var onKeyUp = function (e)
	{
		e.preventDefault();
		var keyInfo = AnalyzeKeyEvent(e);
		webSocketStreamer.reproduceKeyAction(false, keyInfo.windowsCode, keyInfo.modifiers);
		return false;
	}
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
	}
	var swallowEvent = function (e)
	{
		e.stopPropagation();
		e.preventDefault();
	}
	var onMouseMove = function (e)
	{
		var offset = $videoFrame.offset();
		var x = e.pageX - offset.left;
		var y = e.pageY - offset.top;

		if (webSocketStreamer.currentDesktopInfo && webSocketStreamer.currentDesktopInfo.screens.length > 0)
		{
			var screen = webSocketStreamer.currentDesktopInfo.screens[0];
			x = x - screen.X;
			x = (x / (screen.Width-1)) * 65535;
			y = y - screen.Y;
			y = (y / (screen.Height-1)) * 65535;
		}
		else
			return;
		webSocketStreamer.reproduceMouseMoveAction(x, y);
	}
	var onMouseDown = function (e)
	{
		e.preventDefault();
		var winButton = jsMouseButtonToWindowsMouseButton(e.which);
		webSocketStreamer.reproduceMouseButtonAction(true, winButton);
		return false;
	}
	var onMouseUp = function (e)
	{
		e.preventDefault();
		var winButton = jsMouseButtonToWindowsMouseButton(e.which);
		webSocketStreamer.reproduceMouseButtonAction(false, winButton);
		return false;
	}
	var jsMouseButtonToWindowsMouseButton = function (jsButton)
	{
		switch (jsButton)
		{
			case 1: return MouseButton.Left;
			case 2: return MouseButton.Middle;
			case 3: return MouseButton.Right;
			default: return MouseButton.None;
		}
	}
	var onMouseWheel = function (e)
	{
		e.preventDefault();
		console.log("Mouse wheel (x, y, f)", e.deltaX, e.deltaY, e.deltaFactor);
		if (e.deltaY > 0)
			webSocketStreamer.reproduceMouseWheelAction(0, 1);
		else if (e.deltaY < 0)
			webSocketStreamer.reproduceMouseWheelAction(0, -1);
	}

	Initialize();
}
///////////////////////////////////////////////////////////////
// Main Menu //////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function MainMenu()
{
	// TODO: Implement a better FPS counter which updates several times per second even if no frames are coming in, averaging the last 1 second or so's frames.
	var self = this;
	this.frameCounter = 0;
	this.framesThisSecond = 0;
	this.bytesThisSecond = 0;
	var qualityDialog = null;

	var InitializeMainMenu = function ()
	{
		InitializeQualityButton();
		setInterval(function ()
		{
			$("#fps").html(self.framesThisSecond);
			self.framesThisSecond = 0;
		}, 1000);
		setInterval(function ()
		{
			var kBps = parseInt(self.bytesThisSecond / 1000);
			$("#Mbps").html((kBps) / 125);
			self.bytesThisSecond = 0;
		}, 1000);
	}
	var InitializeQualityButton = function ()
	{
		$("#qualityBtn").click(function ()
		{
			if (qualityDialog)
				qualityDialog.close();

			var $qdiv = $('<div id="qualityDialog"></div>');

			$qdiv.append(GetClientSettingsEle("quality", qualitySettingsChanged));
			$qdiv.append(GetClientSettingsEle("colorDetail", qualitySettingsChanged));
			$qdiv.append(GetClientSettingsEle("maxFps", qualitySettingsChanged));
			$qdiv.append(GetClientSettingsEle("maxFramesInTransit", qualitySettingsChanged));

			qualityDialog = $qdiv.dialog({ title: "Video Quality" });
		});
	}
	var qualitySetTimeout = null;
	var qualitySettingsChanged = function ()
	{
		clearTimeout(qualitySetTimeout);
		qualitySetTimeout = setTimeout(function ()
		{
			webSocketStreamer.setStreamSettings();
		}, 100);
	}

	InitializeMainMenu();
}
///////////////////////////////////////////////////////////////
// Desktop Video Player Base Class ////////////////////////////
///////////////////////////////////////////////////////////////
function DesktopVideoPlayer()
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
	}
	this.SocketOpen = function ()
	{
		self.Play();
	}
	this.SocketClose = function ()
	{
		isPlaying = false;
		playerModule.ConnectionLost();
		toaster.Warning("WebSocket closed.  Attempting to reconnect.");
		webSocketStreamer = new WebSocketStreamer();
	}
	this.Play = function ()
	{
		if (documentIsHidden())
			return;
		isPlaying = true;
		playerModule.Play();
	}
	this.Stop = function ()
	{
		isPlaying = false;
		playerModule.Stop();
	}
	this.NewFrame = function (buf)
	{
		playerModule.NewFrame(buf);
	}
}
///////////////////////////////////////////////////////////////
// Jpeg Fragmented Video Player ///////////////////////////////
///////////////////////////////////////////////////////////////
function JpegFragmentedVideoPlayer()
{
	var self = this;
	var submittedImageFragments = 0;
	var processedImageFragments = 0;
	var frameSequenceNumberCounter = 0;
	var nextFrameSequenceNumber = 1;
	this.Play = function ()
	{
		webSocketStreamer.setStreamSettings();
		webSocketStreamer.startStreaming();
	}
	this.Stop = function ()
	{
		webSocketStreamer.stopStreaming();
	}
	this.ConnectionLost = function ()
	{
	}
	this.NewFrame = function (buf, myFrameSequenceNumber, tries)
	{
		if (!myFrameSequenceNumber)
			myFrameSequenceNumber = ++frameSequenceNumberCounter;
		if (!tries)
			tries = 0;
		try
		{
			if (nextFrameSequenceNumber != myFrameSequenceNumber)
			{
				setTimeout(function () { self.NewFrame(buf, myFrameSequenceNumber, tries); }, 1);
				return;
			}
			if (submittedImageFragments != processedImageFragments)
			{
				if (tries > 250)
				{
					toaster.Warning("Hung NewFrame detected in JpegFragmentedVideoPlayer. Pushing ahead with possible image corruption.");
					// TODO: Restart stream
				}
				else
				{
					if (!tries)
						tries = 0;
					setTimeout(function () { self.NewFrame(buf, myFrameSequenceNumber, ++tries); }, 1);
					return;
				}
			}

			// Parse input
			var offsetWrapper = { offset: 2 }; // Skip 1 byte for command and 1 byte for stream ID.
			var moveFragCount = new DataView(buf, offsetWrapper.offset, 2).getUint16(0, false);
			offsetWrapper.offset += 2;
			var dirtyFragCount = new DataView(buf, offsetWrapper.offset, 2).getUint16(0, false);
			offsetWrapper.offset += 2;
			LogVerbose("      New Frame (moved: " + moveFragCount + ", dirty: " + dirtyFragCount + ")");

			var moveList = new Array();
			var dirtList = new Array();
			for (var i = 0; i < moveFragCount; i++)
				moveList.push(new MovedImageFragment(buf, offsetWrapper));
			for (var i = 0; i < dirtyFragCount; i++)
				dirtList.push(new DirtyImageFragment(buf, offsetWrapper));

			// Handle MoveImageFragments
			for (var i = 0; i < moveList.length; i++)
				MoveImageFragment(moveList[i].source, moveList[i].bounds);

			// Handle DirtyImageFragments
			for (var i = 0; i < dirtList.length; i++)
			{
				var dirtyImageFragment = dirtList[i];
				var tmpImg = document.createElement('img');
				tmpImg.bounds = dirtyImageFragment.bounds;
				tmpImg.onload = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					DrawImageFragment(this, this.bounds);
				}
				tmpImg.onerror = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					toaster.Error("Failed to decode image fragment");
				}
				submittedImageFragments++;
				tmpImg.src = dirtyImageFragment.imgBlobURL;
			}
			nextFrameSequenceNumber++;
			mainMenu.frameCounter++;
			mainMenu.framesThisSecond++;
			$("#frames").html(mainMenu.frameCounter);
			$("#kbps").html(buf);
		}
		catch (ex)
		{
			console.log(ex);
		}
	}
	var DrawImageFragment = function (tmpImg, regionRect)
	{
		var canvas = $("#myCanvas").get(0);
		var requiredWidth = regionRect.X + regionRect.Width;
		var requiredHeight = regionRect.Y + regionRect.Height;
		if (requiredWidth > canvas.width || requiredHeight > canvas.height)
		{
			toaster.Info("Resizing canvas from " + canvas.width + "x" + canvas.height + " to " + requiredWidth + "x" + requiredHeight);
			canvas.width = requiredWidth;
			canvas.height = requiredHeight;
		}
		var context2d = canvas.getContext("2d");
		context2d.drawImage(tmpImg, regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
	}
	var MoveImageFragment = function (srcPoint, regionRect)
	{
		var canvas = $("#myCanvas").get(0);
		var context2d = canvas.getContext("2d");
		context2d.drawImage(canvas, srcPoint.X, srcPoint.Y, regionRect.Width, regionRect.Height, regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
	}
}
function MovedImageFragment(buf, offsetWrapper)
{
	this.bounds = new ImageRegionRectangle(buf, offsetWrapper);
	this.source = new ImageSourcePoint(buf, offsetWrapper);
}
function DirtyImageFragment(buf, offsetWrapper)
{
	this.bounds = new ImageRegionRectangle(buf, offsetWrapper);
	var imgLength = new DataView(buf, offsetWrapper.offset, 4).getInt32(0, false);
	offsetWrapper.offset += 4;
	if (imgLength <= 0)
	{
		this.imgBlobURL = "";
		toaster.Error(imgLength + "-byte image fragment received");
	}
	else
	{
		LogVerbose("   buf.byteLength: " + buf.byteLength + ", offset: " + offsetWrapper.offset + ", imgLength: " + imgLength + ", calc length: " + (offsetWrapper.offset + imgLength));
		var imgView = new Uint8Array(buf, offsetWrapper.offset, imgLength);
		offsetWrapper.offset += imgLength;
		var imgBlob = new Blob([imgView], { type: 'image/jpeg' });
		this.imgBlobURL = URL.createObjectURL(imgBlob);
	}
}
function ImageSourcePoint(buf, offsetWrapper)
{
	this.X = ReadInt16(buf, offsetWrapper);
	this.Y = ReadInt16(buf, offsetWrapper);
}
function ImageRegionRectangle(buf, offsetWrapper)
{
	this.X = ReadInt16(buf, offsetWrapper);
	this.Y = ReadInt16(buf, offsetWrapper);
	this.Width = ReadUInt16(buf, offsetWrapper);
	this.Height = ReadUInt16(buf, offsetWrapper);
}
///////////////////////////////////////////////////////////////
// Desktop Info ///////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function DesktopInfo(buf, offsetWrapper)
{
	var numScreens = ReadByte(buf, offsetWrapper);
	this.screens = new Array(numScreens);
	for (var i = 0; i < numScreens; i++)
		this.screens[i] = new DesktopScreen(buf, offsetWrapper, i);
}
function DesktopScreen(buf, offsetWrapper, index)
{
	var self = this;

	this.index = index;

	this.adapterIndex = ReadByte(buf, offsetWrapper);
	this.outputIndex = ReadByte(buf, offsetWrapper);

	var adapterNameLength = ReadUInt16(buf, offsetWrapper);
	this.adapterName = ReadUTF8(buf, offsetWrapper, adapterNameLength);

	var outputNameLength = ReadUInt16(buf, offsetWrapper);
	this.outputName = ReadUTF8(buf, offsetWrapper, outputNameLength);

	this.X = ReadInt16(buf, offsetWrapper);
	this.Y = ReadInt16(buf, offsetWrapper);
	this.Width = ReadUInt16(buf, offsetWrapper);
	this.Height = ReadUInt16(buf, offsetWrapper);
}
///////////////////////////////////////////////////////////////
// Web Socket Streaming ///////////////////////////////////////
///////////////////////////////////////////////////////////////
var clientId = UrlParameters.Get("clientid");
// TODO: Replace this socketHost functionality. In the future, all communication will happen with the server from which the Client.html page loaded. e.g. location.hostname
var socketHost = GetSocketHost();
function GetSocketHost()
{
	if (clientId == "SHRD TestClient")
		return "192.168.0.115";
	else if (clientId == "brick")
		return "192.168.0.165";
	else
		return location.hostname;
}
function WebSocketStreamer()
{
	var self = this;
	var socket;
	var WebSocket_Connecting = 0;
	var WebSocket_Open = 1;
	var WebSocket_Closing = 2;
	var WebSocket_Closed = 3;
	var ws_is_ready = false;
	this.currentDesktopInfo = null;

	var currentStreamId = 0;

	// Enums
	var Command =
		{
			StartStreaming: 0
			, StopStreaming: 1
			, AcknowledgeFrame: 2
			, ReproduceUserInput: 3
			, GetDesktopInfo: 4
			, SetStreamSettings: 5
			, GetStreamSettings: 6
			, GetScreenCapture: 10
			, Error_SyntaxError: 253
			, Error_CommandCodeUnknown: 254
			, Error_Unspecified: 255
		};
	var ByteFlagConstants =
		{
			b0000_0000: 0
			, b0000_0001: 1
			, b0000_0010: 2
			, b0000_0100: 4
			, b0000_1000: 8
			, b0001_0000: 16
			, b0010_0000: 32
			, b0100_0000: 64
			, b1000_0000: 128
		};
	var ImgFlags =
		{
			Grayscale: ByteFlagConstants.b0000_0000
			, Color420: ByteFlagConstants.b0000_0001
			, Color440: ByteFlagConstants.b0000_0010
			, Color444: ByteFlagConstants.b0000_0001 | ByteFlagConstants.b0000_0010
			, Refresh: ByteFlagConstants.b0000_0100 // A full desktop refresh is requested, not differential
		};
	var StreamType =
		{
			JPEG: ByteFlagConstants.b0000_0000
			, H264: ByteFlagConstants.b0000_0001
		};
	var InputType =
		{
			KeyDown: 0 // Followed by Int32 (key code), UInt32 (ModifierKeys)
			, KeyUp: 1 // Same as KeyDown
			, MouseMove: 100 // Followed by two Float32 (global X coordinate, global Y coordinate)
			, MouseButtonDown: 101 // Followed by one byte (MouseButtons number)
			, MouseButtonUp: 102 // Same as MouseButtonDown
			, MouseWheel: 103 // Followed by two Int16 (mouse wheel delta X, Y)
		};

	var Initialize = function ()
	{
		socket = new WebSocket("ws://" + socketHost + ":8089/SHRD");
		socket.binaryType = "arraybuffer";
		socket.onopen = function (event)
		{
			console.info("WebSocket Open");
			// TODO: Send auth info here
			ws_is_ready = true;
			getDesktopInfo();
			player.SocketOpen();
		}
		socket.onclose = function (event)
		{
			player.SocketClose();
			var codeTranslation = WebSocketCloseCode.Translate(event.code);
			var errmsg = "WebSocket Closed. (Code " + htmlEncode(event.code + (event.reason ? (" " + event.reason) : "")) + ")<br/>" + codeTranslation[0] + "<br/>" + codeTranslation[1];
			// TODO: Replace modal dialog with a toast, a dimmed remote display, and maybe a reconnect icon overlayed in the middle.
			//ModalDialog(errmsg);
			console.info(errmsg);
		}
		socket.onerror = function (event)
		{
			toaster.Warning("WebSocket Error");
		};
		socket.onmessage = function (event)
		{
			HandleWSMessage(event.data);
		};
	}
	var HandleWSMessage = function (data)
	{
		var cmdView = new Uint8Array(data, 0, 1);
		if (cmdView.length == 0)
		{
			toaster.Warning("empty message from server");
			return;
		}
		switch (cmdView[0])
		{
			case Command.StartStreaming:
				currentStreamId = ReadByte(data, { offset: 1 });
				console.log("Streaming Started: " + currentStreamId);
				break;
			case Command.StopStreaming:
				toaster.Info("StopStreaming message received from server.");
				break;
			case Command.GetScreenCapture:
				var streamId = ReadByte(data, { offset: 1 });
				if (streamId != currentStreamId)
				{
					console.info("dropped frame from stream " + streamId + " because current stream is " + currentStreamId);
					break;
				}
				player.NewFrame(data);
				mainMenu.bytesThisSecond += data.byteLength;
				acknowledgeFrame(streamId);
				break;
			case Command.ReproduceUserInput:
				toaster.Warning("ReproduceUserInput message received from server. This should not ever happen.");
				break;
			case Command.GetDesktopInfo:
				toaster.Info("GetDesktopInfo message received from server.");
				self.currentDesktopInfo = new DesktopInfo(data, { offset: 1 });
				break;
			case Command.Error_SyntaxError:
				toaster.Warning("Error_SyntaxError message received from server");
				break;
			case Command.Error_CommandCodeUnknown:
				toaster.Warning("Error_CommandCodeUnknown message received from server");
				break;
			case Command.Error_Unspecified:
				toaster.Warning("Error_Unspecified message received from server");
				break;
			default:
				toaster.Warning("Unidentifiable message received from server, starting with byte: " + cmdView[0]);
				break;
		}
	}
	this.setStreamSettings = function ()
	{
		var arg = new Uint8Array(5);
		arg[0] = Command.SetStreamSettings;
		arg[1] = GetImageColorFlags(clientSettings.colorDetail);
		arg[2] = Clamp(parseInt(clientSettings.quality), 1, 100);
		arg[3] = Clamp(parseInt(clientSettings.maxFps), 1, 60);
		arg[4] = Clamp(parseInt(clientSettings.maxFramesInTransit), 1, 60);
		SendToWebSocket(arg);
	}
	this.startStreaming = function ()
	{
		console.log("StartStreaming");
		var arg = new Uint8Array(3);
		arg[0] = Command.StartStreaming;
		arg[1] = StreamType.JPEG; // Stream type (JPEG / H.264). H.264 not yet implemented.
		arg[2] = 0; // 0 = Primary display

		SendToWebSocket(arg);
	}
	this.stopStreaming = function ()
	{
		console.log("StopStreaming");
		var arg = new Uint8Array(2);
		arg[0] = Command.StopStreaming;
		SendToWebSocket(arg);
	}
	var acknowledgeFrame = function (streamId)
	{
		var arg = new Uint8Array(2);
		arg[0] = Command.AcknowledgeFrame;
		arg[1] = streamId;
		SendToWebSocket(arg);
	}
	this.reproduceKeyAction = function (keyDown, keyCode, modifiers)
	{
		var arg = new Uint8Array(10);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = keyDown ? InputType.KeyDown : InputType.KeyUp;
		var offsetWrapper = { offset: 2 };
		WriteInt32(arg, offsetWrapper, keyCode);
		WriteUInt32(arg, offsetWrapper, modifiers);
		SendToWebSocket(arg);
	}
	this.reproduceMouseMoveAction = function (x, y)
	{
		var arg = new Uint8Array(10);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = InputType.MouseMove;
		var offsetWrapper = { offset: 2 };
		WriteFloat(arg, offsetWrapper, x);
		WriteFloat(arg, offsetWrapper, y);
		SendToWebSocket(arg);
	}
	this.reproduceMouseButtonAction = function (buttonDown, buttonCode)
	{
		var arg = new Uint8Array(3);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = buttonDown ? InputType.MouseButtonDown : InputType.MouseButtonUp;
		arg[2] = buttonCode;
		SendToWebSocket(arg);
	}
	this.reproduceMouseWheelAction = function (deltaX, deltaY)
	{
		var arg = new Uint8Array(6);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = InputType.MouseWheel;
		var offsetWrapper = { offset: 2 };
		WriteInt16(arg, offsetWrapper, deltaX);
		WriteInt16(arg, offsetWrapper, deltaY);
		SendToWebSocket(arg);
	}
	var GetImageColorFlags = function (colorDetail)
	{
		if (colorDetail == 1)
			return ImgFlags.Color420;
		if (colorDetail == 2)
			return ImgFlags.Color440;
		if (colorDetail == 3)
			return ImgFlags.Color444;
		return ImgFlags.Grayscale;
	}
	var getDesktopInfo = function ()
	{
		var arg = new Uint8Array(1);
		arg[0] = Command.GetDesktopInfo;
		SendToWebSocket(arg);
	}
	var SendToWebSocket = function (message)
	{
		if (typeof socket == "undefined" || !socket)
			return;
		switch (socket.readyState)
		{
			case WebSocket_Open:
				if (ws_is_ready)
					socket.send(message);
				else
					toaster.Error("Authentication error");
				break;
			case WebSocket_Connecting:
				toaster.Warning("WebSocket is still connecting.");
				break;
			case WebSocket_Closing:
				toaster.Warning("WebSocket is closing.");
				break;
			case WebSocket_Closed:
				toaster.Warning("WebSocket is closed.");
				break;
		}
	}

	Initialize();
}
///////////////////////////////////////////////////////////////
// Misc ///////////////////////////////////////
///////////////////////////////////////////////////////////////
var logVerbose = false;
function LogVerbose(msg)
{
	if (logVerbose)
		console.info(msg);
}
function ModalDialog(msg)
{
	$("<div></div>").html(msg).modalDialog({ title: "SHRD Message" });
}