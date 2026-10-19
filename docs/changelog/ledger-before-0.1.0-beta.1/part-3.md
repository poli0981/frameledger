<!-- Moved verbatim from CHANGELOG.md on 2026-10-19 (beta.24, owner decision D66): part 3 of 5 of the ledger kept before the first tag, newest first. CHANGELOG.md lists every part. -->

## Ledger before 0.1.0-beta.1 (continued, part 3 of 5)

### Changed

- **7a on Dying Light: The Beast closes as measured `N/A`** (docs): the 00:53 capture on the
  whole-resource build still carried no render size on either global-tag route, so the quality
  byte and the render size are `N/A` on this title, honestly, and the derived-label decision
  applies to the titles that print the line. The DLSS-FG-on `presents = tokens` result is now
  five for five; by owner decision it is handled in its own session and PR (`HANDOFF` 7a, §H5).
- **`20_OPEN_QUESTIONS` §H5 has a candidate title for case 3.** Dying Light: The Beast at DLSS
  with DLSS Frame Generation on read `presents = tokens` on four captures (the same title with
  FSR FG read ×2; Cyberpunk on Streamline 2.7.1 read ×3.99), so the report printed `frame
  generation: none` on a generating title. The two readings the record cannot separate and the
  discriminator — the owner's counter — are pre-committed there.

- **A row for `sl.interposer.dll!slSetTagForFrame`, Streamline 2.8's frame-based tag entry
  point.** Dying Light: The Beast ships Streamline 2.8.0, which deprecates `slSetTag` in favour of
  `slSetTagForFrame` (the frame token first, then the same tag list); the title published `Dlss`
  identity on every batch across three captures and the params bit on none, because the only
  params row hooked the deprecated export. The new detour hands the list to the same tag walk, has
  its own trampoline, and is selected by SYMBOL
  through a constant the inventory header binds to its row with a `static_assert` — Pass B forbids
  the literal anywhere else. **The params family is published when whole**, once every tag export
  the loaded interposer has is patched — a 2.7 interposer is whole with `slSetTag` alone, a 2.8 one
  needs both — because the first cut published it on the first row and the frame-based fixture
  measured 38 of 41 records claiming params, the ffx leaves' install-window defect from #110
  again. The stub exports it, the harness tags through it under `--sl-tag-for-frame`, and a Catch2
  case asserts the exact tagged extent arrives on every record with `slSetTag` never called.
- **`tools/vendor-exports.ps1` unions exports across every copy of a module name** and lists every
  distinct version seen, instead of recording the first copy the walk reached. With `sl.interposer.dll`
  installed as 2.7.1 and 2.8.0 side by side, whether the oracle contained `slSetTagForFrame`
  depended on directory order; `docs/vendor-exports.json` is regenerated (ten interposer copies,
  eight versions from 1.5.6 to 2.10.3).

- **The report's `render -> output` line is computed, not "not computed yet".** Every real-title
  run since 2026-08-15 printed the raw sizes two lines above an N/A. `UpscaleExtent` takes the
  MODAL (render, output) tuple over the records that claim both `FL_MEASURED_UPSCALER_PARAMS` and
  `FL_MEASURED_OUTPUT_RES`, and the line prints the two measured sizes, `03_METRICS` §Upscaling's
  `sqrt((outW×outH)/(renW×renH))`, the reciprocal as a render-scale percentage, the record counts,
  and a **SETTINGS MOVED** flag when the window held more than one tuple — modal rather than mean,
  because averaging across a settings change is the classic way a benchmark number stops meaning
  anything. Cyberpunk's 1485×835 at 2560×1440 reads `1.72x (58% render scale)`. **No preset name is
  derived from it**: whether a label such as "≈ Balanced" may appear beside a measured field is
  HANDOFF item 7a's owner decision, and the quality byte stays what the writer measured.

- **The SDK 2.x loader is the fourth AMD row, because three loader-shipping titles read silent
  behind the leaf-only build.** The owner's run on #110 (thirteen captures, nine titles, one launch
  each) landed rows A1–A3 exactly as pre-committed — Lies of P `Fsr3` / `FsrFg` / `118.98 → 238.02
  (×2)` with the two per-frame counts agreeing at 1.00, `none` by count with frame generation off;
  Hell Is Us `FSR (3.1 or 4 …)` / `FsrFg` / ×2 — and produced a row the table lacked: Dying Light:
  The Beast at FSR + FSR frame generation, KCD2 at FSR and Black Myth: Wukong at FSR each dispatched
  **zero** times at any leaf export while the leaves sat in the census. The game calls the loader's
  export, and the signed loader reaches its providers through an object rather than the leaves'
  `ffxDispatch`, so "five identical export tables leave it no other route" was wrong about the
  loader. `amd_fidelityfx_loader_dx12.dll` is now an inventory row alongside the three leaves, the
  `static_assert` that refused it asserts the opposite with the measurement in its message, the
  loader stub forwards through the leaves' FrameLedger-named *direct* entry (never their export) to
  model what was measured, `--probe-ffx-resolve` asserts the forward touched no leaf export, and a
  third harness topology (`--ffx-topology ue`: the two effect DLLs called directly, no loader) keeps
  the leaf detours exercised now that the loader case never reaches them. The K = 1 control with
  all four modules hooked, and the consumer's `frames/upscale-drained`, are what would read 2× for a
  loader that re-entered an export. Also from the run: Expedition 33 at FSR upscaling under DLSS
  frame generation ×3 printed `×2.97`, *technology not identified*, from the UPSCALE count; Cyberpunk
  at FSR 3 generates through the 1.1.x monolith (`FsrFg`) while upscaling through the deferred FSR
  3.0 route; and **Rune Factory: Guardians of Azuma is HANDOFF 7b's statically linked title** — the
  monolith ships beside it and is never loaded, yet the Streamline token count still read
  `59.99 → 119.95 (×2 FG)`, so the ceiling for such a title is the trio with the vendor missing, not
  Presented FPS alone. **Row R6 ran the same night on this build:** Dying Light: The Beast at FSR +
  FSR FG printed `FSR (3.1 or 4 …)` / `FsrFg` / `70.49 → 140.9 (×2 FG)` with `frames/upscale-drained`
  1.01, and KCD2 at FSR the identity with `none` at 1.00 — the loader row carries both, once, which
  is the K = 1 control confirmed on a real title. Wukong at FSR with FG off read silent at the
  monolith and the loader alike with the monolith loaded, and offers no FSR frame generation at
  all: FSR upscaling compiled into its UE plugin (Rune Factory's shape), the monolith shipped and
  loaded for nothing this hook sees — the second static title, and nothing more is owed on AMD
  identity beyond the FSR 3.0 host route.
  `20_OPEN_QUESTIONS` §H11 carries the run tables; `HANDOFF` 7b/7c and `spike-notes` §9 carry the
  corrections.

- **AMD identity is measured through the ffx-api leaves, and the first non-NVIDIA vendor
  reaches the record.** HANDOFF item 7c. Lies of P at FSR 3.1 + frame generation printed
  `upscaler: N/A … frame generation: N/A` plus a census WARNING because the three AMD modules
  export the same five generic names and the identity lives in the descriptor the game passes
  — which needs the vendor's headers to decode, and `20_OPEN_QUESTIONS` §H11 forbids decoding
  it from observation. Five headers are vendored from FidelityFX-SDK **tag `v2.3.0`**,
  types-only under upstream's `Kits/FidelityFX/` layout, with upstream's `docs/license.md`
  verbatim: a binary-only default licence whose **MIT exception list names every one of the
  tree's 845 files** (the signed DLLs included), the five among them, and each header carries
  the grant inline; `license-check.ps1` §2d asserts both per vendored file. `v2.3.0` rather
  than the `v1.1.4` `18_GPU_VENDOR_APIS` first named, because its own reversal condition
  fired: installed titles already ship SDK 2.x effect DLLs (Hell Is Us 4.0.x, Expedition 33
  and Dying Light: The Beast 4.0.2 + 3.1.5) and the sample dispatches `PrepareV2`, which
  `v1.1.4` does not declare; every value and layout read is identical in both tags.
  **Three inventory rows, one compound family, one observer, three trampolines, and the rows
  are the LEAVES** — `amd_fidelityfx_dx12.dll` (the SDK 1.1.x monolith),
  `amd_fidelityfx_upscaler_dx12.dll`, `amd_fidelityfx_framegeneration_dx12.dll` — **never the
  SDK 2.x loader**, which forwards to the effect DLLs through their own exports (the only
  route five identical export tables allow) so hooking it too would count every dispatch
  twice, straight into `fg_factor`; a UE5 title ships the two leaves with no loader at all.
  The detour reads the descriptor's head type and, once matched, `renderSize` (UPSCALE,
  PREPARE) and `frameID` (PREPARE) — rule 4's class of read — and publishes: identity
  (`FSR3` from the monolith; the new **`FL_UPSCALER_FSR_UNVERSIONED`** from the 2.x upscaler
  DLL, which hosts FSR 3.1 and FSR 4 behind one dispatch type — an enumerator, no layout
  change), `renderW/H` exact with quality and sharpness `0xFF` (the ffx-api dispatch carries
  neither, so that is the true value), `fgMode = FSR_FG` on a present that drained a
  generated batch, and `fgEvaluations` from PREPARE's `frameID` counted on a **new** index
  only — this vendor's `slGetNewFrameToken` — falling back to the UPSCALE count on a title that
  never prepares. Streamline keeps precedence once it has ever issued a token, so every title
  validated on §S31 is byte-identical to before. Fixtures: three leaf stubs, a **forwarding
  loader decoy**, `--probe-ffx-resolve` (ctest `fl_ffx_resolve`), and `--hold-presenting-ffx`
  driven at K = 1 / K = 4 on both topologies with the PREPARE issued twice per frame — the
  K = 1 control reads 2.0 for a writer that counts calls, or that hooks the loader.
  `hookinventory-check` Pass C now reads the Overlay's **export table** too: `ffx_api.h`
  declares its entry points `__declspec(dllexport)` unconditionally. Hook-path overhead: the
  present path gains two `exchange`, two loads and a branch; the detour is one 8-byte read, a
  switch and one RMW per dispatch (a CAS loop on PREPARE). Owner-only runs owed: Lies of P at
  FSR + FG and FSR alone, Hell Is Us at FSR, against the table pre-committed in §H11.

