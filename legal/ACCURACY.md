<!--
  THE ONE ACCURACY BLOCK. Embedded verbatim in README.md and legal/DISCLAIMER.md
  between the two HTML-comment markers named `accuracy-block:begin` and
  `accuracy-block:end` (spelled out in tools/accuracy-check.ps1 — not here, because a
  literal marker inside this comment would end the comment early), and that gate
  fails the build when any copy differs from this file
  (20_OPEN_QUESTIONS §S23-6: the accuracy blocks were hand-maintained prose that
  nothing verified, and DISCLAIMER's went stale four times in one direction and once
  in the other). Whether the sentences below are TRUE is still a human's job — the
  gate only makes there be one place to get them wrong. Re-read this file in the PR
  that changes what FrameLedger.Overlay or the capture host does; date the change.
  HTML comments are not part of the block.
-->
> ⚠ **What FrameLedger actually measures today — 2026-11-11.** The software is a beta; its latest
> **beta is `0.1.0-beta.48`** (2026-11-14), an unsigned installer built from that tag with its
> checksums published beside it (alphas — experimental builds that try a newer .NET — are published apart). The source holds the desktop app
> (library, store import, charts, settings) and the background Agent, which records a session when a
> game in the library runs (unless you switched its recording off), injects only into games you enabled
> and only past the safety guard, and
> stores sessions in a local database. What that path measures:
>
> - **Frame times and output resolution:** measured from the present call for Direct3D 11/12 and
>   OpenGL titles FrameLedger injects into. Vulkan titles are measured only when FrameLedger itself
>   starts the game with its Vulkan layer enabled, which the Play button set to *Start directly* (since
>   beta.21) and the Agent's command-line launch do. A Vulkan title started any other way is not
>   observed by the layer; FrameLedger injects its
>   Direct3D component into it instead, and what that records on a Vulkan title has not been
>   verified. Direct3D 9 and every 32-bit title are not measured.
> - **Which upscaler is running:** measured from the API a Direct3D title calls — DLSS or NIS through
>   NVIDIA Streamline (with Ray Reconstruction Yes/No), and FSR through AMD's shipped FidelityFX DLLs:
>   named FSR 3 from the older DLLs, and FSR without a version from the newer ones, which host both
>   FSR 3.1 and FSR 4. FSR 2, and FSR shipped any other way, is not identified. For DLSS titles that
>   bypass Streamline, the NVIDIA driver's own per-process record is stored with the session and shown
>   as *DLSS (driver-reported)*. The NVIDIA driver's profile for the game — the DLSS and frame-generation
>   overrides the NVIDIA App sets — is read, never changed, and shown as the driver's settings beside what
>   was measured. Intel XeSS is **not** read (its SDK licence
>   forbids it), and neither is an upscaler compiled into the game: those read `N/A`, or "Unknown
>   upscaler" when another vendor's hook ran in the game. Vulkan and OpenGL titles read `N/A`.
> - **Quality mode:** shown only when it is known. When the NVIDIA App overrides DLSS Super Resolution for
>   the game and the NVIDIA driver reports that the override applied in the session, the override's mode is
>   shown and marked as the NVIDIA App's. Otherwise the mode the game itself passed: the DLSS options a
>   Streamline title passes with its per-frame call (not yet seen on a real title), or, for FSR through
>   AMD's FidelityFX libraries, the mode whose render size the game asked for and then rendered at. In
>   every other case — FSR at a dynamic resolution among them — no mode is shown.
> - **DLSS version:** the version of each DLSS library the game loaded and whether it came from the game or from
>   the NVIDIA driver; "DLSS 4" or "DLSS 4.5" only when the session shows it (the model preset the driver reports,
>   a measured multi-frame factor, or the NVIDIA App's Dynamic frame generation that the driver reports applied);
>   "DLSS 5" only when the game was seen running its neural rendering, otherwise "runtime loaded" when its library was
>   loaded.
> - **Render → output resolution:** measured where the vendor's own per-frame call carries the render
>   size (AMD FidelityFX dispatches, and Streamline titles whose resource tags state it); `N/A` where it
>   does not: DLSS titles that bypass Streamline, Streamline titles whose tags carry no size, and Vulkan
>   and OpenGL titles.
> - **Frame generation (Direct3D titles only):** the Displayed rate is counted from presents, plus the
>   presents DXGI's own counter saw on the same swap chain that the hook did not; the Native rate from
>   the vendor's own per-frame calls (NVIDIA Streamline, AMD FidelityFX) where the title makes them, and
>   `N/A` where it does not. When frame generation did not stay in one state for the whole session
>   (menus, loading screens, a settings change), no session-wide factor is shown: the factor, Native and
>   Displayed are those of the state that held longest — five-second windows whose ratios agree within
>   10 %, covering at least 10 s and 10 % of the time the game was presenting — and are always shown with
>   the share of the session they cover; with no such state, the reason is shown instead. The session
>   summary lists each state it held, with its share of the time and its own Native → Displayed rates.
>   DLSS-G and FSR FG are named from the calls the game makes; any other generator (XeSS-FG, one compiled
>   into the game) is shown only as active with the technology not identified, and is never named. Where a
>   technology is named but its frames could not be counted, or frame generation is not measured, the
>   software shows **Presented FPS** with a note on what it may include — never a Native figure. Where generated
>   frames were counted, the lows, median and stutter are taken over application frames only.
> - **Ray tracing:** Yes/No measured on Direct3D 12 from ray-dispatch and acceleration-structure-build
>   calls; the technique and path tracing are `N/A`, and so is ray tracing on other APIs.
> - **Display mode:** how each game's window was shown — exclusive full-screen, borderless, windowed or
>   minimised — as a share of the session, with the window's size and the monitor's, in every session.
>   Exclusive full-screen is known only for Direct3D titles FrameLedger injects into, from the game's own
>   swap chain; for every other session the window alone is read, and a window covering its monitor reads
>   *full-screen or borderless*. What Windows does behind the game (fullscreen optimizations) is not visible.
> - **Engine version:** the exact Unreal Engine 4 or 5 version, read from the game's executable on disk and
>   labelled with what it rests on; `N/A` where the executable names none. Since beta.19 also Unity's (from its
>   player library or its data file — without the release letter where only the library's number is readable),
>   Godot's (from its data pack) and RPG Maker's maker and version (MV and MZ from their core script; XP, VX and VX
>   Ace by their runtime library's version, which is not the editor's). RPG Maker 2000/2003 is not detected. Since
>   beta.20 also Ren'Py's (from its engine's version file). Other engines show what their own files state.
> - **The game's own memory (since beta.12):** its dedicated and shared GPU memory and its private
>   working set — the numbers Task Manager's Details tab shows — with its working set and commit, read
>   once a second from Windows' own counters, from outside the game, in every session: a game
>   FrameLedger injects into through the handle it already holds, any other by opening it briefly with
>   the least access Windows offers (and adding up every process running its executable). Shown with
>   mean, median and peak. The game's other processes (launchers, helpers) are not counted.
> - **Video memory:** in use on the whole graphics card, recorded from Windows and GPU-driver telemetry
>   and charted. **Not measured at all:** the video-memory budget Windows gives a game, which frame
>   spikes were shader compilation, end-to-end PC latency, and HDR. Stutter count and stutter time are
>   measured from frame times.
> - **NVIDIA's latency report (only with Advanced metrics on; Direct3D 12; a game that uses NVIDIA Reflex):** how
>   long the driver says each stage of a frame took — simulation, render submit, present, driver, the operating
>   system's queue, the graphics card — read from the graphics driver about once a second and shown as the driver
>   reported it: **the driver's numbers, not a measurement by FrameLedger**, an average of the frames of one read,
>   never checked against another tool, and not the delay from a click to the screen. **The stages overlap and do
>   not add up to the total.** The graphics card's stage runs from its start to its end and includes the time the
>   card waited — in one game it was longer than the whole frame — so since beta.48 two figures the driver works
>   out itself are shown beside it: the card's active time, and the time from one frame's end to the next. A
>   driver may give neither, and then there is no line. Read in two real games, on one driver and one card
>   (2026-11-14). A game that does not use Reflex has none. The session summary's latency fields stay N/A.
> - **Pacing, VSync and the graphics card (since beta.14):** the share of the time below 30 FPS, below 60 FPS and below
>   the monitor's refresh rate, measured from frame times with small margins so that a game held at its limit is not
>   counted as below it (a rate less than 2 % under the line reads partly as at it), and how much one frame's time
>   differs from the next; for Direct3D titles, how many frames asked to wait for the display and how many allowed
>   tearing — what the game asked, not what the driver did; the graphics card's clock speeds, fan speed and memory
>   temperature in every session, and, on NVIDIA cards only, how much of the time its power limit or its temperature held
>   it back, read once a second; and frames per watt — the game's own frames per second for each watt the graphics card
>   drew, never counting generated frames, `N/A` where the game's own frame rate is not known.
> - **Processor and memory:** how busy the processor was (time busy, all cores together — not the
>   frequency-scaled figure Task Manager draws) and how much system memory was in use, read from
>   Windows once a second and charted. Processor temperature is read only when the Agent runs as
>   administrator — an option, off by default, for which Windows asks at every start of the Agent — with
>   PawnIO installed, and that reading has not yet been checked on real hardware; everywhere else it is `N/A`.
> - **Advanced metrics (since beta.39; off unless you switch it on in Settings):** with it on, a session also stores
>   what the graphics driver and the sensor library report about the hardware — the graphics card's memory, load,
>   clocks, fans, voltages and power in detail, what a display can do, the driver's settings for the game — as they
>   report it: NVIDIA's figures once a second, the sensor library's every five seconds. None of these readings has
>   been checked against another tool. The processor's, the memory's and the motherboard's need the Agent as
>   administrator with PawnIO and have never been read on real hardware; a drive's temperature is not read. The
>   Advanced page shows them as they were stored, and what the PC reads at the moment only while that page is open.
> - **What a game tells its upscaler (since beta.40; only with Advanced metrics on, only for a game measured from
>   inside):** the output size, presets and exposure a game hands DLSS, and the sharpening, jitter, exposure, depth
>   range, field-of-view range, flags and frame time it hands FSR, are read from the arguments of the calls
>   FrameLedger intercepts and shown as the game stated them — not checked against what the upscaler did. Most games
>   set their DLSS options through a call that cannot be intercepted and show none; the FSR values need FSR 3.1 or
>   later through AMD's own library. Since beta.43 also what a game tells NVIDIA Streamline about each frame (jitter,
>   motion-vector scale, depth range, field-of-view range, how often it reset the upscaler's history — for the first
>   viewport that called, where a game has several) and how it configures FSR frame generation (the settings it asked
>   for, not whether frames were generated). Since beta.44 also what Streamline answers when a game asks about a
>   feature — its version, and what it requires (graphics APIs, V-Sync, GPU scheduling, Windows and driver versions):
>   Streamline's answer as the game received it, not a check of the PC. A game that asks only while it starts has
>   none and the session says so; the Unreal Engine games run so far ask again later, and were read. Since beta.45
>   also whether Streamline says each feature is supported and whether it is loaded, and what the game did with it
>   after FrameLedger attached — switched it on or off, allocated and freed its resources (counted to fifteen), shut
>   Streamline down: calls the game made, not a statement that a feature ran or produced anything. FSR's upscaling
>   values, Streamline's constants (seven games), its answers about a feature (seven games), the
>   frame-generation configuration (one game) and five of beta.45's six calls (five games) have been read from
>   real games; no game run so far allocated a feature's resources after FrameLedger attached, and the DLSS
>   options have not been confirmed on one yet. A game may give a value in a unit of its own: one gives
>   Streamline its field of view in degrees, and it is shown as given.
> - **Safety:** every pre-injection check runs before injection, including the signed-by-a-known-vendor
>   half of the suspicious-module rule. During every capture the Agent re-runs the checks every 30 s and
>   stops capturing on a refusal: a Direct3D or OpenGL game's hooks are removed, and the Vulkan layer
>   goes passthrough. A global switch in Settings turns all hooking off, and a running capture stops at
>   its next check; it can only refuse, never permit. **There is no override.** A finding about a game — anti-cheat
>   named in its process or in its folder (any kernel driver there counts), or the game on a title list by its
>   store id or executable name — turns hooking off for that game, whether found by the Agent's own check of every
>   game in the library, before injection, at a session's start or by the 30 s re-check; the game's page says so,
>   and nothing turns it back on for that executable — except a user-mode exception you make yourself, per game,
>   in Settings (off by default): only where the only finding is one anti-cheat that runs entirely in user mode, with
>   no driver file in the game's folder. Its first two sessions are a trial, and one that does not go well ends it
>   for good. The guard still runs every check under it and ends it on anything new; a ban remains possible.
>
> Where a value is not measured it reads `N/A`, with two exceptions: FPS then shows Presented FPS with a
> note on what it may include, and ray-tracing flags may show a value you set yourself, labelled as
> yours. The software never substitutes an estimate.
