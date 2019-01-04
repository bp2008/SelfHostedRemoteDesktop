<template>
	<footer>Self-Hosted Remote Desktop {{serverVersion}}</footer>
</template>

<script>

	export default {
		data: function ()
		{
			return {
				serverVersion: appContext.serverVersion
			};
		}
	}
</script>

<style scoped>
	footer
	{
		margin-top: auto;
		padding: 10px;
		width: 100%;
		text-align: center;
		/*background-color: rgba(255,255,255,0.25);*/
		/*box-shadow: 0 0 16px rgba(0,0,0,0.25);*/
	}
</style>