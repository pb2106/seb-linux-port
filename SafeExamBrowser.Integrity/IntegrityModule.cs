/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SafeExamBrowser.Configuration.Contracts;
using SafeExamBrowser.Integrity.Contracts;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Integrity
{
	public class IntegrityModule : IIntegrityModule
	{
		private const string DLL_NAME =
#if X86
		"seb_x86.dll";
#else
		"seb_x64.dll";
#endif

		private static readonly byte[] SESSION_DATA_IV =
		{
			0x12, 0x07, 0x14, 0x02, 0x03, 0x10, 0x14, 0x18,
			0x11, 0x01, 0x04, 0x15, 0x06, 0x16, 0x05, 0x12
		};
		private static readonly byte[] SESSION_DATA_KEY =
		{
			0x01, 0x04, 0x07, 0x08, 0x09, 0x10, 0x13, 0x06,
			0x11, 0x14, 0x15, 0x16, 0x05, 0x03, 0x13, 0x06,
			0x01, 0x04, 0x02, 0x03, 0x14, 0x15, 0x07, 0x08,
			0x11, 0x12, 0x16, 0x05, 0x09, 0x10, 0x12, 0x02
		};

		private readonly AppConfig appConfig;
		private readonly ILogger logger;

		public IntegrityModule(AppConfig appConfig, ILogger logger)
		{
			this.appConfig = appConfig;
			this.logger = logger;
		}

		public void CacheSession(string configurationKey)
		{
			if (TryReadSessionCache(out var sessions) && TryWriteSessionCache(sessions.Append(configurationKey)))
			{
				logger.Debug("Successfully cached session.");
			}
			else
			{
				logger.Error("Failed to cache session!");
			}
		}

		public void ClearSession(string configurationKey)
		{
			if (TryReadSessionCache(out var sessions) && TryWriteSessionCache(sessions.Where(s => s != configurationKey)))
			{
				logger.Debug("Successfully cleared session.");
			}
			else
			{
				logger.Error("Failed to clear session!");
			}
		}

		public bool IsRemoteSession()
		{
			if (!OperatingSystem.IsWindows())
			{
				var sshClient = Environment.GetEnvironmentVariable("SSH_CLIENT");
				var sshTty = Environment.GetEnvironmentVariable("SSH_TTY");
				var xrdp = Environment.GetEnvironmentVariable("XRDP_SESSION");
				return !string.IsNullOrEmpty(sshClient) || !string.IsNullOrEmpty(sshTty) || !string.IsNullOrEmpty(xrdp);
			}

			var isRemoteSession = false;

			try
			{
				isRemoteSession = Native.IsRemoteSession();
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to query remote session status!", e);
			}

			return isRemoteSession;
		}

		public bool IsVirtualMachine(out string manufacturer, out int probability)
		{
			if (!OperatingSystem.IsWindows())
			{
				manufacturer = "None";
				probability = 0;
				return false;
			}

			var isVirtualMachine = false;

			manufacturer = default;
			probability = default;

			try
			{
				isVirtualMachine = Native.IsVirtualMachine(out var bstr, out probability);

				if (bstr != IntPtr.Zero)
				{
					manufacturer = Marshal.PtrToStringBSTR(bstr);
					Marshal.FreeBSTR(bstr);
				}
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to query virtual machine information!", e);
			}

			return isVirtualMachine;
		}

		public bool TryCalculateAppSignatureKey(string connectionToken, string salt, out string appSignatureKey)
		{
			if (!OperatingSystem.IsWindows())
			{
				using (var sha = SHA256.Create())
				{
					var data = Encoding.UTF8.GetBytes((connectionToken ?? "") + (salt ?? "") + (appConfig.CodeSignatureHash ?? "SafeExamBrowserLinux"));
					appSignatureKey = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
					return true;
				}
			}

			appSignatureKey = default;

			try
			{
				appSignatureKey = Native.CalculateAppSignatureKey(connectionToken, salt);
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to calculate app signature key!", e);
			}

			return appSignatureKey != default;
		}

		public bool TryCalculateBrowserExamKey(string configurationKey, string salt, out string browserExamKey)
		{
			if (!OperatingSystem.IsWindows())
			{
				using (var sha = SHA256.Create())
				{
					var data = Encoding.UTF8.GetBytes((configurationKey ?? "") + (salt ?? "") + (appConfig.CodeSignatureHash ?? "SafeExamBrowserLinux"));
					browserExamKey = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
					return true;
				}
			}

			browserExamKey = default;

			try
			{
				browserExamKey = Native.CalculateBrowserExamKey(configurationKey, salt);
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to calculate browser exam key!", e);
			}

			return browserExamKey != default;
		}

		public bool TryGenerateVerificatorCode(string payload, out string code)
		{
			if (!OperatingSystem.IsWindows())
			{
				using (var sha = SHA256.Create())
				{
					var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload ?? ""));
					code = Convert.ToBase64String(hash).Substring(0, 8).ToUpperInvariant();
					return true;
				}
			}

			var success = false;

			code = default;

			try
			{
				success = Native.TryGenerateVerificatorCode(payload, out var bstr);

				if (bstr != IntPtr.Zero)
				{
					code = Marshal.PtrToStringBSTR(bstr);
					Marshal.FreeBSTR(bstr);
				}
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to generate verificator code!", e);
			}

			return success;
		}

		public bool TryVerifyCodeSignature(out bool isValid)
		{
			if (!OperatingSystem.IsWindows())
			{
				isValid = !string.IsNullOrEmpty(appConfig.CodeSignatureHash);
				return true;
			}

			var success = false;

			isValid = default;

			try
			{
				isValid = Native.VerifyCodeSignature();
				success = true;
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to verify code signature!", e);
			}

			return success;
		}

		public bool TryVerifyRuntimeIntegrity(out bool isValid)
		{
			if (!OperatingSystem.IsWindows())
			{
				isValid = true;

				try
				{
					if (File.Exists("/proc/self/status"))
					{
						var lines = File.ReadAllLines("/proc/self/status");
						var tracerLine = lines.FirstOrDefault(l => l.StartsWith("TracerPid:", StringComparison.OrdinalIgnoreCase));

						if (tracerLine != null)
						{
							var pidStr = tracerLine.Split(':')[1].Trim();

							if (int.TryParse(pidStr, out var tracerPid) && tracerPid > 0)
							{
								logger.Warn($"Runtime integrity check: process is being traced by PID {tracerPid}!");
								isValid = false;
							}
						}
					}
				}
				catch
				{
				}

				return true;
			}

			var success = false;

			isValid = default;

			try
			{
				isValid = Native.VerifyRuntimeIntegrity(out var data, out var count);

				for (var index = 0; index < count; index++)
				{
					var pointer = Marshal.ReadIntPtr(data, index * IntPtr.Size);
					var raw = Marshal.PtrToStringBSTR(pointer);
					var item = string.Join(" ", raw.ToCharArray().Select(c => Convert.ToInt32(c)));

					logger.Warn($"Runtime Integrity Violation #{index}: {item}");

					Marshal.FreeBSTR(pointer);
				}

				Marshal.FreeCoTaskMem(data);
				success = true;
			}
			catch (DllNotFoundException)
			{
				logger.Warn("Integrity module is not available!");
			}
			catch (Exception e)
			{
				logger.Error("Unexpected error while attempting to verify runtime integrity!", e);
			}

			return success;
		}

		public bool TryVerifySessionIntegrity(string configurationKey, out bool isValid)
		{
			var success = false;

			isValid = false;

			if (TryReadSessionCache(out var sessions))
			{
				isValid = sessions.All(s => s != configurationKey);
				success = true;
				logger.Debug($"Successfully verified session integrity, session is {(isValid ? "valid." : "compromised!")}");
			}
			else
			{
				logger.Error("Failed to verify session integrity!");
			}

			return success;
		}

		private bool TryReadSessionCache(out IList<string> sessions)
		{
			var success = false;

			sessions = new List<string>();

			try
			{
				if (File.Exists(appConfig.SessionCacheFilePath))
				{
					using (var file = new FileStream(appConfig.SessionCacheFilePath, FileMode.Open))
					using (var aes = Aes.Create())
					using (var stream = new CryptoStream(file, aes.CreateDecryptor(SESSION_DATA_KEY, SESSION_DATA_IV), CryptoStreamMode.Read))
					using (var reader = new StreamReader(stream))
					{
						for (var session = reader.ReadLine(); session != default; session = reader.ReadLine())
						{
							sessions.Add(session);
						}
					}
				}

				success = true;
			}
			catch (Exception e)
			{
				logger.Error("Failed to read session cache!", e);
			}

			return success;
		}

		private bool TryWriteSessionCache(IEnumerable<string> sessions)
		{
			var success = false;

			try
			{
				if (sessions.Any())
				{
					using (var file = new FileStream(appConfig.SessionCacheFilePath, FileMode.Create))
					using (var aes = Aes.Create())
					{
						aes.Key = SESSION_DATA_KEY;
						aes.IV = SESSION_DATA_IV;

						using (var stream = new CryptoStream(file, aes.CreateEncryptor(), CryptoStreamMode.Write))
						using (var writer = new StreamWriter(stream))
						{
							foreach (var session in sessions)
							{
								writer.WriteLine(session);
							}
						}
					}
				}
				else
				{
					File.Delete(appConfig.SessionCacheFilePath);
				}

				success = true;
			}
			catch (Exception e)
			{
				logger.Error("Failed to write session cache!", e);
			}

			return success;
		}

		private static class Native
		{
			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			[return: MarshalAs(UnmanagedType.BStr)]
			internal static extern string CalculateAppSignatureKey(string connectionToken, string salt);

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			[return: MarshalAs(UnmanagedType.BStr)]
			internal static extern string CalculateBrowserExamKey(string configurationKey, string salt);

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			internal static extern bool IsRemoteSession();

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			internal static extern bool IsVirtualMachine(out IntPtr manufacturer, out int probability);

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			internal static extern bool TryGenerateVerificatorCode(string payload, out IntPtr code);

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			internal static extern bool VerifyCodeSignature();

			[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
			internal static extern bool VerifyRuntimeIntegrity(out IntPtr data, out int count);
		}
	}
}
