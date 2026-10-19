*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 8 · The accuracy question — why this rewrite exists

≥ 3 real offline titles. Verify against each game's own settings menu.

| Title | Upscaler | Quality | Render → output | FG active | RT | Matches menu? |
|---|---|---|---|---|---|---|
| **Cyberpunk 2077** (2026-08-15) | ❌ `Unknown` | ⬜ `0xFF` | ✅ **1485×835 → 2560×1440** | ⬜ not measured | ⬜ not measured | **1 of 5** |
| **Cyberpunk 2077** (2026-08-15, later, after §S30) | ✅ `Dlss` | ⬜ `0xFF` | ✅ **1485×835 → 2560×1440** | ◐ on, factor not measurable on this route | ⬜ not measured | **2 of 5** |
| **Cyberpunk 2077** (2026-08-20, PT on · RR on · MFG ×4 · Balanced) | ✅ `Dlss` | ⬜ `0xFF` | ✅ **1485×835 → 2560×1440** | ◐ on, `presents/batch` = 4.00 as a PROXY | ✅ **`Yes`** | **3 of 5** |
| **Alan Wake 2** (2026-08-20, RR on · PT ultra · FG 4X *set but reportedly not applying*) | ✅ `Dlss` | ⬜ `0xFF` | ⬜ no local tag; **1706×960 corroborated arithmetically, see below** | ❓ `presents/batch` = 1.00 — unexplained | ✅ **`Yes`** | **2 of 5** |
| **Black Myth: Wukong** (2026-08-20, RT High · FG ×4 · Balanced) | ⬜ `Unknown` — *coverage short, not the title's* | ⬜ | ⬜ no local tag | ◐ `presents / RT-active` = **4.0000** as a PROXY | ✅ **`Yes`** | **2 of 5** |
| **Rune Factory: Guardians of Azuma** (2026-08-20, no RT option · FG ×6 via NVIDIA App · DLAA) | ⬜ `Unknown` | ⬜ | ⬜ no local tag | ⬜ nothing observed | ✅ **`No`** | **1 of 5** |
| **Cyberpunk 2077** (2026-08-20, **all RT off** · RR off · MFG ×4 · Balanced) | ✅ `Dlss` | ⬜ `0xFF` | ✅ **1485×835 → 2560×1440** | ◐ `presents/batch` = 4.00 as a PROXY | ✅ **`No`** | **3 of 5** |
| **Cyberpunk 2077** (2026-09-04, RR on · MFG **×4** · Balanced) | ✅ `Dlss` | ⬜ `0xFF` (title never chains `sl::DLSSOptions`) | ✅ **1485×835 → 2560×1440** | ✅ **×3.99 MEASURED** — `presents / tokens`, §S31 row P1, `tokens/batch` 1.00 | ✅ `Yes` | **4 of 5** |
| **Cyberpunk 2077** (2026-09-04, MFG **×3**) | ✅ `Dlss` | ⬜ `0xFF` | ✅ | ✅ **×2.99 measured** | ✅ | **4 of 5** |
| **Cyberpunk 2077** (2026-09-04, MFG **off**, RR on) | ✅ `Dlss` | ⬜ `0xFF` | ✅ | ✅ **`none` by counting** — 1.00, 4,922 tokens against 4,922 presents | ✅ | **4 of 5** |
| **Hell Is Us** (2026-09-04, DLSS · FG **×4**) | ❌ `N/A` — SL loaded, `slEvaluateFeature` never called (NGX-direct SR) | ⬜ | ⬜ | ✅ **×4.00 measured**, identity `Active (technology not identified)` | ✅ `No` | **2 of 5** — the FG factor is right on a title whose upscaler is invisible to this writer |

