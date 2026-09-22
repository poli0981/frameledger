# FrameLedger — Disclaimer

**Version:** 2.5 · **Effective:** {{RELEASE_DATE}}

> **How this document is kept true.** The statement of what FrameLedger measures (§4) is `legal/ACCURACY.md`,
> embedded here and in `README.md` and bound to its source by `tools/accuracy-check.ps1`, which fails the build when a
> copy differs. Earlier versions of this document carried a hand-maintained audit of how its own promises drifted from
> the software — six re-counts between 2026-08-04 and 2026-09-15; that history is `docs/legal-drift-history.md`,
> moved out of the text you accept on 2026-09-16 and kept in full.

## 0. This is pre-release (beta) software

**FrameLedger is a beta.** Every build published so far is a pre-release. It has been tested on a small number of
machines and games, and **it certainly still contains bugs — some of them not yet known to anyone.** Features can
change or disappear between builds, recorded data can turn out to be wrong, and an update can require your data to be
migrated.

By installing or running a pre-release build you accept that:

- it may crash, hang, fail to record, or record wrongly, and may cause the game you are measuring to do the same;
- **the developer is not responsible for any incident, loss or damage that results**, including lost progress, corrupted
  saves, lost recordings, wasted time, or anything else described in this document;
- no support, fix, or response time is promised. Reports are welcome and are read; nothing is owed in return.

If that is not acceptable to you, do not use a pre-release build.

## 1. How FrameLedger measures (read this first)

To measure what a game is *actually* doing — its real render resolution, which upscaler is running at which quality preset, whether frame generation is active, whether rays are being traced — FrameLedger loads a component (`FrameLedger.Overlay.dll`) **inside the game process** and observes calls the game makes to graphics APIs. Vulkan titles use a standard Vulkan layer instead. This is the same class of technique used by widely-used tools such as frame-rate overlays, screen-recording software, and post-processing injectors.

None of this information is obtainable from outside the process, which is why the software works this way.

**FrameLedger only loads a library from its own installation directory.** The injection path refuses anything else, and there is no setting that changes this.

Be precise about what that does and does not promise, because the shorter version — *"only FrameLedger's own component is ever loaded"* — claims more than the check performs:

- It establishes **where the file came from**, not what is in it. There is no filename, version or content comparison, and that is deliberate: a check on the name `FrameLedger.Overlay.dll` would be satisfied by any library that borrowed the name.
- A FrameLedger installation contains **several hundred files**, because the application ships self-contained with the .NET runtime beside it. Any library in that directory satisfies the check.
- FrameLedger is distributed **unsigned**, so nothing attests to that directory's contents. Anyone able to write there could alter what gets loaded — and could equally replace the component that performs this check.

Install it somewhere only you can write to, and verify the published SHA-256 checksums.

**What it observes:** arguments the game passes to graphics APIs we intercept (presentation, upscaling, ray tracing, pipeline creation) and video-memory usage reported by the graphics runtime.

> **What of that list is observed today is stated once, in §4 below** (the block `legal/ACCURACY.md` carries, dated). The list here describes what the software is designed to observe, and the boundary — API arguments and nothing else — holds for all of it.

**What it never does:** read or write the game's memory outside those API arguments; read save files, input, chat, or network traffic; modify game behavior; hide itself from any security software; or install a kernel driver of its own.

## 2. Anti-cheat systems — the main risk to you

**Loading code into a game process can be detected by anti-cheat and anti-tamper systems, and may result in a warning, a block, or a permanent ban of your account.** This risk falls on you, not on the developer.

FrameLedger is designed to reduce that risk substantially:

- Injection is **off by default** and must be enabled by you **per game**.
- Before injecting, and every 30 seconds afterwards, FrameLedger scans for known anti-cheat and anti-tamper components. **If it finds one, it refuses to inject; if a session is already running, it stops at the next scan.** No setting turns this off, globally or per game (§2A). Because the scan runs every 30 seconds rather than continuously, anti-cheat that loads mid-session may be present for **up to 30 seconds** before FrameLedger detects it and stops.

  **And there is a second, longer window you should know about.** The part of FrameLedger running inside the game also stops on its own if it loses contact with the part doing the scanning — because a scanner that has stopped cannot protect you. It waits **65 seconds** before concluding that contact is lost, so that one delayed scan on a busy machine does not end your session. In the worst case those windows combine: if the scanner stops at the moment anti-cheat appears, the component inside the game may keep running for up to **65 seconds** afterwards. That number is 65 and not 30, and this document says so rather than leaving the 30 above to imply it.

