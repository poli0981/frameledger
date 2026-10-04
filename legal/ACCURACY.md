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
> ⚠ **What FrameLedger actually measures today — 2026-10-04.** The software is a beta; its latest
> **pre-release is `0.1.0-beta.13`** (2026-10-04), an unsigned installer built from that tag with its
> checksums published beside it. The source holds the desktop app
> (library, store import, charts, settings) and the background Agent, which records a session when a
> game in the library runs (unless you switched its recording off), injects only into games you enabled
> and only past the safety guard, and
> stores sessions in a local database. What that path measures:
>
> - **Frame times and output resolution:** measured from the present call for Direct3D 11/12 and
>   OpenGL titles FrameLedger injects into. Vulkan titles are measured only when FrameLedger itself
>   starts the game with its Vulkan layer enabled, which today only the Agent's command-line launch
>   does. A Vulkan title started any other way is not observed by the layer; FrameLedger injects its
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
> - **Quality preset:** not reported on any title measured. The one route this software may use — the
>   DLSS options a Streamline title passes with its per-frame call — has not been seen on a real
>   title, and AMD's per-frame call carries no preset.
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
>   the share of the session they cover; with no such state, the reason is shown instead. DLSS-G and FSR
>   FG are named from the calls the game makes; any other generator (XeSS-FG, one compiled into the game)
>   is shown only as active with the technology not identified, and is never named. Where a technology is
>   named but its frames could not be counted, or frame generation is not measured, the software shows
>   **Presented FPS** with a note on what it may include — never a Native figure. Where generated frames
>   were counted, the lows, median and stutter are taken over application frames only.
> - **Ray tracing:** Yes/No measured on Direct3D 12 from ray-dispatch and acceleration-structure-build
>   calls; the technique and path tracing are `N/A`, and so is ray tracing on other APIs.
> - **Display mode:** how each game's window was shown — exclusive full-screen, borderless, windowed or
>   minimised — as a share of the session, with the window's size and the monitor's, in every session.
>   Exclusive full-screen is known only for Direct3D titles FrameLedger injects into, from the game's own
>   swap chain; for every other session the window alone is read, and a window covering its monitor reads
>   *full-screen or borderless*. What Windows does behind the game (fullscreen optimizations) is not visible.
> - **Engine version:** the exact Unreal Engine 4 or 5 version, read from the game's executable on disk and
>   labelled with what it rests on; `N/A` where the executable names none. Other engines show what their own
>   files state.
> - **The game's own memory (since beta.12):** its dedicated and shared GPU memory and its private
>   working set — the numbers Task Manager's Details tab shows — with its working set and commit, read
>   once a second from Windows' own counters, from outside the game, in every session: a game
>   FrameLedger injects into through the handle it already holds, any other by opening it briefly with
>   the least access Windows offers (and adding up every process running its executable). Shown with
>   mean, median and peak. The game's other processes (launchers, helpers) are not counted.
> - **Video memory:** in use on the whole graphics card, recorded from Windows and GPU-driver telemetry
>   and charted. **Not measured at all:** the video-memory budget Windows gives a game, which frame
>   spikes were shader compilation, PC latency (Reflex), and HDR. Stutter count and stutter time are
>   measured from frame times.
> - **Processor and memory:** how busy the processor was (time busy, all cores together — not the
>   frequency-scaled figure Task Manager draws) and how much system memory was in use, read from
>   Windows once a second and charted. Processor temperature is read only when the Agent runs as
>   administrator — an option, off by default, for which Windows asks at every start of the Agent — with
>   PawnIO installed, and that reading has not yet been checked on real hardware; everywhere else it is `N/A`.
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