- **`none` is reachable by counting, §S31 is resolved, and P0's frame-generation blocker is
  gone.** The owner's second run on #105's monotone count landed on row **P1** of the table
  pre-committed in `20_OPEN_QUESTIONS` §S31: Cyberpunk 2077 off / ×3 / ×4 at `presents/tokens`
  **1.00 / 2.99 / 3.99**, Hell Is Us DLSS + FG ×4 at **4.00**, and `tokens/batch` **1.00** on
  every leg with batches — so a drained Streamline batch *is* an application frame on that
  title, answered by a second per-frame count rather than by any of the four oracles that fell.
  The pre-P1 refusal of ratios near 1 is replaced: a published factor ≤ 1.05
  (`FgWindow.NoneCeiling`) is rung 4's `none` — every present carried an application frame,
  measured — a factor ≥ 1.5 (`ActiveThreshold`) is active, and the band between is refused as a
  configuration no vendor ships. `fgMode` reads `None` / `Active (technology not identified)`
  from the count when the identity hook named nothing; the report gains its third shape, the
  bare `FPS: 84.02` with a `frame generation: none` line — no pair, no factor, no qualifier.
  `15_ROADMAP` item 7 ✅; §S24 counts nine. *Flower in Us* resolved to its browser process,
  injected, and captured three swapchains' worth of presents, so the Overlay loads inside NW.js.

- **The frame-token count keys on a NEW frame INDEX, and the Chromium resolver knows the shape
  with no GPU process.** Both from the run that followed #103 / #104 the same evening.
  Cyberpunk 2077 asks `slGetNewFrameToken` three to four-and-a-half times per application frame
  and receives a distinct object each time, so a pointer-keyed count read the request rate:
  `presents/tokens` 0.22 / 0.64 / 1.31 at off / ×2 / ×4 — `20_OPEN_QUESTIONS` §S31 row **P4**,
  the refinement it prescribed. The detour now reads the frame index — the title-supplied
  `frameIndex`, or the token's own accessor when none is given — and counts it **only when it is
  above every index seen so far**: "differs from the last index" would still count every switch
  between the two or three frames a title keeps in flight. The stub hands back distinct objects,
  and the harness asks three times per frame with the previous frame's index in the middle, so
  the K = 1 control is red for a pointer-keyed writer and for a last-index-keyed one. No factor
  had been published on any leg — the ≥ 1.5 gate held — and the four-leg run is owed again.
  *Flower in Us* still refused through #104's rule because **NW.js has no `--type=gpu-process`**:
  six processes, the GPU in-process in the untyped browser. Second rule: exactly one untyped
  candidate among typed siblings is the browser and the target; two untyped are two instances
  and refuse. The refusal line prints the tree.

- **Chromium-based titles resolve to their GPU process instead of refusing as ambiguous.**
  NW.js, Electron and RPG Maker MV/MZ run several processes from one image path, and *Flower in
  Us* refused as `TargetAmbiguous` on 2026-09-03. The presenting process is Chromium's GPU
  process — it owns no window, so no window-based pick is correct — and Chromium labels it with
  `--type=gpu-process` on its own command line. `TargetResolver` now reads each candidate's
  command line through `NtQueryInformationProcess(ProcessCommandLineInformation)` — served by
  the kernel with `PROCESS_QUERY_LIMITED_INFORMATION`, no `ReadProcessMemory`, no PEB walk, the
  rule-4 line `HeldProcessHandle` already draws — and resolves only when **exactly one** readable
  candidate carries the flag as a whole argument. Two GPU processes, none, or an unreadable
  sibling still refuse; §S27 is untouched (consent keyed on the path, every candidate is that
  path, the guard scans the pid chosen, no `--pid`). `TargetResolverTests` drives it against
  real processes: a uniquely-named copy of `cmd.exe` started three times, one with the flag.

- **The frame-generation producer, chosen with no oracle and built: `slGetNewFrameToken`.**
  HANDOFF item 3 had five candidate routes and four fallen oracles. The one taken needs neither:
  Streamline hands a title **one frame token per application frame** by contract and the title
  must ask for it, so a detour on `sl.interposer.dll!slGetNewFrameToken` — a third inventory row,
  module-scoped, lazily installed — counts distinct tokens between presents, and `fg_factor =
  presents / tokens`. No premise about Ray Reconstruction batches, and it installs on the titles
  where `slEvaluateFeature` is never called (three of five measured). `fgEvaluations` keeps its
  name and its `03_METRICS` definition ("application frames, counted at the source") and changes
  producer; `FL_HOOK_FG_EVALUATIONS` moves to the new row and the evaluate detour claims identity
  only, with the `static_assert` flipped to say so.

  **The one premise it carries is gated, not assumed.** From inside the process, "no frames were
  generated" and "the DLSS-G plugin requested a token for every frame it generated" both read
  1.0. A ratio ≥ 1.5 cannot come from the second, so the trio is published from it now; a ratio
  near 1 is refused by name (`FgWindow.PublishableFactor`) until the owner's Cyberpunk off / ×2 /
  ×4 and Hell Is Us ×4 run lands on row P1 of the table pre-committed in `20_OPEN_QUESTIONS`
  §S31. `none` on a Streamline title is the PR after that run.

  Fixtures: the stub interposer hands out tokens from a pool of eight (a nullptr token would
  have made every call the same frame); `--hold-presenting-fg` issues one token and one
  evaluation per K presents, and `fl_guard`'s K = 1 / K = 4 pair now discriminates the token
  count from the present count. Report labels `evaluations=` → `tokens=`. Hook-path overhead:
  one atomic exchange and one conditional increment on a call the title makes once per frame;
  nothing on the present path changed.

- **Presented FPS, and a runtime census that says what the one number may and may not mean.**
  With this writer, `03_METRICS`' frame-generation ladder lands on rung 0 for *every* title — no
  non-Streamline title sets `FL_MEASURED_FG`, and `Σ fgEvaluations` is 0 on every Streamline title
  measured — so a 2D title with no upscaler and Black Myth: Wukong running DLSS-G through a path
  this writer does not hook printed the same report line. `08_UI` said *"FG not detected →
  `144 FPS`"* while `03_METRICS` rung 0 said `N/A`, not `none`; both were half right.

  **The rule (owner decision 2026-09-03, CLAUDE.md rule 6 amended):** when frame generation is
  not measured, the one number that stands alone is **Presented FPS** — `presents / D`,
  numerically Displayed FPS, named so that "Native" and "Displayed" are only ever printed
  *together* — with a mandatory qualifier chosen by the **runtime census**.

  **The census.** `FlWriterState.runtimeCensus` (`reserved[0]`, no layout bump: the build-id
  handshake refuses an older writer first) is taken on the Overlay's watchdog once a second —
  `GetModuleHandleExW(UNCHANGED_REFCOUNT)` per name in `FL_RUNTIME_CENSUS`, twenty measured
  module names in two families, OR-only, nothing on the present path, nothing patched. `Ran` is
  bit 0 so a writer that never took it publishes 0 and decodes as "nobody looked".
  `hookinventory-check` gains **Pass D**: every census name must be a module `vendor-exports.json`
  has actually seen, because a misspelt name reads as "not loaded", which reads as the 2D case.

  **What it changes in the report.** Under Presented FPS, one of three lines: the census did
  not run; *no known frame-generation runtime was loaded, so this cannot include in-process
  generated frames (statically linked FSR3-FG and driver-level AFMF excepted)*; or **WARNING:
  a frame-generation runtime was loaded (`nvngx_dlssg.dll`) and no evaluation was observed —
  this MAY include generated frames**. The upscaler line stops saying *"our coverage is short,
  not the title's"* — false in the commonest case — and names the three causes it cannot tell
  apart: settings off, an unhooked path, an undecoded vendor.

  **What it never does: produce `none`.** `FL_FG_NONE` / `FL_UPSCALER_NONE` mean a hook ran and
  saw the alternative. FSR is routinely linked statically, so a census-`none` on an FSR3-FG
  title would print presents as native — the exact number rule 6 forbids — with a confident
  label. The census narrows an N/A's reason and raises a warning; it does not close the question.

  Fixtures: `fl_guard`'s present-only case asserts `runtimeCensus == FL_CENSUS_RAN` by equality,
  the Streamline-stub case asserts the interposer bit set and every FG bit clear;
  `amd_fidelityfx_dx12.dll` — the FSR 3.1 facade, which Lies of P ships ALONE while generating
  frames — is in the FG group, so its presence warns rather than reassures;
  `CensusReportTests` drives all three report shapes, the Witcher-3 shape (runtime loaded, no
  hook) and the inconsistent-writer shape. **Hook-path overhead: none** — the census runs only on
  the watchdog. **Run the same day on six titles** (`spike-notes` §9): both qualifiers seen on real
  games with the loader's own names; the census is a startup property on UE5 (identical word at
  off / FSR / DLSS + FG ×4); Hell Is Us is the third title where DLSS is on and `slEvaluateFeature`
  sees nothing; a Chromium-based title refuses as `TargetAmbiguous`, now a scope row.

