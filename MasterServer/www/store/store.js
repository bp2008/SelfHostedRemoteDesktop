import Vue from 'vue';
import Vuex from 'vuex';
Vue.use(Vuex);
import createPersistedState from 'vuex-persistedstate';
import ExecJSON from 'appRoot/api/api.js';

export default function CreateStore()
{
	return new Vuex.Store({
		strict: true, // TODO: Disable 'strict' for releases to improve performance
		plugins: [createPersistedState({ storage: window.sessionStorage })],
		state: {
			appPath: "/",
			sid: "",
			clientComputerGroups: []
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
			SetSid(state, sid)
			{
				state.sid = sid;
			},
			SetClientComputerGroups(state, clientComputerGroups)
			{
				state.clientComputerGroups = clientComputerGroups;
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
			}
		},
		getters: {
			sid(state)
			{
				return state.sid;
			}
		}
	});
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