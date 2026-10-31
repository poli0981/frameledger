# HANDOFF — read this first, then stop reading it

The one file a new session opens to pick up ~~P0~~ work (corrected 2026-10-04). It carries **sequencing, decisions
and traps**. It deliberately carries **no status**.

> ## The rule this file lives under
>
> **This file must never restate what another document already states.** Four
> documents disagreeing about the gate's composition is what §S23-4 and §S23-5 were
> raised for, and `rules-validate` now fails a fifth copy in the shipped data. A
> handoff that summarises status becomes the next stale copy within a day — that has
> already happened three times here (#45's note, then #46–#52; `15_ROADMAP`'s status
> block; `spike-notes` §8 correcting a stale claim and going stale in the same file).
>
> So: **status lives where it lives.** Read it there.
>
> | Question | File |
> |---|---|
> | What is unresolved and what does it block? | `docs/20_OPEN_QUESTIONS.md` — §S24 is the index; the ✅/◐/❓/🔴/🅓 markers are load-bearing |
> | What was *measured*, on what machine? | `docs/spike-notes.md` |
> | Which P0 item is where? | `docs/15_ROADMAP.md` |
> | What landed? | `CHANGELOG.md` |
> | What does the software promise users? | `README.md`, `legal/DISCLAIMER.md` — both carry accuracy blocks |
>
> **And verify a status claim against the code before planning on it.** Four times now
> those files have asserted in the present tense something that had been closed, or
> was never true. The 2026-08-05 session found the ledger stale across seven PRs and
> two documents contradicting themselves *inside one section*.

---

## How this file is split (2026-10-19, beta.24, owner decision D66)

Everything this file held is still here or one link away, moved verbatim: what to do next and the open
owner-only items stay below; the history of every train, the passed owner-only items and the traps moved to
`docs/handoff/`. Code and documents cite the old sections by name — each name resolves here:

| Cited as | Now in |
|---|---|
| `§Where P0 actually stands`, queue items 1–6 (`§2b`, "item 3" …) | [`handoff/history/p0-queue-1-6.md`](handoff/history/p0-queue-1-6.md) |
| queue item 7 (`7a`–`7c`), `§Separable`, `§M5` | [`handoff/history/p0-queue-7-m5.md`](handoff/history/p0-queue-7-m5.md) |
| `§P2` (D1–D10) | [`handoff/history/p2.md`](handoff/history/p2.md) |
| `§P3` (D11–D17) | [`handoff/history/p3.md`](handoff/history/p3.md) |
| `§P4` | [`handoff/history/p4.md`](handoff/history/p4.md) |
| `§beta.3` | [`handoff/history/0.1.0-beta.3.md`](handoff/history/0.1.0-beta.3.md) |
| `§beta.4` | [`handoff/history/0.1.0-beta.4.md`](handoff/history/0.1.0-beta.4.md) |
| `§beta.5` | [`handoff/history/0.1.0-beta.5.md`](handoff/history/0.1.0-beta.5.md) |
| `§beta.6` | [`handoff/history/0.1.0-beta.6.md`](handoff/history/0.1.0-beta.6.md) |
| `§beta.7` | [`handoff/history/0.1.0-beta.7.md`](handoff/history/0.1.0-beta.7.md) |
| `§beta.8` | [`handoff/history/0.1.0-beta.8.md`](handoff/history/0.1.0-beta.8.md) |
| `§beta.9` | [`handoff/history/0.1.0-beta.9.md`](handoff/history/0.1.0-beta.9.md) |
| `§beta.10` | [`handoff/history/0.1.0-beta.10.md`](handoff/history/0.1.0-beta.10.md) |
| `§beta.11` | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) |
| `§beta.12` | [`handoff/history/0.1.0-beta.12.md`](handoff/history/0.1.0-beta.12.md) |
| `§beta.13` | [`handoff/history/0.1.0-beta.13.md`](handoff/history/0.1.0-beta.13.md) |
| `§beta.14` | [`handoff/history/0.1.0-beta.14.md`](handoff/history/0.1.0-beta.14.md) |
| `§beta.15` | [`handoff/history/0.1.0-beta.15.md`](handoff/history/0.1.0-beta.15.md) |
| `§beta.16` | [`handoff/history/0.1.0-beta.16.md`](handoff/history/0.1.0-beta.16.md) |
| `§beta.17` | [`handoff/history/0.1.0-beta.17.md`](handoff/history/0.1.0-beta.17.md) |
| `§beta.18` | [`handoff/history/0.1.0-beta.18.md`](handoff/history/0.1.0-beta.18.md) |
| `§beta.19` | [`handoff/history/0.1.0-beta.19.md`](handoff/history/0.1.0-beta.19.md) |
| `§beta.20` | [`handoff/history/0.1.0-beta.20.md`](handoff/history/0.1.0-beta.20.md) |
| `§beta.21` | [`handoff/history/0.1.0-beta.21.md`](handoff/history/0.1.0-beta.21.md) |
| `§beta.22` | [`handoff/history/0.1.0-beta.22.md`](handoff/history/0.1.0-beta.22.md) |
| `§beta.23` | [`handoff/history/0.1.0-beta.23.md`](handoff/history/0.1.0-beta.23.md) |
| `§beta.24` | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) |
| `§beta.25` | [`handoff/history/0.1.0-beta.25.md`](handoff/history/0.1.0-beta.25.md) |
| `§beta.26` | [`handoff/history/0.1.0-beta.26.md`](handoff/history/0.1.0-beta.26.md) |
| `§beta.27` | [`handoff/history/0.1.0-beta.27.md`](handoff/history/0.1.0-beta.27.md) |
| `§beta.28` | [`handoff/history/0.1.0-beta.28.md`](handoff/history/0.1.0-beta.28.md) |
| `§beta.29` | [`handoff/history/0.1.0-beta.29.md`](handoff/history/0.1.0-beta.29.md) |
| `§beta.30` | [`handoff/history/0.1.0-beta.30.md`](handoff/history/0.1.0-beta.30.md) |
| `§beta.31` | [`handoff/history/0.1.0-beta.31.md`](handoff/history/0.1.0-beta.31.md) |
| `§Owner-only`, an open item | [§Owner-only](#owner-only--no-pr-can-close-these) below |
| `§Owner-only`, a passed item (3, 12, 14–19) | [`handoff/owner-only-passed.md`](handoff/owner-only-passed.md) |
| `§Traps`, `§Two corrections carried forward` | [`handoff/traps.md`](handoff/traps.md) |
| `Dnn` | [§Decisions index](#decisions-index) below |

---

## **START HERE** — 2026-10-31, 0.1.0-beta.32: the alpha line — .NET 11 RC3 / C# 15 beside the betas

*Status is `docs/changelog/0.1.0-beta.32.md` (and, for the first alpha, `docs/changelog/0.1.0-alpha.1.md`); this is what
no other file carries.* The owner: "Bạn có thể phân tích, tìm kiếm sự khác biệt so với .NET 10, đánh giá, lên plan và tạo
thêm bản alpha, nơi thử nghiệm các dotnet mới, cập nhật code (nếu cần) cho .NET 11 RC3/c# 15 để chuẩn bị cho .NET 11 ra
mắt, thêm cảnh báo đây là bản thử nghiệm, yêu cầu tự chịu trách nhiệm nếu cần nhé." — an analysis of .NET 11 against
.NET 10, an evaluation, a plan, and an alpha build where new .NET versions are tried, code updated where needed, with an
experimental-build, own-risk warning. #344 (the line's plumbing), #346 (the warning, the backup, the legal text), #345
(what .NET 11's SDK asks of the source) and this release; the `alpha` branch and `0.1.0-alpha.1` follow.

**Decisions (asked while planning, do not re-ask):**

- **D73 — the alpha line** (`12_BUILD` §The alpha line, `13_CI_CD` §The alpha line, `11_UPDATER` §Channels): the source of a
  released beta built on a prerelease .NET from the long-lived `alpha` branch, tagged `vX.Y.Z-alpha.N`, packed on
  Velopack's `alpha` channel. An alpha **replaces** the beta on the PC (same packId, install folder and data folder — the
  native layer reads that folder by fixed path) and **copies the ledger before it first opens it**, once per alpha
  version, newest three kept (`06_DATA_MODEL` §Migrations).
- **D74 — the warning** (`08_UI` §First-run flow and §Settings, `01_ARCHITECTURE` §Lifecycle): `legal/ALPHA_NOTICE.md`
  in an alpha's Legal Gate with a tick box of its own that Accept waits for; the alpha's Agent does not serve until it is
  accepted (exit 8); an ALPHA badge, a banner at every start, the update channel locked.
- **D75 — the legal text made true**: EULA 1.6 §2B and Disclaimer 2.12 §0 name alphas beside betas; the owner approved
  the Alpha notice 1.0 and both sentences with the plan.
- **D76 — alpha.1 is a retarget only**: `net11.0`, C# 15 by `LangVersion latest`, the three runtime-serviced packages
  `11.0.0-rc.3.26522.148`; no preview feature (no runtime-async), no C# 15 syntax — main must keep building as C# 14 on
  .NET 10.

**The evaluation, as given to the owner:** .NET 11 is STS — supported to about 2028-11, no later than .NET 10 LTS
(2028-11-14) — so moving the main line buys no support time; Microsoft's supported-OS list for .NET 11 has Windows 10
only in its LTSC and IoT editions, against NFR-8's Windows 10 22H2 floor; C# 15's unions and closed hierarchies would suit
the tri-states and refusal reasons once the main line compiles C# 15. Recommendation: the main line stays on .NET 10;
the alpha line gathers the evidence; `20_OPEN_QUESTIONS` §G *The main line's .NET* holds the decision table.

**Choices made here:**

- **One alpha per beta, never several in a row**: Velopack's `GithubSource` asks GitHub for one page of ten releases, so
  ten of one line in a row would hide the other line's newest from its update check (read from Velopack's code).
- **Every alpha-only behaviour lives on main, dormant**: `Shared.BuildLine` is composed and injected, so the alpha's
  gate, banner, hold, Agent wait and backup are tested on main; the branch differs only by `RELEASE_LINE`, `global.json`,
  the TFM, three package versions and `legal/licenses/nuget/`.
- **The App holds the Agent while an alpha's gate is open** on a hold of its own: the connection's first round runs under
  the first-run window, and a held-back Agent started anyway would exit 8 and be started again every round — under the
  admin mode a Windows prompt each time.
- **The backup has its own lock**, five minutes, not the migration lock: that one gives a waiter 30 s and throws.
- **.NET 11's `IHost` rethrow is accepted on the alpha**: a failed background service ends an alpha's Agent through the
  crash path with a minidump, where .NET 10 exits 0 — said in `LIMITATIONS.md`.

**Measured on the way:** the whole gate on .NET 11 RC3 in a throwaway worktree (`spike-notes` §24): native 32/32, every
test suite green with no change of answer, coverage and format clean; 21 analyzer sites in 14 files (CA1307 ×13, MA0002
×7, CA2026 ×1), all fixed on main in forms .NET 10 also compiles; license-check red only because `legal/licenses/nuget/`
is the beta's package set.

**Traps met on the way:**

- **`sed -i` in Git Bash rewrites a CRLF file as LF** (seen on the probe's props files); edit CRLF files with the Edit tool
  or Python with `newline=''`, and `unix2dos` a file the Write tool created.
- **The keyboxd I/O error at signing came back**: `gpgconf --kill keyboxd` (the Windows GnuPG's), then commit again.
- **A CMake configure in a fresh worktree works when the gate is started from PowerShell**, whose `git` is
  `Git\cmd\git.exe`; from Git Bash, CMake caches `ucrt64\bin\git.exe` and the submodule step fails.

**Owner-only:** item 26 — beta.32 on real hardware.


## Owner-only — no PR can close these

*The items that passed — 3, 12, 14–19 — moved verbatim to [`handoff/owner-only-passed.md`](handoff/owner-only-passed.md) on 2026-10-19 (D66); the numbers are kept, and a number is never reused.*

1. **§S23-2 — branch protection.** `Rules / validate` is not a required status check on
   `main`, so the gate that makes the anti-cheat blocklist un-removable is advisory.
   **Second-order problem:** `rules-publish.yml` filters on four paths for both `push`
   and `pull_request`, so requiring the context as-is blocks every PR touching none of
   them, forever. Land the skip-shim (drop `paths:`, short-circuit inside the job) first;
   the setting is the owner's.
2. **Real-title verification runs** for the upscaler/FG/RT work, against each game's own
   settings menu. ~~**Live as of 2026-09-03:** the `slGetNewFrameToken` producer's table —
   Cyberpunk 2077 off / ×2 / ×4 and Hell Is Us DLSS + FG ×4, one launch each, reading
   `presents/tokens` off the `FG counts:` line against §S31's rows P1–P4.~~ **Run twice,
   2026-09-03 (P4) and 2026-09-04 (P1); done.** What is still owed here is the **quality
   preset** on a title that chains `sl::DLSSOptions` — Alan Wake 2 is the candidate — and
   the DLSS-G identity on a title where `kFeatureDLSS_G` is evaluated, if one exists.
4. **§H7's confirmation against the overlays actually resident on the dev machine.** The
   compare-and-restore path is proven against a fixture (ctest `fl_unhook_inline`); §H7 still
   says the six real overlays are *"worth doing once the Overlay has real hooks"*, and it has
   them since P1. One capture with each overlay active; the result is a `spike-notes` row.
5. **Re-ratify §S18 blocker 3 and §S27's basis** once P2's PR-C/PR-F make the Agent the shipped
   injecting entry point (§P2, decision D4). The PRs restate both entries; whether the
   restatement stands is the owner's.
6. ~~**D33's first real use (beta.9).**~~ **Done by 2026-09-27, read from a copy of the owner's ledger:** three normal
   hooked sessions of GIRLS' FRONTLINE 2 under the exception (NetEase Yidun) on 2026-09-26 and 09-27, the longest
   3 h 9 min; the `ja` review below is still owed. *(The item as written:)* Turn the option on; GIRLS' FRONTLINE 2 should be listed eligible (NetEase
   Yidun, `NEP2.dll`, three sessions); make the exception, turn hooking on and play one session: hooked, marked
   *Hooked · exception*, not stopped by the 30 s re-scan or by the Overlay when `NEP2.dll` loads. Aniimo should read
   *not eligible* with its `NEPKernel.sys`; the option off makes the next session Tier 2 with the exception kept. And
   the `ja` text of `Safety_Exception_*`, which ships as English until a reviewer signs.
7. **beta.10 on real hardware.** (a) **The display mode:** one DXGI game taken through exclusive full-screen →
   borderless → windowed in one session — the summary's shares, the Sessions column, the Trend's three share metrics —
   with a note of what the game's own menu called each mode. (b) **The admin mode:** turn it on → the disclosure → the
   UAC prompt → Settings says *Running elevated*; decline it → *the administrator prompt was declined*; sign out and in
   with the logon task → the prompt waits on the taskbar and one Agent results; start a game from FrameLedger while
   elevated → Task Manager's *Elevated* column says *No*; a game that runs as administrator → hooked; the CPU temperature
   with PawnIO (closes §M10 if it reads). (c) **The Unreal Engine version** on each Unreal game's page. (d) The Legal
   Gate shows Privacy 2.5 and Disclaimer 2.8 once. (e) The `ja` text of `Safety_AdminMode_*`, English until reviewed.
   (f) `docs/LIST_GAME_TESTED.md` — reviewed and approved by the owner before it was committed (D35, 2026-09-27);
   keep it current as games are run.
8. **beta.11 on real hardware.** (a) GIRLS' FRONTLINE 2: the option on → *Eligible* → *Make exception…* (the disclosure
   `ac-exception-dialog/2`, with its *Trial* paragraph) → hooking on → two sessions: the card says *trial: 0 of 2*, then
   *1 of 2*, then *Exception in force since …* alone. (b) The library at your window's width, and the list; Refresh
   after installing a game, and with the USB drive unplugged (*Not installed*, the drive named). (c) Delete all sessions
   on a game's page and in Settings ▸ Data. (d) *Sessions at a glance* and the Trend with a second unit. (e) The Legal
   Gate shows the Disclaimer 2.9 and the EULA 1.4 once. (f) The `ja` text of `Safety_Exception_Trial` (English until
   reviewed) and the App strings marked `review`.
9. **beta.12 on real hardware.** (a) **The game's memory:** one hooked game and one that is not (hooking off, or a
   blocked game), each beside Task Manager's Details tab at the same moment — the summary's *VRAM (this game)* against
   *Dedicated GPU memory*, *RAM (this game)* against *Memory (private working set)*; the Dashboard's live line; the
   Sessions columns, the Trend's memory metrics and the Sensors charts. (b) **The Legal Gate** shows the EULA 1.5, the
   Disclaimer 2.10 and the Privacy Policy 2.6 once, rendered, with the GPL as *your licence*, needing no acceptance.
   (c) **Help ▸ User guide**, **Help ▸ About** (its three tabs; *Open the licences folder* in the installed copy), and
   *View on GitHub* opening the `v0.1.0-beta.12` tag. (d) The `ja` strings marked `review` (the memory labels, the
   About window, the first-start summary). (e) `docs/LIST_GAME_TESTED.md` is as of beta.10: refresh it from a copy of
   the ledger, with the games' memory figures, and approve it before it is committed (D35).
