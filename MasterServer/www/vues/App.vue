<template>
	<div class="appRoot">
		<dialogs-wrapper name="dialogDefault"></dialogs-wrapper>
		<router-view></router-view>
	</div>
</template>

<script>

	export default {
		data: function ()
		{
			return {
			};
		},
		computed:
		{
		},
		methods:
		{
		},
		created()
		{
		}
	};
</script>

<style scoped>
	.appRoot
	{
		font-size: 0px;
		height: 100%;
		width: 100%;
	}
</style>