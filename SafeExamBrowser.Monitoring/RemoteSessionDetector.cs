/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using SafeExamBrowser.Integrity.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Monitoring.Contracts;

namespace SafeExamBrowser.Monitoring
{
	public class RemoteSessionDetector : IRemoteSessionDetector
	{
		private readonly IIntegrityModule integrityModule;
		private readonly ILogger logger;

		public RemoteSessionDetector(IIntegrityModule integrityModule, ILogger logger)
		{
			this.logger = logger;
			this.integrityModule = integrityModule;
		}

		public bool IsRemoteSession()
		{
			// On Linux, detect remote sessions via SSH_CLIENT/SSH_TTY env vars,
			// or DISPLAY forwarded over SSH (DISPLAY set but SSH_CONNECTION set too).
			var isRemote = IsLinuxRemoteSession() || integrityModule.IsRemoteSession();

			logger.Debug($"Current user session appears {(isRemote ? "" : "not ")}to be a remote session.");

			return isRemote;
		}

		private static bool IsLinuxRemoteSession()
		{
			// SSH session indicators
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CLIENT"))) return true;
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_TTY"))) return true;
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CONNECTION"))) return true;

			// X2Go / NoMachine / VNC remote desktop
			var xdgSession = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
			if (!string.IsNullOrEmpty(xdgSession) &&
				xdgSession.Equals("x11", StringComparison.OrdinalIgnoreCase) &&
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CONNECTION")))
				return true;

			return false;
		}
	}
}
