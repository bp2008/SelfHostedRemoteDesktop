import DesktopInfo from 'appRoot/scripts/remote/objects/DesktopInfo.js';

export function WebSocketStreamer(computerId)
{
	var self = this;
	var socket;
	var ws_is_ready = false;
	this.currentDesktopInfo = null;
	this.onFrameReceived = null;
	this.onStateChanged = null;

	var currentStreamId = 0;
	this.Connect = function (sessionId)
	{
		socket = new WebSocket("ws" + (location.protocol === "https:" ? "s" : "") + "://" + location.hostname + ":" + location.port + "/WebSocketClientProxy/" + computerId + "/" + sessionId + "/WebSocketStream_" + computerId);
		raiseStateChanged();
		socket.binaryType = "arraybuffer";
		socket.onopen = function (event)
		{
			console.info("WebSocket Open");
			ws_is_ready = true;
			getDesktopInfo();
			raiseStateChanged();
		};
		socket.onclose = function (event)
		{
			var codeTranslation = Util.TranslateWebSocketCloseCode(event.code);
			var errmsg = "Code " + Util.EscapeHTML(event.code + (event.reason ? " " + event.reason : "")) + "<br/>" + codeTranslation[0] + "<br/>" + codeTranslation[1];
			toaster.Error("WebSocket Closed", errmsg, 30000);
			// TODO: Replace modal dialog with a toast, a dimmed remote display, and maybe a reconnect icon overlayed in the middle.
			//ModalDialog(errmsg);
			raiseStateChanged();
		};
		socket.onerror = function (event)
		{
			// We can't find out what the error was.  Yay web standards.
			toaster.Warning("WebSocket Error");
		};
		socket.onmessage = function (event)
		{
			HandleWSMessage(event.data);
		};
	};
	this.Disconnect = function ()
	{
		socket.close();
		socket = null;
	};
	var HandleWSMessage = function (data)
	{
		var cmdView = new Uint8Array(data, 0, 1);
		if (cmdView.length === 0)
		{
			toaster.Warning("empty message from server");
			return;
		}
		switch (cmdView[0])
		{
			case Command.StartStreaming:
				currentStreamId = Util.ReadByte(data, { offset: 1 });
				console.log("Streaming Started: " + currentStreamId);
				break;
			case Command.StopStreaming:
				toaster.Info("StopStreaming message received from server.");
				break;
			case Command.GetScreenCapture:
				var streamId = Util.ReadByte(data, { offset: 1 });
				if (streamId !== currentStreamId)
				{
					console.info("dropped frame from stream " + streamId + " because current stream is " + currentStreamId);
					break;
				}
				if (typeof self.onFrameReceived === "function")
					self.onFrameReceived(data);
				//mainMenu.bytesThisSecond += data.byteLength;
				acknowledgeFrame(streamId);
				break;
			case Command.ReproduceUserInput:
				toaster.Warning("ReproduceUserInput message received from server. This should not ever happen.");
				break;
			case Command.GetDesktopInfo:
				toaster.Info("GetDesktopInfo message received from server.");
				self.currentDesktopInfo = new DesktopInfo(data, { offset: 1 });
				console.log("GetDesktopInfo", self.currentDesktopInfo);
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
	};
	this.setStreamSettings = function (clientSettings)
	{
		var arg = new Uint8Array(5);
		arg[0] = Command.SetStreamSettings;
		arg[1] = GetImageColorFlags(clientSettings.colorDetail);
		arg[2] = Util.Clamp(parseInt(clientSettings.quality), 1, 100);
		arg[3] = Util.Clamp(parseInt(clientSettings.maxFps), 1, 60);
		arg[4] = Util.Clamp(parseInt(clientSettings.maxFramesInTransit), 1, 60);
		SendToWebSocket(arg);
	};
	this.startStreaming = function ()
	{
		console.log("StartStreaming");
		var arg = new Uint8Array(3);
		arg[0] = Command.StartStreaming;
		arg[1] = StreamType.JPEG; // Stream type (JPEG / H.264). H.264 not yet implemented.
		arg[2] = 0; // 0 = Primary display

		SendToWebSocket(arg);
	};
	this.stopStreaming = function ()
	{
		console.log("StopStreaming");
		var arg = new Uint8Array(2);
		arg[0] = Command.StopStreaming;
		SendToWebSocket(arg);
	};
	var acknowledgeFrame = function (streamId)
	{
		var arg = new Uint8Array(2);
		arg[0] = Command.AcknowledgeFrame;
		arg[1] = streamId;
		SendToWebSocket(arg);
	};
	this.reproduceKeyAction = function (keyDown, keyCode, modifiers)
	{
		var arg = new Uint8Array(10);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = keyDown ? InputType.KeyDown : InputType.KeyUp;
		var offsetWrapper = { offset: 2 };
		Util.WriteInt32(arg, offsetWrapper, keyCode);
		Util.WriteUInt32(arg, offsetWrapper, modifiers);
		SendToWebSocket(arg);
	};
	this.reproduceMouseMoveAction = function (x, y)
	{
		var arg = new Uint8Array(10);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = InputType.MouseMove;
		var offsetWrapper = { offset: 2 };
		Util.WriteFloat(arg, offsetWrapper, x);
		Util.WriteFloat(arg, offsetWrapper, y);
		SendToWebSocket(arg);
	};
	this.reproduceMouseButtonAction = function (buttonDown, buttonCode)
	{
		var arg = new Uint8Array(3);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = buttonDown ? InputType.MouseButtonDown : InputType.MouseButtonUp;
		arg[2] = buttonCode;
		SendToWebSocket(arg);
	};
	this.reproduceMouseWheelAction = function (deltaX, deltaY)
	{
		var arg = new Uint8Array(6);
		arg[0] = Command.ReproduceUserInput;
		arg[1] = InputType.MouseWheel;
		var offsetWrapper = { offset: 2 };
		Util.WriteInt16(arg, offsetWrapper, deltaX);
		Util.WriteInt16(arg, offsetWrapper, deltaY);
		SendToWebSocket(arg);
	};
	var GetImageColorFlags = function (colorDetail)
	{
		if (colorDetail === 1)
			return ImgFlags.Color420;
		if (colorDetail === 2)
			return ImgFlags.Color440;
		if (colorDetail === 3)
			return ImgFlags.Color444;
		return ImgFlags.Grayscale;
	};
	var getDesktopInfo = function ()
	{
		var arg = new Uint8Array(1);
		arg[0] = Command.GetDesktopInfo;
		SendToWebSocket(arg);
	};
	var SendToWebSocket = function (message)
	{
		if (typeof socket === "undefined" || !socket)
		{
			console.log("Outgoing websocket message suppressed because socket is not open");
			return;
		}
		switch (socket.readyState)
		{
			case WebSocketState.Connecting:
				toaster.Warning("WebSocket is still connecting.");
				break;
			case WebSocketState.Open:
				if (ws_is_ready)
					socket.send(message);
				else
					toaster.Error("Authentication error");
				break;
			case WebSocketState.Closing:
				toaster.Warning("WebSocket is closing.");
				break;
			case WebSocketState.Closed:
				toaster.Warning("WebSocket is closed.");
				break;
		}
	};

	///////////////////////////////////////////////////////////////
	// Simple Public Getters //////////////////////////////////////
	///////////////////////////////////////////////////////////////
	this.getReadyState = () => socket ? socket.readyState : WebSocketState.Closed;
	///////////////////////////////////////////////////////////////
	// Private Helper Methods /////////////////////////////////////
	///////////////////////////////////////////////////////////////
	let raiseStateChanged = function ()
	{
		if (socket && typeof self.onStateChanged === "function")
			self.onStateChanged(socket.readyState);
	};
}

///////////////////////////////////////////////////////////////
// Enums //////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
const Command =
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
const ByteFlagConstants =
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
const ImgFlags =
{
	Grayscale: ByteFlagConstants.b0000_0000
	, Color420: ByteFlagConstants.b0000_0001
	, Color440: ByteFlagConstants.b0000_0010
	, Color444: ByteFlagConstants.b0000_0001 | ByteFlagConstants.b0000_0010
	, Refresh: ByteFlagConstants.b0000_0100 // A full desktop refresh is requested, not differential
};
const StreamType =
{
	JPEG: ByteFlagConstants.b0000_0000
	, H264: ByteFlagConstants.b0000_0001
};
const InputType =
{
	KeyDown: 0 // Followed by Int32 (key code), UInt32 (ModifierKeys)
	, KeyUp: 1 // Same as KeyDown
	, MouseMove: 100 // Followed by two Float32 (global X coordinate, global Y coordinate)
	, MouseButtonDown: 101 // Followed by one byte (MouseButtons number)
	, MouseButtonUp: 102 // Same as MouseButtonDown
	, MouseWheel: 103 // Followed by two Int16 (mouse wheel delta X, Y)
};
export const WebSocketState =
{
	Connecting: 0,
	Open: 1,
	Closing: 2,
	Closed: 3
};