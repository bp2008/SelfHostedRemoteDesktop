<template>
	<RemoteDesktopConnection :computerId="$route.params.computerId"></RemoteDesktopConnection>
</template>

<script>
	import RemoteDesktopConnection from 'appRoot/vues/client/remote/RemoteDesktopConnection.vue';

	export default {
		components: { RemoteDesktopConnection }
	};
</script>