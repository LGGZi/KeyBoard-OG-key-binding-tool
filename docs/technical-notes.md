# Technical notes — protocol, architecture and firmware pitfalls

This document describes how the host application talks to the keyboard, what the firmware does
with each command, and the traps discovered while building it. It is the reference behind
[README](../README.md); the earlier Chinese development log is preserved in git history.

Contents

1. [Native library and patched behaviour](#1-native-library-and-patched-behaviour)
2. [Transport architecture](#2-transport-architecture)
3. [Protocol reference](#3-protocol-reference)
4. [Device byte, offsets and parameters](#4-device-byte-offsets-and-parameters)
5. [Minimal-write planner](#5-minimal-write-planner)
6. [Firmware pitfalls](#6-firmware-pitfalls)
7. [EEPROM layout](#7-eeprom-layout)
8. [What has been verified](#8-what-has-been-verified)
9. [UI translation and self-check](#9-ui-translation-and-self-check)

---

## 1. Native library and patched behaviour

`HIDComm.dll` keeps **global state** (the selected device handle, the enumerated device table and
an error buffer) and `Hid_ReadResponse` is a blocking call. The managed side therefore funnels
every native call through a single worker thread (see §2).

The copy under `KeyboardOG.Native/src` is taken from `WindowsGUIIncludes` and carries **one**
change, marked as a host-side patch in the source:

```cpp
CancelIo(g_hSelectedDevice);
DWORD cancelled = 0;
GetOverlappedResult(g_hSelectedDevice, &ov, &cancelled, TRUE);   // wait for the cancel
CloseHandle(ov.hEvent);
```

`CancelIo` only *requests* cancellation; the original code closed the handle and returned while
the IRP could still write into the function's stack buffer. A keyboard that never sends
unsolicited reports means **every read ends in the timeout branch**, so that hazard was hit
constantly. Merge the change back by adding that one line to the timeout branch.

Build:

```powershell
cd KeyboardOG.Native
powershell -ExecutionPolicy Bypass -File build.ps1     # -DebugBuild / -Clean are available
```

---

## 2. Transport architecture

* One dedicated background thread consumes a `BlockingCollection<Action>` queue; all native calls
  run there, serialized.
* The UI thread only issues async requests (`await`) and receives events — it never blocks and
  never touches controls from the worker.
* Events return to the UI thread through a `SynchronizationContext` built on `Control.BeginInvoke`.
* Every command carries an incrementing sequence number in byte 8, so two identical commands are
  still distinguishable (the firmware drops byte-identical repeats, see §6).

Two timeouts matter:

| Setting | Default | Meaning |
|---|---|---|
| Send rate | 500 Hz | minimum gap between commands in a batch (2 ms) |
| Response timeout | **1000 ms** | how long to wait for ACK / FIN |

---

## 3. Protocol reference

Report ID `0x04`, 8 data bytes (Report ID not counted). Firmware entry point:
`HID.cpp::setup()` receives `SET_REPORT` → `HIDcmd[8]` → `HIDcmdResponseIfChanged()` →
`HIDcmdResponse()`.

| idx | SET_MAP (0x01) | SET_PARAM (0x02) | CALIBRATE (0x03) | QUERY (0x04) |
|----|----------------|------------------|------------------|--------------|
| [0] | CMD = 0x01 | CMD = 0x02 | CMD = 0x03 | CMD = 0x04 |
| [1] | device | parameter id | mode 0 = all / 1 = single | **query target 0 = mapping / 1 = parameter** |
| [2] | key 0..13 | key 0..13 | key 0..13 | 0 = all / 1 = single |
| [3] | offset | value high byte | — | key 0..13 |
| [4] | operation: 0 APPEND / 1 OVERRIDE / 2 CLEAR | value low byte | — | 0 |
| [5] | storage index (slot / clear range) | — | — | — |
| [6] | 0 | 0 | 0 | 0 |
| [7] | **sequence (host-incremented)** | sequence | sequence | sequence |

### SET_MAP operations

The firmware implements three distinct operations (`HIDcmdHandler.h`):

| Operation | Byte6 (`cmd_buf[5]`) | Effect |
|---|---|---|
| `MAP_DO_APPEND` (0x00) | unused | appends the mapping to the key's first empty slot |
| `MAP_DO_OVERRIDE` (0x01) | slot index | writes **only** that slot; the tail is left untouched |
| `MAP_DO_CLEAR` (0x02) | start slot | clears from that slot to the end of the key's area; `0` clears the whole key; `BUTTONCOUNT` clears **all** keys |

Older firmware cleared the tail inside `OVERRIDE`; that behaviour was removed, so the host now
sends an explicit `CLEAR` when the new list is shorter (see §5).

### Replies (all Report ID 4, 8 bytes)

| [0] | Meaning | Other bytes |
|---|---|---|
| `0x05` ACK | command received | [1] = acknowledged command, [2] = sequence |
| `0x06` FIN | command finished | [1] = finished command, [2] = key (calibration progress), [7] = sequence, rest echoed |
| `0x01` | query result · mapping | [1] = device, [2] = key, [3] = offset, [4] = OVERRIDE, [5] = slot |
| `0x02` | query result · parameter | [1] = parameter id, [2] = key, [3] = value high, [4] = value low |
| `0xFF` | **device info** (standalone frame, **no FIN**) | [1] = key count, [2] = hall sensor (0xF0 yes / 0x0F no), [3] = encoder count, [4] = device type (reserved), [5..6] reserved, [7] = **firmware version** |

### CMD_REFRESH (0x07)

| [0] | [1] | Effect |
|---|---|---|
| 0x07 | `REFRESH_MAPPING` (0x00) | firmware calls `keyCodeInit()`, mappings reloaded from EEPROM |
| 0x07 | `REFRESH_PARAM` (0x01) | firmware calls `keyParameterInit()`, hall parameters reloaded |

The host appends the matching refresh command after every write and at the end of a batch, so
**no replug is required**. Query commands never trigger it.

### One command, two frames — read both

The keyboard answers with ACK first and FIN when done. The host must consume both, otherwise the
stale FIN is mistaken for the next command's reply (symptom: "half of the mapping changes are lost").

Matching rules (hard-earned):

* **ACK** is `{ACK, command, sequence, 0…}` → compare `[1]` with the command and `[2]` with the sequence.
* **FIN** echoes the whole command with `[0]` replaced, so `[1]` is *not* the command code.
  Compare bytes `[1..7]` instead. During "calibrate all", `[2]` is replaced by the key number,
  so only `[1]` and `[7]` are compared.
* A frame that does not match is **not** treated as failure immediately: it may be a late reply to a
  previous (timed-out) command. The host keeps reading until the deadline and only reports a
  mismatch if nothing matched. A FIN only counts when it matches.
* After a timeout the input buffer is drained before the next command, so leftovers cannot poison it.

### Device info (QUERY with `[1] = 0xFF`)

* The first thing the app does after connecting is querying device info
  (`04 FF 00 00 00 00 00 seq`). The `0xFF` frame ends the exchange — the firmware sends no FIN
  for it, so the host treats "device-info frame received" as the end condition.
* On receipt the app:
  1. rebuilds the key buttons from `[1]` (clamped to `DeviceLimits.KeyCount`, with a warning);
  2. shows or hides the hall-parameter panel from `[2]`;
  3. stores `[3]` encoder count, `[4]` device type and the reserved bytes, and logs the raw frame;
  4. parses `[7]` as the firmware version (`2 | 3 | 3` bits) and evaluates compatibility.
* If no device info arrives (old firmware), the app keeps the local defaults and does not stall.

---

## 4. Device byte, offsets and parameters

### The two conflicting device numbering schemes

| Byte | Name in `HIDcmdHandler.h` | **Actually interpreted as** (`Universal.h` / `KeyManager.h`) | UI |
|---|---|---|---|
| 0x01 | MAP_TYPE_KEYBOARD | KEYBOARD → `report.keyboardKeys[]` | Keyboard |
| 0x02 | MAP_TYPE_MOUSE | **CONTROLLER → `report.xboxButtons`** | Gamepad |
| 0x03 | MAP_TYPE_GAMEPAD | **MOUSE → `report.mouseButtons`** | Mouse |
| 0x04 | MAP_TYPE_CONSUMER | MULTIMEDIA → `report.mediaButtons` | Consumer |

The device byte is stored in EEPROM as-is and interpreted by `keyOperation()` using the
`Universal.h` numbering, so **the right-hand column is what counts**. The host app sends the
effective values (gamepad = 2, mouse = 3); the `MAP_TYPE_*` macros in `HIDcmdHandler.h` are dead code.

### Offset ranges

| Device | Meaning of offset | Range |
|---|---|---|
| Keyboard · normal keys | HID Usage ID (Keyboard/Keypad page 0x07) | 4..103 → `keyboardKeys[0..12]` |
| Keyboard · modifiers | **must be converted**: `104 + (usage - 224)`, usage 224..231 | 104..111 → `keyboardKeys[13]` |
| Gamepad | bit index into `xboxButtons` | 0..15 |
| Mouse | bit index into `mouseButtons` | 0..2 (left / right / middle) |
| Consumer | bit index into `mediaButtons` | 0..5 (mute / vol- / vol+ / play-pause / next / prev) |

Why modifiers need conversion (the keyboard report is 14 bytes):

```
keyboardKeys[0..12] = 13 bytes of normal keys (usage 0..103, one bit per key)
keyboardKeys[13]    = 1 byte of modifiers (usage 0xE0..0xE7 → bit0..bit7)
```

`keyOperation()` does `keyboardKeys[offset/8] |= 1 << (offset%8)`. To land in `[13]` the offset must
be `13*8 + (usage-0xE0) = 104..111`. Sending 224..231 directly gets truncated to `0xC0..0xC7`,
`high` becomes 12 and the bit lands in the normal-key area. `BindingCatalog.ToWireOffset()` performs
the conversion, so the picker can expose Ctrl/Shift/Alt/Win directly.

### Parameter ids and values

`4 = RT_ACTIVE`, `6 = RT_DEACTIVE`, `8 = UPPER_DEADZONE`, `10 = LOWER_DEADZONE`.

Values are per-mille of full travel (`EEPROM value / 1000 × (max - min)`). The firmware accepts
only `10 < value < 980` (**11..979**) and silently ignores anything else.

---

## 5. Minimal-write planner

`Protocol/MappingWriter.cs` compares the last known device state with the desired list and emits
the cheapest command sequence:

| Situation | Commands |
|---|---|
| identical | none |
| desired = current + appended tail | one `APPEND` per new mapping |
| desired empty | one `CLEAR` with start slot 0 |
| otherwise | `OVERRIDE` for changed slots; if shorter, one `CLEAR` from the new length |
| device state unknown (never synced) | full rewrite (`OVERRIDE` every slot + tail `CLEAR`) |

The device state is refreshed after every sync, successful write and clear-all; a `null` entry
means "unknown" and forces the safe full rewrite.

---

## 6. Firmware pitfalls

1. **`HIDcmdResponseIfChanged()` drops duplicate commands.** It `memcmp`s the previous 8 bytes,
   so an identical command is ignored. The host writes an incrementing sequence into byte 8 to
   keep every command unique.

2. **`keyOperation()`'s `switch` must stay exhaustive.** If EEPROM ever contains a device value the
   firmware does not handle, `key_map_addr` would not advance and the device would hang in the
   `while` loop. The host does not offer axis/trigger mapping types (5..12); exposing them would
   require a `default: key_map_addr++; break;` in the firmware.

3. **`APPEND` / `OVERRIDE` write EEPROM only.** They do not update the running `key_map[][]`;
   only `keyCodeInit()` (boot, or `CLEAR`) reloads it. `CMD_REFRESH` is what makes changes take
   effect without replugging — old firmware without it needs a replug.

4. **`CALIBRATE` is blocking and needs a human.** `calibrateKey()` waits until the key's sample
   first drops below 260 and then rises above it (press, then release). `calibrateAll()` does that
   for every key, and the firmware main loop is busy meanwhile — other commands are not served.
   `HIDSendCalibFin()` reports each finished key.

5. **`CLEAR` now takes a range.** Byte 3 is the key, byte 6 is the start slot (`0` = whole key,
   `BUTTONCOUNT` = all keys). The old build ignored byte 6 and always cleared everything.

6. **Storage index = slot number within the key.** Mapping storage is partitioned per key
   (`KEYMAP_SIZE_PER_KEY = MAXIMUM_BINDABLE_KEY * 2` bytes, 2 bytes per mapping), so `OVERRIDE`
   touches only that key. Reading stops at the first empty slot, so never leave holes in the middle.

7. **Unwritten EEPROM reads as `0xFF`.** `device` can only be `KEYBOARD..MULTIMEDIA` (1..4), so the
   firmware treats anything outside that range as an empty slot (`keyMapSlotIsEmpty()`), covering
   0, 0xFF and stale data. All three read paths use it; the host also discards query results with
   an out-of-range device id. Without this, a fresh board would import garbage mappings and could
   hang old `keyOperation()`.

8. **Calibration values live in EEPROM.** `MAX_VALUE` / `MIN_VALUE` are written by calibration and
   are not editable by hand in the app.

9. **Media reports must be edge-triggered.** Consumer usages are dispatched per report by Windows,
   so re-uploading an unchanged report fires the action repeatedly. Keyboard/mouse bits are
   state-based and tolerate repeats; consumer reports must only be sent when the bit set changes.

10. **Do not leave `Serial.print` on the hot path.** With `DEBUG` enabled and no serial monitor,
    each print can block for hundreds of milliseconds, which breaks timing and causes host timeouts.

---

## 7. EEPROM layout

```
0                              (BUTTONCOUNT * PSOOSK - 1)     parameter area
KEYMAP_START_POSITION          ...                            mapping area
```

* `PSOOSK = 12` bytes per key of parameters; `KEYMAP_START_POSITION = BUTTONCOUNT * PSOOSK`.
* Each key owns `MAXIMUM_BINDABLE_KEY * 2` bytes in the mapping area, keys packed back to back.
* `keyMapSlotToEEPROMIndex(key, slot) = KEYMAP_START_POSITION + key * KEYMAP_SIZE_PER_KEY + slot * 2`.
* With `BUTTONCOUNT = 14` and `MAXIMUM_BINDABLE_KEY = 12`: parameters 168 bytes, mappings 336 bytes,
  total 504 of 1024 bytes. The firmware performs no bounds checks, so re-check the arithmetic when
  changing either constant.

---

## 8. What has been verified

Checked on a real machine with a real keyboard over HID:

* Enumeration, VID/PID/product/usage parsing, command-interface detection and the probe filter.
* Connect / disconnect, status bar, queue counter, log entries of every kind.
* Pickers for all four device types, including the modifier conversion
  (`Shift_L` → `offset=0x69` → `keyboardKeys[13]` bit 1).
* Device info driving the UI: key count rebuild, hall panel visibility, encoder count.
* Mapping sync, per-key writes, and the minimal-write planner (unit-tested for ten scenarios:
  identical / append / clear / one-slot / middle-change / shrink / reorder / unknown / unknown-full).
* Hall parameters: read, per-key pending edits, batch scope, per-key success evaluation.
* Calibration of a single key and of all keys, with progress reporting.
* Command log window, CSV export (read-only target, unwritable folder) and focus behaviour.
* Settings dialog: persistence, cancel semantics, language switch.
* Firmware version decoding (all 256 byte values round-trip) and compatibility evaluation.
* Translation self-check reporting zero missing entries.

Still worth re-testing on new hardware: the calibration threshold (260), gamepad button naming in
games, and firmware behaviour with `DEBUG` enabled.

---

## 9. UI translation and self-check

UI strings live in `Language.cs`: Chinese literals stay in the source and `L.Apply(control)` /
`L.T()` translate at runtime. The table is bidirectional, so switching back and forth works.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Check-Localization.ps1
```

* exit code 0 = everything translated, 1 = missing entries (report in `tools/localization-report.txt`)
* trailing comments and `#`-prefixed ini strings are ignored
* `L.T("zh", "en")` / `L.F("zh", "en", ...)` carry their English inline

## 10. Key layout upload (0xFE)

Newer firmware describes its physical key arrangement so the host can draw the real
layout instead of a fixed grid.

**Device info byte `[5]` = layoutKind**

| value | meaning |
|---|---|
| `0x00` | legacy device, no layout — host keeps the fixed grid |
| `0xFF` | custom layout, upload it after connecting |
| `0x01`~`0xFE` | reserved for built-in layout ids |

**Query**

```
host 04 FE 00 00 00 00 00 seq      QUERY target 0xFE (layout)
dev  05 04 seq ...                 ACK
dev  FE idx x y w h flags seq      one frame per key, BUTTONCOUNT frames
dev  06 ...                        FIN
```

`0x02` is already taken by `QUERY_CALIB`, so the layout target is `0xFE`, which also keeps
the "device-info family grows downwards from 0xFF" rule.

**Frame `0xFE`**

| byte | meaning |
|---|---|
| `[0]` | `0xFE` |
| `[1]` | key index `0..BUTTONCOUNT-1` |
| `[2]` | x, units of 0.25u (4 = 1u), origin top-left |
| `[3]` | y |
| `[4]` | w, minimum 1 |
| `[5]` | h, minimum 1 |
| `[6]` | flags: bit7|bit6 = `11` → key needs rotation (angle/centre come later in a `0xFD` frame); bits 5..0 = key type (0 normal, 1 modifier, 2 encoder, 3 decor) |
| `[7]` | sequence, echoed like every other reply |

**Host side**

* `KeyLayoutItem.FromFrame()` parses one frame, `ResponseKind.LayoutItem` identifies it.
* `HidTransport.QueryLayoutAsync()` collects replies until FIN (400 ms between frames).
* `KeyLayout.FromFrames()` validates: frame count equals `BUTTONCOUNT`, indices cover
  `0..N-1` exactly once, `w >= 1`, `h >= 1`. Any failure returns `null` plus a reason.
* `Form1.LoadLayoutAsync()` runs after device info; on failure it logs the reason and
  keeps the fixed grid — layout problems never block the mapping/parameter sync.
* `LayoutPanel` draws the keys, colours them like the left-hand buttons
  (green = matches device, red = local edit, blue = selected) and raises
  `KeySelected` so clicking a key selects it.

Coordinates are integers in 0.25u, so 60 %, TKL and 104-key boards all fit in one byte per
axis (max 63.75u). Layout data lives in firmware flash (PROGMEM), never in EEPROM.
