﻿/*
 * Dialog - jQuery Plugin for creating simple dialog boxes
 * MIT License - https://opensource.org/licenses/MIT
 */
/// <reference path="jquery-3.1.1.js" />

(function ($)
{
	var idAutoIncrement = 0;
	var zIndexAutoIncrement = 1000;
	$.fn.dialog = function (options)
	{
		return new Dialog(this, options);
	}
	$.fn.modalDialog = function (options)
	{
		if (!options)
			options = {};
		if (!options.overlayOpacity)
			options.overlayOpacity = 0.3;
		return new Dialog(this, options);
	}
	function Dialog($content, options)
	{
		var self = this;

		this.$overlay = $("");
		this.$dialog = $("");
		this.$titlebar = $("");
		this.$title = $("");
		this.$closebtn = $("");

		var myId = idAutoIncrement;
		var isOpen = false;
		var mouseMem = { offsetX: 0, offsetY: 0, down: false, originalX: 0, originalY: 0 };
		this.settings = $.extend(
			{
				// These are the default settings
				title: "Message",
				closeOnOverlayClick: false,
				onClosing: null,
				overlayOpacity: 0,
				cssClass: ""
			}, options);

		var open = function ()
		{
			if (isOpen)
				return;
			isOpen = true;
			var opacity = parseFloat(self.settings.overlayOpacity);

			if (opacity > 0)
			{
				self.$overlay = $('<div class="dialog_overlay"></div>');
				self.$overlay.css('opacity', opacity);
				self.$overlay.css("z-index", zIndexAutoIncrement++);
				if (self.settings.closeOnOverlayClick)
				{
					self.$overlay.on("contextmenu", function () { self.close(); return false; });
					self.$overlay.click(self.close);
				}
				$("body").append(self.$overlay);
			}

			{
				self.$dialog = $('<div class="dialog_wrapper"></div>');
				if (self.settings.cssClass)
					self.$dialog.addClass(self.settings.cssClass);
				self.$dialog.on('mousedown touchstart', focusSelf);
				{
					self.$titlebar = $('<div class="dialog_titlebar"></div>');
					{
						self.$title = $('<div class="dialog_title"></div>');
						self.$title.html(self.settings.title);
						self.$title.on('mousedown touchstart', dragStart);
						$(document).on('mousemove touchmove', dragMove);
						$(document).on('mouseup touchend mouseleave', dragEnd);
						$(document).on('touchcancel', dragCancel);
						self.$titlebar.append(self.$title);
					}
					{
						self.$closebtn = $('<div class="dialog_close">'
							+ '<div class="dialog_close_icon"><svg viewbox="0 0 10 10">'
							+ '<path stroke-width="1.4" d="M 0,0 L 10,10 M 0,10 L 10,0" />'
							+ '</svg></div></div>');
						self.$closebtn.click(self.close);
						self.$titlebar.append(self.$closebtn);
					}
					self.$dialog.append(self.$titlebar);
				}

				if (!$content.hasClass("dialog_content"))
					$content.addClass("dialog_content");
				self.$dialog.append($content);

				$("body").append(self.$dialog);

				self.bringToTop();
			}

			positionCentered();

			$(window).bind("resize.dialog" + myId + " orientationchange.dialog" + myId + " scroll.dialog" + myId, onResize);

			onResize(self);
		}
		this.bringToTop = function ()
		{
			self.$dialog.css("z-index", zIndexAutoIncrement++);
		}
		this.close = function (suppressCallback)
		{
			if (!isOpen)
				return;
			isOpen = false;

			if (typeof self.settings.onClosing == "function" && !suppressCallback)
				if (self.settings.onClosing())
					return false;

			$(window).unbind(".dialog" + myId);

			self.$overlay.remove();
			self.$dialog.remove();

			return true;
		}
		var positionCentered = function ()
		{
			var windowW = $(window).width();
			var windowH = $(window).height();

			var w = self.$dialog.width();
			var h = self.$dialog.height();

			self.$dialog.css("left", $(window).scrollLeft() + ((windowW - w) / 2) + "px");
			self.$dialog.css("top", $(window).scrollTop() + ((windowH - h) / 2) + "px");
		}
		var onResize = function ()
		{
			if (!isOpen)
				return;
			var windowW = $(window).width();
			var windowH = $(window).height();

			var offset = self.$dialog.offset();
			var w = self.$dialog.width();
			var h = self.$dialog.height();

			var topOfWindow = $(window).scrollTop();
			var leftOfWindow = $(window).scrollLeft();
			var bottomOfWindow = topOfWindow + windowH;
			var rightOfWindow = leftOfWindow + windowW;

			if (offset.left + (w / 2) < leftOfWindow)
				self.$dialog.css("left", leftOfWindow + (w / -2) + "px");
			else if (offset.left + (w / 2) > rightOfWindow)
				self.$dialog.css("left", rightOfWindow - (w / 2) + "px");

			if (offset.top < topOfWindow)
				self.$dialog.css("top", topOfWindow + "px");
			else if (offset.top > bottomOfWindow - 24)
				self.$dialog.css("top", bottomOfWindow - 24 + "px");

			self.$overlay.css('width', $(document).width()).css('height', $(document).height());
		}
		var focusSelf = function (e)
		{
			self.bringToTop();
		}
		var dragStart = function (e)
		{
			mouseCoordFixer.fix(e);
			mouseMem.down = true;
			var pos = self.$dialog.position();
			mouseMem.originalX = pos.left;
			mouseMem.originalY = pos.top;
			mouseMem.offsetX = pos.left - e.pageX;
			mouseMem.offsetY = pos.top - e.pageY;
		}
		var dragMove = function (e)
		{
			mouseCoordFixer.fix(e);
			if (mouseMem.down)
			{
				var newX = e.pageX + mouseMem.offsetX;
				var newY = e.pageY + mouseMem.offsetY;

				var windowW = $(window).width();
				var windowH = $(window).height();
				var topOfWindow = $(window).scrollTop();
				var leftOfWindow = $(window).scrollLeft();
				var bottomOfWindow = (topOfWindow + windowH);
				var rightOfWindow = (leftOfWindow + windowW);

				var w = self.$dialog.outerWidth(true);
				var h = self.$dialog.outerHeight(true);

				if (newX < leftOfWindow)
					newX = leftOfWindow;
				else if (newX + w > rightOfWindow)
					newX = rightOfWindow - w;

				if (newY < topOfWindow)
					newY = topOfWindow;
				else if (newY > bottomOfWindow - h)
					newY = bottomOfWindow - h;
				// TODO: Make sure the panel can't be dragged beyond the right or bottom edge of the screen.
				self.$dialog.css("left", newX + "px");
				self.$dialog.css("top", newY + "px");
			}
		}
		var dragEnd = function (e)
		{
			dragMove(e);
			mouseMem.down = false;
		}
		var dragCancel = function (e)
		{
			mouseCoordFixer.fix(e);
			if (mouseMem.down)
			{
				mouseMem.down = false;
				self.$dialog.css("left", mouseMem.originalX + "px");
				self.$dialog.css("top", mouseMem.originalY + "px");
			}
		}
		var mouseCoordFixer =
			{
				last: {
					x: 0, y: 0
				}
				, fix: function (e)
				{
					if (typeof e.pageX == "undefined")
					{
						if (e.originalEvent && e.originalEvent.touches && e.originalEvent.touches.length > 0)
						{
							mouseCoordFixer.last.x = e.pageX = e.originalEvent.touches[0].pageX;
							mouseCoordFixer.last.y = e.pageY = e.originalEvent.touches[0].pageY;
						}
						else
						{
							e.pageX = mouseCoordFixer.last.x;
							e.pageY = mouseCoordFixer.last.y;
						}
					}
					else
					{
						mouseCoordFixer.last.x = e.pageX;
						mouseCoordFixer.last.y = e.pageY;
					}
				}
			};

		open();
	}
}(jQuery));
var SimpleDialog = new function ()
{
	var self = this;
	this.text = this.Text = function (message)
	{
		$('<div></div>').text(message).modalDialog();
	}
	this.html = this.Html = function (message)
	{
		$('<div></div>').html(message).modalDialog();
	}
	this.confirmText = this.ConfirmText = function (question, onYes, onNo)
	{
		return Confirm($('<div></div>').text(question), onYes, onNo);
	}
	this.confirmHtml = this.ConfirmHtml = function (question, onYes, onNo)
	{
		return Confirm($('<div></div>').html(question), onYes, onNo);
	}
	var Confirm = function (questionEle, onYes, onNo)
	{
		var $dlg = $('<div></div>');
		$dlg.append(questionEle);

		var $yes = $('<input type="button" value="Yes" style="margin-right:15px;" />');
		var $no = $('<input type="button" value="No" />');
		$dlg.append($('<div style="margin: 20px 0px 10px 0px; text-align: center;"></div>').append($yes).append($no));

		var dlg = $dlg.modalDialog({ title: "Confirm" });
		$yes.click(function ()
		{
			dlg.close();
			if (onYes)
				try
				{ onYes(); } catch (ex)
				{
					console.log(ex);
				}
		});
		$no.click(function ()
		{
			dlg.close();
			if (onNo)
				try
				{ onNo(); } catch (ex)
				{
					console.log(ex);
				}
		});
	}
}();