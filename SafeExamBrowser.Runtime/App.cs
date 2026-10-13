/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using SafeExamBrowser.Configuration.Contracts;

namespace SafeExamBrowser.Runtime
{
	public class App
	{
		private static readonly Mutex Mutex = new Mutex(true, AppConfig.RUNTIME_MUTEX_NAME);
		private readonly CompositionRoot instances = new CompositionRoot();
		private readonly ManualResetEventSlim shutdownEvent = new ManualResetEventSlim(false);

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
			instances.BuildObjectGraph(Shutdown);
			instances.LogStartupInformation();

			Task.Run(new Action(TryStart));
			shutdownEvent.Wait();
		}

		private void TryStart()
		{
			var success = instances.RuntimeController.TryStart();

			if (!success)
			{
				Shutdown();
			}
		}

		public void Shutdown()
		{
			Task.Run(new Action(ShutdownInternal));
		}

		private void ShutdownInternal()
		{
			instances.RuntimeController.Terminate();
			instances.LogShutdownInformation();

			shutdownEvent.Set();
		}
	}
}
