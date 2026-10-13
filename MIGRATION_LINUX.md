# Linux (Debian) migration status

Toolchain: .NET 8 SDK (local: `.tools/dotnet`, installed via `dotnet-install.sh`). Converter: `tools/migrate_csproj.py`
(originals preserved as `*.csproj.legacy`).

## Phase 1 - done: Contracts & Foundational Libraries (builds & passes unit tests on Debian 13)
- `SafeExamBrowser.Core` + `SafeExamBrowser.Core.Contracts` + `SafeExamBrowser.Core.UnitTests` (**44/44 tests pass**)
  - Cross-platform `pack://` URI scheme registration (`System.IO.Packaging.PackUriHelper` compatibility in `IconResource`).
- `SafeExamBrowser.I18n` + `SafeExamBrowser.I18n.Contracts` + `SafeExamBrowser.I18n.UnitTests` (**10/10 tests pass**)
- `SafeExamBrowser.Logging` + `SafeExamBrowser.Logging.Contracts` + `SafeExamBrowser.Logging.UnitTests` (**23/23 tests pass**)
- `SafeExamBrowser.Settings` + `SafeExamBrowser.Settings.UnitTests` (**3/3 tests pass**)
- `SafeExamBrowser.Server` + `SafeExamBrowser.Server.Contracts` + `SafeExamBrowser.Server.UnitTests` (**12/12 tests pass**)
- All remaining `*.Contracts` projects (`Applications.Contracts`, `Browser.Contracts`, `Communication.Contracts`, `Configuration.Contracts`, `Integrity.Contracts`, `Lockdown.Contracts`, `Monitoring.Contracts`, `Proctoring.Contracts`, `SystemComponents.Contracts`, `UserInterface.Contracts`, `WindowsApi.Contracts`).

## Phase 2 - done: SafeExamBrowser.LinuxApi (implements the WindowsApi.Contracts interfaces)
- `NativeMethods`: X11/EWMH windows (list, title, icon, style, activate, minimize, close, hide/restore), working area via `_NET_WM_STRUT` dock window, clipboard clearing, wallpaper (GNOME/XFCE/feh), sticky keys (gsettings/xkbset), always-on via `systemd-inhibit` + `xset`, internet check, thread suspend/resume via `ptrace`, foreground/new-window event hooks.
- Keyboard/mouse hooks: exclusive `evdev` grab + `uinput` clone forwarding (blocking works on X11, Wayland and console); `evdev` codes are translated to Windows virtual-key codes so existing interceptors keep working; optional blocking of `Ctrl+Alt+Fn` / `Ctrl+Alt+Backspace` / `Ctrl+Alt+Del` / `SysRq` (`BlockSystemKeySequences`).
- `ProcessFactory`/`Process`: `/proc` based, signature = `dpkg` package hash (replaces Authenticode).
- `DesktopFactory`/`Desktop`/`DesktopMonitor`: dedicated X server on a free VT (`Xorg`, root) or fullscreen `Xephyr`, protected by `xauth` cookie, VT-switch detection and reactivation.
- `ExplorerShell`: panel/desktop shell terminate+restart (shells that are also the WM, e.g. `gnome-shell`, are minimized instead).
- Tests: `SafeExamBrowser.LinuxApi.UnitTests` (**6/6 tests pass**, including a live X11 test).

## Phase 3 - done: System, Lockdown, Communication, Configuration, Applications, Service, Browser, UI, Runtime & Client
- `SafeExamBrowser.SystemComponents` + `SafeExamBrowser.SystemComponents.UnitTests` (**35/35 tests pass**)
  - Linux `/proc`, `/sys`, DMI, `nmcli`, `pactl`/`wpctl`, `setxkbmap`/`gsettings`, `upower`, and JSON-backed registry store (`~/.config/SafeExamBrowser/registry.json`).
- `SafeExamBrowser.Monitoring` + `SafeExamBrowser.Monitoring.UnitTests` (**24/24 tests pass**)
  - Cross-platform display monitoring (`xrandr`/`/sys/class/drm`), virtual machine detector (DMI/hypervisor/device-tree/systemd-detect-virt), remote session detector, cursor/session/system event monitors.
- `SafeExamBrowser.Integrity` (builds with 0 errors)
  - Cross-platform integrity verification with Linux fallback when `SafeExamBrowser.Integrity.dll` native Windows binary is absent.
