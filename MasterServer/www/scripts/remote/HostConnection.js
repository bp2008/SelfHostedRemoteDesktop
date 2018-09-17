import * as Util from 'appRoot/scripts/Util.js';
import { GetNamespaceLocalStorage } from 'appRoot/scripts/LocalSettings.js';
import DesktopVideoPlayer from 'appRoot/scripts/DesktopVideoPlayer.js';
import InputCatcher from 'appRoot/scripts/InputCatcher.js';
import WebSocketStreamer from 'appRoot/scripts/WebSocketStreamer.js';

export default function HostConnection(computerId, sessionId)
{
	let self = this;
	let clientSettings = new GetNamespaceLocalStorage();
	let player = new DesktopVideoPlayer();
	let inputCatcher = new InputCatcher();
	let webSocketStreamer = new WebSocketStreamer(computerId, sessionId);

	this.IsConnected = function ()
	{
		return webSocketStreamer.connected;
	};

	this.Connect = function ()
	{
		webSocketStreamer.Connect();
	};

	this.Disconnect = function ()
	{
		webSocketStreamer.Disconnect();
	};
}