/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.SystemComponents.Contracts;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.FileSystemDialog;
using SafeExamBrowser.UserInterface.Contracts.Windows;
using SafeExamBrowser.UserInterface.Shared;

namespace SafeExamBrowser.UserInterface.Desktop
{
	public class FileSystemDialogFactory : Guardable, IFileSystemDialog
	{
		private readonly ISystemInfo systemInfo;
		private readonly IText text;

		public FileSystemDialogFactory(ISystemInfo systemInfo, IText text, IWindowGuard windowGuard = default) : base(windowGuard)
		{
			this.systemInfo = systemInfo;
			this.text = text;
		}

		public FileSystemDialogResult Show(
			FileSystemElement element,
			FileSystemOperation operation,
			string initialPath = default,
			string message = default,
			string title = default,
			IWindow parent = default,
			bool restrictNavigation = false,
			bool showElementPath = true)
		{
			return new FileSystemDialogResult
			{
				FullPath = initialPath,
				Success = !string.IsNullOrEmpty(initialPath)
			};
		}
	}
}
