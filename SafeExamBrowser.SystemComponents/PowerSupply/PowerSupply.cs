/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Linq;
using System.Timers;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.SystemComponents;
using SafeExamBrowser.SystemComponents.Contracts.PowerSupply;
using SafeExamBrowser.SystemComponents.Contracts.PowerSupply.Events;

namespace SafeExamBrowser.SystemComponents.PowerSupply
{
	/// <summary>
	/// Linux power supply monitoring using /sys/class/power_supply.
	/// </summary>
	public class PowerSupply : IPowerSupply
	{
		private const string POWER_SUPPLY_PATH = "/sys/class/power_supply";

		private readonly ILogger logger;
		private readonly PowerSupplySettings settings;

		private double critical;
		private double low;
		private DateTime lastStatusLog;
		private Timer timer;

		public event StatusChangedEventHandler StatusChanged;

		public PowerSupply(ILogger logger, PowerSupplySettings settings)
		{
			this.logger = logger;
			this.settings = settings;
		}

		public IPowerSupplyStatus GetStatus()
		{
			var status = new PowerSupplyStatus();

			try
			{
				var batteries = Directory.Exists(POWER_SUPPLY_PATH)
					? Directory.EnumerateDirectories(POWER_SUPPLY_PATH)
						.Where(d => ReadSys(d, "type")?.Trim() == "Battery")
						.ToList()
					: new System.Collections.Generic.List<string>();

				// AC adapter: online if any "AC*" / "Mains" / "ACAD" supply is online
				var acAdapters = Directory.Exists(POWER_SUPPLY_PATH)
					? Directory.EnumerateDirectories(POWER_SUPPLY_PATH)
						.Where(d =>
						{
							var t = ReadSys(d, "type")?.Trim();
							return t == "Mains" || t == "USB";
						})
					: Enumerable.Empty<string>();

				status.IsOnline = acAdapters.Any(d => ReadSys(d, "online")?.Trim() == "1");

				if (batteries.Any())
				{
					var bat = batteries.First();

					// Charge in percent
					var chargeNow = ReadSysDouble(bat, "charge_now") ?? ReadSysDouble(bat, "energy_now") ?? 0;
					var chargeFull = ReadSysDouble(bat, "charge_full") ?? ReadSysDouble(bat, "energy_full") ?? 1;
					var chargePercent = chargeFull > 0 ? ReadSysDouble(bat, "capacity") / 100.0 : chargeNow / chargeFull;
					var charge = chargePercent ?? (chargeNow / chargeFull);

					// Time remaining in seconds
					var currentNow = ReadSysDouble(bat, "current_now");
					var timeRemaining = 0;

					if (currentNow.HasValue && currentNow.Value > 0 && !status.IsOnline)
					{
						var remainingCapacity = chargeNow;
						var secondsRemaining = (int)(remainingCapacity / currentNow.Value * 3600);
						timeRemaining = secondsRemaining;
					}

					status.BatteryCharge = Math.Max(0, Math.Min(1, charge));
					status.BatteryChargeStatus = charge <= low
						? (charge <= critical ? BatteryChargeStatus.Critical : BatteryChargeStatus.Low)
						: BatteryChargeStatus.Okay;
					status.BatteryTimeRemaining = timeRemaining > 0
						? TimeSpan.FromSeconds(timeRemaining)
						: TimeSpan.Zero;
				}
				else
				{
					status.BatteryCharge = 1.0;
					status.BatteryChargeStatus = BatteryChargeStatus.Okay;
					status.BatteryTimeRemaining = TimeSpan.Zero;
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to read power supply status!", e);
				status.IsOnline = true;
				status.BatteryCharge = 1.0;
				status.BatteryChargeStatus = BatteryChargeStatus.Okay;
			}

			if (lastStatusLog < DateTime.Now.AddMinutes(-1))
			{
				logger.Debug($"Power grid is {(status.IsOnline ? "" : "not ")}connected, battery charge at {status.BatteryCharge * 100:F0}%{(status.IsOnline ? "" : $" ({status.BatteryTimeRemaining})")}.");
				lastStatusLog = DateTime.Now;
			}

			return status;
		}

		public void Initialize()
		{
			const int FIVE_SECONDS = 5000;

			critical = SanitizeThreshold(settings.ChargeThresholdCritical);
			low = SanitizeThreshold(settings.ChargeThresholdLow);

			timer = new Timer(FIVE_SECONDS);
			timer.Elapsed += Timer_Elapsed;
			timer.AutoReset = true;
			timer.Start();

			logger.Info($"Started monitoring the power supply (battery charge thresholds: low = {low * 100}%, critical = {critical * 100}%).");
		}

		public void Terminate()
		{
			if (timer != null)
			{
				timer.Stop();
				logger.Info("Stopped monitoring the power supply.");
			}
		}

		private static string ReadSys(string devicePath, string file)
		{
			try { return File.ReadAllText(Path.Combine(devicePath, file)); }
			catch { return null; }
		}

		private static double? ReadSysDouble(string devicePath, string file)
		{
			var raw = ReadSys(devicePath, file)?.Trim();
			return double.TryParse(raw, out var v) ? v : (double?)null;
		}

		private double SanitizeThreshold(double value)
			=> value < 0 ? 0 : (value > 1 ? 1 : value);

		private void Timer_Elapsed(object sender, ElapsedEventArgs e)
			=> StatusChanged?.Invoke(GetStatus());
	}
}
