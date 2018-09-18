///// <reference path="jquery-3.1.1.js" />
///// <reference path="jquery.ba-bbq.min.js" />
///// <reference path="toastr.min.js" />
///// <reference path="Client.js" />
//"use strict";
/////////////////////////////////////////////////////////////////
//// URL Parameters /////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//var UrlParameters =
//	{
//		loaded: false,
//		parsed_url_params: {},
//		Get: function (key)
//		{
//			if (!this.loaded)
//			{
//				var params = this.parsed_url_params;
//				window.location.search.replace(/[?&]+([^=&]+)=([^&]*)/gi, function (str, key, value)
//				{
//					params[key.toLowerCase()] = decodeURIComponent(value);
//				})
//				this.loaded = true;
//			}
//			if (typeof this.parsed_url_params[key.toLowerCase()] != 'undefined')
//				return this.parsed_url_params[key.toLowerCase()];
//			return "";
//		}
//	};
/////////////////////////////////////////////////////////////////
//// Helper Objects /////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//var toaster = new Toaster();
/////////////////////////////////////////////////////////////////
//// Persistent Settings ////////////////////////////////////////
/////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//// Settings GUI ///////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//function GetClientSettingsEle(key, change)
//{
//	var def = GetClientSettingsDef(key);
//	if (def == null || typeof def.label == "undefined")
//		return $();
//	var $label = $('<div class="clientSettingsLabel"></div>');
//	$label.text(def.label + ":");
//	var $eles = $label;
//	var val = clientSettings.getItem(key);
//	if (typeof def.value == "number")
//	{
//		if (def.inputType == "range")
//		{
//			var $input = $('<input type="range" class="clientSettingsInput" />');
//			if (typeof def.step == "number") $input.attr("step", def.step);
//			if (typeof def.min == "number") $input.attr("min", def.min);
//			if (typeof def.max == "number") $input.attr("max", def.max);
//			$input.val(val);

//			var $rangeLabel = $('<label></label>');
//			$rangeLabel.text(val);

//			$input.on("input", function ()
//			{
//				var newVal = parseFloat($(this).val());
//				clientSettings.setItem(key, newVal);
//				if ($rangeLabel)
//					$rangeLabel.text(newVal);
//				if (typeof change == "function")
//					change(newVal);
//			});

//			$eles = $eles.add($input);
//			$eles = $eles.add($rangeLabel);
//		}
//		else
//		{
//			var $input = $('<input type="number" class="clientSettingsInput" />');
//			if (typeof def.step == "number") $input.attr("step", def.step);
//			if (typeof def.min == "number") $input.attr("min", def.min);
//			if (typeof def.max == "number") $input.attr("max", def.max);
//			$input.val(val);
//			$input.on("change", function ()
//			{
//				var newVal = parseFloat($(this).val());
//				clientSettings.setItem(key, newVal);
//				if (typeof change == "function")
//					change(newVal);
//			});
//			$eles = $eles.add($input);
//		}
//	}
//	else
//	{
//		var $input = $('<input type="text"/>');
//		$input.val(val);
//		$input.on("change", function ()
//		{
//			var newVal = $(this).val();
//			clientSettings.setItem(key, newVal);
//			if (typeof change == "function")
//				change(newVal);
//		});
//		$eles = $eles.add($input);
//	}
//	return $eles;
//}
/////////////////////////////////////////////////////////////////
//// ExecJSON ///////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//function ExecJSON(args, callbackSuccess, callbackFail, synchronous)
//{
//	if (!args.session)
//		args.session = settings.shrd_session;
//	$.ajax({
//		type: 'POST',
//		url: "json",
//		data: JSON.stringify(args),
//		dataType: "json",
//		contentType: "application/json",
//		async: !synchronous,
//		success: function (data)
//		{
//			if (callbackSuccess)
//				callbackSuccess(data);
//		},
//		error: function (jqXHR, textStatus, errorThrown)
//		{
//			jqXHR.OriginalURL = "json";
//			jqXHR.ErrorMessageHtml = 'Response: ' + jqXHR.status + ' ' + jqXHR.statusText + '<br>Status: ' + textStatus + '<br>Error: ' + errorThrown + '<br>URL: ' + jqXHR.OriginalURL;
//			if (callbackFail)
//				callbackFail(jqXHR, textStatus, errorThrown);
//		}
//	});
//}
/////////////////////////////////////////////////////////////////
//// On-screen Toast Messages ///////////////////////////////////
/////////////////////////////////////////////////////////////////
//function Toaster()
//{
//	toastr.options = {
//		"closeButton": false,
//		"debug": false,
//		"positionClass": "toast-bottom-right",
//		"onclick": null,
//		"showDuration": "300",
//		"hideDuration": "1000",
//		"timeOut": "3000",
//		"extendedTimeOut": "3000",
//		"showEasing": "swing",
//		"hideEasing": "linear",
//		"showMethod": "fadeIn",
//		"hideMethod": "fadeOut"
//	}
//	var showToastInternal = function (type, message, showTime, closeButton)
//	{
//		if (typeof message == "object" && typeof message.stack == "string")
//			message = message.stack;
//		var overrideOptions = {};

