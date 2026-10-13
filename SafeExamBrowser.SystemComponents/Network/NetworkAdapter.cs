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
using System.Net.NetworkInformation;
using System.Timers;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Network;
using SafeExamBrowser.SystemComponents.Contracts.Network.Events;

namespace SafeExamBrowser.SystemComponents.Network
{
	/// <summary>
	/// Linux implementation using nmcli for wireless scanning and
	/// System.Net.NetworkInformation for connectivity detection.
	/// </summary>
	public class NetworkAdapter : INetworkAdapter
	{
		private const int FIVE_SECONDS = 5000;

		private readonly ILogger logger;
		private readonly object @lock = new object();
		private readonly List<WirelessNetwork> wirelessNetworks = new List<WirelessNetwork>();
		private readonly Dictionary<string, int> attempts = new Dictionary<string, int>();

		private Timer timer;
		private string currentSsid;

		public bool HasWirelessAdapter { get; private set; }
		public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
		public ConnectionType Type { get; private set; } = ConnectionType.Undefined;

		public event ChangedEventHandler Changed;
	// On Linux, WPA2-Enterprise credential prompts are handled by NetworkManager UI;
	// we declare the event for interface compliance but don't fire it from code.
	public event CredentialsRequiredEventHandler CredentialsRequired;

		public NetworkAdapter(ILogger logger)
		{
			this.logger = logger;
		}

		public void ConnectToWirelessNetwork(string name)
		{
			var isFirstAttempt = !attempts.TryGetValue(name, out _);

			if (!attempts.ContainsKey(name)) attempts[name] = 0;
			attempts[name]++;

			try
			{
				Run("nmcli", $"device wifi connect \"{name}\"");
				logger.Info($"Issued nmcli connect to '{name}'.");
			}
			catch (Exception e)
			{
				logger.Error($"Failed to connect to wireless network '{name}'!", e);
			}

			Changed?.Invoke();
		}

		public IEnumerable<IWirelessNetwork> GetWirelessNetworks()
		{
			lock (@lock)
			{
				return new List<WirelessNetwork>(wirelessNetworks);
			}
		}

		public void Initialize()
		{
			timer = new Timer(FIVE_SECONDS);
			timer.Elapsed += (o, args) => Update();
			timer.AutoReset = true;

			InitializeAdapter();

			NetworkChange.NetworkAddressChanged += (s, e) => Update();
			NetworkChange.NetworkAvailabilityChanged += (s, e) => Update();

			Update();

			logger.Info("Started monitoring the network adapter.");
		}

		public bool RequestAccess()
		{
			// On Linux, access to wireless is controlled by the OS permissions / polkit.
			// nmcli works if the user/service has the right permissions.
			logger.Debug("Wireless access is controlled by system permissions on Linux.");
			return true;
		}

		public void StartWirelessNetworkScanning()
		{
			timer?.Start();

			if (HasWirelessAdapter)
			{
				// Trigger a rescan
				Run("nmcli", "device wifi rescan");
			}
		}

		public void StopWirelessNetworkScanning()
		{
			timer?.Stop();
		}

		public void Terminate()
		{
			NetworkChange.NetworkAddressChanged -= (s, e) => Update();
			NetworkChange.NetworkAvailabilityChanged -= (s, e) => Update();
			timer?.Stop();
			logger.Info("Stopped monitoring the network adapter.");
		}

		private void InitializeAdapter()
		{
			// Check if any wireless interface exists
			var output = RunOutput("nmcli", "-t -f TYPE,DEVICE device status");
			HasWirelessAdapter = output?.Split('\n')
				.Any(line => line.StartsWith("wifi:", StringComparison.OrdinalIgnoreCase)) == true;
		}

		private void Update(bool rescan = false)
		{
			try
			{
				var hasConnection = NetworkInterface.GetIsNetworkAvailable();
				var isConnecting = Status == ConnectionStatus.Connecting;
				var networks = new List<WirelessNetwork>();
				var previousStatus = Status;
				WirelessNetwork currentNetwork = null;

				if (HasWirelessAdapter)
				{
					if (rescan) Run("nmcli", "device wifi rescan");

					// Get current SSID
					currentSsid = RunOutput("nmcli", "-t -f active,ssid device wifi")
						?.Split('\n')
						.FirstOrDefault(l => l.StartsWith("yes:", StringComparison.OrdinalIgnoreCase))
						?.Split(':').ElementAtOrDefault(1)?.Trim();

					// List available networks
					var wifiOut = RunOutput("nmcli", "-t -f ssid,signal,security device wifi list");
					if (wifiOut != null)
					{
						foreach (var line in wifiOut.Split('\n'))
						{
							var parts = line.Split(':');
							if (parts.Length < 2) continue;
							var ssid = parts[0].Trim();
							if (string.IsNullOrEmpty(ssid)) continue;

							if (!int.TryParse(parts.ElementAtOrDefault(1), out var signal))
								signal = 0;

							var wn = new WirelessNetwork
							{
								Name = ssid,
								SignalStrength = signal,
								Status = ssid == currentSsid ? ConnectionStatus.Connected : ConnectionStatus.Disconnected
							};

							networks.Add(wn);

							if (ssid == currentSsid) currentNetwork = wn;
						}
					}

					hasConnection &= !string.IsNullOrEmpty(currentSsid);
				}

				lock (@lock)
				{
					wirelessNetworks.Clear();
					wirelessNetworks.AddRange(networks);
				}

				Type = HasWirelessAdapter ? ConnectionType.Wireless : (hasConnection ? ConnectionType.Wired : ConnectionType.Undefined);
				Status = hasConnection ? ConnectionStatus.Connected : (isConnecting ? ConnectionStatus.Connecting : ConnectionStatus.Disconnected);

				LogNetworkChanges(previousStatus, currentNetwork);
			}
			catch (Exception e)
			{
				logger.Error("Failed to update network adapter!", e);
			}

			Changed?.Invoke();
		}

		private void LogNetworkChanges(ConnectionStatus previousStatus, WirelessNetwork currentNetwork = null)
		{
			if (previousStatus != ConnectionStatus.Connected && Status == ConnectionStatus.Connected)
				logger.Info($"Connection established ({Type}{(currentNetwork != null ? $", {currentNetwork.Name}" : "")}).");

			if (previousStatus != ConnectionStatus.Disconnected && Status == ConnectionStatus.Disconnected)
				logger.Info("Connection lost.");
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
				p.WaitForExit(5000);
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
				p.WaitForExit(5000);
				return output;
			}
			catch { return null; }
		}
	}
}
