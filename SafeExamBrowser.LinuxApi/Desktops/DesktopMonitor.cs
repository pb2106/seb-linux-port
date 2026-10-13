/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Timers;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi.Desktops
{
	/// <summary>
	/// Detects switches away from the SEB desktop (i.e. to another virtual terminal) and reactivates the SEB desktop.
	/// </summary>
	public class DesktopMonitor : IDesktopMonitor
	{
		private readonly ILogger logger;
		private readonly Timer timer;

		private IDesktop desktop;

		public DesktopMonitor(ILogger logger)
		{
			this.logger = logger;
			this.timer = new Timer(1000);
		}

		public void Start(IDesktop desktop)
		{
			this.desktop = desktop;

			timer.AutoReset = false;
			timer.Elapsed += Timer_Elapsed;
			timer.Start();

			logger.Info($"Started monitoring desktop {desktop}.");
		}

		public void Stop()
		{
			timer.Stop();
			timer.Elapsed -= Timer_Elapsed;

			logger.Info($"Stopped monitoring desktop {desktop}.");
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (desktop is LinuxDesktop linuxDesktop && linuxDesktop.Vt > 0)
			{
				var active = LinuxDesktop.ReadActiveVt();

				if (active == 0)
				{
					logger.Warn("Failed to get currently active desktop!");
				}
				else if (active != linuxDesktop.Vt)
				{
					logger.Warn($"Detected desktop switch to 'tty{active}', trying to reactivate {desktop}...");

					try
					{
						desktop.Activate();
					}
					catch (Exception exception)
					{
						logger.Error($"Failed to reactivate {desktop}!", exception);
					}
				}
			}

			timer.Start();
		}
	}
}
