/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Threading.Tasks;

namespace SafeExamBrowser.Communication
{
	/// <summary>
	/// Adapts a <see cref="Task"/> to the APM (Begin/End) pattern required by
	/// <see cref="System.ServiceModel.ICommunicationObject"/>.
	/// </summary>
	internal static class TaskApmExtensions
	{
		/// <summary>
		/// Wraps a <see cref="Task"/> so it can be returned from a Begin* method.
		/// </summary>
		public static IAsyncResult AsApm(this Task task, AsyncCallback callback, object state)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));

			var tcs = new TaskCompletionSource<object>(state);

			task.ContinueWith(t =>
			{
				if (t.IsFaulted)
					tcs.TrySetException(t.Exception.InnerExceptions);
				else if (t.IsCanceled)
					tcs.TrySetCanceled();
				else
					tcs.TrySetResult(null);

				callback?.Invoke(tcs.Task);
			}, TaskScheduler.Default);

			return tcs.Task;
		}
	}
}
