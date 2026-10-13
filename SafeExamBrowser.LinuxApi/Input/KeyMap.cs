/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;

namespace SafeExamBrowser.LinuxApi.Input
{
	/// <summary>
	/// Translates Linux evdev key codes (linux/input-event-codes.h) to Windows virtual-key codes, as the SEB keyboard
	/// callbacks and interceptors are specified in terms of virtual-key codes.
	/// </summary>
	internal static class KeyMap
	{
		internal const int KEY_LEFTCTRL = 29;
		internal const int KEY_RIGHTCTRL = 97;
		internal const int KEY_LEFTALT = 56;
		internal const int KEY_RIGHTALT = 100;
		internal const int KEY_BACKSPACE = 14;
		internal const int KEY_DELETE = 111;
		internal const int KEY_F1 = 59;
		internal const int KEY_F10 = 68;
		internal const int KEY_F11 = 87;
		internal const int KEY_F12 = 88;
		internal const int KEY_SYSRQ = 99;

		internal const int VK_LCONTROL = 0xA2;
		internal const int VK_RCONTROL = 0xA3;
		internal const int VK_LMENU = 0xA4;
		internal const int VK_RMENU = 0xA5;
		internal const int VK_DELETE = 0x2E;

		private static readonly Dictionary<int, int> Map = new Dictionary<int, int>();

		static KeyMap()
		{
			Add(1, 0x1B);
			for (var i = 0; i < 9; i++) Add(2 + i, 0x31 + i); // 1-9
			Add(11, 0x30); // 0
			Add(12, 0xBD); Add(13, 0xBB); Add(14, 0x08); Add(15, 0x09);

			var row1 = "QWERTYUIOP";
			for (var i = 0; i < row1.Length; i++) Add(16 + i, row1[i]);
			Add(26, 0xDB); Add(27, 0xDD); Add(28, 0x0D); Add(29, VK_LCONTROL);

			var row2 = "ASDFGHJKL";
			for (var i = 0; i < row2.Length; i++) Add(30 + i, row2[i]);
			Add(39, 0xBA); Add(40, 0xDE); Add(41, 0xC0); Add(42, 0xA0); Add(43, 0xDC);

			var row3 = "ZXCVBNM";
			for (var i = 0; i < row3.Length; i++) Add(44 + i, row3[i]);
			Add(51, 0xBC); Add(52, 0xBE); Add(53, 0xBF); Add(54, 0xA1); Add(55, 0x6A);
			Add(56, VK_LMENU); Add(57, 0x20); Add(58, 0x14);

			for (var i = 0; i < 10; i++) Add(59 + i, 0x70 + i); // F1-F10
			Add(69, 0x90); Add(70, 0x91);
			Add(71, 0x67); Add(72, 0x68); Add(73, 0x69); Add(74, 0x6D);
			Add(75, 0x64); Add(76, 0x65); Add(77, 0x66); Add(78, 0x6B);
			Add(79, 0x61); Add(80, 0x62); Add(81, 0x63); Add(82, 0x60); Add(83, 0x6E);
			Add(86, 0xE2); Add(87, 0x7A); Add(88, 0x7B);
			Add(96, 0x0D); Add(97, VK_RCONTROL); Add(98, 0x6F); Add(99, 0x2C); Add(100, VK_RMENU);
			Add(102, 0x24); Add(103, 0x26); Add(104, 0x21); Add(105, 0x25); Add(106, 0x27);
			Add(107, 0x23); Add(108, 0x28); Add(109, 0x22); Add(110, 0x2D); Add(111, VK_DELETE);
			Add(119, 0x13); Add(125, 0x5B); Add(126, 0x5C); Add(127, 0x5D); Add(139, 0x5D);
			for (var i = 0; i < 12; i++) Add(183 + i, 0x7C + i); // F13-F24
		}

		internal static int ToVirtualKey(int evdevCode)
		{
			return Map.TryGetValue(evdevCode, out var vk) ? vk : 0;
		}

		private static void Add(int evdev, int vk)
		{
			Map[evdev] = vk;
		}
	}
}
