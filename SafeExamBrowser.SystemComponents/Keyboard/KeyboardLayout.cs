/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using SafeExamBrowser.SystemComponents.Contracts.Keyboard;

namespace SafeExamBrowser.SystemComponents.Keyboard
{
	internal class KeyboardLayout : IKeyboardLayout
	{
		public string CultureCode { get; set; }
		public string CultureName { get; set; }
		public Guid Id { get; } = Guid.NewGuid();
		public bool IsCurrent { get; set; }
		public string LayoutName { get; set; }

		public override string ToString()
		{
			return $"'{CultureName}' [{CultureCode}, {LayoutName}]";
		}
	}
}
