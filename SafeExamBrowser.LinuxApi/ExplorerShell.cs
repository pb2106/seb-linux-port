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
using System.Threading;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi
{
	/// <summary>
	/// Linux counterpart of the Windows explorer shell: the desktop shell is the process which owns the panel (dock) or desktop
	/// windows, e.g. xfce4-panel, plasmashell, lxqt-panel, tint2 or gnome-shell's X11 dock. It is terminated while the kiosk
	/// session is active and restarted with its original command line afterwards. A shell which is at the same time the window
	/// manager (e.g. gnome-shell) is never terminated, as this would also remove all window management of the session; in this
	/// case the windows are minimized instead and the dedicated desktop mode should be used for strict isolation.
	/// </summary>
	public class ExplorerShell : IExplorerShell
	{
		private readonly ILogger logger;
		private readonly INativeMethods nativeMethods;
		private readonly IList<(IntPtr handle, string title)> minimizedWindows = new List<(IntPtr, string)>();

		private string[] commandLine;
		private string workingDirectory;

		public ExplorerShell(ILogger logger, INativeMethods nativeMethods)
		{
			this.logger = logger;
			this.nativeMethods = nativeMethods;
		}

		public void HideAllWindows()
		{
			logger.Info("Searching for windows to be minimized...");

			foreach (var handle in nativeMethods.GetInteractiveWindows())
			{
				var title = nativeMethods.GetWindowTitle(handle);

				minimizedWindows.Add((handle, title));
				logger.Info($"Found window '{title}' with handle = {handle}.");
			}

			logger.Info("Minimizing all open windows...");
			nativeMethods.MinimizeAllOpenWindows();
			logger.Info("Open windows successfully minimized.");
		}

		public void RestoreAllWindows()
		{
			logger.Info("Restoring all minimized windows...");

			foreach (var (handle, title) in minimizedWindows)
			{
				nativeMethods.RestoreWindow(handle);
				logger.Info($"Restored window '{title}' with handle = {handle}.");
			}

			minimizedWindows.Clear();
			logger.Info("Minimized windows successfully restored.");
		}

		public void Start()
		{
			if (commandLine == null || commandLine.Length == 0)
			{
				logger.Info("No terminated shell known which could be restarted.");

				return;
			}

			logger.Debug("Starting desktop shell process...");

			var info = new System.Diagnostics.ProcessStartInfo(commandLine[0]) { UseShellExecute = false, WorkingDirectory = workingDirectory };

			foreach (var argument in commandLine.Skip(1))
			{
				info.ArgumentList.Add(argument);
			}

			using (var process = System.Diagnostics.Process.Start(info))
			{
				logger.Debug("Waiting for desktop shell to initialize...");

				for (var elapsed = 0; nativeMethods.GetShellWindowHandle() == IntPtr.Zero && elapsed < 10000; elapsed += 20)
				{
					Thread.Sleep(20);
				}

				logger.Info($"Desktop shell successfully started with PID = {process.Id}.");
			}
		}

		public void Terminate()
		{
			const int THREE_SECONDS = 3000;
			var processId = (int) nativeMethods.GetShellProcessId();

			if (processId == 0)
			{
				logger.Info("The desktop shell seems to already be terminated.");

				return;
			}

			System.Diagnostics.Process process;

			try
			{
				process = System.Diagnostics.Process.GetProcessById(processId);
			}
			catch (ArgumentException)
			{
				logger.Info("The desktop shell seems to already be terminated.");

				return;
			}

			if (IsWindowManager(processId))
			{
				logger.Warn($"The desktop shell with PID = {processId} is also the window manager and thus can't be terminated without breaking the session!");

				return;
			}

			RememberCommandLine(processId);
			logger.Debug($"Found desktop shell process with PID = {processId}. Sending close message...");
			nativeMethods.PostCloseMessageToShell();
			logger.Debug("Waiting for desktop shell to terminate...");

			for (var elapsed = 0; nativeMethods.GetShellWindowHandle() != IntPtr.Zero && elapsed < THREE_SECONDS; elapsed += 20)
			{
				Thread.Sleep(20);
			}

			process.WaitForExit(THREE_SECONDS);
			process.Refresh();

			if (!process.HasExited)
			{
				KillShell(process);
			}

			process.Refresh();

			if (process.HasExited)
			{
				logger.Info($"Successfully terminated desktop shell process with PID = {processId}.");
			}
			else
			{
				logger.Error($"Failed to completely terminate desktop shell process with PID = {processId}.");
			}

			process.Close();
		}

		private void KillShell(System.Diagnostics.Process process)
		{
			logger.Warn("Failed to gracefully terminate desktop shell, attempting forceful termination...");

			try
			{
				process.Kill();
				process.WaitForExit();
			}
			catch (Exception e)
			{
				logger.Error("Failed to kill desktop shell!", e);
			}
		}

		private void RememberCommandLine(int processId)
		{
			try
			{
				commandLine = File.ReadAllText($"/proc/{processId}/cmdline").Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
				workingDirectory = Directory.ResolveLinkTarget($"/proc/{processId}/cwd", true)?.FullName;
			}
			catch (Exception e)
			{
				logger.Warn($"Failed to read command line of desktop shell: {e.Message}");
			}
		}

		private bool IsWindowManager(int processId)
		{
			var name = File.ReadAllText($"/proc/{processId}/comm").Trim();

			return new[] { "gnome-shell", "mutter", "kwin_x11", "kwin_wayland", "cinnamon", "budgie-wm", "muffin" }.Contains(name);
		}
	}
}
