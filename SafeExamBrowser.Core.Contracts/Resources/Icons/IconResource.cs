/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;

namespace System.IO.Packaging
{
	public static class PackUriHelper
	{
		public static readonly string UriSchemePack = "pack";

		static PackUriHelper()
		{
			if (!UriParser.IsKnownScheme(UriSchemePack))
			{
				UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), UriSchemePack, -1);
			}
		}
	}
}

namespace SafeExamBrowser.Core.Contracts.Resources.Icons
{
	/// <summary>
	/// Defines an icon resource.
	/// </summary>
	public abstract class IconResource
	{
		static IconResource()
		{
			_ = System.IO.Packaging.PackUriHelper.UriSchemePack;
		}
	}
}
