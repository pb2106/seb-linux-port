/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */


using System;
using System.Drawing;
using System.Drawing.Imaging;
using SafeExamBrowser.Settings.Proctoring;
using ImageFormat = SafeExamBrowser.Settings.Proctoring.ImageFormat;

namespace SafeExamBrowser.Proctoring.ScreenProctoring.Imaging
{
	internal static class Extensions
	{
		internal static void DrawCursorPosition(this Graphics graphics, int cursorX = 0, int cursorY = 0)
		{
			graphics.DrawArc(new Pen(Color.Red, 3), cursorX - 25, cursorY - 25, 50, 50, 0, 360);
			graphics.DrawArc(new Pen(Color.Yellow, 3), cursorX - 22, cursorY - 22, 44, 44, 0, 360);
			graphics.FillEllipse(Brushes.Red, cursorX - 4, cursorY - 4, 8, 8);
			graphics.FillEllipse(Brushes.Yellow, cursorX - 2, cursorY - 2, 4, 4);
		}

		internal static PixelFormat ToPixelFormat(this ImageQuantization quantization)
		{
			switch (quantization)
			{
				case ImageQuantization.BlackAndWhite1bpp:
					return PixelFormat.Format1bppIndexed;
				case ImageQuantization.Color8bpp:
					return PixelFormat.Format8bppIndexed;
				case ImageQuantization.Color16bpp:
					return PixelFormat.Format16bppArgb1555;
				case ImageQuantization.Color24bpp:
					return PixelFormat.Format24bppRgb;
				case ImageQuantization.Grayscale2bpp:
					return PixelFormat.Format4bppIndexed;
				case ImageQuantization.Grayscale4bpp:
					return PixelFormat.Format4bppIndexed;
				case ImageQuantization.Grayscale8bpp:
					return PixelFormat.Format8bppIndexed;
				default:
					throw new NotImplementedException($"Image quantization '{quantization}' is not yet implemented!");
			}
		}

		internal static System.Drawing.Imaging.ImageFormat ToSystemFormat(this ImageFormat format)
		{
			switch (format)
			{
				case ImageFormat.Bmp:
					return System.Drawing.Imaging.ImageFormat.Bmp;
				case ImageFormat.Gif:
					return System.Drawing.Imaging.ImageFormat.Gif;
				case ImageFormat.Jpg:
					return System.Drawing.Imaging.ImageFormat.Jpeg;
				case ImageFormat.Png:
					return System.Drawing.Imaging.ImageFormat.Png;
				default:
					throw new NotImplementedException($"Image format '{format}' is not yet implemented!");
			}
		}
	}
}
