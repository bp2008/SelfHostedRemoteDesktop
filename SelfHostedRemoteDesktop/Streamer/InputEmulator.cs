using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BPUtil;
using SelfHostedRemoteDesktop;
using SelfHostedRemoteDesktop.NetCommand;
using WindowsInput;
using WindowsInput.Native;
using MouseButton = SelfHostedRemoteDesktop.NetCommand.MouseButton;

namespace SelfHostedRemoteDesktop.Streamer
{
	internal class InputEmulator// : CriticalFinalizerObject, IDisposable
	{
		//private bool isDisposed = false;
		InputSimulator sim;
		public InputEmulator()
		{
			sim = new InputSimulator();
		}

		//~InputEmulator()
		//{
		//	if (isDisposed)
		//		return;
		//	Dispose(false);
		//}

		//public void Dispose()
		//{
		//	if (isDisposed)
		//		return;
		//	Dispose(true);
		//	GC.SuppressFinalize(this);
		//}

		//protected void Dispose(bool disposing)
		//{
		//	isDisposed = true;
		//}

		public void EmulateInput(SharedMemoryStream sm)
		{
			DesktopManager.AssociateCurrentThreadWithDefaultDesktop();
			InputType type = (InputType)sm.ReadByte();
			switch (type)
			{
				case InputType.KeyDown:
				case InputType.KeyUp:
					{
						int keyCode = sm.ReadInt32();
						ModifierKeys modifiers = (ModifierKeys)sm.ReadUInt32();
						bool isUp = type == InputType.KeyUp;
						EmulateKeyboard(keyCode, modifiers, isUp);
					}
					break;
				case InputType.MouseMove:
					{
						float X = sm.ReadFloat();
						float Y = sm.ReadFloat();
						EmulateMouseMove(X, Y);
					}
					break;
				case InputType.MouseButtonDown:
				case InputType.MouseButtonUp:
					{
						MouseButton button = (MouseButton)sm.ReadByte();
						bool isUp = type == InputType.MouseButtonUp;
						EmulateMouseButton(button, isUp);
					}
					break;
				case InputType.MouseWheel:
					{
						short deltaX = sm.ReadInt16();
						short deltaY = sm.ReadInt16();
						if (deltaX != 0)
							EmulateMouseWheelX(deltaX);
						if (deltaY != 0)
							EmulateMouseWheelY(deltaY);
					}
					break;
				default:
					break;
			}
		}