- **The first telemetry source, and the measurement it exists for: §M5 is answered — GPU
  sensors work unelevated, without PawnIO, on NVIDIA.** `FrameLedger.Application.Telemetry`
  gains the port `18_GPU_VENDOR_APIS` specified (`IGpuTelemetrySource`, `GpuSample`,
  `GpuCapabilities`, `TelemetryLayer`); `FrameLedger.Infrastructure.Telemetry` gains
  `LhmTelemetrySource` (L2) over LibreHardwareMonitorLib 0.9.6 — GPU group only, its own
  poller thread, 1 Hz and never under 500 ms, *throws or hangs twice ⇒ disabled for the
  session*, capabilities value-derived and monotonic so a sensor that never reports is never
  a capability — behind an `ILhmComputer` seam so every branch of the fault policy runs on
  CI, which has no GPU. `SensorMap` carries the name heuristics and their two traps (NVIDIA's
  `GPU Memory` *load* is VRAM-in-use; `Control` is fan duty, not RPM).

  **The instrument is `FrameLedger.CaptureHost probe-lhm [--seconds n]`** — the unshipped
  host's fifth verb, the only one besides `consent list` that names no game, because it opens
  a sensor library in its own process and nothing else. It prints the raw tree before the
  mapped sample before the verdict, and the verdict names a row of the decision table that
  `20_OPEN_QUESTIONS` §M5 carried **before** the run.

  **Row R1, twice.** Unelevated: core and memory temperature, load, VRAM used, core and memory
  clock, package power, fan — eight fields, all five deciding ones among them. The elevated
  control, launched through UAC, returned the same eight. PawnIO was installed on the box and
  never opened: only LHM's CPU and board groups use it. So the sentence already in `README`,
  `legal/EULA.md` and `OperatorDisclosure` — *"whatever hardware telemetry this machine can
  provide"* — is true as written, and `README`'s row now names what that is on NVIDIA.
  `OperatorDisclosure.Version` stays /2. Hotspot temperature is absent on this driver / GPU /
  library and is recorded as N/A, not as a defect. Numbers in `spike-notes` §10; the L2 NVIDIA
  column of the capability matrix is filled; AMD and Intel are exactly as untested as before.

  **One defect the raw tree caught before the verdict did:** the first `SensorMap` mapped
  `D3D Shared Memory Used` — system RAM — to adapter VRAM by a fragment rule.
  `SharedMemoryIsNotVram` pins the fix. Printing the evidence ahead of the interpretation is
  the whole reason the probe is shaped the way it is.

  Closes the `HANDOFF` loose end that `LibreHardwareMonitorLib` shipped into the Agent's
  output under an MPL-2.0 §3.1 obligation while used by zero lines.

### Changed

- **The consent texts now say what DECLINING costs, and the anti-nudge rule is normative.**
  `README`'s safety row, `legal/DISCLAIMER.md`, `legal/EULA.md` and `19_SAFETY` §User-facing consent
  all called a no-injection mode *"the default"*. That mode does not exist. The consent disclosure
  in `OperatorDisclosure` said it too, and added *"Nothing here is required to measure frame
  times"* — **the worst sentence in the tree**, because it told someone about to type
  `I ACCEPT THE INJECTION RISK` that the risk was optional when hooking is now the only
  frame-time path.

  **Removing the fallback makes declining more costly, so the wording gets more careful rather
  than more persuasive.** §19_SAFETY gains a normative rule: both options are enumerated at the
  same grain, neither is recommended, *"limited"* / *"reduced"* / *"degraded"* are not acceptable
  descriptions of Tier 2, no sentence describes what the user **loses**, and Tier 2 is never
  described as temporary — a user deciding today decides against what exists today.

  **The disclosure is also REORDERED.** The Tier-2 sentence used to be the last thing before the
  prompt, so whatever it said read as the set-up for accepting. Anti-cheat risk and terms of
  service now sit there instead.

  **`OperatorDisclosure.Version` bumps /1 → /2**, because /1 carried two sentences that were false
  rather than differently phrased. Traced while doing it: **nothing re-prompts on a version
  mismatch.** The field is written, merged forward, cleared on revoke and read by exactly one
  non-test caller — the `consent list` status line. `HookRequest.FromConsent` never sees it, so a
  /1 record still yields consent. That limit is now written into the type rather than assumed.

### Fixed

- **A candidate with no main module yet is counted as unreadable, not dropped as absent.** `TargetResolverTests.OneInstanceResolves` went red on the hosted runner twice in one hour on 2026-09-04 with `TargetNotRunning` about a process the test had just shown was enumerable: a freshly started process can be in the snapshot before its image is mapped, `Process.MainModule` is then null, and the resolver skipped it without counting it — narrowing the set exactly the way its own comment says "could not look" must not. It is now counted like a process we lack the rights to read (so the answer is `TargetAmbiguous`, never a false `TargetNotRunning`), and the test's "visible" wait means readable, not merely enumerated.

- **Two review checklists were boxes that could not fail**, in `19_SAFETY` §Review checklist and in
  the pull-request template: *"Is there a Tier-2 degradation path if the hook is unavailable?"*
  Under a two-rung ladder the answer is always yes. Replaced with something falsifiable — does the
  session record `N/A` rather than a fabricated value, **and record why**.

- **`CommandLineSurfaceTests` could not fail for the thing that changed.** It asserted
  `Contain("Tier-2")` against the disclosure while claiming to guard `19_SAFETY`'s four statements
  — so any rewrite keeping that literal token would have passed while the claim behind it changed
  completely. It now pins the substance (`N/A`, and the absence of both removed false sentences),
  and the new assertion was **canaried**: restoring one removed sentence turns it red.

- **The capture ladder collapses to two rungs, and Tier 2 is not a measurement.** Owner decision
  2026-08-28, and it is the honest shape once PresentMon was dropped: there is no no-injection
  measurement mode, so nothing holds a middle rung. What was Tier 3 is now Tier 2 — **session
  duration, whatever hardware telemetry the machine can provide, and the REASON nothing was
  measured**. Every measured field reads `N/A` (FR-4.9), and the reason is the payload rather than
  a consolation attached to it.

  **Frame times, FPS and the lows are now Tier-1-only**, which is a real reduction in what this
  product promises: `README` previously offered them without injection.

  **No capture tier needs an elevated Agent any more.** The requirement came from ETW trace
  sessions and there is no ETW rung. That closes §Scope's "Tier 2 requires an elevated Agent" row
  — as false now, not merely vacuous.

  **§M5 was promoted from a design note to a blocker on a user-facing claim.** Whether LHM GPU
  sensors work unelevated without PawnIO is UNMEASURED; if they do not, the DEFAULT Agent's Tier-2
  session is **duration only**, which would falsify the sentence this change puts into `README`,
  the consent dialog and the EULA. All three therefore say *"whatever hardware telemetry this
  machine can provide"* — honest under either outcome, vague on purpose.

  Twenty files. The ones that changed MEANING without changing WORDS are annotated in place —
  chiefly the 32-bit / D3D9 catalogue, "Tier 2" before and after, which went from *frame times
  without injection* to *unmeasurable in v1* across six files.

### Removed

- **`14_TESTING` §Tier cross-validation — a RELEASE-BLOCKING accuracy gate — is struck, and nothing
  replaces it.** It required Tier 1 and Tier 2 over the same frames agreeing within 1%. There is no
  second instrument. Its own last sentence is why this is recorded rather than dropped: it called
  itself *"the check that would have caught the original detection-accuracy problem"*.

  **So Tier-1 frame times are not falsifiable by anything inside this project.** This is the same
  absence that killed the frame-generation oracle — one root cause, two casualties, and until now
  only §S31 was written down. A Tier-2 mechanism would restore both (§G).

