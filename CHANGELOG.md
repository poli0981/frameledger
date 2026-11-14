# Changelog

All notable changes to FrameLedger are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — `MAJOR`
bumps for a database schema or IPC protocol break (`docs/11_UPDATER.md`).

`release.yml` reads the section for the tag being released (`tools/release-notes.ps1`) and
uses it as the GitHub release body. A missing or empty `## [x.y.z]` section is a **red
release**, not an empty note — so cutting a release means moving the `[Unreleased]` entries
under a `## [x.y.z] - date` heading in the same commit that bumps `VERSION`, then tagging
(`docs/13_CI_CD.md` §Branch & release policy). *(For a pre-release, `vX.Y.Z-beta.N`, `VERSION` already holds the
numeric core and does not move; the heading is the full version — corrected 2026-10-04.)*

> **Built 2026-09-14 (P4 PR-5).** From 2026-08-06 until then this paragraph said the workflow
> "will" exist and that a missing section "will mean an empty release note"; the second half
> was the wrong design and is replaced rather than implemented. ~~This file still has no
> `## [x.y.z]` heading: the first one is the owner's, in the commit that cuts the first tag.~~
>
> **The first heading is `## [0.1.0-beta.1]` (2026-09-16), and it is a summary rather than the moved
> entries.** The instruction above says "move the `[Unreleased]` entries under the heading"; by the first
> tag those entries were 370 KB — every PR since 2026-08-04 — and a GitHub release body holds 125,000
> characters. So the versioned section is a written summary of what the pre-release is and is not, and
> the entries it summarises stay below it, verbatim, under a heading that is not a version
> (`## Ledger before 0.1.0-beta.1`), where `release-notes.ps1` stops reading. From the second tag on,
> the entries under `[Unreleased]` are short enough to move as written.

> **Split 2026-10-19 (beta.24, owner decision D66).** Each released section now lives in its own file under
> `docs/changelog/`, moved verbatim, and the ledger kept before the first tag in five parts under
> `docs/changelog/ledger-before-0.1.0-beta.1/`; the table below lists them all. This file keeps `[Unreleased]`, where
> every pull request adds its entry (`tools/changelog-check.ps1` looks for this file). `tools/release-notes.ps1` reads
> `docs/changelog/<version>.md` for the tag being released, so cutting a release moves the `[Unreleased]` entries into
> that new file — under its `## [x.y.z] - date` heading — and adds its row to the table, in the commit that is tagged.

## [Unreleased]

### Added

