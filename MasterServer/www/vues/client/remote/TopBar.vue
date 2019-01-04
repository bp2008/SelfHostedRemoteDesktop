<template>
	<div class="topBarWrapper">
		<div ref="topBar" class="topBar" :style="topBarStyle">
			<TopBarContent @drawerChange="onDrawerChange" :computer="computer" :drawerController="drawerController" />
		</div>
		<TopBarDrawer class="topBarDrawer" :style="topBarDrawerStyle" :computer="computer" :drawerController="drawerController" />
	</div>
</template>
<script>
	import TopBarContent from 'appRoot/vues/client/remote/TopBarContent.vue';
	import TopBarDrawer from 'appRoot/vues/client/remote/TopBarDrawer.vue';
	export default {
		components: { TopBarContent, TopBarDrawer },
		props:
		{
			computer: {
				type: Object,
				default: null
			}
		},
		data: function ()
		{
			return {
				barWidth: 340,
				position: 0.5,
				vw: 0,
				vh: 0,
				mouseState: {
					dragging: false,
					dragStartX: 0, // Window offset X of drag start point
					dragLeftOffsetX: 0 // X of offset of left edge of topBar from cursor
				},
				drawerController: {
					drawerName: ""
				}
			};
		},
		computed:
		{
			topBarLeftPx()
			{
				return 20 + ((this.vw - this.barWidth) * this.position);
			},
			topBarStyle()
			{
				return {
					left: this.topBarLeftPx + "px"
				};
			},
			topBarDrawerStyle()
			{
				let topBarWidth = this.$refs.topBar ? this.$refs.topBar.offsetWidth : 0;
				let topBarCenter = this.topBarLeftPx + (topBarWidth / 2);
				return {
					left: Util.Clamp(topBarCenter - 150, 0, this.vw - 300) + "px",
					maxHeight: (this.vh - 38) + "px"
				};
			}
		},
		methods:
		{
			mouseDown(e)
			{
				if (e.target === this.$refs.topBar)
				{
					this.mouseState.dragging = true;
					this.mouseState.dragLeftOffsetX = e.offsetX;
					this.mouseState.dragStartX = e.pageX;
					e.preventDefault();
					e.stopPropagation();
					e.stopImmediatePropagation();
				}
			},
			mouseUp(e)
			{
				if (this.mouseState.dragging)
				{
					e.stopPropagation();
					this.moveTopBar(e.pageX);
				}
				this.mouseState.dragging = false;
			},
			mouseMove(e)
			{
				if (this.mouseState.dragging)
					this.moveTopBar(e.pageX);
			},
			touchCancel(e)
			{
				if (this.mouseState.dragging)
					this.moveTopBar(this.mouseState.dragStartX);
				this.mouseState.dragging = false;
			},
			moveTopBar(pageX)
			{
				let desiredTopBarLeftPx = pageX - this.mouseState.dragLeftOffsetX - 20;
				this.position = Util.Clamp(desiredTopBarLeftPx / (this.vw - this.barWidth), 0, 1);
			},
			dblClick(e)
			{
				if (e.target === this.$refs.topBar)
					this.position = 0.5;
			},
			onDrawerChange(drawerName)
			{
				if (drawerName === this.drawerName)
				{
					this.drawerName = null;
					return;
				}
				this.drawerName = drawerName;
			}
		},
		created()
		{
			this.vw = window.innerWidth;
			this.vh = window.innerHeight;
			window.addEventListener('resize', e =>
			{
				this.vw = window.innerWidth;
				this.vh = window.innerHeight;
			});
			Util.AddEvents(document, "mousemove touchmove", this.mouseMove);
			Util.AddEvents(document, "mouseup touchend", this.mouseUp);
			Util.AddEvents(document, 'touchcancel', this.touchCancel);
		},
		mounted()
		{
			Util.AddEvents(this.$refs.topBar, "mousedown touchstart", this.mouseDown);
			Util.AddEvents(this.$refs.topBar, "dblclick", this.dblClick);
			this.barWidth = 40 + this.$refs.topBar.offsetWidth;
		},
		updated()
		{
			this.barWidth = 40 + this.$refs.topBar.offsetWidth;
		},
		beforeDestroy()
		{
			Util.RemoveEvents(this.$refs.topBar, "mousedown touchstart", this.mouseDown);
			Util.RemoveEvents(this.$refs.topBar, "dblclick", this.dblClick);
			Util.RemoveEvents(document, "mousemove touchmove", this.mouseMove);
			Util.RemoveEvents(document, "mouseup touchend", this.mouseUp);
			Util.RemoveEvents(document, 'touchcancel', this.touchCancel);
		}
	};
</script>
<style scoped>
	.topBarWrapper
	{
		width: 0px;
		height: 0px;
	}

	.topBar
	{
		height: 36px;
		background-color: #FFFFFF;
		filter: drop-shadow(0px 0px 1px #000000);
		position: absolute;
		z-index: 100;
		cursor: ew-resize;
		user-select: false;
	}

		.topBar > *
		{
		}

		.topBar:before,
		.topBar:after
		{
			content: '';
			position: absolute;
			width: 0px;
			height: 0px;
			border-top: 36px solid #FFFFFF;
		}

		.topBar:before
		{
			right: 100%;
			border-left: 18px solid transparent;
		}

		.topBar:after
		{
			left: 100%;
			border-right: 18px solid transparent;
		}

	.topBarDrawer
	{
		position: absolute;
		padding: 2px 4px;
		top: 36px;
		z-index: 110;
		border: 1px solid #666666;
		width: 300px;
		background-color: #FFFFFF;
		box-shadow: inset 0px 0px 2px rgba(0,0,0,0.5);
		font-size: 10pt;
	}
</style>
