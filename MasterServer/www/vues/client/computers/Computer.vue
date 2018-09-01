<template>
	<div class="computerRoot" :class="{ online: isOnline }">
		<div><svg class="icon"><use xlink:href="#computer"></use></svg> {{computer.Name}}</div>
		<div class="status"><span class="statusText">{{status}}</span></div>
	</div>
</template>

<script>
	import svg1 from 'appRoot/images/sprite/computer.svg'; // Ignore the warning that this is never read.
	import { GetFuzzyTime, GetDateStr } from 'appRoot/scripts/Util.js';

	export default {
		props: {
			computer: {
				type: Object,
				default: {}
			}
		},
		data: function ()
		{
			return {
			};
		},
		computed: {
			isOnline()
			{
				return this.computer.Uptime >= 0;
			},
			status()
			{

				if (this.computer.Uptime > -1) // Computer is online
				{
					var dateLastConnect = new Date(Date.now() - this.computer.Uptime);
					return 'Online ' + GetFuzzyTime(this.computer.Uptime) + ' since ' + GetDateStr(dateLastConnect);
				}
				else if (this.computer.LastDisconnect === 0) // Computer has never connected
					return 'Never Connected';
				else // Computer is offline
				{
					var dateLastDisconnect = new Date(this.computer.LastDisconnect);
					var timeSinceDisconnect = Date.now() - this.computer.LastDisconnect;
					return 'Disconnected since ' + GetFuzzyTime(timeSinceDisconnect) + ' at ' + GetDateStr(dateLastDisconnect);
				}
			}
		},
		methods: {
		},
		created()
		{
		}
	}
</script>

<style scoped>
	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.computerRoot
	{
		border: 1px solid black;
		border-radius: 3px;
		padding: 1px 5px;
		margin: 5px;
		display: flex;
		flex-wrap: wrap;
		justify-content: space-between;
	}

	.icon
	{
		fill: #BBBBBB;
		width: 24px;
		height: 24px;
	}

	.online .icon
	{
		fill: #0083FF;
	}

	.statusText
	{
		color: #BBBBBB;
	}

	.online .statusText
	{
		color: #000000;
	}
</style>