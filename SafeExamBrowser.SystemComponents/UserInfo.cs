/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts;

namespace SafeExamBrowser.SystemComponents
{
	/// <summary>
	/// Linux UserInfo: uses 'id' command for UIDs and /etc/passwd for lookups.
	/// Note: Linux doesn't have Windows SIDs; we expose the UID as a pseudo-SID
	/// of the form "S-1-0-0-&lt;uid&gt;" so callers that pattern-match SIDs still work.
	/// </summary>
	public class UserInfo : IUserInfo
	{
		private const string PSEUDO_SID_PREFIX = "S-1-0-0-";

		private readonly ILogger logger;

		public UserInfo(ILogger logger)
		{
			this.logger = logger;
		}

		public string GetUserName()
		{
			return Environment.UserName;
		}

		public string GetUserSid()
		{
			return UidToSid(GetUid(Environment.UserName));
		}

		public bool TryGetSidForUser(string userName, out string sid)
		{
			sid = default;

			try
			{
				var uid = GetUid(userName);

				if (uid >= 0)
				{
					sid = UidToSid(uid);
					logger.Info($"Found UID {uid} (pseudo-SID '{sid}') for user name '{userName}'.");
					return true;
				}
			}
			catch (Exception e)
			{
				logger.Error($"Failed to get UID for user name '{userName}'!", e);
			}

			logger.Error($"Could not retrieve SID (UID) for user name '{userName}'.");
			return false;
		}

		private static int GetUid(string userName)
		{
			// Try 'id -u <name>'
			try
			{
				var p = Process.Start(new ProcessStartInfo("id", $"-u {userName}")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});
				var output = p.StandardOutput.ReadToEnd().Trim();
				p.WaitForExit(3000);

				if (int.TryParse(output, out var uid)) return uid;
			}
			catch { }

			// Fallback: parse /etc/passwd
			try
			{
				if (File.Exists("/etc/passwd"))
				{
					var line = File.ReadAllLines("/etc/passwd")
						.FirstOrDefault(l => l.Split(':').FirstOrDefault() == userName);

					if (line != null)
					{
						var parts = line.Split(':');
						if (parts.Length >= 3 && int.TryParse(parts[2], out var uid))
							return uid;
					}
				}
			}
			catch { }

			return -1;
		}

		private static string UidToSid(int uid)
		{
			return uid >= 0 ? $"{PSEUDO_SID_PREFIX}{uid}" : null;
		}
	}
}
