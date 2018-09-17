import * as Util from 'appRoot/scripts/Util.js';

export default function JpegFragmentedVideoPlayer()
{
	var self = this;
	var submittedImageFragments = 0;
	var processedImageFragments = 0;
	var frameSequenceNumberCounter = 0;
	var nextFrameSequenceNumber = 1;
	this.Play = function ()
	{
		webSocketStreamer.setStreamSettings();
		webSocketStreamer.startStreaming();
	};
	this.Stop = function ()
	{
		webSocketStreamer.stopStreaming();
	};
	this.ConnectionLost = function ()
	{
	}
	thi; s.NewFrame = function (buf, myFrameSequenceNumber, tries)
	{
		if (!myFrameSequenceNumber)
			myFrameSequenceNumber = ++frameSequenceNumberCounter;
		if (!tries)
			tries = 0;
		try
		{
			if (nextFrameSequenceNumber != myFrameSequenceNumber)
			{
				setTimeout(function () { self.NewFrame(buf, myFrameSequenceNumber, tries); }, 1);
				return;
			}
			if (submittedImageFragments != processedImageFragments)
			{
				if (tries > 250)
				{
					toaster.Warning("Hung NewFrame detected in JpegFragmentedVideoPlayer. Pushing ahead with possible image corruption.");
					// TODO: Restart stream
				}
				else
				{
					if (!tries)
						tries = 0;
					setTimeout(function () { self.NewFrame(buf, myFrameSequenceNumber, ++tries); }, 1);
					return;
				}
			}

			// Parse input
			var offsetWrapper = { offset: 2 }; // Skip 1 byte for command and 1 byte for stream ID.
			var moveFragCount = new DataView(buf, offsetWrapper.offset, 2).getUint16(0, false);
			offsetWrapper.offset += 2;
			var dirtyFragCount = new DataView(buf, offsetWrapper.offset, 2).getUint16(0, false);
			offsetWrapper.offset += 2;
			LogVerbose("      New Frame (moved: " + moveFragCount + ", dirty: " + dirtyFragCount + ")");

			var moveList = new Array();
			var dirtList = new Array();
			for (var i = 0; i < moveFragCount; i++)
				moveList.push(new MovedImageFragment(buf, offsetWrapper));
			for (var i = 0; i < dirtyFragCount; i++)
				dirtList.push(new DirtyImageFragment(buf, offsetWrapper));

			// Handle MoveImageFragments
			for (var i = 0; i < moveList.length; i++)
				MoveImageFragment(moveList[i].source, moveList[i].bounds);

			// Handle DirtyImageFragments
			for (var i = 0; i < dirtList.length; i++)
			{
				var dirtyImageFragment = dirtList[i];
				var tmpImg = document.createElement('img');
				tmpImg.bounds = dirtyImageFragment.bounds;
				tmpImg.onload = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					DrawImageFragment(this, this.bounds);
				}
				tmpImg.onerror = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					toaster.Error("Failed to decode image fragment");
				}
				submittedImageFragments++;
				tmpImg.src = dirtyImageFragment.imgBlobURL;
			}
			nextFrameSequenceNumber++;
			mainMenu.frameCounter++;
			mainMenu.framesThisSecond++;
			$("#frames").html(mainMenu.frameCounter);
			$("#kbps").html(buf);
		}
		catch (ex)
		{
			console.log(ex);
		}
	};
	var DrawImageFragment = function (tmpImg, regionRect)
	{
		var canvas = $("#myCanvas").get(0);
		var requiredWidth = regionRect.X + regionRect.Width;
		var requiredHeight = regionRect.Y + regionRect.Height;
		if (requiredWidth > canvas.width || requiredHeight > canvas.height)
		{
			toaster.Info("Resizing canvas from " + canvas.width + "x" + canvas.height + " to " + requiredWidth + "x" + requiredHeight);
			canvas.width = requiredWidth;
			canvas.height = requiredHeight;
		}
		var context2d = canvas.getContext("2d");
		context2d.drawImage(tmpImg, regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
	};
	var MoveImageFragment = function (srcPoint, regionRect)
	{
		var canvas = $("#myCanvas").get(0);
		var context2d = canvas.getContext("2d");
		context2d.drawImage(canvas, srcPoint.X, srcPoint.Y, regionRect.Width, regionRect.Height, regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
	};
}
class MovedImageFragment
{
	constructor(buf, offsetWrapper)
	{
		this.bounds = new ImageRegionRectangle(buf, offsetWrapper);
		this.source = new ImageSourcePoint(buf, offsetWrapper);
	}
}
class DirtyImageFragment
{
	constructor(buf, offsetWrapper)
	{
		this.bounds = new ImageRegionRectangle(buf, offsetWrapper);
		var imgLength = new DataView(buf, offsetWrapper.offset, 4).getInt32(0, false);
		offsetWrapper.offset += 4;
		if (imgLength <= 0)
		{
			this.imgBlobURL = "";
			toaster.Error(imgLength + "-byte image fragment received");
		}
		else
		{
			LogVerbose("   buf.byteLength: " + buf.byteLength + ", offset: " + offsetWrapper.offset + ", imgLength: " + imgLength + ", calc length: " + (offsetWrapper.offset + imgLength));
			var imgView = new Uint8Array(buf, offsetWrapper.offset, imgLength);
			offsetWrapper.offset += imgLength;
			var imgBlob = new Blob([imgView], { type: 'image/jpeg' });
			this.imgBlobURL = URL.createObjectURL(imgBlob);
		}
	}
}
class ImageSourcePoint
{
	constructor(buf, offsetWrapper)
	{
		this.X = ReadInt16(buf, offsetWrapper);
		this.Y = ReadInt16(buf, offsetWrapper);
	}
}
class ImageRegionRectangle
{
	constructor(buf, offsetWrapper)
	{
		this.X = ReadInt16(buf, offsetWrapper);
		this.Y = ReadInt16(buf, offsetWrapper);
		this.Width = ReadUInt16(buf, offsetWrapper);
		this.Height = ReadUInt16(buf, offsetWrapper);
	}
}