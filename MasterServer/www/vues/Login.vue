<template>
	<div class="loginRoot">
		<div class="loginPanel">
			<div class="systemName">{{systemName}}</div>
			<input type="text" class="txtUser" v-model="user" placeholder="User Name" v-on:keyup.enter="TryLogin" />
			<input type="password" class="txtPass" v-model="pass" placeholder="Password" v-on:keyup.enter="TryLogin" />
			<input type="button" class="btnLogin" @click="TryLogin" value="Log in" :disabled="!loginEnabled" on:keyup.space.enter="TryLogin" />
		</div>
		<Footer />
	</div>
</template>
<script>
	import ExecJSON from 'appRoot/api/api.js';
	import bcrypt from 'appRoot/scripts/bcrypt.min.js';
	import Footer from 'appRoot/vues/common/Footer.vue';

	export default {
		components: { Footer },
		data: function ()
		{
			return {
				systemName: appContext.systemName,
				user: "admin",
				pass: "admin",
				lastUsedUser: null,
				lastUsedPass: null,
				lastSalt: null,
				loginEnabled: true
			};
		},
		computed:
		{
		},
		methods:
		{
			TryLogin()
			{
				if (!this.loginEnabled)
					return;
				this.loginEnabled = false;
				this.lastUsedUser = this.user;
				this.lastUsedPass = this.pass;
				this.lastSalt = null;
				var args = { cmd: "login", user: this.lastUsedUser };
				ExecJSON(args).then(data =>
				{
					toaster.error("Login Error", "Login protocol failed. Server did not return expected challenge.");
					this.loginEnabled = true;
				}
				).catch(err =>
				{
					if (err.message !== "login challenge")
					{
						toaster.error("Login Error", err);
						this.loginEnabled = true;
						return;
					}

					let data = err.data;
					args.session = data.session;
					this.$store.commit("SetSid", data.session);
					this.lastSalt = data.salt;
					try
					{
						// Use BCrypt on the password, using the salt provided by the server.
						var bCryptResult = bcrypt.hashSync(this.lastUsedPass, data.salt);
						// Compute SHA512 so we have the desired output size for later XORing
						var bCryptResultHex = Util.bytesToHex(Util.stringToUtf8ByteArray(bCryptResult));
						var onceHashedPw = Util.ComputeSHA512Hex(bCryptResultHex);
						// We prove our identity by transmitting onceHashedPw to the server.
						// However we won't do that in plain text.
						// Hash one more time; PasswordHash is the value remembered by the server
						var PasswordHash = Util.ComputeSHA512Hex(onceHashedPw);
						var challengeHashed = Util.ComputeSHA512Hex(PasswordHash + data.challenge);
						args.response = Util.XORHexStrings(challengeHashed, onceHashedPw);
					}
					catch (ex)
					{
						toaster.error("Login Error", ex);
						this.loginEnabled = true;
						return;
					}
					ExecJSON(args).then(data =>
					{
						this.$store.commit("SessionAuthenticated", { sid: data.session, settingsKey: data.settingsKey });
						this.HandleSuccessfulLogin(data);
					}
					).catch(err =>
					{
						toaster.error(err.message);
					}
					).finally(() =>
					{
						this.loginEnabled = true;
					});
				});
			},
			HandleSuccessfulLogin(response)
			{
				if (response.admin)
					this.$router.push({ name: "adminStatus" });
				else
					this.$router.push({ name: "clientHome" });
			}
		},
		created()
		{
		}
	}
</script>
<style scoped>
	.loginRoot
	{
		font-size: 16px;
		display: flex;
		flex-direction: column;
		justify-content: space-between;
		align-items: center;
		width: 100%;
		height: 100%;
		/*background-image: linear-gradient(120deg, #84fab0 0%, #8fd3f4 100%);*/
		background-image: linear-gradient(120deg, #a1c4fd 0%, #c2e9fb 100%);
	}

	.loginPanel
	{
		margin-top: auto;
		padding: 20px 20px;
		background-color: rgba(255,255,255,0.25);
		border: 1px solid rgba(0,0,0,1);
		border-radius: 8px;
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
	}

	.systemName, .txtUser, .txtPass, .btnLogin
	{
		width: 200px;
	}

	.systemName
	{
		margin-bottom: 15px;
		text-align: center;
		overflow-x: hidden;
		word-break: break-word;
	}

	.txtUser, .txtPass, .btnLogin
	{
		display: block;
		border: 1px solid gray;
		border-radius: 3px;
		padding: 2px 4px;
	}

	.txtUser
	{
		border-bottom-left-radius: 0px;
		border-bottom-right-radius: 0px;
	}

	.txtPass
	{
		border-top-width: 0px;
		border-top-left-radius: 0px;
		border-top-right-radius: 0px;
	}

	.btnLogin
	{
		margin-top: 10px;
		cursor: pointer;
		background-image: linear-gradient(to top, #e6e9f0 0%, #eef1f5 100%);
	}

		.btnLogin:hover
		{
			background-image: linear-gradient(to top, #f0f0f3 0%, #f3f7fb 100%);
		}

		.btnLogin:active
		{
			background-image: linear-gradient(to top, #ffffff 0%, #ffffff 100%);
		}
</style>
