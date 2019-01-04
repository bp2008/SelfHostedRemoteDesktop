<template>
	<div :class="{ drawerToggleButton: true, active: drawerController.drawerName === name }" @click="toggle">
		<slot></slot>
	</div>
</template>

<script>
	export default {
		components: {},
		props:
		{
			name: {
				type: String,
				required: true
			},
			drawerController: {
				type: Object,
				required: true
			}
		},
		data: function ()
		{
			return {
			};
		},
		methods:
		{
			toggle()
			{
				if (this.drawerController.drawerName === this.name)
					this.drawerController.drawerName = null;
				else
					this.drawerController.drawerName = this.name;
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
		},
		computed:
		{
		}
	};
</script>

<style scoped>
	.drawerToggleButton
	{
		display: inline-block;
		font-size: 0px;
	}

		.drawerToggleButton.active
		{
			background-color: rgba(0,0,0,0.05);
			outline: 2px auto;
		}
</style>