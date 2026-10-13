/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Linq;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Monitoring.Contracts.Keyboard;
using SafeExamBrowser.Settings.Monitoring;
using SafeExamBrowser.WindowsApi.Contracts;
using SafeExamBrowser.WindowsApi.Contracts.Events;

namespace SafeExamBrowser.Monitoring.Keyboard
{
	public class KeyboardInterceptor : IKeyboardInterceptor
	{
		// Windows Virtual Key codes (same values used by LinuxApi's KeyMap evdev→VK mapping)
		private const int VK_ESCAPE      = 0x1B;
		private const int VK_SPACE       = 0x20;
		private const int VK_TAB         = 0x09;
		private const int VK_SNAPSHOT    = 0x2C; // Print Screen
		private const int VK_APPS        = 0x5D; // Menu / Apps key
		private const int VK_LWIN        = 0x5B;
		private const int VK_RWIN        = 0x5C;
		private const int VK_F1          = 0x70;
		private const int VK_F2          = 0x71;
		private const int VK_F3          = 0x72;
		private const int VK_F4          = 0x73;
		private const int VK_F5          = 0x74;
		private const int VK_F6          = 0x75;
		private const int VK_F7          = 0x76;
		private const int VK_F8          = 0x77;
		private const int VK_F9          = 0x78;
		private const int VK_F10         = 0x79;
		private const int VK_F11         = 0x7A;
		private const int VK_F12         = 0x7B;
		private const int VK_C           = 0x43;
		private const int VK_V           = 0x56;
		private const int VK_X           = 0x58;

		private Guid? hookId;
		private readonly ILogger logger;
		private readonly INativeMethods nativeMethods;
		private readonly KeyboardSettings settings;

		public KeyboardInterceptor(ILogger logger, INativeMethods nativeMethods, KeyboardSettings settings)
		{
			this.logger = logger;
			this.nativeMethods = nativeMethods;
			this.settings = settings;
		}

		public void Start()
		{
			hookId = nativeMethods.RegisterKeyboardHook(KeyboardHookCallback);
		}

		public void Stop()
		{
			if (hookId.HasValue)
			{
				nativeMethods.DeregisterKeyboardHook(hookId.Value);
			}
		}

		private bool KeyboardHookCallback(int keyCode, KeyModifier modifier, KeyState state)
		{
			var block = false;

			block |= keyCode == VK_APPS;
			block |= keyCode == VK_ESCAPE && modifier == KeyModifier.None && !settings.AllowEsc;
			block |= keyCode == VK_F1  && !settings.AllowF1;
			block |= keyCode == VK_F2  && !settings.AllowF2;
			block |= keyCode == VK_F3  && !settings.AllowF3;
			block |= keyCode == VK_F4  && !settings.AllowF4;
			block |= keyCode == VK_F5  && !settings.AllowF5;
			block |= keyCode == VK_F6  && !settings.AllowF6;
			block |= keyCode == VK_F7  && !settings.AllowF7;
			block |= keyCode == VK_F8  && !settings.AllowF8;
			block |= keyCode == VK_F9  && !settings.AllowF9;
			block |= keyCode == VK_F10 && !settings.AllowF10;
			block |= keyCode == VK_F11 && !settings.AllowF11;
			block |= keyCode == VK_F12 && !settings.AllowF12;
			block |= keyCode == VK_LWIN && !settings.AllowSystemKey;
			block |= keyCode == VK_SNAPSHOT && !settings.AllowPrintScreen;
			block |= keyCode == VK_RWIN && !settings.AllowSystemKey;

			block |= modifier.HasFlag(KeyModifier.Alt) && keyCode == VK_ESCAPE  && !settings.AllowAltEsc;
			block |= modifier.HasFlag(KeyModifier.Alt) && keyCode == VK_F4      && !settings.AllowAltF4;
			block |= modifier.HasFlag(KeyModifier.Alt) && keyCode == VK_SPACE;
			block |= modifier.HasFlag(KeyModifier.Alt) && keyCode == VK_TAB;

			block |= modifier.HasFlag(KeyModifier.Ctrl) && keyCode == VK_C      && !settings.AllowCtrlC;
			block |= modifier.HasFlag(KeyModifier.Ctrl) && keyCode == VK_ESCAPE && !settings.AllowCtrlEsc;
			block |= modifier.HasFlag(KeyModifier.Ctrl) && keyCode == VK_V      && !settings.AllowCtrlV;
			block |= modifier.HasFlag(KeyModifier.Ctrl) && keyCode == VK_X      && !settings.AllowCtrlX;

			block |= modifier.HasFlag(KeyModifier.Injected) && !settings.AllowInjected;

			if (block)
			{
				Log(keyCode, modifier, state);
			}

			return block;
		}

		private void Log(int keyCode, KeyModifier modifier, KeyState state)
		{
			var modifierFlags = Enum.GetValues(typeof(KeyModifier)).OfType<KeyModifier>().Where(m => m != KeyModifier.None && modifier.HasFlag(m));
			var modifiers = modifierFlags.Any() ? String.Join(" + ", modifierFlags) + " + " : string.Empty;

			logger.Info($"Blocked '{modifiers}0x{keyCode:X2}' (keyCode={keyCode}) when {state.ToString().ToLower()}.");
		}
	}
}