> ### ✅ FIVE CAPTURES ACROSS FOUR TITLES — 2026-08-20, and the RT tri-state is complete
>
> Every run: 40 s, one identified swapchain, one segment, **0 gaps, 0 dropped**, foreground on
> all 369 drain ticks, `status=Ready`, `layoutVersion=3`, `rtTier=12`, `apiMask=0x4` (D3D12),
> and **`faults=0`** — including a title that pushed **37,823 presents through the hook in 39
> seconds** (~950/s) without a drop. All six hook families installed on every title. The
> Overlay payload was hash-verified against the just-built DLL before each run.
>
> | Run | records | batches | Streamline ids seen | asBuild / dispatch | `rt_frame_pct` | Displayed FPS |
> |---|---|---|---|---|---|---|
> | Cyberpunk, PT on | 10,603 | 2,578 | `DLSS_RR` 2578 | 2,561 / 2,567 | 24.2% (**25.0%** of the claiming window) | 264.93 |
> | Alan Wake 2 | 900 | 875 | `DLSS_RR` 875 | 869 / 871 | 96.8% | 22.5 |
> | Wukong | 5,956 | **0** | **none at all** | 1,437 / 1,441 | 24.2% (**25.0%**) | 148.89 |
> | Rune Factory | 37,823 | **0** | **none at all** | **0 / 0** | **0.0%** | 946.44 |
> | Cyberpunk, all RT off | 17,456 | 4,242 | **`kFeatureDLSS` 4242** | **0 / 0** | **0.0%** | 436.28 |
>
> **1 · The tri-state is complete, and `No` is correct twice for two different reasons.**
> `Yes` on Cyberpunk (path tracing) and Wukong (RT High); `No` on Rune Factory, whose settings
> menu has no ray-tracing option at all, **and** on Cyberpunk with every RT option switched off
> — a title that *can* ray-trace and is not doing so, which is the harder and more valuable
> case. Both negatives satisfy all three conjuncts `03_METRICS` requires: `rtTier` 12,
> `RtAsBuild` installed, and zero evidence across 36,575 and 16,871 claiming records.
>
> **2 · `slEvaluateFeature` IS NOT A GENERAL ROUTE, and this generalises HANDOFF item 3 from
> one title to four.** Two of the four titles never call it *at all* — Wukong with both
> `sl.interposer.dll` and `sl.dlss_g.dll` loaded and DLSS-G demonstrably running, and Rune
> Factory likewise. The other two call it only for Ray Reconstruction. **`kFeatureDLSS_G` is
> zero on every run**, so item 3's counter has nothing to count on any of them.
>
> **3 · §S30's closure survived a test in the REVERSE direction, which it never had.** It
> concluded that Ray Reconstruction *replaces* the super-resolution pass rather than running
> beside it, from one configuration. Turn RR off on the same title, same machine, one setting
> changed: the census flips from `DLSS_RR` 2578 / `DLSS` 0 to **`DLSS` 4242 / `DLSS_RR` 0**.
> This is also the **first observation of `kFeatureDLSS` anywhere in this project**, and the
> local-tag extent arrives on that evaluation exactly as it did on the RR one — the same
> `1485×835`.
>
> **4 · A SECOND FG proxy, and it covers what the first cannot.** On Wukong there are no
> Streamline batches, so `presents/batch` is unreadable — and `presents ÷ RT-active presents`
> reads **5,764 / 1,441 = 4.0000 exactly** against a title configured for ×4. It carries the
> *same* unverified premise as `presents/batch` (that the work is recorded once per application
> frame), so it is a proxy and not a producer. What it adds is coverage: the two are readable
> on disjoint sets of titles, and on the one run where both were available they agreed.
>
> **5 · The #87 falsifier did not fire, on two independent titles.** It pre-committed that
> `rt_frame_pct` must read ≈25% at ×4 and not ≈100%. Cyberpunk: 25.0%. Wukong: 25.0% — and
> Wukong reached it through the RT evidence alone, with no Streamline batches involved.
>
> **6 · Alan Wake 2 is the outlier and is NOT explained.** `presents/batch` = 1.00 and
> `rt_frame_pct` = 96.8%, i.e. every present carried an application frame's work, against a
> menu set to FG 4X. Two readings fit every number equally: generated presents not reaching the
> vtable we patch (§H5 case 3), or frame generation simply not running. **The operator reported
> that this title would not apply its settings**, which is evidence for the second and is why
> it is not written up as §H5 case 3. The discriminating run — the same scene with FG off — was
> not taken.
>
> **7 · One arithmetic result worth keeping on its own.** Alan Wake 2's dispatch volume is
> `4,279,466,880 / 871 = 4,913,280` rays per RT-active present, and `3 × 1706 × 960 =
> 4,913,280` **exactly** — three rays per pixel of the render resolution the game's own menu
> states. The params hook reported nothing on that title, so the render resolution was
> confirmed by a completely different route from the one meant to measure it.
>
> **8 · `FL_MEASURED_UPSCALER_PARAMS` produced nothing on three of the four titles.** Only
> Cyberpunk tags scaling inputs locally. §2b's local-tag route is narrower than that entry
> assumed, and the honest failure mode held: the writer published *nothing* rather than
> something wrong.

