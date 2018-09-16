<template>
	<div class="root">
		Client Computer Home
		<div><router-link :to="{ name: 'remoteDesktopFull', params: { computerId: $route.params.computerId } }">Remote Desktop (Connect)</router-link></div>
	</div>
</template>

<script>

	export default {
		components: {},
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
	};
</script>

<style scoped>
	.root
	{
	}
</style>