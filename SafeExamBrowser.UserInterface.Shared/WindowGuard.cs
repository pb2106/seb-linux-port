/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Concurrent;
using System.Linq;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Windows;

namespace SafeExamBrowser.UserInterface.Shared
{
	public class WindowGuard : IWindowGuard
	{
		private readonly ConcurrentStack<object> cache;
		private readonly ILogger logger;

		private bool? active;

		public WindowGuard(ILogger logger)
		{
			this.cache = new ConcurrentStack<object>();
			this.logger = logger;
		}

		public void Activate()
		{
			active = true;

			while (cache.Any())
			{
				if (cache.TryPop(out var window))
				{
					Guard(window);
				}
			}
		}

		public void Deactivate()
		{
			active = false;
			cache.Clear();
		}

		public void Register(object @object)
		{
			if (@object is IWindow)
			{
				Triage(@object);
			}
			else
			{
				throw new ArgumentException($"The given object '{@object}' is not an application window!", nameof(@object));
			}
		}

		private void Guard(object window)
		{
			var info = $"{window?.GetType().Name}";

			try
			{
				logger.Debug($"Successfully guarded window '{info}'.");
			}
			catch (Exception e)
			{
				logger.Error($"Failed to guard window '{info}'!", e);
			}
		}

		private void Triage(object window)
		{
			if (active == true)
			{
				Guard(window);
			}
			else if (!active.HasValue)
			{
				cache.Push(window);
			}
		}
	}
}
