/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using SafeExamBrowser.LinuxApi.Desktops;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi.Processes
{
	public class ProcessFactory : IProcessFactory
	{
		private readonly IModuleLogger logger;

		public IDesktop StartupDesktop { private get; set; }

		public ProcessFactory(IModuleLogger logger)
		{
			this.logger = logger;
		}

		public IList<IProcess> GetAllRunning()
		{
			var processes = new List<IProcess>();

			foreach (var raw in System.Diagnostics.Process.GetProcesses())
			{
				try
				{
					var (name, originalName, path, signature) = LoadProcessNamesFor(raw);

					if (path != default)
					{
						processes.Add(new LinuxProcess(raw, name, originalName, LoggerFor(raw, name), path, signature));
					}
				}
				catch (Exception e)
				{
					logger.Debug($"Skipping process {raw.Id}: {e.Message}");
				}
			}

			return processes;
		}

		public IProcess StartNew(string path, params string[] args)
		{
			logger.Info($"Attempting to start process '{path}'...");

			var raw = default(System.Diagnostics.Process);

			try
			{
				raw = Start(path, args);
			}
			catch (Win32Exception e)
			{
				logger.Error($"Failed to start process '{path}' on desktop '{StartupDesktop}'! Error code: {e.NativeErrorCode}.");

				throw;
			}

			var (name, originalName, _, signature) = LoadProcessNamesFor(raw, path);
			var process = new LinuxProcess(raw, name, originalName, LoggerFor(raw, name), path, signature);

			logger.Info($"Successfully started process '{path}' with ID = {process.Id}.");

			return process;
		}

		public bool TryGetById(int id, out IProcess process)
		{
			process = default;

			try
			{
				var raw = System.Diagnostics.Process.GetProcessById(id);
				var (name, originalName, path, signature) = LoadProcessNamesFor(raw);

				process = new LinuxProcess(raw, name, originalName, LoggerFor(raw, name), path, signature);
			}
			catch (Exception e)
			{
				logger.Error($"Failed to get process with ID = {id}!", e);
			}

			return process != default(IProcess);
		}

		private (string name, string originalName, string path, string signature) LoadProcessNamesFor(System.Diagnostics.Process process, string fallbackPath = default)
		{
			var name = process.ProcessName;
			var originalName = default(string);
			var path = fallbackPath;
			var signature = default(string);

			try
			{
				var executable = Directory.ResolveLinkTarget($"/proc/{process.Id}/exe", false)?.FullName;

				if (executable != null)
				{
					path = executable.Replace(" (deleted)", string.Empty);
					name = Path.GetFileName(path);
				}

				originalName = File.ReadAllText($"/proc/{process.Id}/comm").Trim();

				if (path != default && File.Exists(path))
				{
					signature = PackageSignature.Compute(path);
				}
			}
			catch (Exception e)
			{
				logger.Debug($"Failed to load process names for {process.ProcessName}: {e.Message}");
			}

			return (name, originalName, path, signature);
		}

		private ILogger LoggerFor(System.Diagnostics.Process process, string name)
		{
			return logger.CloneFor($"{nameof(LinuxProcess)} '{name}' ({process.Id})");
		}

		private System.Diagnostics.Process Start(string path, string[] args)
		{
			var resolvedPath = ResolveExecutablePath(path);
			var process = new System.Diagnostics.Process();

			process.StartInfo.Arguments = string.Join(" ", args);
			process.StartInfo.FileName = resolvedPath;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			if (StartupDesktop is LinuxDesktop desktop)
			{
				process.StartInfo.Environment["DISPLAY"] = desktop.Display;

				if (!string.IsNullOrEmpty(desktop.AuthorityFile))
				{
					process.StartInfo.Environment["XAUTHORITY"] = desktop.AuthorityFile;
				}

				process.StartInfo.Environment.Remove("WAYLAND_DISPLAY");
			}

			process.Start();

			return process;
		}

		private static string ResolveExecutablePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
			{
				return path;
			}

			if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				var withoutExe = path.Substring(0, path.Length - 4);

				if (File.Exists(withoutExe))
				{
					return withoutExe;
				}

				var siblingCandidate = withoutExe.Replace(
					$"{Path.DirectorySeparatorChar}SafeExamBrowser.Runtime{Path.DirectorySeparatorChar}",
					$"{Path.DirectorySeparatorChar}SafeExamBrowser.Client{Path.DirectorySeparatorChar}");

				if (File.Exists(siblingCandidate))
				{
					return siblingCandidate;
				}
			}

			return path;
		}
	}
}
