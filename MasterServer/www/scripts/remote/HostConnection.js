import { GetNamespaceLocalStorage } from 'appRoot/scripts/LocalSettings.js';
import DesktopVideoRenderer from 'appRoot/scripts/remote/DesktopVideoRenderer.js';
import InputCatcher from 'appRoot/scripts/remote/InputCatcher.js';
import { WebSocketStreamer, WebSocketState } from 'appRoot/scripts/remote/WebSocketStreamer.js';

export default function HostConnection(args)
{
	let self = this;
	let clientSettings = {};
	let renderer = null;
	let inputCatcher = null;
	let webSocketStreamer = null;
	let unbindClientSettingsWatcher = null;

	this.onSocketStateChanged = null;

	let Initialize = function ()
	{
		myApp.$store.dispatch('getComputerSpecificSettings', args.computerId).then(cs =>
		{
			clientSettings = cs;
			unbindClientSettingsWatcher = myApp.$watch(() => cs, clientSettingsChanged, { deep: true });
		}
		).catch(err =>
		{
			toaster.error(err);
		});
		renderer = new DesktopVideoRenderer(args.canvas);
		//inputCatcher = new InputCatcher(args.canvas);
		webSocketStreamer = new WebSocketStreamer(args.computerId, renderer);
		webSocketStreamer.onFrameReceived = onFrameReceived;
		webSocketStreamer.onStateChanged = onSocketStateChanged;
	};
	let clientSettingsChanged = function ()
	{
		webSocketStreamer.setStreamSettings(clientSettings);
	};
	this.IsConnected = function ()
	{
		return webSocketStreamer.getReadyState() === WebSocketState.Open;
	};

	this.Connect = function ()
	{
		webSocketStreamer.Connect(args.sid);
	};

	this.Disconnect = function ()
	{
		webSocketStreamer.Disconnect();
	};
	this.Dispose = function ()
	{
		self.Disconnect();
		if (unbindClientSettingsWatcher)
			unbindClientSettingsWatcher();
		//inputCatcher.Dispose();
	};
	let onSocketStateChanged = function (state)
	{
		if (state === WebSocketState.Connecting)
		{
			console.log("HostConnection", "WebSocketState.Connecting");
		}
		else if (state === WebSocketState.Open)
		{
			console.log("HostConnection", "WebSocketState.Open");
			webSocketStreamer.setStreamSettings(clientSettings);
			webSocketStreamer.startStreaming();
		}
		else if (state === WebSocketState.Closing)
		{
			console.log("HostConnection", "WebSocketState.Closing");
		}
		else if (state === WebSocketState.Closed)
		{
			console.log("HostConnection", "WebSocketState.Closed");
		}
		else
			console.error("Unknown web socket state: " + state);

		if (typeof self.onSocketStateChanged === "function")
			self.onSocketStateChanged(state);
	};
	let onFrameReceived = function (frame)
	{
		console.log("HostConnection: frame received", frame.byteLength);
		renderer.NewFrame(frame);
	};

	// The Initialize call should be at the end of the HostConnection function so that everything else is defined.
	Initialize();
}