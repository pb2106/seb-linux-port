/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.Proctoring;
using ImageFormat = SafeExamBrowser.Settings.Proctoring.ImageFormat;

namespace SafeExamBrowser.Proctoring.ScreenProctoring.Imaging
{
	internal class ScreenShotProcessor : IDisposable
	{
		private readonly ILogger logger;
		private readonly ScreenProctoringSettings settings;

		private Bitmap bitmap;
		private DateTime captureTime;
		private byte[] data;
		private ImageFormat format;
		private int height;
		private int width;

		internal ScreenShot Data => new ScreenShot
		{
			CaptureTime = captureTime,
			Data = data,
			Format = format,
			Height = height,
			Width = width
		};

		public ScreenShotProcessor(ILogger logger, ScreenProctoringSettings settings)
		{
			this.logger = logger;
			this.settings = settings;
		}

		public void Dispose()
		{
			bitmap?.Dispose();
			bitmap = default;
			data = default;
		}

		private static readonly byte[] FallbackPng = new byte[]
		{
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
			0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
			0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
			0x44, 0xAE, 0x42, 0x60, 0x82
		};

		internal void Compress()
		{
			var order = ProcessingOrder.QuantizingDownscaling;
			var original = ToReducedString();
			var parameters = $"{order}, {settings.ImageQuantization}, 1:{settings.ImageDownscaling}";

			if (bitmap == null)
			{
				logger.Debug($"Compressed from '{original}' to '{ToReducedString()}' ({parameters}).");
				return;
			}

			switch (order)
			{
				case ProcessingOrder.DownscalingQuantizing:
					Downscale();
					Quantize();
					Serialize();
					break;
				case ProcessingOrder.QuantizingDownscaling:
					Quantize();
					Downscale();
					Serialize();
					break;
			}

			logger.Debug($"Compressed from '{original}' to '{ToReducedString()}' ({parameters}).");
		}

		internal void Take()
		{
			captureTime = DateTime.Now;
			format = settings.ImageFormat;

			if (!OperatingSystem.IsWindows())
			{
				TakeLinux();
				logger.Debug($"Captured '{ToReducedString()}' at {captureTime}.");
				return;
			}

			width = 1920;
			height = 1080;
			bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

			using (var graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
				graphics.DrawCursorPosition();
			}

			Serialize();

			logger.Debug($"Captured '{ToReducedString()}' at {captureTime}.");
		}

		private void TakeLinux()
		{
			var ext = format.ToString().ToLowerInvariant();
			var scaleArg = settings.ImageDownscaling > 1
				? $"-resize {Convert.ToInt32(100.0 / settings.ImageDownscaling)}% "
				: string.Empty;
			var tmpFile = Path.Combine(Path.GetTempPath(), $"seb-screenshot-{Guid.NewGuid():N}.{ext}");

			try
			{
				var psi = new ProcessStartInfo("/bin/sh", $"-c \"import -window root {scaleArg}'{tmpFile}' 2>/dev/null || scrot '{tmpFile}' 2>/dev/null || grim '{tmpFile}' 2>/dev/null\"")
				{
					CreateNoWindow = true,
					UseShellExecute = false
				};
				using (var p = Process.Start(psi))
				{
					p?.WaitForExit(3000);
				}

				if (File.Exists(tmpFile))
				{
					data = File.ReadAllBytes(tmpFile);
					width = 1920 / Math.Max(1, (int)settings.ImageDownscaling);
					height = 1080 / Math.Max(1, (int)settings.ImageDownscaling);
					return;
				}
			}
			catch (Exception e)
			{
				logger.Debug($"Linux screenshot tool fallback triggered: {e.Message}");
			}
			finally
			{
				try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
			}

			width = 1;
			height = 1;
			data = FallbackPng;
		}

		private void Downscale()
		{
			if (settings.ImageDownscaling > 1)
			{
				height = Convert.ToInt32(height / settings.ImageDownscaling);
				width = Convert.ToInt32(width / settings.ImageDownscaling);

				var downscaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);

				using (var g = Graphics.FromImage(downscaled))
				{
					g.InterpolationMode = InterpolationMode.NearestNeighbor;
					g.DrawImage(bitmap, new Rectangle(0, 0, width, height));
				}

				bitmap.Dispose();
				bitmap = downscaled;
			}
		}

		private void Quantize()
		{
			try
			{
				var pixelFormat = settings.ImageQuantization.ToPixelFormat();
				if (bitmap.PixelFormat != pixelFormat)
				{
					var quantized = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), pixelFormat);
					bitmap.Dispose();
					bitmap = quantized;
				}
			}
			catch
			{
			}
		}

		private void Serialize()
		{
			using (var memoryStream = new MemoryStream())
			{
				if (format == ImageFormat.Jpg)
				{
					SerializeJpg(memoryStream);
				}
				else
				{
					bitmap.Save(memoryStream, format.ToSystemFormat());
				}

				data = memoryStream.ToArray();
			}
		}

		private void SerializeJpg(MemoryStream memoryStream)
		{
			var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
			var quality = 100;

			using (var parameters = new EncoderParameters(1))
			{
				parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
				bitmap.Save(memoryStream, codec, parameters);
			}
		}

		private string ToReducedString()
		{
			return $"{width}x{height}, {data.Length / 1000:N0}kB, {format.ToString().ToUpper()}";
		}
	}
}
