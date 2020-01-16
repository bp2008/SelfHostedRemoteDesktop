<template>
	<div>
		<Editor v-if="mySettings" :object="mySettings" :spec="settingsSpec" @valueChanged="onValueChanged" />
		<div v-else>{{status}}</div>
	</div>
</template>
<script>
	import Editor from 'appRoot/vues/common/editor/Editor.vue';
	import { GetDefaultComputerSettings } from 'appRoot/scripts/ComputerSpecificSettings.js';
	export default {
		components: { Editor },
		props:
		{
			computer: {
				type: Object,
				required: true
			}
		},
		data()
		{
			return {
				status: "Loading…",
				mySettings: null,
				settingsSpec: GetDefaultComputerSettings()
			};
		},
		methods:
		{
			onValueChanged(key, value)
			{
				console.log("onValueChanged", key, value);
				this.$store.dispatch("setComputerSpecificSetting", { computerId: this.computer.ID, key, value });
			}
		},
		created()
		{
			this.$store.dispatch('getComputerSpecificSettings', this.computer.ID).then(cs =>
			{
				this.mySettings = cs;
			}
			).catch(err =>
			{
				toaster.error(err);
				this.status = err.message;
			});
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
</style>
