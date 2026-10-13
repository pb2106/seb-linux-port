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
using System.IO;
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
		private readonly IBrowserControl control;
		private readonly BrowserSettings settings;
		private readonly bool isMainWindow;
		private readonly ILogger logger;
		private Process browserProcess;
		private string profileDir;
		private string currentUrl;

		public bool CanNavigateBackwards { set { } }
		public bool CanNavigateForwards { set { } }
		public IntPtr Handle => browserProcess != null && !browserProcess.HasExited ? browserProcess.MainWindowHandle : IntPtr.Zero;

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

		public CrossPlatformBrowserWindow(IBrowserControl control = null, BrowserSettings settings = null, bool isMainWindow = true, ILogger logger = null)
		{
			this.control = control;
			this.settings = settings;
			this.isMainWindow = isMainWindow;
			this.logger = logger;
		}

		public void FocusAddressBar() { }
		public void FocusBrowser() { }
		public void FocusToolbar(bool forward) { }
		public void ShowFindbar() { }

		public void UpdateAddress(string address)
		{
			currentUrl = address;
			AddressChanged?.Invoke(address);
		}

		public void UpdateIcon(IconResource icon) { }
		public void UpdateDownloadState(DownloadItemState state) { }
		public void UpdateLoadingState(bool isLoading) { }
		public void UpdateProgress(double value) { }
		public void UpdateTitle(string title) { }
		public void UpdateZoomLevel(double value) { }

		public override void Show()
		{
			base.Show();
			StartBrowserProcess();
		}

		public override void Close()
		{
			StopBrowserProcess();
			base.Close();
		}

		private void StartBrowserProcess()
		{
			if (browserProcess != null && !browserProcess.HasExited)
			{
				return;
			}

			var targetUrl = !string.IsNullOrWhiteSpace(currentUrl) ? currentUrl : settings?.StartUrl;
			if (string.IsNullOrWhiteSpace(targetUrl))
			{
				targetUrl = "https://www.safeexambrowser.org/start";
			}

			var (executable, arguments, profilePath) = ResolveBrowserCommand(targetUrl);
			if (executable == null)
			{
				logger?.Error("No suitable Linux browser found to display URL: " + targetUrl);
				return;
			}

			profileDir = profilePath;

			try
			{
				var startInfo = new ProcessStartInfo(executable, arguments)
				{
					UseShellExecute = false,
					WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
				};

				// Prevent browser from connecting to outer Wayland compositor; force X11 and software GL
				startInfo.Environment.Remove("WAYLAND_DISPLAY");
				startInfo.Environment["GDK_BACKEND"] = "x11";
				startInfo.Environment["MOZ_ENABLE_WAYLAND"] = "0";
				startInfo.Environment["MOZ_X11_EGL"] = "0";
				startInfo.Environment["MOZ_DISABLE_CONTENT_SANDBOX"] = "1";
				startInfo.Environment["MOZ_FORCE_DISABLE_EGL"] = "1";
				startInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
				startInfo.Environment["QT_QPA_PLATFORM"] = "xcb";

				var currentDisplay = Environment.GetEnvironmentVariable("DISPLAY");
				if (!string.IsNullOrEmpty(currentDisplay))
				{
					startInfo.Environment["DISPLAY"] = currentDisplay;
				}

				var currentAuth = Environment.GetEnvironmentVariable("XAUTHORITY");
				if (!string.IsNullOrEmpty(currentAuth))
				{
					startInfo.Environment["XAUTHORITY"] = currentAuth;
				}

				var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
				if (!string.IsNullOrEmpty(sudoUser) && (Environment.UserName == "root" || Environment.GetEnvironmentVariable("USER") == "root"))
				{
					// If started with sudo, running browsers (especially Firefox) directly as root
					// will be rejected because the X11 session and authority belong to the desktop user.
					if (!string.IsNullOrEmpty(profileDir) && Directory.Exists(profileDir))
					{
						try
						{
							var chown = Process.Start(new ProcessStartInfo("chown", $"-R {sudoUser}:{sudoUser} \"{profileDir}\"") { UseShellExecute = false });
							chown?.WaitForExit(1000);
						}
						catch { }
					}

					Console.WriteLine($"[SEB Linux] Detected sudo execution: launching browser as user '{sudoUser}'...");
					var fullCmd = $"{executable} {arguments}";
					startInfo.FileName = "su";
					startInfo.Arguments = $"{sudoUser} -c \"{fullCmd.Replace("\"", "\\\"")}\"";
				}

				Console.WriteLine($"[SEB Linux] Selected browser executable: '{executable}'");
				Console.WriteLine($"[SEB Linux] Command: {executable} {arguments}");
				Console.WriteLine($"[SEB Linux] Target URL: '{targetUrl}' on DISPLAY '{currentDisplay}'");

				browserProcess = Process.Start(startInfo);
				logger?.Info($"Started Linux browser process '{executable}' with PID {browserProcess?.Id} on DISPLAY '{currentDisplay}' loading '{targetUrl}'.");

				if (browserProcess != null)
				{
					browserProcess.EnableRaisingEvents = true;
					browserProcess.Exited += (s, e) =>
					{
						Console.WriteLine($"[SEB Linux] Browser process {browserProcess?.Id} exited with code {browserProcess?.ExitCode}.");
						logger?.Info("Browser process exited.");
						Close();
					};
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[SEB Linux] Failed to start browser process '{executable}': {ex.Message}");
				logger?.Error($"Failed to start browser process '{executable}': {ex.Message}", ex);
			}
		}

		private void StopBrowserProcess()
		{
			try
			{
				if (browserProcess != null && !browserProcess.HasExited)
				{
					browserProcess.Kill();
					browserProcess.WaitForExit(2000);
				}
			}
			catch
			{
			}
			finally
			{
				browserProcess = null;
			}

			if (!string.IsNullOrEmpty(profileDir) && Directory.Exists(profileDir))
			{
				try
				{
					Directory.Delete(profileDir, true);
				}
				catch
				{
				}
			}
		}

		private static (string executable, string arguments, string profilePath) ResolveBrowserCommand(string url)
		{
			var tempProfile = Path.Combine(Path.GetTempPath(), $"seb_profile_{Guid.NewGuid():N}");

			// 1. Check user override via environment variable SEB_BROWSER
			var customBrowser = Environment.GetEnvironmentVariable("SEB_BROWSER");
			if (!string.IsNullOrWhiteSpace(customBrowser))
			{
				var customPath = FindExecutable(customBrowser);
				if (customPath != null)
				{
					var customLower = customPath.ToLowerInvariant();
					if (customLower.Contains("firefox"))
					{
						return PrepareFirefox(customPath, url, tempProfile);
					}
					else if (customLower.Contains("chrome") || customLower.Contains("chromium") || customLower.Contains("brave"))
					{
						return PrepareChromium(customPath, url, tempProfile);
					}
					else
					{
						return (customPath, $"\"{url}\"", null);
					}
				}
			}

			// 2. Check Firefox first (most reliable on Xephyr / X11 without GPU hardware acceleration issues)
			var firefoxCandidates = new[] { "firefox", "firefox-esr" };
			foreach (var candidate in firefoxCandidates)
			{
				var path = FindExecutable(candidate);
				if (path != null)
				{
					return PrepareFirefox(path, url, tempProfile);
				}
			}

			// 3. Check Chromium / Google Chrome / Brave (requires --disable-gpu and --no-sandbox on Xephyr)
			var chromiumCandidates = new[] { "chromium", "chromium-browser", "google-chrome", "google-chrome-stable", "brave-browser", "brave" };
			foreach (var candidate in chromiumCandidates)
			{
				var path = FindExecutable(candidate);
				if (path != null)
				{
					return PrepareChromium(path, url, tempProfile);
				}
			}

			// 4. Check Epiphany
			var epiphanyCandidates = new[] { "epiphany-browser", "epiphany" };
			foreach (var candidate in epiphanyCandidates)
			{
				var path = FindExecutable(candidate);
				if (path != null)
				{
					return (path, $"--application-mode \"{url}\"", null);
				}
			}

			// 5. Fallback: xdg-open
			var xdgOpen = FindExecutable("xdg-open");
			if (xdgOpen != null)
			{
				return (xdgOpen, $"\"{url}\"", null);
			}

			return (null, null, null);
		}

		private static (string executable, string arguments, string profilePath) PrepareFirefox(string path, string url, string tempProfile)
		{
			try
			{
				Directory.CreateDirectory(tempProfile);
				File.WriteAllText(Path.Combine(tempProfile, "user.js"),
					"user_pref(\"browser.startup.homepage_override.mstone\", \"ignore\");\n" +
					"user_pref(\"browser.shell.checkDefaultBrowser\", false);\n" +
					"user_pref(\"toolkit.telemetry.reportingpolicy.firstRun\", false);\n" +
					"user_pref(\"datareporting.policy.dataSubmissionPolicyAcceptedVersion\", 2);\n" +
					"user_pref(\"layers.acceleration.disabled\", true);\n" +
					"user_pref(\"gfx.webrender.all\", false);\n" +
					"user_pref(\"gfx.webrender.software\", true);\n");
			}
			catch { }

			var args = $"--no-remote --new-instance --profile \"{tempProfile}\" --kiosk \"{url}\"";
			return (path, args, tempProfile);
		}

		private static (string executable, string arguments, string profilePath) PrepareChromium(string path, string url, string tempProfile)
		{
			var args = $"--kiosk --no-first-run --no-default-browser-check --disable-translate --disable-gpu --disable-software-rasterizer --no-sandbox --disable-dev-shm-usage --user-data-dir=\"{tempProfile}\" \"{url}\"";
			return (path, args, tempProfile);
		}

		private static string FindExecutable(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			if (File.Exists(name))
			{
				try
				{
					var linkTarget = File.ResolveLinkTarget(name, true);
					if (linkTarget != null && !File.Exists(linkTarget.FullName))
					{
						return null; // broken symlink
					}
				}
				catch { }
				return name;
			}

			var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin:/usr/local/bin";
			foreach (var dir in pathEnv.Split(':'))
			{
				if (string.IsNullOrWhiteSpace(dir)) continue;
				var full = Path.Combine(dir.Trim(), name);
				if (File.Exists(full))
				{
					try
					{
						var linkTarget = File.ResolveLinkTarget(full, true);
						if (linkTarget != null && !File.Exists(linkTarget.FullName))
						{
							continue; // broken symlink
						}
					}
					catch { }
					return full;
				}
			}

			return null;
		}
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
