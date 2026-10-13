/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Threading;

namespace SafeExamBrowser.Service
{
	public class Service
	{
		private CompositionRoot instances;

		public static void Main(string[] args)
		{
			var service = new Service();
			var stopEvent = new ManualResetEventSlim(false);

			Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				stopEvent.Set();
			};
			AppDomain.CurrentDomain.ProcessExit += (_, _) => stopEvent.Set();

			service.OnStart(args);
			stopEvent.Wait();
			service.OnStop();
		}

		protected void OnStart(string[] args)
		{
			instances = new CompositionRoot();
			instances.BuildObjectGraph();
			instances.LogStartupInformation();

			var success = instances.ServiceController.TryStart();

			if (!success)
			{
				Environment.Exit(-1);
			}
		}

		protected void OnStop()
		{
			instances?.ServiceController?.Terminate();
			instances?.LogShutdownInformation();
		}
	}
}