**The first row, and it is one row.** Read the legend before the marks: ✅ measured and
correct against the title's own settings; ❌ measured and **wrong**; ⬜ honestly absent — no
producer, or the title does not expose it on the route we take.

Conditions: Cyberpunk 2077 in combat, 40 s bounded capture, **10,169 presents, 0 gaps, 0
dropped**, 254.82 displayed FPS, `apiMask` = D3D12, hooks `Present | UpscalerIdentity |
UpscalerParams`. Consent granted by the operator; the guard evaluated `Allow`. A second run
minutes earlier gave 11,108 presents with the same shape.

**Render → output is the one that lands, and it lands exactly.** `UserSettings.json` records
`DLSS = Balanced` at `2560x1440`; Balanced is 0.58, so 2560 × 0.58 = 1484.8 and
1440 × 0.58 = 835.2. The writer said **1485×835**. This is the first of exit criterion 1's
five values measured correctly from a real game.

> ### The second row, and §S30's answer: Ray Reconstruction was doing the upscaling
>
> **Three further 40 s captures, same title, same settings** (`DLSS = Balanced`,
> `DLSS_D = True`, `DLSS_MultiFrameGeneration = x4`, `ReflexMode = Enabled`, 2560×1440),
> with the id census the previous row's defect motivated. Conditions: 10,092–10,443
> presents, 0 gaps, 0 dropped, ~260 displayed FPS, `apiMask` = D3D12, `rtTier` = **12**
> (`TIER_1_2`), hooks `Present | UpscalerIdentity | UpscalerParams | FgEvaluations`.
>
> **The census, and it is the whole of §S30:** of 2,523 batches, `kFeatureDLSS_RR` = 2,523,
> `kFeatureDLSS` = **0**, `kFeatureNIS` = 0, `kFeatureDLSS_G` = **0**, undecoded = 0. With
> Ray Reconstruction on, the title evaluates RR **instead of** super-resolution, not beside
> it. The decode had no arm for that and reported `UNKNOWN`. What settles it rather than
> suggesting it: `renderW/H` are published only on a frame that drained an evaluation, and
> they are 1485×835 — so the scaling-input tag arrives ON the RR evaluation. The evaluation
> that upscales is the one the decode was already looking at.
>
> **`presents/batch = 4.000` on three independent runs** (10,176/2,544 · 10,276/2,569 ·
> 10,092/2,523), against the title's own `x4`. Two things follow, and the second is the one
> that changes the plan:
>
> - **DLSS-G's GENERATED presents reach the vtable we patch.** A factor of exactly 4 cannot
>   be produced by a writer that only sees application frames. §H5's fear — `fg_factor`
>   structurally 1.0 on every Streamline title — does **not** hold for the present path.
> - **`slEvaluateFeature(kFeatureDLSS_G)` is never called.** Zero, in ~7,600 batches across
>   three runs, while frame generation is demonstrably active. On Streamline 2.x DLSS-G is
>   driven through the interposer's swapchain proxy, not through the feature-evaluation
>   entry point — so **HANDOFF item 3's premise is wrong for this route**, and the counter
>   built for it is correct and has nothing to count. `presents/batch` is a working proxy
>   only because RR happens to be evaluated once per application frame.
>
> **Still unmeasured, and it is one setting away:** the same capture with multi-frame
> generation OFF. If `presents/batch` falls to ~1 the 4.000 is proven to be FG rather than a
> property of how this title evaluates RR, and §H5 closes on two points instead of one.
> Until then the ×4 reading has one configuration behind it.

