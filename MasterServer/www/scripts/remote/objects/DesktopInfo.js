import DesktopScreen from 'appRoot/scripts/remote/objects/DesktopScreen.js';

export default class DesktopInfo
{
	constructor(buf, offsetWrapper)
	{
		let numScreens = Util.ReadByte(buf, offsetWrapper);
		this.screens = new Array(numScreens);
		for (var i = 0; i < numScreens; i++)
			this.screens[i] = new DesktopScreen(buf, offsetWrapper, i);
	}
}