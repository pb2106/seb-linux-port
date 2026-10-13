/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using SafeExamBrowser.WindowsApi.Contracts;
using SafeExamBrowser.WindowsApi.Contracts.Events;

namespace SafeExamBrowser.UserInterface.Shared.Activators
{
	public enum Key
	{
		None = 0,
		Tab = 0x09,
		Up = 0x26,
		A = 0x41,
		Q = 0x51,
		LWin = 0x5B,
		RWin = 0x5C,
		LeftShift = 0xA0,
		RightShift = 0xA1,
		LeftCtrl = 0xA2,
		RightCtrl = 0xA3
	}

	internal static class KeyInterop
	{
		internal static Key KeyFromVirtualKey(int virtualKey)
		{
			switch (virtualKey)
			{
				case 0x09: return Key.Tab;
				case 0x10:
				case 0xA0: return Key.LeftShift;
				case 0xA1: return Key.RightShift;
				case 0x11:
				case 0xA2: return Key.LeftCtrl;
				case 0xA3: return Key.RightCtrl;
				case 0x26: return Key.Up;
				case 0x41: return Key.A;
				case 0x51: return Key.Q;
				case 0x5B: return Key.LWin;
				case 0x5C: return Key.RWin;
				default: return Key.None;
			}
		}
	}

	public abstract class KeyboardActivator
	{
		private Guid? hookId;
		private INativeMethods nativeMethods;
		private bool paused;

		protected KeyboardActivator(INativeMethods nativeMethods)
		{
			this.nativeMethods = nativeMethods;
		}

		protected abstract bool Process(Key key, KeyModifier modifier, KeyState state);

		public void Pause()
		{
			OnBeforePause();
			paused = true;
		}

		public void Resume()
		{
			OnBeforeResume();
			paused = false;
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

		protected virtual void OnBeforePause()
		{
		}

		protected virtual void OnBeforeResume()
		{
		}

		private bool KeyboardHookCallback(int keyCode, KeyModifier modifier, KeyState state)
		{
			if (!paused)
			{
				return Process(KeyInterop.KeyFromVirtualKey(keyCode), modifier, state);
			}

			return false;
		}
	}
}
