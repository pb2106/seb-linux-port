/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SafeExamBrowser.LinuxApi.Processes
{
	/// <summary>
	/// Replacement for the Authenticode signature of Windows executables. An executable which is owned by a Debian package is
	/// identified by the SHA-1 hash of "dpkg:" followed by the package name. Executables which are not owned by any package
	/// don't have a signature. As the dpkg database can only be modified by root, a signature can't be spoofed by a user.
	/// </summary>
	public static class PackageSignature
	{
		private static readonly object Lock = new object();
		private static Dictionary<string, string> owners;

		public static string Compute(string executablePath)
		{
			if (string.IsNullOrWhiteSpace(executablePath))
			{
				return default;
			}

			lock (Lock)
			{
				owners = owners ?? Load();
			}

			var resolved = executablePath;

			try
			{
				resolved = new FileInfo(executablePath).ResolveLinkTarget(true)?.FullName ?? executablePath;
			}
			catch
			{
			}

			foreach (var candidate in new[] { resolved, executablePath, "/usr" + resolved })
			{
				if (owners.TryGetValue(candidate, out var package))
				{
					return Hash(package);
				}
			}

			return default;
		}

		public static string Hash(string package)
		{
			using (var sha1 = SHA1.Create())
			{
				return string.Concat(sha1.ComputeHash(Encoding.UTF8.GetBytes("dpkg:" + package)).Select(b => b.ToString("x2")));
			}
		}

		private static Dictionary<string, string> Load()
		{
			var map = new Dictionary<string, string>();
			const string directory = "/var/lib/dpkg/info";

			if (!Directory.Exists(directory))
			{
				return map;
			}

			foreach (var list in Directory.EnumerateFiles(directory, "*.list"))
			{
				var package = Path.GetFileNameWithoutExtension(list);
				var colon = package.IndexOf(':');

				if (colon > 0)
				{
					package = package.Substring(0, colon);
				}

				try
				{
					foreach (var line in File.ReadLines(list))
					{
						if (line.Contains("/bin/") || line.Contains("/sbin/") || line.Contains("/lib/") || line.Contains("/opt/"))
						{
							map[line] = package;
						}
					}
				}
				catch
				{
				}
			}

			return map;
		}
	}
}
