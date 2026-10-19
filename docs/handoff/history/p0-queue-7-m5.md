# HANDOFF history — The queue, item 7 (7a–7c), Separable, §M5

*Moved verbatim from `docs/HANDOFF.md` on 2026-10-19 (beta.24, owner decision D66); `docs/HANDOFF.md` §How this file is split lists every part. Nothing below was edited, so its "below" and "above" point inside the original file.*

### ~~7. **START HERE**~~ — the three things left once the FG blocker fell (2026-09-04, owner's list) — **CLOSED 2026-09-06; the live head is §P2 below (struck 2026-09-09 so this file holds one START HERE, not two)**

P0 exit criterion 1 reads **4 of 5** as of 2026-09-04: upscaler identity, render → output,
FG factor and RT state are measured against the titles' own menus. What follows is the
owner's list for the next session, in the order the dependencies run, each with what makes
it fail on unmodified `main` — because a criterion already true on `main` is decoration.

#### ~~7a. The quality preset — the last value criterion 1 lacks~~ — **CLOSED 2026-09-06 (owner): DLSS / DLSS-G done on every measured title**

*Closed on the Cronos FG-on capture (`20_OPEN_QUESTIONS` §H5 head, `spike-notes` §9): the trio or a counted
`none` on every NVIDIA title through the hooked bodies, DXGI's counter where the hook cannot count (DL:TB —
that title's integration, not SDK 2.8.0's), and the driver-reported identity where no hook can see it, with
its negative measured. The quality preset itself stays `0xFF` / N/A everywhere — the override fields are
the override's, the tags carry no size on D3D12 — and the owner closed the item on that. What follows is
the history.*


*Fails on `main` because:* every real title measured reads `upscalerQuality = 0xFF`.
Cyberpunk 2077 never chains `sl::DLSSOptions`, so 0xFF there is the title's truth; **Alan Wake
2 (SL 2.7.0) is the candidate that may chain it and is unmeasured** (Owner-only item 2).

- **First, the cheap measurement:** one Alan Wake 2 capture at a known preset, read
  `quality=` off the `render …  quality=…  upscaler=…` line. If it reads the preset, criterion 1
  is 5 of 5 on that title and this item is a spike-notes entry. If it reads 0xFF too, the
  Streamline options route yields nothing on either measured title, and the decision below is
  live. *(Runbook title 12 is Alan Wake 2 as of 2026-09-04; Epic may update the executable on
  launch, so launch first and grant after.)*
- **The input to that decision is on the report now** (2026-09-04): `render -> output` prints the
  two measured sizes, `03_METRICS`' ratio and the render-scale percentage — Cyberpunk reads
  `1485x835 -> 2560x1440 = 1.72x (58% render scale)` — plus a **SETTINGS MOVED** flag when the
  window held more than one tuple. It prints **no preset name**; that is the decision below,
  still the owner's. Every FSR title measured on 2026-09-04 also reads `0xFF` for the quality
  byte, and on that route it is the true value rather than a gap: the ffx-api dispatch carries
  no quality mode at all, so the derived label is the only route to a preset name for AMD too.
- **The decision, if 0xFF everywhere:** `renderW/H ÷ outputW/H` is *exact* (Cyberpunk: 1485×835
  at 2560×1440 = 0.58, which IS DLSS Balanced, and the menu agreed), and DLSS's preset ratios
  are documented constants (DLAA 1.0, Quality 0.667, Balanced 0.58, Performance 0.5, Ultra
  Performance 0.333). A label **derived** from the measured ratio — printed as *"render scale
  58% ≈ Balanced"*, never written into `upscalerQuality`, never aggregated as the vendor enum —
  is an inference over a measurement, and `05_DETECTION`'s rule against static hints setting
  runtime facts does not forbid it: the input is a runtime measurement. **Whether a derived
  label may appear beside a measured field is the owner's call**; do not build it before that
  call, and if built, the byte stays 0xFF and the report says *derived*.
- **Do not** reach for `slDLSSSetOptions` via `slGetFeatureFunction`: §2b refused it on five
  grounds that have not changed, and NGX-direct `NVSDK_NGX_Parameter_GetUI` is licence-blocked.