- `EtwFrameSource` from the design, `fg_source`'s `etw` value, `HelloAck.etwAvailable`,
  `CaptureError.EtwAccessDenied`, the `PresentMon version` field in `sysinfo.json`, and the
  three-tier dropdown in the bug-report template.

- **PresentMon is DROPPED — owner decision 2026-08-27 — and Tier 2 keeps its place while losing
  its mechanism.** §S31 retired it as a measurement oracle (row P2); this removes it as an
  implementation. It is not bundled, not fetched, not used, and `tools/frametype-oracle.ps1` — the
  parser written for its output — is deleted with it, along with its `build.ps1` gate.

  **Twelve documents named it as the Tier-2 mechanism and every one is corrected**: `CLAUDE.md`'s
  pinned-stack row, `README`'s capture-tier table **and its safety row**, `01_ARCHITECTURE`'s tier
  table and ADR-3, `03_METRICS` rung 2, `04_CAPTURE`'s source table and tier selection,
  `05_DETECTION`'s two Tier-2 rows, `07_IPC`'s `PresentMonMissing` error code, `12_BUILD`,
  `13_CI_CD`, `14_TESTING`'s CSV-parser row, `15_ROADMAP`'s P2 phase and v2 backlog, and
  `legal/THIRD_PARTY_NOTICES.md` plus `legal/licenses/README.md`.

  **The notices row and `license-check`'s claim had to go together, and that is the gate working.**
  `tools/license-check.ps1` bound `Intel PresentMon` to `assets/native/PresentMon.exe`
  bidirectionally; removing the row alone fails with *"no table row mentions Intel PresentMon, so
  its bundling claim cannot be checked"*. Removing a claim is a deliberate act and is recorded as
  one. `legal/licenses/README.md` also listed a `presentmon-MIT.txt` that never existed.

  **§M1, §M2 and §M6 close by DECISION, not by measurement**, and each says so: they were questions
  about a tool that no longer has a subject. §M2's answers are kept in full because every one cost a
  measurement. **§G is not closed — it gets bigger**: *"is the binary committed or fetched?"* becomes
  *"by what mechanism does a shipped build reach Tier 2 at all?"*, and nothing in this repository
  names one.

  **What this costs users, stated where they will read it.** `README`'s **safety** row promises a
  no-injection mode as *"always a way out"*, and `legal/DISCLAIMER.md` §73 and `legal/EULA.md` §33
  both call it **the default** for uncertain cases. The README row now carries the fact that it is
  unbuilt and has no chosen mechanism, so **a refused or unhookable title gets Tier 3** — session
  duration and hardware telemetry. The two legal documents still say "default" without that
  qualification; §G owns the gap and names them. Not fixed here, because changing a legal document
  is not a cleanup.

  **And the discriminator run was attempted and produced nothing**, so it settled nothing: the
  command targeted `explorer.exe`, which may present nothing PresentMon tracks, and "no CSV" is
  indistinguishable from "no `FrameType` column" — the §Traps entry about a canary that dies before
  reaching the gate. Badly chosen target, recorded as such, and moot now.

- **§S31 ran, and PresentMon is RETIRED as the application-frame oracle — row P2.**
  Three legs, three game launches, Cyberpunk 2077 at off / ×2 / ×4. `FrameType` is present and
  **every row of all three legs reads `Application`** — 1,937 / 6,488 / 10,881 rows, no other
  value anywhere, counted off the raw CSVs rather than off the tool's summary.

  **The half that makes it a measurement rather than a shrug:** the two instruments agree on the
  present rate to within **0.3% on every leg** (43.16 vs 43.04, 144.31 vs 144.18, 241.84 vs
  241.80 present/s). They are counting the same stream, displayed rate tracks the configured
  multiplier, and at ×4 roughly three presents in four cannot be application frames — while
  PresentMon classifies **100%** of them as application frames. `tools/frametype-oracle.ps1`
  reported it as a **falsifier** on all three legs and never as a ratio of 1.0, which is the one
  behaviour it was written to guarantee.

  **What P2 retires and what it leaves untouched.** Retired: PresentMon 2.x `FrameType` as the
  oracle for NVIDIA frame generation, and `03_METRICS` rung 2 is narrowed — the honest scope is
  now *no vendor is known to instrument this provider, and one is known NOT to*. **Not answered:**
  §S31's actual question. *Is a drained Streamline batch an application frame?* is exactly as open
  as it was; `presents/batch` still reads 2.00 and 3.99 on the same unverified premise and is
  still unpublishable as `fg_factor`. **Four oracles have now fallen.** `HANDOFF` item 3 goes back
  to the hook routes — unblocked, unmade, and now without an oracle behind whatever is chosen.

  **One sub-question is unmeasured and it changes the REASON, not the action:** whether
  `--track_frame_type` was in effect at all. If the column ships by default, the legs recorded a
  default rather than an absence. Both branches end at *PresentMon did not answer as invoked*,
  which is why the row landed without waiting. The discriminator is one five-second run without
  the flag; it needs elevation, and it is recorded in §S31 with the command.

  **Two findings nobody was looking for.** The `off` leg drained **zero** Streamline batches,
  against `presents/batch = 1.000` at off in §8 — Ray Reconstruction was not on, and §S30
  established RR is what produces batches when FG is off, so our side had two readable legs and
  not three. And the CSV's **first column is named `Application`** while the value being counted
  in column 9 is also `Application`: a parser resolving by position, or grepping for the word,
  counts process names. That rule was written into the oracle before it had ever seen a real CSV.

  §M2's column set is measured at last — 24 columns — and `kFeatureDLSS_G` is zero on all three
  legs, taking that to five capture sets across five titles.

- **Five real-title captures across four titles, and the RT tri-state is complete** —
  `spike-notes` §6 and §8 carry the numbers; §8's per-title table was empty and now has five
  rows. Every run: 40 s, one swapchain, one segment, **0 gaps, 0 dropped**, `faults=0`, all six
  hook families installed, payload hash-verified against the just-built DLL first.

  **`Yes` twice and `No` twice, every verdict agreeing with the game's own settings menu.**
  Cyberpunk 2077 with path tracing on and Black Myth: Wukong at RT High read `Yes`; Rune
  Factory: Guardians of Azuma, whose menu has no ray-tracing option, **and Cyberpunk with every
  RT option switched off** read `No` — the branch that had never been reachable. The second is
  the harder case: a title that *can* ray-trace and is not doing so. Both negatives satisfy all
  three conjuncts (`rtTier` 12, `RtAsBuild` installed, zero evidence over 36,575 and 16,871
  claiming records).

  **The #87 falsifier did not fire, on two independent titles.** `rt_frame_pct` reads **25.0%**
  of the claiming window at ×4 on Cyberpunk and on Wukong — and Wukong got there through the RT
  evidence alone, with no Streamline batches involved at all.

  **HANDOFF item 3's premise generalises from one title to four, and gets stronger.**
  `kFeatureDLSS_G` is zero on every run, and **two of the four titles never call
  `slEvaluateFeature` at all** — Wukong with `sl.interposer.dll` and `sl.dlss_g.dll` both loaded
  and DLSS-G demonstrably running, and Rune Factory. So it is not that DLSS-G avoids that
  export; on half the titles measured nothing goes through it, and `upscaler` correctly reads
  `Unknown` — *a hook ran and could not identify what it saw* — the first time that distinction
  has mattered outside a fixture.

  **§S30's closure survived a test in the reverse direction it never had.** It concluded that
  Ray Reconstruction *replaces* the super-resolution pass, from one configuration. Turning RR
  off on the same title flips the census from `DLSS_RR` 2578 / `DLSS` 0 to **`DLSS` 4242 /
  `DLSS_RR` 0**, which is also the **first observation of `kFeatureDLSS` anywhere in this
  project**, and the local-tag extent arrives on that evaluation exactly as it did on the RR
  one — the same 1485×835.

  **A second FG proxy, covering what the first cannot.** `presents ÷ RT-active presents` read
  **5,764 / 1,441 = 4.0000 exactly** on Wukong, where `presents/batch` is unreadable. Same
  unverified premise, disjoint coverage, still a proxy and not a producer.

  **Two honest absences and one unexplained result.** `FL_MEASURED_UPSCALER_PARAMS` produced
  nothing on three of four titles — §2b's local-tag route is narrower than that entry assumed,
  and the writer published nothing rather than something wrong. And **Alan Wake 2 is not
  explained**: `presents/batch` = 1.00 with `rt_frame_pct` = 96.8% against a menu set to FG 4X
  fits both "generated presents miss our vtable" (§H5 case 3) and "frame generation was not
  running". The operator reported that title would not apply its settings, which is evidence for
  the second, and is why it is **not** written up as §H5 case 3. The discriminating run was not
  taken.

### Added

