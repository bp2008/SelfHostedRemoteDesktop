<template>
	<div class="publicRoot">
		<router-view></router-view>
		<Footer />
	</div>
</template>

<script>
	import Footer from 'appRoot/vues/common/Footer.vue';

	export default {
		components: { Footer },
		data: function ()
		{
			return {
			};
		},
		computed: {
			serverVersion()
			{
				return serverVersion;
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
	.publicRoot
	{
		font-size: 16px;
		display: flex;
		flex-direction: column;
		align-items: center;
		width: 100%;
		height: 100%;
		background-image: linear-gradient(to top, #dfe9f3 0%, white 100%);
	}
</style>