**Quality `0xFF` is a measurement of the TITLE, not a gap in the code.** Cyberpunk sets its
preset out of band through `slDLSSSetOptions` and never chains `sl::DLSSOptions` into
`slEvaluateFeature`'s `inputs`. The only route that would reach it is the one
`docs/HANDOFF.md` §2b refuses on five grounds. So **the `DLSSOptions` half of the inputs walk
has a ZERO hit rate on this title** — the unknown §2b flagged as its largest, now measured
once. `0xFF` is the defined "a hook ran and could not tell", and it is true.

**Upscaler `Unknown` is WRONG, and is filed as §S30 rather than reported as a result.**
Cyberpunk is running DLSS. Every one of the 2,461 params-carrying presents decoded to
`UNKNOWN`. Honest — it is never `NONE` — and not the answer the exit criterion asks for.

**One number fell out that this run was not designed to take.** The params bit appeared on
2,461 of 10,169 presents (24.2%), i.e. **10,169 / 2,461 = 4.13**; the earlier run gave
11,108 / 2,696 = **4.12** independently. `UserSettings.json` says
`DLSS_MultiFrameGeneration = x4`. That is `fg_factor` matching its oracle to two significant
figures **before the frame-generation producer exists** — strong support for counting
evaluations directly (the 2026-08-14 owner ruling) rather than deriving generated frames from
a multiplier. **It is not a measurement of `fgEvaluations`**: it uses the params bit as a
proxy for "an SL feature evaluated during this present", and item 3's producer must reproduce
it directly before the number may be published.

**Two operational facts, learned by hitting them.** The lazy feature-hook install costs the
opening **~1.15 s** of every session — 292 of 10,169 records lack the `Upscaler` bit, and 288
of 11,108 in the other run, both matching the 1 Hz watchdog. And **a game launch yields ONE
capture**: when the host detaches, the Overlay stops receiving guard ticks and self-unhooks at
the 65 s deadline exactly as `19_SAFETY` specifies, so the next attach correctly reports
`SupervisionLost` rather than a live session. The Overlay does not re-arm.

### ✅ The baseline exists — `fl-baseline-probe` (2026-08-03)

**§M9's problem is closed.** `15_ROADMAP` required comparing against "the old
file/module-based detection", which does not exist in this repository, so item 4
had no left-hand side and ADR-7's founding claim was unfalsifiable. The baseline
is now a real tool rather than a memory of one.

- **Baseline used instead:** `src/native/tools/fl-baseline-probe`, reading the
  `capabilities` group of `rules/detection-rules.json` (`rulesVersion 2026.08.1`,
  **7 capabilities**: DLSS, DLSS-G, Ray Reconstruction, Streamline, FSR, XeSS,
  XeSS-FG). It answers two questions per capability — is it **on disk** beside
  the game, and is it **loaded** right now.
- It **reuses the guard's module enumerator** (`SystemSources().EnumerateModules`,
  `LIST_MODULES_ALL`, §1) rather than carrying a second walk, so the baseline and
  the product see the same module list — including the same fail-closed
  behaviour. An `INCOMPLETE` or `FAILED` scan is printed as such and must never
  be read as "no capability loaded".
- It reads the capability data in its **own translation unit**. The guard's jsmn
  parser deliberately reads only the `anticheat` subtree, and teaching it a group
  the hard gate does not need would spend the gate's parse budget (§S17) on
  inference data.
