<template>
	<div class="editor">
		<PropEdit v-for="pair in itemPairs" :key="pair.spec.key" :initialValue="pair.initialValue" :spec="pair.spec" @valueChanged="onValueChanged" />
	</div>
</template>

<script>
	import PropEdit from 'appRoot/vues/common/editor/PropEdit.vue';

	export default {
		components: { PropEdit },
		props:
		{
			object: {
				type: Object,
				required: true
			},
			spec: {
				type: Array,
				required: true
			}
		},
		data()
		{
			return {
				specMap: {}
			};
		},
		computed:
		{
			itemPairs()
			{
				let arr = [];
				for (let i = 0; i < this.spec.length; i++)
				{
					let initialValue = this.object[this.spec[i].key];
					if (typeof initialValue === "undefined")
						initialValue = this.spec[i].value;
					arr.push({ initialValue: initialValue, spec: this.spec[i] });
				}
				return arr;
			}
		},
		methods:
		{
			onValueChanged(key, value)
			{
				this.$emit("valueChanged", key, value);
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
		}
	};
</script>

<style scoped>
</style>