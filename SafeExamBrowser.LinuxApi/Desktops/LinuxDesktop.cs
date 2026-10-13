/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi.Desktops
{
	/// <summary>
	/// A Linux "desktop" is a dedicated X server, which is either started on a free virtual terminal (when privileged) or
	/// nested in fullscreen via Xephyr (when unprivileged), together with a window manager. It is protected by its own
	/// X authority cookie, so that other processes of the user can't connect to it.
	/// </summary>
	internal class LinuxDesktop : IDesktop
	{
		private const int VT_ACTIVATE = 0x5606;
		private const int VT_WAITACTIVE = 0x5607;

		private readonly bool isCurrent;
		private readonly IntPtr obfuscatedHandle;
		private readonly string obfuscatedName;
		private System.Diagnostics.Process server;
		private System.Diagnostics.Process windowManager;

		public IntPtr Handle { get { return obfuscatedHandle != IntPtr.Zero ? obfuscatedHandle : new IntPtr(Number); } }
		public string Name { get { return obfuscatedName ?? RealName; } }

		internal string RealName { get; }
		internal int Number { get; }
		internal int Vt { get; }
		internal string Display { get; }
		internal string AuthorityFile { get; }

		internal static LinuxDesktop CreateCurrent(string name)
		{
			var display = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
			var vt = ReadActiveVt();

			return new LinuxDesktop(name, ParseNumber(display), vt, display, Environment.GetEnvironmentVariable("XAUTHORITY"), null, null, true);
		}

		internal LinuxDesktop(string name, int number, int vt, string display, string authority, System.Diagnostics.Process server, System.Diagnostics.Process windowManager, bool isCurrent = false, IntPtr obfuscatedHandle = default, string obfuscatedName = default)
		{
			this.isCurrent = isCurrent;
			this.obfuscatedHandle = obfuscatedHandle;
			this.obfuscatedName = obfuscatedName;
			this.server = server;
			this.windowManager = windowManager;

			RealName = name;
			Number = number;
			Vt = vt;
			Display = display;
			AuthorityFile = authority;
		}

		public void Activate()
		{
			if (Vt > 0)
			{
				var tty = LibC.open("/dev/tty0", LibC.O_RDWR);

				if (tty < 0)
				{
					throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
				}

				try
				{
					if (LibC.ioctl(tty, VT_ACTIVATE, Vt) < 0 || LibC.ioctl(tty, VT_WAITACTIVE, Vt) < 0)
					{
						throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
					}
				}
				finally
				{
					LibC.close(tty);
				}
			}
		}

		public void Close()
		{
			if (isCurrent)
			{
				return;
			}

			Kill(windowManager);
			Kill(server);

			windowManager = null;
			server = null;

			if (!string.IsNullOrEmpty(AuthorityFile) && File.Exists(AuthorityFile))
			{
				File.Delete(AuthorityFile);
			}
		}

		internal static int ReadActiveVt()
		{
			try
			{
				var active = File.ReadAllText("/sys/class/tty/tty0/active").Trim();

				return active.StartsWith("tty") && int.TryParse(active.Substring(3), out var vt) ? vt : 0;
			}
			catch
			{
				return 0;
			}
		}

		internal static int ParseNumber(string display)
		{
			var colon = display.IndexOf(':');
			var dot = display.IndexOf('.');
			var text = display.Substring(colon + 1, (dot > colon ? dot : display.Length) - colon - 1);

			return int.TryParse(text, out var number) ? number : 0;
		}

		private static void Kill(System.Diagnostics.Process process)
		{
			try
			{
				if (process != null && !process.HasExited)
				{
					process.Kill();
					process.WaitForExit(2000);
				}
			}
			catch
			{
			}
		}

		public override string ToString()
		{
			return $"'{Name}' [{Handle}]";
		}
	}
}
