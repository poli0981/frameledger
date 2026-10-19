*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 9 · Frame generation

- **Rung 1 (API / `fgEvaluations`) vs Tier-2 ETW `FrameType`: NOT RUN, and rung 1 does not
  produce a number to compare.** Measured 2026-08-15 on Cyberpunk 2077 (SL 2.7.1):
  `slEvaluateFeature(kFeatureDLSS_G)` is **never called** — 0 across ~14,000 Streamline
  batches at four frame-generation settings — so `fgEvaluations` is 0 on every record and
  there is nothing for ETW to be compared against. The comparison `15_ROADMAP` item 7 asks
  for is blocked on a PRODUCER, not on tooling.
- **What DID move with the setting is `presents / batch`, and it is a PROXY.** Five 40 s
  captures, one title, one GPU, D3D12, Ray Reconstruction on: **off → 1.000, ×2 → 2.000,
  ×4 → 4.000** (×4 three times independently, one identified swapchain and 0 gaps/0 dropped
  in every run). Application frame rate falls 85.3 → 70.9 → 63.1 as the multiplier rises,
  which is the direction frame-generation overhead predicts, and 70.9 × 2.06 = 146
  reproduces the ×2 displayed figure. So the extra presents are real, they are visible to
  our present hook, and their count tracks the configured multiplier.
  **A batch is "a present that drained a Streamline evaluation", NOT "an application
  frame"** — the two coincide here only because Ray Reconstruction happens to be evaluated
  once per application frame on this title, and no independent oracle has confirmed that.
- **THE APPLICATION-FRAME PREMISE IS STILL NOT MEASURED — a draft of this bullet said it was,
  and was corrected before it landed, 2026-08-16.** Steam's overlay read `DLSS 162 | FPS 81`
  during a ×2 capture whose own figures imply 161.7 and 80.8. That is **one** agreement, not
  two: `presents/batch` was 2.0000 exactly and 162/81 is 2.0000 exactly, so both residuals are
  forced to −0.182%. The surviving comparison is circular (the span was derived from
  `Displayed FPS`, so `presents/span` restates it), and the rival reading — that the overlay's
  `FPS` field is *displayed ÷ 2* rather than an application-frame count — predicts the identical
  number at ×2. Steam's overlay is also **not** an independent instrument: `17_HOOK_ENGINE`
  §Coexistence records that it hooks D3D presentation in-process too.
  **The discriminating run is ×4** (application frames ⇒ ≈65, fixed halving ⇒ ≈130) plus an
  FG-off leg where a genuine counter converges with displayed and a halving does not, comparing
  RATIOS — our `presents/batch` against the overlay's `DLSS/FPS` — so neither side needs a span.
  §S30 carries the full correction.

  > **GENERALISED FROM ONE TITLE TO FOUR — 2026-08-20, §8.** The bullet above rests on
  > Cyberpunk. Four titles, five captures: **`kFeatureDLSS_G` is zero on every one**, and two of
  > the four never call `slEvaluateFeature` *at all* — Black Myth: Wukong with both
  > `sl.interposer.dll` and `sl.dlss_g.dll` loaded and DLSS-G demonstrably running, and Rune
  > Factory: Guardians of Azuma. So this is not "DLSS-G avoids that export"; on half the titles
  > measured, **nothing** goes through it. `slEvaluateFeature` is a route some titles use for
  > some features, not the Streamline entry point.
  >
  > **`presents / RT-active presents` is a second proxy and covers the gap.** Wukong has no
  > batches at all, so `presents/batch` is unreadable — and `5,764 / 1,441 = 4.0000` exactly,
  > against a title configured for ×4. Same unverified premise, disjoint coverage; on the one
  > run where both proxies were readable they agreed. Neither is a producer, and §S31's
  > measurement is still what decides whether either may be published.

  **`fl-baseline-probe` was run on 2026-08-16 and is RETIRED as an FG-engagement oracle by
  its own pre-committed falsifier**: against the running title with FG on at ×2 it reports
  all seven capabilities `loaded`, including `dlss_g` **and** `xefg` — two mutually exclusive
  frame-generation implementations — so `loaded` means "mapped", not "engaged", and one run
  settled it without needing the FG-off leg. Its real job, the static/loaded baseline for
  item 4, is unaffected. **The game's own frame counter is the only remaining candidate**
  and is still unrun.
- Can PresentMon 2.x `FrameType` see driver-level FG / AFMF (§M1):
- AFMF on this machine: **untested — RTX 5080, AMD driver-side feature**

### ✅ Presented FPS + the runtime census, on six real titles — 2026-09-03