//		if (showTime)
//			overrideOptions.timeOut = showTime;

//		if (closeButton)
//		{
//			overrideOptions.closeButton = true;
//			overrideOptions.tapToDismiss = false;
//			overrideOptions.extendedTimeOut = 60000;
//		}

//		toastr[type](message, null, overrideOptions);

//		console.log(type + " toast: " + message);
//	}
//	this.Success = function (message, showTime, closeButton)
//	{
//		showToastInternal('success', message, showTime, closeButton);
//	}
//	this.Info = function (message, showTime, closeButton)
//	{
//		showToastInternal('info', message, showTime, closeButton);
//	}
//	this.Warning = function (message, showTime, closeButton)
//	{
//		showToastInternal('warning', message, showTime, closeButton);
//	}
//	this.Error = function (message, showTime, closeButton)
//	{
//		showToastInternal('error', message, showTime, closeButton);
//	}
//}

/////////////////////////////////////////////////////////////////
//// Encryption Helpers /////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//// Convert a hex string to a byte array
//function hexToBytes(hex)
//{
//	if (hex.length % 2 != 0)
//		return [];
//	var bytes = new Uint8Array(parseInt(hex.length / 2));
//	for (var i = 0; i < bytes.length; i++)
//		bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
//	return bytes;
//}

