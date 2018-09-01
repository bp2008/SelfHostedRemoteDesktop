import Vue from 'vue';
import VueRouter from 'vue-router';

import Login from 'appRoot/vues/Login.vue';
import PublicLayout from 'appRoot/vues/public/PublicLayout.vue';
import PublicHome from 'appRoot/vues/public/PublicHome.vue';
import AdminLayout from 'appRoot/vues/admin/AdminLayout.vue';
import AdminStatus from 'appRoot/vues/admin/AdminStatus.vue';
import AdminComputers from 'appRoot/vues/admin/AdminComputers.vue';
import AdminUsers from 'appRoot/vues/admin/AdminUsers.vue';
import ClientLayout from 'appRoot/vues/client/ClientLayout.vue';
import ClientHome from 'appRoot/vues/client/ClientHome.vue';

Vue.use(VueRouter);

export default function CreateRouter(store, basePath)
{
	const router = new VueRouter({
		mode: 'history',
		routes: [
			{
				path: basePath + '', component: PublicLayout,
				children: [
					{ path: '', component: PublicHome, name: 'publicHome' }
				]
			},
			{
				path: basePath + 'login', component: Login, name: 'login'
			},
			{
				path: basePath + 'client', component: ClientLayout,
				children: [
					{ path: '', redirect: 'home' },
					{ path: 'home', component: ClientHome, name: 'clientHome' }
				]
			},
			{
				path: basePath + 'admin', component: AdminLayout,
				children: [
					{ path: '', redirect: 'status' },
					{ path: 'status', component: AdminStatus, name: 'adminStatus' },
					{ path: 'computers', component: AdminComputers, name: 'adminComputers' },
					{ path: 'users', component: AdminUsers, name: 'adminUsers' }
				]
			}
		],
		$store: store
	});

	router.onError(function (error)
	{
		console.error("Error while routing", error);
		toaster.error('Routing Error', error);
	});

	router.beforeEach((to, from, next) =>
	{
		if (document)
			document.title = "Self Hosted Remote Desktop";

		//store.commit("SetScroll", { key: from.fullPath, value: window.pageYOffset || document.documentElement.scrollTop });

		next();
	});

	return router;
}