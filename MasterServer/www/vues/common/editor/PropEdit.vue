<template>
	<div class="prop">
		<label :for="inputId" v-html="spec.label">
		</label>: {{myValue}}
		<input id="inputId" v-model="myValue" :type="spec.inputType" :min="spec.min" :max="spec.max" @change="onChange" @dblclick="onDoubleClick" />
	</div>
</template>

<script>

	export default {
		components: {},
		props:
		{
			initialValue: {
				required: true
			},
			spec: {
				type: Object, // An object containing the item key, default value, and other metadata.
				required: true
			}
		},
		data()
		{
			return {
				myUid: GetUid(),
				myValue: this.initialValue
			};
		},
		computed:
		{
			inputId()
			{
				return "input_" + this.myUid;
			},
			myValueTypeEnforced()
			{
				if (this.spec.inputType === "number" || this.spec.inputType === "range")
					return new Number(this.myValue.toString()).valueOf();
				else
					return this.myValue;
			}
		},
		methods:
		{
			onChange(e)
			{
				this.$emit("valueChanged", this.spec.key, this.myValueTypeEnforced);
			},
			onDoubleClick(e)
			{
				this.myValue = this.spec.value;
				this.onChange(e);
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
	label
	{
	}

	input[type="checkbox"]
	{
		display: block;
		margin-left: 15px;
	}

	input[type="text"],
	input[type="number"],
	input[type="range"],
	input[type="color"]
	{
		display: block;
		width: 100%;
	}
</style>