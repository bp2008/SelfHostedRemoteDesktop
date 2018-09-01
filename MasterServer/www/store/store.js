import Vue from 'vue';
import Vuex from 'vuex';
Vue.use(Vuex);
import createPersistedState from 'vuex-persistedstate';

export default function CreateStore()
{
	return new Vuex.Store({
		strict: true, // TODO: Disable 'strict' for releases to improve performance
		plugins: [createPersistedState({ storage: window.sessionStorage })],
		state: {
			appPath: "/",
			sid: "",
			//$scroll: new Object() // $ = non reactive.
		},

		mutations: {
			InitAppPath: (state, appPath) =>
			{
				state.appPath = appPath;
			},
			//SetScroll: (state, { key, value }) =>
			//{
			//	state.$scroll[key] = value;
			//},
			SetSid: (state, sid) =>
			{
				state.sid = sid;
			}
		},
		actions: { // actions can be async
		},
		getters: {
			sid: function (state)
			{
				return state.sid;
			}
		}
	});
};