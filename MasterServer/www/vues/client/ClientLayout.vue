<template>
	<div class="root">
		<div class="body">
			<ClientMenu></ClientMenu>
			<router-view></router-view>
		</div>
		<Footer />
	</div>
</template>

<script>

	import ClientMenu from 'appRoot/vues/client/ClientMenu.vue';
	import Footer from 'appRoot/vues/common/Footer.vue';

	export default {
		components: { ClientMenu, Footer },
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
		font-size: 16px;
		display: flex;
		flex-direction: column;
		align-items: center;
		width: 100%;
		height: 100%;
	}

	.body
	{
		width: 100%;
	}
</style>