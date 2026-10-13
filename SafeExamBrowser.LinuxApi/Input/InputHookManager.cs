/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.WindowsApi.Contracts.Events;

namespace SafeExamBrowser.LinuxApi.Input
{
	/// <summary>
	/// Manages the keyboard and mouse hooks. Hooks are chained like on Windows: the most recently registered hook is called
	/// first and a hook which blocks an event prevents all remaining hooks from seeing it.
	/// </summary>
	internal class InputHookManager : IDisposable
	{
		private const int EV_SYN = 0, EV_KEY = 1, EV_ABS = 3;
		private const int BTN_LEFT = 0x110, BTN_RIGHT = 0x111, BTN_MIDDLE = 0x112, BTN_SIDE = 0x113, BTN_EXTRA = 0x114, BTN_TOUCH = 0x14a;

		private readonly ILogger logger;
		private readonly object sync = new object();
		private readonly List<(Guid id, KeyboardHookCallback callback)> keyboardHooks = new List<(Guid, KeyboardHookCallback)>();
		private readonly List<(Guid id, MouseHookCallback callback)> mouseHooks = new List<(Guid, MouseHookCallback)>();
		private readonly List<InputDevice> devices = new List<InputDevice>();

		private X11Display display;
		private bool ctrlPressed, altPressed;
		private volatile bool running;
		private Thread thread;
		private int touchX, touchY;

		/// <summary>
		/// Whether key sequences which would allow to leave the kiosk session on a Linux system (VT switching with Ctrl+Alt+Fn,
		/// Ctrl+Alt+Backspace, Ctrl+Alt+Delete, SysRq) are blocked in addition to the hook callbacks.
		/// </summary>
		internal bool BlockSystemSequences { get; set; }

		internal InputHookManager(ILogger logger)
		{
			this.logger = logger;
		}

		internal Guid Register(KeyboardHookCallback callback)
		{
			var id = Guid.NewGuid();

			lock (sync)
			{
				keyboardHooks.Add((id, callback));
			}

			EnsureRunning();

			return id;
		}

		internal Guid Register(MouseHookCallback callback)
		{
			var id = Guid.NewGuid();

			lock (sync)
			{
				mouseHooks.Add((id, callback));
			}

			EnsureRunning();

			return id;
		}

		internal void DeregisterKeyboard(Guid id)
		{
			lock (sync)
			{
				keyboardHooks.RemoveAll(h => h.id == id);
			}
		}

		internal void DeregisterMouse(Guid id)
		{
			lock (sync)
			{
				mouseHooks.RemoveAll(h => h.id == id);
			}
		}

		private void EnsureRunning()
		{
			lock (sync)
			{
				if (!running)
				{
					running = true;
					X11Display.TryOpen(out display);
					thread = new Thread(Loop) { IsBackground = true, Name = "SEB input hooks" };
					thread.Start();
				}
			}
		}

		private void Loop()
		{
			var lastScan = DateTime.MinValue;

			try
			{
				while (running)
				{
					if (DateTime.Now - lastScan > TimeSpan.FromSeconds(2))
					{
						Scan();
						lastScan = DateTime.Now;
					}

					var fds = devices.Select(d => new LibC.PollFd { fd = d.FileDescriptor, events = LibC.POLLIN }).ToArray();

					if (fds.Length == 0)
					{
						Thread.Sleep(200);
						continue;
					}

					if (LibC.poll(fds, (ulong) fds.Length, 250) <= 0)
					{
						continue;
					}

					for (var i = 0; i < fds.Length; i++)
					{
						if ((fds[i].revents & LibC.POLLIN) != 0)
						{
							Process(devices[i]);
						}
						else if ((fds[i].revents & ~LibC.POLLIN) != 0)
						{
							// Device vanished (e.g. unplugged)
							devices[i].Dispose();
							devices[i] = null;
						}
					}

					devices.RemoveAll(d => d == null);
				}
			}
			catch (Exception e)
			{
				logger?.Error("Input hook loop terminated unexpectedly!", e);
			}
			finally
			{
				foreach (var device in devices)
				{
					device.Dispose();
				}

				devices.Clear();
			}
		}

