var defaultGlobalSettings =
	[
		{
			key: "shrd_session"
			, value: ""
		}
	];
var defaultClientSettings =
	[
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
function LoadDefaultSettings(settingsObj, defaultSettingsObj)
{
	for (var i = 0; i < defaultSettingsObj.length; i++)
	{
		if (settingsObj.getItem(defaultSettingsObj[i].key) === null)
			settingsObj.setItem(defaultSettingsObj[i].key, defaultSettingsObj[i].value);
	}
}
var localStorageDummy = null;
function GetDummyLocalStorage()
{
	if (localStorageDummy === null)
	{
		var dummy = new Object();
		dummy.getItem = function (key)
		{
			return dummy[key];
		};
		dummy.setItem = function (key, value)
		{
			return (dummy[key] = value);
		};
		localStorageDummy = dummy;
	}
	return localStorageDummy;
}
function GetNamespaceLocalStorage(namespace)
{
	if (!ValidateNamespaceSimpleRules(namespace))
	{
		toaster.Warning("Unable to validate namespace name \"" + htmlEncode(namespace) + "\". Settings for this client will not be persisted.", 10000);
		var storageObj = GetDummyLocalStorage();
		LoadDefaultSettings(storageObj, defaultClientSettings);
		return storageObj;
	}

	var prefix = "shrd_" + namespace.toLowerCase().replace(/ /g, '_') + "_";

	var wrappedStorage = new Object();
	wrappedStorage.getItem = function (key)
	{
		return localStorage[prefix + key];
	};
	wrappedStorage.setItem = function (key, value)
	{
		return (localStorage[prefix + key] = value);
	};
	for (var i = 0; i < defaultClientSettings.length; i++)
	{
		var tmp = function (key)
		{
			Object.defineProperty(wrappedStorage, key,
				{
					get: function ()
					{
						return wrappedStorage.getItem(key);
					},
					set: function (value)
					{
						return wrappedStorage.setItem(key, value);
					}
				});
		}(defaultClientSettings[i].key);
	}
	LoadDefaultSettings(wrappedStorage, defaultClientSettings);
	return wrappedStorage;
}
function ValidateNamespaceSimpleRules(val)
{
	if (val.length === 0)
		return false;
	if (val.length > 16)
		return false;
	for (var i = 0; i < val.length; i++)
	{
		var c = val.charAt(i);
		if ((c < "a" || c > "z") && (c < "A" || c > "Z") && (c < "0" || c > "9") && c !== " ")
			return false;
	}
	return true;
}
function GetClientSettingsDef(key)
{
	for (var i = 0; i < defaultClientSettings.length; i++)
	{
		if (defaultClientSettings[i].key === key)
			return defaultClientSettings[i];
	}
}
var globalSettings = localStorage;
LoadDefaultSettings(globalSettings, defaultGlobalSettings);

export { globalSettings, GetNamespaceLocalStorage };