- **`fl-probe-signer`, and §S19(b)'s three questions answered before anything is designed.**
  That entry has been deferred since 2026-08-05 and its own rationale names the next step:
  *"Build `fl-probe-signer` first, in the shape `fl-probe-guard` established, and answer those
  three questions with measurements before any design is fixed."* This is that probe
  (`ctest fl_signer_probe`). **Nothing under `FrameLedger.Injector` changes with it** — reading
  a row off the table is a separate PR, including the row that says build nothing.

  **The two Q1 results cut opposite ways, which is what makes the work affordable.** The CI
  blocker — `System.Security.Cryptography.ProtectedData.dll` — is embedded-signed and verifies
  offline as `O=Microsoft Corporation`, already in the shipped `trustedSigners`, so the embedded
  half alone would clear that refusal. But `mskeyprotect.dll`, the module §S19(b) was *written
  about*, returns `TRUST_E_NOSIGNATURE` on the embedded route and `ERROR_SUCCESS` only on the
  catalog one. The entry predicted exactly that; it is now measured rather than argued, and the
  expensive `CryptCATAdmin*` half is **not** on the path to the merge gate.

  **§S19(b) also mis-describes the module, and the correction closes the cheapest-looking
  route.** It calls the blocker "a .NET shared-framework assembly". It is not —
  `Microsoft.NETCore.App` 10.0.11 does not contain it. It is a NuGet package assembly (6.0.0)
  reached transitively as `Microsoft.NET.Test.Sdk` → `System.Configuration.ConfigurationManager`
  → `System.Security.Cryptography.ProtectedData`. So "just drop the package reference" fails:
  dropping it means dropping the test SDK.

  **Q3 came back neither pass nor fail, and that is the honest answer.** Under
  `WTD_REVOKE_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL`, `cryptnet.dll` is **newly loaded** — in a
  census bracketing the offline arm alone. Mapped is not transmitted, and this probe has no
  packet counter, so it reports the module and refuses to conclude. The discriminating run is the
  owner's: adapters disabled, same subjects, compare verdicts.

  > **The probe's first version could not have told the two arms apart.** It censused once at
  > the top and once at the bottom with *both* the offline and the default
  > `WTD_REVOKE_WHOLECHAIN` calls in between, so `cryptnet.dll` appeared and the delta could not
  > attribute it — a census spanning both arms of the comparison it exists to discriminate.
  > Fixed before the recorded run. It read like an answer, which is why it is written down.

  **Q2:** about 3.5 ms per module, and **warm is not cheaper than cold** (3.45 vs 3.54 ms over
  20 repeats) — there is no amortisation to plan around. Comfortable against the 30 s re-scan
  only because the scan set is small; a cache *within* one evaluation is admissible, a cache
  *across* evaluations is a re-scan that did not run.

  **The decision table is honest about what it is.** §S30 and §S31 each pre-committed theirs
  before the run. This one could not: the probe had to be run to be finished, since its
  acceptance criterion is that it *prints* answers. The rows are therefore pre-committed only
  for the legs still **unrun** — CI, and the adapters-disabled run — and the entry says so in
  its first paragraph rather than borrowing a discipline it did not follow.

  **And a constraint no measurement can lift.** Wiring the signer half makes `trustedSigners` a
  live allow-widening surface, and the gate over it — `Rules / validate` — is **not** a required
  status check on `main` (§S23-2). `19_SAFETY` already ruled on this shape: *"the boundary of
  what the gate looks at is code."* So even a clean result does not authorise the build. Owner
  decision, recorded in §S19(b).

  Carries a canary: the probe's own unsigned executable must return `TRUST_E_NOSIGNATURE` and
  yield no organisation, so a green run discriminates rather than merely running —
  `fl-baseline-probe` was retired by exactly this class of test. The include block is
  `clang-format`-fenced and says why: `.clang-format`'s `IncludeBlocks: Regroup` sorts
  `<mscat.h>` ahead of `<wincrypt.h>`, and `mscat.h` uses types `wincrypt.h` declares, so the
  alphabetical order fails with twenty errors inside the Windows SDK and one in our own code.

- **`tools/frametype-oracle.ps1`, and §S31 with its decision table written BEFORE the run.**
  HANDOFF item 3's producer decision needs one measurement: is a drained Streamline batch an
  application frame? Three oracles have already fallen, so the mapping from measurement to
  action is committed first, in the file that owns the item — the discipline §S30 used,
  including the part where its table turned out to have holes and was kept unchanged anyway.
  Two of §S31's six rows **retire** PresentMon outright. The tool compares **two dimensionless
  ratios** — PresentMon's `displayed / application` against our `presents / batch` — because
  §S30's own correction records that comparing *rates* needs a shared span, and the span is
  where the previous draft went circular. Its `-SelfTest` runs in `build.ps1 check`.

### Fixed

- **`03_METRICS`' frame-generation ladder presented rung 2 as unconditional, and it is not.**
  Measured 2026-08-20: `--track_frame_type` is a **beta** option in PresentMon 2.5.1 whose own
  help says it *"requires application and/or driver instrumentation using Intel-PresentMon
  provider"*. So `FrameType` reports events a vendor chose to emit rather than classifying any
  present from first principles, and whether NVIDIA's DLSS-G driver instruments Intel's
  provider is unmeasured — which decides whether the rung exists at all on the titles P0 needs.
  The ladder now says so, and adds the consequence: a `FrameType` column that classifies
  nothing is an **absence**, so rung 0 turns it into `N/A` rather than rung 4 turning it into
  `none`.
- **Ray tracing has a producer, and the RT tri-state's `Yes` and `No` are reachable for the
  first time.** `ID3D12GraphicsCommandList4::DispatchRays` and
  `::BuildRaytracingAccelerationStructure` are detoured, `rtFlags` and `dispatchRaysVolume`
  are drained per present with `exchange(0)` beside `g_slSeen`'s, and `FL_MEASURED_RT` is set
  whenever either family is live. `rtTier` and `hooksInstalledMask` already had producers, so
  all three conjuncts of `03_METRICS`' `No` branch are now live. §S29(f) is closed on both of
  its halves, and CLAUDE.md rule 7 is amended per the 2026-08-14 owner ruling: `N/A` applies to
  *naming the technique as RayQuery*, not to whether rays are being traced.

  **Both hooks, because one of them alone is a trap.** A writer with only `DispatchRays` sees
  nothing on an inline-RayQuery title, and its silence is indistinguishable from a real
  negative. The injected fixture pair makes that falsifiable rather than a sentence in a
  document: two harness modes differing by **one recorded call**, sharing their acceleration
  structure, state object, swapchain and loop — `--hold-presenting-dxr` yields dispatch
  evidence and an exact multiple of the fixture's own 64×32×1 volume, `--hold-presenting-rayquery`
  yields AS-build evidence and **zero** dispatch evidence.

- **`fl_rt_accum.h`** — the dispatch-volume arithmetic in a header, with its identities as
  `static_assert`s and its behaviour in `ctest fl_dxr_inputs`, the same split `fl_sl_seen.h`
  uses. **The assertions found a real wrap bug before it ever ran**: `AddedTo(cur, add)` with
  `add` near `UINT64_MAX` — which the saturating product returns for a hostile descriptor —
  overflowed the 64-bit sum and wrapped to a small number that passed the ceiling check. They
  also corrected the justification written above them: two `uint32`s multiply *within* a
  `uint64`, and it is the third dimension that overflows.

### Fixed

- **The ray-tracing hook installed, published its family bit, and never fired — and the reason
  generalises past ray tracing.** A command list's first `Reset()` replaces its class vtable
  with a **per-object** one in which the vendor driver has taken methods over: measured on an
  RTX 5080, `DispatchRays` moves from `D3D12Core.dll` into `nvwgf2umx.dll` while
  `BuildRaytracingAccelerationStructure` stays put. Every game resets its lists every frame, so
  the addresses in an unreset throwaway's vtable are ones no title ever calls for the moved
  methods. The injected fixture caught it exactly — `withDispatch = 0` beside
  `hooks = RT_DISPATCH | RT_AS_BUILD`, a mask bit with nothing behind it — and because the
  *other* hook worked it read as a bug in the dispatch detour rather than in the acquisition
  they share. **The rule: put a throwaway object through the same lifecycle the game's objects
  go through, or it is not a sample of them.** The fix is one call.

