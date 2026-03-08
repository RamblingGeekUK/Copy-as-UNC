# Copy as UNC — PowerToys Contribution Guide

Reference for contributing this utility to [microsoft/PowerToys](https://github.com/microsoft/PowerToys).
Related issue: [#35507](https://github.com/microsoft/PowerToys/issues/35507)

---

## What You Need to Build

Based on how PowerToys structures its context menu utilities (FileLocksmith, ImageResizer, PowerRename), the "Copy as UNC" utility needs these components:

### Component Overview

```
src/modules/CopyAsUNC/
├── CopyAsUNCContextMenu/    ← C++ DLL (the shell extension, runs in Explorer)
├── CopyAsUNCLib/            ← C++ core logic (path resolution)
└── (no separate UI needed)  ← Unlike FileLocksmith, you just write to clipboard
```

Plus integration points in the runner, settings UI, and installer — but those follow a fixed pattern that can be copied from an existing utility.

---

## Good News: You Can Simplify the Core Logic

The existing C# proof-of-concept uses WMI (`System.Management`). In C++ you can use `WNetGetUniversalName` — a single Win32 API call that does the same thing with no WMI overhead:

```cpp
DWORD bufSize = MAX_PATH * 2;
std::vector<BYTE> buf(bufSize);
auto info = reinterpret_cast<UNIVERSAL_NAME_INFO*>(buf.data());
if (WNetGetUniversalName(path, UNIVERSAL_NAME_INFO_LEVEL, info, &bufSize) == NO_ERROR) {
    // info->lpUniversalName is your UNC path
}
```

This replaces all the WMI boilerplate.

---

## Step-by-Step Roadmap

### Step 1 — Read the Official Guide First
`doc/devdocs/development/new-powertoy.md` in the PowerToys repo is the authoritative checklist. Read it before touching any code.

### Step 2 — Set Up Your Dev Environment
- Visual Studio 2022 with C++ and C# workloads
- Fork `microsoft/PowerToys` and clone it
- Build the full solution once before making changes (takes a while the first time)
- Set the `runner` project as startup project for debugging

### Step 3 — Build the C++ Context Menu DLL
This is the hardest part. Use `FileLocksmith`'s `FileLocksmithContextMenu` as your reference — it's the simplest of the three. Key things to implement:

| COM Interface | Purpose |
|---|---|
| `IExplorerCommand` | The context menu item itself |
| `IObjectWithSite` | Required companion interface |

In `Invoke()`: call `WNetGetUniversalName`, then `OpenClipboard`/`SetClipboardData`. No separate UI process needed since there's nothing to display.

In `GetState()`: return `ECS_ENABLED` only when the selected path is on a mapped network drive (check `GetDriveType()` == `DRIVE_REMOTE`).

### Step 4 — Generate a CLSID
You need a unique GUID. Generate one in Visual Studio: **Tools → Create GUID**. This goes into:
- The `.vcxproj`
- `AppxManifest.xml`
- The runner registration

### Step 5 — Settings Page (C# WinUI 3)
Minimal: just an enable toggle, plus an option for "show in extended context menu" (the Windows 11 "Show more options" submenu). Copy `FileLocksmithPage.xaml` as a template — it has exactly these two controls.

### Step 6 — Register with the Runner
You need to touch these files in `src/runner/`:
- `modules.h`
- `modules.cpp`
- `settings_window.h/.cpp`
- `main.cpp`

The new-powertoy guide specifies exactly what to add in each.

### Step 7 — WiX Installer
Create `CopyAsUNC.wxs` in `installer/PowerToysSetupVNext/`. PowerRename's `.wxs` is the simplest reference — yours will be even smaller since there's no UI executable to ship.

### Step 8 — AppxManifest Registration
The context menu DLL needs to be registered via MSIX. Look at `FileLocksmithContextMenu`'s `AppxManifest.xml` for the exact XML structure — you're just swapping the CLSID and names.

---

## Suggested Order to Tackle This

1. **Get the core logic working in C++** — write a standalone test console app that calls `WNetGetUniversalName` on a mapped path and copies to clipboard. Validate it works before touching PowerToys at all.
2. **Build the context menu DLL** using FileLocksmith as a template.
3. **Wire up settings** (minimal — just the enable toggle).
4. **Runner integration** (mechanical, follows the guide exactly).
5. **Installer** (last, and fairly mechanical).

---

## Honest Assessment of Complexity

| Part | Difficulty | Notes |
|---|---|---|
| Core logic (C++) | Low | `WNetGetUniversalName` is trivial |
| Context menu DLL (C++ COM) | **Medium-High** | COM boilerplate is the main hurdle |
| Settings page (C# WinUI 3) | Low | Copy/paste from FileLocksmith |
| Runner integration | Low | Mechanical, well-documented |
| Installer | Low | Mechanical |

The C++ COM shell extension is where most contributors get stuck. The code itself isn't complex — it's the COM ceremony (class factory, DLL exports, registration, WRL templates) that's unfamiliar. FileLocksmith's context menu DLL is your best template to work from.

---

## PowerToys Architecture Reference

### How Context Menu Utilities Work

Each utility is structured as:
- A **C++ shell extension DLL** (`IExplorerCommand` + `IObjectWithSite`) that runs inside Explorer
- An optional **C# WinUI 3 UI executable** launched via named pipe IPC (not needed for Copy as UNC)
- A **C# WinUI 3 settings page** in the PowerToys Settings app
- Settings stored at `%LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json`

### Settings Communication Flow

```
User Changes Settings (UI)
  ↓
Settings.UI serializes to JSON
  ↓
Settings transmitted to PowerToys.exe (Runner) via Named Pipe IPC
  ↓
Runner invokes set_config() on module DLL
  ↓
Module parses JSON and applies settings
```

### COM Registration

Context menu DLLs are registered via MSIX (`AppxManifest.xml`), not raw registry writes. The installer handles this.

**CLSID examples from existing utilities:**
- FileLocksmith: `AAF1E27D-4976-49C2-8895-AAFA743C0A7E`
- PowerRename: `1861E28B-A1F0-4EF4-A1FE-4C8CA88E2174`
- ImageResizer: `8F491918-259F-451A-950F-8C3EBF4864AF`

---

## Reference Files to Study in the PowerToys Repo

| File | Purpose |
|---|---|
| `src/modules/FileLocksmith/FileLocksmithContextMenu/` | Primary template for the context menu DLL |
| `src/modules/FileLocksmith/FileLocksmithLib/Settings.h` | Settings pattern |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/FileLocksmithPage.xaml` | Settings UI template |
| `installer/PowerToysSetupVNext/FileLocksmith.wxs` | Installer template |
| `doc/devdocs/development/new-powertoy.md` | The official 8-step checklist |

---

## Contribution Process

1. Comment on issue [#35507](https://github.com/microsoft/PowerToys/issues/35507) that you are actively working on a PR
2. Fork `microsoft/PowerToys`
3. Work on a feature branch (e.g. `feature/copy-as-unc`)
4. Follow the checklist in `doc/devdocs/development/new-powertoy.md`
5. Submit a PR referencing the issue
6. Microsoft team member Clint Rutkas has already expressed enthusiasm — tag them in the PR
