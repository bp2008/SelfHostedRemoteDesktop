import Vue from 'vue';
import Vuex from 'vuex';
Vue.use(Vuex);
import createPersistedState from 'vuex-persistedstate';
import ExecJSON from 'appRoot/api/api.js';
import AES from "crypto-js/aes";
import encUtf8 from "crypto-js/enc-utf8";
import { ComputerSpecificSettings } from 'appRoot/scripts/ComputerSpecificSettings.js';

export default function CreateStore()
{
	return new Vuex.Store({
		strict: true, // TODO: Disable 'strict' for releases to improve performance
		plugins: [createPersistedState({
			storage: window.sessionStorage,
			setState: SetState,
			getState: GetState
		})],
		state: {
			appPath: "/",
			sid: "",
			clientComputerGroups: [],
			settingsEncryptionKey: null,
			computerSpecificSettings: {}
			//$scroll: new Object() // $ = non reactive.
		},

		mutations: { // mutations must not be async
			InitAppPath(state, appPath)
			{
				state.appPath = appPath;
			},
			//SetScroll: (state, { key, value }) =>
			//{
			//	state.$scroll[key] = value;
			//},
			SessionLost(state)
			{
				state.sid = "";
				state.settingsEncryptionKey = null;
				state.computerSpecificSettings = null;
			},
			SessionAuthenticated(state, { sid, settingsKey })
			{
				state.sid = sid;
				state.settingsEncryptionKey = settingsKey;
				if (settingsKey)
				{
					let cs = localStorage.getItem("shrd_computerSpecificSettings");
					if (cs)
						cs = DecryptObject(cs, settingsKey);
					if (cs)
						state.computerSpecificSettings = cs;
					else
						state.computerSpecificSettings = null;
				}
			},
			SetSid(state, sid)
			{
				state.sid = sid;
			},
			SetClientComputerGroups(state, clientComputerGroups)
			{
				state.clientComputerGroups = clientComputerGroups;
			},
			CreateComputerSpecificSettings(state, computerId)
			{
				if (!state.computerSpecificSettings)
					state.computerSpecificSettings = {};
				if (!state.computerSpecificSettings[computerId])
					Vue.set(state.computerSpecificSettings, computerId, new ComputerSpecificSettings());
			},
			SetComputerSpecificSetting(state, { computerId, key, value })
			{
				if (!state.computerSpecificSettings)
					state.computerSpecificSettings = {};
				if (!state.computerSpecificSettings[computerId])
					Vue.set(state.computerSpecificSettings, computerId, new ComputerSpecificSettings());
				Vue.set(state.computerSpecificSettings[computerId], key, value);
			}
		},
		actions: { // actions can be async
			getClientComputerGroups(store)
			{
				return ExecJSON({ cmd: "getComputers" }).then(data =>
				{
					store.commit("SetClientComputerGroups", data.Groups);
					return Promise.resolve(data.Groups);
				}
				).catch(err =>
				{
					return Promise.reject(err);
				});
			},
			getClientComputerInfo(store, computerId)
			{
				let computer = FindComputer(store, computerId);
				if (computer)
					return Promise.resolve(computer);
				return store.dispatch("getClientComputerGroups").then(() =>
				{
					let computer = FindComputer(store, computerId);
					if (computer)
						return Promise.resolve(computer);
					else
						return Promise.reject(new Error("Could not find computer with ID " + computerId));
				});
			},
			getComputerSpecificSettings(store, computerId)
			{
				return store.dispatch('getClientComputerInfo', computerId).then(computer =>
				{
					if (!store.state.computerSpecificSettings || !store.state.computerSpecificSettings[computerId])
						store.commit("CreateComputerSpecificSettings", computerId);
					return Promise.resolve(store.state.computerSpecificSettings[computerId]);
				});
			},
			setComputerSpecificSetting(store, { computerId, key, value })
			{
				return store.dispatch('getComputerSpecificSettings', computerId).then(cs =>
				{
					store.commit("SetComputerSpecificSetting", { computerId, key, value });
					return Promise.resolve();
				});
			}
		},
		getters: {
			sid(state)
			{
				return state.sid;
			},
			getComputerSpecificSettings(state)
			{
				return computerId => state.computerSpecificSettings[computerId];
			}
		}
	});
}
function SetState(storeName, state, storage)
{
	console.log("SetState", storeName);
	storeName = "shrd";
	for (let key in state)
	{
		if (state.hasOwnProperty(key))
		{
			let value = state[key];
			if (key === "computerSpecificSettings")
			{
				// Computer-specific settings are kept encrypted in localStorage.
				if (state.settingsEncryptionKey)
				{
					if (value)
					{
						let enc = EncryptObject(value, state.settingsEncryptionKey);
						if (enc)
							localStorage.setItem(storeName + "_computerSpecificSettings", enc);
						else
							console.error("Failed to encrypt computer-specific settings");
					}
				}
			}
			else
				storage.setItem(storeName + "_" + key, JSON.stringify(value));
		}
	}
}
function GetState(storeName, storage, value)
{
	console.log("GetState", storeName);
	storeName = "shrd";
	try
	{
		let newObj = new Object();
		for (let i = 0; i < storage.length; i++)
		{
			let key = storage.key(i);
			if (key.substr(0, storeName.length + 1) === storeName + "_")
			{
				value = storage.getItem(key);
				if (typeof value !== 'undefined')
					newObj[key.substr(storeName.length + 1)] = JSON.parse(value);
			}
		}
		if (newObj.settingsEncryptionKey)
		{
			let computerSpecificSettings = localStorage.getItem(storeName + "_computerSpecificSettings");
			if (computerSpecificSettings)
				computerSpecificSettings = DecryptObject(computerSpecificSettings, newObj.settingsEncryptionKey);
			if (computerSpecificSettings)
				newObj["computerSpecificSettings"] = computerSpecificSettings;
		}
		return newObj;
	}
	catch (ex)
	{
		console.error(ex); // toaster is not always available here.
	}

	return undefined;
}
function EncryptObject(obj, key)
{
	try
	{
		let plainStr = JSON.stringify(obj);
		let encrypted = AES.encrypt(plainStr, key);
		let base64 = encrypted.toString();
		return base64;
	}
	catch (ex)
	{
		console.error(ex);
		return "";
	}
}
function DecryptObject(base64, key)
{
	try
	{
		let plain = AES.decrypt(base64, key);
		let plainStr = plain.toString(encUtf8);
		if (!plainStr)
			return null;
		return JSON.parse(plainStr);
	}
	catch (ex)
	{
		console.error(ex);
		return null;
	}
}
function FindComputer(store, computerId)
{
	for (let i = 0; i < store.state.clientComputerGroups.length; i++)
	{
		let group = store.state.clientComputerGroups[i];
		let computer = group.Computers.find(c => c.ID === computerId);
		if (computer)
			return computer;
	}
	return null;
}