10. **beta.13 on real hardware.** (a) **The first delta:** update an installed `0.1.0-beta.12` through Help ▸ Check for
   updates — the download is a fraction of the full ~107 MB, and the App comes back as beta.13; if it downloaded the
   full package instead, say so (Velopack falls back silently). (b) **On this PC:** with an Easy Anti-Cheat service
   running for any game, start a hook-enabled game — the notice says the anti-cheat is running on this PC, not in
   the game. (c) **Two graphics cards**, if a machine with them is at hand: the Sensors chart's GPU figures are the
   game's card. (d) The `ja` review of the new safety strings, with the others still marked for it.
11. **beta.14 on real hardware.** (a) **The update from beta.13:** choose *Beta* in Settings ▸ Updates, Help ▸ Check for
   updates, then *Restart to update* with no game running; Settings then reads *Automatic (Beta)*. The `velopack:` lines
   in `ui-*.log` say what was downloaded — a delta of about 2 MB; say so if it was the full package. (b) **Explorer ▸
   Properties ▸ Details** of `FrameLedger.exe` and `FrameLedger.Agent.exe` name FrameLedger and the copyright; Task Manager
   lists *FrameLedger Agent*. (c) **The wheel** on every page, the dialogs and the summary window; over a chart the wheel
   scrolls the page and Ctrl+wheel zooms; the Logs page follows the newest line. (d) **Hide scroll bars** on and off,
   without a restart. (e) **The import list in the dark theme**, and Compare's tick boxes. (f) **A crash**: an Unreal game
   that crashes reads *crashed (… CrashReportClient.exe)*; The Witcher 3 closed normally no longer reads *crashed*. (g)
   **The new measurements** on the RTX 5080: the VSync share against the game's own VSync setting, the render scale
   against the DLSS mode, the clocks, fan and limits. (h) The `ja` strings marked `review`. (i) **The Privacy Policy 2.7
   wording — before PR-5 and PR-7 merge.**

