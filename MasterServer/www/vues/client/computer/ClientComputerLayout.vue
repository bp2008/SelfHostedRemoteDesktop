<template>
	<div class="cclayout">
		<div class="cctitle">
			<Computer :computer="computer"></Computer>
			<!--{{computer.Name}} <span class="ccstatus">{{status}}</span>-->
		</div>
		<b-nav tabs class="ccnav">
			<b-nav-item :to="{ name: 'clientComputerHome' }">Home</b-nav-item>
			<b-nav-item :to="{ name: 'clientComputerPerformance' }">Performance</b-nav-item>
			<b-nav-item :to="{ name: 'clientComputerSecurity' }">Security</b-nav-item>
			<b-nav-item :to="{ name: 'clientComputerEvents' }">Events</b-nav-item>
		</b-nav>
		<div class="ccbody">
			<router-view></router-view>
		</div>
	</div>
</template>

<script>
	import { GetFuzzyTime, GetDateStr } from 'appRoot/scripts/Util.js';
	import Computer from 'appRoot/vues/client/controls/Computer.vue';

	export default {
		components: { Computer },
		data: function ()
		{
			return {
				computer: { Name: "Loading…" }
			};
		},
		computed: {
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
		mounted()
		{
			this.$store.dispatch("getClientComputerInfo", parseInt(this.$route.params.computerId)).then(c =>
			{
				this.computer = c;
			}
			).catch(err =>
			{
				this.computer = { Name: err.message };
			});
		},
		created()
		{
			console.log(this.$store.getters.clientComputerGroups);
		}
	};
</script>

<style scoped>
	.cclayout
	{
		font-size: 16px;
	}

	.cctitle
	{
		margin: 10px;
		max-width: 600px;
	}

	.ccnav
	{
		padding: 0px 10px 0px 10px;
	}

	.ccbody
	{
		margin: 10px;
	}
</style>