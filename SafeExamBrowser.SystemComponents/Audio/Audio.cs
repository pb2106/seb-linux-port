/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.SystemComponents;
using SafeExamBrowser.SystemComponents.Contracts.Audio;
using SafeExamBrowser.SystemComponents.Contracts.Audio.Events;

namespace SafeExamBrowser.SystemComponents.Audio
{
	/// <summary>
	/// Linux implementation using PulseAudio (pactl) with fallback to ALSA (amixer).
	/// </summary>
	public class Audio : IAudio
	{
		private readonly ILogger logger;
		private readonly AudioSettings settings;

		private float originalVolume;
		private bool originallyMuted;
		private bool hasPulse;

		public string DeviceFullName { get; private set; } = string.Empty;
		public string DeviceShortName { get; private set; } = string.Empty;
		public bool HasOutputDevice { get; private set; }
		public bool OutputMuted => GetMuted();
		public double OutputVolume => GetVolume();

		public event VolumeChangedEventHandler VolumeChanged;

		public Audio(AudioSettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;
		}

		public void Initialize()
		{
			hasPulse = Run("pactl", "info") == 0;

			if (hasPulse)
			{
				var output = RunOutput("pactl", "list short sinks");
				HasOutputDevice = !string.IsNullOrWhiteSpace(output);
				DeviceFullName = output?.Split('\n').FirstOrDefault()?.Split('\t').ElementAtOrDefault(1) ?? "PulseAudio";
				DeviceShortName = DeviceFullName.Split('.').LastOrDefault() ?? DeviceFullName;
			}
			else
			{
				// ALSA fallback
				HasOutputDevice = Run("amixer", "sget Master") == 0;
				DeviceFullName = "ALSA Master";
				DeviceShortName = "Master";
			}

			if (!HasOutputDevice)
			{
				logger.Info("No audio output device detected.");
				return;
			}

			originalVolume = (float)GetVolume();
			originallyMuted = GetMuted();

			if (settings.MuteAudio)
			{
				SetMuted(true);
			}

			logger.Info($"Initialized audio: {DeviceFullName}, muted={OutputMuted}, volume={OutputVolume:P0}.");
		}

	
	public void Mute()
	{
		SetMuted(true);
		VolumeChanged?.Invoke(OutputVolume, true);
	}

	public void Unmute()
	{
		SetMuted(false);
		VolumeChanged?.Invoke(OutputVolume, false);
	}

	public void SetVolume(double value)
		{
			var percent = (int)Math.Round(value * 100);

			if (hasPulse)
				Run("pactl", $"set-sink-volume @DEFAULT_SINK@ {percent}%");
			else
				Run("amixer", $"sset Master {percent}%");

			VolumeChanged?.Invoke(OutputVolume, OutputMuted);
		}

		public void Terminate()
		{
			if (!HasOutputDevice) return;

			if (settings.MuteAudio)
			{
				SetMuted(originallyMuted);
			}

			logger.Info("Terminated audio monitoring.");
		}

		private bool GetMuted()
		{
			if (!HasOutputDevice) return false;

			if (hasPulse)
			{
				var output = RunOutput("pactl", "get-sink-mute @DEFAULT_SINK@");
				return output?.Contains("yes", StringComparison.OrdinalIgnoreCase) == true;
			}
			else
			{
				var output = RunOutput("amixer", "sget Master");
				return output?.Contains("[off]", StringComparison.OrdinalIgnoreCase) == true;
			}
		}

		private double GetVolume()
		{
			if (!HasOutputDevice) return 0;

			try
			{
				if (hasPulse)
				{
					var output = RunOutput("pactl", "get-sink-volume @DEFAULT_SINK@");
					// Output: "Volume: front-left: 65536 / 100% / 0.00 dB,   front-right: ..."
					var parts = output?.Split('/');
					if (parts?.Length >= 2)
					{
						var pct = parts[1].Trim().TrimEnd('%');
						if (double.TryParse(pct, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
							return v / 100.0;
					}
				}
				else
				{
					var output = RunOutput("amixer", "sget Master");
					// Output contains "[75%]"
					var start = output?.IndexOf('[') ?? -1;
					var end = output?.IndexOf('%') ?? -1;
					if (start >= 0 && end > start)
					{
						var pct = output.Substring(start + 1, end - start - 1);
						if (double.TryParse(pct, out var v)) return v / 100.0;
					}
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to query volume!", e);
			}

			return 0;
		}

		private void SetMuted(bool mute)
		{
			var flag = mute ? "1" : "0";

			if (hasPulse)
				Run("pactl", $"set-sink-mute @DEFAULT_SINK@ {flag}");
			else
				Run("amixer", $"sset Master {(mute ? "mute" : "unmute")}");
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
