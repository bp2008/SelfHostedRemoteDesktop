<template>
	<div class="homeRoot">
		<p class="welcome">Welcome to {{systemName}} Remote Support</p>
		<p><a class="getHelp" href="javascript:void(0)" @click="GetHelp">Get Help</a></p>
		<p><router-link :to="{ name: 'login' }">Log in</router-link></p>
	</div>
</template>

<script>

	export default {
		data: function ()
		{
			return {
			};
		},
		computed: {
			systemName()
			{
				return appContext.systemName;
			}
		},
		methods: {
			GetHelp()
			{
				toaster.error("Error", "Get Help is not implemented");
			}
		},
		created()
		{
		}
	}
</script>

<style scoped>
	.homeRoot
	{
		width: 100%;
		text-align: center;
	}

	.welcome
	{
		font-size: 24px;
	}

	.getHelp
	{
		display: inline-block;
		padding: 20px 60px;
		background-color: #00b100;
		color: #FFFFFF;
		font-size: 24px;
		font-weight: bold;
		cursor: pointer;
		text-decoration: none;
		border: 2px outset #00FF00;
	}

		.getHelp:focus
		{
			outline-width: thick;
			outline-style: solid;
		}

		.getHelp:hover
		{
			background-color: #06c106;
		}

		.getHelp:active
		{
			background-color: #1bf11b;
			border-style: inset;
		}
</style>