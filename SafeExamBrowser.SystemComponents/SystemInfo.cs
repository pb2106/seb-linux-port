/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using SafeExamBrowser.SystemComponents.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Registry;
using OperatingSystem = SafeExamBrowser.SystemComponents.Contracts.OperatingSystem;

namespace SafeExamBrowser.SystemComponents
{
	/// <summary>
	/// Linux implementation reading hardware/OS info from /proc, /sys/class/dmi, and lscpu.
	/// </summary>
	public class SystemInfo : ISystemInfo
	{
		private readonly IRegistry registry;

		public string BiosInfo { get; private set; }
		public string CpuName { get; private set; }
		public bool HasBattery { get; private set; }
		public string MacAddress { get; private set; }
		public string Manufacturer { get; private set; }
		public string Model { get; private set; }
		public string Name { get; private set; }
		public OperatingSystem OperatingSystem { get; private set; }
		public string OperatingSystemInfo => $"{OperatingSystemName()}, {Environment.OSVersion.VersionString} ({Architecture()})";
		public string[] PlugAndPlayDeviceIds { get; private set; }

		public SystemInfo(IRegistry registry)
		{
			this.registry = registry;

			InitializeBattery();
			InitializeBiosInfo();
			InitializeCpuName();
			InitializeMacAddress();
			InitializeMachineInfo();
			InitializeOperatingSystem();
			InitializePnPDevices();
		}

		public IEnumerable<DriveInfo> GetDrives()
		{
			// On Linux there is no "NoDrives" registry policy; return all mounted drives
			return DriveInfo.GetDrives();
		}

		private void InitializeBattery()
		{
			const string BASE = "/sys/class/power_supply";

			HasBattery = Directory.Exists(BASE) &&
				Directory.EnumerateDirectories(BASE)
					.Any(d => ReadSys(d, "type")?.Trim() == "Battery");
		}

		private void InitializeBiosInfo()
		{
			try
			{
				var vendor = ReadDmi("bios_vendor") ?? ReadDmi("board_vendor") ?? "";
				var version = ReadDmi("bios_version") ?? "";
				BiosInfo = $"{vendor} {version}".Trim();
			}
			catch { BiosInfo = ""; }
		}

		private void InitializeCpuName()
		{
			try
			{
				var cpuinfo = File.ReadAllText("/proc/cpuinfo");
				CpuName = cpuinfo
					.Split('\n')
					.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
					?.Split(':').ElementAtOrDefault(1)?.Trim() ?? "";
			}
			catch { CpuName = ""; }
		}

		private void InitializeMacAddress()
		{
			const string UNDEFINED = "000000000000";

			try
			{
				var iface = NetworkInterface.GetAllNetworkInterfaces()
					.FirstOrDefault(n =>
						n.OperationalStatus == OperationalStatus.Up &&
						n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
						n.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

				MacAddress = iface != null
					? BitConverter.ToString(iface.GetPhysicalAddress().GetAddressBytes()).Replace("-", "").ToUpper()
					: UNDEFINED;
			}
			catch
			{
				MacAddress = UNDEFINED;
			}
		}

		private void InitializeMachineInfo()
		{
			try
			{
				Manufacturer = ReadDmi("sys_vendor") ?? ReadDmi("board_vendor") ?? "";
				var productName = ReadDmi("product_name") ?? "";
				var productFamily = ReadDmi("product_family") ?? "";
				Model = $"{productFamily} {productName}".Trim();
				Name = Environment.MachineName;
			}
			catch
			{
				Manufacturer = "";
				Model = "";
				Name = Environment.MachineName;
			}
		}

		private void InitializeOperatingSystem()
		{
			// On Linux we always set the OS to the Linux entry.
			// The OperatingSystem enum was Windows-only; if a Linux entry isn't available
			// (e.g. contract hasn't been extended yet), we default to Unknown.
			try
			{
				OperatingSystem = (OperatingSystem)Enum.Parse(typeof(OperatingSystem), "Linux", true);
			}
			catch
			{
				OperatingSystem = default;
			}
		}

		private void InitializePnPDevices()
		{
			var deviceList = new List<string>();

			try
			{
				// On Linux, enumerate devices via /sys/bus/*/devices
				if (Directory.Exists("/sys/bus"))
				{
					foreach (var busDir in Directory.EnumerateDirectories("/sys/bus"))
					{
						var devicesDir = Path.Combine(busDir, "devices");
						if (!Directory.Exists(devicesDir)) continue;

						foreach (var deviceDir in Directory.EnumerateDirectories(devicesDir))
						{
							var name = Path.GetFileName(deviceDir);
							if (!string.IsNullOrEmpty(name))
								deviceList.Add(name.ToLower());
						}
					}
				}
			}
			catch { }

			PlugAndPlayDeviceIds = deviceList.Distinct().ToArray();
		}

		private string OperatingSystemName()
		{
			// Try to read /etc/os-release for a human-readable name
			try
			{
				if (File.Exists("/etc/os-release"))
				{
					var name = File.ReadAllLines("/etc/os-release")
						.FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.OrdinalIgnoreCase))
						?.Split('=').ElementAtOrDefault(1)
						?.Trim('"');
					if (!string.IsNullOrEmpty(name)) return name;
				}
			}
			catch { }

			return "Linux";
		}

		private string Architecture()
			=> Environment.Is64BitOperatingSystem ? "x64" : "x86";

		private static string ReadDmi(string field)
		{
			var paths = new[]
			{
				$"/sys/class/dmi/id/{field}",
				$"/sys/devices/virtual/dmi/id/{field}"
			};

			foreach (var p in paths)
			{
				try { if (File.Exists(p)) return File.ReadAllText(p).Trim(); }
				catch { }
			}

			return null;
		}

		private static string ReadSys(string devicePath, string file)
		{
			try { return File.ReadAllText(Path.Combine(devicePath, file)); }
			catch { return null; }
		}
	}
}