//// Convert a byte array to a hex string
//function bytesToHex(bytes)
//{
//	var hex = new Array(parseInt(bytes.length * 2));
//	for (var i = 0, h = 0; i < bytes.length; i++ , h += 2)
//	{
//		hex[h] = ((bytes[i] & 0xF0) >>> 4).toString(16).charAt(0);
//		hex[h + 1] = (bytes[i] & 0xF).toString(16).charAt(0);
//	}
//	return hex.join("");
//}
//function XORHexStrings(s1, s2)
//{
//	if (s1.length != s2.length)
//		return "Hex string lengths did not match";
//	var a = hexToBytes(s1);
//	var b = hexToBytes(s2);
//	var c = new Array(a.length);
//	for (var i = 0; i < a.length; i++)
//		c[i] = a[i] ^ b[i];
//	return bytesToHex(c);
//}
///**
// * From Google Closure library, https://github.com/google/closure-library/blob/e877b1eac410c0d842bcda118689759512e0e26f/closure/goog/crypt/crypt.js
// * Converts a JS string to a UTF-8 "byte" array.
// * @param {string} str 16-bit unicode string.
// * @return {!Array<number>} UTF-8 byte array.
// */
//function stringToUtf8ByteArray(str)
//{
//	var out = [], p = 0;
//	for (var i = 0; i < str.length; i++)
//	{
//		var c = str.charCodeAt(i);
//		if (c < 128)
//		{
//			out[p++] = c;
//		} else if (c < 2048)
//		{
//			out[p++] = (c >> 6) | 192;
//			out[p++] = (c & 63) | 128;
//		} else if (
//			((c & 0xFC00) == 0xD800) && (i + 1) < str.length &&
//			((str.charCodeAt(i + 1) & 0xFC00) == 0xDC00))
//		{
//			// Surrogate Pair
//			c = 0x10000 + ((c & 0x03FF) << 10) + (str.charCodeAt(++i) & 0x03FF);
//			out[p++] = (c >> 18) | 240;
//			out[p++] = ((c >> 12) & 63) | 128;
//			out[p++] = ((c >> 6) & 63) | 128;
//			out[p++] = (c & 63) | 128;
//		} else
//		{
//			out[p++] = (c >> 12) | 224;
//			out[p++] = ((c >> 6) & 63) | 128;
//			out[p++] = (c & 63) | 128;
//		}
//	}
//	return out;
//};
//function ComputeSHA512Hex(hexIn)
//{
//	var shaObj = new jsSHA("SHA-512", "HEX");
//	shaObj.update(hexIn);
//	return shaObj.getHash("HEX");
//}
/////////////////////////////////////////////////////////////////
//// WebSocket close code translation ///////////////////////////
/////////////////////////////////////////////////////////////////
//var WebSocketCloseCode = new (function ()
//{
//	var self = this;
//	this.Translate = function (code)
//	{
//		if (code >= 0 && code <= 999)
//			return ["", "Reserved and not used."];
//		else if (code >= 1000 && code <= 1015)
//			return [ws_code_map_name[code], ws_code_map_desc[code]];
//		else if (code >= 1016 && code <= 1999)
//			return ["", "Reserved for future use by the WebSocket standard."];
//		else if (code >= 2000 && code <= 2999)
//			return ["", "Reserved for use by WebSocket extensions."];
//		else if (code >= 3000 && code <= 3999)
//			return ["", "Available for use by libraries and frameworks. May not be used by applications. Available for registration at the IANA via first-come, first-serve."];
//		else if (code >= 4000 && code <= 4999)
//			return ["", "Available for use by applications."];
//		else
//			return ["unknown", "unknown"];
//	}
//	var ws_code_map_name = {};
//	var ws_code_map_desc = {};
//	ws_code_map_name[1000] = "CLOSE_NORMAL";
//	ws_code_map_desc[1000] = "Normal closure; the connection successfully completed whatever purpose for which it was created.";
//	ws_code_map_name[1001] = "CLOSE_GOING_AWAY";
//	ws_code_map_desc[1001] = "The endpoint is going away, either because of a server failure or because the browser is navigating away from the page that opened the connection.";
//	ws_code_map_name[1002] = "CLOSE_PROTOCOL_ERROR";
//	ws_code_map_desc[1002] = "The endpoint is terminating the connection due to a protocol error.";
//	ws_code_map_name[1003] = "CLOSE_UNSUPPORTED";
//	ws_code_map_desc[1003] = "The connection is being terminated because the endpoint received data of a type it cannot accept (for example, a text-only endpoint received binary data).";
//	ws_code_map_name[1004] = " ";
//	ws_code_map_desc[1004] = "Reserved. A meaning might be defined in the future.";
//	ws_code_map_name[1005] = "CLOSE_NO_STATUS";
//	ws_code_map_desc[1005] = "Reserved.  Indicates that no status code was provided even though one was expected.";
//	ws_code_map_name[1006] = "CLOSE_ABNORMAL";
//	ws_code_map_desc[1006] = "Reserved. Used to indicate that a connection was closed abnormally (that is, with no close frame being sent) when a status code is expected.";
//	ws_code_map_name[1007] = "Unsupported Data";
//	ws_code_map_desc[1007] = "The endpoint is terminating the connection because a message was received that contained inconsistent data (e.g., non-UTF-8 data within a text message).";
//	ws_code_map_name[1008] = "Policy Violation";
//	ws_code_map_desc[1008] = "The endpoint is terminating the connection because it received a message that violates its policy. This is a generic status code, used when codes 1003 and 1009 are not suitable.";
//	ws_code_map_name[1009] = "CLOSE_TOO_LARGE";
//	ws_code_map_desc[1009] = "The endpoint is terminating the connection because a data frame was received that is too large.";
//	ws_code_map_name[1010] = "Missing Extension";
//	ws_code_map_desc[1010] = "The client is terminating the connection because it expected the server to negotiate one or more extension, but the server didn't.";
//	ws_code_map_name[1011] = "Internal Error";
//	ws_code_map_desc[1011] = "The server is terminating the connection because it encountered an unexpected condition that prevented it from fulfilling the request.";
//	ws_code_map_name[1012] = "Service Restart";
//	ws_code_map_desc[1012] = "The server is terminating the connection because it is restarting. [Ref]";
//	ws_code_map_name[1013] = "Try Again Later";
//	ws_code_map_desc[1013] = "The server is terminating the connection due to a temporary condition, e.g. it is overloaded and is casting off some of its clients. [Ref]";
//	ws_code_map_name[1014] = " ";
//	ws_code_map_desc[1014] = "Reserved for future use by the WebSocket standard.";
//	ws_code_map_name[1015] = "TLS Handshake";
//	ws_code_map_desc[1015] = "Reserved. Indicates that the connection was closed due to a failure to perform a TLS handshake (e.g., the server certificate can't be verified).";
//})();
/////////////////////////////////////////////////////////////////
//// Custom Events //////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//var SHRD_CustomEvent =
//	{
//		customEventRegistry: new Object(),
//		AddListener: function (eventName, eventHandler)
//		{
//			if (typeof this.customEventRegistry[eventName] == "undefined")
//				this.customEventRegistry[eventName] = new Array();
//			this.customEventRegistry[eventName].push(eventHandler);
//		},
//		RemoveListener: function (eventName, eventHandler)
//		{
//			if (typeof this.customEventRegistry[eventName] == "undefined")
//				return;
//			var handlers = this.customEventRegistry[eventName];
//			var idx = handlers.indexOf(eventHandler);
//			if (idx > -1)
//				handlers.splice(idx, 1);
//		},
//		Invoke: function (eventName, args)
//		{
//			if (typeof this.customEventRegistry[eventName] != "undefined")
//				for (var i = 0; i < this.customEventRegistry[eventName].length; i++)
//					try
//					{
//						this.customEventRegistry[eventName][i](args);
//					}
//					catch (ex)
//					{
//						toaster.Error(ex);
//					}
//		}
//	};
/////////////////////////////////////////////////////////////////
//// Binary Reading /////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//function ReadByte(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 1).getUint8(0);
//	offsetWrapper.offset++;
//	return v;
//}
//function ReadUInt16(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).getUint16(0, false);
//	offsetWrapper.offset += 2;
//	return v;
//}
//function ReadUInt16LE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).getUint16(0, true);
//	offsetWrapper.offset += 2;
//	return v;
//}
//function ReadInt16(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).getInt16(0, false);
//	offsetWrapper.offset += 2;
//	return v;
//}
//function ReadInt16LE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).getInt16(0, true);
//	offsetWrapper.offset += 2;
//	return v;
//}
//function ReadUInt32(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getUint32(0, false);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadUInt32LE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getUint32(0, true);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadInt32(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getInt32(0, false);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadInt32LE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getInt32(0, true);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadFloat(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getFloat32(0, false);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadFloatLE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).getFloat32(0, true);
//	offsetWrapper.offset += 4;
//	return v;
//}
//function ReadDouble(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 8).getFloat64(0, false);
//	offsetWrapper.offset += 8;
//	return v;
//}
//function ReadDoubleLE(buf, offsetWrapper)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 8).getFloat64(0, true);
//	offsetWrapper.offset += 8;
//	return v;
//}
//function ReadUInt64(buf, offsetWrapper)
//{
//	// This is a hack because JavaScript only has 64 bit doubles with 53 bit int precision.
//	// If a number were to be higher than 2 ^ 53, this method would return the wrong value.
//	var mostSignificant = (ReadUInt32(buf.buffer || buf, offsetWrapper) & 0x001FFFFF) * 4294967296;
//	var leastSignificant = ReadUInt32(buf.buffer || buf, offsetWrapper);
//	return mostSignificant + leastSignificant;
//}
//function ReadUInt64LE(buf, offsetWrapper)
//{
//	// This is a hack because JavaScript only has 64 bit doubles with 53 bit int precision.
//	// If a number were to be higher than 2 ^ 53, this method would return the wrong value.
//	var leastSignificant = ReadUInt32LE(buf.buffer || buf, offsetWrapper);
//	var mostSignificant = (ReadUInt32LE(buf.buffer || buf, offsetWrapper) & 0x001FFFFF) * 4294967296;
//	return mostSignificant + leastSignificant;
//}
//function ReadUTF8(buf, offsetWrapper, byteLength)
//{
//	var v = Utf8ArrayToStr(new Uint8Array(buf.buffer || buf, offsetWrapper.offset, byteLength));
//	offsetWrapper.offset += byteLength;
//	return v;
//}
/////////////////////////////////////////////////////////////////
//// Binary Writing /////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//function WriteByte(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 1).setUint8(0, val);
//	offsetWrapper.offset++;
//}
//function WriteUInt16(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).setUint16(0, val, false);
//	offsetWrapper.offset += 2;
//}
//function WriteUInt16LE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).setUint16(0, val, true);
//	offsetWrapper.offset += 2;
//}
//function WriteInt16(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).setInt16(0, val, false);
//	offsetWrapper.offset += 2;
//}
//function WriteInt16LE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 2).setInt16(0, val, true);
//	offsetWrapper.offset += 2;
//}
//function WriteUInt32(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setUint32(0, val, false);
//	offsetWrapper.offset += 4;
//}
//function WriteUInt32LE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setUint32(0, val, true);
//	offsetWrapper.offset += 4;
//}
//function WriteInt32(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setInt32(0, val, false);
//	offsetWrapper.offset += 4;
//}
//function WriteInt32LE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setInt32(0, val, true);
//	offsetWrapper.offset += 4;
//}
//function WriteFloat(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setFloat32(0, val, false);
//	offsetWrapper.offset += 4;
//}
//function WriteFloatLE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 4).setFloat32(0, val, true);
//	offsetWrapper.offset += 4;
//}
//function WriteDouble(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 8).setFloat64(0, val, false);
//	offsetWrapper.offset += 8;
//}
//function WriteDoubleLE(buf, offsetWrapper, val)
//{
//	var v = new DataView(buf.buffer || buf, offsetWrapper.offset, 8).setFloat64(0, val, true);
//	offsetWrapper.offset += 8;
//}
//// http://www.onicos.com/staff/iz/amuse/javascript/expert/utf.txt

