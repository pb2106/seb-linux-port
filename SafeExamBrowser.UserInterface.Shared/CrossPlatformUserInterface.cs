/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SafeExamBrowser.Applications.Contracts;
using SafeExamBrowser.Browser.Contracts.Events;
using SafeExamBrowser.Configuration.Contracts;
using SafeExamBrowser.Core.Contracts.Notifications;
using SafeExamBrowser.Core.Contracts.Resources.Icons;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Proctoring.Contracts.Events;
using SafeExamBrowser.Server.Contracts;
using SafeExamBrowser.Server.Contracts.Data;
using SafeExamBrowser.Settings.Browser;
using SafeExamBrowser.Settings.Server;
using SafeExamBrowser.Settings.UserInterface;
using SafeExamBrowser.SystemComponents.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Audio;
using SafeExamBrowser.SystemComponents.Contracts.Keyboard;
using SafeExamBrowser.SystemComponents.Contracts.Network;
using SafeExamBrowser.SystemComponents.Contracts.PowerSupply;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Browser;
using SafeExamBrowser.UserInterface.Contracts.Browser.Data;
using SafeExamBrowser.UserInterface.Contracts.Browser.Events;
using SafeExamBrowser.UserInterface.Contracts.FileSystemDialog;
using SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SafeExamBrowser.UserInterface.Contracts.Proctoring;
using SafeExamBrowser.UserInterface.Contracts.Proctoring.Events;
using SafeExamBrowser.UserInterface.Contracts.Shell;
using SafeExamBrowser.UserInterface.Contracts.Shell.Events;
using SafeExamBrowser.UserInterface.Contracts.Windows;
using SafeExamBrowser.UserInterface.Contracts.Windows.Data;
using SafeExamBrowser.UserInterface.Contracts.Windows.Events;

namespace SafeExamBrowser.UserInterface.Shared
{
	public class CrossPlatformWindow : IWindow
	{
		public event WindowClosedEventHandler Closed;
		public event WindowClosingEventHandler Closing;

		public virtual void BringToForeground()
		{
		}

		public virtual void Close()
		{
			Closing?.Invoke();
			Closed?.Invoke();
		}

		public virtual void Hide()
		{
		}

		public virtual void Show()
		{
		}
	}

	public class CrossPlatformProgressWindow : CrossPlatformWindow, IProgressIndicator
	{
		protected int currentValue;
		protected int maxValue = 100;

		public void Progress()
		{
			if (currentValue < maxValue)
			{
				currentValue++;
			}
		}

		public void Regress()
		{
			if (currentValue > 0)
			{
				currentValue--;
			}
		}

		public void SetIndeterminate()
		{
		}

		public void SetMaxValue(int max)
		{
			maxValue = max;
		}

		public void SetValue(int value)
		{
			currentValue = value;
		}

		public void UpdateStatus(TextKey key, bool busyIndication = false)
		{
		}
	}

	public class CrossPlatformRuntimeWindow : CrossPlatformProgressWindow, IRuntimeWindow
	{
		public bool ShowLog { set { } }
		public bool ShowProgressBar { set { } }
		public bool TopMost { set { } }

		public void Notify(ILogContent content)
		{
		}
	}

	public class CrossPlatformSplashScreen : CrossPlatformProgressWindow, ISplashScreen
	{
		public AppConfig AppConfig { set { } }
	}

	public class CrossPlatformBrowserWindow : CrossPlatformWindow, IBrowserWindow
	{
		public bool CanNavigateBackwards { set { } }
		public bool CanNavigateForwards { set { } }
		public IntPtr Handle => IntPtr.Zero;

		public event AddressChangedEventHandler AddressChanged;
		public event ActionRequestedEventHandler BackwardNavigationRequested;
		public event ActionRequestedEventHandler DeveloperConsoleRequested;
		public event FindRequestedEventHandler FindRequested;
		public event ActionRequestedEventHandler ForwardNavigationRequested;
		public event ActionRequestedEventHandler HomeNavigationRequested;
		public event LoseFocusRequestedEventHandler LoseFocusRequested;
		public event ActionRequestedEventHandler ReloadRequested;
		public event ActionRequestedEventHandler ZoomInRequested;
		public event ActionRequestedEventHandler ZoomOutRequested;
		public event ActionRequestedEventHandler ZoomResetRequested;

		public void FocusAddressBar() { }
		public void FocusBrowser() { }
		public void FocusToolbar(bool forward) { }
		public void ShowFindbar() { }
		public void UpdateAddress(string address) { }
		public void UpdateIcon(IconResource icon) { }
		public void UpdateDownloadState(DownloadItemState state) { }
		public void UpdateLoadingState(bool isLoading) { }
		public void UpdateProgress(double value) { }
		public void UpdateTitle(string title) { }
		public void UpdateZoomLevel(double value) { }
	}

	public class CrossPlatformCredentialsDialog : CrossPlatformWindow, ICredentialsDialog
	{
		public CredentialsDialogResult Show(IWindow parent = null)
		{
			return new CredentialsDialogResult { Success = false };
		}
	}

