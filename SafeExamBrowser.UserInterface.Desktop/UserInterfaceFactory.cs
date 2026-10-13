/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using SafeExamBrowser.Applications.Contracts;
using SafeExamBrowser.Configuration.Contracts;
using SafeExamBrowser.Core.Contracts.Notifications;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Server.Contracts;
using SafeExamBrowser.Server.Contracts.Data;
using SafeExamBrowser.Settings.Browser;
using SafeExamBrowser.Settings.Server;
using SafeExamBrowser.Settings.UserInterface;
using SafeExamBrowser.SystemComponents.Contracts.Audio;
using SafeExamBrowser.SystemComponents.Contracts.Keyboard;
using SafeExamBrowser.SystemComponents.Contracts.Network;
using SafeExamBrowser.SystemComponents.Contracts.PowerSupply;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Browser;
using SafeExamBrowser.UserInterface.Contracts.Proctoring;
using SafeExamBrowser.UserInterface.Contracts.Shell;
using SafeExamBrowser.UserInterface.Contracts.Windows;
using SafeExamBrowser.UserInterface.Contracts.Windows.Data;
using SafeExamBrowser.UserInterface.Shared;

namespace SafeExamBrowser.UserInterface.Desktop
{
	public class UserInterfaceFactory : Guardable, IUserInterfaceFactory
	{
		private readonly IText text;

		/// <remarks>
		/// The <see cref="IWindowGuard"/> is optional, as it is only used by the client application component.
		/// </remarks>
		public UserInterfaceFactory(IText text, IWindowGuard windowGuard = default) : base(windowGuard)
		{
			this.text = text;
		}

		public IWindow CreateAboutWindow(AppConfig appConfig)
		{
			return Guard(new CrossPlatformWindow());
		}

		public IActionCenter CreateActionCenter()
		{
			return new CrossPlatformActionCenter();
		}

		public IApplicationControl CreateApplicationControl(IApplication<IApplicationWindow> application, Location location)
		{
			return new CrossPlatformApplicationControl();
		}

		public ISystemControl CreateAudioControl(IAudio audio, Location location)
		{
			return new CrossPlatformSystemControl();
		}

		public IBrowserWindow CreateBrowserWindow(IBrowserControl control, BrowserSettings settings, bool isMainWindow, ILogger logger)
		{
			return Guard(new CrossPlatformBrowserWindow());
		}

		public ICredentialsDialog CreateCredentialsDialog(CredentialsDialogPurpose purpose, string message, string title)
		{
			return Guard(new CrossPlatformCredentialsDialog());
		}

		public IErrorDialog CreateErrorDialog(TextKey message, TextKey title, Action sendMailCallback, bool showIgnoreCheckbox = false, params string[] logFiles)
		{
			return new CrossPlatformErrorDialog();
		}

		public IExamSelectionDialog CreateExamSelectionDialog(IEnumerable<Exam> exams)
		{
			return new CrossPlatformExamSelectionDialog(exams);
		}

		public ISystemControl CreateKeyboardLayoutControl(IKeyboard keyboard, Location location)
		{
			return new CrossPlatformSystemControl();
		}

		public ILockScreen CreateLockScreen(string message, string title, IEnumerable<LockScreenOption> options, LockScreenSettings settings)
		{
			return Guard(new CrossPlatformLockScreen());
		}

		public IWindow CreateLogWindow(ILogger logger)
		{
			return Guard(new CrossPlatformWindow());
		}

		public ISystemControl CreateNetworkControl(INetworkAdapter adapter, Location location)
		{
			return new CrossPlatformSystemControl();
		}

		public INotificationControl CreateNotificationControl(INotification notification, Location location)
		{
			return new CrossPlatformNotificationControl();
		}

		public IPasswordDialog CreatePasswordDialog(string message, string title)
		{
			return Guard(new CrossPlatformPasswordDialog(message, title));
		}

		public IPasswordDialog CreatePasswordDialog(TextKey message, TextKey title)
		{
			return Guard(new CrossPlatformPasswordDialog(text.Get(message), text.Get(title)));
		}

		public ISystemControl CreatePowerSupplyControl(IPowerSupply powerSupply, Location location)
		{
			return new CrossPlatformSystemControl();
		}

		public IProctoringFinalizationDialog CreateProctoringFinalizationDialog(bool requiresPassword)
		{
			return Guard(new CrossPlatformProctoringFinalizationDialog());
		}

		public IProctoringWindow CreateProctoringWindow(IProctoringControl control)
		{
			return Guard(new CrossPlatformProctoringWindow());
		}

		public INotificationControl CreateRaiseHandControl(IInvigilator invigilator, Location location, InvigilationSettings settings)
		{
			return new CrossPlatformNotificationControl();
		}

		public IRuntimeWindow CreateRuntimeWindow(AppConfig appConfig)
		{
			return Guard(new CrossPlatformRuntimeWindow());
		}

		public IServerFailureDialog CreateServerFailureDialog(string info, bool showFallback)
		{
			return new CrossPlatformServerFailureDialog();
		}

		public ISplashScreen CreateSplashScreen(AppConfig appConfig = null)
		{
			return Guard(new CrossPlatformSplashScreen { AppConfig = appConfig });
		}

		public ITaskbar CreateTaskbar(ILogger logger)
		{
			return new CrossPlatformTaskbar();
		}

		public ITaskview CreateTaskview()
		{
			return new CrossPlatformTaskview();
		}

		public IVerificatorOverlay CreateVerificatorOverlay()
		{
			return Guard(new CrossPlatformVerificatorOverlay());
		}
	}
}
