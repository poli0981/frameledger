# Capture pipeline

What the agent does from the moment a game starts. The full document is [`docs/04_CAPTURE.md`](../docs/04_CAPTURE.md);
the guard is [`docs/19_SAFETY_AND_ANTICHEAT.md`](../docs/19_SAFETY_AND_ANTICHEAT.md); the code is in
`src/FrameLedger.Application/Capture`, `src/FrameLedger.Application/Recording` and `src/FrameLedger.Application/Watch`.

## Watching

- Once a second, the watcher lists the running processes and matches each executable's **full path** against the
  library entries that have recording on. A file name alone never starts a session — except the same file on a
  changed drive letter, which the agent follows.
- A launcher's consent does not carry over to the game it starts: the game's own executable is the one that counts.
- One session per game at a time. At every start, the agent first rebuilds any session a crash interrupted.

## A session's life

`Idle → Detected → Guarded → Injecting → Capturing → Finalizing → Saved`, with three side roads:

- **Refused** — the guard found something, or hooking is off: the session continues as Tier 2, with the reason.
- **Unhooked for safety** — a finding at a 30-second re-scan: the hook goes dormant (it is never unloaded), measuring
  stops, and the session keeps the frames measured so far and says it stopped early.
- **Interrupted** — the agent itself stopped: its `.partial` file is turned into a session at the next start.

A session shorter than the minimum length (30 seconds by default) is discarded.

## While capturing

- The ring is drained every 100 ms; it holds about 16 seconds of frames at 500 fps, so a stall of the agent loses
  nothing, and a dropped record is a warning on the session, never silently accepted.
- Sensors are read on their own thread once a second, through three layers: Windows itself (DXGI and performance
  counters, any GPU), LibreHardwareMonitor (any GPU), and NVAPI (NVIDIA cards). A missing layer makes its fields N/A.
- The game's own video memory and RAM are read from outside the game, in both tiers.
- Everything is written to the session's `.partial` file at each step and every 60 seconds.

## Ending

- The last drain, then one SQLite transaction writes the session, its segments, its frame and sensor series.
- How the game ended is classified: *normal*; *crashed* (an exception exit code, an application-error event in the
  window, or the game's own crash reporter starting late); *unhooked for safety*; *degraded* (the hook faulted or lost
  the game); *interrupted*.
- Two abnormal ends within 60 seconds of attaching switch hooking off for that game, and nothing switches it back on
  automatically.

Why a session ended is the `SessionEndReason` type in `src/FrameLedger.Application/Capture` — the list grows, so read
the code rather than a copy of it.
