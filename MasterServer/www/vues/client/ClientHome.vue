<template>
	<div class="homeRoot">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /></div>
		<div v-else class="computerGroups">
			<div v-if="computerGroups.length === 0">You do not have access to any computers.</div>
			<ComputerGroup class="computerGroup" v-for="group in computerGroups" :key="group.ID" :group="group"></ComputerGroup>
		</div>
	</div>
</template>

<script>
	import ExecJSON from 'appRoot/api/api.js';
	import ComputerGroup from 'appRoot/vues/client/computers/ComputerGroup.vue';

	export default {
		components: { ComputerGroup },
		data: function ()
		{
			return {
				error: null,
				loading: false,
				computerGroups: [
				]
			};
		},
		computed: {
		},
		methods: {
			loadComputerList()
			{
				this.loading = true;
				ExecJSON({ cmd: "getComputers" }).then(response =>
				{
					this.computerGroups = response.Groups;
				}
				).catch(err =>
				{
					this.error = err.message;
				}
				).finally(() =>
				{
					this.loading = false;
				});
			}
		},
		created()
		{
			this.loadComputerList();
		}
	}
</script>

<style scoped>
	.homeRoot
	{
		margin: 8px;
	}

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

	.computerGroup
	{
		margin: 10px 0px;
	}

		.computerGroup:first-child
		{
			margin-top: 0px;
		}
</style>