13. **beta.17 on real hardware.** (a) **A theme switch:** on Settings, Dark → Light → Dark, and again with a session's
   summary window open — every description and caption follows at once. (b) **Hide scroll bars** on: the Dashboard,
   Games, a game's page, Logs, About, Help ▸ User guide and a drop-down list draw no bar, and the wheel still scrolls;
   off, the bars come back without a restart. (c) **Help ▸ User guide** lists twelve pages and Limitations; a link to a
   section opens that section. (d) **The wiki**, once the release is out: the Wiki tab shows Home, the sidebar's two
   halves and a footer naming `v0.1.0-beta.17` — then tick *Restrict editing to collaborators only* (Settings ▸ General
   ▸ Features ▸ Wikis), or anyone can change a page until the next release overwrites it. (e) The `ja` strings marked
   `review`.
20. **beta.24 on real hardware — decision tables written before the runs.** (a) **The mix (D62):** a DLSS 4 title
   (Cyberpunk 2077 or Hell Is Us) with the NVIDIA App's *Frame Generation Mode* on *Use 3D app setting*, one session of
   about two minutes each at ×2, ×3 and ×4 chosen in the game's menu, plus its menus → the summary's *Frame generation in
   this session* lists ×2.0 / ×3.0 / ×4.0 with shares near the time spent, each with its own Native → Displayed, and the
   line *Set in the game — no NVIDIA App frame-generation override*. Then the MFG title with the App's *Fixed ×6* → a
   ×6.0 row and *Set in the NVIDIA App: Fixed ×6*. Then *Dynamic, Target FPS = Max refresh rate, Multiplier = Up to 6x*
   → several rows or *Not classified*, and *Set in the NVIDIA App: Dynamic, up to ×6, target: max refresh rate*. Record
   the stored `driver_profile` values for the Dynamic run: `NGX_DLSSG_DYNAMIC_MULTI_FRAME_COUNT_MAX` 5 and the target
   `0x01000000` confirm the decode; anything else corrects `NvidiaOverrides.FrameGenerationContext`.
   (b) **The quality mode (D63) on Expedition 33:** the App's Super Resolution mode *Quality*, then *DLAA*, *Ultra
   Performance*, *Performance*, one short session each → the header reads *DLSS (driver-reported) Quality · NVIDIA App*
   and so on. Stored `performanceMode`: Ultra Performance 3 (already measured on Silent Hill 2) and DLAA 5 confirm NGX's
   numbering; DLAA 4 would mean the report follows the profile's numbering above 3 — then
   `UpscalerNames.FromDriverReport` changes, and the row goes into `spike-notes`.
   (c) **The libraries (D64):** the same sessions' DLSS line names the super-resolution library and *from the graphics
   driver* when the override's copy is loaded; preset L → *DLSS 4.5 (preset L)*. With *Model Presets* on *Use 3D app
   setting*, the game's own library and no generation beyond what was observed. Record the names the override loads
   under `%ProgramData%\NVIDIA\NGX` (the summary's *Loaded:* line) — they correct `05_DETECTION` §Shipped is not loaded.
   (d) **DLSS 5** on the owner's DLSS 5 title: *DLSS 5 runtime loaded*. If it is not shown, record the module names it
   loads (*Loaded:* line); `nvngx_dlssnr.dll` is then corrected in `CensusNames` and `05_DETECTION`.
   (e) **SonarCloud (D67):** bulk-mark per `13_CI_CD` §SonarCloud; optionally its *Ignore issues on multiple criteria*
   rows; close PR #315. (f) The Legal Gate shows **Disclaimer 2.11** and **Privacy Policy 2.12** once. (g) The `ja`
   strings marked `review`. *(2026-10-19, the owner's first runs — `spike-notes` §22: (a) the Dynamic context line
   reads the App's own words, and the mix found ×2.0 / ×3.0 / ×6.1 — plus a ×1336.0 row that was a defect, fixed in
   beta.26; (c) the override loads `160_E658700.bin` 310.9.16.0 and `1B0_E658703.dll` 2.14.16.0 from the NGX folder;
   (e) **done** — every issue marked; the last six, triaged in `13_CI_CD` §SonarCloud (beta.26), the same day, and
   `main`'s quality gate passes. The rest is not run yet.)* *(2026-10-20, the owner's runs of beta.26 — `spike-notes`
   §23: (a) **done** — ×2, ×3 and ×4 chosen in Cyberpunk 2077's menu read ×1.9 (ten seconds), ×3.0 and ×4.0, each with
   its own rates, under "Set in the game — no NVIDIA App frame-generation override"; **Fixed ×6** on Expedition 33 read
   ×6.0 under "Set in the NVIDIA App: Fixed ×6"; *Dynamic* on Silent Hill 2 read ×2.0 and ×3.0, plus a ×2.52 row that
   was a defect, fixed in beta.27. The Witcher 3 under **Fixed ×6** presented ×2.0 throughout: the rows are the frames,
   the line is the setting. (b) **could not pass before beta.27**: every stored driver word had lost
   `CREATED | EVALUATE`, read after the game had closed, so an NGX-direct title under the override never read its mode
   — run it on beta.27. (c) The Witcher 3 under the override loaded `160_E658700.bin` 310.9.16.0 and `1B0_E658700.dll` 2.14.16.0;
   Silent Hill 2 and Expedition 33 loaded `1B0_E658703.dll`.)*
21. **beta.25 on real hardware — FSR's quality mode (D65), decision table written before the run.** Lies of P (FSR
   through AMD's FidelityFX libraries) launched by FrameLedger, not attached, with the game's own *FSR* at *Quality*,
   then a second session at *Performance* → the summary's header reads *FSR 3 Quality*, then *FSR 3 Performance* (or
   *FSR Quality* where the SDK 2.x libraries name no version), and the CSV export's `# quality_mode:` says
   `quality (game)`. Then: **no mode on both** → the title sizes its frames without asking, or asked before the hook
   was in: record the session's render → output resolution and the *Loaded:* line in `spike-notes`, and D65 stays built
   and silent for that title. **A wrong mode** (the Quality session reading Balanced) → the answers this title receives
   are not the vendor's documented ratios: record the stored `upscaler_quality` and the render size in `spike-notes`,
   and the match is withdrawn until it is explained. **With the game's dynamic resolution on** (if it offers one) → no
   mode. *(2026-10-19, `spike-notes` §22: the Quality session read **no mode** with the query hooked — the first
   branch; 1707×961 is the engine's own rounding of 66.67 %, not the size answer, so the title asked before the hook
   was in or never asked. D65 stays built and silent for Lies of P. The Performance session is not run.)*
   *(2026-10-20, §23: the Performance session, started through Steam, read "FSR 3 · 1280×720 → 2560×1440" and no mode —
   the first branch on both. 1280×720 is exactly the vendor's Performance size, so a query the hook met would have
   matched: the title did not ask while the hook was in. It cannot be launched by FrameLedger as this item asks — started
   directly it hands itself to Steam within seconds, and before beta.27 the game Steam started was not recorded at
   all.)*
