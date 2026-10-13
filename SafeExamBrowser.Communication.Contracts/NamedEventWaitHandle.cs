/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SafeExamBrowser.Communication.Contracts
{
	public class NamedEventWaitHandle : EventWaitHandle
	{
		private static readonly ConcurrentDictionary<string, NamedEventWaitHandle> InProcessEvents =
			new ConcurrentDictionary<string, NamedEventWaitHandle>(StringComparer.Ordinal);

		private readonly string eventName;
		private readonly string filePath;
		private bool closed;

		public NamedEventWaitHandle(string eventName) : base(false, EventResetMode.AutoReset)
		{
			this.eventName = eventName;
			this.filePath = GetFilePath(eventName);
			InProcessEvents[eventName] = this;

			if (!OperatingSystem.IsWindows())
			{
				try
				{
					File.WriteAllText(filePath, "0");
				}
				catch
				{
				}
			}
		}

		public static EventWaitHandle Create(string eventName)
		{
			if (OperatingSystem.IsWindows())
			{
				return new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
			}

			return new NamedEventWaitHandle(eventName);
		}

		public static bool Trigger(EventWaitHandle handle)
		{
			if (handle is NamedEventWaitHandle named)
			{
				named.SignalCrossProcess();
			}

			return handle.Set();
		}

		public new bool Set()
		{
			SignalCrossProcess();
			return base.Set();
		}

		public void SignalCrossProcess()
		{
			if (!OperatingSystem.IsWindows() && !string.IsNullOrEmpty(filePath))
			{
				try
				{
					File.WriteAllText(filePath, "1");
				}
				catch
				{
				}
			}
		}

		public static bool WaitOneExisting(string eventName, int timeoutMs)
		{
			if (OperatingSystem.IsWindows())
			{
				var serviceEvent = default(EventWaitHandle);
				var startTime = DateTime.Now;

				do
				{
					if (EventWaitHandle.TryOpenExisting(eventName, out serviceEvent))
					{
						break;
					}
				} while (startTime.AddMilliseconds(timeoutMs) > DateTime.Now);

				if (serviceEvent != default(EventWaitHandle))
				{
					using (serviceEvent)
					{
						return serviceEvent.WaitOne(timeoutMs);
					}
				}

				return false;
			}

			if (InProcessEvents.TryGetValue(eventName, out var inProcess))
			{
				return inProcess.WaitOne(timeoutMs);
			}

			var path = GetFilePath(eventName);
			var sw = Stopwatch.StartNew();

			while (true)
			{
				try
				{
					if (File.Exists(path))
					{
						var content = File.ReadAllText(path).Trim();
						if (content == "1")
						{
							File.WriteAllText(path, "0");
							return true;
						}
					}
				}
				catch
				{
				}

				if (sw.ElapsedMilliseconds >= timeoutMs)
				{
					break;
				}

				Thread.Sleep(Math.Min(10, Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds)));
			}

			return false;
		}

		public override void Close()
		{
			if (!closed)
			{
				closed = true;
				if (!string.IsNullOrEmpty(eventName))
				{
					InProcessEvents.TryRemove(eventName, out _);
				}

				if (!OperatingSystem.IsWindows() && !string.IsNullOrEmpty(filePath))
				{
					try
					{
						if (File.Exists(filePath))
						{
							File.Delete(filePath);
						}
					}
					catch
					{
					}
				}
			}

			base.Close();
		}

		private static string GetFilePath(string name)
		{
			var safe = string.Concat((name ?? "default").Split(Path.GetInvalidFileNameChars()));
			return Path.Combine(Path.GetTempPath(), $"seb-event-{safe}");
		}
	}
}
