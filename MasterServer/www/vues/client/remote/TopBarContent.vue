<template>
	<div class="topBarContent">
		<DrawerToggleButton :drawerController="drawerController" name="settings">
			<SvgButton sprite="settings" :size="30" padding="0px" borderWidth="0px" />
		</DrawerToggleButton>
		<DrawerToggleButton :drawerController="drawerController" name="zoom">
			<SvgButton sprite="zoom_in" :size="30" padding="0px" borderWidth="0px" />
		</DrawerToggleButton>
		<SvgButton :sprite="fullscreenSprite" :size="30" padding="0px" borderWidth="0px" @click="fullscreenClick" />
	</div>
</template>

<script>
	import SvgButton from 'appRoot/vues/common/controls/SvgButton.vue';
	import DrawerToggleButton from 'appRoot/vues/client/remote/TopBarDrawers/DrawerToggleButton.vue';
	import svg1 from 'appRoot/images/sprite/fullscreen.svg';
	import svg2 from 'appRoot/images/sprite/fullscreen_exit.svg';
	//import svg3 from 'appRoot/images/sprite/chart.svg';
	//import svg4 from 'appRoot/images/sprite/network.svg';
	//import svg5 from 'appRoot/images/sprite/settings_network.svg';
	import svg6 from 'appRoot/images/sprite/settings.svg';
	import svg7 from 'appRoot/images/sprite/zoom_fit.svg';
	import svg8 from 'appRoot/images/sprite/zoom_in.svg';
	import svg9 from 'appRoot/images/sprite/zoom_out.svg';

	import FullscreenController from 'appRoot/scripts/remote/FullscreenController.js';

	export default {
		components: { SvgButton, DrawerToggleButton },
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
			drawerBtnClick(e)
			{
				this.drawerController.drawerName = e.currentTarget.getAttribute('drawerName');
			},
			zoomClick(e)
			{
				this.drawerController.drawerName = 'zoom';
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