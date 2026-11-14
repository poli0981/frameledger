# What FrameLedger cannot do

FrameLedger measures a game by loading a small component into it — only for a game you added and switched
hooking on for — and reading the graphics calls the game makes. That is why it can tell the real render
resolution, the upscaler, frame generation and ray tracing apart. It is also where its limits come from. This
page lists them in plain words; each item links to the document that has the details.

*Beta software: this page describes version 0.1.0-beta.47.*

## Games it will not measure

- **Games with anti-cheat.** FrameLedger looks for anti-cheat before it touches a game and refuses when it
  finds one — there is no switch that overrides this. The one exception is an anti-cheat that runs entirely
  inside the game (no driver, no service), which you can allow per game after a warning; its first two
  sessions are a trial, and one that does not go well ends the exception for good. A ban is still possible and
  may come days later. Kernel anti-cheat is never allowed.
  ([Safety](docs/19_SAFETY_AND_ANTICHEAT.md))
- **Anti-cheat FrameLedger does not know about.** The list of anti-cheat products is FrameLedger's own and
  can be incomplete. A game protected by something unknown may be hooked; that is a risk to your account
  that you take by switching hooking on.
- **32-bit games and Direct3D 9 games.** Only 64-bit Direct3D 11, Direct3D 12 and OpenGL games are measured
  from their own calls; a 32-bit or Direct3D 9 game is recorded without measurement.
- **Vulkan games, unless FrameLedger starts them.** A Vulkan game is measured only when FrameLedger launches it
  with its Vulkan layer switched on — **Play** set to *Start directly* does; one started another way is not observed by
  the layer.
- **Games that run as administrator,** unless you turn on *Run the agent as administrator* in Settings
  (off by default; Windows asks every time the agent starts). **A process Windows protects** stays closed
  either way.
- **Vulkan games that run as administrator.** Windows' Vulkan loader does not load FrameLedger's Vulkan
  layer into an elevated game, so admin mode does not help there.
- **A game that was already running when FrameLedger was updated** — restart the game once.
- **Games you did not switch hooking on for.** Adding a game only records its sessions; measuring it is a
  separate, per-game choice with its own warning.

## What an unmeasured session contains

When a game is not measured — any of the reasons above — its session still records **how long it ran, the
machine's sensors (GPU temperature, load, power; CPU load; memory), how much memory the game itself used (read from
Windows, outside the game) and why nothing was measured.** Every
frame-rate, upscaler, frame-generation and ray-tracing value reads **N/A**, never 0. How the game's window was
shown is recorded too, from outside the game. ([Capture](docs/04_CAPTURE.md))

## When a game crashed

- A session reads **crashed** when the game ended with an exception's code, when Windows logged a crash report of it,
  or — since 0.1.0-beta.14 — when it started its engine's crash reporter (Unreal's, CD PROJEKT RED's, Capcom's,
  BugSplat's) and exited within a minute. A game that handles its own crash quietly, starts no reporter and leaves no
  crash report reads **ended normally**, with its exit code.
- A crash reporter is seen from a once-a-second look at running programs: one that opens and closes faster can be
  missed, and one started in the game's first 10 seconds is not counted (it could be a monitor that starts with the
  game). Unity's crash handler and Chromium's `crashpad_handler` start with every game that has them, so their presence
  says nothing. Sessions recorded before 0.1.0-beta.14 keep how they were stored — a few of them may read *crashed*
  because Windows logged a memory report about the game. ([details](docs/04_CAPTURE.md))
- **A game that faults while it is closing can read *ended normally* in a session measured from inside the game.**
  FrameLedger looks for Windows' crash report when it saves the session, and for such a session that is before
  Windows has written it; the same fault in a session that was not measured from inside reads *crashed*. Seen on one
  game whose graphics-driver library faults at every exit, measured or not (2026-11-12), and on a second whose
  Steam crash handler faults at every measured exit. Not changed yet.

## Frame rate and frame generation

- **One inflated number is never shown.** When frame generation is measured, the page shows the game's own
  frames and the displayed frames together (`62 → 118 FPS (×1.9 FG)`). When it cannot be measured, the page
  shows **Presented FPS** with a sentence saying whether generated frames could be included.
- **Frame generation done by the driver outside the game** (AMD Fluid Motion Frames, NVIDIA Smooth Motion)
  is not visible to FrameLedger; the rates shown are what the game presented.
