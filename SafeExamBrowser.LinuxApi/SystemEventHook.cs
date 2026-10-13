/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Runtime.InteropServices;
using System.Threading;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.WindowsApi.Contracts.Events;

namespace SafeExamBrowser.LinuxApi
{
	internal enum SystemEventKind
	{
		Foreground,
		CaptureStart
	}

	/// <summary>
	/// Listens for X11 events on the root window: changes of the active window (foreground) and newly mapped top-level windows (capture start).
	/// </summary>
	internal class SystemEventHook : IDisposable
	{
		private readonly SystemEventCallback callback;
		private readonly SystemEventKind kind;
		private readonly ILogger logger;

		private X11Display display;
		private volatile bool running;
		private Thread thread;

		internal SystemEventHook(ILogger logger, SystemEventKind kind, SystemEventCallback callback)
		{
			this.callback = callback;
			this.kind = kind;
			this.logger = logger;
		}

		internal void Start()
		{
			if (!X11Display.TryOpen(out display))
			{
				logger?.Warn($"Failed to connect to the X server, the {kind} event hook is unavailable!");

				return;
			}

			var mask = kind == SystemEventKind.Foreground ? X11Display.PropertyChangeMask : X11Display.SubstructureNotifyMask;

			X11Display.XSelectInput(display.Handle, display.Root, new IntPtr(mask));
			X11Display.XFlush(display.Handle);

			running = true;
			thread = new Thread(Loop) { IsBackground = true, Name = $"SEB {kind} event hook" };
			thread.Start();
		}

		private void Loop()
		{
			var buffer = Marshal.AllocHGlobal(192);
			var activeWindow = display.Atom("_NET_ACTIVE_WINDOW");
			var fd = X11Display.XConnectionNumber(display.Handle);

			try
			{
				while (running)
				{
					int pending;

					lock (display.SyncRoot)
					{
						pending = X11Display.XPending(display.Handle);
					}

					if (pending == 0)
					{
						var fds = new[] { new LibC.PollFd { fd = fd, events = LibC.POLLIN } };

						LibC.poll(fds, 1, 200);

						continue;
					}

					int type;
					IntPtr window, atom;

					lock (display.SyncRoot)
					{
						X11Display.XNextEvent(display.Handle, buffer);

						type = Marshal.ReadInt32(buffer, 0);
						window = Marshal.ReadIntPtr(buffer, 40);
						atom = Marshal.ReadIntPtr(buffer, 40);
					}

					if (kind == SystemEventKind.Foreground && type == X11Display.PropertyNotify && atom == activeWindow)
					{
						var active = display.GetLongProperty(display.Root, "_NET_ACTIVE_WINDOW", 1);

						if (active.Length > 0 && active[0] != 0)
						{
							callback.Invoke(new IntPtr(active[0]));
						}
					}
					else if (kind == SystemEventKind.CaptureStart && type == X11Display.MapNotify)
					{
						var client = WindowEnumerator.ResolveClient(display, window);

						if (client != IntPtr.Zero)
						{
							callback.Invoke(client);
						}
					}
				}
			}
			catch (Exception e)
			{
				logger?.Error($"The {kind} event hook terminated unexpectedly!", e);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		public void Dispose()
		{
			running = false;
			thread?.Join(1000);
			display?.Dispose();
		}
	}
}