- **Two of #86's five pre-flight answers were wrong, and this corrects them.** That probe read
  vtables off **freshly created** lists and compared vtable ARRAYS — neither of which is what a
  hook depends on, since a game's list is Reset and the Overlay patches the FUNCTION a slot
  points at. Corrected, measuring reset lists and comparing functions: **Q3** is now *DIRECT and
  COMPUTE resolve to the SAME functions*, so one detour per method covers both list types
  (#86 said they did not share and the hook must patch two vtables); **Q4** is now *a WARP list
  and a hardware list resolve to DIFFERENT functions*, so a throwaway-device acquisition would
  silently miss every call (#86 said they shared and one would have worked). A new Q5 prints the
  module on each side of the `Reset` so the next machine answers for itself rather than
  inheriting one driver's behaviour.

- **`IsHonest` did not cover `dispatchRaysVolume`**, on either side of the mirror. A volume set
  with `FL_MEASURED_RT` clear is a writer contradicting itself and is the shape a drain that
  cleared one word and not the other would produce; 0 is a real measurement, so only the mask
  bit can tell the two apart. Added to `MeasuredFacts.IsHonest` and to its native twin.

- **The Overlay's "NOT SET, deliberately" list had gone stale three entries deep**, in a comment
  block whose whole subject is which measurements have no producer. It still described
  `FL_MEASURED_UPSCALER_PARAMS` as having "no source in this writer" after `Hook_SlSetTag` gave
  it one. Rewritten to what is actually absent — PSO, VRAM, latency, HDR — with the staleness
  itself recorded.

- **HANDOFF item 4's pre-flight, run before a single ray-tracing hook was written — and it found
  the defect the hook would have shipped.** `fl_d3d12_vtable.h` records the two
  `ID3D12GraphicsCommandList4` slots (72 `BuildRaytracingAccelerationStructure`, 76
  `DispatchRays`), one header and two consumers, and `ctest fl_d3d12_vtable_indices` proves each
  by **behaviour on both list types** rather than by the COM ABI's say-so. `ctest fl_dxr_probe`
  answers four questions the hook design must not guess at, and prints an unanswerable one as
  unanswered rather than as a pass.

  **DIRECT and COMPUTE command lists do NOT share a vtable.** A hook patching only the DIRECT
  class would have missed every AS build and every `DispatchRays` recorded on a compute list —
  and async BLAS builds on a compute queue are ordinary practice. The mask bit would still be
  set and the evidence would be absent, so `03_METRICS`' `No` branch would have published a
  **confident `Ray Tracing: No` about a title that ray-traces every frame**, with all three of
  its conjuncts satisfied and none of them watching this direction. The two vtables hold the
  *same function pointers*, so the fix is a patch applied twice or one inline patch on the
  shared target; what is now excluded is patching one and stopping.

  Also measured: **WARP reports `RaytracingTier` 12 here**, so a DXR fixture is not condemned to
  a GPU box — item 4's "check first whether WARP supports DXR" is answered for this machine, and
  the probe prints the tier on every run so CI answers for itself. And a WARP list and a
  hardware list **do** share a vtable, so a throwaway-device acquisition would have worked; the
  design still takes the vtable off the game's own device, because this machine lost WARP's
  D3D12 path to an Insider build for a fortnight and a design that needs no WARP cannot be taken
  down by one. `spike-notes.md` §6 carries the numbers, `17_HOOK_ENGINE` §Ray tracing the
  constraint.

- **`presents / batch` gets a guard of its own, because the one it had could not fail.**
  `FgWindow.BucketFactors` splits the window into eight buckets and refuses a factor when one
  departs from the whole — and it divides by `Σ fgEvaluations`, which is **zero on every record**
  on the one route a real title has been measured on. So every bucket matched, the check passed
  vacuously, and `RefusalFor` returned at the data-gap clause before uniformity was ever
  considered, while `presents / batch` was printed underneath with nothing behind it. Measured
  2026-08-16: an alt-tab mid-capture produced an achieved ratio of **1.84** against a title
  configured for ×2 — wrong by 8%, and the report said nothing. `FgWindow.BatchRefusal` is the
  per-bucket `presents / batch` check §S30 named as a prerequisite, `FgWindow.PresentsPerBatch`
  moves the arithmetic out of the renderer, and `SessionReport` prints the verdict on the line
  under the ratio so the number cannot be read without it. Proven both ways: a canary returning
  `null` from the check turns the two uniformity cases red and leaves the uniform case and the
  attribution refusals green.
- **The capture host samples whether the operator was actually watching the game**, once per
  10 Hz drain tick, and the session carries the pair `(DrainTicks, ForegroundTicks)`.
  Out of process — `ForegroundWindowProbe` in `Infrastructure`, two documented Win32 calls — so
  it costs the hook path nothing, reads nothing belonging to the target, and needs no record
  byte; §S30 suggested doing it in-process and that would have bought nothing. **The pair,
  because zero is not a finding:** a target owning no top-level window is unfocused on every
  tick of every run, which is what `hook-harness` does, so collapsing "never had focus" into
  "lost focus" would fire on every integration run. It is attribution and never the guard —
  `BatchRefusal` refuses a mixed window from the records alone, so a caller that never wires
  focus still cannot publish an averaged ratio.

### Fixed

- **`hook-harness/CMakeLists.txt` still carried the pre-2026-08-14 subtraction** in the comment
  justifying module-scoped resolution — a fifth site for the formula `fl_shm.h` retracts. The
  argument survives the correction with its polarity flipped: an inflated count now inflates
  Native FPS and deflates `fg_factor`.
- **Ray Reconstruction answered `N/A` on every frame-generating title, and the reason was a
  consumer bug rather than a coverage gap.** `MeasuredFacts.RayReconstructionOf` required
  `FL_FEAT_RAY_RECONSTRUCTION_OBSERVED` on **every** record in the stream, while the writer sets
  that bit only on the present that drained a Streamline batch — roughly one in four at ×4,
  measured 24% on the Cyberpunk 2077 stream. A condition that cannot hold above ×1 meant the
  verdict was decided by the frame-generation setting rather than by whether Ray Reconstruction
  ran: the title evaluated `kFeatureDLSS_RR` on 2,523 of 2,523 batches and this reported `N/A`.
  The population is now the batch-carrying presents — none ⇒ `N/A`, any carrying the fact bit ⇒
  `Yes`, batches with none ⇒ `No`, the branch that was unreachable. The lazy-install prefix drops
  out for free instead of forcing `N/A` over a whole session.

  The comment this replaces reasoned that the alternative would publish a whole-session verdict
  from a single frame and that fixing it needed the application-frame unit HANDOFF item 3 would
  introduce. Both halves were wrong — the natural population is thousands of batches, not one
  frame, and it needs no application-frame unit, which is fortunate because item 3 could not
  produce one. Proven both ways: restoring the `All` rule turns the three positive cases red and
  leaves `NoBatchObservedIsNAAndNeverNo` green, so the fix is not "answer `Yes` to everything".
- **Four comments still carried the pre-2026-08-14 subtraction `F_app = presents − Σ
  fgEvaluations`**, which `fl_shm.h` explicitly retracts — `ShmLayout.cs`, `MeasuredFactsTests`,
  `stub_sl_common.cpp` and `fl-probe-interposer`. The mirror's doc comment also said the count is
  "3 at ×4"; it is 1 per application frame at every multiplier, and the two forms differ by a
  factor of four on the one real title measured. The decoy stub's arithmetic argument was
  reversed by the same correction: an inflated count now INFLATES Native FPS and DEFLATES
  `fg_factor` rather than the other way round.
- **`docs/HANDOFF.md` item 3 contradicted itself inside one entry** — one block recorded that
  three oracles had been tried and fallen, and a later bullet said neither of the two cheap
  measurements had been run. `fl-baseline-probe` was run on 2026-08-16 and retired by its own
  pre-committed falsifier; only the game's own frame counter is still unrun.
- **`docs/HANDOFF.md`'s "`./build.ps1 check` cannot go fully green here" is no longer true**, and
  it was the first thing a new session read. Measured 2026-08-20: the gate is fully green on the
  dev box, 20/20 native tests, and `hook-harness --probe-d3d12` succeeds. Nothing in this
  repository fixed it — the machine moved from Insider build 26300/29639 to **29648**, with
  `d3d10warp.dll` and `D3D12Core.dll` both at `10.0.29648.1000`, so the WARP D3D12 regression was
  fixed upstream. The trap entry is dated rather than deleted: the durable fact is that this
  dependency broke and healed under the machine without a code change, twice giving the native
  suite a colour that said nothing about `main`.

### Changed

- **`spike-notes` §11 is filled, and the news is bad in a useful way.** PresentMon **2.5.1**
  is present and pinned by hash (956,768 bytes, SHA256 `9BEC…A191`) — **it carries no
  VERSIONINFO at all**, so pinning can only mean hash plus filename, which is worth stating in
  a repository that requires that metadata of everything it builds. It does **not** run
  unelevated here: exit 6, and the account is in neither Administrators nor Performance Log
  Users. `PresentMonSharedService` is installed and running as LocalSystem and **does not
  help** — the console starts its own trace session — which answers half of §M6 in the
  direction nobody expected. The 2.x column set is therefore still unmeasured, and
  `tools/frametype-oracle.ps1` **has never seen a real PresentMon CSV**: it resolves columns by
  name, prints the whole `FrameType` vocabulary rather than assuming one, and refuses loudly
  instead of guessing. Said plainly rather than left for the next reader to assume.

- **XeFG and FSR3-FG identity is deferred with a written rationale (§H11)**, which is what
  HANDOFF item 3 asked for instead of a guess. `libxess_fg.dll` and `ffx_fsr3_x64.dll` export
  nameable entry points; the newer `amd_fidelityfx_framegeneration_dx12.dll` (3.1.5, three
  installed titles) exports only five generic `ffx*` names, so identity lives in a struct field
  and hand-declaring a vendor ABI from observation is the #71 defect class with another
  vendor's name on it. The licence checklist has not been run on either SDK either. **The cost
  is coverage, not correctness**: such a title reports `FL_FG_UNKNOWN`, never `NONE`.

- **The ledger now records what five real-title captures actually established, and HANDOFF
  item 3 is struck because its premise is false.** `slEvaluateFeature(kFeatureDLSS_G)` is
  never called by Cyberpunk 2077 — 0 across ~14,000 Streamline batches at four
  frame-generation settings, while frame generation was demonstrably active — so the
  2026-08-14 owner ruling to count evaluations directly is correct arithmetic on a route the
  vendor does not use. The counter, its drain, its consumer and its fixtures are all in and
  gated; there is nothing to count. The open question is no longer "build a counter" but
  "what is the in-policy producer for DLSS-G on Streamline 2.x", and item 3 now lists the
  candidate routes without choosing one.
- **§H5 is NARROWED, not closed, and the wording is deliberate.** `presents / batch` reads
  1.000 / 2.000 / 4.000 against the title's own off / ×2 / ×4 (×4 three times), one
  identified swapchain and 0 gaps in every run — so generated presents ARE visible to a hook
  on the shared `dxgi.dll` vtable and the count tracks the MULTIPLIER, which a two-point
  sweep could not have shown. Recorded as still open: case 3 is narrowed rather than
  answered; **case 2 is made worse** — at ×4 roughly 75% of records carry `syncInterval` /
  `presentFlags` no application call produced, and the writer claims
  `FL_MEASURED_PRESENT_ARGS` on all of them; the proxy's denominator is unverified, because a
  batch equals an application frame only if Ray Reconstruction is evaluated once per frame,
  which no independent oracle has confirmed.
- **`03_METRICS` §Frame Generation and §RT/PT/RR both carry the consequence.** `F_app =
  Σ fgEvaluations` was written when the counter was expected to have a producer; the section
  now states that on the measured route it yields zero and that `presents / batch` must not
  be promoted to `fg_factor` without its premise attached. And RT's per-application-frame
  quantities — `rt_frame_pct`, `rays_per_pixel`, the 5% gate — are left **without a settled
  denominator**, which is now the first thing HANDOFF item 4 has to decide rather than
  something it inherits.
- `17_HOOK_ENGINE`'s FG row stays ✅ and says it counts zero on the measured route — built,
  honest, and yielding nothing is three different facts from unbuilt.
- `15_ROADMAP` item 7's ETW comparison is struck as blocked on a producer rather than on
  tooling, and `spike-notes` §9 — three empty bullets since the file was written, and the
  section named after this item — carries the five-capture table.
- §S30 closed in §S24 with the two defects it produced on the way out: the pre-committed
  decision table had two holes, and the fix contaminated the census that found it.

### Fixed

- **§S30 is ANSWERED, and the answer is that Ray Reconstruction was doing the upscaling all
  along.** Cyberpunk 2077, 2026-08-15, three 40 s captures: with `DLSS_D = True` the title
  evaluates `kFeatureDLSS_RR` on every application frame and `kFeatureDLSS` **not once** —
  2,569 of 2,569 batches, zero DLSS, zero NIS, zero undecoded ids. RR replaces the separate
  super-resolution pass rather than running beside it, and the decode had no arm for it, so
  every record reported `UNKNOWN`. **What makes this evidence and not an inference:**
  `renderW/H` are published only on a frame where an evaluation was seen, and they came back
  `1485x835` against the title's own `DLSS = Balanced` at `2560x1440` — 0.58 exactly. The
  scaling-input tag arrives ON the RR evaluation. `FL_UPSCALER_DLSS` and not a new value:
  layout v3 retired `FL_UPSCALER_RETIRED_RAY_RECONSTRUCTION` precisely because RR is an
  independent axis, which `FL_FEAT_RAY_RECONSTRUCTION` already carries from the same word.
- **The census was measuring its own subject, and the decode fix is what exposed it.**
  `SlCensus` derived "a super-resolution id arrived" from the DECODED `upscaler` byte, so the
  correction above made it report 2,569 arrivals of an id that arrived zero times — an
  instrument that moves when its subject moves cannot be evidence about the subject, which is
  the only job §S30 gave it. `FL_FEAT_SL_SUPER_RESOLUTION` now carries the RAW fact, set from
  the drain word and independent of any decode. An enumerator, not a field: no layout bump.
  It deliberately gets **no OBSERVED companion** against this enum's own convention — all
  three Streamline facts in the byte are published under one condition, so their companions
  would be equal bit for bit, and `FL_FEAT_RAY_RECONSTRUCTION_OBSERVED` already carries it.
- **`UpscalerOf` read the LAST record, which under frame generation is usually the wrong
  one.** The writer publishes an identity only on presents that drained an evaluation — one
  in N — so at ×4 the last record is `UNKNOWN` with probability 3/4, and the report printed
  "a hook ran and could not identify it" three lines below its own raw block printing
  `upscaler=Dlss on 2561 record(s)`. Now scans for any record naming a technology, exactly as
  `FgModeOf` already did, and for the same reason.
- Two `SlCensusTests` fixtures went red on the raw-fact change and were **right to**: they set
  a decoded `Dlss` without the raw bit, which is a state no writer produces. Corrected, and a
  new case pins the discrimination directly — a record with `upscaler == Dlss` and no raw
  super-resolution fact is the RR-decoded shape and must NOT count as an arrival of
  `kFeatureDLSS`.

### Added

- **The FG factor gets a consumer, and most of it is the refusals.** `FgWindow` takes
  `F_disp` and `F_app` from **one record set** — every drained record in one QPC span, not
  the dominant stream — because `g_slSeen` is one process-wide word drained by whichever
  present arrives first, so an evaluation belonging to the game's frame can be consumed by
  a UI swapchain's present. Summing the denominator over one stream while counting presents
  over all of them overstates the factor with no diagnostic. Five refusals, each a number
  this consumer would otherwise have printed as a measurement: nothing counted (a data gap,
  never 1.0); a saturated 255 (a sentinel, not a floor to divide by); a record with
  `swapchainId 0`; more than one stream in the span; and **the frame-generation state
  changing during the session**.
- **That last one was found by an adversarial review of the design, before any of this was
  written, and it produces a number above the physically achievable one.** Half a session at
  ×4 and half with FG off gives a whole-window factor of 8 from an ×4 configuration, and
  `NativeFps` inherits the whole error — while every other guard stays silent, because the
  count is not zero, the factor is not 1.0 and it is not below 1.0. `03_METRICS:133` already
  forbids this for upscaling ("averaging across a settings change is the classic way
  benchmark numbers become meaningless") and lists `fg_mode` as a **segment** column. The
  window is now split into eight buckets and any bucket whose factor departs from the whole
  refuses the lot.
- **`evaluations/batch`, the premise check that needs no oracle.** HANDOFF item 3 rests on
  `slEvaluateFeature(kFeatureDLSS_G)` firing **once** per application frame, and nothing in
  this repository had verified it. The quotient of the two ratios a run can compute —
  `(presents/batches) ÷ (presents/Σ)` — reduces to `Σ/batches`, and it is the ONLY check
  that catches the k-per-frame case: three evaluations per frame at ×4 yields a factor of
  1.34, which is above 1.0 so an over-counting guard is silent, is not 1.0 so a
  structurally-1.0 guard is silent, and still **moves with the setting** so a three-point
  sweep passes too. Batches come from `FL_FEAT_RAY_RECONSTRUCTION_OBSERVED`, which the
  writer sets under `seen != 0` and nothing else — an exact indicator, unlike the params bit
  whose extra tag-validity conjunct makes the ledger's 4.0134 an upper bound.
- **`NativeFps` is counted, not derived.** `Σ / seconds`, not `DisplayedFps / factor`.
  Derived, the rule-6 trio is internally consistent by construction and a reader can
  conclude nothing from that consistency; computed separately, `Native × Factor ≈ Displayed`
  is a property a test checks — to within exactly one frame, because Displayed counts
  intervals and Native counts records, and that difference is asserted rather than rounded
  away. The renderer now prints all three off **one** window or prints one labelled number
  and the reason, which is the half of rule 6 that had no test at all.
- **`SlCensus` — §S30's "print them first", built rather than promised.** Per record, which
  of the five Streamline feature classes arrived, including the undecoded one, which is only
  separable because of the `FL_FEAT_SL_UNDECODED` bit added in the previous commit. It names
  the §S30 shape directly: batches that generated frames carrying **no** super-resolution id.
- **The O1–O5 decision table is committed to `20_OPEN_QUESTIONS` §S30 BEFORE the run.**
  "Measure, then fix" becomes "fix, then justify" the moment the table is written after the
  numbers are known, and nobody can tell the two apart afterwards. The table includes the
  falsifier for its own independent oracle: if `fl-baseline-probe` reports `nvngx_dlssg.dll`
  LOADED with multi-frame generation off, it is not an oracle for FG engagement and must be
  retired in the same `spike-notes` row rather than quietly relied on.
- **A factor of exactly 1.0 is NOT reported as §H5 case 3.** Three causes produce it and this
  data cannot separate them — generated presents never reaching the vtable we patch, FG
  configured off while the feature is still evaluated, and an evaluation that FAILED and was
  counted anyway, because `Hook_SlEvaluateFeature` increments before forwarding and ignores
  the `sl::Result`. The report prints all three. Naming one is how the item gets routed down
  the wrong branch.

- **`fgEvaluations` and `fgMode` get a producer, and the count is of EVALUATIONS.**
  `slEvaluateFeature(kFeatureDLSS_G)` now contributes to a saturating 24-bit count in the
  high field of the same word the feature bits live in (`fl_sl_seen.h`), consumed by the
  one `exchange(0)` `RecordPresent` already performed. A bit could not carry it: a bit
  collapses several evaluations between two presents into one, and under multi-frame
  generation that is the common case rather than the edge — 10,169 presents carried 2,461
  Streamline batches on the one real title measured.
  **The five existing consumers of that word are untouched**, which is why the split was
  chosen over a second atomic: `seen != 0` still means "an evaluation happened this
  present" (any evaluation either sets a feature bit or increments the count), and the
  three bit tests read bits 0-2, which the count cannot reach. Still exactly one
  read-modify-write per call, on either arm — making the count itself the identity removes
  the need for a compare-exchange rather than paying for one on a hook path.
- **`F_app = Σ fgEvaluations`, not `presents − Σ fgEvaluations`** (owner ruling
  2026-08-14), swept across `03_METRICS` §Frame Generation and its export table,
  `fl_shm.h`'s field and mask comments, `fl_hook_inventory.h`'s module-scoping rationale
  and `17_HOOK_ENGINE`'s FG row. The subtraction needs a count of *generated* frames, and
  nothing can produce that in policy: the evaluation fires once per **application** frame
  and yields N−1 generated ones, where N lives in `sl::DLSSGOptions` — set out of band
  through `slDLSSGSetOptions`, the route HANDOFF §2b refused on five grounds. The two
  forms are not interchangeable: on the one real title measured they differ by ×4.
  `native_or_generated`'s polarity inverts with it, and the per-frame classification is
  now documented as exact in aggregate and accurate to one frame per record.
- **`FL_FEAT_SL_UNDECODED` + its OBSERVED companion**, so §S30 can be answered rather than
  guessed at. Once frame generation leaves the feature bitmask, a present carrying
  `kFeatureDLSS_G` alongside any id that falls to `FL_SL_SEEN_OTHER` — Reflex, PCL,
  DeepDVC, Latewarp, DirectSR — is byte-identical to one that carried DLSS-G alone, so a
  consumer counting "presents that carried an id we cannot decode" would read ZERO on a
  title evaluating one every application frame. Cyberpunk runs Reflex. A decision table
  keyed on that bucket would have closed §S30 on a number that could not have been
  anything else. **Enumerators, not fields**: no struct change, so no
  `FL_SHM_LAYOUT_VERSION` bump, no `fl-layout-dump` entry, no `ShmLayout.cs` struct edit.
- **The CONSUMER half is not in this PR, and a capture will still print "FG state not
  measured".** `MeasuredFacts.FgMode`, `NativeFps` and `FgFactor` stay hard-null: the
  arithmetic they need — one record set for both sides of the ratio, a refusal to publish a
  session-level factor across an FG state change, and the presents-per-batch cross-check —
  is its own change with its own canaries, and shipping half of it would put a number in
  front of a reader before the thing that decides whether the number is allowed. So the
  writer measures and the report is silent, on purpose, for exactly one PR.
- **The decode is UNCHANGED, deliberately.** §S30 — a real title decoding every
  params-carrying record as `UNKNOWN` while running DLSS — is not fixed here. HANDOFF
  forbids by name changing the decode before the ids that actually arrive have been
  printed, because that turns a wrong answer into a confident wrong answer. This PR builds
  the instrument.
- **`--hold-presenting-fg` and `--presents-per-eval`**, the first fixture in the tree where
  presents ≠ evaluations. Every other injected fixture evaluates once per present, so a
  writer counting evaluations and a writer counting presents are indistinguishable in all
  of them — every ratio is 1. It also passes the scaling-input extent as a **local** tag
  through `slEvaluateFeature`'s own `inputs` and never calls `slSetTag`, which closes a
  coverage hole this PR would otherwise have widened: `FindScalingInputExtent` has one
  production call site, inside the detour, and nothing reached it — every test called the
  header directly and every fixture passed `inputs = nullptr`. Frame generation now takes a
  different decode arm, so "the arm must fall through to the walk" became a property
  somebody could reasonably tidy away.

### Fixed

- **Four session calculators asked a question no session could answer yes to.** `UpscalerOf`,
  `RayTracingOf`, `HdrOf` and `RayReconstructionOf` each gated on `stream.All(... bit ...)`,
  and feature hooks install lazily from the 1 Hz watchdog — so the opening of every session
  predates them. Measured on Cyberpunk 2077: **292 of 10,169 records carry no `Upscaler`
  bit**, and the consumer therefore reported *"no upscaler hook ran"* about a session in
  which the hook was live for 97% of the presents. `Program.cs` had already grown a per-bit
  record count to explain that in prose. The three hook-liveness axes now aggregate over the
  maximal claiming **suffix**, and the boundary must be **clean** — a bit that goes on, off
  and on again is a writer contradicting itself, and its trailing run must not be averaged as
  though it were a whole session, which is the same defect reached from the other side.
- **`RayReconstructionOf` is deliberately NOT in that sweep, and the reason is now in the
  code.** It looks identical and is not: the other three gate on a *hook-liveness* bit, which
  is monotonic, while this one gates on a *per-present observation* (`seen != 0`), which under
  frame generation is intermittent by construction — one Streamline batch spans ~4 presents on
  the Cyberpunk stream, so the maximal claiming suffix is ONE record and feeding it to the
  `Any` below would publish a whole-session Yes/No from a single frame. Worse than the `N/A`
  it returns today. Fixing it needs the application-frame unit HANDOFF item 3 introduces.
- **`guard_test.cpp`'s honesty assertion was a hardcoded equality where the managed twin was a
  derived subset test.** It compared `measuredMask` against the constant
  `FL_MEASURED_OUTPUT_RES | FL_MEASURED_PRESENT_ARGS` with `!=` — right while the Overlay
  hooked only presents, and a statement about **one build** rather than about honesty. It is
  now derived from `hooksInstalledMask` the way `MeasuredFacts.EntitledBy` is, as a subset
  test, plus the value-without-bit conjuncts. Left as an equality it would have gone **red on
  a correct writer** the moment any feature bit is per-frame rather than per-session:
  `FL_MEASURED_UPSCALER_PARAMS` is gated on `seen != 0`, so under frame generation three
  records in four legitimately lack it. **The derived form is paired with an equality on
  `hooksInstalledMask` itself** — without it a writer that installed everything would be
  "honest" about any claim and the loop would pass while proving nothing.
  Both sides gained two conjuncts they were missing: `fgEvaluations` with `FG_COUNTS` clear,
  and `featureFlags` on a writer not entitled to claim an upscaler.
  **Two copies of one contract with nothing gating their agreement** — the struct mirror has
  `fl-layout-dump`; this does not. Stated in both files rather than left to be discovered.
- **`slSetTag` shipped on 2026-08-15 with no row in `17_HOOK_ENGINE` §Hook inventory**, whose
  own first line says anything not on the list is not allowed to exist and which NFR-11 makes
  the enumeration of the hook set. `hookinventory-check` never reads that file — it checks
  `FL_HOOK_INVENTORY` against `vendor-exports.json` — so a shipped hook missing from the doc
  is invisible to every gate in the tree. Row added; the banner above it said **three** rows
  were built when the answer was five, and said a present-only writer's `measuredMask` is what
  the record carries, which stopped being true at item 2.
- **The removed `presentdelta` rung outlived its removal in two more places.**
  `03_METRICS` retired `GetFrameStatistics().PresentCount` on 2026-08-05 as *structurally*
  zero rather than unreliable, and `17_HOOK_ENGINE` forbids re-adding it — but
  `05_DETECTION`'s FG row and `06_DATA_MODEL`'s `fg_source` enum both still named it. Both
  corrected, with the reasoning kept where a reader of either would need it.
- **`sessions.fg_mode` defaulted to `'none'`**, reinstating at the storage layer the exact
  affirmative negative the writer and the consumer both refuse — `none` is the one FG state
  `03_METRICS` allows to be aggregated as a negative. Now `'na'`, matching the four tri-state
  columns beside it. And `frame_blobs.rt_flags` named its third bit `rtPsoAlive`, a name
  `fl_shm.h` renamed away because the bit **latches**: creation is observed at
  `CreateStateObject`, destruction is COM `Release`, which is not in the hook inventory and
  must not be added, so "alive" claimed a present-tense fact the hook set cannot retract.
  `06_DATA_MODEL:133` records that the schema is free to edit exactly once, before P2 writes
  `0001_init.sql`; after that these are migrations.

