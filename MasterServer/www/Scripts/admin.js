/// <reference path="jquery-3.1.1.js" />
/// <reference path="jquery.ba-bbq.min.js" />
/// <reference path="toastr.min.js" />
/// <reference path="shared.js" />
"use strict";
///////////////////////////////////////////////////////////////
// Persistent Settings ////////////////////////////////////////
///////////////////////////////////////////////////////////////
var settings = localStorage;
var defaultSettings =
	[
		{
			key: "shrd_session"
			, value: ""
		}
	];
function LoadDefaultSettings()
{
	for (var i = 0; i < defaultSettings.length; i++)
	{
		if (settings.getItem(defaultSettings[i].key) == null)
			settings.setItem(defaultSettings[i].key, defaultSettings[i].value);
	}
}
LoadDefaultSettings();
///////////////////////////////////////////////////////////////
// Page Loading ///////////////////////////////////////////////
///////////////////////////////////////////////////////////////
$(function ()
{
	LoadMainMenu();
	$(window).bind('hashchange', function (e)
	{
		var page = $.bbq.getState("page") || "login";
		$.post("admin/" + page, { session: settings.shrd_session })
			.done(function (response)
			{
				document.title = GetPageDisplayName(page) + " - Administration - Self Hosted Remote Desktop";
				$("#layout_body").html(response);
				PageLoaded(page);
			})
			.fail(function (jqXHR, textStatus, errorThrown)
			{
				if (jqXHR.status == 403)
				{
					LoadPage("login");
				}
				else
				{
					document.title = "Error - Administration - Self Hosted Remote Desktop";
					$("#layout_body").html('<span style="font-weight: bold; font-size: 2em;">' + textStatus
						+ '</span><br/><br/><span style="font-size: 1.5em;">' + jqXHR.status
						+ ' ' + errorThrown + '</span><br/><br/>Please refresh this page.');
				}
			});
	})
	$(window).trigger('hashchange');
});
function GetPageDisplayName(page)
{
	if (page == "login")
		return "Login";
	return $('#layout_left .menuItem[pagename="' + page + '"]').attr('pagetitle') || page;
}
function LoadPage(pageName, args)
{
	var state = {};
	state["page"] = pageName;
	if (args)
		for (var i = 0; i < args.length; i++)
			state["a" + i] = args[i];
	$.bbq.pushState(state);
}
function PageLoaded(page)
{
	switch (page)
	{
		case "login":
			$("#txtUser,#txtPass").on("keydown", function (e)
			{
				if (e.which == 13)
				{
					event.preventDefault();
					DoLogin();
				}
			});
			break;
		case "computers":
			ComputersPageLoaded();
			break;
		case "users":
			UsersPageLoaded();
			break;
	}
}
///////////////////////////////////////////////////////////////
// Log in /////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function DoLogin()
{
	SetLoginButtonState(false);
	var un = $("#txtUser").val();
	var pw = $("#txtPass").val();
	if (un == "" || pw == "")
	{
		SetLoginButtonState(true, "Please enter a user name and password");
		return;
	}
	var args = { cmd: "login", user: un };
	ExecJSON(args, function (data)
	{
		args.session = settings.shrd_session = data.session;
		try
		{
			// Use BCrypt on the password, using the salt provided by the server.
			var bCryptResult = dcodeIO.bcrypt.hashSync(pw, data.salt);
			// Compute SHA512 so we have the desired output size for later XORing
			var bCryptResultHex = bytesToHex(stringToUtf8ByteArray(bCryptResult));
			var onceHashedPw = ComputeSHA512Hex(bCryptResultHex);
			// We prove our identity by transmitting onceHashedPw to the server.
			// However we won't do that in plain text.
			// Hash one more time; PasswordHash is the value remembered by the server
			var PasswordHash = ComputeSHA512Hex(onceHashedPw);
			var challengeHashed = ComputeSHA512Hex(PasswordHash + data.challenge);
			args.response = XORHexStrings(challengeHashed, onceHashedPw);
		}
		catch (ex)
		{
			SetLoginButtonState(true, ex.message);
			return;
		}

		ExecJSON(args, function (data)
		{
			settings.shrd_session = data.session;
			SetLoginButtonState(true, data.result, true);
			if (data.result == "success")
				LoginComplete();
		}
			, function (jqXHR, textStatus, errorThrown)
			{
				SetLoginButtonState(true, "Communication error");
			});
	}
		, function (jqXHR, textStatus, errorThrown)
		{
			SetLoginButtonState(true, "Communication error");
		});
}
function SetLoginButtonState(enabled, errorText, errorTextHtml)
{
	if (enabled)
	{
		$("#btnLogin").prop("disabled", false);
		$("#btnLogin").val("Log in");
	}
	else
	{
		$("#btnLogin").prop("disabled", true);
		$("#btnLogin").val("Logging in");
	}
	if (errorTextHtml)
		$("#errortext").html(errorText);
	else
		$("#errortext").text(errorText);
}
function LoginComplete()
{
	LoadMainMenu();
}
///////////////////////////////////////////////////////////////
// Main Menu //////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
var mainMenuLoaded = false;
function LoadMainMenu()
{
	if (mainMenuLoaded)
		return;
	ExecJSON({ cmd: "admin/getMainMenu" }, function (data)
	{
		if (data.result == "success")
		{
			var $ll = $("#layout_left");
			$ll.empty();
			for (var i = 0; i < data.menuItems.length; i++)
			{
				mainMenuLoaded = true;
				var mi = data.menuItems[i];
				$ll.append('<div class="menuItem" pagename="' + mi.PageNameInternal
					+ '" pagetitle="' + HtmlAttributeEncode(mi.DisplayNameHtml)
					+ '"><a onclick="LoadPage(\''
					+ mi.PageNameInternal + '\')" href="admin#page='
					+ mi.PageNameInternal + '">'
					+ mi.DisplayNameHtml + '</a><div>');
			}
			if (data.menuItems.length > 0)
				LoadPage(data.menuItems[0].PageNameInternal);
		}
		else if (data.reason == "missing session")
			$("#layout_left").html("Log In");
		else
			$("#layout_left").html(data.reason);
	}
		, function (jqXHR, textStatus, errorThrown)
		{
			$("#layout_left").html("Unable to load main menu.<br>" + jqXHR.ErrorMessageHtml);
		});
}
///////////////////////////////////////////////////////////////
// Computers Page /////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function ComputersPageLoaded()
{
	var $root = $("#computerListRoot");
	$root.empty();
	var tableDef =
		[
			{ name: "ID", field: "ID" },
			{ name: "Computer Name", field: "Name" },
			{ name: "Groups", field: "Groups", editable: false, type: "custom", customRender: RenderGroupList },
			{ name: "Status", field: "Uptime", editable: false, type: "custom", customRender: RenderUptime }
		];
	var tableOptions =
		{
			idColumn: "ID"
			, loadingImageUrl: "Images/ajax-loader.gif"
			, theme: "green"
			//, customRowClick: statsRowClick
		}
	var computerTable = $root.TableEditor(tableDef, tableOptions);
	ExecJSON({ cmd: "admin/getComputers" }, function (data)
	{
		if (data.result == "success")
		{
			console.log(data);
			computerTable.LoadData(data.Computers);
		}
		else
			$root.html(data.reason);
	}
		, function (jqXHR, textStatus, errorThrown)
		{
			$root.html("Unable to load computer list!<br>" + jqXHR.ErrorMessageHtml);
		});

	function RenderGroupList(computer, editable, fieldName)
	{
		var groupNames = new Array();
		for (var i = 0; i < computer.Groups.length; i++)
			groupNames.push(computer.Groups[i].Name);
		return groupNames.join(", ");
	}
	function RenderUptime(computer, editable, fieldName)
	{
		if (computer.Uptime > -1)
		{
			// Computer is online
			var dateLastConnect = new Date(Date.now() - computer.Uptime);
			return '<span class="compOnline">Online ' + GetFuzzyTime(computer.Uptime) + ' since ' + GetDateStr(dateLastConnect) + '</span>';
		}
		else if (computer.LastDisconnect === 0)
		{
			// Computer is offline
			return '<span class="compOffline">Has Never Connected</span>';
		}
		else
		{
			// Computer is offline
			var dateLastDisconnect = new Date(computer.LastDisconnect);
			var timeSinceDisconnect = Date.now() - computer.LastDisconnect;
			return '<span class="compOffline">Disconnected since ' + GetFuzzyTime(timeSinceDisconnect) + ' at ' + GetDateStr(dateLastDisconnect) + '</span>';
		}
	}
}
///////////////////////////////////////////////////////////////
// Users Page ///////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
function UsersPageLoaded()
{
	var $root = $("#userListRoot");
	$root.empty();
	var tableDef =
		[
			{ name: "ID", field: "ID" },
			{ name: "User Name", field: "Name" },
			{ name: "Display Name", field: "DisplayName" },
			{ name: "Email", field: "Email" },
			{ name: "IsAdmin", field: "IsAdmin" },
			{ name: "Groups", field: "Groups", editable: false, type: "custom", customRender: RenderGroupList }
		];
	var tableOptions =
		{
			idColumn: "ID"
			, loadingImageUrl: "Images/ajax-loader.gif"
			, theme: "green"
			//, customRowClick: statsRowClick
		}
	var userTable = $root.TableEditor(tableDef, tableOptions);
	ExecJSON({ cmd: "admin/getUsers" }, function (data)
	{
		if (data.result == "success")
		{
			console.log(data);
			userTable.LoadData(data.Users);
		}
		else
			$root.html(data.reason);
	}
		, function (jqXHR, textStatus, errorThrown)
		{
			$root.html("Unable to load user list!<br>" + jqXHR.ErrorMessageHtml);
		});

	function RenderGroupList(user, editable, fieldName)
	{
		var groupNames = new Array();
		for (var i = 0; i < user.Groups.length; i++)
			groupNames.push(user.Groups[i].Name);
		return groupNames.join(", ");
	}
}
///////////////////////////////////////////////////////////////
//  ///////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////
