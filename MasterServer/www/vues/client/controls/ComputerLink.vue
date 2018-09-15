<template>
	<router-link :to="{ name: 'clientComputerHome', params: { computerId: computer.ID } }" class="computerLink">
		<Computer :computer="computer" class="linkedComputer" />
	</router-link>
</template>

<script>
	import Computer from 'appRoot/vues/client/controls/Computer.vue';

	export default {
		components: { Computer },
		props: {
			computer: {
				type: Object,
				default: {}
			}
		}
	};
</script>

<style scoped>
	.computerLink
	{
		text-decoration: none;
		color: #000000;
	}

	.linkedComputer:hover
	{
		background-color: rgba(0,0,127,0.05);
	}
</style>