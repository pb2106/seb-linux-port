/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SafeExamBrowser.LinuxApi.Input;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;
using SafeExamBrowser.WindowsApi.Contracts.Events;

namespace SafeExamBrowser.LinuxApi
{
	/// <summary>
	/// Linux implementation of <see cref="INativeMethods"/>, based on X11/EWMH, evdev/uinput, ptrace, gsettings and the freedesktop services.
	/// Window handles are X11 window IDs. Windows of Wayland-native applications are not visible via X11, hence kiosk sessions
	/// should be run on X11 (or on the dedicated X server created by the desktop factory).
	/// </summary>
	public class NativeMethods : INativeMethods
	{
		private readonly ILogger logger;
		private readonly InputHookManager inputHooks;
		private readonly ConcurrentDictionary<Guid, SystemEventHook> systemHooks = new ConcurrentDictionary<Guid, SystemEventHook>();
		private readonly ThreadSuspender suspender = new ThreadSuspender();
		private readonly object sync = new object();

		private X11Display display;
		private IntPtr strutWindow;
		private IBounds originalWorkingArea;
		private System.Diagnostics.Process inhibitor;
		private bool displayWasConfigured;

		/// <summary>
		/// Enables blocking of the key sequences which allow to leave the kiosk session (Ctrl+Alt+Fn, Ctrl+Alt+Backspace, Ctrl+Alt+Delete, SysRq).
		/// </summary>
		public bool BlockSystemKeySequences
		{
			get { return inputHooks.BlockSystemSequences; }
			set { inputHooks.BlockSystemSequences = value; }
		}

		public NativeMethods(ILogger logger = null)
		{
			this.logger = logger;
			this.inputHooks = new InputHookManager(logger);
		}

		public void ActivateWindow(IntPtr handle)
		{
			With(d => d.SendClientMessage(handle, "_NET_ACTIVE_WINDOW", 2));
		}

		public void DeregisterKeyboardHook(Guid hookId)
		{
			inputHooks.DeregisterKeyboard(hookId);
		}

		public void DeregisterMouseHook(Guid hookId)
		{
			inputHooks.DeregisterMouse(hookId);
		}

		public void DeregisterSystemEventHook(Guid hookId)
		{
			if (systemHooks.TryRemove(hookId, out var hook))
			{
				hook.Dispose();
			}
		}

		public bool DisableStickyKeys()
		{
			return TrySetStickyKeys(new StickyKeysState { IsAvailable = false, IsEnabled = false, IsHotkeyActive = false });
		}

