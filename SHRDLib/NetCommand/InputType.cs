using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHRDLib.NetCommand
{
	//public class InputItem
	//{
	//}
	//public class MouseEvent
	public enum InputType : byte
	{
		KeyDown = 0 // Followed by Int32 (key code), UInt32 (ModifierKeys)
		, KeyUp = 1 // Same as KeyDown
		, MouseMove = 100 // Followed by two float (32 bit) numbers representing  
						  // X and Y coordinates on the Virtual Desktop which 
						  // consists of all active monitors.
						  // The range of each number is [0-65535] where 0 is the 
						  // left or top of the virtual desktop, and 65535 is the 
						  // right or bottom of the virtual desktop.  Client apps 
						  // therefore must be aware of the full monitor layout 
						  // even if only one monitor is being displayed at a time.
		, MouseButtonDown = 101 // Followed by one byte (MouseButtons number)
		, MouseButtonUp = 102 // Same as MouseButtonDown
		, MouseWheel = 103 // Followed by one Int16 (mouse wheel delta)
	}
	public enum MouseButton : byte
	{
		Left = 0
		, Right = 1
		, Middle = 2
		, Back = 3
		, Forward = 4
		, None = 255
	}
	[Flags]
	public enum ModifierKeys : uint
	{
		None = 0
		, LeftCtrl = 1
		, RightCtrl = 1 << 1
		, Ctrl = LeftCtrl | RightCtrl
		, LeftShift = 1 << 2
		, RightShift = 1 << 3
		, Shift = LeftShift | RightShift
		, LeftAlt = 1 << 4
		, RightAlt = 1 << 5
		, Alt = LeftAlt | RightAlt
		, LeftWindows = 1 << 6 // Currently Disabled as web browsers don't report this modifier state
		, RightWindows = 1 << 7 // ^^
		, Windows = LeftWindows | RightWindows // ^^
		, CapsLock = 1 << 8 // ^^
		, NumLock = 1 << 9 // ^^
		, ScrollLock = 1 << 10 // ^^
	}
}
