# Smart Sleep Scheduler (Windows)

A clean, compact, modern Windows Smart Sleep Scheduler built with **C# and .NET 10** using **WPF**, featuring a Fluent translucent design (Windows 11 Mica & Windows 10 Acrylic), accurate non-drifting timing, safe Windows power transitions, and a Single Source of Truth (SSOT) architecture.

---

## ✨ Features

- **Fluent Translucent Design**: Native Windows 11 Mica and Windows 10 Acrylic backdrop support with smooth dark, light, and Windows system auto theme detection (Dark mode by default).
- **Safe by Default**: Requests Windows Sleep (S3 / Modern Standby) safely via native Windows power APIs without killing running apps or forcing critical suspension.
- **Advanced Power Actions**: Advanced users can opt into Hibernate, Graceful Shutdown, Graceful Restart, or Super Force Shutdown / Restart via the settings panel with safety warnings.
- **Accurate Timing**: Calculates remaining time against absolute UTC timestamps, ensuring zero timer drift even when minimized or throttled by Windows.
- **Auto-Start on Launch**: Automatically starts a 1-minute countdown on application launch (configurable in preferences).
- **Visual Urgency Warning**: Displays a subtle pulsing red glow in the final 30 seconds before sleep is initiated.
- **Always on Top & Fullscreen**: Handy top-bar pin toggle (active by default) and full-screen mode (<kbd>F11</kbd>).
- **Single Instance Guard**: Prevents multiple competing sleep schedules from running simultaneously.
- **Zero Runtime Dependencies**: Published as a self-contained single-file executable that runs without requiring a pre-installed .NET runtime.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| <kbd>Esc</kbd> | Immediately stop timer / Close settings / Exit fullscreen |
| <kbd>Space</kbd> | Pause or Resume countdown |
| <kbd>F11</kbd> | Toggle Fullscreen mode |

---

## 🏗️ Architecture & Single Source of Truth (SSOT)

All project metadata, versioning, default parameters, and threshold limits are managed in a single central JSON file:

📁 **`src/Smart-Sleep-Scheduler-Win/Config/DevConfig.json`**

```json
{
  "App": {
    "Name": "SmartSleepScheduler",
    "DisplayName": "Smart Sleep Scheduler",
    "Version": "0.9.0.1",
    "Vendor": "SmartSleep",
    "AppId": "9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B"
  },
  "Defaults": {
    "Theme": "Dark",
    "AlwaysOnTop": true,
    "DefaultTimerDurationSeconds": 180,
    "AutoStartOnLaunch": true,
    "AutoStartDurationSeconds": 60,
    "DefaultPowerAction": "Sleep",
    "MinTimerSeconds": 1,
    "MaxTimerSeconds": 43200,
    "UrgencyWarningThresholdSeconds": 30
  }
}
```

Updating `DevConfig.json` automatically updates:
1. The C# runtime (`AppConfig.Instance`) and UI About section.
2. The Inno Setup installer script (`installer.iss`).
3. The build script (`build.ps1`) and output artifact naming.

---

## 📦 Distribution & Release Artifacts

The automated pipeline produces 4 release targets:

| Artifact | Type | Description |
| :--- | :--- | :--- |
| `SmartSleepScheduler-v0.9.0.1-win-x64-Setup.exe` | Installer | Self-contained Inno Setup installer (No .NET required) |
| `SmartSleepScheduler-v0.9.0.1-win-x64-Portable.zip` | Portable | Self-contained single-file portable archive |
| `SmartSleepScheduler-v0.9.0.1-win-x64-Setup-ReqDotNet10.exe` | Installer | Lightweight installer (Requires .NET 10 runtime) |
| `SmartSleepScheduler-v0.9.0.1-win-x64-Portable-ReqDotNet10.zip` | Portable | Ultra-compact single-file portable archive (Requires .NET 10) |

---

## 🚀 Building Locally

Run the release pipeline directly using PowerShell:

```powershell
# Build using version from DevConfig.json:
.\build.ps1

# Or specify a version tag:
.\build.ps1 -VersionTag "v0.9.0.1"
```

All compiled packages will be placed into the `artifacts/` directory with generated SHA256 checksums.

---

## 🤖 GitHub Actions CI/CD

Pushing any git tag matching `v*` (e.g. `git tag v0.9.0.1 && git push origin v0.9.0.1`) automatically triggers `.github/workflows/release.yml`, building all 4 binaries and creating a GitHub Release.

---

## 📄 License

MIT License. Copyright © 2026 SmartSleep.