///* utf.js - UTF-8 <=> UTF-16 convertion
// *
// * Copyright (C) 1999 Masanao Izumo <iz@onicos.co.jp>
// * Version: 1.0
// * LastModified: Dec 25 1999
// * This library is free.  You can redistribute it and/or modify it.
// */

//function Utf8ArrayToStr(array)
//{
//	var out, i, len, c;
//	var char2, char3;

//	out = "";
//	len = array.length;
//	i = 0;
//	while (i < len)
//	{
//		c = array[i++];
//		switch (c >> 4)
//		{
//			case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7:
//				// 0xxxxxxx
//				out += String.fromCharCode(c);
//				break;
//			case 12: case 13:
//				// 110x xxxx   10xx xxxx
//				char2 = array[i++];
//				out += String.fromCharCode(((c & 0x1F) << 6) | (char2 & 0x3F));
//				break;
//			case 14:
//				// 1110 xxxx  10xx xxxx  10xx xxxx
//				char2 = array[i++];
//				char3 = array[i++];
//				out += String.fromCharCode(((c & 0x0F) << 12) |
//					((char2 & 0x3F) << 6) |
//					((char3 & 0x3F) << 0));
//				break;
//		}
//	}

//	return out;
//}
/////////////////////////////////////////////////////////////////
//// Misc ///////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////
//function AppendArrays(a, b)
//{
//	var c = new Array(a.length + b.length);
//	var i = 0;
//	for (; i < a.length; i++)
//		c[i] = a[i];
//	for (var j = 0; j < b.length; i++ , j++)
//		c[i] = b[j];
//	return c;
//}
//function htmlEncode(value)
//{
//	return $('<div/>').text(value).html();
//}
//function htmlDecode(value)
//{
//	return $('<div/>').html(value).text();
//}
//var HtmlAttributeEncode = function (str)
//{
//	if (typeof str != "string")
//		return "";
//	var sb = new Array("");
//	for (var i = 0; i < str.length; i++)
//	{
//		var c = str.charAt(i);
//		switch (c)
//		{
//			case '"':
//				sb.push("&quot;");
//				break;
//			case '\'':
//				sb.push("&#39;");
//				break;
//			case '&':
//				sb.push("&amp;");
//				break;
//			case '<':
//				sb.push("&lt;");
//				break;
//			case '>':
//				sb.push("&gt;");
//				break;
//			default:
//				sb.push(c);
//				break;
//		}
//	}
//	return sb.join("");
//}
//function Clamp(i, min, max)
//{
//	if (i < min)
//		return min;
//	if (i > max)
//		return max;
//	return i;
//}

