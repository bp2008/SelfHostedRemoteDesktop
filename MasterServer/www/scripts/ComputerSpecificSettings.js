export function GetDefaultComputerSettings()
{
	return [
		{
			label: "Jpeg Quality"
			, key: "quality"
			, value: 25
			, inputType: "range"
			, min: 1
			, max: 100
		}
		, {
			label: "Color Quality"
			, key: "colorDetail"
			, value: 1
			, inputType: "range"
			, min: 0
			, max: 3
		}
		, {
			label: "Max Frames Per Second"
			, key: "maxFps"
			, value: 15
			, inputType: "range"
			, min: 1
			, max: 60
		}
		, {
			label: "Max Frames in Transit"
			, key: "maxFramesInTransit"
			, value: 3
			, inputType: "range"
			, min: 1
			, max: 60
		}
	];
}
export function ComputerSpecificSettings()
{
	let self = this;

	// Load default settings
	let defaultSettings = GetDefaultComputerSettings();
	for (var i = 0; i < defaultSettings.length; i++)
		self[defaultSettings[i].key] = defaultSettings[i].value;
}