/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SafeExamBrowser.UserInterface.Contracts.Windows;

namespace SafeExamBrowser.UserInterface.Mobile
{
	public class MessageBoxFactory : IMessageBox
	{
		private IText text;

		public MessageBoxFactory(IText text)
		{
			this.text = text;
		}

		public MessageBoxResult Show(string message, string title, MessageBoxAction action = MessageBoxAction.Ok, MessageBoxIcon icon = MessageBoxIcon.Information, IWindow parent = null)
		{
			switch (action)
			{
				case MessageBoxAction.YesNo:
					return MessageBoxResult.Yes;
				default:
					return MessageBoxResult.Ok;
			}
		}

		public MessageBoxResult Show(TextKey message, TextKey title, MessageBoxAction action = MessageBoxAction.Ok, MessageBoxIcon icon = MessageBoxIcon.Information, IWindow parent = null)
		{
			return Show(text.Get(message), text.Get(title), action, icon, parent);
		}
	}
}