- **Proven both directions**, ctest `fl_baseline_probe`: a clean process reports
  nothing loaded, a planted module *is* detected, and the answer flips back after
  unload rather than latching. The planted module is **our own
  `FrameLedger.Guard.dll` copied under a capability name** (`14_TESTING`
  §Integration tests) — no NVIDIA, AMD or Intel binary is shipped, downloaded or
  executed, and none needs to be installed for the test to mean something.
- Proven red twice: a probe that never reports `loaded`, and findings that latch
  across scans.

### 🔴 The comparison item 4 asks for cannot be a percentage

**Stated before any README wording is drafted, because the roadmap's phrasing
("quantify the improvement") invites a number that would have to be invented.**

Item 4 asks the baseline about upscaler identity, quality preset, render vs
output resolution, and DLSS-G activity. The baseline can answer **none of them**:

| Item 4 asks | Baseline answers | Hooks answer |
|---|---|---|
| upscaler identity | *ships / loads* DLSS — a different statement (`05_DETECTION`:10) | measured |
| quality preset | **nothing** | measured |
| render vs output resolution | **nothing** | measured |
| DLSS-G active | *ships / loads* DLSS-G | measured |
| engine · platform · version | detected | not measured by hooks at all |

A loaded `nvngx_dlss.dll` means the title *can* use DLSS this run. It does not
say whether DLSS is on, at what preset, or at what render resolution — and a
title that ships the DLL and has it disabled in the menu is indistinguishable
from one using it.

So the honest form of ADR-7's claim is **"the baseline cannot answer four of
these five questions at all"**, not "N% more accurate". That is both stronger and
checkable. **Owner decision, recorded here rather than assumed.**

- **Quantified improvement (belongs in the README):** *not a percentage — see
  above. Fill the per-title table once an offline title is chosen.*

### ✅ The static half, measured on three real titles (2026-08-03)

First run of the detector against real installs. **It failed on three of four
cases, and the failures were the point** — both were real defects that only a
real layout could expose.

| Title | Store / engine | On disk (scanned by hand) | Detector, after the fixes |
|---|---|---|---|
| Deadly Heart Gambit | Steam / Unity | `UnityPlayer.dll`, `steam_api.dll` | `unity` **2022.3.32.13119501**, `steam` |
| Lies of P | Steam / Unreal | `nvngx_dlss.dll`, `amd_fidelityfx_dx12.dll`, `steam_api64.dll` | `unreal`, `steam`, **dlss + fsr** |
| Alan Wake 2 | Epic / *(Northlight)* | `EOSSDK-Win64-Shipping.dll`, 6 NGX/Streamline DLLs | `epic`, **dlss + dlss_g + dlss_rr + streamline** |

Alan Wake 2 reports **no engine**, correctly: Remedy's Northlight is not in the
rules table, and "no rule matched" is a real answer rather than a failure. Lies
of P reports no engine *version* because its `LOP-Win64-Shipping.exe` carries no
`ProductVersion` for the regex to read — also honest.

**None of the three carries anti-cheat**, so all three are legitimate candidates
for the first real injection (item 2).

#### 🔴 Defect A — a depth cap that made the detector useless

`GameFileProbe` capped the walk at depth 4. Measured real depths: **Deadly Heart
Gambit 6, Alan Wake 2 5, Lies of P 9.** Every real game exceeded it.

Worse than the cap was what incompleteness *meant*: an unfinished walk marked
`FileExists`, `SiblingGlob` and `DirExists` all uncollected, so **every**
file-based signal became `Unknown`, the engine walk stopped at its first rule,
and nothing was ever identified. Failing safe is right; failing safe on every
input is not working.

Fixed by separating the two questions. A file the walk **listed** is a file that
is there, however early it stopped afterwards — only *absence* is in doubt. So a
hit stays `Match` regardless, and only a miss becomes `Unknown` when the listing
did not finish. Caps raised to depth 16 / 200,000 entries, with the entry count
as the real bound.

