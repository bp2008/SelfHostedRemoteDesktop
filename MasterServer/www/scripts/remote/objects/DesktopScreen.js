import * as Util from 'appRoot/scripts/Util.js';

export default class DesktopScreen
{
	constructor(buf, offsetWrapper, index)
	{
		this.index = index;

		this.adapterIndex = Util.ReadByte(buf, offsetWrapper);
		this.outputIndex = Util.ReadByte(buf, offsetWrapper);

		let adapterNameLength = Util.ReadUInt16(buf, offsetWrapper);
		this.adapterName = Util.ReadUTF8(buf, offsetWrapper, adapterNameLength);

		let outputNameLength = Util.ReadUInt16(buf, offsetWrapper);
		this.outputName = Util.ReadUTF8(buf, offsetWrapper, outputNameLength);

		this.X = Util.ReadInt16(buf, offsetWrapper);
		this.Y = Util.ReadInt16(buf, offsetWrapper);
		this.Width = Util.ReadUInt16(buf, offsetWrapper);
		this.Height = Util.ReadUInt16(buf, offsetWrapper);
	}
}