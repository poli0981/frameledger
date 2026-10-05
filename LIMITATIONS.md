# What FrameLedger cannot do

FrameLedger measures a game by loading a small component into it — only for a game you added and switched
hooking on for — and reading the graphics calls the game makes. That is why it can tell the real render
resolution, the upscaler, frame generation and ray tracing apart. It is also where its limits come from. This
page lists them in plain words; each item links to the document that has the details.

*Beta software: this page describes version 0.1.0-beta.13.*

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
  with its Vulkan layer switched on; one started another way is not observed by the layer.
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

## Upscaling and ray tracing

- The upscaler is read from the game's calls: DLSS and NIS through NVIDIA Streamline, and FSR through AMD's
  FidelityFX libraries. **Intel XeSS is not read** (its licence forbids it), FSR 2 and upscalers built into the
  game are not identified, and Vulkan and OpenGL games read N/A. Where no hook saw DLSS but the NVIDIA driver
  reports it, the page says it is the driver's report.
- **The quality preset** (Quality, Balanced, …) has not been reported on any game yet, and the render
  resolution is N/A where the game's own calls do not carry it (DLSS without Streamline, for one).
- **Ray tracing** is *Yes*, *No* or *N/A* — measured from the game's ray-tracing calls on Direct3D 12, and
  N/A where FrameLedger could not look. **Path tracing is never stated as a fact**: there is no API signal
  for it. You can correct any of these by hand, and your correction is labelled as yours.
- HDR output, NVIDIA Reflex latency, the video-memory budget Windows gives a game, and shader-compilation
  stutter are **not measured yet** — they read N/A. ([Accuracy](legal/ACCURACY.md))

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
  beta.10, read N/A. ([Display mode](docs/03_METRICS.md#display-mode--both-tiers-beta10))

## Engine version

- The exact version is read for **Unreal Engine 4 and 5** games, from the game's executable file on disk.
  A studio can remove or rename what it is read from; then the page says the version was not found.
  **Unreal Engine 3 and older are not recognised.** Other engines show a version only where their files
  state one. ([Detection](docs/05_DETECTION.md))

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

## Running the agent as administrator

Off by default. When on, **Windows asks every time the agent starts** and names an unknown publisher, because
FrameLedger is not signed. It lets FrameLedger open games that run as administrator and read the CPU
temperature; it does not bypass anti-cheat, does not open protected processes, and does not make games you
start from FrameLedger run as administrator. **The risk:** FrameLedger is installed in your user folder, so a
program running under your account could replace its files and would then run as administrator the next time
you accept the prompt. Turn it on only if you trust everything that runs under your account.
([Architecture, ADR-9](docs/01_ARCHITECTURE.md))

## Platform and what has been tested

- **Windows 10 22H2 or Windows 11, 64-bit only.**
- The games FrameLedger has actually been run with, and what was measured on each, are listed in
  [LIST_GAME_TESTED.md](docs/LIST_GAME_TESTED.md). Everything was measured on one machine (an NVIDIA
  GeForce RTX 5080 with an Intel Core i7-14700KF on Windows 11); other hardware is untested.
- **No real Vulkan or OpenGL game has been measured yet** — only FrameLedger's own test program.
- There is **no in-game overlay**; everything is shown in the FrameLedger window.

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
