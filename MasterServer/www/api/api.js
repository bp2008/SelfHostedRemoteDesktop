export default function ExecJSON(args)
{
	if (!args.session)
		args.session = window.myApp.$store.getters.sid;
	return fetch(appContext.appPath + 'json', {
		method: 'POST',
		headers: {
			'Accept': 'application/json',
			'Content-Type': 'application/json'
		},
		body: JSON.stringify(args)
	}).then(response => response.json()).then(data =>
	{
		if (data.success)
			return Promise.resolve(data);
		else
		{
			if (data.error === "missing session" || data.error === "invalid session")
			{
				console.log(args.cmd + ' error: "' + data.error + '". Redirecting to login.');
				window.myApp.$store.commit("SessionLost");
				window.location.href = appContext.appPath + "login";
			}
			else if (!(args.cmd === 'login' && data.error === 'login challenge'))
				console.error("server json handler returned error response", args, data);
			return Promise.reject(new ApiError(data.error, data));
		}
	}).catch(err =>
	{
		return Promise.reject(err);
	});
}
export class ApiError extends Error
{
	constructor(message, data)
	{
		super(message);
		this.name = "ApiError";
		this.data = data;
	}
}