22. **beta.27 on real hardware.** (a) **The driver's report, and item 20(b):** Expedition 33 under the NVIDIA App's
   DLSS override → the header reads *DLSS (driver-reported)* with the override's mode and *· NVIDIA App* (*Balanced*
   as set on 2026-10-20); then 20(b)'s four modes. Still *Upscaler not identified* → record the session's stored
   `ngx_driver_words`: no reading while the game ran said the feature was created and evaluated. (b) **Start directly
   on Lies of P:** the notice says a Steam game may hand itself to Steam first, and the game Steam starts is recorded —
   `agent-*.log` says *started while the last session of its executable was ending; recording it now*. The crash safety
   counts the hand-off: after two such starts hooking is off again (`19_SAFETY`). (c) **One window per session:** open a session from a Sessions tab, then again from the game
   page or its notification → the same window comes forward, restored if minimised. (d) **A game in exclusive
   full-screen** (Dark Souls III, Lies of P) → its summary's Display card shows the window size.
23. **beta.28 on real hardware.** Two games at once — one hooked, one with hooking off (or refused) → the Dashboard
   shows two cards side by side, the hooked one first, and *2 games recording* beside *Live capture*; each card's
   numbers are its own game's; closing one leaves the other's card where it is, and the count goes. A third game → a
   third card on the next row and *3 games recording*. (The `ja` string `Dashboard_Live_Count_Format` is marked
   `review`.)