- ~~**Alan Wake 2 did not run on 2026-09-04** (the owner tried every workaround; the title is
  broken on this machine)~~ **Corrected 2026-09-27 (owner):** it did run late that evening — the run reports
  `alanwake2-224851` and `alanwake2-230707` hold two captures (DLSS + RR, ray tracing *Yes*, 92.52 FPS; then ray
  tracing off, Presented 114.52), neither with the quality preset, which is what this item needed. The earlier
  attempts that day failed, so the measurement moved to **Dying Light: The Beast at DLSS**, the
  first Streamline title measured whose batches carry `kFeatureDLSS` — identity `Dlss` on 4,575
  of 4,575 batches. It read `UpscalerParams = 0` on three captures, because DL:TB ships
  **Streamline 2.8.0, which deprecates `slSetTag` for `slSetTagForFrame`**, and only the
  deprecated export had a row. The `slSetTagForFrame` row exists as of this entry (#115) —
  **and the rerun on it still read `UpscalerParams = 0` with both rows live**, so the title
  tags with a zero extent or not on this route at all. The next build reads the size the
  tagged `sl::Resource` declares when the extent is zero (`TagSize`, every route); one more
  DL:TB DLSS capture on it is ~~owed~~ **ran 2026-09-05 00:53 (#116 build): `N/A` stands** —
  params 0 of 3523 with both rows live and the fallback in. The route yields nothing on this
  title; the honest answer is `N/A`, and 7a on DL:TB is closed on that. What would reopen it
  is a tag-route census (calls per export, zero-extent seen, Resource-size seen), which needs
  a layout bump and is not worth one for a preset byte. The derived-label decision above is
  unchanged and applies to the titles that do print the line.
- **Separately, and more important than the preset:** every DL:TB DLSS run (five, the last at
  00:53 on the #116 build) had DLSS Frame Generation ON per the owner, and every one read
  `presents = tokens`, `frame generation: none`. That is `20_OPEN_QUESTIONS` §H5 case 3's
  candidate title. **Owner decision 2026-09-05: this is its own session and its own PR.** Read
  §H5 first; the discriminator (the owner's counter beside our `FPS:` line) is pre-committed
  there and is the first thing that session asks for. Nothing in FG counting changes before
  that reading. **The reading came the same day and the first PR landed on it** — the owner:
  the title runs at DLSS ×4, the frames we record are the native ones (§H5, verbatim). The
  capture host now prints each census-named module's file version and withholds `none` on
  that shape (`03_METRICS` §Frame Generation; the key is `sl.dlss_g.dll`, not
  `nvngx_dlssg.dll`). **What is next is the owner's session pre-committed in §H5** — DL:TB with
  DLSS FG off and ×4, Cyberpunk MFG ×4 as the control, PresentMon elevated beside each, the
  counter's NUMBER written down — and the row it lands on decides whether the gate stays, a
  second present sink is costed (§H5 lists the candidates; the route is the owner's), or the
  gate is withdrawn. Nothing in the hook changes before that row. **The counter half of Leg 1
  is read (2026-09-05): ~300 against our 76.7, reading (a) with its number; the PresentMon half
  is still owed.** And the owner's eleven-capture run the same day (`spike-notes` §9) put the
  remaining NVIDIA gap in one sentence: the COUNT is right on every title and the IDENTITY is
  what this build cannot say — DLSS on the NGX-direct UE titles, DLSS-G everywhere. **The identity half landed 2026-09-05 from the Streamline docs** (DLSS-G guide §5.0: the HUD-less / UI tags a
  frame-generating title MUST send, through exports already hooked): `frame generation: DlssG` is expected on
  Cyberpunk at MFG and on the UE titles at ×4 on the next run, with the new `Streamline tag census:` line saying
  which route carried the tags — and DL:TB's census line says whether that title feeds DLSS-G through a route
  this build sees. `slDLSSGGetState` was refused (`03_METRICS` §FG: it resets the plugin's own counter).
  ~~**START HERE next:** `fl-probe-nvapi --ngx-state <pid>` against a running Lies of P, Hell Is Us, DL:TB
  and Cyberpunk (no capture, no injection) — the super-resolution identity on NGX-direct titles is still the
  gap the tags cannot close.~~ **RAN 2026-09-06 on Hell Is Us, Expedition 33 and Lies of P (§H5, `spike-notes`
  §9): `CREATED | EVALUATE` on the SR word without an override on both no-override titles — the identity
  exists in the driver's bookkeeping; the FG word does not see DLSS-G at ×4; `frameGenerationCount` is the
  override target, `scalingRatio` is 0 even under an override.** ~~**START HERE next: the owner decides the
  driver-reported rung (§H5 says exactly what it may claim: `Dlss (driver-reported …)` on a session where
  every hook says N/A, nothing about quality, ratio or FG), and the negative is owed first — the probe on
  Lies of P with DLSS switched OFF, expected `CREATED` clear.**~~ **Decided and BUILT 2026-09-06: the capture
  host probes the driver beside every module snapshot and the `upscaler:` line has the driver-reported rung
  (`03_METRICS` §Upscaling). START HERE next: the owner's run — §H5 rows N1–N4, and N3 (Lies of P with DLSS
  OFF) is the one that keeps or withdraws the rung — LANDED 2026-09-06 midday: N3 read `CREATED` clear with
  upscaling off, N1 prints `Dlss (driver-reported …)` on four NGX-direct titles, N2 prints agreement on two,
  and the FG word turned out to follow DLSS-G too (identity, never the multiplier). START HERE next: Cronos
  (a second Streamline 2.8.0 title, UE) with frame generation ON — does the 2.8.0 pacer bypass the patched
  bodies there too (`DXGI present counter` ≈ 3) or is it Techland's (0)? — ANSWERED 12:17: Cronos FG on reads
  `86.8 → 172.72 (×1.99 FG)` with `unseen=0`; the bypass is Techland's. Item closed; the next item is the
  owner's pick from 7b / 7c below.** **The tag route LANDED on the owner's evening run (`spike-notes` §9): `DlssG` on
  six of six Streamline titles, and DL:TB's census shows its scaling-input tag arrives with no size, which
  closes 7a's last question.** If the driver's per-process NGX feedback is
  populated without an NVIDIA-app override, the identity question has a producer that is not a
  hook and the owner decides whether `03_METRICS` gains a *driver-reported* rung (§H5 says what
  that rung may and may not claim). If it is not populated, the answer stays `N/A` and the
  session goes back to the PresentMon leg. **The in-process half of that leg LANDED (2026-09-05,
  night): DL:TB at DLSS FG ×4 read 2.90 and 2.95 unseen presents per hooked one on the same chain —
  §H5 row P1-DXGI — while Streamline 2.7.3 (Hell Is Us) and 2.10.3 (the Onimusha demo) read 0.00, so
  2.8.0's pacer is the one that bypasses the patched bodies. The record now carries `dxgiUnseen` and
  the window counts `Displayed = hooked + unseen`, labelled DXGI-COUNTED (`03_METRICS`); DL:TB's next
  capture is expected to print the trio at ≈ ×3.9 with that label and no withheld line — LANDED
  2026-09-06: `70.52 → 282.08 (×4 FG)`, DXGI-COUNTED 12,387, `DlssG`. Leg 0 the same morning (FG off,
  twice) showed the plugin is a startup-time load and the report withheld as pre-committed, so the
  gate is now narrowed to sessions where the DXGI counter was not read; a read counter with zero
  unseen prints `none` with DXGI's agreement.** The
  "sl.common must be loaded on 2.8" lead is closed by the 2.10.3 title, which injects and counts like
  2.7 (§H5).

#### 7b. Titles whose upscaler or frame generation is compiled into the executable

*Fails on `main` because:* the census has no module to see, the inventory has no export to
resolve, and the report prints *"cannot include in-process generated frames"* — which on a
statically linked FSR3-FG title is wrong in the dangerous direction. The sentence names the
hole; it does not close it.

- **Measure before costing.** No installed title has this shape: every FSR title on this
  machine ships the `ffx_*` / `amd_fidelityfx_*` DLLs (`vendor-exports.json`, 34 modules), and
  the six-title run found none. The catalogue that links FSR2 statically is mostly
  2022–2023 UE4/Unity titles that took the source SDK. **Find one first.** A rationale written
  about a title that does not exist is the kind of deferral §S24 counts against nobody.
- **What is and is not possible, so the costing is honest.** Nothing by name: no module, no
  export. What survives static linking is behaviour on interfaces we already hook or could:
  FSR3's frame-generation *proxy swapchain* is created through `IDXGIFactory*::CreateSwapChain*`
  on the shared `dxgi.dll` class vtable — visible **only at creation**, i.e. only in launch mode
  (§S1, deferred) or on a title that recreates its swapchain on a settings change; and per-frame
  work on the D3D12 queue (`ExecuteCommandLists` on the FG's async queue) is a cadence signal
  with no vendor name on it, which is rung 3 at best and needs an application-frame count this
  title cannot give. **The honest ceiling for a statically linked title in attach mode is
  Presented FPS with the census's qualifier** — which is what ships today, with the hole named.
- **The only cheap improvement is wording**, and it is already in: the qualifier says
  *statically linked FSR3-FG … outside what this can see*. **Built 2026-09-06, two steps past that
  (owner's choice over "return native", which rule 6 forbids):** the executable FILE is scanned on
  disk after the session for the vendor SDK strings, and a frame-generation-capable one flips the
  clear-census sentence from *cannot* to *MAY*; and the rung-3 line prints its exclusions (the
  driver's FG word, the tags, the ffx census, the module census, the file's markers). The ceiling
  for a title that is both compiled-in and token-less stays Presented FPS + that qualifier. **LANDED
  2026-09-06 14:02: Rune Factory at FSR + FSR FG prints the pre-committed line verbatim (§H11); 7b is at its
  ceiling and closed.** Hell Is Us at XeSS + XeSS-FG the same run: no token, so Displayed measured (186.47,
  0 unseen) and Native unknown — the N/A now carries its witnesses, and that is the XeFG ceiling too. If a title is found, the first PR is
  a `spike-notes` row and a `20_OPEN_QUESTIONS` §Scope entry with the measured shape, not code.
- ~~**Measured 2026-09-04: no such title is installed.**~~ **Found the same evening, by running
  rather than by scanning: Rune Factory: Guardians of Azuma.** The scan was right that every FSR
  title *ships* its DLLs — Rune Factory ships the 1.1.x monolith — and wrong to read shipping as
  loading: with FSR frame generation on, the census has **no AMD module at all**, so the plugin's
  FSR 3 is compiled in. And the ceiling is better than this entry feared: Streamline's frame
  token is requested on that UE5 title regardless, so the count read `59.99 → 119.95 (×2 FG)`,
  *technology not identified* — the trio, with the vendor missing. Identity by name stays out of
  reach; §Scope decisions carries the corrected row. A disk scan cannot find this shape; a
  capture with the census line can. **Black Myth: Wukong is the second** — its FSR upscaling
  dispatches to neither the loaded monolith nor the loader, and it offers no FSR frame generation.

#### ~~7c. AMD and Intel identity, measured rather than inferred~~ — **AMD LANDED 2026-09-04 and re-confirmed complete 2026-09-06 on Lies of P; Intel closed by licence (re-checked 2026-09-06, unchanged)**

*2026-09-06, 12:46: Lies of P at FSR 3.1 + FSR FG prints `Fsr3` / `FsrFg` / `59.77 → 119.55 (×2 FG)` /
`2560x1440 -> 2560x1440` with every count agreeing (`spike-notes` §9) — acceptance step 5 in full, beside
the DXGI counter (0 unseen) and the NVIDIA driver's word (nothing created). The owner called the AMD half
done. Intel: the licence was re-fetched the same day — Intel Simplified Software License, October 2022,
binary-only, "nor any modification", termination; the header banner still forbids copying — so the
reversal condition has not fired and the census stays the answer. What is new: Intel's own README says
XeSS-FG runs on non-Intel GPUs with SM 6.4, so an XeFG session IS capturable on this box (DL:TB, Hell Is
Us, Cronos, Expedition 33 ship `libxess_fg.dll`), and the proposal on the table is identity BY ELIMINATION,
labelled — see `20_OPEN_QUESTIONS` §H11's 2026-09-06 note.*


**Do not start here.** The three ffx-api leaves are hooked, the FidelityFX headers are vendored
at tag **`v2.3.0`** (not the `v1.1.4` step 1 below names — its own reversal condition fired:
installed titles ship SDK 2.x effect DLLs whose `PrepareV2` descriptor 1.1.4 does not declare),
the K = 1 / K = 4 pair runs on both vendor shapes, and the acceptance table is pre-committed in
`20_OPEN_QUESTIONS` §H11 (rows A1–A3, R1–R5). Status is in `CHANGELOG.md` and §H11. **Three
corrections to the entry below, each of which changed what got built:**

- **Scope to the modules a GAME CALLS — the three leaves AND the SDK 2.x loader — not "the
  facade", and this bullet reversed itself within the day.** Step 2 says *"module-scope every row
  to the facade … the plugin modules are called by the facade"*. The morning's build hooked the
  three leaves only and refused the loader by `static_assert`, arguing that five identical export
  tables leave the loader no route to a leaf but the leaf's own export, so hooking both would
  double-count. The UE5 half held — Hell Is Us and Expedition 33 ship the two effect DLLs with
  **no loader** and their dispatches arrive at the leaves, 1×. **The loader half did not:** Dying
  Light: The Beast at FSR + FSR FG, KCD2 at FSR and Wukong at FSR produced **zero** dispatches at
  any leaf export (§H11's run table, row R6). The signed loader reaches its providers through an
  object, not an export. So the row set is *whatever module the game calls*: four rows, the
  loader's stub forwards through the leaves' direct entry to model the measurement, and the
  K = 1 control with all four hooked is what would show a loader that re-entered an export.
- **`ffxCreateContext` is not a row.** Identity comes off the UPSCALE dispatch's type — per
  frame, so it says *running* rather than *created* — and a create-context row's honest family
  is `UPSCALER_IDENTITY` alone, which collides with `slEvaluateFeature`'s under the installer's
  equality binding.
- **"Extend Pass C's forbidden-import list" was already done** — `^(sl\.|_?nvngx|libxess|ffx_|amd_fidelityfx)`
  since 2026-08-14, self-tested. Pass C gained the **export** table instead, because `ffx_api.h`
  declares its entry points `__declspec(dllexport)` unconditionally.

**Run 2026-09-04 evening, thirteen captures, §H11's table:** A1–A3 landed as pre-committed
(`Fsr3` / `FsrFg` / ×2.00 / 1.00 on Lies of P; `none` by count with FG off; `FSR (3.1 or 4 …)` /
`FsrFg` / ×2 on Hell Is Us), Expedition 33 showed the mixed-vendor shape (FSR upscale + DLSS FG ×3
→ *active, technology not identified* at ×2.97), Cyberpunk at FSR 3 split across two AMD
generations, and **Rune Factory turned out to be 7b's statically linked title** with the Streamline
token count still reading ×2. **Row R6 ran the same night on #111:** Dying Light: The Beast at
FSR + FSR FG printed `FSR (3.1 or 4 …)` / `FsrFg` / `70.49 → 140.9 (×2)` with the counts agreeing
at 1.01, KCD2 at FSR printed the identity and `none` by count at 1.00 — the loader row carries
both, once. **Wukong at FSR read silent at the monolith AND the loader with the monolith loaded**:
FSR upscaling compiled into its UE plugin (Rune Factory's shape) with the monolith loaded and
unused — ~~owed: Wukong with FSR frame generation ON~~ **the title has no FSR frame-generation
option (owner, same night)**, so Wukong is the second static title and nothing is owed on it. ~~**The FSR 3.0 host route
(`ffx_fsr3_x64.dll`, step 3 below) is deferred to its own PR** — ten host headers from `v1.1.4`;
Cyberpunk at FSR 3 is its oracle and already prints `FsrFg` from the monolith beside it.~~
**BUILT 2026-09-05, and the tag was not `v1.1.4`.** Cyberpunk's module exports exactly
`fsr3-v3.0.4`'s twelve `ffxFsr3*` names; 1.1.4 appends fields after `renderSize` and declares
three exports the module lacks. One row — `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale`,
the same family and publish point as the ffx-api rows, keyed by module in the installer —
reads `renderSize` and nothing else; `ffxFsr3DispatchFrameGeneration` is deliberately not a
row (the monolith generates on the one title; §H11 pre-commits the reversal). **What is owed
is the owner's acceptance run**, rows B1–B4 in §H11: Cyberpunk at FSR 3 + FG must print
`upscaler: Fsr3` with one extent agreeing between the host and the monolith's PREPARE, and
`frames/upscale-drained = 1.00`; B2 (facade silent, `upscale-drained = 0`) is the pre-committed
condition for adding the `ffx_fsr3upscaler_x64.dll` row instead — never both.

<details>
<summary>The entry as it stood, kept for the reasoning it carries</summary>

#### 7c. AMD and Intel identity, measured rather than inferred — §H11's deferral has a next step

*Fails on `main` because:* Lies of P with FSR 3.1 frame generation demonstrably on reads
`upscaler: N/A … frame generation: N/A` plus a census WARNING, and Hell Is Us at FSR reads the
same as its off run. The three AMD 3.1 modules export the same five generic names
(`ffxCreateContext`, `ffxDispatch`, `ffxQuery`, `ffxConfigure`, `ffxDestroyContext`); XeSS
exports `xessD3D12Init` / `xessD3D12Execute` and XeFG `xefgSwapChainD3D12InitFromSwapChain`
(`vendor-exports.json`).

1. **The `18_GPU_VENDOR_APIS` §Checklist has been run on both SDKs (2026-09-04) — read the
   result before assuming the previous sentence, which said both were MIT and was wrong about
   Intel.** *AMD FidelityFX*: clear. Tag **`v1.1.4`** is MIT at the root and inline in every
   header of interest; vendor **types-only, with a consumer in the same commit**
   (`18_GPU_VENDOR_APIS` records why an unconsumed vendoring is worse than none). The files:
   `ffx-api/include/ffx_api/{ffx_api.h, ffx_api_types.h, ffx_api_loader.h, ffx_upscale.h,
   ffx_framegeneration.h, dx12/ffx_api_dx12.h, vk/ffx_api_vk.h}` for the 3.1 facade the
   installed titles call, and `sdk/include/FidelityFX/host/{ffx_fsr3.h, ffx_fsr3upscaler.h,
   ffx_frameinterpolation.h, ffx_opticalflow.h, ffx_types.h, ffx_error.h, ffx_interface.h,
   ffx_util.h, ffx_assert.h}` for the FSR 3.0 host API *(2026-09-05: vendored from **`fsr3-v3.0.4`**, not `v1.1.4` — the tag whose declarations match the module Cyberpunk ships; see the banner above)*. Do not take `main`: it is SDK 2.x under
   a binary-only `docs/license.md` that excepts these headers as MIT only by an 845-line list.
   Extend `hookinventory-check` Pass C's forbidden-import list with `amd_fidelityfx_*.dll` /
   `ffx_*.dll` in the same PR — the Streamline lesson, one vendor over. *Intel XeSS SDK*:
   **rejected.** Intel Simplified Software License (binary-only grant, no reverse engineering,
   termination), and the headers themselves forbid copying without Intel's permission, so
   step 3 binds — no vendoring and **no re-declaration** of `xessD3D12Execute` or
   `xefgSwapChain*`. **Intel stays at the census — decided 2026-09-04, delegated by the owner
   and taken.** The one route that looked left in policy, a signature-free call counter on a
   named export, was weighed and refused: the thunk declares nothing, but installing it still
   inline-patches Intel's code inside the game process, and the same licence forbids *"any
   modification"*. FrameLedger has never patched a restricted-licence vendor binary — the NGX
   answer was the census, not a cleverer hook — and Intel gets the same answer. It would also
   have been the only hook entry outside `FL_HOOK_GUARD` and a second language in the
   Overlay's build, for `Xess` identity alone with no render extents, on a vendor with no
   test hardware here. `20_OPEN_QUESTIONS` §Scope decisions carries the row and the reversal
   condition (Intel relicensing the headers). **So 7c is AMD identity, full stop.**
2. **AMD 3.1 (`amd_fidelityfx_dx12.dll`, the facade the game calls):** identity is in
   `ffxCreateContextDescHeader.type` — `FFX_API_CREATE_CONTEXT_DESC_TYPE_UPSCALE` vs
   `…_FRAMEGENERATION` — read from the argument of a hooked `ffxCreateContext`, which is the
   same class of read as `slEvaluateFeature`'s `inputs` (rule 4). `ffxDispatch` with a
   frame-generation dispatch desc fires **once per application frame** and is this vendor's
   `slGetNewFrameToken`; an upscale dispatch desc carries `renderSize` / `upscaleSize`, which
   is `renderW/H` exact. **Module-scope every row to the facade**: three modules export the
   same five names, and the plugin modules are called by the facade, not by the game — an
   unscoped hook would count each dispatch twice, straight into `fg_factor`.
3. **AMD 3.0 (`ffx_fsr3_x64.dll`, Cyberpunk's copy):** named exports, no struct decode needed —
   `ffxFsr3ContextDispatchUpscale` for identity and extent, ~~`ffxFsr3DispatchFrameGeneration`
   once per application frame~~ *(built 2026-09-05 without the FG row: on the one title the
   monolith generates and the count is Streamline's; the host's FG export fires per generated
   batch and is not a count producer. §H11 carries the reversal condition.)*
4. **Intel — closed at the census (step 1).** No hook on any `libxess*.dll` export. The
   report line for an Intel title is what the writer already produces: upscaler
   `NOT_REPORTED`, and if `libxess_fg.dll` is in the census, Presented FPS with the *"MAY
   include generated frames"* qualifier rule 6's amendment requires. `renderW/H` stays
   `NOT_REPORTED` for XeSS *by licence, not by ignorance* — the report's wording should say
   so, which is a string change, not a hook. Note that census presence is not identity:
   Cyberpunk loads `libxess.dll` at DLSS, so the census must never be read as *"XeSS on"*.
5. **Acceptance, against the titles' own menus:** Lies of P at FSR + FG prints
   `upscaler: Fsr3`, `frame generation: FsrFg`, and the trio at ≈ ×2 with the same
   `presents / dispatches` arithmetic; Hell Is Us at FSR prints identity where today it prints
   the off run's line; Cyberpunk at XeSS prints upscaler `NOT_REPORTED` and the census
   sentence with `libxess.dll` named, and nothing that reads as identity. **A wrong preset name degrades
   silently** — the histogram and `tokens/batch`-style second count are what make a mistake
   visible, so build the per-vendor second count before trusting the first.

**Traps carried from the Streamline work that apply verbatim:** the vendor's zero is not our
zero (resolve at the writer); a vendor keeps a NAME and changes a SIGNATURE (`SpeaksExpectedAbi`
needs an AMD and an Intel arm); the K = 1 / K = 4 harness pair must exist before the first
real-title run, or a writer that counts presents instead of dispatches is green everywhere.

</details>

### Separable

`fl-probe-signer` → §S19(b) → drop `-SkipIntegration`. **Not** a prerequisite of the
feature hooks — §S29(a) said it was and was wrong; see below. What it buys is the
*managed* drain being in the merge gate.

> **The price went up on 2026-08-06 and is worth stating before someone budgets it.**
> `Category=Integration` was three cases; it is **nine** — the drain loop, the pause
> round trip, the drop path against the real writer, and the capture host's four
> end-to-end cases including the only assertion anywhere that `guardTicks` advances
> from a non-test binary. All nine run on a dev box and none in the merge gate.
>
> **And §S19(b) alone does not buy them back.** `build.ps1 -SkipIntegration` applies
> `--filter 'Category!=Integration'`, which excludes the class *before* the guard is
> ever asked — two independent mechanisms produce one absence, and `ci.yml` has to
> drop the switch as well. Anyone costing this as "fix the signer half" is costing
> half of it.

**Picked up 2026-09-06 (the owner's next item after item 7).** The sequence, with what
each step needs, so nothing here is started out of order:

1. **The CI leg, made readable** (2026-09-06, this PR): the probe's ctest printed nothing,
   so `ci.yml` runs `fl-probe-signer` as its own step after the gate and the answers land in
   the log. Rows G3/G4/G5 are read off the next run on `main`.
2. ~~**The adapters-disabled leg — owner-only, a network setting.** Same three subjects,
   `ctest fl_signer_probe` (or the exe) with every network adapter disabled, compare the
   verdicts with `spike-notes` §1. Unchanged → G1; any change → G2 and the route retires.~~
   **RUN 2026-09-06 by the owner: every verdict unchanged — row G1 (§S19(b)).**
3. ~~**The allow-widening bound — owner-only, a decision no PR may take (§S19(b)).**~~ **DECIDED
   2026-09-06: option 1, the compiled-in allowlist `trustedSigners` may only intersect.**
4. ~~**Then the PR:** …~~ **BUILT 2026-09-06 (§S19(b) ✅ BUILT):** `ModuleSignerOrganisation` in
   `fl_guard_sources.cpp`, `IsCompiledTrustedSigner` as the bound, the trusted module keeps the
   scan looking, every failure refuses, the catalog half deferred with its rationale, both
   directions proved against the real seams through `hook-harness --load`, and `-SkipIntegration`
   dropped from `ci.yml`. **The Separable item is closed**: the nine integration cases are in the
   merge gate, and CI runs the command a developer runs.

### ~~§M5 — **START HERE**~~ — MEASURED 2026-09-03, row R1, and the head moves

**Do not start here.** `LhmTelemetrySource` exists, `probe-lhm` ran unelevated and elevated,
and both landed on row R1 of the table pre-committed in §M5: eight GPU fields with no
elevation and no PawnIO. Status is in §M5, `spike-notes` §10 and `CHANGELOG.md`. What
belongs here is the **sequencing consequence**: the three user-facing sentences stand, so
nothing in `README`, `legal/EULA.md` or the consent disclosure is waiting on telemetry any
more, and the `LibreHardwareMonitorLib` loose end below is closed by having a consumer.

~~**The live head is now the FPS-display rule, decided by the owner on 2026-09-03 and
recorded in no other file until its PR lands:**~~ **LANDED the same day** — `03_METRICS`
§Presented FPS and §Rung 0's qualifier, `fl_shm.h` §FlRuntimeCensus, `17_HOOK_ENGINE`
§Watchdog observations, CLAUDE.md rule 6's amendment. Status is in `CHANGELOG.md`. ~~**What is
still open from it is the owner's:** the two real-title captures that would show the two
qualifiers on real games (Cyberpunk 2077 → WARNING naming `nvngx_dlssg.dll`; any D3D11
title with no upscaler → "cannot include"), one launch each. The fixtures prove both
directions natively; the titles would prove the names.~~ **RUN THE SAME DAY, six titles,
`spike-notes` §9.** Both shapes seen; the names are the loader's; one refusal
(`TargetAmbiguous`, NW.js) became a scope row; Hell Is Us became the third title where DLSS is
on and `slEvaluateFeature` sees nothing. The decision text is kept below
because the reasoning lives in no other file. When frame generation is *not measured*
(`FL_MEASURED_FG` clear — every non-Streamline title today, and every Streamline title
on the measured route), the headline is **Presented FPS** (`presents / D`, the name for the
one number that stands alone; "Native" and "Displayed" appear only together), with a
mandatory qualifier chosen by a **runtime-module census** run on the Overlay's watchdog
and published in `FlWriterState`. **The census never produces `FL_FG_NONE` or
`FL_UPSCALER_NONE`** — statically linked FSR has no module to see, and a census-`none` on
such a title would print the inflated number rule 6 forbids. It refines the *reason* for
N/A and warns loudly when a frame-generation runtime is loaded and nothing was observed.
The two cases that motivated it print identically today: a 2D title with no upscaler, and
a Streamline title with upscaling switched off in its settings. **`SL loaded + zero calls ≠
off`** (Black Myth: Wukong) is why the second may not become `none` either.

<details>
<summary>The entry as it stood while it was the live head, kept for the reasoning</summary>

**Do GPU sensors work unelevated, without PawnIO?** It used to decide how ADR-9 read to
users. It now decides whether a sentence **already in `README`, `legal/EULA.md` and the
operator consent disclosure** is true.

The two-rung ladder says a Tier-2 session records *"session duration and whatever hardware
telemetry this machine can provide"*. That hedge is there because **nobody has measured
this**. If LHM needs elevation for GPU sensors, the DEFAULT unelevated Agent's Tier-2
session is **duration only** — and the honest wording is narrower than what those three
documents now carry. The hedge is deliberate and it is also a placeholder: it buys
correctness at the cost of telling a user something useful.

`18_GPU_VENDOR_APIS` §P0 item (b) already specifies the test and the dev box can run it. It
needs `LhmTelemetrySource`, which does not exist — so the first slice of P0 item 8 is now
the cheapest way to make three user-facing documents say something definite.

**This heading sits under §Separable on purpose, and it is still the live one.** §M5 is not a
prerequisite of any feature hook — nothing in the queue above waits on it. It is here because
it is the smallest piece of work that makes an already-shipped claim definite, which is a
better place to restart than a decision nobody has taken.

**Two other things are open and neither is this.** Item 6 — written rationales for every
❓/⏳/◐ still in §S24, and unifying the glyphs — is P0 **exit criterion 2** itself and is pure
editorial; take it if you want P0 closer to done rather than more correct. And `fl-probe-signer`
→ §S19(b) above buys the managed drain a place in the merge gate. Both are real; neither has a
claim in a legal document depending on it.

**Why this and not the frame-generation route.** Item 3's producer decision is unblocked and
unmade, and whoever takes it chooses **with no oracle behind the choice** (§S31 row P2, and
`14_TESTING`'s cross-validation gate struck the same day). That is a decision to take
deliberately, not a task to pick up. §M5 is a measurement with a specified method, a machine
that can run it, and a claim already shipped that depends on the answer.

Rest of P0 item 8 telemetry: `BaselineTelemetrySource` (L1), `NvapiTelemetrySource` (L3 — the
material is vendored and `fl_nvapi_probe` proves it works). ~~and the PresentMon binary for
`spike-notes` §11~~ — that section is filled and PresentMon is gone.

~~Also loose: `LibreHardwareMonitorLib` is referenced by `FrameLedger.Infrastructure`
and used by **zero lines**, so it ships into the Agent's output — an MPL-2.0 §3.1
redistribution obligation incurred for no capability. Either L2 gets written or the
reference comes out.~~ — **L2 was written, 2026-09-03.** The obligation now buys eight fields.

</details>

---

