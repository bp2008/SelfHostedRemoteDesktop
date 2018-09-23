<template>
	<div class="topBar" :style="topBarStyle">
		<TopBarContent />
	</div>
</template>

<script>
	import TopBarContent from 'appRoot/vues/client/remote/TopBarContent.vue';

	export default {
		components: { TopBarContent },
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
					dragging: false,
					dragStartX: 0, // Window offset X of drag start point
					dragLeftOffsetX: 0 // X of offset of left edge of topBar from cursor
				}
			};
		},
		computed:
		{
			topBarStyle()
			{
				return {
					left: 20 + ((this.vw - this.barWidth) * this.left) + "px"
				};
			}
		},
		methods:
		{
			mouseDown(e)
			{
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
				let desiredTopBarLeftPx = (pageX - this.mouseState.dragLeftOffsetX - 20);
				this.left = Util.Clamp(desiredTopBarLeftPx / (this.vw - this.barWidth), 0, 1);
			},
			dblClick(e)
			{
				if (e.target === this.$el)
					this.left = 0.5;
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
			Util.AddEvents(document, "mousemove touchmove", this.mouseMove);
			Util.AddEvents(document, "mouseup touchend", this.mouseUp);
			Util.AddEvents(document, 'touchcancel', this.touchCancel);
		},
		mounted()
		{
			Util.AddEvents(this.$el, "mousedown touchstart", this.mouseDown);
			Util.AddEvents(this.$el, "dblclick", this.dblClick);
			this.barWidth = 40 + this.$el.offsetWidth;
		},
		updated()
		{
			this.barWidth = 40 + this.$el.offsetWidth;
		},
		beforeDestroy()
		{
			Util.RemoveEvents(this.$el, "mousedown touchstart", this.mouseDown);
			Util.RemoveEvents(this.$el, "dblclick", this.dblClick);
			Util.RemoveEvents(document, "mousemove touchmove", this.mouseMove);
			Util.RemoveEvents(document, "mouseup touchend", this.mouseUp);
			Util.RemoveEvents(document, 'touchcancel', this.touchCancel);
		}
	};
</script>

<style scoped>
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
</style>