- Frame generation is counted on **Direct3D games** only. DLSS Frame Generation and FSR frame generation are
  named from the calls the game makes; any other generator is shown only as *active*, never named, and where
  the generated frames cannot be counted the page shows Presented FPS with a note instead of a factor.
- **A session whose frame generation changed** (menus, loading, a settings change) shows the factor of the
  state it spent most of its time in, with the share of the session it covers — never an average of the two.
  ([Metrics](docs/03_METRICS.md))
- **The mix of frame-generation states** in a session's summary (since 0.1.0-beta.24) is read over five-second
  windows: frame generation that changes faster than that — the NVIDIA App's *Dynamic* mode can — reads partly as
  *Not classified*: a window in which the multiplier stepped reads between two whole multipliers and, since
  0.1.0-beta.27, is never a state of its own. A state shorter than 10 seconds is not listed. A stretch the game
  presents with almost no frame
  count of its own — shaders compiling at start, a loading screen, a menu drawn outside frame generation — reads as
  *Not classified* too. The
  multiplier chosen in a game's own menu is not read; the measured factor is its evidence. How the NVIDIA App's
  *Dynamic* ceiling and *Max refresh rate* target are stored by the driver is read from NVIDIA's own setting names,
  and has been checked against the App on one game. Sessions recorded before 0.1.0-beta.24 have no mix.
  ([Metrics](docs/03_METRICS.md))
- **Time below 30, 60 FPS and below the refresh rate** (since 0.1.0-beta.14) leaves out the small wobble of a game
  held at its limit: a frame counts as below 60 when it took 10 % longer than 1/60 s, or when the frames around it ran
  2 % slower. So a game running steadily just under the line — 59 FPS against 60 — reads partly as at it. Time below
  the refresh rate needs the monitor's rate, which a session saved after a crash does not have.
- **VSync and tearing** (since 0.1.0-beta.14) are what the game *asked* for on each frame; the graphics driver or a
  variable-refresh display can override it, and FrameLedger cannot see which won. OpenGL and Vulkan games do not say,
  and read N/A. ([Metrics](docs/03_METRICS.md))

## Upscaling and ray tracing

- **The render scale chart** (since 0.1.0-beta.14) is drawn only where the render resolution was measured — the
  same games as *Render → output resolution* in the accuracy statement; elsewhere it says it was not measured.
- The upscaler is read from the game's calls: DLSS and NIS through NVIDIA Streamline, and FSR through AMD's
  FidelityFX libraries. **Intel XeSS is not read** (its licence forbids it), FSR 2 and upscalers built into the
  game are not identified — Cyberpunk 2077's FSR 2.1 is one: its sessions say "unknown" beside a measured render
  size — and Vulkan and OpenGL games read N/A. A game that sends FSR upscaling through two unrelated libraries in
  one run is counted from the first alone (none is known). Where no hook saw DLSS but the NVIDIA driver
  reports it, the page says it is the driver's report.
- **The quality mode** (Quality, Balanced, DLAA, …) is shown only when it is known: the NVIDIA App's override
  where the NVIDIA driver reports it applied (marked *· NVIDIA App*, since 0.1.0-beta.24), or the mode the game
  passed to a call FrameLedger reads. For FSR through AMD's FidelityFX libraries (since 0.1.0-beta.25) that is the
  mode whose render size the game asked AMD's library for and then rendered at — checked on a test program, not yet
  on a real game; a game at a dynamic resolution, one that sizes its frames without asking, and a game FrameLedger
  joined after it asked show none. For DLSS it is the options a Streamline game passes, which no game has done yet.
  The mode chosen in a game's own menu is not readable for DLSS without Streamline, and is not inferred from the
  render scale. How the driver numbers DLAA in
  its report is NVIDIA's documented value, not yet seen on a real game. The render resolution is N/A where the
  game's own calls do not carry it (DLSS without Streamline, for one). Sessions recorded before 0.1.0-beta.27 kept
  the NVIDIA driver's answer from after the game had closed, which never says DLSS ran: on a game no hook names, they
  show neither the driver's report nor the NVIDIA App's mode. ([Metrics](docs/03_METRICS.md))
