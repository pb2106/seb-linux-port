# Safe Exam Browser for Linux (.NET 8)

Safe Exam Browser (SEB) for Linux is a port of the Safe Exam Browser 3.x architecture to **.NET 8 (`net8.0`)** targeting Debian/Ubuntu and other modern Linux distributions. It turns a Linux workstation into a secure kiosk environment for online exams by controlling system functions, input devices, display/desktop isolation, network/audio/power telemetry, and application execution.

---

## Table of Contents

1. [System Requirements & Linux Dependencies](#1-system-requirements--linux-dependencies)
2. [Building & Compiling on Linux](#2-building--compiling-on-linux)
3. [Running Unit Tests](#3-running-unit-tests)
4. [Running Safe Exam Browser on Linux](#4-running-safe-exam-browser-on-linux)
5. [Linux Permissions & Kiosk Lockdown Setup](#5-linux-permissions--kiosk-lockdown-setup)
6. [Configuration, Logs & File Paths on Linux](#6-configuration-logs--file-paths-on-linux)
7. [Architecture & Module Overview](#7-architecture--module-overview)

---

## 1. System Requirements & Linux Dependencies

### Runtime / SDK
- **.NET 8 SDK (`8.0.x`)**:
  - A local .NET 8 SDK is already included in this repository at `.tools/dotnet/dotnet`.
  - Alternatively, you can install `dotnet-sdk-8.0` system-wide or add `.tools/dotnet` to your `PATH`:
    ```bash
    export PATH="$PWD/.tools/dotnet:$PATH"
    ```

### Linux Packages (Debian / Ubuntu)
Install the recommended system packages used by `SafeExamBrowser.LinuxApi`, `SafeExamBrowser.SystemComponents`, `SafeExamBrowser.Monitoring`, and `SafeExamBrowser.UserInterface`:

```bash
sudo apt update && sudo apt install -y \
  libx11-6 \
  x11-xserver-utils \
  xserver-xephyr \
  xauth \
  wmctrl \
  xdotool \
  zenity \
  network-manager \
  pulseaudio-utils \
  upower \
  imagemagick
```

| Subsystem | Linux Tool / Interface Used |
| :--- | :--- |
| **Window Management & Working Area** | `libX11.so.6` (EWMH `_NET_CLIENT_LIST`, `_NET_ACTIVE_WINDOW`, `_NET_WM_STRUT`), `wmctrl`, `xdotool` |
| **Isolated Kiosk Desktop** | `Xephyr` (rootless fullscreen X server) or `Xorg` (dedicated VT with `xauth` cookie protection) |
| **Keyboard & Mouse Interception** | Linux `evdev` (`/dev/input/event*` `EVIOCGRAB`) + `uinput` (`/dev/uinput`) virtual device forwarding |
| **Audio Control** | PipeWire (`wpctl`) or PulseAudio (`pactl`) |
| **Network Monitoring** | NetworkManager (`nmcli`) and `/sys/class/net` |
| **Battery / Power Monitoring** | `/sys/class/power_supply/*` and `upower` |
| **Display Monitoring** | `xrandr` and `/sys/class/drm` |
| **VM & Remote Session Detection** | `/sys/class/dmi/id/*`, `/proc/cpuinfo`, `systemd-detect-virt`, SSH/VNC/XRDP session checks |
| **Dialogs & UI Helpers** | `zenity` |

---

## 2. Building & Compiling on Linux

You can use either the bundled `.tools/dotnet/dotnet` binary or a system-installed `dotnet` CLI.

### Build the Entire Solution
From the repository root:

```bash
# Build all 49 Linux projects (Debug configuration)
.tools/dotnet/dotnet build SafeExamBrowser.sln

# Build in Release configuration
.tools/dotnet/dotnet build SafeExamBrowser.sln -c Release
```

> [!TIP]
> If you are building in an offline environment using the local NuGet cache (`~/.nuget/packages`), pass `--no-restore` after the initial restore:
> ```bash
> .tools/dotnet/dotnet build SafeExamBrowser.sln --no-restore
> ```

### Build Individual Executables
Safe Exam Browser consists of four main executable projects:

```bash
# 1. Runtime (Session coordinator & main entry point)
.tools/dotnet/dotnet build SafeExamBrowser.Runtime/SafeExamBrowser.Runtime.csproj

# 2. Client (Kiosk shell, browser host, monitoring & UI)
.tools/dotnet/dotnet build SafeExamBrowser.Client/SafeExamBrowser.Client.csproj

# 3. Service (Background privileged lockdown daemon)
.tools/dotnet/dotnet build SafeExamBrowser.Service/SafeExamBrowser.Service.csproj

# 4. ResetUtility (CLI recovery tool to restore system lockdown settings)
.tools/dotnet/dotnet build SafeExamBrowser.ResetUtility/SafeExamBrowser.ResetUtility.csproj
```

### Publish a Unified Output Directory
To publish all executables and shared libraries into a single self-contained directory (e.g. `dist/linux-x64`) so `SafeExamBrowser.Runtime` and `SafeExamBrowser.Client` sit side-by-side:

```bash
.tools/dotnet/dotnet publish SafeExamBrowser.Runtime/SafeExamBrowser.Runtime.csproj -c Release -o dist/linux-x64
.tools/dotnet/dotnet publish SafeExamBrowser.Client/SafeExamBrowser.Client.csproj -c Release -o dist/linux-x64
.tools/dotnet/dotnet publish SafeExamBrowser.Service/SafeExamBrowser.Service.csproj -c Release -o dist/linux-x64
.tools/dotnet/dotnet publish SafeExamBrowser.ResetUtility/SafeExamBrowser.ResetUtility.csproj -c Release -o dist/linux-x64
```

---

## 3. Running Unit Tests

All **16 unit test projects (888 unit tests in total)** build and pass on Linux under .NET 8.

### Run All Unit Tests
```bash
.tools/dotnet/dotnet test SafeExamBrowser.sln --no-restore
```

### Run Individual Test Suites
```bash
.tools/dotnet/dotnet test SafeExamBrowser.Core.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.I18n.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Logging.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Settings.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Server.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.LinuxApi.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.SystemComponents.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Monitoring.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Applications.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Communication.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Configuration.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Lockdown.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Service.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Browser.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Runtime.UnitTests
.tools/dotnet/dotnet test SafeExamBrowser.Client.UnitTests
```

---

## 4. Running Safe Exam Browser on Linux

### Step 1: (Optional) Start the Background Service
If your `.seb` configuration requires the SEB Service (`ServicePolicy.Mandatory` or `Warn`), run `SafeExamBrowser.Service` in a terminal (or as a `systemd` service):

```bash
sudo ./SafeExamBrowser.Service/bin/Debug/net8.0/SafeExamBrowser.Service
```
*(By default, `IgnoreService = true` in default settings, so the service is optional unless enabled in your `.seb` file.)*

### Step 2: Run Safe Exam Browser (`SafeExamBrowser.Runtime`)
`SafeExamBrowser.Runtime` is the primary entry point. It initializes configuration, sets up the kiosk desktop/session, and automatically spawns and communicates with `SafeExamBrowser.Client` over Unix domain sockets (`NamedPipeServerStream`).

```bash
# Run with default settings (or ~/.config/SafeExamBrowser/SebClientSettings.seb if present)
./SafeExamBrowser.Runtime/bin/Debug/net8.0/SafeExamBrowser

# Run with a specific .seb configuration file or seb:// URI
./SafeExamBrowser.Runtime/bin/Debug/net8.0/SafeExamBrowser /path/to/exam.seb

# Or run directly via the .NET CLI
.tools/dotnet/dotnet run --project SafeExamBrowser.Runtime -- /path/to/exam.seb
```

### Running the Reset Utility
If an exam session was forcefully terminated and you need to reset feature/lockdown backups:

```bash
sudo ./SafeExamBrowser.ResetUtility/bin/Debug/net8.0/SafeExamBrowser.ResetUtility
```

---

## 5. Linux Permissions & Kiosk Lockdown Setup

### Keyboard & Mouse Hook Permissions (`evdev` + `uinput`)
To allow `SafeExamBrowser.LinuxApi` to intercept and block restricted key combinations (such as `Alt+Tab`, `Ctrl+Alt+Del`, `Super`, `Ctrl+Alt+Fn`) without running the entire client as `root`:

1. Add your user to the `input` group:
   ```bash
   sudo usermod -aG input "$USER"
   ```
2. Load the `uinput` kernel module and grant group read/write access via a `udev` rule:
   ```bash
   sudo modprobe uinput
   echo 'KERNEL=="uinput", MODE="0660", GROUP="input", OPTIONS+="static_node=uinput"' | \
     sudo tee /etc/udev/rules.d/99-seb-uinput.rules
   sudo udevadm control --reload-rules && sudo udevadm trigger
   ```

### Virtual Machine & Display Policies During Testing
By default, Safe Exam Browser enforces strict exam security:
- **Virtual Machine Detection**: `VirtualMachinePolicy` defaults to `Deny` (blocks startup inside QEMU/KVM, VMware, VirtualBox, etc.).
- **Display Count**: `AllowedDisplays` defaults to `1` internal display.
- **Kiosk Mode**: `KioskMode` defaults to `CreateNewDesktop` (launches an isolated `Xephyr` fullscreen X server or dedicated `Xorg` VT).

When testing inside a VM or multi-monitor development setup, use a `.seb` configuration file with:
- `allowVirtualMachine = true`
- `enableSebBrowser = true`
- `createNewDesktop = false` (or `killExplorerShell = false` (`KioskMode.None`) for windowed testing)

---

## 6. Configuration, Logs & File Paths on Linux

| Resource | Linux Path |
| :--- | :--- |
| **User Configuration (`AppDataFilePath`)** | `~/.config/SafeExamBrowser/SebClientSettings.seb` |
| **System Configuration (`ProgramDataFilePath`)** | `/usr/share/SafeExamBrowser/SebClientSettings.seb` |
| **Simulated Registry Store** | `~/.config/SafeExamBrowser/registry.json` |
| **Logs (`Runtime`, `Client`, `Browser`, `Service`)** | `~/.local/share/SafeExamBrowser/Logs/` |
| **Browser Cache** | `~/.local/share/SafeExamBrowser/Cache/` |
| **Temporary Session Files** | `~/.local/share/SafeExamBrowser/Temp/` |
| **IPC Named Pipes & Service Events** | `/tmp/CoreFxPipe_net.pipe.safeexambrowser.*` & `/tmp/seb-event-*` |

---

## 7. Architecture & Module Overview

| Module | Description |
| :--- | :--- |
| **`SafeExamBrowser.Runtime`** | Main executable; manages session lifecycle, configuration loading, desktop switching (`CreateNewDesktop` / `DisableExplorerShell`), and launches `SafeExamBrowser.Client`. |
| **`SafeExamBrowser.Client`** | Client executable running inside the exam session; hosts the browser, taskbar/action center, proctoring, and input/display/application monitors. |
| **`SafeExamBrowser.Service`** | Privileged daemon that applies and restores system lockdown settings and monitors session events via `NamedEventWaitHandle`. |
| **`SafeExamBrowser.ResetUtility`** | Command-line utility (requires root `euid == 0`) to restore backed-up system configuration. |
| **`SafeExamBrowser.LinuxApi`** | Linux implementation of `SafeExamBrowser.WindowsApi.Contracts`: X11/EWMH window manager control, `evdev`/`uinput` keyboard & mouse hooks, `/proc` process management & `dpkg` package hash verification, `Xephyr`/`Xorg` isolated desktops, and desktop shell management. |
| **`SafeExamBrowser.SystemComponents`** | Linux hardware & OS integration: audio (`wpctl`/`pactl`), network (`nmcli`), power supply (`/sys/class/power_supply`/`upower`), keyboard layouts (`setxkbmap`/`gsettings`), system/user info, and JSON-backed registry. |
| **`SafeExamBrowser.Monitoring`** | Display monitor (`xrandr`/DRM), application monitor, keyboard/mouse interceptors, VM detector, remote session detector, and system sentinel. |
| **`SafeExamBrowser.Communication`** | Cross-platform RPC communication host and proxy layer over `.NET` `NamedPipeServerStream` / `NamedPipeClientStream` (Unix Domain Sockets on Linux). |
| **`SafeExamBrowser.Configuration`** | `.seb` configuration encryption/decryption (X.509 certificates & passwords), XML/binary plist parsers, and data mapping. |
| **`SafeExamBrowser.Browser`** | Browser application logic, request/resource filtering, LMS integrations (Moodle, Open edX), and JavaScript clipboard/zoom injection. |
| **`SafeExamBrowser.UserInterface.*`** | Cross-platform user interface factories, windows, dialogs, controls, and keyboard/touch activators (`Shared`, `Desktop`, `Mobile`). |
| **`SafeExamBrowser.Lockdown`** | System feature configuration backup and restoration. |
| **`SafeExamBrowser.Applications`** | External permitted/blacklisted application monitoring and window tracking. |
| **`SafeExamBrowser.Server`** | SEB Server REST API client and invigilation support. |
| **`SafeExamBrowser.Proctoring`** | Screen proctoring and screenshot capture/processing. |
| **`SafeExamBrowser.Integrity`** | Session and binary integrity verification. |
| **`SafeExamBrowser.Core` / `I18n` / `Logging` / `Settings`** | Core operation/responsibility patterns, localization, thread-safe logging, and settings models. |