		public void EmptyClipboard()
		{
			With(d =>
			{
				foreach (var selection in new[] { "CLIPBOARD", "PRIMARY", "SECONDARY" })
				{
					lock (d.SyncRoot)
					{
						X11Display.XSetSelectionOwner(d.Handle, d.Atom(selection), IntPtr.Zero, IntPtr.Zero);
					}
				}

				d.Flush();
			});

			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) && Tool.Exists("wl-copy"))
			{
				Tool.Run("wl-copy", "--clear", out _);
				Tool.Run("wl-copy", "--primary --clear", out _);
			}
			else if (Tool.Exists("xclip"))
			{
				Tool.Run("sh", "-c \"xclip -selection clipboard -i /dev/null; xclip -selection primary -i /dev/null\"", out _);
			}
		}

		public bool EnableStickyKeys()
		{
			return TrySetStickyKeys(new StickyKeysState { IsAvailable = true, IsEnabled = true, IsHotkeyActive = true });
		}

		public IEnumerable<IntPtr> GetAllWindows()
		{
			return With(d => WindowEnumerator.GetAllTopLevelWindows(d), new List<IntPtr>());
		}

		public (int x, int y) GetCursorPosition()
		{
			return With(d =>
			{
				lock (d.SyncRoot)
				{
					return X11Display.XQueryPointer(d.Handle, d.Root, out _, out _, out var x, out var y, out _, out _, out _) != 0 ? (x, y) : (0, 0);
				}
			}, (0, 0));
		}

		public IEnumerable<IntPtr> GetInteractiveWindows()
		{
			return With(d =>
			{
				var windows = new List<IntPtr>();

				foreach (var window in WindowEnumerator.GetClientWindows(d))
				{
					var states = WindowEnumerator.States(d, window).ToList();
					var types = WindowEnumerator.Types(d, window).ToList();
					var skip = states.Contains("_NET_WM_STATE_SKIP_TASKBAR");
					var interactive = !types.Any() || types.Contains("_NET_WM_WINDOW_TYPE_NORMAL") || types.Contains("_NET_WM_WINDOW_TYPE_DIALOG");

					if (!skip && interactive && d.TryGetAttributes(window, out _))
					{
						windows.Add(window);
					}
				}

				return windows;
			}, new List<IntPtr>());
		}

		public uint GetProcessIdFor(IntPtr window)
		{
			return With(d => d.GetLongProperty(window, "_NET_WM_PID", 1) is long[] pid && pid.Length > 0 ? (uint) pid[0] : 0u, 0u);
		}

		public IntPtr GetShellWindowHandle()
		{
			return With(d => FindShellWindow(d), IntPtr.Zero);
		}

		public uint GetShellProcessId()
		{
			return With(d =>
			{
				var window = FindShellWindow(d);

				return window != IntPtr.Zero ? GetProcessIdFor(window) : 0u;
			}, 0u);
		}

		public string GetWallpaperPath()
		{
			if (Tool.Exists("gsettings") && Tool.Run("gsettings", "get org.gnome.desktop.background picture-uri", out var uri))
			{
				var path = uri.Trim('\'');

				return path.StartsWith("file://") ? Uri.UnescapeDataString(path.Substring(7)) : path;
			}

			if (Tool.Exists("xfconf-query") && Tool.Run("xfconf-query", "-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image", out var image))
			{
				return image;
			}

			return default;
		}

		public IntPtr GetWindowIcon(IntPtr window)
		{
			return With(d =>
			{
				var data = d.GetProperty(window, "_NET_WM_ICON", 1 << 20, out var format, out var count, out _);

				if (data == null || format != 32 || count < 3)
				{
					return IntPtr.Zero;
				}

				var best = default(WindowIcon);
				var offset = 0L;

				while (offset + 2 < count)
				{
					var width = (int) BitConverter.ToInt64(data, (int) offset * IntPtr.Size);
					var height = (int) BitConverter.ToInt64(data, (int) (offset + 1) * IntPtr.Size);
					var size = (long) width * height;

					if (width <= 0 || height <= 0 || offset + 2 + size > count)
					{
						break;
					}

					if (best == null || width * height > best.Width * best.Height)
					{
						var pixels = new uint[size];

						for (var i = 0; i < size; i++)
						{
							pixels[i] = (uint) BitConverter.ToInt64(data, (int) (offset + 2 + i) * IntPtr.Size);
						}

						best = new WindowIcon { Width = width, Height = height, Pixels = pixels };
					}

					offset += 2 + size;
				}

				return best != null ? GCHandle.ToIntPtr(GCHandle.Alloc(best)) : IntPtr.Zero;
			}, IntPtr.Zero);
		}

		public IWindowStyle GetWindowStyle(IntPtr window)
		{
			return With(d =>
			{
				var states = WindowEnumerator.States(d, window).ToList();
				var types = WindowEnumerator.Types(d, window).ToList();
				var raw = states.Concat(types).ToList();
				var visible = d.TryGetAttributes(window, out var attributes) && attributes.map_state == X11Display.IsViewable && !states.Contains("_NET_WM_STATE_HIDDEN");
				var passive = new[] { "DOCK", "DESKTOP", "TOOLTIP", "NOTIFICATION", "DND", "SPLASH", "POPUP_MENU", "DROPDOWN_MENU", "COMBO", "UTILITY" };
				var hints = d.GetLongProperty(window, "WM_HINTS", 2);
				var acceptsInput = hints.Length < 2 || (hints[0] & 1) == 0 || hints[1] != 0;

				return (IWindowStyle) new WindowStyle(raw)
				{
					IsDisabled = !acceptsInput,
					IsNotActivatable = types.Any(t => passive.Any(p => t == "_NET_WM_WINDOW_TYPE_" + p)),
					IsTopmost = states.Contains("_NET_WM_STATE_ABOVE"),
					IsVisible = visible
				};
			}, new WindowStyle(new string[0]));
		}

		public string GetWindowTitle(IntPtr window)
		{
			return With(d => d.GetStringProperty(window, "_NET_WM_NAME") ?? d.GetStringProperty(window, "WM_NAME") ?? string.Empty, string.Empty);
		}

		public IBounds GetWorkingArea()
		{
			return With(d =>
			{
				var area = d.GetLongProperty(d.Root, "_NET_WORKAREA", 4);

				if (area.Length >= 4)
				{
					return (IBounds) new Bounds { Left = (int) area[0], Top = (int) area[1], Right = (int) (area[0] + area[2]), Bottom = (int) (area[1] + area[3]) };
				}

				return new Bounds { Left = 0, Top = 0, Right = X11Display.XDisplayWidth(d.Handle, 0), Bottom = X11Display.XDisplayHeight(d.Handle, 0) };
			}, new Bounds());
		}

		public bool HasInternetConnection()
		{
			try
			{
				return NetworkInterface.GetAllNetworkInterfaces().Any(n =>
					n.OperationalStatus == OperationalStatus.Up
					&& n.NetworkInterfaceType != NetworkInterfaceType.Loopback
					&& n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)));
			}
			catch
			{
				return false;
			}
		}

		public bool HideWindow(IntPtr window)
		{
			return With(d =>
			{
				if (!d.Exists(window))
				{
					return false;
				}

				lock (d.SyncRoot)
				{
					X11Display.XUnmapWindow(d.Handle, window);
				}

				d.Flush();

				return true;
			}, false);
		}

		public bool IsExistingWindow(IntPtr window)
		{
			return With(d => d.Exists(window), false);
		}

		public bool IsMinimizedWindow(IntPtr window)
		{
			return With(d => WindowEnumerator.States(d, window).Contains("_NET_WM_STATE_HIDDEN"), false);
		}

		public void MinimizeAllOpenWindows()
		{
			With(d =>
			{
				foreach (var window in GetInteractiveWindows())
				{
					d.SendClientMessage(window, "WM_CHANGE_STATE", X11Display.IconicState);
				}
			});
		}

		public void PostCloseMessageToShell()
		{
			var pid = GetShellProcessId();

			if (pid != 0)
			{
				LibC.kill((int) pid, LibC.SIGTERM);
			}
		}

		public Guid RegisterKeyboardHook(KeyboardHookCallback callback)
		{
			return inputHooks.Register(callback);
		}

		public Guid RegisterMouseHook(MouseHookCallback callback)
		{
			return inputHooks.Register(callback);
		}

		public Guid RegisterSystemCaptureStartEvent(SystemEventCallback callback)
		{
			return RegisterSystemHook(SystemEventKind.CaptureStart, callback);
		}

		public Guid RegisterSystemForegroundEvent(SystemEventCallback callback)
		{
			return RegisterSystemHook(SystemEventKind.Foreground, callback);
		}

		public void RemoveWallpaper()
		{
			if (Tool.Exists("gsettings"))
			{
				Tool.Run("gsettings", "set org.gnome.desktop.background picture-options none", out _);
				Tool.Run("gsettings", "set org.gnome.desktop.background picture-uri ''", out _);
				Tool.Run("gsettings", "set org.gnome.desktop.background picture-uri-dark ''", out _);
				Tool.Run("gsettings", "set org.gnome.desktop.background primary-color '#000000'", out _);
			}

			if (Tool.Exists("xfconf-query"))
			{
				Tool.Run("xfconf-query", "-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/image-style -s 0", out _);
			}

			if (Tool.Exists("xsetroot") && Environment.GetEnvironmentVariable("DISPLAY") != null)
			{
				Tool.Run("xsetroot", "-solid black", out _);
			}
		}

		public void RestoreWindow(IntPtr window)
		{
			With(d =>
			{
				lock (d.SyncRoot)
				{
					X11Display.XMapWindow(d.Handle, window);
				}

				d.SendClientMessage(window, "_NET_ACTIVE_WINDOW", 2);
			});
		}

		public bool ResumeThread(int threadId)
		{
			return suspender.Resume(threadId);
		}

		public void SendCloseMessageTo(IntPtr window)
		{
			With(d => d.SendClientMessage(window, "_NET_CLOSE_WINDOW", 0, 2));
		}

		public void SetAlwaysOnState(bool display = true, bool system = true)
		{
			lock (sync)
			{
				if (inhibitor != null && !inhibitor.HasExited)
				{
					inhibitor.Kill();
				}

				inhibitor = null;

				if (display || system)
				{
					var what = string.Join(":", new[] { display ? "idle" : null, system ? "sleep" : null }.Where(w => w != null));

					try
					{
						inhibitor = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("systemd-inhibit",
							$"--what={what} --who=\"Safe Exam Browser\" --why=\"An exam session is running\" --mode=block sleep infinity") { UseShellExecute = false });
					}
					catch (System.ComponentModel.Win32Exception)
					{
						logger?.Warn("Failed to start systemd-inhibit, the system might go to sleep during the session!");
					}

					if (display && Tool.Exists("xset") && Environment.GetEnvironmentVariable("DISPLAY") != null)
					{
						Tool.Run("xset", "s off -dpms", out _);
						displayWasConfigured = true;
					}
				}
				else if (displayWasConfigured && Tool.Exists("xset"))
				{
					Tool.Run("xset", "s on +dpms", out _);
					displayWasConfigured = false;
				}
			}
		}

		public void SetWallpaper(string filePath)
		{
			if (Tool.Exists("gsettings"))
			{
				var uri = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;

				Tool.Run("gsettings", $"set org.gnome.desktop.background picture-options zoom", out _);
				Tool.Run("gsettings", $"set org.gnome.desktop.background picture-uri '{uri}'", out _);
				Tool.Run("gsettings", $"set org.gnome.desktop.background picture-uri-dark '{uri}'", out _);
			}

			if (Tool.Exists("xfconf-query"))
			{
				Tool.Run("xfconf-query", $"-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image -s \"{filePath}\"", out _);
			}

			if (Tool.Exists("feh") && Environment.GetEnvironmentVariable("DISPLAY") != null)
			{
				Tool.Run("feh", $"--bg-fill \"{filePath}\"", out _);
			}
		}

		/// <summary>
		/// Reserves everything outside the specified bounds using an invisible EWMH dock window (_NET_WM_STRUT_PARTIAL), which is the Linux
		/// equivalent of adjusting the working area on Windows. Specifying the original working area removes the reservation again.
		/// </summary>
		public void SetWorkingArea(IBounds bounds)
		{
			With(d =>
			{
				originalWorkingArea = originalWorkingArea ?? GetWorkingArea();

				var width = X11Display.XDisplayWidth(d.Handle, 0);
				var height = X11Display.XDisplayHeight(d.Handle, 0);

				if (strutWindow != IntPtr.Zero)
				{
					lock (d.SyncRoot)
					{
						X11Display.XDestroyWindow(d.Handle, strutWindow);
					}

					strutWindow = IntPtr.Zero;
				}

				var left = Math.Max(0, bounds.Left);
				var top = Math.Max(0, bounds.Top);
				var right = Math.Max(0, width - bounds.Right);
				var bottom = Math.Max(0, height - bounds.Bottom);

				if (left + top + right + bottom == 0)
				{
					d.Flush();

					return;
				}

				lock (d.SyncRoot)
				{
					strutWindow = X11Display.XCreateSimpleWindow(d.Handle, d.Root, 0, 0, 1, 1, 0, IntPtr.Zero, IntPtr.Zero);
					X11Display.XStoreName(d.Handle, strutWindow, "SEB working area");
				}

				d.SetAtomProperty(strutWindow, "_NET_WM_WINDOW_TYPE", "_NET_WM_WINDOW_TYPE_DOCK");
				d.SetLongProperty(strutWindow, "_NET_WM_STRUT", "CARDINAL", left, right, top, bottom);
				d.SetLongProperty(strutWindow, "_NET_WM_STRUT_PARTIAL", "CARDINAL", left, right, top, bottom, 0, height, 0, height, 0, width, 0, width);

				lock (d.SyncRoot)
				{
					X11Display.XMapWindow(d.Handle, strutWindow);
				}

				d.Flush();
			});
		}

		public bool SuspendThread(int threadId)
		{
			return suspender.Suspend(threadId);
		}

		public bool TryGetStickyKeys(out IStickyKeysState state)
		{
			state = default;

			if (Tool.Exists("gsettings")
				&& Tool.Run("gsettings", "get org.gnome.desktop.a11y.keyboard stickykeys-enable", out var enabled)
				&& Tool.Run("gsettings", "get org.gnome.desktop.a11y.keyboard enable", out var hotkey))
			{
				state = new StickyKeysState { IsAvailable = true, IsEnabled = enabled == "true", IsHotkeyActive = hotkey == "true" };
			}
			else if (Tool.Exists("xkbset") && Tool.Run("xkbset", "q", out var query))
			{
				state = new StickyKeysState { IsAvailable = true, IsEnabled = query.Contains("Sticky-Keys = On"), IsHotkeyActive = query.Contains("Exp") };
			}

			return state != default;
		}

		public bool TrySetStickyKeys(IStickyKeysState state)
		{
			var success = false;

			if (Tool.Exists("gsettings"))
			{
				success = Tool.Run("gsettings", $"set org.gnome.desktop.a11y.keyboard stickykeys-enable {state.IsEnabled.ToString().ToLower()}", out _)
					& Tool.Run("gsettings", $"set org.gnome.desktop.a11y.keyboard enable {state.IsHotkeyActive.ToString().ToLower()}", out _);
			}

			if (Tool.Exists("xkbset") && Environment.GetEnvironmentVariable("DISPLAY") != null)
			{
				success |= Tool.Run("xkbset", state.IsEnabled ? "sticky" : "-sticky", out _) & Tool.Run("xkbset", state.IsHotkeyActive ? "exp =sticky" : "exp -sticky", out _);
			}

			return success;
		}

		private IntPtr FindShellWindow(X11Display d)
		{
			var windowManager = d.GetLongProperty(d.Root, "_NET_SUPPORTING_WM_CHECK", 1);
			var wmWindow = windowManager.Length > 0 ? new IntPtr(windowManager[0]) : IntPtr.Zero;
			var wmPid = wmWindow != IntPtr.Zero ? d.GetLongProperty(wmWindow, "_NET_WM_PID", 1) : new long[0];

			foreach (var window in WindowEnumerator.GetAllTopLevelWindows(d))
			{
				var types = WindowEnumerator.Types(d, window);

				if (types.Contains("_NET_WM_WINDOW_TYPE_DOCK") || types.Contains("_NET_WM_WINDOW_TYPE_DESKTOP"))
				{
					var pid = d.GetLongProperty(window, "_NET_WM_PID", 1);

					if (window != strutWindow && pid.Length > 0 && !(wmPid.Length > 0 && pid[0] == wmPid[0]))
					{
						return window;
					}
				}
			}

			return IntPtr.Zero;
		}

		private Guid RegisterSystemHook(SystemEventKind kind, SystemEventCallback callback)
		{
			var hook = new SystemEventHook(logger, kind, callback);
			var id = Guid.NewGuid();

			hook.Start();
			systemHooks[id] = hook;

			return id;
		}

		private X11Display Display()
		{
			lock (sync)
			{
				if (display == null || display.Handle == IntPtr.Zero)
				{
					X11Display.TryOpen(out display);
				}

				return display;
			}
		}

		private void With(Action<X11Display> action)
		{
			var d = Display();

			if (d != null)
			{
				action(d);
			}
		}

		private T With<T>(Func<X11Display, T> function, T fallback)
		{
			var d = Display();

			return d != null ? function(d) : fallback;
		}
	}
}
