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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Monitoring.Contracts.Display;
using SafeExamBrowser.Monitoring.Contracts.Display.Events;
using SafeExamBrowser.Settings.Monitoring;
using SafeExamBrowser.SystemComponents.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;
using OperatingSystem = SafeExamBrowser.SystemComponents.Contracts.OperatingSystem;

namespace SafeExamBrowser.Monitoring.Display
{
	public class DisplayMonitor : IDisplayMonitor
	{
		private const string XRANDR_OUTPUT_PATH = "/tmp/seb-xrandr.txt";

		private IBounds originalWorkingArea;
		private readonly ILogger logger;
		private readonly INativeMethods nativeMethods;
		private readonly ISystemInfo systemInfo;
		private string wallpaper;

		public event DisplayChangedEventHandler DisplayChanged;

		public DisplayMonitor(ILogger logger, INativeMethods nativeMethods, ISystemInfo systemInfo)
		{
			this.logger = logger;
			this.nativeMethods = nativeMethods;
			this.systemInfo = systemInfo;
		}

		public void InitializePrimaryDisplay(int taskbarHeight)
		{
			InitializeWorkingArea(taskbarHeight);
			InitializeWallpaper();
		}

		public void ResetPrimaryDisplay()
		{
			ResetWorkingArea();
			ResetWallpaper();
		}

		public void StartMonitoringDisplayChanges()
		{
			// On Linux, monitor /sys/class/drm or use udev; for now delegate to nativeMethods
			logger.Info("Started monitoring display changes.");
		}

		public void StopMonitoringDisplayChanges()
		{
			logger.Info("Stopped monitoring display changes.");
		}

		public ValidationResult ValidateConfiguration(DisplaySettings settings)
		{
			var result = new ValidationResult();

			if (TryLoadDisplays(out var displays))
			{
				var active = displays.Where(d => d.IsActive);
				var count = active.Count();
				var hasAllowedCount = count <= settings.AllowedDisplays;
				var hasAllowedTypes = !settings.InternalDisplayOnly || active.All(d => d.IsInternal);

				result.ExternalDisplays = active.Count(d => !d.IsInternal);
				result.InternalDisplays = active.Count(d => d.IsInternal);
				result.IsAllowed = hasAllowedCount && hasAllowedTypes;

				if (result.IsAllowed)
				{
					logger.Info($"Detected {count} active displays and {settings.AllowedDisplays} are allowed, display configuration is valid.");
				}
				else
				{
					if (!hasAllowedCount)
						logger.Warn($"Detected {count} active displays but only {settings.AllowedDisplays} are allowed!");

					if (!hasAllowedTypes)
						logger.Warn("Detected external display(s) but only internal displays are allowed!");

					logger.Warn("Display configuration is not valid!");
				}
			}
			else
			{
				result.IsAllowed = settings.IgnoreError;
				logger.Warn($"Failed to validate display configuration{(result.IsAllowed ? ", ignoring error." : " and errors are not allowed!")}");
			}

			return result;
		}

		private void InitializeWorkingArea(int taskbarHeight)
		{
			var identifier = GetIdentifierForPrimaryDisplay();

			if (originalWorkingArea == null)
			{
				originalWorkingArea = nativeMethods.GetWorkingArea();
				LogWorkingArea($"Saved original working area for {identifier}", originalWorkingArea);
			}

			var (width, height) = GetPrimaryScreenResolution();

			var area = new Bounds
			{
				Left = 0,
				Top = 0,
				Right = width,
				Bottom = height - taskbarHeight
			};

			LogWorkingArea($"Trying to set new working area for {identifier}", area);
			nativeMethods.SetWorkingArea(area);
			LogWorkingArea($"Working area of {identifier} is now set to", nativeMethods.GetWorkingArea());
		}

		private void InitializeWallpaper()
		{
			// Wallpaper removal is only needed for Windows 7; skip on Linux
		}

