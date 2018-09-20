<template>
	<div class="topBar" :style="topBarStyle">
		<input type="button" value="TEST" @click="testBtn" />
	</div>
</template>

<script>
	import * as Util from 'appRoot/scripts/Util.js';

	export default {
		components: {},
		props:
		{
		},
		data: function ()
		{
			return {
				barWidth: 340,
				left: 0.5,
				vw: 0,
				vh: 0,
				mouseState: {
					x: 0, // Window offset X of cursor
					y: 0, // Window offset Y of cursor
					dragging: false,
					dragStartX: 0, // Window offset X of drag start point
					dragLeftOffsetX: 0, // X of offset of left edge of topBar from cursor
					//dragCenterOffsetX: 0, // X of offset of center of topBar from cursor
				}
			};
		},
		computed:
		{
			topBarStyle()
			{
				return {
					left: 20 + ((this.vw - 340) * this.left) + "px"
				};
			}
		},
		methods:
		{
			testBtn(e)
			{
				toaster.Info("Click!");
			},
			mouseDown(e)
			{
				this.updateMouseCoords(e);
				if (e.target === this.$el)
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
				this.updateMouseCoords(e);
				if (this.mouseState.dragging)
				{
					e.stopPropagation();
					this.moveTopBar(e.pageX);
				}
				this.mouseState.dragging = false;
			},
			mouseMove(e)
			{
				this.updateMouseCoords(e);
				if (this.mouseState.dragging)
					this.moveTopBar(e.pageX);
			},
			touchCancel(e)
			{
				this.updateMouseCoords(e);
				if (this.mouseState.dragging)
					this.moveTopBar(this.mouseState.dragStartX);
				this.mouseState.dragging = false;
			},
			updateMouseCoords(e)
			{
				this.mouseState.x = e.pageX;
				this.mouseState.y = e.pageY;
			},
			moveTopBar(pageX)
			{
				let desiredTopBarLeftPx = (pageX - this.mouseState.dragLeftOffsetX - 20);
				this.left = Util.Clamp(desiredTopBarLeftPx / (this.vw - this.barWidth), 0, 1);
			}
		},
		created()
		{
			this.vw = window.innerWidth;
			window.addEventListener('resize', e =>
			{
				this.vw = window.innerWidth;
				this.vh = window.innerHeight;
			});
			document.addEventListener('mouseup', this.mouseUp);
			document.addEventListener('touchend', this.mouseUp);
			document.addEventListener('touchcancel', this.touchCancel);
			document.addEventListener('mousemove', this.mouseMove);
			document.addEventListener('touchmove', this.mouseMove);
		},
		mounted()
		{
			this.$el.addEventListener('mousedown', this.mouseDown);
			this.$el.addEventListener('touchstart', this.mouseDown);
		},
		beforeDestroy()
		{
			this.$el.removeEventListener('mousedown', this.mouseDown);
			this.$el.removeEventListener('touchstart', this.mouseDown);
			document.removeEventListener('mouseup', this.mouseUp);
			document.removeEventListener('touchend', this.mouseUp);
			document.removeEventListener('touchcancel', this.touchCancel);
			document.removeEventListener('mousemove', this.mouseMove);
			document.removeEventListener('touchmove', this.mouseMove);
		}
	};
</script>

<style scoped>
	.topBar
	{
		width: 300px;
		height: 36px;
		background-color: #FFFFFF;
		filter: drop-shadow(0px 0px 1px #000000);
		position: absolute;
		z-index: 100;
		cursor: ew-resize;
	}

		.topBar > *
		{
			cursor: auto;
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

	input
	{
		font-size: 14px;
	}
</style>