		private void Scan()
		{
			bool hasKeyboardHooks, hasMouseHooks;

			lock (sync)
			{
				hasKeyboardHooks = keyboardHooks.Any();
				hasMouseHooks = mouseHooks.Any();

				if (!hasKeyboardHooks && !hasMouseHooks)
				{
					running = false;
					display?.Dispose();
					display = null;
				}
			}

			foreach (var device in devices.ToList())
			{
				var relevant = (device.Kind == InputDeviceKind.Keyboard && hasKeyboardHooks) || (device.Kind != InputDeviceKind.Keyboard && hasMouseHooks);

				if (!relevant)
				{
					device.Dispose();
					devices.Remove(device);
				}
			}

			if (!hasKeyboardHooks && !hasMouseHooks)
			{
				return;
			}

			if (!Directory.Exists("/dev/input"))
			{
				return;
			}

			foreach (var path in Directory.GetFiles("/dev/input", "event*"))
			{
				if (devices.Any(d => d.Path == path) || !InputDevice.TryOpen(path, out var device))
				{
					continue;
				}

				var relevant = (device.Kind == InputDeviceKind.Keyboard && hasKeyboardHooks) || (device.Kind != InputDeviceKind.Keyboard && hasMouseHooks);

				if (!relevant)
				{
					device.Dispose();
					continue;
				}

				if (device.Kind != InputDeviceKind.Touch && !device.Grab())
				{
					logger?.Warn($"Failed to grab input device '{device.Name}' ({path})! Is the process allowed to access /dev/input and /dev/uinput?");
					device.Dispose();
					continue;
				}

				logger?.Debug($"Hooked {device.Kind} device '{device.Name}' ({path}).");
				devices.Add(device);
			}
		}

		private void Process(InputDevice device)
		{
			foreach (var (type, code, value) in device.ReadEvents())
			{
				if (type == EV_KEY && device.Kind == InputDeviceKind.Keyboard)
				{
					ProcessKey(device, code, value);
				}
				else if (type == EV_KEY && device.Kind == InputDeviceKind.Pointer && IsMouseButton(code))
				{
					ProcessButton(device, code, value);
				}
				else if (device.Kind == InputDeviceKind.Touch)
				{
					ProcessTouch(device, type, code, value);
				}
				else if (device.IsGrabbed)
				{
					device.Forward(type, code, value);
				}
			}
		}

		private void ProcessKey(InputDevice device, int code, int value)
		{
			var keyCode = KeyMap.ToVirtualKey(code);
			var state = value == 0 ? KeyState.Released : KeyState.Pressed;
			var modifier = KeyModifier.None;

			if (code == KeyMap.KEY_LEFTCTRL || code == KeyMap.KEY_RIGHTCTRL)
			{
				ctrlPressed = value != 0;
			}
			else if (code == KeyMap.KEY_LEFTALT || code == KeyMap.KEY_RIGHTALT)
			{
				altPressed = value != 0;
			}

			if (altPressed)
			{
				modifier |= KeyModifier.Alt;
			}

			if (ctrlPressed)
			{
				modifier |= KeyModifier.Ctrl;
			}

			if (device.IsVirtual)
			{
				modifier |= KeyModifier.Injected;
			}

			var block = value == 2 ? device.IsBlocked(code) : InvokeKeyboard(keyCode, modifier, state);

			if (!block && BlockSystemSequences && value != 0)
			{
				block = IsSystemSequence(code);
			}

			if (value == 1 && block)
			{
				device.Block(code, value);
			}

			if (value == 0 && !device.WasForwarded(code) && !device.IsBlocked(code))
			{
				// Release of a key which was pressed before the hook was installed, forward to avoid stuck keys.
				device.Forward(EV_KEY, code, 0);
			}
			else if (value == 0 || !block)
			{
				device.Forward(EV_KEY, code, value);
			}

			device.Forward(EV_SYN, 0, 0);
		}

