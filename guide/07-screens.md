# The screens

What each part of FrameLedger shows and what each button does. What the numbers mean is in
[Read your results](03-read-your-results.md); every setting is in [Every setting](08-settings.md).

## The main window

**The badge at the top right** says how the capture agent is doing:

| Badge | Meaning |
|---|---|
| Agent connected | Everything works. |
| Connecting to agent… · Starting agent… | FrameLedger is reaching the agent, or starting it. Give it a few seconds. |
| Waiting for administrator permission… | *Run the agent as administrator* is on, and Windows is asking you. |
| Agent offline | The agent is installed but does not answer. FrameLedger keeps trying; **Retry** on the banner tries now. |
| Agent not installed | `FrameLedger.Agent.exe` is missing: install FrameLedger again. |
| Viewer — no Agent | You opened a copy of a data folder to look at ([When something goes wrong](09-troubleshooting.md#looking-at-a-copy-of-your-data)). |

**Banners** under the menus stay until you act on them: *Capture agent offline* with **Retry**; *Update* with **Restart to
update** (it waits while a game is being recorded); and the safety notices — a game's hooking refused, measurement
stopped, hooking turned off — each with one button that only says you have read it.

**The pages** are on the left: **Dashboard**, **Games**, **Compare**, **Logs**, and **Settings** at the bottom.

## The menus

| Menu | Item | What it does |
|---|---|---|
| **File** | **Add game…** | Pick a game's `.exe` file. It is added with hooking off, and its page opens. |
| | **Import library…** | A checklist of the games installed through Steam, GOG Galaxy, Epic and itch.io. |
| | **Export** ▸ **Session CSV** · **Session JSON** · **Chart PNG** | Saves the session you last selected or opened. Read [Your data](05-your-data.md) before you share a file. |
| | **Exit** | Quits FrameLedger, even when *Minimize to tray on close* is on. |
| **Tools** | **Agent status…** | Opens the Dashboard, where the agent's card is. |
| | **Update detection rules** | The agent reads its detection rules again and checks your games again. |
| | **Vulkan layer registration…** | Opens Settings, where the Vulkan layer is. |
| | **Open data folder** | Opens `%LOCALAPPDATA%\FrameLedger` in Explorer. |
| | **Database maintenance…** | **Check**, **Back up…**, **Sweep…** and **Compact** your database ([When something goes wrong](09-troubleshooting.md#the-database)). |
| **Help** | **User guide** | This guide. |
| | **Check for updates** | Asks GitHub now. |
| | **Report a bug…** | Builds a report on your PC ([When something goes wrong](09-troubleshooting.md#report-a-problem)). |
| | **Online documentation** | The project's page on GitHub. |
| | **Limitations** | What FrameLedger cannot do. |
| | **About FrameLedger** | The version, the licences and the notices. |

## Dashboard

- **Live capture** — the game being recorded right now: its name, **T1** (measured) or **T2** (recorded only), and the
  time so far. While it is measured: the frame rate, the resolution, the upscaler, frame generation, ray tracing, the GPU
  and CPU temperatures and the game's memory. *Attached — waiting for the first frames…* means hooking worked and the
  first frames are on their way.
- **Games tracked**, **Total playtime**, **Sessions this week**.
- **Capture agent** — *State*, *Agent version*, *Telemetry source* (which sensor readers work on this PC: `l1` is
  Windows itself, `lhm` is LibreHardwareMonitor, `nvapi` is NVIDIA's own and only on NVIDIA cards), *Overlay build* (the
  version of the component loaded into games), *Elevated* (whether the agent runs as administrator) and *Running
  sessions*. Everything reads N/A until the agent answers.
- **Recent sessions** — the ten newest. Click one to open its summary.

## Games

- **Search games**; sort by **Name**, **Last played** or **Playtime**; **Show as a list** or **Show as a grid**.
- **Refresh** reads your library again: it offers games newly installed through a store, and marks a game whose `.exe`
  is gone as *Not installed*.
- **Add game…** — or drop `.exe` files onto the page.
- Each game shows its name, its store and engine, and one state: **Hooking on**, **Hooking off**, **Anti-cheat** (found,
  so it is not hooked), **Exception** (a user-mode exception is in force) or **Not recorded**; then its sessions,
  playtime and last play. Click it to open its page (in the list: double-click, or Enter).

## A game's page

- **At the top:** the name, publisher, version, store and engine, the `.exe` path, the last session, the last measured
  session's frame rate and its ray-tracing chips. **Edit…** changes the name, publisher, version and notes. **Change
  executable…** points the entry at another `.exe` and turns hooking back off. **Remove…** asks whether to keep the
  game's sessions.
- **Recording** — off, FrameLedger ignores the program completely: for a launcher or a tool that is not a game.
- **Hooking** — on, the game is measured. A warning comes first, and you type `I ACCEPT THE INJECTION RISK` to go on.
  Read [Anti-cheat and safety](04-anti-cheat-and-safety.md) before you do.
- **Anti-cheat** — shown in its place when anti-cheat was found: what was found, and where. **User-mode exception**
  appears when the game qualifies for the one exception.
- **Supports** and **Details** — what the game's own files contain, as far as FrameLedger can tell.
- **Tabs:** **Sessions** (every session: double-click a row or press **Open**; **Delete all sessions…** at the top),
  **Frametime**, **Distribution**, **Trend** (one point per session for a figure you choose, with changes to your
  hardware or driver marked), **Sensors**, and **Latency** when the game reported it.

## A session's summary

It opens from a session row, from the Dashboard, and from the notice when a session is saved.

- A warning at the top when the game crashed or measuring stopped early.
- The **Ray Tracing**, **Path Tracing** and **Ray Reconstruction** chips: click one to correct it by hand, for this
  session or as the game's default. Your correction is labelled as yours.
- The figures, the charts and **Statistics per series** — [Read your results](03-read-your-results.md) explains each.
- **Tags and notes** with **Save**, and **Export CSV**, **Export JSON**, **Export PNG**.

## Compare

Tick **2 to 5 sessions**, from any of your games, and press **Compare**: one frame-rate curve per measured session, and
a table with the best value of each row in bold. Comparing a measured session with an unmeasured one asks first,
because they measured different things.

## Logs

What FrameLedger and its agent wrote down. Choose **App** or **Agent**, a level, and search; **Pause** stops the view from
following new lines. **Open logs folder**, and **Export bug bundle**, which does the same as Help ▸ Report a bug….

## Settings

Every switch and box is described in [Every setting](08-settings.md).

## The tray icon

The icon by the clock has **Open FrameLedger**, **Pause capture** / **Resume capture**, **Agent status** and **Exit**; a
click on it opens the window. **Pause capture** stops measuring the running sessions until you resume, and the paused
stretch is left out of their figures. While the window is hidden, a notice pops up when a session is saved or an update
is ready.

## Keyboard and mouse

- **Ctrl+1** … **Ctrl+5** open Dashboard, Games, Compare, Logs and Settings.
- **Alt** or **F10** moves to the menus.
- **Esc** closes a dialog the way its Close or Cancel button does.
- Over a chart, **Ctrl** and the mouse wheel zoom, dragging pans, and a double-click resets. The wheel alone scrolls the
  page.