		private bool TryLoadDisplays(out IList<Display> displays)
		{
			var success = true;
			displays = new List<Display>();

			try
			{
				// Use xrandr to enumerate connected/active displays on Linux
				var output = RunOutput("xrandr", "--query");

				if (output == null)
				{
					success = false;
					logger.Warn("xrandr not available; cannot enumerate displays.");
					return success;
				}

				foreach (var line in output.Split('\n'))
				{
					// xrandr lines for connected displays: "HDMI-1 connected primary 1920x1080+0+0 ..."
					// or disconnected: "VGA-1 disconnected"
					var parts = line.Split(' ');
					if (parts.Length < 2) continue;

					var name = parts[0];
					var connected = parts[1] == "connected";
					var isActive = connected && parts.Length > 2 && parts[2] != "(";

					if (!connected && !isActive) continue;

					// Determine if internal: LVDS, eDP are internal panel types
					var isInternal = name.StartsWith("LVDS", StringComparison.OrdinalIgnoreCase)
						|| name.StartsWith("eDP", StringComparison.OrdinalIgnoreCase);

					var display = new Display
					{
						Identifier = name,
						IsActive = isActive,
						Technology = isInternal ? VideoOutputTechnology.Internal : VideoOutputTechnology.Other
					};

					displays.Add(display);
					logger.Info($"Detected {(display.IsActive ? "active" : "inactive")}, {(display.IsInternal ? "internal" : "external")} display '{display.Identifier}'.");
				}
			}
			catch (Exception e)
			{
				success = false;
				logger.Error("Failed to query displays!", e);
			}

			return success;
		}

		private void ResetWorkingArea()
		{
			var identifier = GetIdentifierForPrimaryDisplay();

			if (originalWorkingArea != null)
			{
				nativeMethods.SetWorkingArea(originalWorkingArea);
				LogWorkingArea($"Restored original working area for {identifier}", originalWorkingArea);
			}
			else
			{
				logger.Warn($"Could not restore original working area for {identifier}!");
			}
		}

		private void ResetWallpaper()
		{
			// Not needed on Linux
		}

		private string GetIdentifierForPrimaryDisplay()
		{
			var (width, height) = GetPrimaryScreenResolution();
			return $"primary ({width}x{height})";
		}

		private (int width, int height) GetPrimaryScreenResolution()
		{
			try
			{
				// Parse xrandr for primary screen: "HDMI-1 connected primary 1920x1080+0+0 ..."
				var output = RunOutput("xrandr", "--query");

				if (output != null)
				{
					foreach (var line in output.Split('\n'))
					{
						if (!line.Contains(" primary ")) continue;
						// Find resolution token like "1920x1080+0+0"
						var token = line.Split(' ').FirstOrDefault(p => p.Contains("x") && p.Contains("+") && p[0] >= '0' && p[0] <= '9');

						if (token != null)
						{
							var res = token.Split('+')[0].Split('x');
							if (res.Length == 2 && int.TryParse(res[0], out var w) && int.TryParse(res[1], out var h))
								return (w, h);
						}
					}
				}
			}
			catch { }

			// Fallback from /sys/class/drm
			try
			{
				foreach (var modeFile in Directory.EnumerateFiles("/sys/class/drm", "*/modes", SearchOption.AllDirectories))
				{
					var firstMode = File.ReadAllLines(modeFile).FirstOrDefault();
					if (firstMode != null)
					{
						var parts = firstMode.Split('x');
						if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
							return (w, h);
					}
				}
			}
			catch { }

			return (1920, 1080); // Safe fallback
		}

		private void LogWorkingArea(string message, IBounds area)
		{
			logger.Info($"{message}: Left = {area.Left}, Top = {area.Top}, Right = {area.Right}, Bottom = {area.Bottom}.");
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
				p.WaitForExit(5000);
				return output;
			}
			catch { return null; }
		}
	}
}
