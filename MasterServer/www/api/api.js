export default function ExecJSON(args)
{
	if (!args.session)
		args.session = settings.shrd_session;
	return fetch(appContext.appPath + 'json', {
		method: 'POST',
		headers: {
			'Accept': 'application/json',
			'Content-Type': 'application/json'
		},
		body: JSON.stringify(args)
	}).then(response => response.json()).then(jsonResponse =>
	{
		if (jsonResponse.success)
			return Promise.resolve(jsonResponse);
		else
		{
			if (jsonResponse.error === "missing session" || jsonResponse.error === "invalid session")
			{
				console.log(args.cmd + ' error: "' + jsonResponse.error + '". Redirecting to login.');
				window.location.href = appContext.appPath + "login";
			}
			else if (!(args.cmd === 'login' && jsonResponse.error === 'login challenge'))
				console.error("server json handler returned error response", args, jsonResponse);
			return Promise.reject(new ApiError(jsonResponse.error, jsonResponse));
		}
	}).catch(err =>
	{
		return Promise.reject(err);
	});
}
export class ApiError extends Error
{
	constructor(message, response)
	{
		super(message);
		this.name = "ApiError";
		this.response = response;
	}
}