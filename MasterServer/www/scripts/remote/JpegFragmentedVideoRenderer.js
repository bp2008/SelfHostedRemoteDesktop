export default function JpegFragmentedVideoRenderer(canvas)
{
	let self = this;
	let submittedImageFragments = 0;
	let processedImageFragments = 0;
	let frameSequenceNumberCounter = 0;
	let nextFrameSequenceNumber = 1;
	//this.Play = function ()
	//{
	//	webSocketStreamer.setStreamSettings();
	//	webSocketStreamer.startStreaming();
	//};
	//this.Stop = function ()
	//{
	//	webSocketStreamer.stopStreaming();
	//};
	this.ConnectionLost = function ()
	{
	};
	this.NewFrame = function (buf, myFrameSequenceNumber, tries)
	{
		if (!myFrameSequenceNumber)
			myFrameSequenceNumber = ++frameSequenceNumberCounter;
		if (!tries)
			tries = 0;
		try
		{
			if (nextFrameSequenceNumber !== myFrameSequenceNumber)
			{
				setTimeout(function () { self.NewFrame(buf, myFrameSequenceNumber, tries); }, 1);
				return;
			}
			if (submittedImageFragments !== processedImageFragments)
			{
				if (tries > 250)
				{
					toaster.Warning("Hung NewFrame detected in JpegFragmentedVideoRenderer. Pushing ahead with possible image corruption.");
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
			let offsetWrapper = { offset: 2 }; // Skip 1 byte for command and 1 byte for stream ID.
			let moveFragCount = Util.ReadUInt16(buf, offsetWrapper);
			let dirtyFragCount = Util.ReadUInt16(buf, offsetWrapper);
			LogVerbose("      New Frame (moved: " + moveFragCount + ", dirty: " + dirtyFragCount + ")");

			let moveList = new Array();
			let dirtList = new Array();
			for (let i = 0; i < moveFragCount; i++)
				moveList.push(new MovedImageFragment(buf, offsetWrapper));
			for (let i = 0; i < dirtyFragCount; i++)
				dirtList.push(new DirtyImageFragment(buf, offsetWrapper));

			// Handle MoveImageFragments
			for (let i = 0; i < moveList.length; i++)
				MoveImageFragment(moveList[i].source, moveList[i].bounds);

			// Handle DirtyImageFragments
			for (let i = 0; i < dirtList.length; i++)
			{
				let dirtyImageFragment = dirtList[i];
				let tmpImg = document.createElement('img');
				tmpImg.bounds = dirtyImageFragment.bounds;
				tmpImg.onload = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					DrawImageFragment(this, this.bounds);
				};
				tmpImg.onerror = function ()
				{
					processedImageFragments++;
					URL.revokeObjectURL(this.src);
					toaster.Error("Failed to decode image fragment");
				};
				submittedImageFragments++;
				tmpImg.src = dirtyImageFragment.imgBlobURL;
			}
			nextFrameSequenceNumber++;
			//mainMenu.frameCounter++;
			//mainMenu.framesThisSecond++;
			//$("#frames").html(mainMenu.frameCounter);
			//$("#kbps").html(buf);
		}
		catch (ex)
		{
			console.error(ex);
		}
	};
	let DrawImageFragment = function (tmpImg, regionRect)
	{
		try
		{
			let requiredWidth = regionRect.X + regionRect.Width;
			let requiredHeight = regionRect.Y + regionRect.Height;
			if (requiredWidth > canvas.width || requiredHeight > canvas.height)
			{
				toaster.Info("Resizing canvas from " + canvas.width + "x" + canvas.height + " to " + requiredWidth + "x" + requiredHeight);
				canvas.width = requiredWidth;
				canvas.height = requiredHeight;
			}
			let context2d = canvas.getContext("2d");
			context2d.drawImage(tmpImg, regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
		}
		catch (ex)
		{
			console.error(ex);
		}
	};
	let MoveImageFragment = function (srcPoint, regionRect)
	{
		let context2d = canvas.getContext("2d");
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
		let imgLength = Util.ReadInt32(buf, offsetWrapper);
		if (imgLength <= 0)
		{
			this.imgBlobURL = "";
			toaster.Error(imgLength + "-byte image fragment received");
		}
		else
		{
			LogVerbose("   buf.byteLength: " + buf.byteLength + ", offset: " + offsetWrapper.offset + ", imgLength: " + imgLength + ", calc length: " + (offsetWrapper.offset + imgLength));
			let imgView = new Uint8Array(buf, offsetWrapper.offset, imgLength);
			offsetWrapper.offset += imgLength;
			let imgBlob = new Blob([imgView], { type: 'image/jpeg' });
			this.imgBlobURL = URL.createObjectURL(imgBlob);
		}
	}
}
class ImageSourcePoint
{
	constructor(buf, offsetWrapper)
	{
		this.X = Util.ReadInt16(buf, offsetWrapper);
		this.Y = Util.ReadInt16(buf, offsetWrapper);
	}
}
class ImageRegionRectangle
{
	constructor(buf, offsetWrapper)
	{
		this.X = Util.ReadInt16(buf, offsetWrapper);
		this.Y = Util.ReadInt16(buf, offsetWrapper);
		this.Width = Util.ReadUInt16(buf, offsetWrapper);
		this.Height = Util.ReadUInt16(buf, offsetWrapper);
	}
}
var logVerbose = false;
function LogVerbose(msg)
{
	if (logVerbose)
		console.info(msg);
}