- **Which DLSS ran** (since 0.1.0-beta.24) is named only from what the session showed: the model preset the NVIDIA
  driver reports (L or M: DLSS 4.5; J or K: DLSS 4), a measured multi-frame factor (×5 or ×6: DLSS 4.5; ×3 or ×4:
  DLSS 4), the NVIDIA App's *Dynamic* frame generation that the driver reports applied (DLSS 4.5, whatever multiplier
  it chose — since 0.1.0-beta.34), or a library whose major version names one (2.x, 3.x). A *Fixed* ×5 or ×6 set in
  the App names DLSS 4.5 only when the frames show it. A 310 library with no such evidence shows its
  version only. **DLSS 5**
  (since 0.1.0-beta.34) is named where the game was seen running its neural rendering through Streamline, with the share
  of rendered frames; its library loaded and nothing seen reads *runtime loaded* — *not seen running* only where the
  Overlay looked. A game that runs neural rendering outside Streamline is not seen. Where each
  library was loaded from is read from its folder, for sessions recorded since 0.1.0-beta.24.
- **Ray tracing** is *Yes*, *No* or *N/A* — measured from the game's ray-tracing calls on Direct3D 12, and
  N/A where FrameLedger could not look. **Path tracing is never stated as a fact**: there is no API signal
  for it. You can correct any of these by hand, and your correction is labelled as yours.
- HDR output, end-to-end latency (click to screen), the video-memory budget Windows gives a game, and
  shader-compilation stutter are **not measured yet** — they read N/A. ([Accuracy](legal/ACCURACY.md))
- **NVIDIA's own latency report** is read only with *Advanced metrics* on, only on an NVIDIA card, only in a
  Direct3D 12 game that uses NVIDIA Reflex: how long the driver says each stage of a frame took, about once a
  second. They are the driver's numbers, not FrameLedger's measurement. **The stages overlap and do not add up to
  the total**, and the graphics card's stage includes the time the card waited — in one game it is longer than the
  whole frame; the driver's own figure for the card's active time is shown beside it where the driver gives one.
  FrameLedger asks the driver for them from inside the game; a read that is slow or faults is switched off until the game is closed, and the session says so.
  ([Advanced metrics](guide/12-advanced-metrics.md))

## The game's memory

- **What is shown is what Task Manager's Details tab shows** for the game's process — *Dedicated GPU memory*,
  *Shared GPU memory* and *Memory* (the private working set) — read from Windows once a second, from outside
  the game. FrameLedger never looks inside the game's memory to get it.
- **One process.** A measured game is its own process; for a game that is not measured, every process running
  the game's executable is added together. Launchers, crash reporters and other helper programs are not counted.
- **The private working set needs a recent Windows**: Windows 10 22H2 or Windows 11 with the September 2023
  update or later. Without it the working set is shown instead, labelled as such.
- A game that has not created anything on the graphics card yet (its first second, a launcher screen) reads
  N/A for video memory. AMD and Intel graphics cards have not been tested.
  ([Metrics](docs/03_METRICS.md))

## Display mode (full-screen, borderless, windowed)

- **Exclusive full-screen is known only for a measured Direct3D (DXGI) game** — the game's own swap chain
  says so. For OpenGL, Vulkan and unmeasured games, a window covering the screen reads *Fullscreen or
  borderless*, never either one.
- **What Windows does behind the game is not visible:** a game in exclusive full-screen may still be shown
  through Windows' compositor ("fullscreen optimizations"), and a game's menu may call a borderless window
  "Fullscreen". FrameLedger reports what the game asked for.
