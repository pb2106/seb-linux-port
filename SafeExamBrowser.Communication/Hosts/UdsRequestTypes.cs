/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using SafeExamBrowser.Communication.Contracts.Data;

namespace SafeExamBrowser.Communication.Hosts
{
	// Shared request envelope types serialised over the UDS stream.
	// Both the host (server) and proxy (client) use these.

	[Serializable]
	internal class UdsConnectRequest
	{
		public Guid? Token { get; set; }
	}

	[Serializable]
	internal class UdsDisconnectRequest
	{
		public DisconnectionMessage Message { get; set; }
	}

	[Serializable]
	internal class UdsSendRequest
	{
		public Message Message { get; set; }
	}
}
