/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Keyboard;
using SafeExamBrowser.SystemComponents.Contracts.Keyboard.Events;

namespace SafeExamBrowser.SystemComponents.Keyboard
{
	/// <summary>
	/// Linux keyboard layout management via setxkbmap / localectl.
	/// </summary>
	public class Keyboard : IKeyboard
	{
		private readonly IList<KeyboardLayout> layouts = new List<KeyboardLayout>();
		private readonly ILogger logger;
		private string originalLayout;

		public event LayoutChangedEventHandler LayoutChanged;

		public Keyboard(ILogger logger)
		{
			this.logger = logger;
		}

		public void ActivateLayout(Guid layoutId)
		{
			var layout = layouts.First(l => l.Id == layoutId);
			logger.Info($"Changing keyboard layout to {layout}...");

			Run("setxkbmap", layout.CultureCode.ToLower());

			foreach (var l in layouts) l.IsCurrent = l.Id == layoutId;
			layout.IsCurrent = true;
			LayoutChanged?.Invoke(layout);
		}

		public IEnumerable<IKeyboardLayout> GetLayouts()
		{
			return new List<KeyboardLayout>(layouts.OrderBy(l => l.CultureName));
		}

		public void Initialize()
		{
			originalLayout = GetCurrentLayout();
			logger.Info($"Saved current keyboard layout: {originalLayout}.");

			// Enumerate all installed X11 keyboard layouts via localectl/xkb
			var available = GetAvailableLayouts();

			foreach (var code in available)
			{
				try
				{
					var culture = ResolveCulture(code);
					var layout = new KeyboardLayout
					{
						CultureCode = code.ToUpper(),
						CultureName = culture?.NativeName ?? code,
						IsCurrent = code == originalLayout,
						LayoutName = code
					};

					layouts.Add(layout);
					logger.Info($"Detected keyboard layout {layout}.");
				}
				catch (Exception e)
				{
					logger.Warn($"Could not process keyboard layout '{code}': {e.Message}");
				}
			}

			if (!layouts.Any())
			{
				// Fallback: at least add the current layout
				var layout = new KeyboardLayout
				{
					CultureCode = (originalLayout ?? "us").ToUpper(),
					CultureName = originalLayout ?? "us",
					IsCurrent = true,
					LayoutName = originalLayout ?? "us"
				};
				layouts.Add(layout);
			}
		}

		public void Terminate()
		{
			if (!string.IsNullOrEmpty(originalLayout))
			{
				Run("setxkbmap", originalLayout);
				logger.Info($"Restored original keyboard layout: {originalLayout}.");
			}
		}

		private string GetCurrentLayout()
		{
			// setxkbmap -query returns lines like "layout: us"
			var output = RunOutput("setxkbmap", "-query");

			if (output != null)
			{
				foreach (var line in output.Split('\n'))
				{
					if (line.TrimStart().StartsWith("layout:", StringComparison.OrdinalIgnoreCase))
						return line.Split(':').ElementAtOrDefault(1)?.Trim().Split(',')[0];
				}
			}

			return "us";
		}

		private List<string> GetAvailableLayouts()
		{
			var layouts = new List<string>();

			// Try localectl first
			var output = RunOutput("localectl", "list-x11-keymap-layouts");

			if (!string.IsNullOrWhiteSpace(output))
			{
				layouts.AddRange(output.Split('\n')
					.Select(l => l.Trim())
					.Where(l => !string.IsNullOrEmpty(l)));
			}
			else
			{
				// Fallback: just return the current layout
				var current = GetCurrentLayout();
				if (!string.IsNullOrEmpty(current)) layouts.Add(current);
			}

			return layouts;
		}

		private static CultureInfo ResolveCulture(string xkbLayout)
		{
			// Map common XKB layout codes to CultureInfo
			try { return CultureInfo.GetCultureInfoByIetfLanguageTag(xkbLayout); } catch { }
			try { return new CultureInfo(xkbLayout); } catch { }
			return null;
		}

		private static int Run(string cmd, string args)
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo(cmd, args)
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});
				p.WaitForExit(3000);
				return p.ExitCode;
			}
			catch { return -1; }
		}

		private static string RunOutput(string cmd, string args)
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo(cmd, args)
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});
				var output = p.StandardOutput.ReadToEnd();
				p.WaitForExit(3000);
				return output;
			}
			catch { return null; }
		}
	}
}