- The mode is sampled ten times a second in a measured session and once a second in an unmeasured one; very
  short switches can be missed. A session recovered after a crash, and every session recorded before
  beta.10, read N/A. Some sessions recorded before 0.1.0-beta.27 of a game in exclusive full-screen show no window or
  monitor size: the game's last moments, after its window had closed, replaced them.
  ([Display mode](docs/03_METRICS.md#display-mode--both-tiers-beta10))

## Engine version

- The exact version is read for **Unreal Engine 4 and 5** games, from the game's executable file on disk.
  A studio can remove or rename what it is read from; then the page says the version was not found.
  **Unreal Engine 3 and older are not recognised.**
- Since beta.19 also **Unity**, **Godot** and **RPG Maker** (MV, MZ, XP, VX, VX Ace): from the engine's own files —
  Unity's player library or data file, Godot's data pack, RPG Maker's core script or the runtime library its `Game.ini`
  names. A Unity game whose data file is encrypted shows the version without its release letter (`2019.4.40`, not
  `2019.4.40f1`); an XP, VX or VX Ace game shows its runtime library's number, which is not the editor's.
  **RPG Maker 2000 and 2003 are not recognised.**
- Since beta.20 also **Ren'Py**: from the engine's own version file (`renpy/vc_version.py`); a Ren'Py game whose engine
  names no version there shows none. Other engines show a version only where their files state one.
  ([Detection](docs/05_DETECTION.md))

## Sensors

- GPU temperature, load, power and memory are best supported on **NVIDIA** GPUs. **AMD and Intel GPUs have
  not been tested** on real hardware yet.
- **On a PC with more than one graphics card**, the GPU figures follow the card the game draws on once a Direct3D game
  has shown its first frame; before that, in an unmeasured session, and for OpenGL or Vulkan games they describe the
  first card Windows lists, and with two cards of one brand the temperature and load come from that brand's first card.
  The session's hardware details always name the first card. None of this has been tested on a machine with two cards.
  ([Telemetry](docs/18_GPU_VENDOR_APIS.md))
- **CPU temperature** needs *Run the agent as administrator* and the separately installed PawnIO driver, and
  has not been verified on real hardware yet. CPU load is the time the processor was busy — not Task
  Manager's "utility" figure, which can exceed 100%. ([Telemetry](docs/18_GPU_VENDOR_APIS.md))
- **Advanced metrics** (since 0.1.0-beta.39; Settings ▸ Recording, off by default) stores more of what the graphics
  driver and the sensors report with each session. Its limits:
  - **A session recorded while it was off is not filled in later**, and switching it on applies from the next session.
  - **What a game tells DLSS and FSR** (since 0.1.0-beta.40) is kept only for a game FrameLedger measures from
    inside, and only where the game hands it over in a call FrameLedger intercepts. **Most games set their DLSS
    options through a call it cannot see and show nothing**; the FSR rows need FSR 3.1 or later through AMD's own
    library. The session says which it was — nothing seen, or not measured inside the game — and neither means the
    game uses no upscaler. They are the game's words, not a check of what the upscaler did; the field of view is a
    lowest and highest value, never a line over time; FSR's memory is a point where the game asked, so most sessions
    have none. FSR's upscaling values have been read from real games; **the DLSS options have not been confirmed on
    one yet.**
  - **What a game tells Streamline each frame, and how it configures FSR frame generation** (since 0.1.0-beta.43)
    are read from two calls FrameLedger hooks **only once a session with Advanced metrics on has started in that
    game** — a session recorded with the option off puts neither hook in. Streamline's values are those of **the
    first viewport that called**: a game with several (split screen, a picture-in-picture view) shows one, and the
    count of calls covers all. The frame-generation rows are **what the game asked for** — a game that asks for
    generation and gets none still reads "on", and the session's summary is where what was generated is measured. A
    setting the game changed during the session reads "on and off". The session's line says, for each of the two,
    whether it was read, hooked and never called by the game, or not hooked (the game did not load the library).
    Streamline 1.x is not read. Streamline's values have been read from three real games and the frame-generation
    configuration from one (2026-11-12); **a configuration that reaches FSR through AMD's 2.3 loader has not been
    read from a real game yet.** **A game may give a value in a unit of its own**: Cyberpunk 2077 gives Streamline
    its field of view in degrees where NVIDIA's header says radians. A field of view too large to be radians is
    shown as the game gave it, with a line saying so, and is never converted (since 0.1.0-beta.44; a session
    recorded before keeps the line it was stored with, which labels the degrees as radians). The count of resets is
    the number of calls in which the game said so — menus and loading screens raise it.
  - **Which version each Streamline feature is, and what it requires** (since 0.1.0-beta.44) are Streamline's
    answers to two questions a game asks while it starts — and, in the games run so far, again later. FrameLedger
    hooks the two calls only once a session with Advanced metrics on has started in that game. **A game that asks
    only at its start shows nothing here**, and the session's line then says the call was hooked and the game did
    not ask again; the four Unreal Engine games run on 2026-11-12 asked again and were read, and Cyberpunk 2077
    asked again for the requirements and not for the versions. A second session of a game that kept running keeps
    what the first one read. The rows are Streamline's answer as the game received it — the versions
    of Windows and of the driver are the ones Streamline found — not FrameLedger's own check of the PC. Twelve
    features are kept; a thirteenth is not. The lists the answer points to (the buffers, the Vulkan extensions) are
    never read, so the rows give how many buffers a feature needs and not which. **The version Streamline gives for
    a feature can differ from the file in the game's folder** — three games answered 2.14.16 for DLSS frame
    generation while shipping an older plugin; which file that newer one is was not looked into. The games run so
    far do not ask Streamline about DLSS upscaling unless they upscale through it, so a game can list frame
    generation here and no upscaler.
  - **Whether each Streamline feature is supported and loaded, and what the game did with it** (since
    0.1.0-beta.45) come from six more calls, hooked the same way — only once a session with Advanced metrics on has
    started in that game. **What the game did before that is not seen**: a feature it switched on while it started
    has no "switched" row, and resources it allocated then are not counted. The counts are **calls the game made**,
    not resources, and each stops at fifteen (shown as `≥ 15`); they and the switching start again with each
    session, while the two answers are kept from an earlier session of a game that kept running. "Supported" is
    Streamline's answer for the graphics card the game asked about; which card that was is never read, so on a PC
    with two cards the row does not say. A refusal is shown by NVIDIA's own name for it
    (`eErrorFeatureNotSupported`), and it is the game that was refused, not FrameLedger. None of this says a
    feature ran or drew anything. **Read on five games on 2026-11-13**: each asked whether its features are
    supported, three asked whether they are loaded, two switched features off and on, one freed a feature's
    resources once and one shut Streamline down once. **None allocated a feature's resources after FrameLedger was
    in it**, so that row has not been seen in a real game.
  - **What a game started Streamline with is not read, and will not be**: the game makes that call once, before
    FrameLedger can be inside it — also when FrameLedger starts the game itself. The Advanced page lists the entry
    with that reason.
  - **The driver's own figures** — the card's firmware and architecture, its memory in detail, what a display can do
    with HDR and variable refresh, the driver's settings for the game — exist on **NVIDIA** cards only. On other cards
    they read N/A; the card's sensors (fans, voltages, loads) are read on every brand, and none has been tested on an
    AMD or Intel card.
  - **The sensors are read every five seconds**, NVIDIA's figures every second; between two readings a sensor has no
    value, and its last one is not repeated.
  - **The processor's, the memory's and the motherboard's sensors** need *Run the agent as administrator* and the
    separately installed PawnIO driver, and **have never been read on real hardware**. Without them they read N/A
    with the reason.
  - **A drive's temperature is not read at all**: the sensor library cannot list drives without touching removable
    ones. Power supplies, fan controllers and batteries are not read either.
  - **No serial number and no display identification data (EDID) is read**, so two PCs with the same parts look the
    same.
  - **None of these readings has been checked against another tool**, and they are **not in an export or a bug
    report**.
  - **The Advanced page's "This PC" is read only while that page is open** (or a game is being recorded), is not
    stored, and keeps five minutes of each reading in memory. A reading with more than eight parts — every
    processor core — is drawn as its lowest, average and highest; the table lists each part.
  - **A session's chart needs its per-second series**, which are kept for the newest sessions of each game only
    (Settings ▸ Recording); an older session keeps its statistics.

  ([Telemetry](docs/18_GPU_VENDOR_APIS.md#advanced-metrics-owner-decisions-d101d108-2026-11-11))
- **Whether the graphics card was held back by its power limit or by heat** (since 0.1.0-beta.14) is known only on
  NVIDIA cards, and only as it was at each once-a-second reading. On other cards it reads N/A.
- **Frames per watt** (since 0.1.0-beta.14) counts the game's own frames — never generated ones — against the graphics
  card's power alone, not the processor's or the whole PC's. It reads N/A where the game's own frame rate is not known.
  ([Metrics](docs/03_METRICS.md))

## Running the agent as administrator

Off by default. When on, **Windows asks every time the agent starts** and names an unknown publisher, because
FrameLedger is not signed. It lets FrameLedger open games that run as administrator and read the CPU
temperature — with Advanced metrics on, also the processor's, the memory's and the motherboard's sensors — where
PawnIO is installed; it does not bypass anti-cheat, does not open protected processes, and does not make games you
start from FrameLedger run as administrator. **The risk:** FrameLedger is installed in your user folder, so a
program running under your account could replace its files and would then run as administrator the next time
you accept the prompt. Turn it on only if you trust everything that runs under your account.
([Architecture, ADR-9](docs/01_ARCHITECTURE.md))

## Platform and what has been tested

- **Windows 11, 64-bit only.** FrameLedger is built on .NET 11 since 0.1.0-beta.37, which Microsoft supports on
  Windows 11 and, on Windows 10, only in its LTSC and IoT editions. **Windows 10 22H2 Home and Pro are not blocked,
  not tested and not supported**: FrameLedger may run there, and nobody has checked that it does
  ([Build](docs/12_BUILD.md)).
- **It does not start at all on a processor without SSE4.2 and POPCNT** — .NET 11's requirement, which every
  processor Windows 11 accepts meets. On such a PC an update from 0.1.0-beta.36 or older installs and then does not
  start; installing 0.1.0-beta.36 again brings FrameLedger back, with your data as it was.
- The games FrameLedger has actually been run with, and what was measured on each, are listed in
  [LIST_GAME_TESTED.md](docs/LIST_GAME_TESTED.md). Everything was measured on one machine (an NVIDIA
  GeForce RTX 5080 with an Intel Core i7-14700KF on Windows 11); other hardware is untested.
- **One real Vulkan game has been measured** (Everhex Demo, 2026-10-13) **and no OpenGL game**, beyond FrameLedger's
  own test program.
- There is **no in-game overlay**; everything is shown in the FrameLedger window.
- **FrameLedger does not tell whether a game is safe to run.** It reads a game's files to find its engine and its
  anti-cheat, never to judge it, and recording or measuring a game vouches for nothing. Four itch.io downloads this
  project once used as examples turned out to carry malware ([details](docs/spike-notes.md)).

## Accessibility

What works with the keyboard alone, a screen reader, a contrast theme or Magnifier, what does not yet, and how to report
a barrier is on its own page: [Accessibility](ACCESSIBILITY.md) (in the app: Help ▸ Accessibility). The largest limits:

- **A chart's points cannot be read one by one.** A screen reader hears its name and where its numbers are written —
  the session's figures, the tables, **File ▸ Export**; its keys move it.
- **Windows' text size setting is not followed** — use display scaling or Magnifier.

## Play

- **Through GOG GALAXY or itch, you finish in their app.** GOG GALAXY runs the game as its own shortcuts do, and the
  itch app asks which copy to launch. The Epic Games Launcher's link is the one its own shortcuts carry, and has not
  been tried on a real Epic game yet.
- **FrameLedger never starts a game as administrator.** *Start directly* does not start a game that asks for it — start
  that one from its store or its own shortcut — nor a game whose recording is off: FrameLedger starts only what it
  records. While FrameLedger itself runs as administrator, Play starts every game directly, with your standard rights.
- **Some games will not start directly** (seen on two Steam games, 2026-10-19 and 20). Lies of P, started by *Start
  directly*, hands itself to Steam within seconds — since 0.1.0-beta.27 the game Steam then starts is recorded —, and
  Clair Obscur: Expedition 33 closes within seconds, with or without FrameLedger's hook. Each such start ends a
  measured session early, which the crash safety counts: two turn hooking off for the game — and since
  0.1.0-beta.44 the count starts again each time you switch hooking back on (before, one more such end was enough
  once a game had been switched off). Start such a game through
  its store (Settings ▸ Games ▸ *Play button*); the notice after Play says when a Steam game may hand itself to Steam.
  ([details](docs/spike-notes/23-owner-runs-2026-10-20.md))

## Xbox app and Microsoft Store games

- **Only games built on Microsoft's GDK** (since 0.1.0-beta.33) — what the Xbox app and the Microsoft Store install into
  a games folder you chose, PC Game Pass games included — are found by **Import library**. An older Store app (UWP) is
  not listed, and a game Windows has not installed for you, such as one left behind on a drive, is not offered.
  ([details](docs/spike-notes/25-xbox-gdk.md))
- **Their executables cannot be read by other programs** — Windows refuses it to anything outside the game's own
  package — so FrameLedger shows no Unreal Engine version and no *Runs as* for them, and says what it could not read
  rather than guessing. A Unity game's version still reads, from its player library.
- **The anti-cheat list's per-game entries know Steam ids only.** An Xbox copy of a game listed there by its Steam id is
  checked like any other game — its files, its executables, the anti-cheat drivers and services on your PC — but not
  by that entry. ([Safety](docs/19_SAFETY_AND_ANTICHEAT.md), [open question](docs/20_OPEN_QUESTIONS.md))
- **Play starts them through Windows, in either Play mode** — as their Start-menu tile does — because Windows starts
  such a game only through its package: *Start directly* cannot start one, so it is recorded once it appears, a few
  seconds after its start, and the first frames before then are not measured. ([Play](docs/08_UI.md),
  [capture](docs/04_CAPTURE.md))
- **An older Store app (UWP), and a game Windows runs under code protection the hook cannot live under** — only
  Microsoft-signed code, no generated code, strict stack or control-flow protection — **is never hooked**: its session
  keeps its duration, your PC's sensors and the reason. Measuring a game from the Xbox app with the Vulkan graphics API
  has not been tried. ([Safety](docs/19_SAFETY_AND_ANTICHEAT.md), [open question](docs/20_OPEN_QUESTIONS.md))

## Game artwork

- **Only the Steam and GOG GALAXY apps keep pictures FrameLedger can show** (since 0.1.0-beta.23) — **and, since
  0.1.0-beta.33, an Xbox game ships its own**: its square logo and its splash image, shown from its own folder. A game's
  page shows the cover and banner those apps keep on your PC for a game they installed, read where they keep them —
  nothing is downloaded or copied. A game from itch, the Epic Games Launcher or another store, or one whose `.exe` is
  outside a store's own folder, shows a picture only once you choose one (**Edit… ▸ Choose image…**). A GOG picture needs
  Windows' WebP support, which Windows 11 and most Windows 10 installs have. ([Game page](docs/08_UI.md))
- The pictures belong to the games' owners; FrameLedger ships none ([names and trademarks](legal/TRADEMARKS.md)).

## Your data

- Everything stays on your computer (`%LOCALAPPDATA%\FrameLedger`). FrameLedger sends no telemetry; its only
  network requests are the update check and the update download, both to GitHub, and the check can be switched
  off. The anti-cheat list and the detection rules ship with each version — **a newer list reaches you only with
  the next update.** ([Privacy policy](legal/PRIVACY_POLICY.md))
- Per-frame data is kept for the **last 20 sessions of each game** (configurable); older sessions keep their
  summaries.
- A newer version upgrades the database in place, and an **older version cannot open it again** — there is
  no downgrade.

## Installation

FrameLedger's programs are **not code-signed**: Windows SmartScreen may warn when you install it, and the
administrator prompt names an unknown publisher. Each release publishes SHA-256 checksums you can compare.

**An update waits while a game FrameLedger measured is still running.** The part of FrameLedger loaded into a game stays
loaded until the game exits, and Windows will not let the update replace it; *Restart to update* says which file is held
until you close that game ([details](docs/11_UPDATER.md)). Copies older than 0.1.0-beta.14 look only for stable
releases unless you choose *Beta* in Settings ▸ Updates, and apply a downloaded update the next time they start.

## Alpha builds

*Only for an alpha — a version ending in `-alpha.N`, an experimental build on a new .NET
([Alpha builds](guide/11-alpha-builds.md)).*

- **It is built to try a newer .NET — at times one Microsoft has not finished** — and has not been tested the way a
  beta is: everything on this page applies to it, and so can problems no beta has shown
  ([Alpha notice](legal/ALPHA_NOTICE.md)).
- **The alphas are paused since 0.1.0-alpha.4, and a paused alpha gets no updates** — the list of anti-cheat software
  it refuses included, which arrives only inside an update. The betas run on the same .NET 11 since 0.1.0-beta.37:
  install the latest beta over the alpha. Once ten betas have followed 0.1.0-alpha.4, an older alpha no longer finds
  it as an update ([Updater](docs/11_UPDATER.md)).
- **It shares the beta's data folder.** It copies the database into `backups` before it first opens it, and does not
  open it if the copy cannot be made; the newest three copies are kept and older ones it made are deleted.
- **It updates only to newer alphas**, and its update channel is locked; going back to the beta means installing the
  beta over it.
