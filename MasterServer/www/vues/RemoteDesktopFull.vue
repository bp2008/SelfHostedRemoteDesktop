<template>
	<RemoteDesktopConnection :computerId="computerIdNumber"></RemoteDesktopConnection>
</template>

<script>
	import RemoteDesktopConnection from 'appRoot/vues/client/remote/RemoteDesktopConnection.vue';

	export default {
		components: { RemoteDesktopConnection },
		computed:
		{
			computerIdNumber()
			{
				return parseInt(this.$route.params.computerId);
			}
		}
	};
</script>