- **For developers: the arithmetic of controller statistics, with nothing reading a device yet** (D155–D163, the
  owner's decisions of 2026-11-14; `docs/21_INPUT_DEVICES.md`, `docs/03_METRICS.md` §Input). `FrameLedger.Domain.Input`
  says whether an XInput state or a DualSense report is a pad in use and what its battery is, adds a battery's readings
  up, counts switches, and decides when two entries are one pad seen twice; `Application.Input` holds one session's
  slices and turns them into a reading a second; `InputSessionBuilder` turns a session's readings into its totals, its
  controllers as JSON and its timeline. A telemetry sample can carry a reading; none does. **Every byte and reading
  under test was written by hand — no report from a real controller has passed through this code.**
- **For developers: a session can store what it was played with — schema 0024** (D160; `docs/06_DATA_MODEL.md`). Nine
  columns on `sessions` (the time in front and its four classes, the switches of method, the sources that answered,
  the controllers as JSON) and three on `games` (what a store lists of controller support), ADD COLUMN only; the
  `.partial` file gains chunk 11, so a session recovered after the Agent died keeps its readings; the finalizer
  stores them for hooked and unhooked sessions alike; the input series are `sensor_blobs` rows named `input.*`, which
  the sensors' read leaves out. Nothing produces a reading yet. **A ledger this build has opened cannot be opened by
  0.1.0-beta.49 or earlier.**
- **For developers: the input reader, which the Agent does not start yet** (D155, D158, D159; `docs/21_INPUT_DEVICES.md`
  §The reader). `Application.Input.InputReader` takes a slice about ten times a second from
  `Infrastructure.Input.Win32InputBackend` — raw input for joysticks and gamepads delivered to a window that is never
  shown, three of XInput's getters, the time of the last keyboard or mouse input, the window in front — on the thread
  of `InputDeviceHub`, which exists only while a session has a view of it. It opens no device, sends nothing to one,
  and registers for no keyboard or mouse. A new gate, `tools/input-readonly-check.ps1`, holds the reader's Windows
  calls to a committed list and refuses, on the list or off it, a call that sends to a device, injects input or reads
  a key; no other managed source and no native source may name an input function. **With no controller at hand, what
  the reader does with one connected is not established**: its tests use a Windows whose answers they write, and one
  suite runs it on the real Windows of the machine and asserts only that it opens, answers and closes.

### Changed

- **Advanced metrics is closed: the ten readings that were "being studied" are now "not read", with the reason**
  (D154, the owner's decision). What a game sets and asks through Streamline's own functions for a feature — DLSS
  frame generation's options and state, the video memory DLSS estimates for itself, the options of ray
  reconstruction, Image Scaling and Dynamic Vibrance, the PC-latency markers — is reached only by handing the game
  a function of FrameLedger's in place of Streamline's, which is refused. The *Sources* list says so, in English,
  Vietnamese and Japanese, and the guide no longer says a later release reads more. Nothing that was read stops
  being read.

- **The two lines of the graphics card's time say what each is, not which holds the other** (D152, the owner's
  decision). *GPU active render (idle time excluded)* and *GPU render, start to end (idle time included)* are now
  *GPU active render (the driver's own figure)* and *GPU render (between the driver's start and end marks)*, in
  English, Vietnamese and Japanese, and the guide says why: in Cyberpunk 2077 without frame generation the
  driver's active time is longer than its two marks are apart. The numbers are unchanged.

- **The guide's page on Advanced metrics says what two of the driver's switches mean** (`guide/12`): the latency
  report is there in a game that has Reflex built in, whether Reflex is on or off in its menu; `bUseGameSleep`
  says the game calls Reflex's sleep — NVIDIA asks for that even with Reflex off — and `bLowLatencyMode` is the
  one that says the mode is on; frame generation needs Reflex, not the reverse. From NVIDIA's Streamline guide
  for Reflex, and from the owner's ten sessions on 0.1.0-beta.49.

### Documentation

- **Owner-only item 47 was run and read** (`docs/HANDOFF.md`): in Cyberpunk 2077 without frame generation the
  driver's active GPU time is longer than its own two marks are apart on 4 917 of 5 394 frames (1 849 µs beside
  1 571 µs) and on none of 129 900 frames in five other games — the difference is the driver's; samples without a
  report are now confined to a game's start; a session with the option off has no latency line; and the stop of
  D141 fired in a real game for the first time (Kingdom Come: Deliverance II, one call of 5.3 ms while it
  started).
- **Streamline's programming guides for Reflex and for DLSS Ray Reconstruction (2.14.16) read against the code**
  (`docs/spike-notes/30-reflex-latency.md`, *Added 2026-11-14*). Ray reconstruction was already measured the way
  the guide describes; two comments in the Overlay's source and one line of `17_HOOK_ENGINE` said super
  resolution and ray reconstruction run together, and they do not. `03_METRICS` and `05_DETECTION` no longer
  promise "PC latency as reported by Reflex" (D147). No code changed: comments only.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
