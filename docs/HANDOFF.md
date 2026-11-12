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
| `§beta.32` | [`handoff/history/0.1.0-beta.32.md`](handoff/history/0.1.0-beta.32.md) |
| `§beta.33` | [`handoff/history/0.1.0-beta.33.md`](handoff/history/0.1.0-beta.33.md) |
| `§beta.34` | [`handoff/history/0.1.0-beta.34.md`](handoff/history/0.1.0-beta.34.md) |
| `§beta.35` | [`handoff/history/0.1.0-beta.35.md`](handoff/history/0.1.0-beta.35.md) |
| `§beta.36` | [`handoff/history/0.1.0-beta.36.md`](handoff/history/0.1.0-beta.36.md) |
| `§beta.37` | [`handoff/history/0.1.0-beta.37.md`](handoff/history/0.1.0-beta.37.md) |
| `§beta.38`, `§NEXT` (the owner's request of 2026-11-11, the four trains) | [`handoff/history/0.1.0-beta.38.md`](handoff/history/0.1.0-beta.38.md) |
| `§beta.39`, train B's plan as approved (its `§NEXT`) | [`handoff/history/0.1.0-beta.39.md`](handoff/history/0.1.0-beta.39.md) |
| `§beta.40`, what the owner's first sessions with it showed, train B's traps | [`handoff/history/0.1.0-beta.40.md`](handoff/history/0.1.0-beta.40.md) |
| `§beta.41`, what the owner's sessions on beta.40 and beta.41 showed, the plan of 2026-11-12 (D119–D122) | [`handoff/history/0.1.0-beta.41.md`](handoff/history/0.1.0-beta.41.md) |
| `§beta.42`, what the owner's seven sessions on beta.42 showed (D121 answered, the guard's lost launches), beta.43's design as planned | [`handoff/history/0.1.0-beta.42.md`](handoff/history/0.1.0-beta.42.md) |
| `§Owner-only`, an open item | [§Owner-only](#owner-only--no-pr-can-close-these) below |
| `§Owner-only`, a passed item (3, 12, 14–19, 38, 39) | [`handoff/owner-only-passed.md`](handoff/owner-only-passed.md) |
| `§Traps`, `§Two corrections carried forward` | [`handoff/traps.md`](handoff/traps.md) |
| `Dnn` | [§Decisions index](#decisions-index) below |

---

## **START HERE** — 2026-11-12, 0.1.0-beta.43: two hooks installed only where asked for, the guard reads a moving list again, an UPSCALE counted once

*Status is `docs/changelog/0.1.0-beta.43.md`; this is what no other file carries.* The owner ran beta.42 the morning
it was released; seven sessions, a copy of the ledger and the logs answered D121 (Cyberpunk 2077's FSR is 2.1, built
into the game), showed the guard losing launches to a module list that moved while it was read, and showed the
FidelityFX 2.3 loader handing a call on to the leaf's export. The owner decided D123–D125 and approved the plan the
same morning, Privacy Policy 2.17's wording with it. Five pull requests and this one: #413 (the guard reads again,
D123), #414 (an UPSCALE is counted at the first source that saw one, D125; D124), #415 (`slSetConstants`), #416
(`ffxConfigure` on the four ffx-api modules), #417 (the Agent, the App, Privacy Policy 2.17). **What the owner's
sessions on beta.42 showed, and the design as planned, are
[`handoff/history/0.1.0-beta.42.md`](handoff/history/0.1.0-beta.42.md)**; what each pull request built is
[`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) §Pre-injection checks,
[`17_HOOK_ENGINE.md`](17_HOOK_ENGINE.md) §Hook inventory (the two rows, with their measured cost) and
[`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B.

### What this release decided, beyond D123–D125

- **A hook that exists only for Advanced metrics is not put into a game that was never asked.** The two rows are
  gated at the top of `InstallByFamily`, before the symbol is resolved (resolving holds the module). Once a
  session with the option on has started in a process, the patch goes in on that watchdog tick and **stays for the
  life of the process**: a later session with the option off finds the detour there, forwarding without looking.
  Removing a patch between sessions would be a second patch event in a running game for nothing gained. So "off
  means off" (D111) is, for these two: nothing read, nothing published — and, in a game no session ever asked,
  nothing patched. Privacy Policy 2.17 §4 says it that way.
- **Privacy Policy 2.17, not 2.16 stretched.** beta.40's plan had 2.16 stand "once for the three releases"; its
  text then listed DLSS's and FSR's parameters by name, and neither list holds Streamline's constants or the
  frame-generation configuration. The plan of beta.43 carried the new sentences and the owner approved them with it.
  **beta.44 reads feature versions and requirements — check 2.17's lists the same way before assuming it covers
  them.**
- **For each of the two calls a session says which of three things happened** (`AdvancedInGameCall`): read; hooked
  and the game never called; not hooked. "Not hooked" is three causes the session does not tell apart — the game
  did not load the library, a Streamline 1.x interposer (refused), a patch the registry refused — and the Overlay's
  log does: a `HOOK_INSTALLED` line naming the function, or a `PATCH_FAILED`.
- **A count is not part of what makes a session's facts new.** `sl.constants.resets` grows all session; the
  recorder's context would be rewritten each second. `InGameAdvancedSampler.Conclude` brings it up to date once, at
  the end. A session recovered after a crash holds the count as of the last time some other field moved — a lower
  bound.
- **Streamline's summaries are the first viewport's**, and the configure count has an owner slot of its own (the
  2.3 loader would otherwise bring one configure to two modules, as D125's dispatch).
- **The accuracy statement no longer says nothing of what a game tells its upscaler was read from a real game.**
  FSR's upscaling values were (beta.41, beta.42: Lies of P, Dying Light: The Beast, Kingdom Come: Deliverance II).
  The DLSS options, Streamline's constants and the frame-generation configuration are still unconfirmed, and the
  three copies say so.

### Traps this train left

- **An unpatched export may already begin with a jump.** The loader stub's `ffxConfigure` starts with `E9` in the
  file: "the first byte is not a jump" is not a test of "not patched". `ReadTargetExportBytes` compares the target's
  bytes with the file's own (`inFile`).
- **A branch stacked on a pull request that is then squashed**: `git rebase --onto origin/main <the old tip>`, and
  `git diff <old tip> <squash>` empty says the gate that ran still stands.
- **`mcp__ccd_pr__set_auto_merge` says "not bound" for a pull request opened from a script**: `gh pr merge <n>
  --auto --squash`. A run's artifacts while it is still in progress: `gh api
  repos/…/actions/artifacts/<id>/zip`.
- **clang-format re-wraps what an edit script wrote**, so a second script's anchors must be read from the formatted
  file; a multi-file script that fails part-way has already written the earlier files.
- **One local gate printed 871 App tests where every other printed 872**, green. Not looked into.

### NEXT

1. **The owner runs item 41** — the first time either new hook is in a real game. Not first on a game under the
   user-mode exception's trial (D38: a crash there ends the exception for good).
2. **0.1.0-beta.44** — `slGetFeatureVersion` and `slGetFeatureRequirements` — when the owner says beta.43 passed.
   Both are calls a game makes while starting, so most sessions will have missed them: plan what "not seen" says.
3. **Trains C and D** (beta.45, beta.46) each begin with a spike and end in the owner's go or no.

### Left open by this session

- ~~**This release's own facts** — the tag's run, the digests, the delta — are not in any file yet: the release is cut
  after this text. Record them in the next pull request, with what SonarCloud and Qodana said of #413–#417 (the
  `sonarcloud` job on `main` is cancelled by the next merge, so it answers on the release's commit).~~ **Recorded
  2026-11-12: 0.1.0-beta.43 was released 2026-11-12T06:57:57Z** (13:57 in the owner's time zone) from the signed
  tag on `6c7ba74` (#418), by run 43169675460 — green at the first attempt, one run for the one tag push. Eight
  assets; the seven digests of `SHA256SUMS.txt` equal the ones GitHub computed; the four shipped natives carry
  `FL_BUILD_ID v0.1.0-beta.43`. The delta from beta.42 is 1,934,264 bytes beside a full package of 110,736,398 —
  "Delta processed 0452 files. 0025 patched, 0427 unchanged". The wiki was published by run 43171842051. The
  rehearsal on the release branch (run 43167388933, the same tree before it was rebased onto #417's squash) had
  made a delta of 1,944,086 bytes with the same file counts. **SonarCloud answered on `6c7ba74`: the quality gate
  passed and no issue is open on `main`. Qodana's two reports are green on that commit**, and were on #417's.
- **No real game was run on this build.** Two new patch sites, tested with the harness and the stubs; their cost
  measured on the headers. Whether a real `sl.interposer.dll` 2.x lays `sl::Constants` out as the vendored header
  does is checked on every call by the struct's GUID and version, not by a real game yet.
- **Item 40 (d)** — a session that changes upscaler — was not exercised, and the owner has not said item 40 passed.
  **Items 36 (d) and 37 are still open.**
- **What an empty process snapshot does to the watcher's tick in a real session** was not looked into
  (`ToolhelpProcessSnapshotSource.Take` returns an empty list and says nothing; seen once in a test on 2026-11-12).
- **`LIMITATIONS.md` and the accuracy statement still say the processor's, the memory's and the motherboard's
  sensors "have never been read on real hardware"** — the owner's to reword. The alpha line still rests (D98).

**Owner-only:** item 41; item 40 (d).

## Owner-only — no PR can close these

*The items that passed — 3, 12, 14–19, and 38 and 39 on 2026-11-12 — moved verbatim to [`handoff/owner-only-passed.md`](handoff/owner-only-passed.md) (the first eight on 2026-10-19, D66); the numbers are kept, and a number is never reused.*

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
   loads (*Loaded:* line); `nvngx_dlssnr.dll` is then corrected in `CensusNames` and `05_DETECTION`. *(2026-11-06, `spike-notes` §26:
   Hogwarts Legacy, Neural Rendering on, loaded `nvngx_dlssnr.dll` 310.9.16.0 from the game's folder — the name holds;
   whether it ran is what beta.34 measures, item 30(a).)*
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
27. **0.1.0-alpha.1 on real hardware** (D73, D74). (a) **Installing it over beta.32**: `FrameLedger.App-alpha-Setup.exe` from
   its release page — Velopack asks whether to replace the installed FrameLedger; the terms window shows the Alpha notice
   with its warning box, and Accept stays off until the box is ticked; *Decline* closes the App. (b) **While it runs**: the
   ALPHA badge (its tooltip names .NET 11.0.0-rc.3), the title *FrameLedger Alpha*, the banner at start (closable; its
   button opens the notice); About reads *Alpha build — runs on .NET 11.0.0-rc.3…*; Settings ▸ Updates ▸ Channel is locked
   with its line. (c) `%LOCALAPPDATA%\FrameLedger\backups\ledger-before-0.1.0-alpha.1.db` exists and opens. (d) One
   hooked (Tier-1) session of a game measured before records as on the beta; the Agent's log names the release line and
   .NET 11. (e) **Going back**: the beta's `FrameLedger.App-win-Setup.exe` over it — the beta shows the alpha's session.
   (f) The `ja` strings marked `review` that only an alpha shows (`Alpha_*`, `FirstRun_Alpha_*`, `FirstRun_Doc_Alpha`,
   `App_Title_Alpha`, `About_Runtime_Alpha_Format`, `Settings_UpdateChannel_AlphaBody`).
28. **beta.33 on real hardware: the Xbox titles** (D77–D81). (a) **The update from beta.32** (or alpha.2's, item 29): the
   terms window shows the Privacy Policy 2.13 once. (b) **Import library** offers the eleven Xbox titles as *Xbox* — the
   Unreal titles with their shipping executables (`…-WinGDK-Shipping.exe`, `ValorMortisMSG-Win64-Shipping.exe`), the Unity
   titles with their players, never `gamelaunchhelper.exe` — each with its package version (*Xbox version 1.3.5124.0*).
   (c) Each game's page shows its square logo whole in a square slot and its splash image as the banner, captioned
   *Xbox*. (d) **Play** starts Valor Mortis and FINAL FANTASY through Windows in both Play modes, and from an App run as
   administrator — the notice says Windows is starting it. (e) With consent given, one hooked session of each: Tier 1,
   frames, *late attach*, the engine and platform on the game page. (f) The `ja` strings marked `review`: `Store_Xbox_Format`,
   `Art_Source_Xbox`, `Import_NothingFound`, `FirstRun_Import_Body`, `Settings_GameArt_Body`, `Play_Xbox_Format`,
   `Play_Xbox_NotStarted_Format`, `Play_PackagedGame_Format`, `Play_ToolTip_Store`, `Play_ToolTip_Direct`,
   `Settings_PlayMode_Body`, `Guard_TargetAppContainer`, `Guard_TargetMitigation`. Then
   `docs/LIST_GAME_TESTED.md` gains the Xbox titles from a ledger copy (D35), approved before the commit. *(2026-11-06,
   beta.34: done — inKONBINI and Valor Mortis, with the refresh the owner approved with the plan.)*
29. **0.1.0-alpha.2 on real hardware** (D73, D82). (a) **The update from alpha.1**: Help ▸ Check for updates offers
   alpha.2 — the first update an alpha's updater has found — and *Restart to update* installs it; say whether it
   downloaded the delta or the whole package. (b) The terms window shows the Privacy Policy 2.13 and the Alpha notice
   with its box again, and Accept waits for the tick. (c)
   `%LOCALAPPDATA%\FrameLedger\backups\ledger-before-0.1.0-alpha.2.db` exists beside alpha.1's, and opens. (d) Item 28's
   Xbox checks, on the alpha.
30. **beta.34 on real hardware** (D83–D86). (a) **The update from beta.33** (or alpha.3's, item 31): the terms window
   shows the Disclaimer 2.13 and the Privacy Policy 2.14 once. (b) **Hogwarts Legacy with Neural Rendering on**, one
   hooked session → *DLSS 5 (neural rendering on N % of rendered frames)*; then **off** — `spike-notes` §26's table,
   written before the run, decides (on and 0 %: neural rendering runs outside `slEvaluateFeature`, the line stays
   *runtime loaded, not seen running*, and the session's `sl_tag_census` is recorded before anything is named).
   (c) **Phantom Blade Zero** under the NVIDIA App's *Dynamic, up to ×3* → *DLSS 4.5 (Dynamic multi frame generation, set
   in the NVIDIA App)*; the game's own ×3 with no override from the App → *DLSS 4*. (d) **Games** from any page: the
   cards at once, a second visit at once; the Debug line `ui: page … shown … ms after its navigation`. (e) **Settings →
   change the language** → the window comes back on Settings. (f) The filters on Games and Compare, the Compare columns
   and selection bar, the selected page's label in the pane, in both themes. (g) The 34 `ja` strings this release adds,
   all marked `review`: `Dlss_Basis_Dynamic`, `Dlss_NeuralRendering_NotSeen`, `Dlss_Feature_NeuralRendering`,
   `Dlss_Basis_NeuralRendering_Format`, `Games_Filter_*`, `Games_Header_Filtered_Format`, `Filter_Clear`,
   `Compare_Filter_*`, `Compare_Count_Format`, `Compare_Count_Filtered_Format`, `Compare_ClearSelection`,
   `Compare_MidSession_Changed`, `A11y_Games_Filter_*`, `A11y_Compare_Filter_*`.
31. **0.1.0-alpha.3 on real hardware** (D73, D87). (a) **The update from alpha.2**: Help ▸ Check for updates offers
   alpha.3 and *Restart to update* installs it; say whether it downloaded the delta or the whole package. (b) The terms
   window shows the Disclaimer 2.13, the Privacy Policy 2.14 and the Alpha notice with its box again, and Accept waits for
   the tick. (c) `%LOCALAPPDATA%\FrameLedger\backups\ledger-before-0.1.0-alpha.3.db` exists beside alpha.2's, and opens;
   the ledger it left is at schema 22. (d) Item 30's checks, on the alpha.
32. **beta.35 on real hardware** (D88–D91). (a) **The update from beta.34** (or alpha.3's): no terms window, the ledger
   unchanged. (b) **Narrator**: a safety notice is read as it appears; the agent's banner going and coming; Tools ▸
   Database maintenance ▸ Check reads its result; a short message at the bottom is read; **H** moves between each page's
   title and its sections; Settings ▸ Theme's options read their words; the Sessions table's cells read their values;
   Tab to a chart says its name and where its numbers are, and its arrows, Ctrl+arrows and A move it without scrolling
   the page; a double-click shows all of it. (c) **Contrast themes**: Aquatic and Desert, and one with an edited colour
   (Windows 10 if at hand) — every page, a dialog, the title bar's agent pill, a short message, a chart; turned on and
   off with FrameLedger open, it follows at once; Settings ▸ Theme shows its line. (d) **Ctrl+1 … Ctrl+5** put the
   keyboard in the page and Narrator says its title. (e) **Animation effects off** (Settings ▸ Accessibility ▸ Visual
   effects): no page slides in. (f) **Remove** on a game's page: the box opens over the window, its Cancel puts the focus
   back on Remove; the light theme's agent pill and a *done* message read clearly. (g) Help ▸ Accessibility shows the
   statement. (h) The 35 `ja` strings this release adds, all marked `review`: `Menu_Help_Accessibility`,
   `Accessibility_Title`, `Settings_Theme_ContrastOn` and 32 `A11y_*` (the names, the chart names and keys, the
   announcements).
33. **Qodana, and beta.36 on real hardware** (D93). (a) **The update from beta.35**: no terms window, the ledger
   unchanged. (b) **The re-pointed bindings, by hand** — the tests pin their text, not a click: on Games, a card opens
   its game and both Play buttons (card and list) start it, greyed in a viewer; on the Dashboard a recent session
   opens; on a game's page a session row's *Open* opens its summary; in a session's summary a Yes/No/N/A chip opens the
   override dialog; a safety notice's button dismisses it; Settings lists the hooked games with *Revoke*, the
   exception rows with *Grant*/*Withdraw*, and the system information lines. (c) **Qodana Cloud**: the trial's end
   changes nothing in CI (the Community linter needs no licence; the token names the project the report goes to). If
   an Ultimate licence is wanted — JetBrains has a programme for open-source projects — `13_CI_CD` §Qodana says what
   moves. (d) **`qodana` as a required check, or not**: advisory today, like `sonarcloud`. (e) **The members nothing
   uses** (`13_CI_CD` §Qodana, 34 and 49 getters): say which are dead, and a pull request removes them.
34. **beta.37 on real hardware — the first build on .NET 11's release** (D94–D98). (a) **The update from beta.36**:
   Help ▸ Check for updates offers beta.37 and *Restart to update* installs it — a download of about 70 MB, not a few;
   the terms window shows the **EULA 1.7 and the Disclaimer 2.14** once and nothing else; the ledger stays at schema 22.
   (b) **A hooked session on .NET 11.0.0**: one title you measure often, Tier 1 — frame times, the upscaler and frame
   generation as before; the Agent's log opens with `release line main, .NET 11.0.0`, and Help ▸ About reads *Runs on
   .NET 11.0.0*. This is the run `20_OPEN_QUESTIONS` §G's table asked for before the move and did not get (D94).
   (c) **The Agent in admin mode and the logon task** still start it — the publish is self-contained on a new runtime.
   (d) **Windows 10 22H2, if a PC or a VM with it is at hand**: does it install and start? No version has ever been run
   there; D95 states it as untested, and one run would be the first evidence either way. (e) **The Japanese** of the
   three alpha strings that changed or are new (`Alpha_Banner_Body_Format`, `Alpha_Banner_Body_Resting_Format`,
   `About_Runtime_Alpha_Format`) and of the first-run summary's first line. (f) **Going back** — only if something is
   wrong: install beta.36 over it; the ledger opens.
35. **alpha.4 on real hardware — the last alpha for now** (D98). (a) **The update from alpha.3**: Help ▸ Check for
   updates offers alpha.4 and *Restart to update* installs it — a large download, the runtime moving from the release
   candidate to 11.0.0. (b) The terms window shows the **EULA 1.7, the Disclaimer 2.14 and the Alpha notice 1.1** and
   waits for the notice's box; `…\backups\ledger-before-0.1.0-alpha.4.db` exists beside the older copies, and opens;
   the ledger stays at schema 22. (c) **The banner says the alpha line is paused** and to install the beta — not the
   experimental-build warning — and Help ▸ About reads *Alpha build — runs on .NET 11.0.0*, with no "prerelease".
   (d) **Then install beta.37 over it**: the installer asks to replace it; the beta opens the ledger, shows no Alpha
   notice, and its update channel is no longer locked.
36. **beta.38, and the two Qodana reports** (D99, D100). (a) **The update from beta.37**: Help ▸ Check for updates
   offers beta.38 — a small download — and *Restart to update* installs it; no terms window. (b) **Links still open**:
   the documentation from the Help menu, a document's page on GitHub from its window, a link inside the user guide
   and inside the update notes, and the source link in Help ▸ About each open in the browser, as before. (c) **Qodana Cloud**: the projects `frameledger` and
   `frameledger-cpp` each show a report from `main` with no problem. A job's log says *Qodana license plan: Ultimate*
   and the day the subscription is active until; when it lapses both jobs go red — advisory, never a blocked merge —
   and `qodana.yaml` says the way back. (d) **`LogNoteW`**: the Overlay has one function nothing calls
   (`src/native/FrameLedger.Overlay/src/dllmain.cpp`). Say whether it is removed — a pull request of its own, eleven
   lines — or kept; either way the `.editorconfig` section that names it is then removed or reworded.
37. **beta.39, Advanced metrics on real hardware** (D101–D109). (a) **The update from beta.38**: the terms window
   shows the **Privacy Policy 2.15** and the **Disclaimer 2.15** once; the ledger moves to schema 23 and every
   earlier session opens as before. (b) **The switch**: Settings ▸ Recording ▸ *Advanced metrics* on — **Advanced**
   appears as the last item on the left at once, **Ctrl+6** opens it; off — it goes, and Ctrl+6 does nothing.
   (c) **This PC, on the RTX 5080**: the facts (firmware, cores, memory sizes, the display's HDR and variable
   refresh), the *Right now* table beside the NVIDIA app or HWiNFO — the first comparison of any of these values
   with another tool — and the chart moving; a fan's *Read at* a few seconds behind the memory's. Leave the page: the
   Agent's log says the reader closes some fifteen seconds later, and opens again when the page does. (d) **A
   session with a real game, the option on**: it is on *Session* with its statistics and its chart; its summary has
   **Advanced metrics…** and the button opens it there; *variable refresh was active* and the colour space against
   what the game and the display really did; the driver's settings for the game against the NVIDIA app. A session
   recorded with the option off is not on the list. (e) **As administrator, with PawnIO installed** — the first run
   there has ever been (`20_OPEN_QUESTIONS` §M10, §M13): the processor per core, the memory and the motherboard's
   sensors appear on *This PC* and *Sources* says *Read*; watch for a pause or a hang when the page first opens,
   and compare a few values with HWiNFO. Without it, *Sources* gives the reason in words. (f) **What it costs**: the
   Agent's processor time and memory during a session with the option on and with it off (NFR-2: ~~1 %~~ 2.5 % of one
   core since D117, 150 MB) — ~~and with that in hand, the answer to §M16~~ §M16 was measured without a game and
   answered on 2026-11-11; what is left for a real game is the hooked session and the elevated one. (g) **The words**: the `vi` strings read as a Vietnamese
   speaker would write them; the `ja` strings marked `review`. (h) **A copy of the ledger** (`--data-dir`) that holds
   such a session: the page is there, opens on *Session*, and *This PC* says it is a copy.
40. **beta.42 — the log that answers D121, the label, the plane at infinity** (D119–D122). (a) **The update from
   beta.41**: Help ▸ Check for updates, *Restart to update* with no game running; no terms window. (b) **Cyberpunk
   2077 with FSR upscaling** (FSR 3 or 4, with or without FSR frame generation — as it was when the upscaler read
   "unknown"): one session of a few minutes of play, then **quit the game from its own menu**. Nothing else is
   needed: `%LOCALAPPDATA%\FrameLedger\logs\overlay-<pid>-….log` of that session is what is read. If the session
   now names FSR, say so — nothing in this release should have changed that. (c) **A game that gave one depth
   plane** — Lies of P, Clair Obscur or Dying Light: The Beast, FSR upscaling, **Advanced metrics on**: the
   session's Advanced page shows both planes, one of them "∞". If it still shows one, the missing plane is not
   `FLT_MAX` and the log of (b)'s kind says nothing about it — say so. (d) **A session that changes upscaler** —
   Dying Light: The Beast as it was (DLSS evaluated, then FSR), or any game switched between DLSS and FSR in its
   menu during one session: the summary names the one that ran longest, with that one's quality and render size,
   and says *settings changed during session*. (e) **Any hooked game, ended by quitting it**: its overlay log has
   lines after the first second — `HOOK_INSTALLED` with a module and a function, `RUNTIME_CENSUS` — and no
   `PATCH_FAILED`; if there is one, its `a=` number is the reason.
   **Four sessions were run on 2026-11-12 and read the same morning** (START HERE §What the owner's first four
   sessions on beta.42 showed): (a) happened — the sessions carry the beta.42 build; (b) read, and D121 answered:
   the session upscaled with FSR 2.1, built into the game; (c) Lies of P and Dying Light show `cameraNear = ∞`;
   (e) all four logs are whole and none has a `PATCH_FAILED`. **(d) was not exercised**, and the owner has not
   said the item passed.
41. **beta.43 — the first real games with the two new hooks, and the guard's re-read** (D123–D125). **Not first on
   a game under the user-mode exception's trial** (D38): a crash there ends the exception for good. (a) **The
   update from beta.42**: Help ▸ Check for updates, *Restart to update* with no game running; **the terms window
   shows once, with Privacy Policy 2.17**. (b) **The guard**: over the games played, the Agent's log
   (`logs\agent-<date>.log`) has no session that ended at a game's launch with `guard=ModuleScanFailed`. If one
   does, its signal now says which kind — *kept changing while it was read* (four reads were not enough) or *came
   back incomplete* — say which. (c) **A Streamline game with Advanced metrics on** (Dying Light: The Beast, or
   Cyberpunk 2077 with DLSS): a session of a few minutes, quit from the game's menu. Its Advanced page shows the
   Streamline rows — jitter, motion vectors, depth, projection, resets — and the line under the session says
   *read*. **Read them for sense**: a field of view between about 0.3 and 2.5 rad, an aspect ratio near the
   screen's, a jitter under 1, planes that are not both zero. Values that make no sense mean the game's struct is
   not laid out as the one FrameLedger was built against: say so, with the numbers, and do not run further games.
   (d) **FSR frame generation on** (Cyberpunk 2077 or Lies of P), Advanced metrics on: the page shows the
   frame-generation rows — on, asynchronous or not, the flags, the rectangle — and the line says *read*. (e) **The
   three answers of the line**: a game with neither library says *not hooked* for both; a game where it says
   *hooked — the game did not make the call* for a library the game plainly uses is a finding, not a pass — say
   which game. (f) **Off means off**: with Advanced metrics off, **start a game afresh** and record a session; it
   has no Advanced button, and its `logs\overlay-<pid>-….log` has no `HOOK_INSTALLED` line naming `slSetConstants`
   or `ffxConfigure`. (A game that was already running when a session with the option on was recorded keeps the
   two patches until it exits — that is by design, and its log will have the lines.) (g) **Nothing else moved**:
   no crash, no `PATCH_FAILED` in any overlay log, frame rates and the frame-generation factor as on beta.42 for
   the same game and settings.
   **What each outcome means, written before the run.** (c) or (d) empty with *not hooked*: the log of that
   session says why — no `HOOK_INSTALLED` for the function (the library is a 1.x interposer or lacks the export)
   or a `PATCH_FAILED` (its `a=` number is the reason). Empty with *hooked — the game did not make the call*: the
   game reaches Streamline or FSR by a way that does not pass the export — beta.44's plan needs to know. A crash,
   or a game that will not start: say so at once; *Disable all hooking* (Settings ▸ Capture) and switching Advanced
   metrics off are the two ways to run without the hooks, and which of them cured it says which hook it was.

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
| D77 | [`spike-notes/25-xbox-gdk.md`](spike-notes/25-xbox-gdk.md) | the Xbox titles measured first, by package activation, read-only, each closed by its window; the decision table before the run |
| D78 | [`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) §The target must be able to take the Overlay | GDK games only; an app-container (UWP) process is refused as `TargetAppContainer`, Tier 2 |
| D79 | [`05_DETECTION.md`](05_DETECTION.md) §Platform signatures & metadata | the platform is "Xbox" (`xbox`), in full: import, artwork, Play, capture, detection, docs and legal |
| D80 | [`08_UI.md`](08_UI.md) §Games | Play on an Xbox game always activates its package, in both modes; the Agent answers `PackagedGame` and records it in attach mode |
| D81 | [`20_OPEN_QUESTIONS.md`](20_OPEN_QUESTIONS.md) §G | title lists stay keyed by Steam id; an Xbox copy is matched by everything but that entry |
| D82 | [`handoff/history/0.1.0-beta.33.md`](handoff/history/0.1.0-beta.33.md) | alpha.2 right after beta.33; on RC3 unless the train reaches it after .NET 11 ships (then SDK 11.0.100, packages 11.0.0) |
| D83 | [`05_DETECTION.md`](05_DETECTION.md) §DLSS generation | the NVIDIA App's Dynamic frame generation, reported applied by the driver in a session where DLSS Frame Generation was identified, is DLSS 4.5 (amends D64) |
| D84 | [`05_DETECTION.md`](05_DETECTION.md) §DLSS generation | DLSS 5 heads the DLSS line where its neural rendering was measured running (`kFeatureDLSS_NR`, `dlss_nr_pct`); loaded only reads "runtime loaded" (amends D64) |
| D85 | [`08_UI.md`](08_UI.md) §Games, §Compare | filters: Games — store, engine, state, installed, played; Compare — game, tier, frame generation (no period); Compare lists every session |
| D86 | [`08_UI.md`](08_UI.md) (the shell) | the left pane's selected item keeps its label: our copy of WPF UI 4.3.0's LeftFluent item template |
| D87 | [`handoff/history/0.1.0-beta.34.md`](handoff/history/0.1.0-beta.34.md) | alpha.3 right after beta.34, on RC3 (before .NET 11's release, 2026-11-10) |
| D88 | [`../ACCESSIBILITY.md`](../ACCESSIBILITY.md) | the accessibility statement at the root, under Help ▸ Accessibility and on the wiki: WCAG 2.2 AA through WCAG2ICT is a goal, never a claim; reports by severity, no promised times |
| D89 | [`08_UI.md`](08_UI.md) §Screen readers | what appears is said to a screen reader — notices, the agent's and the update's banners, the snackbars, a game page's notice, Database maintenance's results — while FrameLedger's window is active |
| D90 | [`08_UI.md`](08_UI.md) §Screen readers | page titles heading 1, section titles heading 2; every chart named with where its numbers are; no series told apart by colour alone |
| D91 | [`08_UI.md`](08_UI.md) §Contrast | Windows contrast themes are followed, whatever Settings ▸ Theme says |
| D92 | [`handoff/history/0.1.0-beta.35.md`](handoff/history/0.1.0-beta.35.md) | beta.35 in this train, no alpha; alpha.4 is the move to .NET 11's release (SDK 11.0.100, packages 11.0.0), a task of its own after 2026-11-10 |
| D93 | [`13_CI_CD.md`](13_CI_CD.md) §Qodana | Qodana Community for .NET from a workflow of its own (the organisation's licence is a 30-day trial, so not the Rider linter): a gate of zero problems, advisory; an inspection is switched off only in `.editorconfig`, with its reason |
| D94 | [`12_BUILD.md`](12_BUILD.md) §The main line's .NET | the main line moves to .NET 11 with 0.1.0-beta.37, the day after it shipped; condition (a) of `20_OPEN_QUESTIONS` §G's table — two alphas on the released runtime — set aside by the owner, not met |
| D95 | [`02_SPEC.md`](02_SPEC.md) NFR-8 | Windows 10 22H2 is not blocked, and is stated as untested and unsupported (Microsoft supports .NET 11 on Windows 11); a processor needs SSE4.2 and POPCNT |
| D96 | [`13_CI_CD.md`](13_CI_CD.md) §C# 15 and the analyses | C# 15 on the main line from beta.37, a construct only where the build, CodeQL and Qodana read it — none of the four yet (Qodana's engine reads none, 2026-11-11), so beta.37 takes .NET 11's API and no syntax; no preview feature |
| D97 | [`../legal/ALPHA_NOTICE.md`](../legal/ALPHA_NOTICE.md) | an alpha is "a beta's source built to try a newer version of .NET, at times a prerelease one": EULA 1.7, Disclaimer 2.14 (with a paragraph on Windows and the processor), Alpha notice 1.1, approved with the plan; the App says "a prerelease .NET" only where the runtime is one |
| D98 | [`12_BUILD.md`](12_BUILD.md) §The alpha line | alpha.4 is beta.37's source on .NET 11.0.0, and then the alpha line rests until a newer .NET has a preview; a resting alpha tells its user to install the beta |
| D99 | [`13_CI_CD.md`](13_CI_CD.md) §Qodana | Qodana for .NET on the Ultimate licence, in two reports from the start: the managed code to the Qodana Cloud project `frameledger`, the C++ to `frameledger-cpp` |
| D100 | [`13_CI_CD.md`](13_CI_CD.md) §Qodana | an inspection is switched off where the engine that raised it reads — ReSharper's in `.editorconfig`, the rest in `qodana.yaml` (amends D93); hints are off as a severity; in code loaded into a game only defects are fixed |
| D101 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | "Thông số nâng cao" — Advanced metrics: the option `telemetry.advanced`, off by default, and off means nothing more is read or stored and the navigation item is hidden; everything reachable inside a game follows in stages, four trains A–D |
| D102 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | stored in full with the session, in tables of their own (`machine_profiles`, `session_advanced`, schema 0023) and not in columns of `sessions`; series under `adv.` names with NaN, not −1, for a tick with no reading |
| D103 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | LibreHardwareMonitor's CPU, memory, motherboard and storage groups only when the Agent already runs as administrator with PawnIO — the page links to that setting and never asks; power-supply and fan-controller groups refused, battery deferred |
| D104 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | identifiers are never read: the card's serial number and unique id, a display's EDID, the list of programs that are encoding |
| D105 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | the navigation item is the pane's last; Ctrl+1–5 keep their pages and Ctrl+6 opens it only while the option is on |
| D106 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | a `.partial` chunk of an unknown type with a right checksum is skipped, where it ended the read |
| D107 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | with no game running the Agent reads sensors only while the page is open |
| D108 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Advanced metrics | "never call a setter" is a build gate: every `NvAPI_*` call of the bridge is held to a committed list |
| D110 | [`20_OPEN_QUESTIONS.md`](20_OPEN_QUESTIONS.md) §M16 | the L2 telemetry layer's cost is measured in a real Agent before anything is changed (`tools/agent-cost.ps1`); what to do about the number is asked after |
| D117 | [`02_SPEC.md`](02_SPEC.md) NFR-2 | asked with the measurement in hand (1.93 % off, 2.04 % on): NFR-2 is amended to ≤ 2.5 % of one core during capture; the sensors keep one reading a second and no code changes |
| D111 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | inside the game too, off means nothing more is read: the Overlay reads new fields and installs new hooks only while the Agent's control word says the session records Advanced metrics |
| D112 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | `slInit` is not hooked in train B; if ever, never `applicationId`, `projectId` or the paths |
| D113 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | train B is three releases: beta.40 fields of calls already hooked (no new patch site), beta.41 `slSetConstants` + `ffxConfigure`, beta.42 `slGetFeatureVersion` + `slGetFeatureRequirements`; each waits for the owner's run of the one before; C is beta.43, D beta.44 |
| D114 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | the field of view is stored as a session's range, never as a series |
| D115 | [`17_HOOK_ENGINE.md`](17_HOOK_ENGINE.md) §Which struct version carries which field | a tail member of a vendor struct is read only where the upstream tags prove it is there |
| D116 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | "never the camera" is a build gate: `tools/overlay-fields-check.ps1` |
| D118 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | told that the sensor-library fix would take the number D113 gave train B's second release, the owner chose to release it at once as 0.1.0-beta.41: the train's second and third releases are beta.42 and beta.43, train C beta.44, train D beta.45 |
| D119 | [`07_IPC.md`](07_IPC.md) §Region 5 | `FLT_MAX` (or +infinity) in an FSR depth plane is stored and shown as "infinite" — three engines pass it for the plane at infinity; for FSR only: for Streamline `FLT_MAX` is the "not set" sentinel and stays absent |
| D120 | [`03_METRICS.md`](03_METRICS.md) §Upscaling | a session's upscaler is the technology named on the most samples, and its quality and render size are read from that technology's samples — not the first one named, with the others' values beside it |
| D121 | [`HANDOFF.md`](HANDOFF.md) §NEXT | Cyberpunk 2077 was set to FSR upscaling and FrameLedger named none: a detection gap, to be investigated — which first needs a native log that survives the game's exit |
| D122 | [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) §Train B | train B's second step is split: **beta.42** has no new patch site (D119–D121 and the patch registry's hardening); **beta.43** is the two new hooks, begun only after the owner has run Cyberpunk on beta.42 and its log was read. The third release is beta.44, train C beta.45, train D beta.46 |
| D123 | [`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) §Pre-injection checks, check 1 | a module list that changed while the guard read it is read again, at most three times, before it refuses — five real sessions on one PC in a week had ended as a safety unhook with no frame recorded; a list that never settles still refuses, and nothing else about what refuses changed |
| D124 | [`HANDOFF.md`](HANDOFF.md) §NEXT | the guard's re-read (D123) ships with 0.1.0-beta.43 and not as a release of its own — the train's numbers do not move a third time; until then a launch can still be lost that way on beta.42 |
| D125 | [`17_HOOK_ENGINE.md`](17_HOOK_ENGINE.md) §Hook inventory | an FSR UPSCALE is counted at the first source that saw one and at no other — a precaution for a loader that hands a dispatch on to the leaf's export (Cyberpunk 2077's 2.3.0 does, for frame generation); no title run so far upscales through one |
| D109 | [`../legal/DISCLAIMER.md`](../legal/DISCLAIMER.md) §5, §5A | asked on 2026-11-11 with both sentences quoted: the Disclaimer goes to 2.15 with the Privacy Policy — PawnIO and the admin mode are no longer "for the CPU temperature" alone; with Advanced metrics on they also read the processor's, the memory's and the motherboard's sensors. The admin mode's disclosure follows (`agent-admin-dialog/2`) |

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
