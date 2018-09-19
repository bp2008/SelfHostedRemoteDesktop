<template>
	<div class="rdRoot customScroll">
		<div class="loading" v-if="loading">
			<div class="loadingCompName" v-if="computer && computer.Name">{{computer.Name}}</div>
			<ScaleLoader />
			<div>Loading…</div>
		</div>
		<div class="loadingError" v-else-if="loadingError">
			An error occurred during loading:<br />
			<span class="errMsg">{{loadingError}}</span>
		</div>
		<div class="videoFrame" v-show="computer && !loading">
			<canvas ref="myCanvas" class="videoFrameCanvas"></canvas>
			<div class="notConnected" v-if="isConnecting">
				<div class="loadingCompName" v-if="computer && computer.Name">{{computer.Name}}</div>
				<ScaleLoader />
				<div>Connecting…</div>
			</div>
		</div>
		<div class="loadingError" v-if="!computer">
			An error occurred during loading: computer object is null
		</div>
	</div>
</template>

<script>
	import HostConnection from 'appRoot/scripts/remote/HostConnection.js';
	import { WebSocketState } from 'appRoot/scripts/remote/WebSocketStreamer.js';

	let ConnectionState = { Disconnected: 0, Authenticating: 1, Connected: 2 };

	export default {
		components: {},
		data: function ()
		{
			return {
				computer: null,
				loadingError: null,
				loading: false,
				host: null, // Host Connection Handle
				socketState: WebSocketState.Closed
			};
		},
		computed: {
			isConnecting()
			{
				return this.socketState === WebSocketState.Connecting;
			},
			isConnected()
			{
				return this.socketState === WebSocketState.Open;
			}
		},
		methods: {
			fetchData()
			{
				this.loading = true;
				this.loadingError = null;
				this.computer = { Name: "Loading" };
				this.$store.dispatch("getClientComputerInfo", parseInt(this.$route.params.computerId)).then(c =>
				{
					this.computer = c;
					let args = {
						computerId: c.ID,
						sid: this.$store.getters.sid,
						canvas: this.$refs.myCanvas
					};
					console.log(this);
					this.host = new HostConnection(args);
					this.host.onSocketStateChanged = this.onSocketStateChanged;
					this.host.Connect();
					this.connecting = true;
				}
				).catch(err =>
				{
					this.loadingError = err.stack;
					console.error(err);
				}
				).finally(() =>
				{
					this.loading = false;
				});
			},
			onSocketStateChanged(state)
			{
				this.socketState = state;
			}
		},
		created()
		{
			let compatibilityTestResult = CompatibilityTest();
			if (compatibilityTestResult)
			{
				this.loadingError = compatibilityTestResult;
				return;
			}

			this.fetchData();
		},
		watch: {
			'$route': 'fetchData' // called if the route changes
		},
		beforeDestroy()
		{
			if (this.host)
				this.host.Dispose();
		}
	};

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
</script>

<style scoped>
	@import 'CustomScroll.css';

	.rdRoot
	{
		width: 100%;
		height: 100%;
		overflow: auto;
		background-color: #000000;
	}

	.loading,
	.notConnected
	{
		width: 100%;
		height: 100%;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		font-size: 16pt;
		color: #AAAAAA;
	}

	.loadingCompName
	{
		margin-bottom: 5px;
	}

	.loadingError
	{
		box-sizing: border-box;
		margin: 20px;
		padding: 10px 20px;
		border: 2px solid #FF0000;
		border-radius: 5px;
		background-color: #FFFFFF;
		color: #FF0000;
		font-size: 12pt;
	}

	.errMsg
	{
		white-space: pre-wrap;
	}

	.videoFrame
	{
		position: relative;
		background-color: #660000;
	}
</style>