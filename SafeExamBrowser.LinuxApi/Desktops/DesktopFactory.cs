/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using SafeExamBrowser.LinuxApi.Native;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi.Desktops
{
	public class DesktopFactory : IDesktopFactory
	{
		private const int VT_OPENQRY = 0x5600;
		private static readonly string[] WindowManagers = { "openbox", "matchbox-window-manager", "xfwm4", "metacity", "marco", "mutter", "icewm", "fluxbox", "twm" };

		private readonly ILogger logger;
		private readonly Random random;

		public DesktopFactory(ILogger logger)
		{
			this.logger = logger;
			this.random = new Random();
		}

		public IDesktop CreateNew(string name)
		{
			logger.Debug($"Attempting to create new desktop '{name}'...");

			var desktop = Start(name, default, default);

			logger.Debug($"Successfully created desktop {desktop}.");

			return desktop;
		}

		public IDesktop CreateRandom()
		{
			logger.Debug($"Attempting to create random desktop...");

			var name = GenerateRandomDesktopName();
			var desktop = Start(name, new IntPtr(random.Next(100, 10000)), GenerateRandomDesktopName());

			logger.Debug($"Successfully created random desktop {desktop}.");

			return desktop;
		}

		public IDesktop GetCurrent()
		{
			var name = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
			var desktop = LinuxDesktop.CreateCurrent(name);

			logger.Debug($"Successfully determined current desktop {desktop}.");

			return desktop;
		}

		private LinuxDesktop Start(string name, IntPtr obfuscatedHandle, string obfuscatedName)
		{
			var number = FindFreeDisplayNumber();
			var display = $":{number}";
			var authority = CreateAuthority(display);
			var privileged = LibC.geteuid() == 0;
			var vt = 0;
			var server = default(System.Diagnostics.Process);

			if (privileged || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
			{
				vt = FindFreeVt();
				server = Launch("Xorg", $"{display} vt{vt} -nolisten tcp -noreset -auth \"{authority}\"", null, null);

				if (server == null)
				{
					logger.Warn("Failed to launch Xorg on a dedicated virtual terminal, falling back to a nested X server...");
					vt = 0;
				}
			}

			if (server == null)
			{
				server = Launch("Xephyr", $"{display} -fullscreen -nolisten tcp -no-host-grab -auth \"{authority}\" -title \"{name}\"", null, null);
			}

			if (server == null)
			{
				logger.Error($"Failed to create new desktop '{name}'! Neither Xorg nor Xephyr could be started.");

				throw new Win32Exception("Neither Xorg nor Xephyr could be started for the new desktop.");
			}

			if (!WaitForServer(display, authority))
			{
				server.Kill();
				logger.Error($"Failed to create new desktop '{name}'! The X server did not become ready.");

				throw new Win32Exception("The X server of the new desktop did not become ready.");
			}

			var windowManager = StartWindowManager(display, authority);

			return new LinuxDesktop(name, number, vt, display, authority, server, windowManager, false, obfuscatedHandle, obfuscatedName);
		}

		private System.Diagnostics.Process StartWindowManager(string display, string authority)
		{
			foreach (var wm in WindowManagers)
			{
				var process = Launch(wm, string.Empty, display, authority);

				if (process != null)
				{
					logger.Debug($"Started window manager '{wm}' on display {display}.");

					return process;
				}
			}

			logger.Warn("No supported window manager found, window management on the new desktop will be limited!");

			return null;
		}

		private System.Diagnostics.Process Launch(string command, string arguments, string display, string authority)
		{
			try
			{
				var info = new ProcessStartInfo(command, arguments) { UseShellExecute = false };

				if (display != null)
				{
					info.Environment["DISPLAY"] = display;
					info.Environment["XAUTHORITY"] = authority;
				}

				var process = System.Diagnostics.Process.Start(info);

				Thread.Sleep(300);

				return process.HasExited ? null : process;
			}
			catch (Win32Exception)
			{
				return null;
			}
		}

		private bool WaitForServer(string display, string authority)
		{
			var previous = Environment.GetEnvironmentVariable("XAUTHORITY");

			try
			{
				Environment.SetEnvironmentVariable("XAUTHORITY", authority);

				for (var attempt = 0; attempt < 50; attempt++)
				{
					if (X11Display.TryOpen(out var connection, display))
					{
						connection.Dispose();

						return true;
					}

					Thread.Sleep(100);
				}

				return false;
			}
			finally
			{
				Environment.SetEnvironmentVariable("XAUTHORITY", previous);
			}
		}

		private string CreateAuthority(string display)
		{
			var directory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
			{
				directory = Path.GetTempPath();
			}

			var path = Path.Combine(directory, $"seb-xauth-{Guid.NewGuid():N}");
			var cookie = new byte[16];

			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(cookie);
			}

			var hex = string.Concat(cookie.Select(b => b.ToString("x2")));

			File.WriteAllBytes(path, new byte[0]);
			File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

			using (var xauth = System.Diagnostics.Process.Start(new ProcessStartInfo("xauth", $"-f \"{path}\" add {display} . {hex}") { UseShellExecute = false }))
			{
				xauth.WaitForExit();

				if (xauth.ExitCode != 0)
				{
					throw new Win32Exception("Failed to create X authority file using 'xauth'.");
				}
			}

			return path;
		}

		private int FindFreeDisplayNumber()
		{
			for (var number = 90; number < 200; number++)
			{
				if (!File.Exists($"/tmp/.X11-unix/X{number}") && !File.Exists($"/tmp/.X{number}-lock"))
				{
					return number;
				}
			}

			throw new Win32Exception("No free X display number available.");
		}

		private int FindFreeVt()
		{
			var tty = LibC.open("/dev/tty0", LibC.O_RDWR);

			if (tty < 0)
			{
				return 0;
			}

			try
			{
				var result = new byte[4];

				return LibC.ioctl(tty, VT_OPENQRY, result) >= 0 ? BitConverter.ToInt32(result, 0) : 0;
			}
			finally
			{
				LibC.close(tty);
			}
		}

		private string GenerateRandomDesktopName()
		{
			var length = random.Next(5, 20);
			var name = new char[length];

			for (var letter = 0; letter < length; letter++)
			{
				name[letter] = (char) (random.Next(2) == 0 && letter != 0 ? random.Next('a', 'z' + 1) : random.Next('A', 'Z' + 1));
			}

			return new string(name);
		}
	}
}
