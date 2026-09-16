# FrameLedger

**A local-first game performance ledger for Windows.** FrameLedger records frame times, FPS metrics, hardware telemetry, and — crucially — the settings your game is *actually rendering with*, every time you play. Then it turns your history into charts, so you can see exactly how a patch, a driver update, or a settings change affected performance.

> No telemetry. No accounts. All data stays on your machine.

<!-- accuracy-block:begin -->
> ⚠ **What FrameLedger actually measures today — 2026-09-15.** The software is pre-alpha and
> **unreleased**: no tagged build or installer has been published. The source holds the desktop app
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
>   `N/A` where it does not. DLSS-G and FSR FG are named from the calls the game makes; any other
>   generator (XeSS-FG, one compiled into the game) is shown only as active with the technology not
>   identified, and is never named. Where a technology is named but its frames could not be counted, or
>   frame generation is not measured, the software shows **Presented FPS** with a note on what it may
>   include — never a Native figure.
> - **Ray tracing:** Yes/No measured on Direct3D 12 from ray-dispatch and acceleration-structure-build
>   calls; the technique and path tracing are `N/A`, and so is ray tracing on other APIs.
> - **Video memory:** in use on the whole graphics card, recorded from Windows and GPU-driver telemetry
>   and charted. **Not measured at all:** each game's own video-memory use and budget, which frame
>   spikes were shader compilation, PC latency (Reflex), HDR, and CPU temperature. Stutter count and
>   stutter time are measured from frame times.
> - **Safety:** every pre-injection check runs before injection, including the signed-by-a-known-vendor
>   half of the suspicious-module rule. During every capture the Agent re-runs the checks every 30 s and
>   stops capturing on a refusal: a Direct3D or OpenGL game's hooks are removed, and the Vulkan layer
>   goes passthrough. A global switch in Settings turns all hooking off, and a running capture stops at
>   its next check; it can only refuse, never permit. There is no override anywhere.
>
> Where a value is not measured it reads `N/A`, with two exceptions: FPS then shows Presented FPS with a
> note on what it may include, and ray-tracing flags may show a value you set yourself, labelled as
> yours. The software never substitutes an estimate.
<!-- accuracy-block:end -->

> The rows below that describe **safety behaviour** are qualified where they sit, rather than
> here, because that is where a reader checking their own risk will look. What this README
> describes beyond the block above is the finished product; the block is what exists.

## What makes it different

Most tools tell you your frame rate. FrameLedger tells you **what produced that frame rate**:

- **Real render resolution vs output resolution** — the actual internal resolution, not the one in the settings menu.
- **Which upscaler is running** — DLSS / FSR, read from the API the game calls, not guessed from files on disk. Intel XeSS is not read: its SDK licence forbids it.
- **Frame Generation, measured** — native and generated frames counted separately. Shown as `62 → 118 FPS (×1.9 FG)` when both are counted, and as a qualified Presented FPS when they are not — never as one inflated number.
- **Ray tracing, actually detected** — including inline ray tracing (DXR 1.1 `RayQuery`), which frame-counting tools miss entirely.
- **Per-process VRAM** and whether the driver was exceeding its budget (a real stutter explanation).
- **Shader-compilation stutter attribution** — which frame spikes were pipeline compiles.
- **PC latency** when the game uses Reflex.

Plus the usual: Avg / Min / Max / Median FPS, 1% Low, 0.1% Low, frametime distributions, stutter metrics, CPU/GPU temperatures, session history, cross-session comparison, and Steam/GOG/Epic/itch.io library import. UI in **English / Tiếng Việt / 日本語**.

## How it works — and the trade-off, stated plainly

None of the settings data above is visible from outside a game process. To read it, FrameLedger loads a component into the game and observes the calls the game makes to graphics APIs — the same class of technique used by frame-rate overlays, recording software, and post-processing injectors. Vulkan titles use a standard Vulkan layer instead, when FrameLedger starts them.

**That carries a real risk: anti-cheat systems can detect injected code and may ban accounts.**

FrameLedger is built to keep that risk small and to be honest about it:

| Safeguard | |
|---|---|
| **Off by default** | Injection is disabled for every game until you enable it individually, after a consent prompt |
| **Hard refusal** | Known anti-cheat/anti-tamper components are detected before injection and every 30 s during a session. Detected ⇒ FrameLedger refuses, or stops capturing at the next scan — so up to 30 s can pass before it reacts to anti-cheat that loads mid-session. For Direct3D/OpenGL that means hooks are removed; for Vulkan it means the layer goes passthrough, because a layer cannot leave the loader chain of a running game. **There is no override — not in settings, not in a config file, not on the command line**<br><br>✅ **Both halves run since 2026-09-10.** The *pre-injection* refusal runs every documented check, and during every capture the Agent re-runs them every 30 s: the component inside the game removes its hooks within one frame of being told to, and within 65 s if it stops hearing from the scanner. *(Until 2026-09-15 this cell said the in-session re-scan did not run at all, which stopped being true when the Agent's capture loop landed.)* For a Vulkan title the stop is the layer's own: it checks the scanner's counter on every present and goes passthrough within one present of the stop flag, or after 65 s without a scan — a title that has stopped presenting is not stopped by that deadline, and presents nothing to be observed either. |
| **No evasion, ever** | FrameLedger does not hide, rename, obfuscate, or disguise itself. It keeps its real name, real exports, and version info. It is meant to be plainly visible to any security software that looks. This is an architectural rule, not a setting |
| **Read-only** | It never reads or writes game memory, never modifies game behavior, never touches saves or input, and never changes GPU clocks, fans, or power limits |
| **Always a way out** | FrameLedger refuses rather than pushes through. When the guard finds anti-cheat, or the hook fails, or you have not enabled a game, it **stops** — and records the session anyway: start, end, duration, whatever hardware sensors your machine provides, and the reason there is nothing else. Every measured value reads `N/A`; it never estimates one.<br><br>**The way out is that it does not inject, not that it measures another way.** An earlier version of this row promised a no-injection measurement mode. It was designed around Intel PresentMon, that was dropped on 2026-08-27 after measurement, and no replacement was chosen — so **frame times require hooking**. Said in this row rather than a footnote because the row is in the SAFETY table, and a reader deciding whether to enable a game needs the real alternative, not a comfortable one |

**FrameLedger is for offline and single-player games.** If you enable it for anything with an online or competitive component, that is your call and your responsibility. The developer cannot reverse a ban. Please read [`legal/DISCLAIMER.md`](legal/DISCLAIMER.md) and [`docs/19_SAFETY_AND_ANTICHEAT.md`](docs/19_SAFETY_AND_ANTICHEAT.md) before enabling it for anything.

### Capture tiers

| Tier | How | What you get |
|---|---|---|
| **1** | Injected hooks (opt-in, per game) | Everything above |
| **2** | None — nothing is injected | **Session duration, whatever hardware telemetry your machine can provide, and why there is nothing else** (which check refused, or that the hook failed, was not enabled, or the game is 32-bit). On NVIDIA that telemetry is measured to be GPU temperature, load, power, clocks, VRAM in use and fan speed, with no elevation; AMD and Intel are untested. Everything else reads `N/A` |

The tier is recorded on every session and shown in the UI. Metrics unavailable at a session's tier read `N/A` — FrameLedger never substitutes an estimate for a measurement.

> **This ladder used to have three rungs and a middle one that measured frame times without injecting.** It was to be built on Intel PresentMon; that was dropped on 2026-08-27 after measurement, and no replacement was chosen. **So frame times, FPS and the lows are now Tier-1-only.** Said plainly because the previous README promised them without injection, and a reader who remembers that would otherwise assume it still holds.

## Architecture

| Component | Runs as | Role |
|---|---|---|
| `FrameLedger.exe` | Standard user | Fluent desktop UI (WPF + [WPF UI](https://github.com/lepoco/wpfui)), charts, library, settings |
| `FrameLedger.Agent.exe` | Standard user (elevation optional) | Injection control, safety guard, data collection, GPU telemetry, storage |
| `FrameLedger.Overlay.dll` | Inside the game | C++20 hooks + lock-free shared-memory writer. Records only; never analyzes, allocates, or blocks |
| `FrameLedger.VkLayer.dll` | Inside the game | Vulkan implicit layer (Vulkan titles use this instead of injection) |

Elevation is **optional — for everything.** Hooked capture is the normal path and runs as a standard user, and there is no longer any tier that needs more than that. Elevation unlocks exactly two extras: CPU temperature sensors (not read yet — see the block above), and attaching to games that themselves run elevated. ~~and the Tier-2 ETW fallback. If you expect to rely on Tier 2, run the Agent elevated.~~ — there is no ETW fallback to rely on.

## Requirements

- Windows 10 (22H2) or Windows 11, 64-bit
- A 64-bit DirectX 11/12 or OpenGL game, or a Vulkan game FrameLedger starts, for hooked (Tier-1) capture. **32-bit games — including most DirectX 9 titles — cannot be measured at all**: the component FrameLedger loads is x64 and an x64 DLL cannot enter a 32-bit process. Such a title records duration and hardware telemetry and nothing else. *(This line previously said "supported at Tier 2 only", which meant frame times without injection. That tier no longer exists.)*
- Optional: [PawnIO](https://pawnio.eu/) for CPU temperature, which is not read yet (GPU telemetry works without it, through your graphics driver's own libraries)

## Install

> **No release has been published yet.** The steps below are for the first tagged release; until then FrameLedger runs only from a source build (`docs/12_BUILD.md`).

1. Download the latest `FrameLedger.App-win-Setup.exe` from [Releases](https://github.com/poli0981/frameledger/releases). It installs into `%LOCALAPPDATA%\FrameLedger.App`; your data stays in `%LOCALAPPDATA%\FrameLedger`, and uninstalling asks before touching it.
2. SmartScreen may warn — releases are not code-signed (free, open-source project). Verify the SHA-256 checksum published with each release, then **More info → Run anyway**.
3. Follow the first-run Legal Gate and Agent setup. Nothing is injected until you enable it for a specific game.

## Privacy

Everything lives locally in `%LOCALAPPDATA%\FrameLedger`. The only network calls FrameLedger makes are **the update check** — a few seconds after launch unless you turn it off in Settings ▸ Updates, and when you choose Help ▸ Check for updates — and **the download of an update you accepted**; both go to GitHub Releases, from an installed copy only, and nothing is applied until you restart (never while a game is hooked). There is no other request: the anti-cheat blocklist and the detection rules ship with the build and update only when you install a new one (`docs/20_OPEN_QUESTIONS.md` §S20), and nothing is looked up on any store. Bug reports are always built locally, shown to you, and submitted by you. Full policy: [`legal/PRIVACY_POLICY.md`](legal/PRIVACY_POLICY.md) — version 2.2 removed the three rows (a rules feed twice over, a Steam lookup) that earlier versions listed for requests the software never made.

## License

GPL-3.0-only. See `LICENSE`. Third-party components: [`legal/THIRD_PARTY_NOTICES.md`](legal/THIRD_PARTY_NOTICES.md).

GPU telemetry is layered so the project never depends on a proprietary vendor licence: a vendor-neutral DXGI/performance-counter baseline, LibreHardwareMonitor (MPL-2.0) for sensors on all vendors, and NVIDIA's NVAPI SDK (MIT) for NVIDIA-only extras (telemetry and the driver's DLSS record today; Reflex latency is not read yet). No Intel or AMD GPU *telemetry* SDK is bundled — see `docs/18_GPU_VENDOR_APIS.md` for why. The one AMD component in the tree is five MIT headers from the FidelityFX SDK, used for types only so the upscaler hook can read an FSR title's own dispatch descriptor; nothing AMD-built is linked or redistributed.

## Documentation

Developer/AI-facing docs in [`docs/`](docs/). Start with `CLAUDE.md`, then `docs/19_SAFETY_AND_ANTICHEAT.md` (which constrains everything else), then `docs/01_ARCHITECTURE.md`.

## Reporting a safety gap

If you find a game with anti-cheat that FrameLedger fails to detect, please open an issue with the *Safety gap* form — **that is a safety bug and is treated with the same priority as a security report.** A vulnerability in FrameLedger itself takes the private route in [`SECURITY.md`](SECURITY.md).

**How fast a fix can reach you, stated accurately.** Blocklist entries are data rather than code, so a fix is a one-line change here. But the software has **no rules-update path yet**: it installs the blocklist that shipped with your build and never fetches another (`docs/20_OPEN_QUESTIONS.md` §S20, feed half). Until that exists, a blocklist fix reaches you **only when you install a new release**. This paragraph previously said the opposite, and the sentence it said it in was a response-time promise attached to a security-priority commitment.

---

**Status:** pre-alpha, under active development. Roadmap: [`docs/15_ROADMAP.md`](docs/15_ROADMAP.md).
