# Read your results

Open **Games ▸ a game ▸ Sessions**: one row per session. Double-click a row (or press **Open**) for the session's
summary. The Dashboard lists your most recent sessions too.

## N/A means "not measured"

A value FrameLedger could not measure reads **N/A** — never 0, and never an estimate. A session that was only recorded
(**T2**) has N/A for everything that needs hooking, and says why.

## Frame rate

- **When frame generation was measured**, the game's own frames and the frames you saw are shown together:
  `62 → 118 FPS (×1.9 FG)`. If frame generation changed during the session (menus, loading screens, a settings change),
  the figures are for the state that lasted longest, with the share of the session it covers: `(×1.9 FG · 75%)`.
- **When it could not be measured**, you see **Presented FPS** with a sentence saying whether generated frames could be
  included in it. FrameLedger never shows a single number that may count generated frames as the game's own.
- **1% low** and **0.1% low** are the frame rate of the slowest 1% and 0.1% of frames: the further they fall below the
  average, the less smooth the game felt. **Stutter** counts frames that took much longer than the frames around them,
  and the share of time they took.
- **Time below 60 FPS** (with *below 30* and *display under* your monitor's refresh rate beneath it) is how much of the
  session ran slower than that. A game held at its limit is not counted as dropping below it for the tiny wobble every
  frame limit has. **Frame to frame** is how much one frame's time differs from the next: lower is smoother.
- **VSync** is how many frames asked to wait for the display, and *tearing allowed* how many did not mind tearing — what
  the game asked; your graphics driver can still override it.
- **GPU clocks**, the fan and the video memory's temperature are the card's own readings. **GPU held back** says how much
  of the time its power limit or its temperature slowed it down (NVIDIA cards only). **Frames per watt** is the game's
  own frames per second for each watt the graphics card drew — generated frames never count.

## Memory

- **VRAM (this game)** is the game's own video memory — what Task Manager's Details tab calls *Dedicated GPU memory* for
  the game's process. **RAM (this game)** is its *Memory (private working set)*. Both are read from Windows once a
  second, from outside the game, whether or not the game was hooked.
- The **median** is the typical level during the session; the **peak** is the highest it reached.
- *VRAM, whole graphics card* and *RAM in use, whole PC* count everything else running too, so they are usually much higher.
- **Statistics per series**, at the bottom of the summary, lists the mean, median, minimum and peak of every sensor.

## Ray tracing

**Ray tracing**, **path tracing** and **ray reconstruction** read *Yes*, *No* or *N/A*. Ray tracing is measured on
DirectX 12 games; path tracing is never stated as a fact, because nothing in a game's calls says so. Click a chip to
correct it by hand — your correction is labelled as yours.

## Charts

- **Frametime** — how long each frame took. Spikes are stutters.
- **Distribution** — how often each frame rate occurred.
- **Render scale** — the share of the screen's resolution the game rendered at before upscaling (100% means no
  upscaling), and when it changed.
- **Sensors** — temperatures, load and power; video memory; memory; and the card's clocks and fan, with the moments it
  was held back shaded. The game's own line and the whole card's or the whole PC's are drawn separately.
- **Trend** (on the game's page) — one point per session over time, for example before and after a driver update.
  Changes to your hardware or driver are marked on it.
- **Compare** (in the navigation on the left) — pick sessions, even of different games, and see them side by side.

To look closer, hold **Ctrl** and turn the mouse wheel over a chart to zoom, drag to pan, and double-click to reset. The
wheel alone scrolls the page.

## Export

The summary's **Export CSV**, **Export JSON** and **Export PNG** buttons — or File ▸ Export — save a session to a file.
Read [Your data](05-your-data.md) before you share one.