#### 🔴 Defect B — the pre-scan was looking in the wrong directory

**This one is a hole in a hard gate, not a detection miss.**

Both the probe and `ImageDirectoryImpl` derived "the game directory" by stripping
the filename from the executable's path. Unreal puts the exe at
`<root>\<Project>\Binaries\Win64\` — measured on Lies of P, that directory holds
**seven files**, none of which could ever have been an anti-cheat SDK, because
`EasyAntiCheat/` sits at the install root three levels up.

So for exactly the layout most likely to carry EAC, **check 4 scanned a folder
that could not contain what it was looking for** and returned clean. It also cost
Lies of P its DLSS capability, which is how the defect was noticed at all.

Fixed by `ResolveInstallRoot`: walk up from the executable to a hardcoded
platform boundary (`steamapps\common\<X>`, `GOG Galaxy\Games\<X>`,
`Epic Games\<X>`) and scan from there. Hardcoded for the same reason
`IsPlatformLauncher` is — a data-driven boundary would let a rules update move
where the hard gate looks.

**When no boundary is recognised the executable's own directory is kept.** Alan
Wake 2 is installed at `D:\another\epic\AlanWake2` with no store marker in the
path; walking up blindly would reach a folder of unrelated games, and refusing a
title because a *sibling* ships anti-cheat is a false refusal with no appeal.
That residual is stated rather than hidden: a nested executable outside a
recognised store layout is still scanned from its own directory.

#### The entry point must not change the answer — and now it does not

Unreal titles conventionally ship **two** executables: a shim at the install root
and the real shipping binary nested under `<Project>\Binaries\Win64\`. Lies of P
has `LOP.exe` and `LOP-Win64-Shipping.exe`.

That matters because a user adds one of them to the watchlist, and the guard is
handed whichever process it is handed. Before this resolution existed the two
disagreed:

| Entered via | Before | After |
|---|---|---|
| `LOP.exe` (root shim) | engine **undetermined**, capabilities **none** | `unreal`, `steam`, `dlss + fsr` |
| `LOP-Win64-Shipping.exe` | `unreal`, `steam`, **`fsr` only** | `unreal`, `steam`, `dlss + fsr` |

Both now resolve to the same install root and produce identical results, which is
asserted in both languages rather than left as an observation.

### Still unmeasured

The runtime table above is empty on purpose. The static half is now real and has
been run on three real installs; the per-title runtime rows still need a
**hooked** session.

**That is no longer item 2.** Item 2 closed on 2026-08-03 — the Overlay loads
into a real title and the process survives it (§7).

> **"Nothing is hooked yet" was true when written and is not now** (corrected
> 2026-08-05). `Present` interception, the ring writer and the fault policy all
> landed in #40–#44, ahead of the P1 label they were filed under. What blocks the
> rows above is therefore no longer the *present* path — it is that **no upscaler,
> FG or RT hook exists** (P0 items 4, 6 and 7), and that ~~**nothing reads the ring**:
> `src/FrameLedger.Shared` holds a `.csproj` and no `.cs` files, so the Overlay's
> output is observable only from inside `guard_test.cpp`~~. Both are prerequisites of
> filling this table, and neither is "finding a game".
>
> > **The reader half is done, corrected later the same day.** `ShmLayout.cs` (#47),
> > `ShmHandshakeValidator` (#49), `ShmRingReader` (#50) and a closed write-read
> > integration test (#51) all exist, and the Overlay's output is readable from managed
> > code. This passage was itself written to correct an earlier stale claim and went
> > stale within hours, in the same file — which is why the strike is left visible.
> >
> > **What still blocks this table is one thing plus one caveat.** The one thing: no
> > upscaler, FG or RT hook exists. The caveat: the reader has **no production caller**,
> > so something must drive it against a real title — and §S27 forbids an injecting
> > entry point on a shipped binary until a consent record exists, so that driver is its
> > own piece of work rather than a command-line flag.

