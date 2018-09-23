<template>
	<div class="computersRoot">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /></div>
		<div v-else><VueGoodTable :columns="columns" :rows="rows"></VueGoodTable></div>
	</div>
</template>

<script>
	import ExecJSON from 'appRoot/api/api.js';
	import { VueGoodTable } from 'vue-good-table';

	export default {
		components: { VueGoodTable },
		data: function ()
		{
			return {
				error: null,
				loading: false,
				columns: [
					{ label: 'ID', field: 'ID', type: 'number' }
					, { label: 'Name', field: 'Name', type: 'text' }
					, { label: 'Groups', field: 'Groups', type: 'text', sortable: false, formatFn: this.formatGroups }
					, { label: 'Status', field: 'StatusHtml', type: 'text', html: true }

				],
				rows: []
			};
		},
		computed: {
		},
		methods: {
			loadComputerList()
			{
				this.loading = true;
				ExecJSON({ cmd: "admin/getComputers" }).then(data =>
				{
					for (let i = 0; i < data.Computers.length; i++)
					{
						let c = data.Computers[i];
						if (c.Uptime > -1) // Computer is online
						{
							var dateLastConnect = new Date(Date.now() - c.Uptime);
							c.StatusHtml = '<span class="compOnline">Online ' + Util.GetFuzzyTime(c.Uptime) + ' since ' + Util.GetDateStr(dateLastConnect) + '</span>';
						}
						else if (c.LastDisconnect === 0) // Computer has never connected
							c.StatusHtml = '<span class="compOffline">Has Never Connected</span>';
						else // Computer is offline
						{
							var dateLastDisconnect = new Date(c.LastDisconnect);
							var timeSinceDisconnect = Date.now() - c.LastDisconnect;
							c.StatusHtml = '<span class="compOffline">Disconnected since ' + Util.GetFuzzyTime(timeSinceDisconnect) + ' at ' + Util.GetDateStr(dateLastDisconnect) + '</span>';
						}
					}
					this.rows = data.Computers;
				}
				).catch(err =>
				{
					this.error = err.message;
				}
				).finally(() =>
				{
					this.loading = false;
				});
			},
			formatGroups(groups)
			{
				var groupNames = new Array();
				for (var i = 0; i < groups.length; i++)
					groupNames.push(groups[i].Name);
				return groupNames.join(", ");
			},
			formatStatus(value)
			{
				console.log(arguments);
				return new Date(value).toString();
			}
		},
		created()
		{
			this.loadComputerList();
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

	.compOnline
	{
		font-weight: bold;
		color: #66BB66;
	}

	.compOffline
	{
		font-weight: bold;
		color: #FF0000;
	}
</style>