24. **beta.29 — SonarCloud from CI (D68), the owner's settings.** None blocks anything. (a) *Optional:* add
   `SONAR_TOKEN` to the repository's **Dependabot secrets** (Settings ▸ Secrets and variables ▸ Dependabot) if
   Dependabot's pull requests should be analysed too; until then `ci.yml`'s `sonarcloud` job is skipped for them, as it
   is for a fork's. (b) *Optional:* SonarCloud's branch list holds `beta29/pr1-sonarcloud-ci`, left by a dispatch of
   that branch the plan then refused (`13_CI_CD` §SonarCloud, *The first analyses*); delete it under *Branches*, or let
   SonarCloud remove it after 30 days without an analysis. (c) *Optional:* `analyze (actions, none)` (CodeQL over the
   workflows) and `sonarcloud` are not required checks; making either one required is the owner's setting. (d) Velopack
   1.2.161 and Markdig 1.5.0 were not taken by #335 (`docs/changelog/0.1.0-beta.29.md`, *The libraries Dependabot
   never updated*): the first changes how updates install and wants a run of the update path on the owner's machine,
   the second its licence copy re-read. *(2026-10-26, beta.30: Velopack 1.2.161 taken — the owner's decision D71, its
   update path is item 25(c); Markdig went to 1.4.2, its licence text re-read and unchanged; 1.5.0 waits for the
   cooldown.)*
25. **beta.30 on real hardware.** (a) **Enter in the consent dialog** (D69): on a game whose hooking is off, turn the
   switch on, type the phrase exactly and press Enter → hooking is on, and the switch and its text agree; Enter before
   the phrase is complete → nothing happens; Esc → the switch is back off beside *off*, and leaving the page and coming
   back shows the same. (b) **The numbered markers** (D70): the game whose GPU driver changed on 2026-10-24 — *Sessions at
   a glance* and the *Trend* show a badge *1* above the chart, whole, in both themes; the list under each chart names
   that day and *Driver GPU: 32.0.16.1742 → 32.0.16.2101*; pointing at the dashed line shows the same; a narrow window
   keeps every badge whole. (c) **The updater** (D71): Help ▸ Check for updates on beta.29 installs beta.30 (beta.29's
   updater, a delta); the update to beta.31, when it comes, is the first Velopack 1.2.161 installs — say whether it
   downloaded the delta or the full package, and whether it came back. (d) The `ja` strings marked `review`
   (`Trend_Changes_Header`, `Trend_Change_Heading_Format`).
26. **beta.32 on real hardware.** (a) **The update from beta.31**: Help ▸ Check for updates, *Restart to update* — the
   terms window shows the EULA 1.6 and the Disclaimer 2.12 once, with no tick box, and nothing else of the alpha appears:
   no ALPHA badge, no banner, the title *FrameLedger*, Settings ▸ Updates ▸ Channel as before. (b) About ▸ Legal documents
   lists the Alpha notice, and its version line reads *Runs on .NET 10.0.x*; Help ▸ User guide has *Alpha builds*.
   (c) Whether the update downloaded the delta or the whole package. (d) The `ja` strings marked `review`: `About_Runtime_*`
   here; `Alpha_*`, `FirstRun_Alpha_*`, `FirstRun_Doc_Alpha`, `App_Title_Alpha` and `Settings_UpdateChannel_AlphaBody`
   show only in an alpha.
**Answered 2026-08-05, do not re-ask:** remove `gameguard` and keep `guard` (approved
over a red `Rules` gate, with the reasoning recorded in the merge commit); vendor NVAPI
headers **and** `amd64/nvapi64.lib`; Windows 11 is the **measurement scope** only —
`SupportedOSPlatformVersion` stays at Win 10 22H2 and the D3DKMT two-OS requirement is
deferred with a written rationale.

---

## Decisions index

Where each owner decision is recorded — the file and its section, and a label to find it by. The decision itself is the text there, not this table.

