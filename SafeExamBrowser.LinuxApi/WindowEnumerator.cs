/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi
{
	internal class Bounds : IBounds
	{
		public int Left { get; set; }
		public int Top { get; set; }
		public int Right { get; set; }
		public int Bottom { get; set; }
	}

	internal class StickyKeysState : IStickyKeysState
	{
		public bool IsAvailable { get; set; }
		public bool IsEnabled { get; set; }
		public bool IsHotkeyActive { get; set; }
	}

	internal class WindowStyle : IWindowStyle
	{
		private readonly IEnumerable<string> raw;

		public bool IsDisabled { get; set; }
		public bool IsNotActivatable { get; set; }
		public bool IsTopmost { get; set; }
		public bool IsVisible { get; set; }

		internal WindowStyle(IEnumerable<string> raw)
		{
			this.raw = raw;
		}

		public IEnumerable<string> GetRawStyles()
		{
			return raw;
		}
	}

	/// <summary>
	/// The pixel data of a window icon (ARGB, 32 bit), as published by EWMH via _NET_WM_ICON. The window icon handle returned by
	/// <see cref="INativeMethods.GetWindowIcon(IntPtr)"/> on Linux is a GCHandle to an instance of this class, see <see cref="FromHandle(IntPtr)"/>.
	/// </summary>
	public class WindowIcon
	{
		public int Width { get; internal set; }
		public int Height { get; internal set; }
		public uint[] Pixels { get; internal set; }

		public static WindowIcon FromHandle(IntPtr handle)
		{
			return handle == IntPtr.Zero ? null : System.Runtime.InteropServices.GCHandle.FromIntPtr(handle).Target as WindowIcon;
		}
	}

	internal static class WindowEnumerator
	{
		/// <summary>
		/// Gets all managed client windows (EWMH _NET_CLIENT_LIST). Falls back to the children of the root window if the window manager does not provide the list.
		/// </summary>
		internal static IEnumerable<IntPtr> GetClientWindows(X11Display display)
		{
			var list = display.GetLongProperty(display.Root, "_NET_CLIENT_LIST");

			if (list.Length > 0)
			{
				return list.Select(l => new IntPtr(l)).ToList();
			}

			return display.Children(display.Root).Where(w => HasTypeOrPid(display, w)).ToList();
		}

		/// <summary>
		/// Gets all top-level windows including docks, desktops and unmanaged windows. Windows of reparenting window managers are resolved to the client window.
		/// </summary>
		internal static IEnumerable<IntPtr> GetAllTopLevelWindows(X11Display display)
		{
			var windows = new List<IntPtr>();

			foreach (var child in display.Children(display.Root))
			{
				if (HasTypeOrPid(display, child))
				{
					windows.Add(child);
				}
				else
				{
					windows.AddRange(display.Children(child).Where(w => HasTypeOrPid(display, w)));
				}
			}

			return windows.Union(GetClientWindows(display)).ToList();
		}

		internal static IntPtr ResolveClient(X11Display display, IntPtr window)
		{
			if (HasTypeOrPid(display, window))
			{
				return window;
			}

			return display.Children(window).FirstOrDefault(w => HasTypeOrPid(display, w));
		}

		internal static bool HasTypeOrPid(X11Display display, IntPtr window)
		{
			return display.GetLongProperty(window, "_NET_WM_PID", 1).Length > 0
				|| display.GetLongProperty(window, "_NET_WM_WINDOW_TYPE", 1).Length > 0
				|| display.GetLongProperty(window, "WM_STATE", 1).Length > 0;
		}

		internal static IEnumerable<string> States(X11Display display, IntPtr window)
		{
			return display.GetLongProperty(window, "_NET_WM_STATE").Select(a => display.AtomName(new IntPtr(a))).Where(n => n != null).ToList();
		}

		internal static IEnumerable<string> Types(X11Display display, IntPtr window)
		{
			return display.GetLongProperty(window, "_NET_WM_WINDOW_TYPE").Select(a => display.AtomName(new IntPtr(a))).Where(n => n != null).ToList();
		}
	}
}
