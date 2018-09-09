<template>
	<div class="usersRoot">
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
					, { label: 'DisplayName', field: 'DisplayName', type: 'text' }
					, { label: 'Email', field: 'Email', type: 'text' }
					, { label: 'IsAdmin', field: 'IsAdmin', type: 'boolean' }
					, { label: 'Groups', field: 'Groups', type: 'text', sortable: false, formatFn: this.formatGroups }

				],
				rows: []
			};
		},
		computed: {
		},
		methods:
		{
			loadUserList()
			{
				this.loading = true;
				ExecJSON({ cmd: "admin/getUsers" }).then(response => { this.rows = response.Users; })
					.catch(err => { this.error = err.message; })
					.finally(() => { this.loading = false; });
			},
			formatGroups(groups)
			{
				var groupNames = new Array();
				for (var i = 0; i < groups.length; i++)
					groupNames.push(groups[i].Name);
				return groupNames.join(", ");
			}
		},
		created()
		{
			this.loadUserList();
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
</style>