The two qualifiers `03_METRICS` §Rung 0's qualifier specifies were both seen on real games,
and the report line that prints the census by name (`runtime census:`) is what made each row
below readable. Operator: the owner. Instrument: `FrameLedger.CaptureHost capture --seconds 60`,
one launch per capture, consent granted after launch. Exit codes and full reports in
`E:\frameledger-runs\2026-09-03\` (not in the repository).

| Title | API | Hooks installed | Census: FG group | Census: upscaler group | Presented FPS | Qualifier printed |
|---|---|---|---|---|---|---|
| **DARK SOULS III** (2016, no anti-cheat on this install) | D3D11 | Present | — | — | 59.8 | *cannot include* — the clean (a) shape |
| **KOISHIKARUBEKI** (Unity, 2D VN, x64) | D3D11 | Present | — | — | 59.6 | *cannot include* — the clean (a) shape |
| **Cyberpunk 2077** | D3D12 | Present, SL identity+params, FG evals, RT ×2 | `nvngx_dlssg`, `libxess_fg`, `ffx_frameinterpolation_x64`, `ffx_fsr3_x64`, `amd_fidelityfx_dx12` | `sl.interposer`, `nvngx`, `nvngx_dlss`, `nvngx_dlssd`, `libxess`, `ffx_fsr3upscaler` | 142.0 | **WARNING**, names listed; `presents/batch = 2` against the title's own ×2 |
| **Lies of P** ×3 — **FSR + AMD frame generation, FSR + AMD frame generation, DLSS** (operator-confirmed; no off run) | D3D12 | Present, RT ×2 — **no Streamline** | `amd_fidelityfx_dx12` in **every** run, the DLSS one included | `nvngx`, `nvngx_dlss` in every run | **231.1 / 237.5** (FG on) / **59.9** (DLSS, no FG) | **WARNING** naming `amd_fidelityfx_dx12.dll` — the facade regroup of the same morning is what put it there |
| **Hell Is Us** ×3 (off / FSR / DLSS + FG ×4 — the third labelled by the operator's own note) | D3D12 | Present, SL identity+params, FG evals, RT ×2 | `nvngx_dlssg`, `libxess_fg`, `amd_fidelityfx_framegeneration_dx12` in **every** run | `sl.interposer`, `nvngx`, `nvngx_dlss`, `nvngx_dlssd`, `libxess`, `amd_fidelityfx_upscaler_dx12` in every run | 60.0 / 60.0 / **300.4** | **WARNING**, names listed, all three runs |
| **Flower in Us** (NW.js / RPG Maker, not Ren'Py) | — | — | — | — | — | **`TargetAmbiguous`, exit 6** — no capture |

**What it settles.**

- **Both shapes are reachable on real titles**, and the census names are the loader's, not
  the directory's: Hell Is Us ships `sl.dlss_g.dll` on disk and the loader reported
  `nvngx_dlssg.dll`; Cyberpunk's row is five FG runtimes for one title.
- **The census is a STARTUP property on UE5, and it cannot see settings.** Hell Is Us
  reported the identical word `0x2794D` at off, FSR and DLSS + FG ×4; Lies of P the identical
  `0x41801` across its three. The plugins load at init whatever the menu says. So (b) *"module
  loaded, upscaling off"* and (c) *"module loaded, driven through an unhooked path"* print the
  same WARNING — exactly what `03_METRICS` says the census may and may not do, now measured.
- **The facade regroup was load-bearing.** Lies of P ships `amd_fidelityfx_dx12.dll` alone
  and the operator reports Steam's counter counting generated frames on its FSR run. With the
  facade in the upscaler group the report would have printed *"cannot include in-process
  generated frames"* on a title generating them.
- **A THIRD title where nothing goes through `slEvaluateFeature`.** Hell Is Us with DLSS on
  and frame generation at ×4: `records carrying Upscaler = 17,920 / 18,033`, **0 batches
  drained, `kFeatureDLSS = 0`**, `upscaler: N/A`. UE5's DLSS plugin drives super-resolution
  through NGX directly and its Streamline plugin drives DLSS-G somewhere this hook does not
  see. `spike-notes` §8's "two of the four" is now **three of five**. Presented 300 FPS at ×4
  reads as ~75 native, and the WARNING is the only honest line the report has for it.
- **The (a) shape is exact on a title with no vendor module at all**: DS3 and the Unity VN
  publish `runtimeCensus == 0x1`, precisely the `FL_CENSUS_RAN` equality `fl_guard` asserts on
  the harness.

**What it opens.**

- **Multi-process titles refused as `TargetAmbiguous`** — NW.js / Electron / Chromium-based
  games (RPG Maker MV/MZ, many VNs) run several `Game.exe` processes with one image path —
  browser, renderer, GPU — and the presenting one is the GPU process, which has no window of
  its own. **Resolved the same day by Chromium's `--type=gpu-process` flag** read through a
  kernel query (`20_OPEN_QUESTIONS` §Scope). What the next *Flower in Us* run measures is
  whether the Overlay loads inside Chromium's GPU process at all: NW.js runs without the
  Chromium sandbox by default, so the expectation is yes, and the expectation is not a result.
- **Hell Is Us at FSR reported 60 FPS presented with the operator's Steam counter not
  counting**, identical to its off run. Whether FSR frame generation was engaged at all in
  that run is unknown; the census cannot say and the report does not claim to.
- **Second run, same evening, after #103 / #104 merged — the frame-token producer landed on
  §S31 row P4 and the NW.js shape had no GPU process.** Cyberpunk 2077 off / ×2 / ×4 (legs
  identified by their own `presents/batch` = 1.00 / 2.00 / 3.99): `presents/tokens` read
  **0.22 / 0.64 / 1.31** and `tokens/batch` **4.63 / 3.10 / 3.05**; Hell Is Us DLSS + FG ×4
  read **1.29** (17,788 presents, 13,783 tokens). The title asks for a token three to four-and-
  a-half times per application frame and gets a distinct object each time, so a count keyed on
  pointer change read the request rate. No factor was published on any leg — the ≥ 1.5 gate is
  what the table was written for. The detour now keys on the frame index; the run is owed
  again. *Flower in Us* through the merged resolver: still `TargetAmbiguous`, and the tree
  says why — browser, `crashpad-handler`, `utility` ×3, `renderer`, **no `--type=gpu-process`**;
  NW.js keeps the GPU in the browser process. Rule added for the untyped-browser shape.
- **Run 3, 2026-09-04 morning, on #105's monotone count — row P1, and §S31's question
  answered.** Cyberpunk 2077 off (RR on) / off (RR off) / ×3 / ×4: `presents/tokens` **1.00 /
  1.00 / 2.99 / 3.99**, `tokens/batch` **1.00** on every leg with batches; Hell Is Us DLSS +
  FG ×4: **4.00** (17,942 presents, 4,485 tokens, zero batches). The report printed the trio on
  the three FG legs — `61.37 → 183.49 (×2.99)`, `58.74 → 234.09 (×3.99)`, `76.54 → 306.17
  (×4)` — and refused the two off legs at 1.00 exactly as the pre-P1 gate was written to; that
  refusal is lifted in the PR that records this. **Two independent per-frame counts agreeing
  to the frame is the oracle four instruments could not be.** *Flower in Us* through #105's
  browser rule: **resolved, injected, captured** — `target: 6 processes share the image
  (browser=1, crashpad-handler=1, renderer=1, utility=3); pid 32652 is the untyped browser
  process`; 3,587 records over **three swapchains** (Chromium presents its UI and content
  separately; the dominant stream carried 3,435), `Presented FPS 59.76`, census `0x1`, the
  clean (a) shape. So the Overlay loads inside NW.js's browser process, which was the one
  thing the rule could not promise. Lies of P, FSR + FG: WARNING as before, no tokens (no
  Streamline), 239 presented.
- **Lies of P's FSR3 frame-generation presents REACH the hook** — the §H5 fear does not occur
  for the FidelityFX 3.1 facade either. The two FSR + FG runs presented 231 and 237 FPS with
  Steam's counter counting generated frames; the DLSS run, with AMD frame generation off,
  presented 59.9. So the facade's proxy swapchain forwards to the real `Present` on the shared
  `dxgi.dll` vtable, exactly as `--probe-proxy` predicted for a forwarding proxy — and the
  presented figure on those runs is therefore the DISPLAYED rate, which is what the WARNING
  says to read it as. What the run does not give is the native rate: nothing in this writer
  counts FSR3 interpolations (H11), so `fg_factor` stays N/A there. The 59.9 on the DLSS run
  is a cap or vsync, not a measurement of anything this run was about.

- **2026-09-04, the AMD identity PR (HANDOFF 7c), measured before the code was written.** Both
  Steam libraries scanned to full depth for the AMD module topology, because the hook's scoping
  depends on which module the *game* calls:

  | Title | AMD modules shipped | Shape |
  |---|---|---|
  | Lies of P, Cyberpunk 2077, Rune Factory: Guardians of Azuma | `amd_fidelityfx_dx12.dll` 1.0.1.41314 | SDK 1.1.x **monolith** — a leaf |
  | Black Myth: Wukong | the monolith **and** `amd_fidelityfx_loader_dx12.dll` 2.1.0 **and** `amd_fidelityfx_denoiser_dx12.dll` | mixed |
  | Dying Light: The Beast, Kingdom Come: Deliverance II | `amd_fidelityfx_loader_dx12.dll` 1.0.2 + `amd_fidelityfx_upscaler_dx12.dll` 4.0.2 (+ `…framegeneration_dx12.dll` 3.1.5 on DL:TB) | SDK 2.x **loader + leaves** |
  | Expedition 33 | upscaler 4.0.2 + frame generation 3.1.5, **no loader** | UE5: the plugin's built-in loader calls the leaves directly |
  | Hell Is Us | upscaler **4.0.3** + frame generation **4.0.0** under `Plugins/FSR/Source/fidelityfx-sdk/Kits/FidelityFX/signedbin/`, **no loader** | UE5, SDK 2.1.x |
  | Red Dead Redemption 2 | `ffx_fsr2_api_x64.dll` + `_dx12_` + `_vk_` | FSR 2, dynamic |
  | Cyberpunk 2077 (also) | `ffx_fsr3_x64.dll`, `ffx_backend_dx12_x64.dll`, `ffx_frameinterpolation_x64.dll`, `ffx_fsr3upscaler_x64.dll`, `ffx_opticalflow_x64.dll` | FSR 3.0 host DLLs, named exports — ~~the deferred route~~ hooked 2026-09-05 at the host facade (`ffxFsr3ContextDispatchUpscale`, tag `fsr3-v3.0.4`: the module's twelve exports match that tag, not `v1.1.4`) |

  Every module in the ffx-api family exports the same five names and nothing else
  (`vendor-exports.json`) — which the morning read as "the loader can only reach a leaf through
  the leaf's own export, so the rows are the leaves and never the loader". **The evening's run
  reversed that** (below): on the three loader-shipping titles the leaves' exports stayed silent
  while FSR ran, so the loader is the game's entry and is now a row. ~~**No statically linked FSR
  title is installed**~~ — shipping is not loading: Rune Factory ships the monolith and never
  loads it.

- **2026-09-04 evening, thirteen captures across nine titles under the leaf-only build (#110), one
  launch each** — the full table with the settings the owner set is in `20_OPEN_QUESTIONS` §H11.
  In one line each: **Lies of P** `Fsr3` / `FsrFg` / `118.98 → 238.02 (×2)` / counts agree 1.00,
  and `none` by count with FG off; **Hell Is Us** `FSR (3.1 or 4 …)` / `FsrFg` / ×2 / 1.00, and
  `none` with FG off; **Expedition 33** FSR upscale under DLSS FG ×3 → `×2.97`, *technology not
  identified*, count from UPSCALE dispatches (no PREPARE, no token); **Cyberpunk** at FSR 3: the FSR
  3.0 host DLLs upscale (`upscaler: N/A` on that build; the host row landed 2026-09-05, §H11 B1 owed) while the 1.1.x monolith generates
  (`FsrFg`, 4261 batches, ×2); **Dying Light: The Beast** (FSR + FSR FG), **KCD2** (FSR) and
  **Wukong** (FSR): **zero dispatches at any leaf export**, `upscaler: N/A` — the loader-row
  reversal; **Rune Factory** (FSR FG): no AMD module in the census, `59.99 → 119.95 (×2)` from
  Streamline tokens, *technology not identified* — 7b's statically linked title, with the count
  surviving and the identity not. **Row R6, same night, on #111:** DL:TB FSR + FSR FG →
  `FSR (3.1 or 4 …)` / `FsrFg` / `70.49 → 140.9 (×2)` / 1.01 and KCD2 FSR → identity + `none` /
  1.00, both through the loader row at 1×; Wukong FSR (×2 runs) → silent at monolith and loader
  with the monolith loaded, and the title has no FSR frame-generation option — FSR upscaling
  compiled in (Rune Factory's shape), the monolith shipped and loaded for nothing this hook sees.
- **2026-09-04, late: Dying Light: The Beast at DLSS, three captures (23:31 / 23:39 / 23:43), for
  7a after Alan Wake 2 would not run.** The first Streamline title measured whose batches carry
  `kFeatureDLSS`: `upscaler: Dlss` on 4720/4783, 4271/4345 and 4575/4657 records, `FPS 80.41 /
  72.73 / 77.69`, every batch identified, RT measured on ~98% of presents. **`UpscalerParams`
  0 of N on all three**, `render -> output: N/A`, quality byte never published. Cause, measured
  with dumpbin: DL:TB ships `sl.interposer.dll` **2.8.0**, which exports `slSetTagForFrame`
  (Cyberpunk's 2.7.1 does not); `sl.h` marks `slSetTag` deprecated in its favour, and a 2.8 title
  tagging per frame never calls the export the only params row hooked. The oracle had hidden it:
  `vendor-exports.ps1` recorded the FIRST `sl.interposer.dll` found (2.7.4.0), so the 2.8 export
  was absent from `docs/vendor-exports.json` until the tool unioned across copies (ten copies,
  eight distinct versions from 1.5.6 to 2.10.3). All three runs read `frame generation: none`
  with presents = tokens = batches; **which of the three had DLSS Frame Generation on is the
  owner's to say** — if any did, `none` there is the §H5 false negative on a title where the FG
  runtime (`sl.dlss_g.dll`) is in the census, and it is a finding about DLSS-G on SL 2.8, not
  about the tag row. Row added; rerun owed.
- **2026-09-05 00:19, DL:TB at DLSS again, on the #115 build** (the payload is the CaptureHost
  bin's `FrameLedger.Overlay.dll`, rebuilt 00:17): `Dlss` 4331/4401, `FPS 73.64`, and
  **`UpscalerParams` still 0 of 4401** with the params family live — which, under
  publish-when-whole, means BOTH tag rows were patched. So the title either tags the scaling
  input with a **zero extent** ("use the entire resource") or tags it nowhere we hook. The
  zero-extent reading is the one the record can still act on: `sl::Resource` carries its own
  `width/height` (mandatory only on Vulkan, so filled or not on D3D12 at the title's choice),
  and `TagSize` in `fl_sl_inputs.h` now reads them when the extent is zero — on every route,
  local and both global — GUID-checked, from the argument the hooked API received. If the
  next run prints a `render -> output` line, that was the shape; if not, the title does not
  state the size on this route at all and the honest line stays `N/A`.
- **2026-09-05 00:53, DL:TB at DLSS on the #116 build (payload rebuilt 00:51): `N/A` stands.**
  `Dlss` 3460/3523, `FPS 58.87`, **`UpscalerParams` 0 of 3523**, both tag rows live, the
  whole-resource fallback in. So on this title the scaling-input tag reaches neither global
  export with a size in either place, or does not reach them at all — the record cannot
  separate those two, and that is the one thing left to instrument if 7a is ever reopened on
  this title (a tag-route census: calls seen per export, zero-extent seen, Resource-size seen —
  a record bit or a writer-state word, so a layout bump). **7a on DL:TB closes as measured
  `N/A`** for the quality byte and the render size; the derived label has no input here and
  stays the owner's decision for the titles that do print the line (Cyberpunk).
  `frame generation: none` again with presents = tokens = 3441 and DLSS FG on — the fifth
  such run, §H5.
- **And the owner's answer to the FG question: all four DLSS runs had DLSS Frame Generation
  ON.** `presents = tokens` on every one, against ×2 with FSR FG on the same title and ×3.99
  on Cyberpunk (SL 2.7.1, DLSS-G 310.8) — recorded in `20_OPEN_QUESTIONS` §H5 as case 3's
  candidate, with the discriminator (the owner's counter reading) pre-committed there.
- **2026-09-05, the owner's run on #118 + #119 — eleven captures, seven titles, every one with
  DLSS or frame generation on**, the first with the `module:` lines (interposer versions read off
  the files: DL:TB 2.8.0, Cyberpunk 2.7.1, Hell Is Us 2.7.3, Expedition 33 2.7.30, Wukong 2.7.4;
  `_nvngx.dll` 32.0.16.1664 from the driver store on every NVIDIA title):

  | Title / setting | What printed | Reading |
  |---|---|---|
  | **Cyberpunk 2077**, FSR 3 + FSR FG | `upscaler: Fsr3` (host route, 4332 records at 1506×847), `FsrFg`, `74.31 → 148.56 (×2)`, `frames/upscale-drained 1.01` | **§H11 B1 accepted** |
  | **Cyberpunk 2077**, FSR 3, FG off | `Fsr3`, `none` by count 4919/4919, `1506x847 -> 2560x1440 = 1.7x (59%)` | **B1-off accepted** |
  | **Cyberpunk 2077**, DLSS + RR + MFG ×3 | `Dlss` (via RR, 3704 batches), `62.86 → 187.49 (×2.98)`, `Active (technology not identified)`, `render 1707x960` on 3677 records | count right, DLSS-G unnamed (`kFeatureDLSS_G` never evaluated) |
  | **Dying Light: The Beast**, DLSS FG ×4 (twice) | `Presented FPS 76.7` / `93.24`, **`none` WITHHELD** with the §H5 qualifier, `sl.interposer.dll 2.8.0.0` named | **owner's counter ~300 = 3.9× ours** — reading (a) with its number |
  | **Hell Is Us**, DLSS + FG ×4 (twice) | `82.23 → 328.98 (×4)`, `Active (technology not identified)`, `upscaler: N/A`; the first run refused the factor (bucket 2.25 vs 3.65 — 53 unfocused ticks) | NGX-direct SR, Streamline tokens still count |
  | **Expedition 33**, DLSS + FG (twice) | `54.9 → 218.38 (×3.98)`, `Active (technology not identified)`, `upscaler: N/A` | same shape |
  | **Black Myth: Wukong**, DLSS + FG ×4 | `55.39 → 221.53 (×4)`, `Active (technology not identified)`, `upscaler: N/A`, `sl.dlss_g.dll 2.7.4.0` loaded | same shape; the plugin bit is set on a 2.7 title, and the gate correctly did not fire (factor active) |
  | **Lies of P**, DLSS, no FG (owner) | `Presented FPS 59.77`, WARNING naming `amd_fidelityfx_dx12.dll`, `upscaler: N/A`, `tokens=0` | NGX-direct SR with no Streamline at all: nothing this build hooks fires |

  Two things the run found beyond the rows. **A consumer defect:** `render -> output: N/A` on every
  FG-on capture — Cyberpunk at FSR 3 + FG printed it two lines under `render 1506x847 on 4332
  record(s)` — because `UpscaleExtent` keyed its window on the params bit, which under frame
  generation rides on one present in N; fixed the same day (identity-bit window). **And the shape
  of what is left:** on every NVIDIA title measured the *count* is right and the *identity* is
  what this build cannot say — DLSS on four NGX-direct titles, DLSS-G on all of them — because
  the NGX SDK is licence-blocked and `kFeatureDLSS_G` is never evaluated through the export the
  Streamline hook owns. `20_OPEN_QUESTIONS` §H5 carries the one candidate that is not a hook
  (`NvAPI_NGX_GetNGXOverrideState`, by PID) and the probe that measures it before anything is
  built on it.

- **2026-09-05 evening, the owner's run on #121 (the tag route) — eight captures, six titles, every one at DLSS**
  **with frame generation on**, the first with the `Streamline tag census:` line. The pre-commitment in
  `20_OPEN_QUESTIONS` §H5 landed on every title that goes through Streamline:

  | Title / setting | `frame generation:` | Trio | Tag census (route: types) |
  |---|---|---|---|
  | **Hell Is Us**, DLSS + FG ×4 (twice) | **`DlssG`** | `38.5 → 154.04 (×4)`, `81.41 → 325.58 (×4)` | global: depth, mvec, hudless, ui-color-alpha, backbuffer, other |
  | **Expedition 33**, DLSS + FG ×2 | **`DlssG`** | `75.95 → 149.83 (×1.97)` | global: same set |
  | **Black Myth: Wukong**, DLSS + FG ×4 | **`DlssG`** | `47.99 → 191.89 (×4)` | global: depth, mvec, hudless, ui-color-alpha, backbuffer |
  | **Rune Factory**, DLSS + FG ×4 | **`DlssG`** | `128.85 → 513.02 (×3.98)` | global: same as Hell Is Us |
  | **Cyberpunk 2077**, DLSS + RR + MFG | **`DlssG`**, `upscaler: Dlss`, `1707x960 -> 2560x1440 = 1.5x (67%)` | **factor refused** — bucket 1 of 8 reads 3.0 against 1.67 overall; `tokens/batch 2.97` | global: depth, mvec, hudless, scaling-in, scaling-out, other (no UI tag) |
  | **Dying Light: The Beast**, DLSS FG ×4 | N/A (`none` withheld, §H5) + *"it is FEEDING frame generation"* | `Presented FPS 67.13` | **frame** (`slSetTagForFrame`): depth, mvec, hudless, scaling-in, scaling-out, ui-color-alpha, other |

  Three readings. **The identity is measured on six of six Streamline titles** — the UE Streamline plugin tags
  the DLSS-G inputs globally (`slSetTag`) on every UE title, Cyberpunk on 2.7.1 tags globally too, DL:TB on 2.8
  tags through `slSetTagForFrame` — so `Active (technology not identified)` is gone from every one of them,
  and `upscaler: N/A` on the UE titles is now the ONLY NVIDIA gap (NGX-direct super-resolution, the NVAPI
  probe's job). **DL:TB's 7a question is answered by the census, at no layout cost**: the title DOES tag
  `kBufferTypeScalingInputColor`, through the 2.8 export, and `UpscalerParams` is still 0 of 4030 — so the tag
  carries neither an extent nor a Resource size, which is exactly the shape #116 named and could not see; the
  `N/A` there is the title's truth. **Cyberpunk's refusal is the guard working**: `tokens/batch 2.97` with
  `presents/batch 4.97` and bucket 1 at 3.0 is a session whose state changed after the first eighth — tokens
  kept flowing while Ray Reconstruction stopped being evaluated, which is what a menu or a pause does — and the
  report refused the session-level number rather than averaging two configurations (the 2026-08-16 alt-tab
  lesson). The owner's settings for that run are recorded when known; the earlier 14:01 run at MFG ×3 read
  `×2.98` with `tokens/batch 1.00` on the same title.

- **2026-09-05 night, the owner's run on #124 (the DXGI present counter) — DL:TB twice, Hell Is Us, and a
  new title, the Onimusha: Way of the Sword demo on Streamline 2.10.3.** `20_OPEN_QUESTIONS` §H5 row
  P1-DXGI landed on the first title and both controls read zero:

  | Title / Streamline | Trio or Presented | `DXGI present counter:` | `frame generation:` |
  |---|---|---|---|
  | **Dying Light: The Beast**, 2.8.0, DLSS FG ×4 (21:56) | `Presented FPS 69.99` (withheld) | `unseen=12204 over samples=4136 (2.95)` | N/A (withheld) + *FEEDING* |
  | **Dying Light: The Beast**, 2.8.0, DLSS FG ×4 (22:06) | `Presented FPS 73.19` (withheld) | `unseen=12741 over samples=4392 (2.90)` | same |
  | **Hell Is Us**, 2.7.3, DLSS + FG ×4 | `79.89 → 308.14 (×3.86)` | `unseen=0 over samples=18207` | `DlssG` |
  | **Onimusha: Way of the Sword demo**, **2.10.3**, DLSS + FG | `60.04 → 220.33 (×3.67)` | `unseen=0 over samples=12914` | `DlssG`, `upscaler: Dlss`, RT Yes (27.1 %) |

  The 2.8.0 pacer presents on the hooked chain through a body the inline patches do not cover; 2.7.3 and
  2.10.3 present through the body they do. The owner's "`sl.interposer.dll` + `sl.common.dll` must be
  loaded on ≥ 2.8" lead is closed by the 2.10.3 title: it injects, tags on the frame route (`depth, mvec,
  hudless, scaling-in, scaling-out, ui-color-alpha, backbuffer, other`) and counts like 2.7. Onimusha's
  `render -> output: N/A` with `UpscalerParams 0/13014` is DL:TB's shape again — the frame-route tags carry
  no size on D3D12, where `sl::Resource` declares none. The PR after #124 turns the 2.8.0 reading into a
  DXGI-COUNTED Displayed (`03_METRICS` §Displayed may be DXGI-COUNTED).

- **2026-09-06 morning, the owner's run on #125 — DL:TB three times (frame generation on, then off twice)
  and Expedition 33 with frame generation off.** The promotion landed and §H5's Leg 0 answered:

  | Title / setting | Line | `DXGI present counter:` | `frame generation:` |
  |---|---|---|---|
  | **Dying Light: The Beast**, DLSS FG ×4 (07:31) | `Native FPS: 70.52 -> Displayed FPS: 282.08 (x4 FG) over 16516 present(s)` + *Displayed is DXGI-COUNTED: 12387* | `unseen=12483 over samples=4202 (2.97)` | **`DlssG`** |
  | **Dying Light: The Beast**, DLSS, FG **off** (07:45) | `Presented FPS 75.52` (withheld) | `unseen=0 over samples=4519` | N/A (withheld) — census still `0x25F0F`, tags `0x23600` (no hudless / UI) |
  | **Dying Light: The Beast**, DLSS, FG **off** (07:55) | `Presented FPS 61.14` (withheld) | `unseen=0 over samples=3659` | same |
  | **Expedition 33**, FG off (Streamline 2.7.30) | `FPS: 85.86` | `unseen=0 over samples=5063` | `none` by count; tag census `0x0` |

  Two readings. **The trio is printed on the title that could not print it**, from a count DXGI made
  (12,387 presents this hook never timed), and it is the number the owner's own counter showed (~300 at
  ×4 against ~75 native). **Leg 0: `sl.dlss_g.dll` is a startup-time load** — the census cannot tell FG
  off from FG on, exactly the cost the gate pre-committed — but the DXGI counter can (0 unseen with FG
  off, 2.9–3.0 with it on), so the gate is narrowed to sessions where the counter was not read, and the
  two FG-off captures above print `none` with DXGI's agreement on the next build. Also visible: with FG
  off DL:TB sends no hudless / UI tag and the UE plugin sends no tags at all, so the DLSS-G identity is
  per-setting on both.

- **2026-09-06, 09:20–09:35, the NVAPI probe against three running titles (`fl-probe-nvapi --ngx-state
  <pid>`, driver 616.64), each beside a capture on the #126 build.** The question §H5 opened on 2026-09-05 —
  are the driver's per-process NGX feedback bits populated without an NVIDIA-app override? — is answered
  YES for super resolution and NO for frame generation:

  | Title / override / capture beside it | SR | RR | FG | `scalingRatio` / `performanceMode` / `renderPreset` / `frameGenerationCount` |
  |---|---|---|---|---|
  | **Hell Is Us**, no override, `83.21 → 332.82 (×4 FG)` `DlssG`, `upscaler: N/A` | `0x80605` CREATED EVALUATE (+ INITIALIZED DLL_EXISTS ERR_NOT_FOUND) | INITIALIZED DLL_EXISTS ERR_NOT_FOUND | `0x5` INITIALIZED DLL_EXISTS only | 0 / 0 / 0 / 0 |
  | **Expedition 33**, override ON, FG off, `FPS: 79.4` `none` (DXGI agrees), `upscaler: N/A` | `0x8067F` CREATED EVALUATE ENABLED DLL_LOADED DLL_SELECTED PRESET PERF_MODE (+ …) | INITIALIZED DLL_EXISTS ERR_NOT_FOUND | `0x1F` INITIALIZED ENABLED DLL_EXISTS DLL_LOADED DLL_SELECTED | 0 / 2 / 11 / 0 |
  | **Lies of P**, no override, `Presented FPS 59.89`, `upscaler: N/A` | `0x605` CREATED EVALUATE (+ INITIALIZED DLL_EXISTS) | INITIALIZED ERR_FAILED ERR_NOT_FOUND | INITIALIZED ERR_FAILED ERR_NOT_FOUND | 0 / 0 / 0 / 0 |

  The SR word carries the identity the hooks cannot see on these three titles; the FG word does not see
  DLSS-G running at ×4 (it tracks the override machinery's DLL, not the plugin), `frameGenerationCount` is
  the override target and not the multiplier, and `scalingRatio` is 0 even under the override. §H5 carries
  what a driver-reported rung may and may not claim, and the negative still owed (DLSS off in the menu).
  Also on this run: Expedition 33 with FG off now prints `none` with *DXGI's own present counter agrees:
  0 unseen over 4774* (#126), and Lies of P's `Presented FPS` keeps its "MAY include generated frames"
  qualifier because `amd_fidelityfx_dx12.dll` is loaded there — the census cannot say `none` and the
  token count is 0 on that title, so that line is right as printed.

- **2026-09-06, 10:39–11:36, the owner's run on #128 (the driver-reported rung) — §H5 rows N1–N4 and one
  correction.** The standalone probe first: Lies of P with upscaling OFF reads `SR 0x5` (CREATED clear), with
  DLSS on `0x605` — N3 keeps the rung. Then the captures:

  | Title / setting | `upscaler:` | `NVIDIA driver NGX state:` SR / RR / FG | FG line |
  |---|---|---|---|
  | **Lies of P**, DLSS (no override) | **`Dlss (driver-reported: …)`** | `0x605` / `0x90001` / `0x90001` | N/A (no FG in this title) |
  | **Expedition 33**, override ON, FG off | **`Dlss (driver-reported: …)`** | `0x8067F` (mode 2, preset 11) / `0x80005` / `0x1F` | `none`, DXGI agrees |
  | **Hell Is Us**, DLSS + FG ×4 | **`Dlss (driver-reported: …)`** | `0x80605` / `0x80005` / **`0x605`** (CHANGED between readings) | `81.32 → 321.73 (×3.96)`, `DlssG` |
  | **Onimusha demo**, SL 2.10.3, DLSS + FG | `Dlss — the NVIDIA driver agrees` | `0x605` / `0x5` / **`0x605`** (CHANGED) | `59.99 → 235.47 (×3.93)`, `DlssG` |
  | **Dying Light: The Beast**, DLSS + FG ×4 | `Dlss — the NVIDIA driver agrees` | `0x605` / `0x90001` / **`0x605`** (CHANGED) | `39.63 → 158.5 (×4)` DXGI-COUNTED, `DlssG` |
  | **Cronos: The New Dawn demo** (new; UE, **SL 2.8.0**), DLSS, FG off | **`Dlss (driver-reported: …)`** | `0x80605` / `0x80005` / `0x5` | `FPS 105.01`, `none`, DXGI agrees; RT Yes |
  | **Dying Light: The Beast**, **FSR upscaling + DLSS FG** | `FSR (3.1 or 4 …)`, `2560x1440 -> 2560x1440` | **`0x205`** (CREATED, not EVALUATE) / `0x90001` / `0x605` | `DlssG` from the tags; factor REFUSED (bucket 8 of 8 at 1.96 vs 2.85); DXGI 1.80 unseen / present |

  Three readings. **The rung works as written and its negative held.** **The FG word is not blind** — the
  morning's Hell Is Us reading was taken before the feature came up, and every ×4 session's loop readings
  show `CREATED | EVALUATE` with the word changing mid-session; the multiplier bits never set, so it is an
  identity witness and is now printed beside `frame generation:`. **`SR 0x205` on the FSR session is the
  word telling the truth finely**: DLSS was created at some point (the title initialises it) and not
  evaluated, and the rung's two-bit key keeps it from claiming DLSS beside FSR. The FSR + DLSS-G session
  also exposed a wrong qualifier ("no evaluation was observed" beside 4,415 tokens and a refused factor),
  fixed the same day. Cronos is a second Streamline 2.8.0 title; its FG-on capture is the one that says
  whether the pacer bypass measured on DL:TB belongs to SDK 2.8.0 or to that title.

- **2026-09-06, 12:17, Cronos: The New Dawn demo with frame generation ON — the row that closed §H5's
  DLSS / DLSS-G question.** Streamline 2.8.0, UE: `Native FPS: 86.8 -> Displayed FPS: 172.72 (x1.99 FG)`
  over 9,948 presents, `unseen=0 over samples=10063`, tag census `frame=[depth, mvec, hudless,
  ui-color-alpha, backbuffer, other]`, `frame generation: DlssG — the NVIDIA driver agrees`, `upscaler: Dlss
  (driver-reported)`, RT Yes. The 2.8.0 SDK presents its generated frames through the body the inline
  patches cover; the bypass measured on Dying Light: The Beast is that title's integration. With this row
  every measured NVIDIA title prints the rule-6 trio or a counted `none`, and the owner closed the item.

- **2026-09-06, 12:46, Lies of P at FSR 3.1 + FSR frame generation, on the #129 build — §H11 row A1 again,
  now with every witness on the page.** `upscaler: Fsr3`, `render -> output: 2560x1440 -> 2560x1440 = 1x` on
  3,538 records, `frame generation: FsrFg`, `Native FPS: 59.77 -> Displayed FPS: 119.55 (x2 FG)` over 7,042
  presents; the ffx census `upscale-drained=3518  fg-dispatch-drained=3518  frames=3520`, `presents/frame=2.00`,
  `frames/upscale-drained=1.00`; `DXGI present counter: unseen=0 over samples=7096` (the FSR 3.1 pacer presents
  through the patched body); the NVIDIA driver's word `SR 0x5` / `FG 0x90001` — nothing created, so the
  driver-reported rung correctly says nothing beside an AMD identity. The census names only
  `amd_fidelityfx_dx12.dll` (the 3.1 monolith Lies of P calls) — the AMD half of 7c is complete on its oracle.
  Same run, Rune Factory at DLSS + FG ×4: `125.45 → 498.51 (×3.97)`, `Dlss (driver-reported)`, `DlssG` with the
  driver agreeing — the 7b title on its NVIDIA path.

- **2026-09-06, 13:45–14:02, the owner's run on #132 (rung 3 by elimination + the executable scan): Cronos at
  XeSS (no FG option), Hell Is Us at XeSS + XeSS-FG, Rune Factory at FSR + FSR FG.**

  | Title / setting | Line | `frame generation:` | `executable markers:` |
  |---|---|---|---|
  | **Cronos**, XeSS, no FG | `FPS: 110.02`, `none` by count, DXGI agrees | `None` | ffxFrameInterpolation ×1, NVSDK_NGX ×112, xefgSwapChain ×13 |
  | **Hell Is Us**, XeSS + **XeSS-FG** | `Presented FPS: 186.47`, **`tokens=0`**, `unseen=0` | N/A — no token on this plugin at XeSS; witnesses: not DLSS-G, no FSR dispatch, runtimes nvngx_dlssg + libxess_fg + amd FG, file carries xefgSwapChain | same set |
  | **Rune Factory**, FSR + **FSR FG** (compiled in) | `30.05 → 60.05 (×2 FG)` from the token, `unseen=0` | `Active (technology not identified) — by elimination …; the executable itself carries ffxFsr3, ffxFrameInterpolation` | ffxFsr3 ×13, ffxFrameInterpolation ×9, NVSDK_NGX ×112 |

  Three readings. Rune Factory is the pre-committed 7b line, verbatim. XeFG presents through the patched
  body (0 unseen) so its Displayed rate is measured; its Native is not, because Hell Is Us's plugin requests
  no Streamline token at XeSS (it does at DLSS, and Cronos's does at XeSS) — the count producer is the
  plugin's choice, and nothing in policy replaces it. The Steam overlay could not show FG on that session
  either. The upscaler N/A on both XeSS titles now names Intel's licence as the reason (7c item 4).