//var mouseCoordFixer =
//	{
//		last: {
//			x: 0, y: 0
//		}
//		, fix: function (e)
//		{
//			if (typeof e.pageX == "undefined")
//			{
//				if (e.originalEvent && e.originalEvent.touches && e.originalEvent.touches.length > 0)
//				{
//					mouseCoordFixer.last.x = e.pageX = e.originalEvent.touches[0].pageX;
//					mouseCoordFixer.last.y = e.pageY = e.originalEvent.touches[0].pageY;
//				}
//				else
//				{
//					e.pageX = mouseCoordFixer.last.x;
//					e.pageY = mouseCoordFixer.last.y;
//				}
//			}
//			else
//			{
//				mouseCoordFixer.last.x = e.pageX;
//				mouseCoordFixer.last.y = e.pageY;
//			}
//		}
//	};
//function IE_GetDevicePixelRatio()
//{
//	return Math.sqrt(screen.deviceXDPI * screen.deviceYDPI) / 96;
//}

//function GetDevicePixelRatio()
//{
//	var returnValue = window.devicePixelRatio || IE_GetDevicePixelRatio() || 1;
//	if (returnValue <= 0)
//		returnValue = 1;
//	return returnValue;
//}
//function getHiddenProp()
//{
//	var prefixes = ['webkit', 'moz', 'ms', 'o'];