	public class CrossPlatformErrorDialog : IErrorDialog
	{
		public ErrorDialogResult Show(IWindow parent = null)
		{
			return new ErrorDialogResult { ClearCrash = false };
		}
	}

	public class CrossPlatformExamSelectionDialog : IExamSelectionDialog
	{
		private readonly IEnumerable<Exam> exams;

		public CrossPlatformExamSelectionDialog(IEnumerable<Exam> exams)
		{
			this.exams = exams;
		}

		public ExamSelectionDialogResult Show(IWindow parent = null)
		{
			var first = exams?.FirstOrDefault();
			return new ExamSelectionDialogResult
			{
				SelectedExam = first,
				Success = first != null
			};
		}
	}

	public class CrossPlatformLockScreen : CrossPlatformWindow, ILockScreen
	{
		private readonly ManualResetEventSlim waitEvent = new ManualResetEventSlim(false);
		private bool canceled;

		public void Cancel()
		{
			canceled = true;
			waitEvent.Set();
		}

		public void InitializeBounds()
		{
		}

		public LockScreenResult WaitForResult()
		{
			return new LockScreenResult { Canceled = canceled };
		}
	}

	public class CrossPlatformPasswordDialog : CrossPlatformWindow, IPasswordDialog
	{
		private readonly string message;
		private readonly string title;

		public CrossPlatformPasswordDialog(string message, string title)
		{
			this.message = message;
			this.title = title;
		}

		public PasswordDialogResult Show(IWindow parent = null)
		{
			if (LinuxDialogHelper.TryZenityPassword(title, message, out var password))
			{
				return new PasswordDialogResult { Password = password, Success = true };
			}

			return new PasswordDialogResult { Success = false };
		}
	}

	public class CrossPlatformProctoringFinalizationDialog : CrossPlatformWindow, IProctoringFinalizationDialog
	{
		public string QuitPassword => null;
		public event CancellationRequestedEventHandler CancellationRequested;

		public void Update(RemainingWorkUpdatedEventArgs status)
		{
		}
	}

	public class CrossPlatformProctoringWindow : CrossPlatformWindow, IProctoringWindow
	{
		public void HideWithDelay() { }
		public void SetTitle(string title) { }
		public void Toggle() { }
	}

	public class CrossPlatformServerFailureDialog : IServerFailureDialog
	{
		public ServerFailureDialogResult Show(IWindow parent = null)
		{
			return new ServerFailureDialogResult { Abort = true, Success = true };
		}
	}

	public class CrossPlatformVerificatorOverlay : CrossPlatformWindow, IVerificatorOverlay
	{
		public void UpdateCode(byte[] data) { }
	}

	public class CrossPlatformActionCenter : IActionCenter
	{
		public bool ShowClock { set { } }
		public bool ShowQuitButton { set { } }
		public event QuitButtonClickedEventHandler QuitButtonClicked;

		public void AddApplicationControl(IApplicationControl control, bool atFirstPosition = false) { }
		public void AddNotificationControl(INotificationControl control) { }
		public void AddSystemControl(ISystemControl control) { }
		public void Close() { }
		public void Hide() { }
		public void InitializeBounds() { }
		public void InitializeText(IText text) { }
		public void Promote() { }
		public void Register(IActionCenterActivator activator) { }
		public void Show() { }
	}

	public class CrossPlatformTaskbar : ITaskbar
	{
		public bool ShowClock { set { } }
		public bool ShowQuitButton { set { } }
		public event QuitButtonClickedEventHandler QuitButtonClicked;
		public event LoseFocusRequestedEventHandler LoseFocusRequested;

		public void AddApplicationControl(IApplicationControl control, bool atFirstPosition = false) { }
		public void AddNotificationControl(INotificationControl control) { }
		public void AddSystemControl(ISystemControl control) { }
		public void Close() { }
		public void Focus(bool forward = true) { }
		public int GetAbsoluteHeight() => 40;
		public void InitializeBounds() { }
		public void InitializeText(IText text) { }
		public void Register(ITaskbarActivator activator) { }
		public void Show() { }
	}

	public class CrossPlatformTaskview : ITaskview
	{
		public void Add(IApplication<IApplicationWindow> application) { }
		public void Register(ITaskviewActivator activator) { }
	}

	public class CrossPlatformApplicationControl : IApplicationControl { }
	public class CrossPlatformNotificationControl : INotificationControl { }
	public class CrossPlatformSystemControl : ISystemControl
	{
		public void Close() { }
	}

	internal static class LinuxDialogHelper
	{
		internal static bool HasGuiDisplay()
		{
			return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
			       !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
		}

		internal static bool TryZenityPassword(string title, string text, out string password)
		{
			password = null;

			if (!HasGuiDisplay())
			{
				return false;
			}

			try
			{
				using (var process = new Process())
				{
					process.StartInfo.FileName = "zenity";
					process.StartInfo.Arguments = $"--password --title=\"{Escape(title)}\"";
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.StartInfo.CreateNoWindow = true;

					if (process.Start() && process.WaitForExit(30000) && process.ExitCode == 0)
					{
						password = process.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static string Escape(string value)
		{
			return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
