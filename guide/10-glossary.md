# Words FrameLedger uses

What the words on FrameLedger's screens mean, in alphabetical order. How each figure is computed is in the developers'
[metrics document](../docs/03_METRICS.md).

- **0.1% low, 1% low** — the frame rate of the game's slowest 0.1% and 1% of frames. The further they fall below the
  average, the less smooth the game felt. They need 10,000 and 1,000 of the game's own frames.
- **Agent** (the capture agent) — the program FrameLedger runs in the background, with your normal rights, that notices
  a game starting and records it.
- **Anti-cheat finding** — what FrameLedger's anti-cheat check found for a game: a component loaded into it, a file or
  folder of it, or the game itself on a list. A game with a finding is not hooked ([Anti-cheat and
  safety](04-anti-cheat-and-safety.md)).
- **Bug bundle** — the zip **Help ▸ Report a bug…** writes on your PC. Nothing is sent by FrameLedger.
- **Compare** — two to five sessions side by side, from any of your games.
- **Disable all hooking** — the switch that stops every game from being hooked at once, whatever each game's own switch
  says. Sessions are still recorded.
- **Displayed FPS** — the frames you saw, generated frames included.
- **FG factor** — Displayed FPS divided by Native FPS, shown as `×1.9 FG`.
- **FPS** — frames per second over the time measured.
- **Frame generation** — the technology (DLSS Frame Generation, FSR Frame Generation) that inserts frames the game did
  not render itself.
- **Frame time** — how long one frame took, in milliseconds.
- **Frame to frame** — how much one frame's time differs from the next. Lower is smoother.
- **Frames per watt** — the game's own frames per second for each watt the graphics card drew.
- **GPU held back** — how much of the time the card's power limit or its temperature slowed it down. NVIDIA cards only.
- **Hooking** — loading FrameLedger's small component into a game to measure it. Off for every game until you switch
  it on for that game.
- **Ledger** — the one database file that holds your library and sessions, `%LOCALAPPDATA%\FrameLedger\ledger.db`.
- **N/A** — not measured. Never 0, and never an estimate.
- **Native FPS** — the frames the game rendered itself.
- **Overlay build** — the version of the component that hooking loads into a game. It draws nothing on your screen.
- **Override** — your own Yes, No or N/A for a ray-tracing chip. The measured value is kept, and yours is marked as
  yours.
- **Presented FPS** — every frame the game handed to Windows, shown when frame generation could not be measured, with a
  note saying whether generated frames could be among them.
- **RAM (this game)** — the game's own memory, what Task Manager's Details tab shows as its private working set.
- **Ray tracing, Path tracing, Ray reconstruction** — read *Yes*, *No* or *N/A*. Ray tracing is measured on DirectX 12
  games; path tracing is never stated as a fact.
- **Render scale** — the share of the screen's resolution the game drew at before upscaling. 100% means no upscaling.
- **Session** — one run of a game, from its start to its end. A session shorter than 30 seconds is not kept.
- **Steady state** — when frame generation changed during a session (menus, loading screens, a settings change), the
  figures are for the state that lasted longest, with the share of the session it covers: `×1.9 FG · 75%`.
- **Stutter** — frames that took more than twice as long as the frames around them, counted, with the share of time
  they took.
- **T1** — a measured session. **T2** — a session that was only recorded: how long you played, your PC's sensors and
  the game's memory, with every frame figure N/A.
- **Tags and notes** — what you write on a session's summary.
- **Telemetry source** — which sensor readers work on this PC: `l1` is Windows itself, `lhm` is LibreHardwareMonitor,
  `nvapi` is NVIDIA's own.
- **Time below 60 FPS** — how much of the session ran slower than 60 frames per second (and *below 30*, and *below your
  display's refresh rate*), not counting the tiny wobble of a game held at its frame limit.
- **Trend** — one point per measured session over time, on a game's page.
- **Upscaler** — the technology that draws the game at a lower resolution and scales it up (DLSS, FSR).
- **User-mode exception** — the one way to hook a game where anti-cheat was found: only an anti-cheat that runs entirely
  inside the game, one game at a time, after a warning. Its first two sessions are a trial.
- **Viewer** — FrameLedger opened over a copy of a data folder: no agent, nothing recorded, nothing changed.
- **VRAM (this game)** — the game's own video memory, what Task Manager shows as its *Dedicated GPU memory*.
- **VSync** — how many frames asked to wait for the display; *tearing allowed*, how many did not mind tearing. It is
  what the game asked: your graphics driver can override it.
- **Vulkan layer** — the component that measures Vulkan games, used only when FrameLedger starts a game itself.