| `0.1.0-beta.49` | 2026-11-14 | [`docs/changelog/0.1.0-beta.49.md`](docs/changelog/0.1.0-beta.49.md) |
| `0.1.0-beta.48` | 2026-11-14 | [`docs/changelog/0.1.0-beta.48.md`](docs/changelog/0.1.0-beta.48.md) |
| `0.1.0-beta.47` | 2026-11-14 | [`docs/changelog/0.1.0-beta.47.md`](docs/changelog/0.1.0-beta.47.md) |
| `0.1.0-beta.46` | 2026-11-13 | [`docs/changelog/0.1.0-beta.46.md`](docs/changelog/0.1.0-beta.46.md) |
| `0.1.0-beta.45` | 2026-11-12 | [`docs/changelog/0.1.0-beta.45.md`](docs/changelog/0.1.0-beta.45.md) |
| `0.1.0-beta.44` | 2026-11-12 | [`docs/changelog/0.1.0-beta.44.md`](docs/changelog/0.1.0-beta.44.md) |
| `0.1.0-beta.43` | 2026-11-12 | [`docs/changelog/0.1.0-beta.43.md`](docs/changelog/0.1.0-beta.43.md) |
| `0.1.0-beta.42` | 2026-11-12 | [`docs/changelog/0.1.0-beta.42.md`](docs/changelog/0.1.0-beta.42.md) |
| `0.1.0-beta.41` | 2026-11-12 | [`docs/changelog/0.1.0-beta.41.md`](docs/changelog/0.1.0-beta.41.md) |
| `0.1.0-beta.40` | 2026-11-12 | [`docs/changelog/0.1.0-beta.40.md`](docs/changelog/0.1.0-beta.40.md) |
| `0.1.0-beta.39` | 2026-11-11 | [`docs/changelog/0.1.0-beta.39.md`](docs/changelog/0.1.0-beta.39.md) |
| `0.1.0-beta.38` | 2026-11-11 | [`docs/changelog/0.1.0-beta.38.md`](docs/changelog/0.1.0-beta.38.md) |
| `0.1.0-alpha.4` | 2026-11-11 | [`docs/changelog/0.1.0-alpha.4.md`](docs/changelog/0.1.0-alpha.4.md) |
| `0.1.0-beta.37` | 2026-11-11 | [`docs/changelog/0.1.0-beta.37.md`](docs/changelog/0.1.0-beta.37.md) |
| `0.1.0-beta.36` | 2026-11-10 | [`docs/changelog/0.1.0-beta.36.md`](docs/changelog/0.1.0-beta.36.md) |
| `0.1.0-beta.35` | 2026-11-09 | [`docs/changelog/0.1.0-beta.35.md`](docs/changelog/0.1.0-beta.35.md) |
| `0.1.0-alpha.3` | 2026-11-06 | [`docs/changelog/0.1.0-alpha.3.md`](docs/changelog/0.1.0-alpha.3.md) |
| `0.1.0-beta.34` | 2026-11-06 | [`docs/changelog/0.1.0-beta.34.md`](docs/changelog/0.1.0-beta.34.md) |
| `0.1.0-alpha.2` | 2026-11-03 | [`docs/changelog/0.1.0-alpha.2.md`](docs/changelog/0.1.0-alpha.2.md) |
| `0.1.0-beta.33` | 2026-11-03 | [`docs/changelog/0.1.0-beta.33.md`](docs/changelog/0.1.0-beta.33.md) |
| `0.1.0-alpha.1` | 2026-10-31 | [`docs/changelog/0.1.0-alpha.1.md`](docs/changelog/0.1.0-alpha.1.md) |
| `0.1.0-beta.32` | 2026-10-31 | [`docs/changelog/0.1.0-beta.32.md`](docs/changelog/0.1.0-beta.32.md) |
| `0.1.0-beta.31` | 2026-10-26 | [`docs/changelog/0.1.0-beta.31.md`](docs/changelog/0.1.0-beta.31.md) |
| `0.1.0-beta.30` | 2026-10-26 | [`docs/changelog/0.1.0-beta.30.md`](docs/changelog/0.1.0-beta.30.md) |
| `0.1.0-beta.29` | 2026-10-25 | [`docs/changelog/0.1.0-beta.29.md`](docs/changelog/0.1.0-beta.29.md) |
| `0.1.0-beta.28` | 2026-10-20 | [`docs/changelog/0.1.0-beta.28.md`](docs/changelog/0.1.0-beta.28.md) |
| `0.1.0-beta.27` | 2026-10-20 | [`docs/changelog/0.1.0-beta.27.md`](docs/changelog/0.1.0-beta.27.md) |
| `0.1.0-beta.26` | 2026-10-19 | [`docs/changelog/0.1.0-beta.26.md`](docs/changelog/0.1.0-beta.26.md) |
| `0.1.0-beta.25` | 2026-10-19 | [`docs/changelog/0.1.0-beta.25.md`](docs/changelog/0.1.0-beta.25.md) |
| `0.1.0-beta.24` | 2026-10-19 | [`docs/changelog/0.1.0-beta.24.md`](docs/changelog/0.1.0-beta.24.md) |
| `0.1.0-beta.23` | 2026-10-14 | [`docs/changelog/0.1.0-beta.23.md`](docs/changelog/0.1.0-beta.23.md) |
| `0.1.0-beta.22` | 2026-10-13 | [`docs/changelog/0.1.0-beta.22.md`](docs/changelog/0.1.0-beta.22.md) |
| `0.1.0-beta.21` | 2026-10-13 | [`docs/changelog/0.1.0-beta.21.md`](docs/changelog/0.1.0-beta.21.md) |
| `0.1.0-beta.20` | 2026-10-12 | [`docs/changelog/0.1.0-beta.20.md`](docs/changelog/0.1.0-beta.20.md) |
| `0.1.0-beta.19` | 2026-10-12 | [`docs/changelog/0.1.0-beta.19.md`](docs/changelog/0.1.0-beta.19.md) |
| `0.1.0-beta.18` | 2026-10-07 | [`docs/changelog/0.1.0-beta.18.md`](docs/changelog/0.1.0-beta.18.md) |
| `0.1.0-beta.17` | 2026-10-07 | [`docs/changelog/0.1.0-beta.17.md`](docs/changelog/0.1.0-beta.17.md) |
| `0.1.0-beta.16` | 2026-10-06 | [`docs/changelog/0.1.0-beta.16.md`](docs/changelog/0.1.0-beta.16.md) |
| `0.1.0-beta.15` | 2026-10-06 | [`docs/changelog/0.1.0-beta.15.md`](docs/changelog/0.1.0-beta.15.md) |
| `0.1.0-beta.14` | 2026-10-06 | [`docs/changelog/0.1.0-beta.14.md`](docs/changelog/0.1.0-beta.14.md) |
| `0.1.0-beta.13` | 2026-10-04 | [`docs/changelog/0.1.0-beta.13.md`](docs/changelog/0.1.0-beta.13.md) |
| `0.1.0-beta.12` | 2026-10-04 | [`docs/changelog/0.1.0-beta.12.md`](docs/changelog/0.1.0-beta.12.md) |
| `0.1.0-beta.11` | 2026-10-03 | [`docs/changelog/0.1.0-beta.11.md`](docs/changelog/0.1.0-beta.11.md) |
| `0.1.0-beta.10` | 2026-09-28 | [`docs/changelog/0.1.0-beta.10.md`](docs/changelog/0.1.0-beta.10.md) |
| `0.1.0-beta.9` | 2026-09-26 | [`docs/changelog/0.1.0-beta.9.md`](docs/changelog/0.1.0-beta.9.md) |
| `0.1.0-beta.8` | 2026-09-25 | [`docs/changelog/0.1.0-beta.8.md`](docs/changelog/0.1.0-beta.8.md) |
| `0.1.0-beta.7` | 2026-09-23 | [`docs/changelog/0.1.0-beta.7.md`](docs/changelog/0.1.0-beta.7.md) |
| `0.1.0-beta.6` | 2026-09-23 | [`docs/changelog/0.1.0-beta.6.md`](docs/changelog/0.1.0-beta.6.md) |
| `0.1.0-beta.5` | 2026-09-22 | [`docs/changelog/0.1.0-beta.5.md`](docs/changelog/0.1.0-beta.5.md) |
| `0.1.0-beta.4` | 2026-09-21 | [`docs/changelog/0.1.0-beta.4.md`](docs/changelog/0.1.0-beta.4.md) |
| `0.1.0-beta.3` | 2026-09-21 | [`docs/changelog/0.1.0-beta.3.md`](docs/changelog/0.1.0-beta.3.md) |
| `0.1.0-beta.2` | 2026-09-17 | [`docs/changelog/0.1.0-beta.2.md`](docs/changelog/0.1.0-beta.2.md) |
| `0.1.0-beta.1` | 2026-09-16 | [`docs/changelog/0.1.0-beta.1.md`](docs/changelog/0.1.0-beta.1.md) |

The ledger kept before the first tag (2026-08-04 → 2026-09-16), every pull request in full, newest first:

- [`docs/changelog/ledger-before-0.1.0-beta.1/part-1.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-1.md) — 455 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-2.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-2.md) — 805 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-3.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-3.md) — 954 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-4.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-4.md) — 990 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-5.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-5.md) — 1218 lines

[Unreleased]: https://github.com/poli0981/frameledger/commits/main
