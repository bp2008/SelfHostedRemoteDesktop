import Vue from 'vue';
import App from 'appRoot/vues/App.vue';
import CreateStore from 'appRoot/store/store.js';
import CreateRouter from 'appRoot/router/index.js';

var ModalDialogs = require('vue-modal-dialogs');
Vue.use(ModalDialogs);

import '@deveodk/vue-toastr/dist/@deveodk/vue-toastr.css';
import VueToastr from '@deveodk/vue-toastr';
Vue.use(VueToastr);

// https://bootstrap-vue.js.org/docs/
import BootstrapVue from 'bootstrap-vue';
Vue.use(BootstrapVue);
import 'bootstrap/dist/css/bootstrap.css';
import 'bootstrap-vue/dist/bootstrap-vue.css';

import VueGoodTablePlugin from 'vue-good-table';
import 'vue-good-table/dist/vue-good-table.css';
Vue.use(VueGoodTablePlugin);

import ScaleLoader from 'appRoot/vues/common/ScaleLoader.vue';
Vue.component('ScaleLoader', ScaleLoader);

// Any recursively nested components must be globally registered here
//Vue.component('Example', require('Example.vue').default);

let store = window.store = CreateStore();
let router = window.router = CreateRouter(store, appContext.appPath);
let myApp = window.myApp = new Vue({
	store,
	router,
	...App
});

import ToasterHelper from 'appRoot/scripts/ToasterHelper.js';
window.toaster = new ToasterHelper(myApp.$toastr);

import { globalSettings } from 'appRoot/scripts/LocalSettings.js';
window.settings = globalSettings;

import * as Util from 'appRoot/scripts/Util.js';
window.Util = Util;

router.onReady(() =>
{
	const matchedComponents = router.getMatchedComponents();

	if (matchedComponents.length < 1)
		window.location.replace(appContext.appPath + "404.html");

	myApp.$mount('#app');
});