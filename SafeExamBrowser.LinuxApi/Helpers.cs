/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using SafeExamBrowser.LinuxApi.Native;

namespace SafeExamBrowser.LinuxApi
{
	/// <summary>
	/// Runs external helper tools (gsettings, xset, xclip...) and returns their output.
	/// </summary>
	internal static class Tool
	{
		internal static bool Run(string tool, string arguments, out string output)
		{
			output = string.Empty;

			try
			{
				var info = new ProcessStartInfo(tool, arguments)
				{
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false
				};

				using (var process = System.Diagnostics.Process.Start(info))
				{
					output = process.StandardOutput.ReadToEnd().Trim();
					process.WaitForExit(5000);

					return process.HasExited && process.ExitCode == 0;
				}
			}
			catch (Win32Exception)
			{
				return false;
			}
		}

		internal static bool Exists(string tool)
		{
			return Run("which", tool, out var path) && !string.IsNullOrWhiteSpace(path);
		}
	}

	/// <summary>
	/// Thread suspension on Linux is implemented via ptrace(2) (SEIZE + INTERRUPT / DETACH). As ptrace requests must always be issued
	/// by the tracer thread, all requests are executed on one dedicated thread.
	/// </summary>
	internal class ThreadSuspender
	{
		private readonly BlockingCollection<(Func<bool> action, ManualResetEventSlim done, bool[] result)> queue = new BlockingCollection<(Func<bool>, ManualResetEventSlim, bool[])>();
		private readonly object sync = new object();
		private Thread worker;

		internal bool Suspend(int threadId)
		{
			return Execute(() =>
			{
				if (LibC.ptrace(LibC.PTRACE_SEIZE, threadId, IntPtr.Zero, IntPtr.Zero) < 0)
				{
					return false;
				}

				if (LibC.ptrace(LibC.PTRACE_INTERRUPT, threadId, IntPtr.Zero, IntPtr.Zero) < 0)
				{
					LibC.ptrace(LibC.PTRACE_DETACH, threadId, IntPtr.Zero, IntPtr.Zero);

					return false;
				}

				return LibC.waitpid(threadId, out _, LibC.__WALL) >= 0;
			});
		}

		internal bool Resume(int threadId)
		{
			return Execute(() => LibC.ptrace(LibC.PTRACE_DETACH, threadId, IntPtr.Zero, IntPtr.Zero) >= 0);
		}

		private bool Execute(Func<bool> action)
		{
			lock (sync)
			{
				if (worker == null)
				{
					worker = new Thread(Work) { IsBackground = true, Name = "SEB ptrace worker" };
					worker.Start();
				}
			}

			using (var done = new ManualResetEventSlim())
			{
				var result = new bool[1];

				queue.Add((action, done, result));

				return done.Wait(5000) && result[0];
			}
		}

		private void Work()
		{
			foreach (var (action, done, result) in queue.GetConsumingEnumerable())
			{
				try
				{
					result[0] = action.Invoke();
				}
				catch
				{
					result[0] = false;
				}
				finally
				{
					done.Set();
				}
			}
		}
	}
}