- `SafeExamBrowser.Applications` + `SafeExamBrowser.Applications.UnitTests` (**16/16 tests pass**)
  - Cross-platform external application monitoring, executable/icon resolution, and window tracking.
- `SafeExamBrowser.Communication` + `SafeExamBrowser.Communication.UnitTests` (**75/75 tests pass**)
  - Replaced Windows-only `System.ServiceModel` WCF named pipes with a cross-platform `NamedPipeServerStream`/`NamedPipeClientStream` (Unix Domain Sockets on Linux) RPC channel in `CommHost`/`CommProxy`.
- `SafeExamBrowser.Configuration` + `SafeExamBrowser.Configuration.UnitTests` (**86/86 tests pass**)
  - Cross-platform X.509 certificate store reading (`StoreLocation.CurrentUser` on Linux) and `AppDataFileParser` binary plist support.
- `SafeExamBrowser.Lockdown` + `SafeExamBrowser.Lockdown.UnitTests` (**22/22 tests pass**)
  - Cross-platform `FeatureConfiguration`, backup/restore, and auto-restore mechanisms.
- `SafeExamBrowser.Service` + `SafeExamBrowser.Service.UnitTests` (**38/38 tests pass**)
  - Cross-platform `NamedEventWaitHandle` IPC synchronization (`EventWaitHandle` on Windows, filesystem event file in `/tmp` on Linux) and POSIX signal daemon host.
- `SafeExamBrowser.ResetUtility` (builds with 0 errors)
  - Linux `geteuid() == 0` root check and cross-platform console handling.
- `SafeExamBrowser.Proctoring` (builds with 0 errors)
  - Linux screen capture fallback (`import`/`grim`/1x1 BMP) and cross-platform imaging.
- `SafeExamBrowser.UserInterface.Shared`, `SafeExamBrowser.UserInterface.Desktop`, `SafeExamBrowser.UserInterface.Mobile` (all build with 0 errors)
  - Cross-platform UI factories, windows, controls, `zenity` password dialog helper, and cross-platform keyboard/touch activators.
- `SafeExamBrowser.Browser` + `SafeExamBrowser.Browser.UnitTests` (**95/95 tests pass**)
  - Cross-platform `CefSharp` and `Syroot.Windows.IO.KnownFolders` (XDG download directory) compatibility layer.
- `SafeExamBrowser.Runtime` + `SafeExamBrowser.Runtime.UnitTests` (**245/245 tests pass**)
  - Wired `SafeExamBrowser.LinuxApi` in `CompositionRoot`, cross-platform `NamedEventWaitHandle` in `ServiceOperation`, and cross-platform application lifecycle in `App`.
- `SafeExamBrowser.Client` + `SafeExamBrowser.Client.UnitTests` (**154/154 tests pass**)
  - Wired `SafeExamBrowser.LinuxApi` in `CompositionRoot` and cross-platform application lifecycle in `App`.

## Unit Test Summary on Linux (.NET 8)
- `SafeExamBrowser.Core.UnitTests`: 44/44 passed
- `SafeExamBrowser.I18n.UnitTests`: 10/10 passed
- `SafeExamBrowser.Logging.UnitTests`: 23/23 passed
- `SafeExamBrowser.Settings.UnitTests`: 3/3 passed
- `SafeExamBrowser.Server.UnitTests`: 12/12 passed
- `SafeExamBrowser.LinuxApi.UnitTests`: 6/6 passed
- `SafeExamBrowser.SystemComponents.UnitTests`: 35/35 passed
- `SafeExamBrowser.Monitoring.UnitTests`: 24/24 passed
- `SafeExamBrowser.Applications.UnitTests`: 16/16 passed
- `SafeExamBrowser.Communication.UnitTests`: 75/75 passed
- `SafeExamBrowser.Configuration.UnitTests`: 86/86 passed
- `SafeExamBrowser.Lockdown.UnitTests`: 22/22 passed
- `SafeExamBrowser.Service.UnitTests`: 38/38 passed
- `SafeExamBrowser.Browser.UnitTests`: 95/95 passed
- `SafeExamBrowser.Runtime.UnitTests`: 245/245 passed
- `SafeExamBrowser.Client.UnitTests`: 154/154 passed
- **Total**: **888/888 unit tests passing** across all 16 unit test projects on Linux!