		private void EmulateKeyboard(int keyCode, ModifierKeys modifiers, bool isUpCommand)
		{
			// Make sure the modifier key state is correct
			// But skip that step if the key code being sent is that modifier key.

			Keys keyVal = (Keys)keyCode;

			if (keyVal != Keys.Control && keyVal != Keys.ControlKey && keyVal != Keys.LControlKey && keyVal != Keys.RControlKey)
				EnforceModifierKeyState((modifiers & ModifierKeys.Ctrl) != 0, VirtualKeyCode.CONTROL, sim.InputDeviceState.IsKeyDown, sim.Keyboard.KeyDown, sim.Keyboard.KeyUp);

			if (keyVal != Keys.Alt && keyVal != Keys.Menu && keyVal != Keys.LMenu && keyVal != Keys.RMenu)
				EnforceModifierKeyState((modifiers & ModifierKeys.Alt) != 0, VirtualKeyCode.MENU, sim.InputDeviceState.IsKeyDown, sim.Keyboard.KeyDown, sim.Keyboard.KeyUp);

			if (keyVal != Keys.Shift && keyVal != Keys.ShiftKey && keyVal != Keys.LShiftKey && keyVal != Keys.RShiftKey)
				EnforceModifierKeyState((modifiers & ModifierKeys.Shift) != 0, VirtualKeyCode.SHIFT, sim.InputDeviceState.IsKeyDown, sim.Keyboard.KeyDown, sim.Keyboard.KeyUp);

			if (keyVal != Keys.LWin)
				EnforceModifierKeyState((modifiers & ModifierKeys.LeftWindows) != 0, VirtualKeyCode.LWIN, sim.InputDeviceState.IsKeyDown, sim.Keyboard.KeyDown, sim.Keyboard.KeyUp);

			if (keyVal != Keys.RWin)
				EnforceModifierKeyState((modifiers & ModifierKeys.RightWindows) != 0, VirtualKeyCode.RWIN, sim.InputDeviceState.IsKeyDown, sim.Keyboard.KeyDown, sim.Keyboard.KeyUp);

			//if (keyVal != Keys.CapsLock)
			//	EnforceModifierKeyState((modifiers & ModifierKeys.CapsLock) != 0, VirtualKeyCode.CAPITAL, sim.InputDeviceState.IsTogglingKeyInEffect, sim.Keyboard.KeyPress, sim.Keyboard.KeyPress);

			//if (keyVal != Keys.NumLock)
			//	EnforceModifierKeyState((modifiers & ModifierKeys.NumLock) != 0, VirtualKeyCode.NUMLOCK, sim.InputDeviceState.IsTogglingKeyInEffect, sim.Keyboard.KeyPress, sim.Keyboard.KeyPress);

			//if (keyVal != Keys.Scroll)
			//	EnforceModifierKeyState((modifiers & ModifierKeys.ScrollLock) != 0, VirtualKeyCode.SCROLL, sim.InputDeviceState.IsTogglingKeyInEffect, sim.Keyboard.KeyPress, sim.Keyboard.KeyPress);

			Logger.Info("Key " + (isUpCommand ? "up" : "down") + ", keyCode: " + keyCode + ", modifiers: " + modifiers);
			if (isUpCommand)
				sim.Keyboard.KeyUp((VirtualKeyCode)keyCode);
			else
				sim.Keyboard.KeyDown((VirtualKeyCode)keyCode);
		}


		private void EmulateMouseMove(float x, float y)
		{
			//Logger.Info("Mouse " + x + ", " + y);
			sim.Mouse.MoveMouseToPositionOnVirtualDesktop(x, y);
		}

		private void EmulateMouseButton(MouseButton buttonNumber, bool isUpCommand)
		{
			Logger.Info("Mouse Button " + buttonNumber + " " + (isUpCommand ? "up" : "down"));
			switch (buttonNumber)
			{
				case MouseButton.Left:
					if (isUpCommand)
						sim.Mouse.LeftButtonUp();
					else
						sim.Mouse.LeftButtonDown();
					break;
				case MouseButton.Right:
					if (isUpCommand)
						sim.Mouse.RightButtonUp();
					else
						sim.Mouse.RightButtonDown();
					break;
				case MouseButton.Middle:
					//if (isUpCommand)
					//	sim.Mouse.MButtonUp();
					//else
					//	sim.Mouse.LeftButtonDown()
					break;
				case MouseButton.Back:
					if (isUpCommand)
						sim.Mouse.XButtonUp(0);
					else
						sim.Mouse.XButtonDown(0);
					break;
				case MouseButton.Forward:
					if (isUpCommand)
						sim.Mouse.XButtonUp(1);
					else
						sim.Mouse.XButtonDown(1);
					break;
			}
		}

		private void EmulateMouseWheelX(short delta)
		{
			sim.Mouse.HorizontalScroll(delta);
		}
		private void EmulateMouseWheelY(short delta)
		{
			sim.Mouse.VerticalScroll(delta);
		}

		private void EnforceModifierKeyState(bool desiredState
			, VirtualKeyCode vkc
			, Func<VirtualKeyCode, bool> getKeyState
			, Func<VirtualKeyCode, IKeyboardSimulator> enableModifierKey
			, Func<VirtualKeyCode, IKeyboardSimulator> disableModifierKey)
		{
			bool currentState = getKeyState(vkc);
			if (currentState != desiredState)
			{
				if (desiredState)
					enableModifierKey(vkc);
				else
					disableModifierKey(vkc);
			}
		}
	}
}
