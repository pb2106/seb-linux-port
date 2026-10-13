/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Runtime.InteropServices;
using System.Threading;
using SafeExamBrowser.Configuration.Contracts;

namespace SafeExamBrowser.Client
{
	public class App
	{
		private const int ILMCM_CHECKLAYOUTANDTIPENABLED = 0x00001;
		private const int ILMCM_LANGUAGEBAROFF = 0x00002;

		private static readonly Mutex Mutex = new Mutex(true, AppConfig.CLIENT_MUTEX_NAME);
		private readonly CompositionRoot instances = new CompositionRoot();
		private readonly ManualResetEventSlim shutdownEvent = new ManualResetEventSlim(false);
		private int isShuttingDown;

		[STAThread]
		public static void Main()
		{
			try
			{
				StartApplication();
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Fatal Error: {e.Message}\n\n{e.StackTrace}");
			}
			finally
			{
				Mutex.Close();
			}
		}

		private static void StartApplication()
		{
			if (NoInstanceRunning())
			{
				new App().Run();
			}
			else
			{
				Console.Error.WriteLine("Startup Not Allowed: You can only run one instance of SEB at a time.");
			}
		}

		private static bool NoInstanceRunning()
		{
			return Mutex.WaitOne(TimeSpan.Zero, true);
		}

		public void Run()
		{
			if (OperatingSystem.IsWindows())
			{
				// We need to manually initialize a monitor in order to prevent Windows from automatically doing so and thus rendering an input lanuage
				// switch in the bottom right corner of the desktop. This must be done before any UI element is initialized or rendered on the screen.
				InitLocalMsCtfMonitor(ILMCM_CHECKLAYOUTANDTIPENABLED | ILMCM_LANGUAGEBAROFF);
			}

			instances.BuildObjectGraph(Shutdown);
			instances.LogStartupInformation();

			var success = instances.ClientController.TryStart();

			if (!success)
			{
				Shutdown();
			}
			else
			{
				shutdownEvent.Wait();
			}
		}

		public void Shutdown()
		{
			if (Interlocked.Exchange(ref isShuttingDown, 1) == 0)
			{
				instances.ClientController?.Terminate();
				instances.LogShutdownInformation();

				if (OperatingSystem.IsWindows())
				{
					UninitLocalMsCtfMonitor();
				}

				shutdownEvent.Set();
			}
		}

		[DllImport("MsCtfMonitor.dll", SetLastError = true)]
		private static extern IntPtr InitLocalMsCtfMonitor(int dwFlags);

		[DllImport("MsCtfMonitor.dll", SetLastError = true)]
		private static extern IntPtr UninitLocalMsCtfMonitor();
	}
}
