# Chipmunk Developer Guide

[![Build Windows App](https://github.com/kjh2159/Chipmunk/actions/workflows/dotnet.yml/badge.svg)](https://github.com/kjh2159/Chipmunk/actions/workflows/dotnet.yml)

*Developer guide is written because we allow any third person to modify and redistribute this Chipmunk source code.*

This document describes how to build, run, test, and package Chipmunk, as well as how the major application components work.

For end-user documentation, see the [main README](README.md).

## Technology stack

- C#
- .NET 8
- WPF
- MVVM
- CommunityToolkit.Mvvm
- LibreHardwareMonitor
- Windows Forms `NotifyIcon`
- Windows Native APIs
- Inno Setup
- x64 target

## Development requirements

Required:

- Windows 10 version 19041 or later, or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with the `.NET desktop development` workload, or a compatible command-line environment
- x64 development environment

Optional:

- Inno Setup 6.4 or later for installer generation
- PawnIO for CPU sensors requiring low-level hardware access

## Repository structure

```text
Chipmunk/
├─ Chipmunk.sln
├─ Directory.Build.props
├─ README.md
├─ README-DEV.md
├─ LICENSE
├─ THIRD-PARTY-NOTICES.md
├─ PAWNIO-NOTICE.md
├─ installer/
│  ├─ Chipmunk.iss
│  └─ dependencies/
│     └─ PawnIO_setup.exe
├─ scripts/
│  ├─ publish.ps1
│  ├─ build-installer.ps1
│  └─ fetch-pawnio.ps1
├─ src/
│  └─ Chipmunk/
│     ├─ Chipmunk.csproj
│     ├─ App.xaml
│     ├─ App.xaml.cs
│     ├─ app.manifest
│     ├─ Assets/
│     ├─ Models/
│     ├─ Services/
│     ├─ ViewModels/
│     ├─ Views/
│     ├─ Converters/
│     ├─ Interop/
│     └─ Properties/PublishProfiles/
└─ tests/
   └─ Chipmunk.Tests/
```

## Build from the command line

Open PowerShell in the repository root.

### Restore dependencies

```powershell
dotnet restore .\Chipmunk.sln
```

### Build

```powershell
dotnet build .\Chipmunk.sln `
  -c Release `
  -p:Platform=x64
```

### Run tests

```powershell
dotnet test .\Chipmunk.sln `
  -c Release `
  -p:Platform=x64
```

### Run the application

```powershell
dotnet run `
  --project .\src\Chipmunk\Chipmunk.csproj `
  -c Debug `
  -p:Platform=x64
```

The application starts in the system tray and displays the widget above the taskbar.

## Running from Visual Studio

1. Open `Chipmunk.sln`.
2. Select `Chipmunk` as the startup project.
3. Select the `x64` platform.
4. Build the solution.
5. Press `F5` to debug or `Ctrl+F5` to run without debugging.

Administrator privileges are not required for normal development.

Some CPU sensors may remain unavailable unless PawnIO is installed.

## Architecture overview

```text
LibreHardwareMonitor ─┐
                      ├── SensorDiscoveryService
Windows Native APIs ──┘             │
                                    ▼
                         HardwareMonitoringService
                                    │
                         MonitoringSnapshot event
                                    │
                     ┌──────────────┴──────────────┐
                     ▼                             ▼
             WidgetViewModel              Detailed monitor
                     │
                     ▼
               WidgetWindow
```

Application-wide services are created and coordinated by `App.xaml.cs`.

The application separates hardware polling, immutable monitoring snapshots, UI presentation, settings, and Windows integration.

## Application startup

`App.xaml.cs` is the composition root.

Startup performs the following operations:

1. Acquires the single-instance mutex.
2. Loads the JSON settings file.
3. Initializes logging and theme services.
4. Initializes hardware monitoring.
5. Creates the widget and tray icon.
6. Subscribes to display, power, DPI, and Explorer events.
7. Starts asynchronous sensor polling.

Shutdown cancels the application cancellation token, stops monitoring, removes event handlers, disposes the tray icon, closes hardware resources, and releases the single-instance handles.

## Models

The `Models` directory contains application data and configuration types.

Important models include:

- `AppSettings`: persisted user preferences
- `ThresholdSettings`: warning and critical thresholds
- `SensorReading`: normalized sensor information
- `HardwareDevice`: discovered hardware metadata
- `MonitoringSnapshot`: immutable values produced by each polling cycle

Settings models are kept independent from WPF controls so they can be serialized and tested without starting the UI.

## Hardware monitoring

### HardwareMonitoringService

`HardwareMonitoringService` implements `IHardwareMonitoringService`.

Responsibilities include:

- Opening LibreHardwareMonitor
- Discovering sensors once during initialization
- Reusing selected sensor references
- Polling sensors asynchronously
- Catching errors independently for each update cycle
- Publishing immutable snapshots
- Rescanning hardware when requested
- Reconnecting sensors after power resume
- Closing LibreHardwareMonitor during shutdown

The default polling interval is one second.

Supported intervals are:

- 500 milliseconds
- 1 second
- 2 seconds
- 5 seconds

Polling is performed outside the UI thread. UI changes are dispatched separately through the view model.

### CPU usage and memory fallbacks

Chipmunk uses LibreHardwareMonitor where possible.

Windows native APIs provide fallback information for:

- Total CPU usage
- Total and available system memory

CPU usage is calculated using Windows system-time deltas. System memory is read using `GlobalMemoryStatusEx`.

## Sensor discovery

`SensorDiscoveryService` selects sensors by sensor type and normalized candidate names instead of relying on one hard-coded sensor name.

### CPU temperature priority

1. CPU Package
2. CPU Tctl/Tdie
3. Core Average
4. Average of available CPU core temperature sensors

### CPU usage priority

1. Total CPU load
2. Windows native CPU usage fallback

### GPU temperature priority

1. GPU Core
2. Representative GPU temperature
3. A non-hotspot, non-memory, non-VRM temperature sensor

### GPU usage priority

1. GPU Core
2. GPU Utilization
3. GPU 3D

When multiple GPUs are present, Chipmunk uses the explicitly selected GPU or the GPU with the highest current usage.

Sensor discovery results are written to the debug log.

## PawnIO integration

PawnIO is an optional low-level hardware-access driver used when LibreHardwareMonitor cannot read certain CPU sensors.

The application follows these rules:

- PawnIO is never installed silently.
- The prompt is shown only after repeated missing CPU temperature samples.
- The user must explicitly select **Install**.
- The bundled installer SHA-256 is verified before execution.
- Windows UAC is requested through the `runas` verb.
- The unrestricted PawnIO edition is not used.
- Declining or cancelling installation does not terminate Chipmunk.

The official installer is stored at:

```text
installer\dependencies\PawnIO_setup.exe
```

To download and verify the pinned installer again:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\fetch-pawnio.ps1
```

The script verifies both SHA-256 and the Authenticode signer.

## MVVM implementation

### WidgetViewModel

`WidgetViewModel` subscribes to monitoring snapshots and exposes formatted properties for the widget.

It is responsible for:

- CPU and GPU display strings
- Memory formatting
- Celsius and Fahrenheit conversion
- Decimal-place formatting
- Severity classification
- Tooltip text
- One-line and two-line presentation data

### SettingsViewModel

`SettingsViewModel` works with a cloned settings object so changes can be cancelled without modifying the active configuration.

When settings are saved:

1. Values are validated.
2. The JSON file is updated.
3. The settings service raises a change notification.
4. Monitoring and UI services apply the new configuration.

Business logic remains outside the settings window code-behind.

## Views

### WidgetWindow

The widget is a borderless transparent WPF window.

It supports:

- Always-on-top behavior
- Rounded corners
- Transparency
- Drag movement
- Click-through mode
- Per-monitor DPI
- Dynamic taskbar positioning
- Hidden taskbar button
- Compact one-line and two-line layouts

### SettingsWindow

Provides configuration for:

- Sensor visibility
- GPU selection
- Update interval
- Warning thresholds
- Font size
- Background opacity
- Layout
- Startup registration
- Always-on-top behavior
- Click-through mode
- Theme
- Monitor selection
- Taskbar margin
- Decimal places
- Temperature unit

### DetailedMonitorWindow

Displays a larger view of the current monitoring snapshot.

### PawnIoConsentWindow

Explains why the optional driver may be needed and collects explicit user consent.

## Windows integration

### WindowPositionService

Uses monitor work areas and per-monitor DPI to position the widget above the Windows taskbar.

It handles:

- Horizontal and vertical taskbars
- Auto-hidden taskbars
- Multiple monitors
- Negative monitor coordinates
- DPI changes
- Resolution changes
- Monitor connection and removal
- Explorer and taskbar recreation

### Click-through mode

Click-through mode adds the `WS_EX_TRANSPARENT` extended window style so mouse input reaches the window below the widget.

The mode can be disabled from the system tray menu.

### SingleInstanceService

Uses a named mutex to prevent duplicate instances.

A named activation event allows a second launch attempt to notify the existing instance.

### StartupService

Registers the current executable under:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

No administrator privileges are required.

### TrayIconService

Uses Windows Forms `NotifyIcon` because WPF does not provide a built-in system tray component.

The tray icon is extracted from the Chipmunk executable so it matches the application, taskbar, shortcuts, and installer branding.

## Settings and logging

Settings:

```text
%LocalAppData%\Chipmunk\settings.json
```

Logs:

```text
%LocalAppData%\Chipmunk\Logs
```

`SettingsService` restores defaults if JSON loading or deserialization fails.

`RateLimitedFileLogger` suppresses repeated identical errors and rotates the log when it exceeds the configured size limit.

Release logging avoids unnecessarily detailed hardware-identifying information.

## Themes and severity colors

`ThemeService` applies light, dark, or Windows system theme resources.

Severity is calculated independently for:

- CPU temperature
- GPU temperature
- CPU usage
- GPU usage
- RAM usage

Continuous blinking is intentionally avoided.

## Branding assets

Branding assets are stored in:

```text
src\Chipmunk\Assets
```

Files:

- `chipmunk_logo.png`: full logo with product name
- `chipmunk_logo_2.png`: icon-only source image
- `chipmunk.ico`: multi-resolution Windows application icon

The ICO file contains:

```text
16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel images
```

The icon is embedded into the application executable and used by WPF windows, the tray icon, Task Manager, shortcuts, and the Inno Setup installer.

## Testing

The tests use mock hardware services so most behavior can be verified without physical sensors.

Covered areas include:

- Sensor priority selection
- CPU core temperature averaging
- Null sensor values
- Temperature and usage severity
- Memory unit conversion
- Settings serialization and recovery
- Widget position calculation
- Multi-GPU selection
- Monitoring service lifecycle
- Settings propagation
- Display changes
- Power-resume reconnection
- PawnIO prompt policy
- PawnIO installer hash validation

Run all tests with:

```powershell
dotnet test .\Chipmunk.sln `
  -c Release `
  -p:Platform=x64
```

## Publishing

### Portable and single-file builds

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\publish.ps1 `
  -Mode All
```

Output:

```text
artifacts\portable
artifacts\single-file
```

Both profiles are self-contained x64 deployments.

Trimming is disabled because WPF and hardware-monitoring dependencies are not guaranteed to be trimming-safe.

The single-file build may extract native libraries at runtime.

The `Dependencies` directory and license notices remain external and must be distributed with the executable.

## Building the installer

Install Inno Setup 6.4 or later, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\build-installer.ps1
```

Output:

```text
artifacts\installer\Chipmunk-Setup-x64.exe
```

The installer:

- Installs Chipmunk under the current user's Local AppData
- Does not require UAC for the application itself
- Can create startup and desktop shortcuts
- Offers PawnIO as an unchecked optional task
- Requests a separate UAC confirmation only if PawnIO is selected
- Uses the Chipmunk application icon

## Runtime resource cleanup

Normal application shutdown disposes:

- LibreHardwareMonitor resources
- Hardware sensor references
- Cancellation tokens
- Tray icon resources
- Native event handles
- Single-instance synchronization objects
- Display and power event subscriptions

New long-lived services should implement `IDisposable` or provide an asynchronous stop method.

## Adding a new sensor

When adding a new sensor:

1. Add the normalized value to `MonitoringSnapshot`.
2. Extend discovery using both `SensorType` and candidate names.
3. Store and reuse the selected sensor reference.
4. Add formatting to the appropriate view model.
5. Handle missing values as `null`.
6. Add unit tests using the mock sensor provider.
7. Avoid performing sensor discovery on every polling cycle.

## Troubleshooting development builds

### `dotnet` is not found

Install the .NET 8 SDK or add `dotnet.exe` to `PATH`.

### CPU temperature is unavailable

This may be expected without PawnIO. Verify sensor discovery logs and test with the latest supported LibreHardwareMonitor version.

### The single-file output cannot be overwritten

Exit any running `Chipmunk.exe` process before publishing again.

### Inno Setup cannot be found

Install Inno Setup 6.4 or later and reopen the terminal.

### The PawnIO hash check fails

Run `scripts\fetch-pawnio.ps1` to download and verify the pinned official installer again.

## Contribution guidelines

Before submitting a change:

1. Build the Release configuration.
2. Run all tests.
3. Verify the widget at 100%, 125%, and 150% DPI if UI code changed.
4. Test at least one light and one dark theme.
5. Test widget positioning if Win32 or monitor code changed.
6. Confirm that all unavailable values degrade to `N/A`.
7. Update both README files when behavior or build instructions change.

Do not add telemetry or external data transmission without clearly documenting and reviewing the change.

## License

See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

> *This README-DEV.md is written by GPT-5.6, thereby the document could contain wrong information. Please modify and share the wrong information in this README-DEV.md file.*