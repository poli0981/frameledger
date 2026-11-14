# 21 — Input devices: controllers, and keyboard-and-mouse activity

What FrameLedger reads about game controllers and about the keyboard and mouse, from where, and what it never reads.
The numbers a session makes of it are in [`03_METRICS.md`](03_METRICS.md) §Input; the columns are in
[`06_DATA_MODEL.md`](06_DATA_MODEL.md).

> **Status, 2026-11-14 — nothing reads a device yet.** The model and the arithmetic exist (`FrameLedger.Domain.Input`,
> `FrameLedger.Application.Input`, `Application.Recording.InputSessionBuilder`), under tests whose every byte and
> reading was written by hand. The reader, the storage, the switch and the screens come in the pull requests after
> it, and this note is rewritten by each. **No report from a real controller has passed through any of this code**
> (owner decision D157): the developer has no controller, and what happens on a real one is owner-only item 48.

## The owner's request and decisions (2026-11-14)

The request: whether a game supports controllers, on the game's page; the share of a session played with a controller
and with the keyboard; how often a controller connected and disconnected in a session; the battery state that lasted
longest; switches between makers' controllers and each maker's share; stored, and drawn; Xbox controllers and the
DualSense first.

| | Decision |
|---|---|
| **D155** | Controllers are read **by the Agent, from outside the game**. No hook is added to a game and CLAUDE.md rule 4 is untouched. A session that is not hooked has the numbers as a hooked one does. |
| **D156** | The reading is **on unless switched off** (Settings ▸ Controllers and input, `capture.input_devices`), and **reads nothing until the Privacy Policy that describes it — 2.22 or later — has been accepted on the PC**. An Agent that the logon task started after an update records sessions without it until the App has been opened once. |
| **D157** | **Built without a probe.** Asked whether to run a probe tool with real controllers first, the owner chose to build straight. So every source says what it did (§What the reader says about itself), and each pull request and `LIMITATIONS.md` say what was verified against hand-made data only. |
| **D158** | Keyboard and mouse are **one class, known only by the time of the last input** (`GetLastInputInfo`). No Raw Input registration for a keyboard or a mouse, no hook: no key ever enters FrameLedger's process. |
| **D159** | **Nothing is ever sent to a controller**: no output report, no feature-report request, no `XInputSetState`. A DualSense on Bluetooth therefore has a battery level only when Steam or the game has already switched it to its long report (§DualSense). |
| **D160** | The numbers are **in the CSV/JSON export and in a bug bundle** — they are columns of `sessions`, as the display mode's are — and the Privacy Policy's §3A says so. |
| **D161** | **A session that is not hooked is: duration, hardware telemetry, the reason — and what the Agent reads from outside the game** (the game's memory since beta.12, controllers now). Every field measured inside the game still reads N/A. CLAUDE.md's sentence is amended with the pull request that makes it true. |
| **D162** | Also built: the number of switches between a controller and the keyboard and mouse, and the share of time with no input; marks on the frame-time chart where a controller connected, dropped or ran low; a battery's first and last level and its rate; the kind of connection; a note where Steam Input presented the controller as an Xbox pad; a trend metric, a row in Compare, and "usually played with" on the game's page. |
| **D163** | "Supports controllers" is **three pieces of evidence, never a verdict**: what Steam's own file on the disk says (no network: rule 8), which input libraries the game loaded, and how the game was actually played. From outside a game one can know that a controller was in use while the game was in front — not what the game did with it. |

## What is read, and from where

Everything is a Windows API in a library Windows ships. **No third-party library is added.**

| Source | What it gives | Why this one |
|---|---|---|
| Raw Input (`RegisterRawInputDevices`, usage page 1, usages 4 and 5, `RIDEV_INPUTSINK \| RIDEV_DEVNOTIFY`) | the reports game controllers send, while the Agent is in the background; a controller arriving and leaving; its vendor and product ids | the documented way to receive a HID controller's reports without being in front, and it opens no handle to the device |
| `hid.dll`'s parser (`HidP_GetCaps`, `HidP_GetUsages`, `HidP_GetUsageValue`) | which usages of a report are set | a parser, not I/O: it reads the bytes Raw Input handed over |
| XInput 1.4 (`XInputGetState`, `XInputGetCapabilities`, `XInputGetBatteryInformation`) | each of its four pads: its state, wired or wireless, a battery in four levels | what an Xbox pad's battery is reported through |
| `GetLastInputInfo` | the time of the last keyboard or mouse input in the user's session | a time and nothing else (D158) |
| `GetForegroundWindow`, `GetWindowThreadProcessId` | whose window is in front | already used for the focus sample (`04_CAPTURE`) |
| Steam's `appcache\appinfo.vdf`, on the disk | whether the store lists controller support for a library game | a local file: no request is made (rule 8) |

**Rejected, with the reason** (the checklist of `18_GPU_VENDOR_APIS` §Checklist applies to any library):

- **Windows.Gaming.Input** — its readings stop when the program is not in front, which the Agent never is.
- **GameInput** — on a PC it is a NuGet package under Microsoft's licence terms with a redistributable to install.
- **Opening the HID device** (`CreateFile`) — a handle of FrameLedger's on a controller can make a game's, or a
  remapper's, exclusive open of it fail. Raw Input needs none.
- **A sixth native library** — parsing bytes a device controls in C++, inside an Agent that may run as administrator.
  The reader is C#, and what parses a report is `FrameLedger.Domain.Input`, with no pointer in it.

## What is never read

Which button, where a stick or a trigger is, the motion sensors, the touchpad; which key, what was typed, where the
mouse is; a controller's serial number, its Bluetooth address, its device path. A report is looked at to answer one
question — *is anything on this pad in use?* — and, for a DualSense, to take its battery byte; the answer is kept and
the report is not. `InputSessionBuilderTests` hold the stored JSON to a list of keys, so a key that could carry an
identifier has to be added to a test first.

## Who a controller is

By its USB vendor and product ids only (`Domain.Input.ControllerCatalog`): Microsoft is the Xbox family, Sony
PlayStation, Nintendo Nintendo, anything else *other* — except that a pad Windows presents through XInput is in the
Xbox family whoever made it, because that is all Windows says of it. A model has a name only where the catalog lists
its ids; a wrong name would be worse than none. Two pads of one model on one kind of connection cannot be told apart
across a reconnect, and are not.

## XInput

A state is a pad *in use* when a button is down, a trigger is past `XINPUT_GAMEPAD_TRIGGER_THRESHOLD` or a stick is
outside its `XINPUT_GAMEPAD_*_THUMB_DEADZONE` (`Domain.Input.XInputPad`) — a stick held counts, one that drifts inside
the dead zone does not. Its battery is XInput's words: a wired pad, or four levels with no word for charging.

Two things about XInput are not established and are what item 48 is for: Microsoft's page for `XInputEnable` says
that on Windows 10 and later "game controller input is automatically enabled/disabled by the system based on the
application window focus", which may mean a program in the background reads a neutral state; and a pad on Bluetooth
is reported not to answer `XInputGetBatteryInformation`. XInput also has no documented way to say which device one of
its four pads is.

## DualSense

Sony's 054C:0CE6, and the Edge, 054C:0DF2 (`Domain.Input.DualSenseReport`). Three reports:

| Connection | Report | What it carries |
|---|---|---|
| USB | `0x01`, 64 bytes | sticks, triggers, buttons — and the battery: a level in tenths and what the battery is doing |
| Bluetooth, as connected | `0x01`, 10 bytes | sticks, buttons, triggers. **No battery** |
| Bluetooth, once switched | `0x31`, 78 bytes | the USB report's body, one byte later |

The pad sends the short report until some program asks it for a feature report or sends it an output report; from then
until it reconnects it sends the long one. **FrameLedger never asks** (D159): a game that reads the short report through
DirectInput would lose its input. So over Bluetooth the level is there when Steam or the game switched the pad, and is
not otherwise. Windows hands every report of a device over at the length of the device's longest, so the short report
arrives padded to 78 bytes with the id the USB report has: the connection, not the length, says which layout a report
`0x01` has.

The layout was written from the open descriptions of the protocol. No code was taken from the Linux kernel's driver,
which is under a licence this project cannot take code from.

## What the reader says about itself

`Application.Input.InputHealth`, carried by every reading and stored with the session: whether Windows accepted the
registration, how many reports came and how many could not be read, whether XInput's library was there and how many
states it returned, how often a controller's own reports said *in use* while XInput said nothing, whether the time of
the last input could be read, how many slices were taken and how much processor time the reader's thread used. Counts
and yes-or-no answers; nothing in it names a device.
