# KeyboardOG — Key Mapping & Parameter Editor for Hall-Effect Keyboards

[中文](README.md) | **English**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6?logo=windows&logoColor=white)](#requirements)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.6.1-512BD4?logo=dotnet&logoColor=white)](#requirements)
[![C# WinForms](https://img.shields.io/badge/C%23-WinForms-239120?logo=csharp&logoColor=white)](#build-from-source)
[![UI Language](https://img.shields.io/badge/UI-%E4%B8%AD%E6%96%87%20%7C%20English-2ea44f)](#chinese--english-ui)
[![Version](https://img.shields.io/badge/version-1.01-blue)](#firmware-version-check)

A Windows host application that talks to the keyboard firmware over a **USB HID command
interface**. It edits key mappings, tunes hall-effect parameters, triggers calibration and
shows the result of every command it sends.

No driver, no serial port, and it does not steal the keyboard's normal input:

```
Host app ──(HID Report ID 4, 8-byte commands)──> keyboard firmware
         <─(ACK / FIN / query results / device info)─
```

Matching firmware: the hall-effect version (`rtkb-layout-edited`) and the six-key low-profile
version (`6kb`). Both share the same Report ID 4 command interface.

## Screenshots

Main window (Chinese UI):

![Main window (zh)](docs/images/main-window-zh.png)

Main window (English UI):

![Main window (en)](docs/images/main-window-en.png)

Settings dialog (language can be switched live):

![Settings](docs/images/settings-zh.png)

---

## Features

### Connecting and identifying the device

- **Only real command interfaces are listed.** The app enumerates HID devices, keeps those
  exposing the `UsagePage=0xFF00 / Usage=0x01` vendor collection, then **probes each candidate**
  with a device-info query. Only devices that actually answer are shown, so touchpads that
  happen to expose a similar collection are filtered out.
- **Automatic sync after connecting**: device info first (key count, hall sensor, encoder count),
  then every mapping and every hall parameter — what you see is what the keyboard holds.
- **Disconnect on unplug**: the app listens for device-tree changes and pings the connection;
  two consecutive failures are required before it disconnects, so a temporarily busy firmware
  is not mistaken for an unplugged keyboard.

### Key mappings

- Four visual picker tabs: **Keyboard / Mouse / Gamepad / Consumer**, click a key/pad directly.
- Modifier keys (Ctrl / Shift / Alt / Win) are converted automatically into the modifier byte
  of the HID report.
- Up to 12 mappings per key (matching the firmware's `MAXIMUM_BINDABLE_KEY`), with move up/down,
  delete and clear-key actions.
- Key buttons are colour-coded: **green = in sync with the keyboard, red = local changes not written yet**.
- One consistent mouse gesture across all four pickers:

  | Action | Result |
  |---|---|
  | Left-click an **existing** mapping | Removes that single entry ("click again to cancel") |
  | Left-click a **new** mapping | Replaces the currently selected row; appends when nothing is selected |
  | Right-click / Shift+Left-click a **new** mapping | Appends a row |

### Writing: as few commands as possible

Before writing, the app diffs "what the keyboard currently holds" against the local list and
picks the cheapest strategy:

| Situation | Commands sent |
|---|---|
| Local list identical to the keyboard | **None** — the status bar just says there is nothing to send |
| Only appends at the end | One `MAP_DO_APPEND` per new mapping |
| The key is cleared locally | A single `MAP_DO_CLEAR` (Byte6 = 0, clears the whole key) |
| A few entries in the middle changed | `MAP_DO_OVERRIDE` for the changed slots only |
| Local list shorter than the keyboard's | Overrides for changed slots + one `CLEAR` from the new length |
| Never synced (device state unknown) | Falls back to a full rewrite to stay correct |

Example: with 12 mappings on the keyboard and one changed in the middle, **1 command** is sent
(the old implementation sent 12).

After a successful write a `CMD_REFRESH` follows automatically, so **no replugging is needed**.

### Hall-effect parameters and calibration

- Four parameters: RT trigger, RT release, upper deadzone, lower deadzone. Each has a vertical
  slider and a numeric box, kept in sync both ways.
- **Per-key pending edits are preserved**: switch to another key and back and your edited values
  are still there (tinted red), while the real value on the keyboard is always shown next to it.
  Edits are discarded only on a successful write or after "re-read parameters".
- Batch scope supports **single / multi / select-all**, and success is judged per key: a key turns
  green only when all four parameters were acknowledged.
- Calibration: single key or all keys. It is a blocking firmware operation, and the UI updates
  as the firmware reports each finished key.

### Command log

- A separate, non-blocking log window that never steals focus.
- Records name, detail, raw 8 bytes and result (pending / sent / done / timeout / failed) per
  command, with a footer counting sent / acked / done / timeout / failed.
- CSV export (saved to Documents by default; read-only targets are handled, unwritable folders
  fall back with a message).

### Chinese / English UI

- Switch between 中文 and English from **Settings/config**, effective immediately, no restart.
- The choice is stored in `KeyboardOG.settings.ini` next to the executable.
- A self-check script keeps new UI strings from missing translations (see [Development](#ui-translation-and-self-check)).

### Firmware version check

- The last byte of the device-info frame carries the firmware version, packed as **2 | 3 | 3** bits:
  the first 2 bits are the integer part, the next 3 bits the first decimal, the last 3 bits the
  second decimal (e.g. `0b01_011_001` = `0x59` → **1.31**, `0x41` → **1.01**).
- The app keeps a "software version ↔ supported firmware range" table and evaluates it right
  after connecting:
  - supported: `固件 1.01 / 软件 1.01` (firmware / software version);
  - firmware too old: red `固件版本过老，请更新固件`;
  - firmware too new: red `固件版本过新，请更新软件`;
  - plus `版本不匹配可能导致意料之外的问题`.
- Software `1.01` currently supports firmware **1.00 – 1.57** (see `Protocol/VersionInfo.cs`).

### Other details

- The transport is single-threaded and serialized; the UI thread never performs blocking I/O,
  so a timeout never freezes the window.
- After a timeout or a mismatched reply the app resynchronizes: only ACK/FIN frames whose
  sequence matches are accepted, and the input buffer is drained when needed.
- Fixes "blank controls on the first launch after moving the app": the window schedules extra
  repaints at 120 / 400 / 1200 ms after it is shown.

---

## Quick start

### 1. Connect

1. Click **Refresh** and pick your keyboard in the **Device** combo box (only devices that
   answered the device-info probe are listed).
2. Click **Connect**. Device info, all mappings and all hall parameters are read automatically.
3. If the status bar says no device info was received, the firmware is old or not a matching
   build — see the [FAQ](#faq).

### 2. Remap a key

1. Select a key number on the left (for example `0`).
2. In the picker tabs on the right, click the key you want: **left-click replaces / right-click appends**;
   clicking an existing mapping again removes it.
3. Click **"Write this key"** on the far right. Only this key is touched.

### 3. Tune hall parameters

1. Choose the target keys in the scope selector (left-click single, right-click multi, or select all).
2. Drag a slider or type a value (values are per-mille of full travel, `40` = 4.0%).
3. Click **Send all four**. A key turns green only when all four parameters were acknowledged.

---

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 / 11 (x64) |
| Runtime | .NET Framework 4.6.1 or newer |
| Privileges | A normal user is enough; run as administrator if another program holds the device exclusively |
| Hardware | A keyboard with matching firmware (Report ID 4 command interface + device-info frame) |

> ⚠️ The process must run as **x64**: the native `HIDComm.dll` is 64-bit and the project sets
> `<Prefer32Bit>false</Prefer32Bit>`. Do not change that.

---

## Build from source

### 1. Native library (required once)

```powershell
cd KeyboardOG.Native
powershell -ExecutionPolicy Bypass -File build.ps1
# produces bin\HIDComm.dll (x64); the project copies it to bin\Debug|Release
```

### 2. WinForms project

Open `KeyboardOG.sln` in Visual Studio, or from the command line:

```powershell
& "D:\Microsoft VS\MSBuild\Current\Bin\MSBuild.exe" `
  KeyboardOG\KeyboardOG.csproj /t:Build /p:Configuration=Release
```

> If a command-line build fails with `MSB4018 / X509Store access denied`, it is the ClickOnce
> manifest signing looking for a certificate; add `/p:SignManifests=false`. It does not affect
> the Authenticode signature applied below.

### 3. Build and sign in one step

```bat
sign.cmd            :: build Release -> sign -> verify
sign.cmd -NoBuild   :: sign only (use what Visual Studio built)
```

Every rebuild drops the previous Authenticode signature, so run `sign.cmd` before publishing.
A self-signed certificate removes "Unknown publisher" but not SmartScreen reputation —
see [signing/README.md](signing/README.md).

---

## Repository layout

```
KeyboardOG/
├─ Form1.cs / Form1.Designer.cs   main window: device, key list, mapping list, pickers, action bar
├─ Keyboard.cs                    keyboard picker
├─ MouseControl.cs                mouse picker
├─ GamepadControl.cs              gamepad picker
├─ ConsumerControl.cs             media/consumer picker
├─ HallParamControl.cs            hall parameters + calibration
├─ LogForm.cs / CommandLogView.cs command log window and view
├─ PickerSupport.cs               shared picker interface, events, palette and drawing helpers
├─ SettingsForm.cs                settings dialog (including language)
├─ Language.cs                    zh/en text table and runtime translation
├─ Protocol/
│  ├─ HidNative.cs                P/Invoke declarations for HIDComm.dll
│  ├─ HidTransport.cs             serialized HID transport (queue + async wrapper)
│  ├─ ProtocolDefs.cs             protocol constants, 8-byte command builder, name tables
│  ├─ KeyMapModel.cs              mapping list model
│  ├─ MappingWriter.cs            minimal-write planner (APPEND / OVERRIDE / CLEAR)
│  ├─ VersionInfo.cs              firmware/software versions and compatibility table
│  ├─ CommandLog.cs               log entries and store
│  └─ AppSettings.cs              settings persistence (ini)
├─ signing/                       code-signing scripts and notes
├─ tools/Check-Localization.ps1   UI translation self-check
└─ docs/                          screenshots and technical notes
```

---

## Protocol at a glance

Commands and replies all use **Report ID 4** with 8 data bytes. The complete byte tables,
ACK/FIN matching rules, offset ranges, EEPROM layout and firmware pitfalls live in
[**docs/technical-notes.md**](docs/technical-notes.md).

Three things to know up front:

1. **One command produces two frames**: ACK then FIN. Reading only one leaves the FIN to be
   mistaken for the next command's reply ("half of the mappings get lost").
2. **The device-info frame has no FIN**: treat "received a `0xFF` frame" as the end condition.
3. **The firmware drops duplicate commands**: each command carries an incrementing sequence
   number in byte 8.

---

## FAQ

**The device list is empty / my keyboard is not listed**

- The firmware must implement the command interface (`UsagePage=0xFF00 / Usage=0x01`) and the
  device-info frame; older firmware does not answer the probe and will not be listed.
- Make sure no other program (serial monitor, HID test tool) holds the device.
- Try running as administrator.

**Commands time out after connecting / "no FIN received"**

- Raise **Response timeout** in Settings to 1000–2000 ms. If the firmware is built with `DEBUG`,
  `Serial.print` can block for hundreds of milliseconds without a serial monitor; disable `DEBUG`
  in release firmware.
- Do not keep the Arduino serial monitor open.

**The write succeeded but the keyboard did not change**

- Current firmware reloads its configuration on `CMD_REFRESH`, so no replug is needed;
  with older firmware, replug the keyboard.

**A media key fires many times per press**

- The firmware was re-uploading the same consumer report continuously. Keyboard/mouse bits are
  state-based (repeating them is harmless), but Windows dispatches one action per report for the
  consumer page. Fixing it requires "upload only on change" in the firmware; it is unrelated to
  the host app.

**"Unknown publisher" / "Windows protected your PC"**

- The former is solved by installing the certificate (`signing\Install-Trust.ps1`); the latter is
  SmartScreen reputation, which a self-signed certificate cannot fix.

**Controls are blank the first time the app is started from another folder**

- Fixed by scheduling extra repaints after the window is shown. If it still happens, please report
  the steps to reproduce.

---

## UI translation and self-check

All UI text lives in `Language.cs`: Chinese literals stay in the source, and `L.Apply(control)` /
`L.T()` translate at runtime (the table works both ways, so zh↔en can be switched repeatedly).

After adding Chinese UI text, run the self-check to list anything without an English mapping:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Check-Localization.ps1
```

- Exit code `0` = everything translated, `1` = something is missing (list written to
  `tools/localization-report.txt`)
- Chinese inside trailing comments is ignored; literals starting with `#` (ini comments) are skipped
- `L.T("中文", "English")` / `L.F("中文", "English", ...)` carry their English inline and need no table entry

---

## Related projects

| Project | Description |
|---|---|
| `rtkb-layout-edited` | Hall-effect keyboard firmware (mapping, hall parameters, calibration, EC11 encoder) |
| `6kb` | Six-key low-profile keyboard firmware |
| `KeyboardOG.Native` | Native HID library (enumerate, select, send/read reports) |

---

## Acknowledgements

- The native HID library is derived from `HIDComm` in `WindowsGUIIncludes`; the only change is
  waiting for `CancelIo` to complete before returning from the read timeout path.
- Thanks to everyone who tested mappings, parameters, calibration and the firmware version
  protocol on real hardware.

> No license is included yet. If you want to publish this under MIT / Apache-2.0, add a
> `LICENSE` file at the repository root.
