# FrameLedger — Privacy Policy

**Version:** 2.3 · **Effective:** {{RELEASE_DATE}}

**Short version: everything stays on your PC. FrameLedger has no accounts, no telemetry, no analytics, and never uploads your data anywhere.**

## 1. Data the app stores — locally only

Stored in `%LOCALAPPDATA%\FrameLedger` on your device:

- Your game library entries (names, executable paths, metadata you or the app filled in, and whether each one is recorded).
- Performance sessions: frame timing series, computed statistics, hardware sensor series (temperatures, load, memory usage), session duration, crash flags, tags and notes you write.
- A hardware snapshot per session (CPU/GPU model, driver version, RAM size, OS build, display mode) used for the "what changed between sessions" feature.
- App settings, logs (rotated daily — the app keeps 7 days, the capture agent 14 — including the logs written by the component loaded into games), and, after a crash of the app itself, crash dump files.
- Which games you enabled code injection for, and when you consented.

This data never leaves your device unless **you** export it or attach it to a bug report yourself. Deleting the app offers deletion of this folder; you can also delete it manually at any time.

## 2. Network connections the app can make

FrameLedger makes **no network connections except the following two**, both from an installed copy only, and the first can be switched off in Settings ▸ Updates:

| Purpose | Endpoint | When | Data sent |
|---|---|---|---|
| Update check | GitHub Releases API for `poli0981/frameledger` | A few seconds after startup (can be disabled) and when you choose Help ▸ Check for updates | Standard HTTP request metadata only — the request carries `User-Agent: FrameLedger/<version>` and nothing else of ours; no identifiers beyond your IP as seen by GitHub |
| Update download | GitHub release assets | Only after an update is found, and applied only after you restart (never while a game is being measured) | Same as above |

There is no other request. In particular, the anti-cheat safety list and the detection rules ship **with the build** and are installed locally on first run; the software fetches no rules from anywhere, and it looks nothing up on any store. When a rules feed exists it will be a new version of this document, which the app shows you again before continuing (§6).

> **History.** Versions 2.0 and 2.1 of this document listed three further rows — a weekly safety-list update, a weekly detection-rules update, and an opt-in Steam store lookup — describing requests the software did not make; from 2026-08-04 they carried an audit note saying so. Version 2.2 (2026-09-16) removes them. A privacy policy that lists a transmission which never happens is as wrong as one that omits a transmission which does.

GitHub's own privacy practices apply to requests it receives: <https://docs.github.com/privacy>.

## 3. Bug reports — always manual

The "Report a bug" feature builds a zip file **on your device**, shows you exactly which files it contains, and lets you inspect them. Nothing is transmitted by the app; **you** decide whether to attach the file to a GitHub issue in your browser. Logs are redacted (user directory paths removed) before bundling. Optional items (crash dumps, last-session metadata) are included only when you tick their checkboxes.

## 4. What the app can technically observe

To do its job, FrameLedger observes: which of *your tracked* executables — the entries in your library whose recording is on — are running, and their process trees; the parameters your games pass to the graphics APIs it intercepts (presentation, upscaling, ray tracing, pipeline creation); video-memory usage reported by the graphics runtime; loaded module names of a game being captured (for detection and for the anti-cheat safety check); and hardware sensor values from your graphics driver's own libraries.

It does **not** read game memory outside those API parameters, and does not read game saves, chat, input content, network traffic, or anything unrelated to performance measurement. It records only for programs you added to your library and did not switch recording off for — a program whose recording is off is not watched at all — and injects only into games you individually enabled.

> **History.** Version 2.3 (2026-09-23) adds the per-entry recording switch. Before it, every program in your library was recorded whenever it ran, which recorded a utility that starts with Windows at every boot.

All of this stays on your device. None of it is transmitted anywhere.

## 5. Children

FrameLedger is a technical utility, provides no communication features, and collects no personal information from anyone.

## 6. Changes

Material changes to this policy increment its version; the app will show the updated document for review before continuing.

Contact: <contact@poli0981.dev> · Developer: <https://poli0981.dev/> · Project: <https://github.com/poli0981/frameledger>