- **What "stops" means differs by graphics API, and the difference is worth
  stating plainly.** For Direct3D and OpenGL titles FrameLedger injects a
  library and genuinely removes its hooks. Vulkan titles use a Khronos *layer*
  instead, and a layer cannot remove itself from the loader's chain while the
  game is running — attempting to leave crashes the application. There,
  "stops" means FrameLedger stops observing and passes every call through
  untouched. It records nothing further, but its library remains loaded until
  the game exits.
- FrameLedger contains **no evasion techniques of any kind** — it does not hide, rename, obfuscate, or disguise itself. It is intended to be plainly identifiable to any security software that looks.

  For completeness about what it *does* run inside the game: besides the intercepted calls themselves, the component starts **one background thread** that sleeps for a second at a time and checks whether it has been told to stop. It reads only FrameLedger's own shared memory. It does not scan the game, enumerate what the system has loaded, or query Windows services — deliberately, because software that does those things from inside a game process looks like the thing this tool is trying not to be mistaken for.
- FrameLedger injects into a game only after you enable that game individually. For every other game — and for any game where the safety checks refuse — it records the session's start, end and duration together with whatever hardware sensor readings your machine provides, and reports every other measurement as not available. **There is no measurement mode that works without injecting**; an earlier version of this document described one, and it was never built. FrameLedger never estimates a value it could not measure, and it always shows which of the two modes produced a session. Neither mode requires administrator rights.

**However, these protections cannot be complete:**

1. The list of known anti-cheat systems cannot be exhaustive. New systems appear, and a game can add one in an update.
2. Some anti-tamper technology (for example Denuvo) can react badly to injection even in single-player games — usually a crash, but potentially lost progress or play time.
3. Heuristic detection can flag well-behaved software.
4. **The developer cannot restore a banned account, recover lost progress, or intervene with any game publisher on your behalf.**

**FrameLedger is intended for offline and single-player play. If you enable injection for a game with any online or competitive component, you do so entirely at your own risk and are solely responsible for the consequences, including compliance with that game's terms of service.**

## 2A. There is no way past the guard

Versions 0.1.0-beta.3 and 0.1.0-beta.4 offered a per-game switch to overrule the guard. **It has been removed.** If
FrameLedger finds anti-cheat or anti-tamper software in a game — before it injects, when a session starts, or during a
session — it refuses, turns hooking off for that game, and says so on the game's page. There is no setting, per game or
global, that turns that back on for that executable. Sessions recorded under the earlier switch stay in your database,
marked as such in their notes.


## 3. Stability

Software running inside another process can, in principle, destabilize it. FrameLedger guards every intercepted call, disables itself automatically after repeated internal faults, and automatically stops injecting into a game that crashes shortly after injection twice. Despite this, the developer is not responsible for crashes, lost progress, corrupted saves, or any other loss arising from use of the software. **Save your game before benchmarking.**

## 4. Measurement accuracy

Frame timing is derived from high-resolution timestamps taken at the moment the game presents each frame; upscaling, frame-generation and ray-tracing state are read from the parameters the game passes to those APIs. This is substantially more accurate than inferring settings from files on disk, but **no measurement is guaranteed to be exact**:

<!-- accuracy-block:begin -->
> ⚠ **What FrameLedger actually measures today — 2026-09-22.** The software is a beta; its latest
> **pre-release is `0.1.0-beta.5`** (2026-09-22), an unsigned installer built from that tag with its
> checksums published beside it. The source holds the desktop app
> (library, store import, charts, settings) and the background Agent, which records a session when a
> game in the library runs, injects only into games you enabled and only past the safety guard, and
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
>   bypass Streamline, the NVIDIA driver's own per-process record is stored with the session, labelled
>   *driver-reported*; the app does not display it yet. Intel XeSS is **not** read (its SDK licence
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
>   **Presented FPS** with a note on what it may include — never a Native figure.
> - **Ray tracing:** Yes/No measured on Direct3D 12 from ray-dispatch and acceleration-structure-build
>   calls; the technique and path tracing are `N/A`, and so is ray tracing on other APIs.
> - **Video memory:** in use on the whole graphics card, recorded from Windows and GPU-driver telemetry
>   and charted. **Not measured at all:** each game's own video-memory use and budget, which frame
>   spikes were shader compilation, PC latency (Reflex), and HDR. Stutter count and stutter time are
>   measured from frame times.
> - **Processor and memory:** how busy the processor was (time busy, all cores together — not the
>   frequency-scaled figure Task Manager draws) and how much system memory was in use, read from
>   Windows once a second and charted. Processor temperature is read only when the Agent runs as
>   administrator with PawnIO installed, and that reading has not yet been checked on real hardware;
>   everywhere else it is `N/A`.
> - **Safety:** every pre-injection check runs before injection, including the signed-by-a-known-vendor
>   half of the suspicious-module rule. During every capture the Agent re-runs the checks every 30 s and
>   stops capturing on a refusal: a Direct3D or OpenGL game's hooks are removed, and the Vulkan layer
>   goes passthrough. A global switch in Settings turns all hooking off, and a running capture stops at
>   its next check; it can only refuse, never permit. **There is no override.** A finding about a game — anti-cheat
>   named in its process, in its folder or on its title lists — turns hooking off for that game, whether found
>   before injection, at a session's start or by the 30 s re-check; the game's page says so, and nothing turns it
>   back on for that executable.
>
> Where a value is not measured it reads `N/A`, with two exceptions: FPS then shows Presented FPS with a
> note on what it may include, and ray-tracing flags may show a value you set yourself, labelled as
> yours. The software never substitutes an estimate.
<!-- accuracy-block:end -->

- Frame timing: typically better than 0.05% error; hardware sensors carry their own accuracy limits (±1–2 °C is normal).
- 1% Low and 0.1% Low are statistical and are hidden when the sample size is insufficient.
- Path tracing has no reliable technical signature; FrameLedger reports it as `N/A` and never asserts it. *(This line promised a confidence-scored suggestion; none is computed. Corrected 2026-09-15.)*
- When measurement is unavailable (no-injection mode, unsupported API, missing vendor support), fields read `N/A`. FrameLedger does not substitute estimates for measurements.

Do not use FrameLedger's output as the sole basis for purchasing, warranty, overclocking, or safety decisions.

## 4A. Your PC must meet the game's own requirements

FrameLedger measures a game; it does not make one run. **Make sure your PC meets at least the minimum system
requirements published by the game's developer or publisher before you measure it** — read them first.

If you run FrameLedger with a game on a machine below that game's minimum requirements, low frame rates, stutter,
crashes, overheating, failures to start, and readings that look wrong are consequences of that choice. **They are your
responsibility, not the developer's**, and they are not defects of FrameLedger. The same applies to unstable overclocks
or undervolts, overheating or failing hardware, out-of-date or beta graphics drivers, a Windows installation that is
missing updates, and other overlays, injectors or "optimizer" tools running at the same time.

FrameLedger adds a small cost of its own while it records. On a machine that is already at its limit, that cost can
be the difference between a game that runs and one that does not. Turn hooking off for that game if so.

## 5. Hardware and drivers

FrameLedger reads GPU telemetry through vendor libraries that ship with your graphics driver. It **only reads** — it never changes clocks, fan curves, power limits, or any other hardware setting. Optional CPU-temperature support relies on the separate third-party **PawnIO** driver, which you install at your own discretion under its own license; some anti-cheat systems object to third-party kernel drivers regardless of what they are used for.

## 6. Third-party names

Windows and DirectX are trademarks of Microsoft Corporation. NVIDIA, DLSS, Reflex, NVAPI, AMD, FSR, Intel, XeSS, Vulkan, Steam, GOG, Epic Games, itch.io, Unity, Unreal Engine, and all other product names are trademarks of their respective owners. FrameLedger is an independent project and is **not affiliated with, endorsed by, or sponsored by** any of them, including any game publisher or anti-cheat vendor.

## 7. Content creators

If you publish benchmarks or videos using FrameLedger data, you are responsible for how you present the numbers. The software deliberately shows native frame rate alongside frame-generation-boosted output, and labels which measurement tier produced each session; keeping both visible in published material is strongly encouraged.

## 7A. Your data, unsigned builds, and third parties

- **Your recordings are yours to back up.** They live in one folder on your PC (`%LOCALAPPDATA%\FrameLedger`). A bug, an
  update, a disk problem or an uninstall can damage or remove them. The developer cannot recover them.
- **Builds are not code-signed.** Windows SmartScreen and antivirus products may warn about or block the installer or
  the component loaded into a game. Verify the download against the published `SHA256SUMS.txt`; only download from the
  project's own GitHub Releases page. A build obtained anywhere else is not the developer's and is not covered by
  anything here.
- **FrameLedger is not affiliated with any game, publisher, platform, GPU vendor or anti-cheat vendor** (§6). None of
  them supports it. If a game's or a platform's terms forbid third-party software that loads into the game, using
  FrameLedger with it is your decision and your responsibility.
- **Numbers are for your own information.** Do not rely on them for a purchase, a warranty claim, a refund dispute, a
  review you are paid for, or any decision where being wrong costs money, without checking them another way (§4).

## 8. No professional advice

Nothing in the software or its documentation constitutes professional, legal, or engineering advice.

---

Contact: <contact@poli0981.dev> · Developer: <https://poli0981.dev/> · Project: <https://github.com/poli0981/frameledger>
