<template>
	<div class="computerGroupRoot">
		<div class="groupName">{{group.Name}}</div>
		<div class="computers">
			<div v-if="!group.Computers || group.Computers.length === 0">There are no computers in this group.</div>
			<Computer class="computer" v-for="computer in group.Computers" :key="computer.ID" :computer="computer"></Computer>
		</div>
	</div>
</template>

<script>
	import Computer from 'appRoot/vues/client/computers/Computer.vue';

	export default {
		components: { Computer },
		props: {
			group: {
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
	.computerGroupRoot
	{
		max-width: 600px;
	}
	.groupName
	{
		font-size: 20px;
	}
</style>