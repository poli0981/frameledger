# FrameLedger — Privacy Policy

**Version:** 2.7 · **Effective:** {{RELEASE_DATE}}

**Short version: everything stays on your PC. FrameLedger has no accounts, no telemetry, no analytics, and never uploads your data anywhere.**

## 1. Data the app stores — locally only

Stored in `%LOCALAPPDATA%\FrameLedger` on your device:

- Your game library entries (names, executable paths, metadata you or the app filled in, and whether each one is recorded).
- Performance sessions: frame timing series, computed statistics, hardware sensor series (temperatures, load, memory usage), the game's own memory use (its dedicated and shared video memory, private working set, working set and commit, once a second), session duration, crash flags, the graphics driver's settings the game ran under (NVIDIA only), how the game's window was shown (full-screen, borderless or windowed, for how long, its size and the monitor's), tags and notes you write.
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

GitHub's own privacy practices apply to requests it receives: <https://docs.github.com/privacy>.

## 3. Bug reports — always manual

The "Report a bug" feature builds a zip file **on your device**, shows you exactly which files it contains, and lets you inspect them. Nothing is transmitted by the app; **you** decide whether to attach the file to a GitHub issue in your browser. Logs are redacted (user directory paths removed) before bundling. Optional items (crash dumps, last-session metadata) are included only when you tick their checkboxes.

## 3A. Sharing an export or a bug report

An exported session (CSV or JSON) and a bug report can contain your games' names and **the paths to their files** — which include your **Windows user name** when a game is installed under your user folder — your hardware model, graphics driver and Windows build, the times you played, and your tags and notes. A bug report removes user directory paths from its logs; an export is written as it is. Look at a file before you share it. Once you share it, it is outside FrameLedger: whoever receives it, and the site you post it to, can read it under their own terms.

## 4. What the app can technically observe

To do its job, FrameLedger observes: which of *your tracked* executables — the entries in your library whose recording is on — are running, and their process trees; the parameters your games pass to the graphics APIs it intercepts (presentation, upscaling, ray tracing, pipeline creation); the memory use of a tracked game's process — its dedicated and shared video memory, from Windows' GPU performance counters, and its working sets and commit, from Windows' process-memory information, for which a game it does not inject into is opened with the least access Windows offers and closed again (never its memory's content); loaded module names of a game being captured (for detection and for the anti-cheat safety check); the names of the files in each library game's install folder, the version information of its executable and of the upscaling libraries it ships, the content of its executable file (the markers of the engine and of the graphics libraries it carries, the libraries it imports, and the engine build name an Unreal Engine game carries), and the store files that name it (Steam's and GOG's, on your disk) — to detect its engine, its engine version and its features and to check it for anti-cheat before any injection is possible; the size, position and state of a tracked game's window and the monitor it is on, read from Windows and not from the game, and — for a game FrameLedger injects into — whether the game asked for exclusive full-screen; the NVIDIA driver's settings for your tracked executables (read only: the DLSS and frame-generation overrides the NVIDIA App set); hardware sensor values from your graphics driver's own libraries; how busy the processor is and how much memory is in use, from Windows' own counters; the crash reports Windows keeps in its Application log about a tracked game's executable while it ran (to tell a crash from an ordinary exit — the reports are read, never sent anywhere); and the names of the programs a tracked game starts, so that a game's own crash reporter is recognised.

It does **not** read game memory outside those API parameters, and does not read game saves, chat, input content, network traffic, or anything unrelated to performance measurement. It records only for programs you added to your library and did not switch recording off for — a program whose recording is off is not watched at all — and injects only into games you individually enabled.

All of this stays on your device. None of it is transmitted anywhere.

## 5. Children

FrameLedger is a technical utility, provides no communication features, and collects no personal information from anyone.

## 6. Changes

Material changes to this policy increment its version; the app will show the updated document for review before continuing.

Contact: <contact@poli0981.dev> · Developer: <https://poli0981.dev/> · Project: <https://github.com/poli0981/frameledger>
