<template>
	<div class="topBarContent">
		<SvgButton sprite="settings" :size="30" padding="0px" borderWidth="0px" @click="settingsClick" />
		<SvgButton :sprite="fullscreenSprite" :size="30" padding="0px" borderWidth="0px" @click="fullscreenClick" />
	</div>
</template>

<script>
	import SvgButton from 'appRoot/vues/common/controls/SvgButton.vue';
	import svg1 from 'appRoot/images/sprite/fullscreen.svg';
	import svg2 from 'appRoot/images/sprite/fullscreen_exit.svg';
	//import svg3 from 'appRoot/images/sprite/chart.svg';
	//import svg4 from 'appRoot/images/sprite/network.svg';
	//import svg5 from 'appRoot/images/sprite/settings_network.svg';
	import svg6 from 'appRoot/images/sprite/settings.svg';

	import FullscreenController from 'appRoot/scripts/remote/FullscreenController.js';

	export default {
		components: { SvgButton },
		props:
		{
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