//	// if 'hidden' is natively supported just return it
//	if ('hidden' in document) return 'hidden';

//	// otherwise loop over all the known prefixes until we find one
//	for (var i = 0; i < prefixes.length; i++)
//	{
//		if ((prefixes[i] + 'Hidden') in document)
//			return prefixes[i] + 'Hidden';
//	}

//	// otherwise it's not supported
//	return null;
//}
//function documentIsHidden()
//{
//	var prop = getHiddenProp();
//	if (!prop) return false;

//	return document[prop];
//}
//function GetFuzzyTime(ms)
//{
//	/// <summary>Gets a fuzzy time string accurate within 1 year.</summary>
//	var years = Math.round(ms / 31536000000);
//	if (years > 0)
//		return years + " year" + (years == 1 ? "" : "s");
//	var months = Math.round(ms / 2628002880);
//	if (months > 0)
//		return months + " month" + (months == 1 ? "" : "s");
//	var weeks = Math.round(ms / 604800000);
//	if (weeks > 0)
//		return weeks + " week" + (weeks == 1 ? "" : "s");
//	return GetFuzzyTime_Days(ms);
//}
//function GetFuzzyTime_Days(ms)
//{
//	/// <summary>Gets a fuzzy time string accurate within 1 day.</summary>
//	var days = Math.round(ms / 86400000);
//	if (days > 0)
//		return days + " day" + (days == 1 ? "" : "s");
//	var hours = Math.round(ms / 3600000);
//	if (hours > 0)
//		return hours + " hour" + (hours == 1 ? "" : "s");
//	var minutes = Math.round(ms / 60000);
//	if (minutes > 0)
//		return minutes + " minute" + (minutes == 1 ? "" : "s");
//	return "less than 1 minute";
//}
//var use24HourTime = false;
//function GetTimeStr(date, includeMilliseconds)
//{
//	var ampm = "";
//	var hour = date.getHours();
//	if (!use24HourTime)
//	{
//		if (hour == 0)
//		{
//			hour = 12;
//			ampm = " AM";
//		}
//		else if (hour == 12)
//		{
//			ampm = " PM";
//		}
//		else if (hour > 12)
//		{
//			hour -= 12;
//			ampm = " PM";
//		}
//		else
//		{
//			ampm = " AM";
//		}
//	}
//	var ms = includeMilliseconds ? ("." + date.getMilliseconds()) : "";

//	var str = hour.toString().padLeft(2, '0') + ":" + date.getMinutes().toString().padLeft(2, '0') + ":" + date.getSeconds().toString().padLeft(2, '0') + ms + ampm;
//	return str;
//}
//function GetDateStr(date, includeMilliseconds)
//{
//	var str = date.getFullYear() + "/" + (date.getMonth() + 1) + "/" + date.getDate() + " " + GetTimeStr(date, includeMilliseconds);
//	return str;
//}
//String.prototype.padLeft = function (len, c)
//{
//	var pads = len - this.length;
//	if (pads > 0)
//	{
//		var sb = [];
//		var pad = c || "&nbsp;";
//		for (var i = 0; i < pads; i++)
//			sb.push(pad);
//		sb.push(this);
//		return sb.join("");
//	}
//	return this;

//};
//$(function ()
//{
//	var visProp = getHiddenProp();
//	if (visProp)
//	{
//		var evtname = visProp.replace(/[H|h]idden/, '') + 'visibilitychange';
//		document.addEventListener(evtname, function ()
//		{
//			// Called when page visibility changes.
//			SHRD_CustomEvent.Invoke("visibilityChange", !documentIsHidden())
//		});
//	}
//});