| D | Recorded in | About |
|---|---|---|
| D1 | [`handoff/history/p2.md`](handoff/history/p2.md) | FrameLedger.Application gains a reference to FrameLedger.Shared (A); Domain still |
| D2 | [`handoff/history/p2.md`](handoff/history/p2.md) | never assigned — the numbering skips D2 and D3 (§P2 says so) |
| D3 | [`handoff/history/p2.md`](handoff/history/p2.md) | never assigned — see D2 |
| D4 | [`handoff/history/p2.md`](handoff/history/p2.md) | the Agent ships --console consent grant |
| D5 | [`handoff/history/p2.md`](handoff/history/p2.md) | FileGameConsentStore is retired in B; the CaptureHost writes a build-tree ledger.db |
| D6 | [`handoff/history/p2.md`](handoff/history/p2.md) | --data-dir exists only under --console (F; 12_BUILD §Debugging), so --serve can never |
| D7 | [`handoff/history/p2.md`](handoff/history/p2.md) | the FR-2.4 kill switch is the FOURTH input of HookedCaptureGate, not an upstream |
| D8 | [`handoff/history/p2.md`](handoff/history/p2.md) | FR-11 is NOT a consent precondition in P2 |
| D9 | [`handoff/history/p2.md`](handoff/history/p2.md) | P2 never clears hook_blocked_reason |
| D10 | [`handoff/history/p2.md`](handoff/history/p2.md) | the pipe (07_IPC) is P3, not P2; --serve is watcher-driven and logs |
| D11 | [`handoff/history/p3.md`](handoff/history/p3.md) | the pipe's transport |
| D12 | [`handoff/history/p3.md`](handoff/history/p3.md) | commands join as one more producer |
| D13 | [`handoff/history/p3.md`](handoff/history/p3.md) | SessionProgress is a third ICaptureObserver |
| D14 | [`handoff/history/p3.md`](handoff/history/p3.md) | Safety_ lives in FrameLedger.Shared, once |
| D15 | [`handoff/history/p3.md`](handoff/history/p3.md) | the App opens ledger.db directly, under 06_DATA_MODEL §Writer ownership: it writes games' |
| D16 | [`handoff/history/p3.md`](handoff/history/p3.md) | the settings registry (closes §G's row, written into 06_DATA_MODEL in PR-3): ui.language |
| D17 | [`handoff/history/p3.md`](handoff/history/p3.md) | FL_MOCK, if the owner wants it, is --serve --mock under --data-dir only (D6's shape), so a |
| D18 | [`handoff/history/0.1.0-beta.3.md`](handoff/history/0.1.0-beta.3.md) | the guard has one override, and its shape is fixed |
| D19 | [`handoff/history/0.1.0-beta.3.md`](handoff/history/0.1.0-beta.3.md) | the DLSS preset derived from the render scale is STILL not shown |
| D20 | [`handoff/history/0.1.0-beta.3.md`](handoff/history/0.1.0-beta.3.md) | the bug bundle does not get the machine's hardware |
| D21 | [`handoff/history/0.1.0-beta.4.md`](handoff/history/0.1.0-beta.4.md) | a process an anti-cheat driver protects is TargetUnreadable, and nothing offers a way past it |
| D22 | [`handoff/history/0.1.0-beta.5.md`](handoff/history/0.1.0-beta.5.md) | the per-game guard override (D18) is withdrawn; a finding about the game turns its hooking off |
| D23 | [`handoff/history/0.1.0-beta.5.md`](handoff/history/0.1.0-beta.5.md) | Tier 2 is a held session: duration, telemetry and the reason |
| D24 | [`handoff/history/0.1.0-beta.5.md`](handoff/history/0.1.0-beta.5.md) | a row follows its executable to another drive letter when exactly one other root holds it |
| D25 | [`handoff/history/0.1.0-beta.6.md`](handoff/history/0.1.0-beta.6.md) | a file name is not an identity |
| D26 | [`handoff/history/0.1.0-beta.6.md`](handoff/history/0.1.0-beta.6.md) | the Agent merges two entries for one executable under two drive letters |
| D27 | [`handoff/history/0.1.0-beta.6.md`](handoff/history/0.1.0-beta.6.md) | other bytes at the entry's path on another letter: the entry follows them with hooking off |
| D28 | [`handoff/history/0.1.0-beta.6.md`](handoff/history/0.1.0-beta.6.md) | HHW is measured through an entry pointed at swiftshader\Game.exe |
| D29 | [`handoff/history/0.1.0-beta.7.md`](handoff/history/0.1.0-beta.7.md) | a per-entry Recording switch (games.record_sessions) and the import's known-tools list |
| D30 | [`handoff/history/0.1.0-beta.8.md`](handoff/history/0.1.0-beta.8.md) | strict anti-cheat rules: any .sys in the install tree, and per-title lists |
| D31 | [`handoff/history/0.1.0-beta.8.md`](handoff/history/0.1.0-beta.8.md) | the store half's matrix |
| D32 | [`handoff/history/0.1.0-beta.8.md`](handoff/history/0.1.0-beta.8.md) | games blocked by anti-cheat: hide the hooking part, and scan the library ahead |
| D33 | [`handoff/history/0.1.0-beta.9.md`](handoff/history/0.1.0-beta.9.md) | the user-mode anti-cheat exception (CLAUDE.md rule 2, amended) |
| D34 | [`handoff/history/0.1.0-beta.10.md`](handoff/history/0.1.0-beta.10.md) | the admin mode asks Windows at EVERY Agent start |
| D35 | [`handoff/history/0.1.0-beta.10.md`](handoff/history/0.1.0-beta.10.md) | the tested-games list comes from a COPY of the owner's ledger (scratchpad, Python sqlit… |
| D36 | [`handoff/history/0.1.0-beta.10.md`](handoff/history/0.1.0-beta.10.md) | legal text changes where it would become false (Privacy 2.5, Disclaimer 2.8; EULA stays… |
| D37 | [`handoff/history/0.1.0-beta.10.md`](handoff/history/0.1.0-beta.10.md) | each PR merges when its CI is green, then the release PR and the GPG-signed tag |
| D38 | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) | the user-mode exception's evidence moved under the grant, as a trial |
| D39 | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) | charts across a game's sessions: both — Sessions at a glance on the Sessions tab, and A… |
| D40 | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) | Delete all sessions: both places — the game's page and Settings ▸ Data |
| D41 | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) | Refresh does all three: reload the library, offer the games the stores installed since,… |
| D42 | [`handoff/history/0.1.0-beta.11.md`](handoff/history/0.1.0-beta.11.md) | the library switches between a grid that fills the window and a list (ui.library_view) |
| D43 | [`handoff/history/0.1.0-beta.12.md`](handoff/history/0.1.0-beta.12.md) | the game's memory is measured from OUTSIDE the game, in both tiers |
| D44 | [`handoff/history/0.1.0-beta.12.md`](handoff/history/0.1.0-beta.12.md) | a user guide in English, guide/, rendered in the App (Help ▸ User guide), linked from t… |
| D45 | [`handoff/history/0.1.0-beta.12.md`](handoff/history/0.1.0-beta.12.md) | the legal split |
| D46 | [`handoff/history/0.1.0-beta.12.md`](handoff/history/0.1.0-beta.12.md) | documentation is corrected in place (struck, dated) with an index, docs/README.md; hist… |
| D47 | [`handoff/history/0.1.0-beta.14.md`](handoff/history/0.1.0-beta.14.md) | the update channel follows the running copy |
| D48 | [`handoff/history/0.1.0-beta.14.md`](handoff/history/0.1.0-beta.14.md) | the guard's narrow fix, both halves |
| D49 | [`handoff/history/0.1.0-beta.14.md`](handoff/history/0.1.0-beta.14.md) | all four metric groups |
| D50 | [`handoff/history/0.1.0-beta.14.md`](handoff/history/0.1.0-beta.14.md) | the managed binaries' identity |
| D51 | [`handoff/history/0.1.0-beta.15.md`](handoff/history/0.1.0-beta.15.md) | the scan set cuts a parent/child link only when it is proven wrong: both creation times… |
| D52 | [`handoff/history/0.1.0-beta.15.md`](handoff/history/0.1.0-beta.15.md) | the App's --data-dir is a viewer over a copy: no Agent is started or contacted (its com… |
| D53 | [`handoff/history/0.1.0-beta.17.md`](handoff/history/0.1.0-beta.17.md) | the wiki's source is the repository |
| D54 | [`handoff/history/0.1.0-beta.17.md`](handoff/history/0.1.0-beta.17.md) | the wiki's End-user half is the user guide, one source: it grows past D44's eight pages… |
| D55 | [`handoff/history/0.1.0-beta.19.md`](handoff/history/0.1.0-beta.19.md) | a game you uninstalled leaves the library by itself when it has no session (amends D41'… |
| D56 | [`handoff/history/0.1.0-beta.19.md`](handoff/history/0.1.0-beta.19.md) | an NW.js runtime is the import's game: an executable beside nw.dll + package.json ranks… |
| D57 | [`handoff/history/0.1.0-beta.21.md`](handoff/history/0.1.0-beta.21.md) | the four itch "launchers" are malware: the documentation is corrected, the import is no… |
| D58 | [`handoff/history/0.1.0-beta.21.md`](handoff/history/0.1.0-beta.21.md) | Wuthering Waves is on the title list (Steam 3513350, Anti-Cheat Expert), beside the fix… |
| D59 | [`handoff/history/0.1.0-beta.21.md`](handoff/history/0.1.0-beta.21.md) | Play, in two modes switched in Settings: "1: press Play → the platform opens → it runs;… |
| D60 | [`handoff/history/0.1.0-beta.23.md`](handoff/history/0.1.0-beta.23.md) | a game's artwork is what is already on the PC |
| D61 | [`handoff/history/0.1.0-beta.23.md`](handoff/history/0.1.0-beta.23.md) | the band takes the artwork's colour as far as every text on it reads: primary and secon… |
| D62 | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) | the mix: every frame-generation state a session held, with its share, never averaged |
| D63 | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) | the NVIDIA App's override is the quality mode where the driver reports it applied |
| D64 | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) | DLSS labels are evidence-bound: versions and folder class, DLSS 4/4.5 only when shown, 5 only loaded |
| D65 | [`handoff/history/0.1.0-beta.25.md`](handoff/history/0.1.0-beta.25.md) | FSR's quality mode through ffxQuery — deferred from beta.24, built in beta.25; none at a dynamic resolution |
| D66 | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) | the long documents are split; every original path stays as the index of its parts |
| D67 | [`handoff/history/0.1.0-beta.24.md`](handoff/history/0.1.0-beta.24.md) | SonarCloud stays on Automatic Analysis with .sonarcloud.properties; the owner marks in its UI |
| D68 | [`13_CI_CD.md`](13_CI_CD.md) §SonarCloud | SonarCloud is analysed from CI (ci.yml's sonarcloud job, SonarQube.Analysis.xml), with coverage; replaces D67 |
| D69 | [`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) §User-facing consent | Enter enables once the consent phrase is typed exactly; before it Enter does nothing |
| D70 | [`08_UI.md`](08_UI.md) §Games › Trend | the hardware-change markers are numbered badges, their changes listed under each chart, with a tooltip |
| D71 | [`13_CI_CD.md`](13_CI_CD.md) §Dependabot and the NuGet audit | Velopack and vpk 1.2.161 in beta.30 (item 24(d)'s Velopack half) |
| D72 | [`13_CI_CD.md`](13_CI_CD.md) §Dependabot and the NuGet audit | beta.31 takes the remaining updates inside the seven-day cooldown: .NET 10.0.14 packages, Markdig 1.5.0, CsWin32, Meziantou, coverlet 10.1.0, Catch2 3.16.1 |
| D73 | [`12_BUILD.md`](12_BUILD.md) §The alpha line | the alpha line: a released beta's source on a prerelease .NET (.NET 11 RC3 first), on the `alpha` branch and Velopack's `alpha` channel; it replaces the beta, shares its data folder and copies the ledger before it first opens it |
| D74 | [`08_UI.md`](08_UI.md) §First-run flow | the alpha's warning: the Alpha notice with its own tick box in the Legal Gate, the alpha's Agent waiting for it, an ALPHA badge and a banner, the update channel locked |
| D75 | [`../legal/EULA.md`](../legal/EULA.md) §2B | EULA 1.6 and Disclaimer 2.12 name alphas beside betas; the Alpha notice 1.0 approved with the plan |
| D76 | [`12_BUILD.md`](12_BUILD.md) §The alpha line | alpha.1 is a retarget only: `net11.0`, C# 15 by `LangVersion latest`, three runtime-serviced packages; no preview feature, no C# 15 syntax |

---

## Keeping this file honest

It goes stale the moment it starts describing state. If you find yourself writing "X is
done" here, that belongs in `CHANGELOG.md` or §S24 — put it there and write a pointer.
The only things that belong here are: **what to do next and in what order**, **why that
order**, **decisions that live in no other file**, and **traps**.

**Since 2026-10-19 (beta.24, D66) this file is split, and the split is kept up by the release:** at each release
the previous START HERE section moves, verbatim, to `docs/handoff/history/<version>.md` and gets its row in
§How this file is split; an owner-only item the owner reports passed moves to `handoff/owner-only-passed.md`
with its number; a new decision gets its row in §Decisions index. `tools/docs-split-check.ps1` holds the
history index and its files to each other.