		private bool IsSystemSequence(int code)
		{
			if (!ctrlPressed || !altPressed)
			{
				return code == KeyMap.KEY_SYSRQ;
			}

			return (code >= KeyMap.KEY_F1 && code <= KeyMap.KEY_F10) || code == KeyMap.KEY_F11 || code == KeyMap.KEY_F12
				|| code == KeyMap.KEY_BACKSPACE || code == KeyMap.KEY_DELETE;
		}

		private bool InvokeKeyboard(int keyCode, KeyModifier modifier, KeyState state)
		{
			List<(Guid id, KeyboardHookCallback callback)> hooks;

			lock (sync)
			{
				hooks = keyboardHooks.ToList();
			}

			for (var i = hooks.Count - 1; i >= 0; i--)
			{
				try
				{
					if (hooks[i].callback.Invoke(keyCode, modifier, state))
					{
						return true;
					}
				}
				catch (Exception e)
				{
					logger?.Error("Keyboard hook callback failed!", e);
				}
			}

			return false;
		}

		private void ProcessButton(InputDevice device, int code, int value)
		{
			var button = code == BTN_LEFT ? MouseButton.Left : code == BTN_RIGHT ? MouseButton.Right : code == BTN_MIDDLE ? MouseButton.Middle : MouseButton.Auxiliary;
			var state = value == 0 ? MouseButtonState.Released : MouseButtonState.Pressed;
			var block = value == 2 ? device.IsBlocked(code) : InvokeMouse(button, state, new MouseInformation { IsTouch = false, X = CursorX(), Y = CursorY() });

			if (value == 1 && block)
			{
				device.Block(code, value);
			}

			if (value == 0 || !block)
			{
				device.Forward(EV_KEY, code, value);
			}
		}

		private void ProcessTouch(InputDevice device, int type, int code, int value)
		{
			if (type == EV_ABS && (code == 0 || code == 0x35))
			{
				touchX = (int) ((long) value * ScreenWidth() / device.AbsMaxX);
			}
			else if (type == EV_ABS && (code == 1 || code == 0x36))
			{
				touchY = (int) ((long) value * ScreenHeight() / device.AbsMaxY);
			}
			else if (type == EV_KEY && code == BTN_TOUCH && value != 2)
			{
				var state = value == 0 ? MouseButtonState.Released : MouseButtonState.Pressed;

				InvokeMouse(MouseButton.Left, state, new MouseInformation { IsTouch = true, X = touchX, Y = touchY });
			}
		}

		private bool InvokeMouse(MouseButton button, MouseButtonState state, MouseInformation info)
		{
			List<(Guid id, MouseHookCallback callback)> hooks;

			lock (sync)
			{
				hooks = mouseHooks.ToList();
			}

			for (var i = hooks.Count - 1; i >= 0; i--)
			{
				try
				{
					if (hooks[i].callback.Invoke(button, state, info))
					{
						return true;
					}
				}
				catch (Exception e)
				{
					logger?.Error("Mouse hook callback failed!", e);
				}
			}

			return false;
		}

		private static bool IsMouseButton(int code)
		{
			return code >= BTN_LEFT && code <= 0x117;
		}

		internal (int x, int y) CursorPosition()
		{
			return (CursorX(), CursorY());
		}

		private int CursorX() => QueryPointer().x;
		private int CursorY() => QueryPointer().y;

		private (int x, int y) QueryPointer()
		{
			var d = display;

			if (d != null)
			{
				lock (d.SyncRoot)
				{
					if (X11Display.XQueryPointer(d.Handle, d.Root, out _, out _, out var x, out var y, out _, out _, out _) != 0)
					{
						return (x, y);
					}
				}
			}

			return (0, 0);
		}

		private int ScreenWidth() => display != null ? X11Display.XDisplayWidth(display.Handle, 0) : 1;
		private int ScreenHeight() => display != null ? X11Display.XDisplayHeight(display.Handle, 0) : 1;

		public void Dispose()
		{
			running = false;
			thread?.Join(1000);
		}
	}
}
