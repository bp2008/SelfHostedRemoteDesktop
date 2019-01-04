<template>
	<div v-if="drawerController.drawerName">
		<SettingsDrawer v-if="drawerController.drawerName === 'settings'" :computer="computer" />
		<ZoomDrawer v-else-if="drawerController.drawerName === 'zoom'" />
	</div>
</template>

<script>
	import SettingsDrawer from 'appRoot/vues/client/remote/TopBarDrawers/SettingsDrawer.vue';
	import ZoomDrawer from 'appRoot/vues/client/remote/TopBarDrawers/ZoomDrawer.vue';

	import FullscreenController from 'appRoot/scripts/remote/FullscreenController.js';

	export default {
		components: { SettingsDrawer, ZoomDrawer },
		props:
		{
			computer: {
				type: Object,
				default: null
			},
			drawerController: {
				type: Object,
				required: true
			}
		},
		data: function ()
		{
			return {
				fullscreenController: new FullscreenController(this.onFullscreenChange),
				fullscreenSprite: 'fullscreen'
			};
		},
		computed:
		{
		},
		methods:
		{
			settingsClick(e)
			{
				toaster.Warning("Settings is not implemented");
				this.$emit('drawerChange', 'settings');
			},
			zoomClick(e)
			{
				this.$emit('drawerChange', 'zoom');
			},
			onFullscreenChange(e)
			{
				this.fullscreenSprite = this.fullscreenController.isFullScreen() ? "fullscreen_exit" : "fullscreen";
			},
			fullscreenClick(e)
			{
				this.fullscreenController.toggleFullScreen();
			}
		},
		created()
		{
		},
		mounted()
		{
		},
		beforeDestroy()
		{
		}
	};
</script>

<style scoped>
	.topBarContent
	{
		display: inline-block;
		box-sizing: border-box;
		margin-top: 3px;
		max-width: 300px;
		height: 30px;
		cursor: auto;
	}

		.topBarContent > *
		{
			margin-left: 3px;
		}

			.topBarContent > *:last-child
			{
				margin-right: 3px;
			}

	input
	{
		font-size: 14px;
	}
</style>