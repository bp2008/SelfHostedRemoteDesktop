<template>
	<b-navbar toggleable="sm" type="dark" variant="success" :sticky="true">

		<b-navbar-brand href="#" variant="faded">{{systemName}}</b-navbar-brand>

		<b-navbar-toggle target="nav_collapse"></b-navbar-toggle>

		<b-collapse is-nav id="nav_collapse">

			<b-navbar-nav>
				<b-nav-item :to="{ name: 'adminStatus' }">Status</b-nav-item>
				<b-nav-item :to="{ name: 'adminComputers' }">Computers</b-nav-item>
				<b-nav-item :to="{ name: 'adminUsers' }">Users</b-nav-item>
			</b-navbar-nav>

			<!-- Right aligned nav items -->
			<b-navbar-nav class="ml-auto">
				<b-nav-item-dropdown right>
					<!-- Using button-content slot -->
					<template slot="button-content">
						<em>User</em>
					</template>
					<b-dropdown-item :to="{ name: 'clientHome' }">Client App</b-dropdown-item>
					<b-dropdown-item-button @click="logoutClicked">Log Out</b-dropdown-item-button>
				</b-nav-item-dropdown>
			</b-navbar-nav>

		</b-collapse>
	</b-navbar>
</template>

<script>
	import ExecJSON from 'appRoot/api/api.js';
	export default {
		data: function ()
		{
			return {
				systemName: appContext.systemName
			};
		},
		computed:
		{
		},
		methods:
		{
			logoutClicked()
			{
				ExecJSON({ cmd: "logout" }).finally(() =>
				{
					this.$router.push({ name: "publicHome" });
				});
			}
		},
		created()
		{
		}
	}
</script>

<style scoped>
	.smallText
	{
		font-size: 0.7em;
	}
	/*nav
	{
		display: flex;
		flex-wrap: wrap;
		flex-direction: column;
		background-color: #808080;
		padding: 15px 30px;
	}

		nav *
		{
			margin: 0px 10px;
			font-size: 20px;
			color: #000000;
			text-decoration: none;
		}

	@media (min-width: 600px)
	{
		nav
		{
			flex-direction: row;
		}
	}*/
</style>