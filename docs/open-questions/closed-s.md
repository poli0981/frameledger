# Open questions — closed S-series entries

*Moved verbatim from `docs/20_OPEN_QUESTIONS.md` on 2026-10-19 (beta.24, owner decision D66): the entries §S24 marks resolved (✅), with their reasoning, in their original order — never deleted. The table at the end of the S-series series there lists each one. Nothing below was edited, so its "below" and "above" point inside the original file.*

### S26 ✅ · The ring counted occlusion probes as frames — **closed 2026-08-05**

`DXGI_PRESENT_TEST` runs the presentation test and **submits nothing**. The writer
recorded them like any other present, so a minimised or fully occluded game — which
issues them continuously — produced records that `03_METRICS` would have turned into
a frame rate it was not rendering, and into frame-time intervals that bound no frame.

**Who was responsible for filtering was assigned to nobody.** `07_IPC` did not say,
`03_METRICS:9` lists `presentFlags` among the consumed fields and is silent on this
value, and the harness's own history shows why that matters: every present in
`hook-harness` was once a probe, which made "N presents → N records" satisfiable
**only** by a writer that counts non-frames (`gates-that-cannot-fail`, #35).

**Decided: the writer drops them**, so the ring means one thing. The filter is placed
*after* the safety checks, so a probe-only process still evaluates the stop rather
than going unsupervised because it stopped drawing.

**Measured, both directions.** `hook-harness --hold-presenting 12` *without* `--real`
— a live, hooked, supervised target presenting nothing but probes — puts **142
records** in the ring against the pre-fix writer and **0** after. The test asserts
`status == READY` and `faultCount == 0` alongside the count, so "empty because we
unhooked" and "empty because we faulted" cannot pass for the right answer.

### S28 ✅ · The guard is not re-entrant, and nothing enforced that — **closed 2026-08-05**

Predicted by the drain design review, then **reproduced by CI** rather than argued.

`fl_guard_abi.h` states the one-at-a-time contract and three more comments in the
native tree repeat it. It was a comment, not a mechanism: `NativeAntiCheatGuard`
dispatched every call onto an arbitrary thread-pool thread, and `grep` for
`lock`/`SemaphoreSlim`/`Interlocked` over `Application` and `Infrastructure`
returned nothing.

**All four entry points share process-wide statics.** `ParseRules` keeps a
`static jsmntok_t toks[]` and is reached by `FlGuardEvaluate` and `FlGuardedInject`
through `LoadRules`, by `FlStaticPreScan`, and by `FlGuardCheckRules`.
`EvaluateImpl` additionally clears a function-scope `static Rules` on entry.

**Why that is a fail-open and not merely a race.** A second concurrent call clears
the blocklist while the first is matching against it — and an **empty blocklist
matches nothing**, so `CheckServices` iterates zero families, `CheckModules` and
`CheckDrivers` fall through, and `Evaluate` returns `Allow` from checks that looked
at nothing. `CheckModules` already refuses an empty *scan set* on the grounds that
it "must never read as clean"; the same rule had never been applied to an empty
*blocklist*.

**How it surfaced.** The drain integration test injects through the guard;
`RulesSeedingEndToEndTests` validates a document through `FlGuardCheckRules`; xUnit
runs test classes in parallel. The seeder test returned the wrong outcome. Two
callers, two entry points, one static — and neither test is about concurrency.

**Closed by a `static SemaphoreSlim(1,1)` in `NativeAntiCheatGuard`**, around every
native call including the synchronous `NativeCheckRules`. It goes in the facade
because that is the one place every native call passes through (§S15: one matcher,
not two), and it is `static` because the contract is a property of the loaded DLL
rather than of an instance.

> **What this does NOT do**, said rather than left to be assumed: it serialises
> *managed* callers. The native contract is still unenforced for anything that
> reaches the ABI another way — the Vulkan layer compiles `fl_ac_rules.cpp` in and
> parses its own copy at init, which is a different process and therefore fine
> today, and `fl_guard_test` drives the guard directly. If a second managed host
> ever loads the DLL, this lock does not span processes.
>
> > **A second managed host now exists, and it needs no lock — recorded 2026-08-06.**
> > `FrameLedger.CaptureHost` loads `FrameLedger.Guard.dll` alongside the Agent, and a
> > design panel over that PR proposed a machine-wide `GuardProcessLock` on the strength
> > of the sentence above. **It was wrong, and the refutation is this entry's own text.**
> > The fail-open is a race on PROCESS-LOCAL statics — `ParseRules`' `static jsmntok_t
> > toks[]` and `EvaluateImpl`'s function-scope `static Rules` — and a second process
> > gets its own image of the DLL and its own copies, which is exactly what this entry
> > already concedes for the Vulkan layer: *"a different process and therefore fine
> > today"*. The trailing sentence scopes what the `SemaphoreSlim` covers; it does not
> > assert a cross-process hazard. Wrapping a hard safety gate in a named mutex would
> > have added abandonment and deadlock failure modes to the gate for a hazard that does
> > not exist.
> >
> > The one genuine cross-process interaction is the rules FILE, and `FileSystemRulesStore`
> > already handles it: `ReplaceFileW` with a named backup, `FileShare.ReadWrite |
> > FileShare.Delete` on the read, both measured.

### S30 ✅ · The record names the WRONG upscaler on a real title — **closed 2026-08-15, and the fix broke the instrument that found it**

> **Answered: Ray Reconstruction was doing the upscaling.** With `DLSS_D = True` Cyberpunk
> 2077 evaluates `kFeatureDLSS_RR` on every application frame and `kFeatureDLSS` **not once**
> — 2,523 of 2,523 batches across three 40 s captures, zero DLSS, zero NIS, zero undecoded
> ids. RR **replaces** the super-resolution pass rather than running beside it; the decode
> had no arm for that and fell through to `UNKNOWN`. `spike-notes` §8 carries the run.
>
> **Not the shortcut this entry forbade.** No preference is applied and no id is outranked,
> because only one id ever arrives. What makes it a measurement: `renderW/H` are published
> only on a frame that drained an evaluation, and they read 1485×835 against the title's own
> `DLSS = Balanced` at 2560×1440 — 0.58 exactly — so the scaling-input tag arrives **on the
> RR evaluation**. The evaluation that upscales is the one the decode was already holding.
> Mapped to `FL_UPSCALER_DLSS` and not a new value: layout v3 retired
> `FL_UPSCALER_RETIRED_RAY_RECONSTRUCTION` precisely because RR is an independent axis, and
> `FL_FEAT_RAY_RECONSTRUCTION` already carries it from the same word.
>
> **THE DECISION TABLE BELOW HAD TWO HOLES, AND THE RUN FOUND BOTH.** It was written before
> the measurement, exactly as this entry demanded, and it was still incomplete: **O1 assumed
> `kFeatureDLSS_G > 0`**, so no row covered "batches arrive and DLSS-G never does"; and **no
> row anticipated a single non-super-resolution id accounting for 100% of batches.** The
> table is kept unchanged rather than quietly repaired, because a pre-committed table that
> gets edited after the fact is worth nothing, and because what it got wrong is the useful
> part: the hole was in the *hypotheses*, not in the discipline. Writing it first is what
> made the gap visible instead of invisible.
>
> **AND THE FIX CONTAMINATED THE INSTRUMENT.** `SlCensus` derived "a super-resolution id
> arrived" from the DECODED `upscaler` byte, so correcting the decode made it report 2,569
> arrivals of an id that arrives zero times — a measurement that moves when its subject moves
> cannot be evidence about the subject, which is the only job this entry gave it, and the
> next reader would have taken it at face value. `FL_FEAT_SL_SUPER_RESOLUTION` now carries
> the raw fact from the drain word, independent of any decode. Generalises: **when a decode
> is corrected, check every consumer that derived a fact FROM that decode.**
>
> **What is NOT closed by this.** `kFeatureDLSS_G` is never evaluated either — see
> `spike-notes` §8 and §H5 — so item 3's counter has nothing to count on this route. That is
> a separate finding with its own consequences for HANDOFF item 3, and it does not belong to
> this entry.

<details>
<summary>The entry as it stood while it was open, including the pre-committed table with its two holes</summary>

### S30 ❓ · The record names the WRONG upscaler on a real title, and it is honest while being wrong

**Measured 2026-08-15, Cyberpunk 2077, and it is the first defect a real-title run
has produced** (`spike-notes.md` §8). The title is running DLSS. Every one of the
**2,461** params-carrying presents in a 40 s / 10,169-present capture decoded to
`FL_UPSCALER_UNKNOWN`.

`UNKNOWN` is the honest failure — `fl_shm.h` allows only `NONE` to be aggregated as
a negative, and the writer correctly never says `NONE`. But exit criterion 1 asks
for the **correct** upscaler, and `Unknown` is not it. **A wrong answer that cannot
be called a lie is still a wrong answer**, and it is the exact shape
`17_HOOK_ENGINE` calls the highest false-confidence risk in the spike, reached from
a direction nobody was watching: not a misspelt symbol, but a correctly-resolved
hook attributing the frame to the wrong feature.

**The mechanism, as far as the data shows.** `RecordPresent` drains `g_slSeen` with
`exchange(0)`, so a record is told about whichever Streamline features evaluated
since the *previous* present. Under multi-frame generation the app frame's
`kFeatureDLSS` evaluation and the presents that carry it are not one-to-one: 10,169
presents carried only 2,461 evaluation batches (×4.13, and the title's settings say
`DLSS_MultiFrameGeneration = x4`). The batches that reached a present held
`DLSS_G` / `DLSS_RR` / `OTHER` and not `kFeatureDLSS`, so the decode fell through to
`UNKNOWN`.

**What is NOT yet established, and must not be assumed while fixing it:**

- Whether `kFeatureDLSS` is evaluated at all in this title, or only `DLSS_G` and
  `DLSS_RR`. Cyberpunk ships `sl.dlss.dll`, so it *should* be — but "should" is what
  this ledger exists to distrust. The identity hook fires; **which** ids it sees was
  never printed.
- Whether the same happens without frame generation. One configuration was measured.
- Whether the fix belongs in the drain (accumulate identity across presents until an
  app frame boundary) or in the decode (prefer a super-resolution id over an FG id
  when several are seen). These produce different answers for a title that switches
  upscaler mid-session.

**Whose it is.** Item 3 restructures this exact drain — `g_slSeen` is a bitmask and
frame generation needs a **count** — so the fix lands there rather than as a
separate change that item 3 would immediately rewrite. **Do not "fix" it by making
the decode prefer DLSS without first measuring which ids arrive**: that would turn
a wrong answer into a confident wrong answer.

> ### The instrument is built. The decision table is below, and it is written BEFORE the run.
>
> **Why it is here rather than in the PR that acts on it.** "Measure, then fix" becomes
> "fix, then justify" the moment the table is written after the numbers are known — and
> nobody can tell the two apart afterwards, least of all the person who did it. So the
> mapping from measurement to action is committed first, in the file that owns the item,
> and the run that follows either lands on a row or does not.
>
> **What produces the input.** `FrameLedger.CaptureHost` now prints a Streamline id census
> (`SlCensus`) over the window the identity hook governs: how many presents drained a batch,
> and of those how many carried `kFeatureDLSS`, `kFeatureNIS`, `kFeatureDLSS_RR`,
> `kFeatureDLSS_G` and an **undecoded** id — the last of which is only separable because
> layout v3's `FL_FEAT_SL_UNDECODED` exists. It also prints `evaluations/batch`, which
> tests item 3's unverified premise with no oracle at all.
>
> | # | What the census says | What it means | The decode change |
> |---|---|---|---|
> | **O1** | `kFeatureDLSS = 0`, `kFeatureDLSS_G > 0`, `UNDECODED = 0` | the title does not route super-resolution through `slEvaluateFeature` at all | **none.** `UNKNOWN` is the true answer and the entry closes as "measured, not a defect". The upscaler fact then needs a different producer, which is its own item |
> | **O2** | `kFeatureDLSS > 0` on some batches, `0` on the batches that also carry `kFeatureDLSS_G` | identity and frame generation arrive in *different* batches | **drain**, not decode: accumulate identity across the presents of one application frame. Do NOT prefer DLSS in the decode — that gives the wrong answer for a title that switches upscaler mid-session, which is the whole reason these two options differ |
> | **O3** | `kFeatureDLSS > 0` on the SAME batches that carry `kFeatureDLSS_G`, yet the record still decoded `UNKNOWN` | the decode is losing an id it was handed | **decode.** The if/else-if chain assigns ONE bit per call and the last writer wins within a batch; make the mapping additive and re-check |
> | **O4** | `UNDECODED > 0` on the batches that decoded `UNKNOWN` | an id we do not map is arriving, and may be the super-resolution one under another name | **neither, yet.** Print the raw `sl::Feature` values before mapping anything — a new id decoded by guess is exactly this entry repeating. That needs a writer change of its own |
> | **O5** | `batches = 0`, or `evaluations/batch` far from 1 | the hook is not seeing what we think it sees | **stop.** Neither fix is safe: `evaluations/batch ≠ 1` falsifies item 3's premise outright and the FG factor is wrong by that factor, so that is the defect to chase first |
>
> ### THE x4 LEG, AND WHAT IT DID AND DID NOT SETTLE — 2026-08-16
>
> **The named rival is dead.** Read at x4, the overlay shows `DLSS 268 | FPS 67`,
> `272 | 68` and `260 | 65` across three instants. "Application frames" predicted the
> second field at ~65; "displayed divided by a fixed 2" predicted ~130. It reads 65-68.
> The prediction was written before the screenshots and it discriminated.
>
> Ratio against ratio, which needs no span and no shared window: ours
> `presents / batch` = 4.0000, the overlay's `DLSS / FPS` = 4.0000. At x2 both read
> 2.0000. Two instruments agreeing on a dimensionless quantity across two multipliers.
>
> **A SECOND CONCLUSION WAS DRAWN AND THEN WITHDRAWN, and the withdrawal is the useful
> part.** All five readings are EXACT integer ratios, which requires the true ratio to
> fall inside about +-1% every time. That looked like proof the overlay DERIVES one field
> from the other, because our own capture had measured an achieved factor of 1.84 rather
> than 2.00 — so the real factor seemed to vary while the overlay never did.
>
> **The 1.84 was ALT-TAB.** The operator switched away from the game during that capture.
> Frame generation stops while the title is unfocused, so the window mixed intervals at
> 2.00 with intervals near 1.00 and averaged 1.84. With that explained, the achieved factor
> is exactly N whenever the game has focus — and an INDEPENDENT counter would then also read
> exact integers every time. The evidence for "it derives" evaporates with its premise.
>
> **So the position is: "independent count" and "correct derivation" both survive, and no
> instrument available here can separate them** — each produces the true application rate
> whenever frame generation meets its configured multiplier, which is always, when focused.
> The premise that a drained Streamline batch equals an application frame is therefore
> CORROBORATED ACROSS TWO MULTIPLIERS AND NOT PROVEN.
>
> **The instrument that would settle it is already named in this repository.** PresentMon
> 2.x's `FrameType` classifies each present as application or generated from ETW, by a
> mechanism that divides by nothing. `15_ROADMAP` item 7 and `03_METRICS` rung 2 both
> pre-committed it, which is why they exist. That is the next measurement, not another
> reading of a present-hook overlay.
>
> ### AND THE ALT-TAB EXPOSED A REAL DEFECT, which is the finding with code attached
>
> `FgWindow` has a uniformity guard built for exactly this — split the window into buckets,
> refuse a factor when one bucket departs from the whole, because "averaging across a
> settings change is the classic way benchmark numbers become meaningless"
> (`03_METRICS`:133). **It cannot see this case.** `BucketsOf` sums `fgEvaluations`, which
> is zero on every record on this route, so every bucket is identical and the check passes
> vacuously. The 1.84 run is the proof: a real session where the number a consumer would
> publish was wrong by 8% and nothing in the report said so.
>
> **Consequences, and they are prerequisites rather than nice-to-haves.** If
> `presents / batch` is ever published as `fg_factor`, it needs a uniformity guard OF ITS
> OWN, keyed on the per-bucket `presents / batch` rather than on `fgEvaluations`. And a
> capture whose validity depends on the window being uniform should be able to say when it
> was not: focus loss is observable in-process, and an unfocused interval is not a
> measurement of the title's performance in any case.

> ### THE REPLACEMENT ORACLE DOES NOT SETTLE IT EITHER — corrected 2026-08-16, before it landed
>
> **A draft of this entry claimed the application-frame premise was measured. It was wrong on
> three counts and is recorded here rather than quietly deleted, because the draft read
> exactly like a result.** Steam's overlay, during a ×2 capture, showed `DLSS 162 | FPS 81`
> against a capture whose own numbers imply 161.7 and 80.8 — "0.2% on both".
>
> 1. **THE TWO AGREEMENTS ARE ONE, BY ALGEBRA.** `presents / batch` was 2.0000 exactly and
>    the overlay's own ratio is 162/81 = 2.0000 exactly, so `batches ÷ 81` and
>    `presents ÷ 162` are forced to the same residual — both −0.182%, to three digits. Two
>    independent checks essentially never do that. This is the defect `SlCensus.cs` was
>    corrected for **the same day** — one quantity read twice and cited as two witnesses —
>    reproduced in prose hours later. Fixing the code did not fix the habit.
> 2. **THE SURVIVING COMPARISON IS CIRCULAR.** The span was derived FROM `Displayed FPS`, so
>    `presents / span` is `Displayed FPS` restated. It compares our present count against
>    another present count. No step in the arithmetic touches an application frame.
> 3. **THE RIVAL HYPOTHESIS PREDICTS THE SAME NUMBER.** If the overlay's `FPS` field is
>    *displayed ÷ 2* rather than a count of application frames, then at ×2 the two are
>    numerically identical. The pre-registered prediction separated "application frames" from
>    "displayed frames" and never enumerated this one, so it was not a discriminating test —
>    and calling the result pre-registered borrowed credibility the design did not have.
>
> **And the oracle is not what the draft called it.** Steam's overlay is not "the game's own
> frame counter" and shares more with us than the draft claimed: `17_HOOK_ENGINE` §Coexistence
> records that RTSS and the Steam overlay **also hook D3D presentation in-process**. Which
> layer it counts at is unmeasured, and this title's present path has at least two — with the
> interposer engaged the swapchain the title holds is not an instance of the class whose
> vtable we patch (§H5). If Steam counts at the interposer's input, "FPS 81" is a present
> count one hook over, and equating it with an application frame is the same unverified step
> relocated.
>
> **THE DISCRIMINATING TEST, and it costs one screenshot.** Read the same overlay at **×4**:
> "application frames" predicts the `FPS` field reads ≈ 65 while "displayed ÷ fixed 2"
> predicts ≈ 130 — a factor of two apart. Then read it with **frame generation OFF**, where a
> genuine application counter converges with the displayed one and a fixed halving does not.
> **Compare RATIOS, not rates:** our `presents / batch` against the overlay's `DLSS / FPS`.
> Both are dimensionless and each is internal to one instrument, so neither needs a span —
> which also sidesteps the window defect below.
>
> **A code gap this exposed.** `FgWindow.Seconds` is computed and never printed, so the span
> over the window the batches were actually counted in cannot be read off a report at all.
> The draft reconstructed it from `Displayed FPS`, which spans a DIFFERENT window —
> `MeasuredFacts` runs from record 0 while `FgWindow` starts after the lazy-install prefix —
> and the resulting rate moves across 78.6–83 on window choice alone, ten times the residual
> the draft quoted. Print it unconditionally beside the other FG counts.

> ### THE PRE-COMMITTED ORACLE IS RETIRED, BY ITS OWN RULE — measured 2026-08-16
>
> `fl-baseline-probe --pid <game> --dir "<game-dir>/bin/x64"` against a running Cyberpunk 2077
> with frame generation ON at ×2 reports **all seven capabilities `loaded`** — `dlss`,
> `dlss_g`, `dlss_rr`, `streamline`, `fsr`, `xess` **and `xefg`**.
>
> **That falsifies it as an FG-engagement oracle in ONE run, without needing the FG-off leg
> the falsifier asked for.** `dlss_g` watches `nvngx_dlssg.dll` / `sl.dlss_g.dll` and `xefg`
> watches `libxess_fg.dll` (`rules/detection-rules.json`) — two **mutually exclusive**
> frame-generation implementations. A title cannot be generating frames with both, and both
> read `loaded`. So `loaded` means "mapped into the address space", which is exactly what the
> probe's own header claims and nothing more; §S30 hoped it would stand in for engagement and
> it cannot, in any configuration.
>
> **The probe is not at fault and its real job is unaffected**: it is the static/loaded
> baseline `15_ROADMAP` item 4 compares against, and it answered that correctly. What is
> retired is the use §S30 pre-committed it for.
>
> **So the app-frame premise still has no independent oracle**, and the one remaining
> candidate is the game's own frame counter beside a capture — the other half of
> `docs/HANDOFF.md`'s "the game's own settings menu and frame counter". Writing the falsifier
> down first is what made this a one-command result instead of a temptation: the output reads
> like confirmation (`dlss_g … loaded`) and is not.

> **The independent oracle, and its own falsifier — also written first.** `fl-baseline-probe`
> against a **running** Cyberpunk 2077 at MFG ×4, ×2 and off answers "is `nvngx_dlssg.dll`
> loaded", which is evidence about FG engagement that shares nothing with the writer under
> test. **If it reports LOADED with MFG off, it is not an oracle for this question** —
> plugin loading happens at `slInit`/feature discovery, not at feature engagement — and it
> must be retired in the same `spike-notes` row rather than quietly relied on. The fallback
> is the game's own frame counter.
>
> **Not in the table on purpose:** a factor of exactly 1.0 must NOT be recorded as §H5 case 3.
> At least three causes produce it and this data cannot separate them — generated presents
> never reaching the vtable we patch, frame generation configured off while the feature is
> still evaluated, and an evaluation that FAILED and was counted anyway, because
> `Hook_SlEvaluateFeature` increments before forwarding and ignores the `sl::Result`. The
> report prints all three rather than naming one.

</details>

### S31 ✅ · Is a drained Streamline batch an application frame? — **YES, measured 2026-09-04 by an oracle that divides by nothing**

> ### RUN 2, 2026-09-04 — row **P1**, and the original question answered on the way
>
> Same instrument as run 1, the monotone-index count from #105. Cyberpunk 2077, three
> launches, and Hell Is Us, one; legs identified by the title's own `presents/batch` where it
> has one:
>
> | leg | presents | tokens | **presents/tokens** | batches | tokens/batch | report |
> |---|---|---|---|---|---|---|
> | Cyberpunk **off** (RR on) | 4,922 | 4,924 | **1.00** | 4,922 | **1.00** | `Presented FPS 84.02` — factor refused at 1.0 by the pre-P1 gate, as designed |
> | Cyberpunk **off** (RR off) | 2,664 | 2,665 | **1.00** | 0 | — | `Presented FPS 45.47`, same refusal |
> | Cyberpunk **×3** | 10,567 | 3,534 | **2.99** | 3,532 | **1.00** | `Native 61.37 → Displayed 183.49 (×2.99 FG)` |
> | Cyberpunk **×4** | 13,706 | 3,439 | **3.99** | 3,437 | **1.00** | `Native 58.74 → Displayed 234.09 (×3.99 FG)` |
> | Hell Is Us **DLSS + FG ×4** | 17,942 | 4,485 | **4.00** | 0 | — | `Native 76.54 → Displayed 306.17 (×4 FG)` |
>
> **P1 word for word:** 1.00 at off, 2.99 / 3.99 at ×3 / ×4, 4.00 on the title that never
> drains a batch. P3 is excluded on the title that would have shown it — the DLSS-G plugin
> does not request tokens for the frames it generates — and the off leg at exactly 1.00
> retires P4. **The producer is accepted.** `fg_factor` is `presents / tokens`.
>
> **And the question this entry was opened for is answered by the same numbers.**
> `tokens/batch` reads **1.00** on every leg that has batches: a drained Streamline batch
> IS an application frame on Cyberpunk 2077 — Ray Reconstruction is evaluated exactly once
> per frame the title asked a token for. Two independent counts, one per API call the title
> makes, agreeing to the frame. That is the oracle four instruments could not provide,
> because it divides by nothing: it is a second count, not a ratio against the first.
>
> **Consequences, taken in the PR that records this:** rung 4's `none` is reachable by
> counting (`FgWindow.IsNone`, factor ≤ 1.05 with a published window); the pre-P1 refusal is
> replaced by a refusal of the band between 1.05 and the cadence threshold, which no vendor
> ships; `fgMode` reads `None` / `Active (technology not identified)` from the count when the
> identity hook named nothing. `14_TESTING`'s struck cross-validation regains an in-project
> oracle for the FG factor: two counts that must agree.
>
> *The entry as it stood while open follows, unchanged.*

### S31 ◐ · Is a drained Streamline batch an application frame? — the ORACLE is retired, the QUESTION is not *(superseded above)*

> ### THE QUESTION IS SIDESTEPPED, NOT ANSWERED — 2026-09-03, the producer route is chosen
>
> Nothing divides by batches any more. HANDOFF item 3's producer is **`slGetNewFrameToken`**:
> Streamline hands a title one frame token per application frame by contract, the Overlay
> counts distinct tokens between presents, and `fg_factor = presents / tokens`. The choice was
> taken **with no oracle behind it**, as the entry required the next PR to say — so this table
> was written before the run, and the run is the owner's: **Cyberpunk 2077 at off / ×2 / ×4
> and Hell Is Us at DLSS + FG ×4, one launch each**, reading `presents/tokens` off the
> `FG counts:` line.
>
> | Row | Reads | Consequence |
> |---|---|---|
> | **P1** | ≈ 1.00 / 2.00 / 4.00 on Cyberpunk, ≈ 4.0 on Hell Is Us | Producer accepted. Rung 4's `none` becomes reachable at ratio ≈ 1 in the following PR; §S31 → ✅ by construction, `14_TESTING`'s cross-validation gate regains an inside-the-project oracle for the FG factor |
> | **P2** | tokens = 0 on a title with frame generation demonstrably on | That title does not call the export from where we can see it. Route dead **there**; N/A stays; record which title |
> | **P3** | ≈ 1 at ×2 and ×4 | The DLSS-G plugin requests a token for every frame it generates. Route dead everywhere; the detour comes out; back to the candidate list |
> | **P4** | The off leg reads ≠ 1 (e.g. 0.5), or the ratios are non-integer and stable | The dedupe premise is wrong (several distinct tokens per frame, or one per several). Refine by `frameIndex` before anything is published; nothing above the cadence threshold is trusted until the off leg reads 1 |
>
> **Until a row lands, the consumer publishes a factor only at ≥ 1.5** (`FgWindow.PublishableFactor`),
> because P3 is the one outcome a ratio near 1 cannot be told apart from, and P3 is exactly
> the shape that would print `300 → 300 (×1.0 FG)` on Hell Is Us.
>
> #### RUN 1, 2026-09-03 evening — row **P4**, and the refinement it prescribed is built
>
> Cyberpunk 2077, three launches, read off the `FG counts:` line (`spike-notes` §9):
>
> | leg (by its own `presents/batch`) | presents | tokens | presents/tokens | tokens/batch |
> |---|---|---|---|---|
> | off (1.00) | 5,199 | 24,059 | **0.22** | 4.63 |
> | ×2 (2.00) | 8,544 | 13,259 | **0.64** | 3.10 |
> | ×4 (3.99) | 15,193 | 11,633 | **1.31** | 3.05 |
> | Hell Is Us, DLSS + FG ×4 | 17,788 | 13,783 | **1.29** | — |
>
> **The off leg reads 0.22, not 1**, and `tokens/batch` reads 3.05–4.63: the title asks for a
> frame token **three to four-and-a-half times per application frame** and the interposer hands
> back a **different object each time**, so the pointer-change count read the request rate,
> not the frame rate. That is P4 word for word — *"several distinct tokens per frame"* — and
> not P3: at ×4 the ratio moved (1.31 against 0.64 at ×2), so tokens are not tracking presents.
> The gate held: **no factor was published** on any leg, because every ratio sat below 1.5.
>
> **Refinement built the same evening:** the detour keys on the frame **index** — the
> title-supplied `frameIndex` when given, else the token's own accessor — and counts it **only
> when it is above every index seen so far**, so a frame asked for N times counts once even when
> the requests for frames N and N+1 arrive interleaved from the two or three threads a title
> keeps in flight ("differs from the last index" would count every switch). A jump backwards
> of more than 1,024 re-bases the maximum, so a level reload that restarts the counter does not
> freeze the count at zero. The stub interposer now hands back distinct objects carrying the same
> index and the harness asks twice per frame, so the K = 1 control fails a pointer-keyed writer.
> **The table above is unchanged and the run is owed again**, same four legs; P1 is still what
> makes `none` reachable.

> ### 🔴 RUN 2026-08-27 — ROW **P2**. PresentMon is RETIRED as the application-frame oracle.
>
> Three legs, three game launches, Cyberpunk 2077, frame generation off / ×2 / ×4.
> `spike-notes` §11 carries the numbers. What belongs here is the row and what it does
> and does not settle.
>
> **`FrameType` is present and every single row of all three legs reads `Application`** —
> 1,937 / 6,488 / 10,881 rows, zero rows of any other value, counted off the raw CSVs
> rather than off the tool's summary.
>
> **And the title was demonstrably generating frames, which is the half P2 needs.** The
> two instruments agree on the present rate to within 0.3% on every leg:
>
> | leg | our hook | PresentMon | our `presents/batch` |
> |---|---|---|---|
> | off | 43.16 /s | 43.04 /s | N/A — no batch drained |
> | ×2 | 144.31 /s | 144.18 /s | **2.00** |
> | ×4 | 241.84 /s | 241.80 /s | **3.99** |
>
> So **both instruments are counting the same present stream**, displayed rate tracks the
> configured multiplier, and at ×4 roughly three presents in four cannot be application
> frames — while PresentMon classifies **100%** of them `Application`. That is P2 word for
> word: *"PresentMon sees the presents but classifies none as generated, while the title is
> demonstrably generating them."*
>
> `tools/frametype-oracle.ps1` reported it as a **falsifier** on all three legs and never as
> a ratio of 1.0, which is the one behaviour that entry was written to guarantee.
>
> #### What is NOT yet excluded, and the one command that excludes it
>
> **Whether `--track_frame_type` was in effect is unmeasured.** If `FrameType` appears in
> `--v2_metrics` output *regardless* of the flag, these legs show PresentMon's default rather
> than an absence of vendor events. The discriminator costs five seconds, no game and no
> capture — run PresentMon **without** the flag and look at the header:
>
> ```
> PresentMon-2.5.1-x64.exe --process_name explorer.exe --v2_metrics \
>     --output_file %TEMP%\pm-noflag.csv --timed 5 --terminate_after_timed --stop_existing_session
> ```
>
> No `FrameType` column there ⇒ the column exists only because of the flag ⇒ the vendor
> emitted nothing. Column present ⇒ these three legs measured a default and this entry needs
> re-reading. **It still needs elevation, so it is the owner's** — attempted unelevated
> 2026-08-27 and refused with the same exit 6 this item has been blocked on from the start.
>
> > **AND THE DISCRIMINATOR WAS RUN, AND PRODUCED NOTHING — 2026-08-27.** The owner ran it
> > from an elevated shell and no CSV appeared anywhere: not at the given path, not in either
> > TEMP, not in the repository. **That is not an answer, and it must not be read as one.**
> > The command named `explorer.exe` as the target, and a process that presents nothing through
> > a swapchain PresentMon tracks produces no file — so "no CSV" and "no `FrameType` column"
> > are indistinguishable here, which is the §Traps entry *"a canary that dies before reaching
> > the gate looks exactly like a canary that worked"*. The command was badly chosen; a target
> > known to present (`hook-harness --hold-presenting`) would have settled it.
> >
> > **It is now moot.** PresentMon was dropped outright on 2026-08-27, so the reason this
> > sub-question would have decided — whether a later attempt with a different invocation was
> > worth anyone's time — no longer has anything to decide about. Recorded rather than deleted
> > so nobody re-runs it believing it is untried.
>> **THE ACTION DOES NOT DEPEND ON IT, WHICH IS WHY THIS ROW LANDS ANYWAY.** Both branches end
> at the same place: either the vendor emits nothing, or the invocation produced no frame-type
> data at all. In both, PresentMon did not answer the question **as invoked**, and an oracle
> that does not answer is retired rather than re-run until it does — the discipline
> `fl-baseline-probe` was retired under. What the discriminator changes is the *reason*, and
> therefore whether a later attempt with a different invocation is worth anyone's time.
>
> #### What P2 retires, and what it leaves exactly where it was
>
> **RETIRED:** PresentMon 2.x `FrameType` as the application-frame oracle for NVIDIA frame
> generation. `03_METRICS` §Frame Generation rung 2 is narrowed accordingly.
>
> **NOT ANSWERED:** *is a drained Streamline batch an application frame?* This entry's actual
> question is untouched. `presents / batch` still reads 2.00 and 3.99 against a title's own
> ×2 and ×4, still on the unverified premise that the work is recorded once per application
> frame, and still unpublishable as `fg_factor` for that reason. **Four oracles have now
> fallen** — `fl-baseline-probe` by its own falsifier, two readings of Steam's overlay (§S30),
> and this one. `HANDOFF` item 3's producer decision goes back to the hook routes, which is
> where P1/P2 said it would go.
>
> #### Two things the run produced that nobody was looking for
>
> **The `off` leg drained ZERO Streamline batches**, where `spike-notes` §8 recorded
> `presents/batch = 1.000` at off. The difference is Ray Reconstruction: §S30 established that
> RR is evaluated once per application frame, so with RR on there are batches even at off. RR
> was not on here. **Consequence for anyone rebuilding this comparison: our side had only TWO
> readable legs, not three** — enable RR if you need the off leg to carry a ratio.
>
> **The CSV's FIRST column is named `Application`** — it holds the process name — while the
> value being counted, in column 9, is also the string `Application`. A parser resolving by
> position, or grepping for the word, counts process names and reports a ratio. The
> *"columns are resolved BY NAME from the header, never by position"* rule was written into
> `tools/frametype-oracle.ps1` before it had ever seen a real CSV, against exactly the
> collision that turned out to be there. Recorded because the rule looked like ordinary care
> when it was written and was load-bearing.

**The question HANDOFF item 3 cannot close without.** `presents / batch` reads
1.000 / 2.000 / 4.000 against Cyberpunk 2077's own off / ×2 / ×4, and a *batch* is
"a present that drained a Streamline evaluation", not an application frame. The two
coincide on that title only because Ray Reconstruction happens to be evaluated once
per application frame there. **Three oracles have been tried and all three fell** —
`fl-baseline-probe` by its own written falsifier, and two readings of Steam's
overlay, which §S30 records at length. PresentMon 2.x's `FrameType` was
pre-committed as the next measurement because it classifies each present from ETW
and divides by nothing.

> **TWO OBSTACLES MEASURED 2026-08-20, BEFORE THE RUN, AND THE SECOND MAY RETIRE THE
> ORACLE OUTRIGHT** (`spike-notes` §11):
>
> 1. **The console binary will not run unelevated here.** Exit 6, *"requires either
>    administrative privileges or to be run by a user in the Performance Log Users
>    user group"*. The account is neither. So this is not "one command beside a
>    capture"; it needs an elevated shell or a group change, and both are the owner's.
>    `PresentMonSharedService` is running as LocalSystem and does **not** help — the
>    console starts its own session.
> 2. **`--track_frame_type` is a BETA option whose own help says it *"requires
>    application and/or driver instrumentation using Intel-PresentMon provider"*.**
>    `FrameType` is therefore a report of events a vendor chose to emit, not a
>    classification of any present. Whether NVIDIA's driver instruments Intel's
>    provider is unmeasured, and it decides whether this oracle exists at all for
>    DLSS-G.
>
> Recorded here rather than discovered mid-run, because the previous oracle's output
> *read like confirmation* and was not.

**What produced the input** *(past tense: the tool was deleted on 2026-08-27 with PresentMon; kept here because the reasoning is why the run could be trusted).* `tools/frametype-oracle.ps1`, run against a PresentMon
2.x CSV and, optionally, a saved CaptureHost report. It compares **two dimensionless
ratios** — PresentMon's `displayed / application` against our `presents / batch` —
which is §S30's own correction applied: comparing *rates* needs a shared span, and
the span is where the previous draft went circular. Each ratio is internal to one
instrument, so neither side needs to know when the other started.

**The parser has never seen a real PresentMon CSV**, for obstacle 1. It resolves
columns by NAME, refuses loudly when `FrameType` is absent, prints the whole
`FrameType` vocabulary it saw rather than assuming one, and refuses rather than
dividing by zero when nothing is spelled `Application`. Its `-SelfTest` decision
table runs in `build.ps1 check` on every build, because that table is the only thing
standing behind an unvalidated parser.

> ### The decision table, written BEFORE the run
>
> Committed first, in the file that owns the item, so the mapping from measurement
> to action cannot be assembled after the numbers are known — the discipline §S30
> used, including the part where its table turned out to have holes and was kept
> unchanged anyway. **Run at THREE settings: frame generation off, ×2 and ×4.**
>
> | # | What the comparison says | What it means | What to do |
> |---|---|---|---|
> | **P1** | No `FrameType` column at all | The beta option produced nothing here: NVIDIA does not instrument Intel's provider, or the build/driver combination does not | **RETIRE IT**, in this row, exactly as `fl-baseline-probe` was retired. `03_METRICS` rung 2 must then be narrowed to the vendors it actually covers, and item 3's producer question goes back to the hook routes |
> | **P2** | `FrameType` present, **every row `Application`** at ×4 | PresentMon sees the presents but classifies none as generated, while the title is demonstrably generating them | **RETIRE IT** as above. The tool reports this as a falsifier and never as a ratio of 1.0 — a 1.0 here is an absence, not a measurement |
> | **P3** | Ratios agree (within ~2%) at **all three** settings | Two instruments sharing no mechanism agree on a dimensionless quantity across three multipliers. **A drained Streamline batch IS an application frame** | `presents / batch` may be published as `fg_factor` with `fg_source = etw-corroborated`, keeping `FgWindow.BatchRefusal`. `03_METRICS` §Frame Generation and `fl_shm.h` both need the promotion written down in the same PR |
> | **P4** | Ratios agree at ×2 **only** | The ×2 leg cannot discriminate — §S30 already burned one draft on exactly this, because "displayed ÷ a fixed 2" predicts the same number there | **Not an answer.** Do not promote. Report the ×4 disagreement as the finding and chase it |
> | **P5** | Ratios disagree at ×4 by more than ~2% | Either a batch is not an application frame, or PresentMon is counting at a different layer | **Stop and localise before deciding.** Both instruments count presents somewhere; §H5 records that this title's present path has at least two layers. Print the vocabulary and the per-setting numbers before touching either side |
> | **P6** | `FrameType` present but the run needed elevation the operator does not want to grant | Nothing was measured | **Not a result.** An unrun leg is unrun; do not infer P1 from it |
>
> **A rival this table must not be read as excluding.** Agreement at three settings
> makes "a batch is an application frame" the best-supported reading; it does not
> make it proven. If PresentMon's `Application` classification were itself derived
> from a vendor event that fires once per Streamline evaluation, the two instruments
> would share a mechanism and agree for that reason. Nothing measured so far
> excludes it, and §S30's whole lesson is that two numbers agreeing can be one number
> read twice.

**Blocks:** HANDOFF item 3's producer decision, `03_METRICS` §Frame Generation rung 2's
scope, and — through `presents / batch` — whether `fg_factor` is ever publishable on a
Streamline title.

### S27 · The chokepoint is the ANTI-CHEAT gate, and it is not the consent gate

Found 2026-08-05 by an adversarial review of the drain host's design, before it was
built. Recorded because the design said `FlGuardedInject` "ONLY (the chokepoint)"
and read as though that covered the whole of CLAUDE.md rule 1. It does not.

`fl_guard_abi.h` says so in its own header: the ABI *"deliberately does NOT carry
per-game consent … This ABI enforces the ANTI-CHEAT gate — the part that protects
accounts — not the opt-in."* `fl_guard.h` adds `kHookNotEnabled` / `kConsentMissing`
/ `kPreviouslyBlocked` and states **"the guard itself never returns these"**. The
only producer is `HookedCaptureGate`, and its three inputs — `hook_enabled`,
`hook_consent_at`, `hook_blocked_reason` — come from a `games` table that **exists
in `06_DATA_MODEL` and in no `.cs` file**.

So the proposed `Agent --diag <pid>`, on a binary `12_BUILD` publishes
self-contained, would have loaded the Overlay into any x64 process a user named on
a command line: no game record, no consent stamp, no enablement, and the
`19_SAFETY` disclosure never shown. Rule 1's "never automatic", automatically. Not
an anti-cheat bypass — every anti-cheat check still runs — but a **consent and
disclosure gap in a shipped binary**, which is its own class.

**Two tempting fixes were rejected.**

- *Route it through `HookedCaptureGate` with `HookEnabled = true, ConsentedAt =
  UtcNow` synthesised.* It compiles. It is a gate whose verdict is decided before
  it looks — this file's signature defect, wearing the gate's own name.
- *Build it as a native `fl-session-probe` instead.* Strictly worse: a native tool
  structurally cannot read `games.hook_consent_at`, so the opt-in gate could not
  exist inside it at all, and it is then §S9's user-runnable injector renamed.

**What was built instead.** `ShmDrainIntegrationTests` — nothing packages it, and
the target is `hook-harness`, our own dummy D3D app built from this tree, carrying
no anti-cheat and belonging to no publisher. Injecting into it raises no consent
question: no game, no account, no terms of service. The test asserts that
constraint **on itself**, so it cannot grow into something that injects elsewhere
by increments.

~~**The real gate arrives with the `games` table, not before.** Until then there is
no injecting entry point on any shipped binary, and that is the correct state
rather than a missing feature.~~

> **Restated 2026-08-06, because item 1 built the thing this entry was closed by NOT
> building — and the ✅ survives on a different sentence.**
>
> The load-bearing clause was never "the `games` table"; it was **"no injecting entry
> point on any SHIPPED binary"**. `FrameLedger.CaptureHost` is an injecting entry point,
> and it is not shipped: `12_BUILD` publishes exactly `FrameLedger.App` and
> `FrameLedger.Agent`, and neither references it.
>
> **That is now a gate rather than a fact about today's reference graph.**
> `tools/package-closure-check.ps1` walks the transitive `ProjectReference` closure of
> both publish roots and fails on anything outside the allowlist, naming the edge —
> proven by planting a reference from the Agent, which leaves the build green and turns
> the gate red. Without it, §S27's ✅ would have depended on nobody adding a line to a
> csproj.
>
> **Two more things hold it, and both came from a refuter over the design rather than
> from the build:**
>
> - **The consent inputs are no longer synthesisable.** This entry rejected
>   `HookEnabled = true, ConsentedAt = UtcNow` as *"a gate whose verdict is decided
>   before it looks"* — and `HookRequest` was a `record` with `init` members, so that
>   expression compiled from anywhere and reached `GuardedInjectAsync`. A store and a
>   provenance flag only add an honest path beside it. It is now get-only behind a
>   private constructor with `FromConsent` as the sole entry, so the rejected expression
>   is a compile error.
> - **`GameConsentRecord.Stored` is `internal`.** `FrameLedger.Domain` is inside both
>   publish closures, so a public minting factory there would be a blessed *shipped* API
>   for producing consent nobody gave — and the closure gate structurally cannot see it,
>   because Domain legitimately belongs to both. The `InternalsVisibleTo` list is the
>   reviewable artifact.
>
> **What the record does NOT claim.** `19_SAFETY` requires the timestamp *"stamped by the
> Agent, never supplied by a client"*, and a file on disk is by construction supplied by
> whoever can write it. That property is **not** upheld and `04_CAPTURE` now says so
> rather than implying otherwise; what stands in for it is a build-tree file belonging to
> an unshipped binary, a provenance whose default means no disclosure was shown, and an
> `en`-only operator acknowledgement that states in its first line that it is not FR-2.1
> consent. The real gate still arrives with the `games` table.

> Also settled by the same review, and worth keeping where the next reader will
> look: **`--diag` is already taken.** `10_LOGGING_AND_BUG_REPORTS` assigns it to
> the App as a stdout capability report while `12_BUILD` and `Program.cs` list it
> as an Agent flag. Whatever the eventual capture flag is called, it is not that.

> **Restated 2026-09-09 (P2 PR-B), because the basis moved.** The 2026-08-06 restatement held the ✅
> on *"no injecting entry point on any SHIPPED binary"* and on the consent adapter living in the
> unshipped host. Neither is true now: `Infrastructure.Persistence.SqliteGameConsentStore` — the
> adapter `04_CAPTURE` said P2 would write — SHIPS in both publish roots, and `GameConsentRecord.Stored`'s
> `InternalsVisibleTo` list names `FrameLedger.Infrastructure` (the "deliberate, reviewable act"
> `Domain.csproj` asked for). PR-C/PR-F then make the Agent the injecting entry point by design, which
> `01_ARCHITECTURE` always said it was. **What holds rule 1 from here is not packaging.** It is that
> the only producers of an acknowledgement are a disclosure shown to a human — the unshipped host's
> verb today, the Agent's console verb (HANDOFF §P2 decision D4), the UI's dialog (P3) — that the
> provenance is recorded by NAME and an unknown one reads as `NotRecorded`, that `HookRequest.FromConsent`
> stays the gate's only producer, and that every anti-cheat check still runs afterwards.
> `package-closure-check` keeps its meaning for the CaptureHost only. **The owner re-ratifies this
> and §S18 blocker 3 together** (HANDOFF §Owner-only item 5); until then the ✅ stands on the four
> properties above, stated, rather than on the one that no longer holds.

> **Restated again 2026-09-09 (P2 PR-C): the loop ships too.** `CaptureLoop` — the code that has
> reached `FlGuardedInject` on every hooked session since 2026-08-06 — is now
> `Application.Capture.CaptureSession`, in both publish closures, with `Infrastructure.Capture`
> holding the adapters (`ShmRingAttacher`, `HeldProcessLivenessSource`, `TargetResolver`,
> `ProcessLauncher`). Nothing in the Agent calls it yet (PR-F), so today's shipped binaries still
> carry no injecting *entry point*; what they carry is the injecting *code path*, and that is by
> design (`01_ARCHITECTURE`). The four properties above are what hold, and PR-C adds two pins:
> `HookRequestSoleProducerTests` (reflection: no public constructor, no setter, exactly one factory)
> and `NoSecondRingReaderTests` (nothing but `ShmRingReader` opens the ring). The resolver still has
> no pid parameter — `TargetResolver.Resolve(normalisedExePath)` is the only way to a pid, and the
> gate is the only way from a pid to the guard.

> **Restated 2026-09-10 (P2 PR-F): the Agent is the injecting entry point now, by design.**
> `FrameLedger.Agent --serve` watches the `games` table and records a session per tracked process;
> `--console capture | launch` do it on an operator's verb; both reach `FlGuardedInject` through
> `SessionRecorder → CaptureSession → HookedCaptureGate` and nothing else. What holds rule 1 is what the
> 2026-09-09 restatement said it would be: the only producers of an acknowledgement are a disclosure shown
> to a human — now including the Agent's own console (`ConsentProvenance.AgentConsoleOperator`, decision D4,
> stamped by the Agent's clock, refusing redirected stdin), the provenance is recorded by NAME and travels
> with the acknowledgement (a nameless one is refused by the store), `HookRequest.FromConsent` stays the
> gate's only producer — with a FOURTH input, FR-2.4's kill switch (decision D7), which can only refuse —
> and every anti-cheat check still runs afterwards. The command line is pinned
> (`AgentCommandLineSurfaceTests`: no pid, no payload, no override; `--data-dir` only under `--console`).
> `package-closure-check` keeps its meaning for the CaptureHost only. **The owner's re-ratification of this
> and §S18 blocker 3 is still HANDOFF §Owner-only item 5**; the ✅ stands on the properties above.

### S25 ✅ · Both runtime stops were unreachable in a non-presenting process, and pause was unreachable on a ticking one — **closed 2026-08-05**

Found by tracing call paths while planning the next phase, in code merged the
same day (#43). Two defects, one root cause: **`MayObserve()` had exactly one
caller.** `RecordPresent` calls it, and only `Hook_Present` and `Hook_Present1`
call that. `Hook_ResizeBuffers` does not.

**(a) Neither stop fired in a process that had stopped presenting.** A game that
has hung, been alt-tabbed, or is sitting in a menu makes no present calls, so
`unhookRequested` was never read and the `guardTicks` deadline was never
evaluated. The hooks stayed patched in for the life of the process.

`fl_shm.h` states over `FL_GUARD_TICK_DEADLINE_MS`, in capitals, that this must
not be driven by the present hook — *"the clock would stop when presents stop,
which is the exact scenario this exists for — a game that has hung, or been
alt-tabbed, while anti-cheat loads behind it"* — and §S2 rejected a
present-driven re-scan for that reason a day earlier. **A normative comment
prescribing the opposite of the code beneath it**, which is the same shape as the
`MoveFileEx`/`ReplaceFileW` prescription §S21 records: a comment that names a
mechanism is a design somebody builds against.

Be precise about the exposure rather than overstating it: a process that is not
presenting is also not *recording*, so nothing false was written. What failed is
`19_SAFETY` §During a session's clean unhook on detection, and the promise
`legal/DISCLAIMER.md` §2 makes to the user in its own words.

**Measured, both directions.** `unhookRequested = 1` against a live injected
`hook-harness --hold` target (240 presents, then sleep): `status` stayed
`FL_STATUS_READY` through 10 s of polling. After the fix it reaches
`FL_STATUS_UNHOOKED`. ctest `fl_guard`, *"the safety stop fires in a target that
has STOPPED presenting"*.

**(b) `pauseRequested` was unreachable on any frame where `guardTicks` changed.**
The tick-freshness check sat between the safety stop and the pause check and
`return`ed `true` as soon as the tick differed from its cached value — so the
first present after every guard evaluation was recorded regardless of pause.

**Measured: 12 leaked records across 12 guard ticks, exactly one per tick**
(`writeIndex` 9 → 21 while paused). Each carries a `qpc` ~30 s after its
predecessor at the real re-scan cadence, which is a **fabricated 30-second frame
interval** in the series `03_METRICS` computes 1% and 0.1% lows from — the exact
artefact `07_IPC:114-119` forbids for torn records. Latent only because nothing
writes `pauseRequested` yet.

**Closed by a watchdog thread** (`17_HOOK_ENGINE` §The watchdog thread), which
takes the deadline off the present path entirely and thereby fixes (b) by
construction — there is no early return left to jump over the pause check. The
present path keeps its `unhookRequested` check, because `07_IPC` requires that
within one frame and a one-second watchdog cannot promise it. `StopObserving` is
now a compare-exchange, since two threads can reach it and `MH_DisableHook` must
run once; the **first** reason wins, so a self-disable is not overwritten by a
safety stop arriving a millisecond later.

**One thing this did NOT close, stated rather than left to look covered.** The
same trace found that `NoteFault` called `MH_DisableHook`, **discarded its return
value**, and stored `SELF_DISABLED` unconditionally — while setting no
`g_observing`, the flag whose own canary comment calls it *"the only thing that
holds if `MH_DisableHook` ever fails"*. That claim was true of the safety stop and
false of the fault policy. It is now routed through `StopObserving`, **but there
is still no fault-policy test at all**, so the fix has no regression net. The
vehicle is the blocker, not the assertions; `src/native/tests/CMakeLists.txt`
records both rejected approaches and why.

### S2 ✅ · The Vulkan layer has no guard — **both halves done; part three built 2026-09-06 (P1 item 3)**

An implicit layer is machine-wide and loads **before** anything of ours runs, so
the injection guard cannot cover it: no module scan, no driver scan, no
blocklist, no multiplayer heuristic. And `19_SAFETY` §During a session — "the
single most important runtime behavior in the whole capture layer" — is
Agent-driven and Overlay-targeted; with the Agent not running there is no
runtime guard inside a layered process at all.

**✅ Half one, done and measured** (`spike-notes.md` §2, `17_HOOK_ENGINE`
§Vulkan). `enable_environment` is in the manifest and verified against loader
1.4.357: with the variable unset, the loader locates the manifest and never maps
the DLL — and it compares the variable's *value*, so a stray `=0` does not
enable us. The cost is accepted: **Vulkan Tier 1 is now launch-mode-only.**
`tools/vklayer-blastradius.ps1` runs the check and unregisters in a `finally`.

> **That cost read larger than it was.** Recorded 2026-08-03: launch mode was
> blocked by §S18, and §S1 does *not* cover the Vulkan path (no injection, no
> suspended target), so §S18 alone blocked every Vulkan Tier-1 session — which is
> why it was not merely "launch mode, which is blocked anyway".
>
> **§S18 closed 2026-08-04.** The remaining blocker is the layer's own
> `vkQueuePresentKHR`, P1 and not started.

> **The measurement also killed the design that looked obvious.** Declining
> `vkNegotiateLoaderLayerInterfaceVersion` for a process that did not opt in
> does *not* make the loader skip the layer — it access-violates the host
> application. That would have crashed every Vulkan program on the machine
> outside our enable-list. The layer now always accepts and always forwards;
> the enable-list decides what we *intercept*, never whether we *load*.

**✅ Half two, done.** The layer scans its OWN process against the blocklist
at init and goes fully passthrough on any hit, using the SAME matcher and the
SAME rules file as the injection guard (`fl_ac_rules.h`, compiled into both). A
layer with its own blocklist would be a second matcher that can disagree with
the first, which is the defect the managed facade was built to avoid.

Every uncertainty resolves to inert: rules unreadable, malformed or incomplete,
module enumeration failed, a truncated list, a module that could not be named,
or an actual hit. That is the opposite *polarity* from the injection guard —
where an unknown means refuse to inject — but the same principle: do the thing
that leaves the host alone.

Verified by `fl-probe-vklayer` (ctest `fl_vklayer_selfscan`), which asserts
**both** directions — a clean process is *not* forced inert, and a process
carrying a planted module *is*. The planted module is our own DLL copied under a
blocklisted name, per `14_TESTING` §Integration tests; no real anti-cheat
software is shipped, downloaded or executed. Proven red by making the matcher
stop matching.

The probe installs the repository's seed rules to the product's one rules
location when nothing is there, and removes them afterwards. That is deliberate:
there is no way to point the layer at a different rules file (§S3), so without
it the test silently skipped on any machine that had not run the product — and a
ctest that always skips is a gate that cannot fail.

### ~~◐~~ ✅ Part three: mid-session guard inside a layered process — decided, ~~half built~~ built 2026-09-06 (heading corrected 2026-10-04)

> #### ✅ Built 2026-09-06 (P1 item 3), in the PR that added the present hook — as the deferral below said it would be
>
> The layer intercepts `vkQueuePresentKHR` (+ `vkCreateSwapchainKHR` / `vkDestroySwapchainKHR`), owns the
> same ring the Overlay would (`fl_shm_host.h`, one per process) and runs the in-layer check **on the
> present path**: `unhookRequested` → `UNHOOKED` within one present; `guardTicks` not advancing for the
> 07_IPC deadline → `UNHOOKED`; `pauseRequested` → no record, still forwarded. "Stops" is passthrough forever
> with the reason on the mapping. Driven end to end by a fake loader chain (ctest `fl_vklayer`), the deadline
> proven with a 1.5 s test-only flavour of the DLL (`fl_vklayer_supervision`: ticks every 600 ms keep it
> alive past the deadline, 1.8 s without one stops it), and the real loader + the harness's `--vulkan` mode
> on the dev box (`fl_vklayer_real`, 241 records in 4 s; skips on CI, which has no loader). `17_HOOK_ENGINE`
> §Vulkan carries the shape. **The residual below is unchanged** and the Disclaimer's wording with it.
>
> #### 🅓 Deferred to P1, rationale written 2026-09-06
>
> The decision is taken (below: supervision is the Agent's job; a layer that cannot confirm
> supervision goes inert) and half of it is built. The other half is the in-layer check that
> reads `guardTicks` — and it has nothing to run on until `vkQueuePresentKHR` is intercepted,
> which is P1 (`15_ROADMAP` §P1: *"Vulkan layer to parity — `vkQueuePresentKHR` plus the
> in-layer supervision check §S2 gates on it"*). Building the predicate now would be one
> whose wrong answer changes nothing observable, the defect class this file records. Cost of
> waiting: none a user can see — the layer intercepts nothing, so there is no Vulkan capture
> to stop.

**The mechanism is decided.** The layer does NOT re-scan on its own after init.
Four options were pressure-tested (2026-08-02); the reasons the other three lost
are worth keeping, because each looks reasonable until costed:

- **A layer-owned worker thread** re-running the self-scan on a timer. Repeating
  a ~1.15 MB transient allocation and a full module enumeration every 30 s inside
  a game, against an 8 MB total budget — and, if the driver half were added,
  `NtQuerySystemInformation` plus SCM probing *from inside a game process*, which
  is the behavioural signature of anti-analysis code. CLAUDE.md rule 3 forbids
  hiding from anti-cheat; looking like the thing you are trying not to be
  mistaken for is the opposite failure. Also: the Vulkan loader owns the layer's
  mapping, and a thread outliving it is an access violation in a host we do not
  own — the same shape as the crash already measured in §S2.
- **Driving the re-scan from the present hook.** Rejected outright. It violates
  CLAUDE.md rule 5 structurally; it fails NFR-1 worst exactly where the user has
  least frame budget; and — decisively — **the clock stops when presents stop**,
  which is the scenario the requirement was written for. It would also inject a
  periodic stall into the frame-time series this product exists to measure.
- **Dropping Vulkan to Tier 2.** Not fatal to the product, but the stated form is
  false — ~~Tier 2 needs an elevated Agent~~ **and as of 2026-08-28 the premise is dead
  while the conclusion gets STRONGER: Tier 2 needs no elevation and measures nothing,
  so dropping Vulkan there is not a degradation, it is a removal.** For most users the real proposition
  is "duration and sensors only".

**What is decided:** supervision is the Agent's job, and a capture side that
cannot confirm supervision stops observing. That is the same polarity the rest of
the layer already has — every uncertainty resolves to inert.

> **"Publishes" is wrong, and the ✅ covers less than it reads as.** Corrected
> 2026-08-04. `GuardSupervisor` **counts** completed evaluations in a private
> setter; nothing writes `FlControlBlock.guardTicks`, because nothing in either
> language maps the shared memory at all (`grep guardTicks` over `src` and
> `tests` returns comments, a declaration, a `static_assert` and the layout
> dump). It also has **no production call site** — `Program.cs` seeds the rules
> file and exits. What is built and tested is the *counting discipline*: the tick
> counts evaluations rather than seconds. What is not built is the supervision.
> A reader ticking this off as delivered would then find `07_IPC` §Supervision
> loss unimplementable, which is exactly what happened. *(Overtaken in P2: `ShmRingReader` publishes the tick from
> `GuardSupervisor`, which `CaptureSession` runs on every Tier-1 session. Marked 2026-10-04.)*

**✅ The Agent half's counting discipline is built and proven.** `GuardSupervisor`
(`FrameLedger.Application`) computes what will become `guardTicks`, and the load-bearing property
is tested: **the tick counts completed evaluations, not seconds.** A timer-driven
heartbeat attests that the Agent *process* is alive while the guard loop can be
dead — a swallowed exception, a blocked service query, a stall on one unreadable
process in the §S16 scan set — and the capture side would then keep observing
*because* the thing supervising it had stopped. Seven tests force each of those.
`07_IPC` and `fl_shm.h` are corrected, and so is the polarity at the other end:
"the Overlay keeps writing (harmless)" on heartbeat loss described an
unsupervised hooked process as harmless.

**~~◐ The in-layer half is deliberately NOT built yet, and this stays open.~~ ✅ Built 2026-09-06, see the top of this part.**
The layer intercepts nothing — `vkQueuePresentKHR` is P1 — so a `ShouldObserve()`
today would be a predicate whose wrong answer *in either direction* changes
nothing observable. That is a gate on something that does not exist, which is the
defect class this file keeps recording. It lands in the PR that adds the present
hook, where a fake loader chain can drive it end to end. *(It did: `MayObserve()` in
`layer.cpp`, ctest `fl_vklayer` / `fl_vklayer_supervision`.)*

**Residual, to state rather than discover:** even once built, the mid-session
*driver* case is invisible from inside a layered process, and a layer cannot
leave a running game's loader chain — "stops" means passthrough, not unhook.
`legal/DISCLAIMER.md` and `README.md` now say so.

### S3 ✅ · `UpdateRules { path }` — **closed, and it was not alone**

Fixed in `07_IPC` §Messages, with the reasoning kept in a new §The pipe is not a
trust boundary so the conveniences are not reintroduced.

`UpdateRules` is now a bare trigger — the rules *source* is no longer a
parameter. Auditing the rest of the message table for the same shape found two
more, neither previously recorded:

- **`SetHookEnabled` took `consentAt` from the client.** The per-game informed
  consent FR-2.1 and `19_SAFETY` make the basis of every injection was
  client-asserted and forgeable. The Agent now stamps it.
- **`SetWatchlist` carried `hookEnabled` with no pre-scan**, while the adjacent
  `SetHookEnabled` re-scans and may reply `Refused` — and the UI re-sends the
  full watchlist on every connect, so a value forced once would be re-asserted
  forever. It now carries identity only.

The rule that generalises them, now written down: **no inbound message may
assert a safety fact.** It may request a state change; the Agent establishes the
fact itself.

### S5 ✅ · `detection-rules.json` schema — **closed**

`rules/detection-rules.schema.json` (JSON Schema 2020-12) now fixes the shape,
and `tools/rules-validate.ps1` enforces it. The seed gained the representations
the `19_SAFETY` blocklist table needed and the abbreviated shape in
`05_DETECTION` could not express: `directories`, `services`, `files`, and a
`heuristic` block for the unknown-but-suspicious rule.

**`Test-Json` fails OPEN on a malformed schema** — measured on PowerShell 7.6.4,
`-Schema '{'` returns `$true` while writing a parse error to the error stream.
A truncated schema file would therefore make every rules file "valid", including
one with an empty blocklist. The validator now proves the schema is
*discriminating* before trusting it: a canary document that must fail is checked
first, and if the canary passes we refuse rather than report success.

Still imperative, because a schema cannot express them: required families still
present, no case-insensitive duplicate values, no prefix so short it would shadow
a system DLL.

**Two families remain unrepresented in the data** — Activision Ricochet (driver
and service names unconfirmed) and Valve VAC (needs `blockedStoreIds`). Recorded
in the seed's own `$comment`. ~~Ricochet~~ **has data since 2026-09-25** (`randgrid.sys`,
`atvi-randgrid_sr`/`_msstore`, from Activision's support pages — documented, unmeasured; `19_SAFETY`
§Blocklist seed). VAC still has none. The `heuristic.trustedSigners` list is a guess and
is marked UNVERIFIED.

### S13(c) · Is launch-mode injection salvageable?

> **(a) and (b) are decided (2026-08-02) and no longer open.**
>
> **(a) The guard stays in the C++ `FrameLedger.Injector`,** as `19_SAFETY` §The
> anti-cheat guard and CLAUDE.md §Solution layout always said. The managed
> proposal is rejected. Four consequences travel with that choice and are
> tracked as §S15 rather than left implicit — each is a fail-open or a dead test
> if forgotten.
>
> **(b) No clearance escapes the guard.** The guard **owns the chokepoint**: it
> collects evidence, matches, and calls the injection primitive itself. A token
> that escapes can be ignored — a caller can decline to ask for one — whereas a
> primitive with no reachable symbol cannot be called at all. In C++ that is
> internal linkage in the guard's own translation unit, which is a stronger
> mechanism than the C# accessibility trick §S8 disproved. It also deletes the
> staleness question: mint-to-inject shrinks to nothing because nothing is
> minted.

A `CREATE_SUSPENDED` target has loaded almost nothing — **measured**: the module
scan does not merely come back thin, `EnumProcessModulesEx` *fails* with
`ERROR_PARTIAL_COPY` (§S1, `spike-notes.md` §1). The originally proposed "defer
hooks until first present" gate is also circular, because the ring handshake that
would signal first-present is published by the Overlay *after* injection.

An externally observable proxy is needed instead — e.g. resume, then poll until
the target has mapped a presentation runtime (`dxgi.dll` plus `d3d11`/`d3d12`, or
`opengl32`, or `vulkan-1`) *and* the scan passes *and* the blocklist is clean.
Decide whether that is acceptable, or whether launch-mode injection is dropped.

**The one input still missing** is how much early-init data injecting late
actually costs. It needs a title that loads a presentation runtime lazily; the
local fixtures (`hook-harness`, which creates D3D at startup, and a 2D GOG
prologue) cannot produce a figure that generalises.

> **🅓 Deferred 2026-08-05 by owner decision — the rationale is written once, under
> §S1**, because this item and §S1 turn on the same missing measurement and two
> copies of a rationale is how they drift apart. §S24 lists both as deferred.
>
> Note what the deferral does **not** touch: the externally observable proxy this
> entry proposes (resume, then poll until the target has mapped a presentation
> runtime *and* the scan passes *and* the blocklist is clean) remains the only
> candidate design for "inject late". It is unbuilt, not rejected.
>
> > **✅ Built 2026-09-06 (P1 item 2), exactly that proxy** — `FlGuardedInjectWhenReady`, §S1 carries
> > the mechanism and the measurements. "Salvageable" is answered yes; the cost of injecting late is
> > reported by the session itself (`dxgiPresentsBeforeHook`, and the wait).

### S15 ✅ · The four consequences of putting the guard in C++ (§S13(a)) — **closed**

Not questions — commitments the §S13(a) decision creates. **All four are done**,
each with the tests that keep it true listed below.

> **This section said "Three of four are done; item 1 is the one still open"
> while every one of its four bullets said DONE, item 1 included.** Corrected
> 2026-08-04. Recorded rather than silently fixed because it is this file's own
> recurring defect wearing a different costume: a status line whose verdict was
> decided before anyone read the list under it. The cost was real — a phase
> planned against this ledger over-scopes, and §S15 was carried into the
> 2026-08-04 next-phase review as open work.

- **1 — one matcher, not two: DONE.** `FrameLedger.Guard.dll` exposes a C ABI;
  `Infrastructure`'s `NativeAntiCheatGuard` is a thin P/Invoke facade over it,
  and `Application`'s `IAntiCheatGuard` exposes only the two questions the guard
  answers — no rules, no blocklist, no evidence. `04_CAPTURE` and
  `01_ARCHITECTURE` are corrected. Two tests keep it true: one asserts no
  managed type carries a blocklist token and the port accepts no evidence, the
  other asserts the managed `AntiCheatRefusalReason` has not drifted from
  `fl::guard::Reason` by reading every name back through the ABI.

  Three things that came out of building it:

  - **The guard DLL is loaded by absolute path**, via a
    `NativeLibrary.SetDllImportResolver`, and never by search. A planted
    `FrameLedger.Guard.dll` earlier on the probe order would replace the entire
    gate with whatever an attacker wanted it to say — a worse outcome than any
    other DLL-hijack in this application. CA5393 rejects `ApplicationDirectory`
    for exactly that reason, and no "safe" search path fits a DLL of our own,
    so the search path is not merely restricted but never consulted.
  - **A default-constructed `AntiCheatVerdict` must not read as permission.**
    The native `Verdict` gets a member initialiser; a C# struct zeroes every
    field and `Allow` is 0 — which it must stay, because it mirrors the native
    enum. The verdict therefore records whether it came from an evaluation at
    all: a value nobody assigned has evaluated nothing and permits nothing.
  - **`FrameLedger.Application` shadows `System.Windows.Application`** inside
    `FrameLedger.App`, because a sibling namespace beats a `using`. The WPF
    `App` class now says `System.Windows.Application` in full.
- **2 — Catch2: DONE.** `src/native/tests/guard_test.cpp`, ctest `fl_guard`.
  The seams are `fl::guard::Sources`, plain function pointers so the guard
  allocates nothing, and every failure in `14_TESTING`'s matrix is forced
  through a fake rather than hoped for.
- **3 — C++ JSON: DONE.** jsmn, pinned by commit. Chosen for what it does not
  do: no allocation, no exceptions, no recursion beyond a token array we own,
  and failure reported as a return code. Every parse failure is a REFUSE with a
  distinct reason.
- **4 — the override mechanism: DONE, and now enforced.** `14_TESTING` is
  rewritten, and `tools/chokepoint-check.ps1` fails the build if any translation
  unit other than the guard's names the injection primitive *or* the Win32 calls
  that constitute injection. Proven red both ways.

> **The injection primitive is now real**, in the order CLAUDE.md rule 2
> requires: the guard and its full matrix landed first, then the primitive.
> `VirtualAllocEx` + `WriteProcessMemory` + `CreateRemoteThread` on documented
> `LoadLibraryW`, verified end to end against `hook-harness --hold`.
>
> Hardening that came out of writing it: **the evidence seam is compiled out of
> everything that ships.** `GuardedInject` and `Evaluate` take no `Sources` and
> always use `SystemSources()`; the injectable versions exist only under
> `FL_GUARD_TESTABLE`, which only `src/native/tests` defines. The guard sources
> are compiled *into* the test binary rather than linked from the static lib, so
> `FrameLedger.Injector.lib` contains **zero** `WithSources` symbols — verified
> with `dumpbin`, not assumed. While the primitive was a stub a caller passing
> all-clean fakes was theoretical; the moment injection became real it would
> have been a route into a game process that consulted no genuine signal at all.

1. **One matcher, not two.** `04_CAPTURE` §The guard writes
   `AntiCheatGuard.Check(pid)` and `01_ARCHITECTURE` draws the guard inside the
   Agent box, while `19_SAFETY` says "implemented in `FrameLedger.Injector` and
   re-checked by the Agent". The managed side must be documented as a **P/Invoke
   facade over the single native implementation**, living in `Infrastructure`
   (CLAUDE.md: the native layer is reachable only through `Infrastructure`). Two
   blocklist matchers that can disagree is a fail-open by construction.
2. **Catch2 is now a prerequisite, not a P1 nicety.** `14_TESTING` §Safety-guard
   tests requires forcing `EnumProcessModulesEx` failure, a partial module list
   and an unreadable process — all native tests now.
   `src/native/tests/CMakeLists.txt` still defers Catch2 to P1, and the guard
   needs seams (injectable enumerator function pointers) before those failures
   are forceable at all.
3. **The Injector must parse `detection-rules.json` in C++.** Record the parser
   choice, pinned by commit like MinHook, and the exceptions policy for that
   target — `-D_HAS_EXCEPTIONS=0` is Overlay-specific and the guard is *not* a
   hook path, so STL and allocation are fine there. Parse failure ⇒ **REFUSE**.
4. **`14_TESTING`'s absence-of-override mechanism must be rewritten.** It
   currently specifies a C# `sealed` token type that §S8 disproved by compiling.
   The replacement is (b) above plus a build-time check that no translation unit
   other than the guard's references the injection primitive.

### S16 ✅ · *Which* process does the guard scan? — **closed: the game's own subtree**

Decided 2026-08-02, specified in `19_SAFETY` §Pre-injection checks item 1.

**The injection target, its descendants, and its ancestors up to but excluding
the first known platform launcher.** Neither obvious reading survived: the
presenter alone misses a game launcher that initialises anti-cheat before
spawning the renderer, and unbounded ancestors would scan `steam.exe` — which
loads VAC modules — and so refuse every Steam title. A gate that refuses
everything is not a strict gate but a broken one, and it is how a user ends up
looking for the override CLAUDE.md rule 2 says does not exist.

A hit anywhere in the set refuses; a process in the set that cannot be inspected
refuses too. Sibling *services* are covered by name via the rules data rather
than by tree walking, which is more reliable. The runtime re-scan recomputes the
set rather than caching it.

**Amended 2026-10-06 (D51, beta.15).** The set's parent links come from a snapshot that links by pid alone, and pids
are reused: the walk followed a launcher's pid after the launcher had exited and something else had taken it, and a
cycle of reused pids was walked round. A link is now cut only when creation times prove it wrong (the child created
before its named parent); an unread time keeps it, and each pid is walked once. `19_SAFETY` §Pre-injection checks item 1
carries the rule and its one residual — a clock set backwards between a parent's start and its child's.

<details><summary>The question as originally recorded</summary>

Found 2026-08-02. Not previously recorded anywhere, and it changes the guard's
own signature, so it belongs before the guard is written rather than after.

`04_CAPTURE` §The guard is `AntiCheatGuard.Check(pid)` — **singular**. But
`04_CAPTURE` §Process watcher says the capture target is "the descendant that
actually presents", elected from a ppid tree, and in attach mode elected only
after we see a ring handshake or count presents. So the guard is specified
against one pid while the subject of the capture is a *tree* whose presenting
member is identified later.

That matters because **anti-cheat frequently does not live in the presenting
process.** A launcher starts, loads the anti-cheat, and spawns the renderer as a
child; or a sibling service process holds it. Scanning only the elected
presenter would miss exactly that arrangement — and it is a common one, not an
exotic one.

**Needs a decision, and it is not obviously "scan everything":**

- Scanning the whole tree makes the guard stricter but raises the false-refusal
  rate, since a launcher may legitimately carry components a game process never
  loads.
- Scanning the presenter only is the narrow reading and is what the current
  signature implies.
- Whatever is chosen, the *machine-wide* driver check (check 2) already covers
  the worst case independently of process identity, which lowers the stakes but
  does not answer the question.

Also unspecified: what the guard does when the tree changes mid-session
(level-transition relaunches re-elect the presenting pid — `04_CAPTURE`
§Process watcher), and whether a newly appearing sibling is re-scanned.

</details>

### S17 ✅ · The schema accepted rules files the guard refuses to parse — **closed**

Found 2026-08-03 while scoping P0 item 3. Recorded rather than fixed silently,
because it was live in shipped artifacts and the shape recurs.

`rules/detection-rules.schema.json` and `fl_ac_rules.h` bounded the same things
with different numbers, and **the schema was looser in every case**. That matters
more than it sounds: an over-cap entry does not drop the entry. `ReadFamily`
returns false, `ParseRules` returns `kMalformed`, and `19_SAFETY` turns an
unparseable rules file into **REFUSE — for every title on the machine**. Rules
ship as updatable data pushed to every client and `tools/rules-validate.ps1`
validated against the *loose* schema, so a CI-green rules edit could have taken
the product out in the field.

| Bound | Schema said | Parser accepts |
|---|---|---|
| `blockedExecutables` / `blockedStoreIds` element | **object** | **bare string** — shape, not size |
| values per family | 64 | 16 (`kMaxValuesPerFamily`) |
| token length | 128 | 95 — `CopyToken` rejects at `len >= cap` |
| family name | 64 | 63, same off-by-one |
| prefix floor | 3 | 4 (`kMinPrefixLen`) |
| per-title arrays | 4096 | 256, now `kMaxTitleRules` |
| `nameFragments` / `trustedSigners` | 32 / 64 | 16 / 16, hardcoded |
| families across all five groups | 1280 | 64 (`kMaxFamilies`) |

**Proven, not argued:** a 17-value family passes the *old* schema (`exit 0`)
while making `ParseRules` return `kMalformed`; under the calibrated schema it
fails. Same input, both directions.

**The shape mismatch was the worst of them.** Both per-title arrays are objects
in the schema (`family`, `match`, `values`, `reason`) and were read by
`ReadStringArray` as bare strings. The first entry anyone added would either
overflow `kMaxValueLen` with its JSON text and refuse the whole file, or fit and
be stored as an unmatchable blob. Only the two empty arrays kept that theoretical.
The parser now reads the objects and composes `store` + `id` into the joined
`"steam:730"` form `fl_ac_rules.h` always promised.

Closed by: calibrating the schema to the parser, deriving nothing by hand
(`tools/rules-validate.ps1` reads the thresholds out of the header by regex and
**fails rather than skips** if it cannot, and ctest `fl_rules_budget` generates
its boundary cases from the same constants). That ctest also asserts something
nothing asserted before — **that the rules file we actually ship parses in the
guard at all.** Every case in `guard_test.cpp` parses an inline fixture.

**Two things this turned up that are not the schema's fault:**

- **A `static_assert` that could not fire on the change it existed to catch.**
  `fl_guard_abi.cpp` pinned `kRulesIncomplete == 16` to protect
  `FlGuardReasonCount() == 17` — but `kRulesIncomplete` was the *last*
  enumerator, so appending a `Reason` left it at 16, the assert passed, the
  exported count stayed stale, and the managed mirror test iterated 0–16 and
  never compared the new value. Replaced with a `Reason::kCount` sentinel the
  count is derived from. Verified: appending a reason now moves the count to 18
  and `GuardMirrorTests` fails with a precise message.
- **`ReasonName`'s exhaustiveness was not compiler-enforced, though a comment
  said it was.** Omitting `default:` does *not* make MSVC object: C4061/C4062 are
  off by default even at `/W4`, measured by appending an enumerator with no case
  and watching `/W4 /WX` build clean. That is a gate that existed only in prose.
  It is now ctest `fl_guard`'s "every Reason has a distinct name", proven red.

### S18 ✅ · The guard refuses itself — **closed 2026-08-04**

Found 2026-08-03 during the first real injection (`spike-notes.md` §7), and
isolated on one title with everything else held constant:

| Evaluating process | Verdict |
|---|---|
| An **ancestor** of the game, with `FrameLedger.Guard.dll` loaded | `SuspiciousUnsigned`, signal `FrameLedger.Guard.dll` |
| Not an ancestor | `Allow` |

§S16 walks the game's ancestors up to the first platform launcher. The name
`FrameLedger.Guard.dll` contains **`guard`**, one of the heuristic's
`nameFragments`; the signer half is deliberately unwired, so an unchecked
signature is untrusted by definition (`fl_guard.cpp`, "that is the correct
direction") and fragment-plus-untrusted refuses. In launch mode the Agent is the
game's parent (`04_CAPTURE` §Launch mode) and hosts that exact DLL. Same shape as
the `EasyAntiCheat_EOS` service defect: **a gate that cannot pass is not strict,
it is broken.**

**Not fixable by signing.** The project ships unsigned — CLAUDE.md rule 9 and the
pinned-stack Packaging row. (§S18 and `spike-notes.md` §7 previously cited
`12_BUILD` §Packaging for this; that section contains no signing text. Citation
corrected, conclusion unchanged.)

> **The KEYING below was superseded on 2026-08-04 by §S22(b); the principle was
> not.** Everything here about *why* the exception exists, why it is confined to
> the fuzzy tier, why it never covers the target, and why identity is by
> directory-file-id rather than by name still holds and is still implemented.
> What changed is the SUBJECT: the exemption asked whether the scan-set
> **process** lived in our directory, and now asks whether the **module that
> matched** is ours. That fixed a measured refusal of every FrameLedger host not
> sitting beside `FrameLedger.Guard.dll`, and it is strictly narrower. Read the
> "Not by module name" bullet below with that in mind — it rejected a *name*
> allowlist, and it was right to; a file-id check on the module is a different
> mechanism and is not what it argued against.

#### The decision — identity by **install root**, heuristic tier only

Taken 2026-08-03 after a four-lens panel, three adversarial refuters and a
completeness critic. **All three refuters refuted the panel's own first answer**;
what follows is what survived.

Suppress **only** the `sawSuspicious` fragment tier, **only** for a scan-set
process whose image path resolves under FrameLedger's own install directory, and
**never** for the injection target. Everything else in `CheckModules` is
untouched: the exact blocklist (`st.hit`) still returns first, `kFailed` still
gives `kProcessUnreadable`, `kIncomplete` still gives `kModuleScanFailed`. The
ancestor walk keeps climbing **past** us rather than stopping at us.

Why install root and not the three obvious alternatives:

- **Not by module name** (the original option 1). `HasSuspiciousFragment` is
  called for every process in the scan set, so a name allowlist suppresses that
  name *inside the game too* — and it is spoofable by a DLL that borrows the name.
- **Not by `GetCurrentProcessId()`** (the panel's first answer). It identifies
  the process *executing* the guard; the defect is a property of the **binary**.
  `FrameLedger.App` also carries `FrameLedger.Guard.dll` — `Infrastructure.csproj`
  copies it to "everything that references it", and FR-2.2's pre-launch question
  loads it in the UI process. A second FrameLedger process in the ancestor chain
  refuses exactly as before. This is what the third refuter broke, correctly.
- **Not by stopping the ancestor walk at us**, the way `IsPlatformLauncher` stops
  it at `steam.exe`. That boundary is justified because everything above
  `steam.exe` is shared platform infrastructure that legitimately loads VAC.
  Everything above the Agent is an *undefined* category — a shell, an IDE,
  Playnite — so truncating there deletes processes we merely did not want to
  think about. That is "could not look" recorded as "looked and it was clean",
  which this file exists to prevent. Skipping one process and continuing costs
  the same and is strictly more conservative.

Install root has the trust base the other three lack: an attacker who can place
a binary inside our install directory can already replace `FrameLedger.Guard.dll`
itself, which `NativeAntiCheatGuard.cs:48` calls a worse outcome than any other
DLL-hijack in the application. It also costs one `QueryFullProcessImageNameW` per
scanned pid — the call `ImageDirectoryImpl` already makes for check 4 — and it
covers `FrameLedger.exe`, `FrameLedger.Agent.exe` and every future FrameLedger
process for free. "Could not read the path" refuses, as everywhere else.

**Explicitly rejected, with the reason, so nobody re-proposes them:**

- **Re-parenting the launched game** (`PROC_THREAD_ATTRIBUTE_PARENT_PROCESS`) so
  we are never an ancestor. This is lineage spoofing: anti-cheat reads parent
  lineage precisely to see who started the game. CLAUDE.md rule 3 — rejected on
  principle before merit. Recorded because it is the clever answer someone
  proposes next.
- **Running the fragment tier on the target subtree only, never on ancestors.**
  Attack cost zero: ship the anti-cheat in the launcher under a name not in
  `modules`. It deletes coverage for exactly the arrangement §S16 was written to
  catch.
- **Deleting the `protect` fragment** to cure the false positive in §S19 below.
  It is a detection removal in a hard gate, on the mode that ships. See §S19.
- **Deferral alone** — "attach mode only, launch mode deferred". Correct as a
  scope statement, not an answer: §S19 shows the same code path refuses
  *attach*-mode titles.

#### Why this is worth more than it looked

The old text said §S18 blocks "the early-init upscaler data §S1/§S13(c) are
about". That understated it. **§S18 is the sole blocker of the entire Vulkan
Tier-1 path.** The layer is gated by `enable_environment`
(`FRAMELEDGER_ENABLE_VK_LAYER=1`), which only the process that *starts* the game
can set — `17_HOOK_ENGINE:161` states this makes Vulkan Tier 1 launch-mode-only.
And §S1 does **not** apply there: the Vulkan path performs no injection, so there
is no suspended target and no `ERROR_PARTIAL_COPY`. "Launch mode is blocked by
§S1 anyway" is therefore false for an entire Tier-1 API family.

#### ✅ Closed 2026-08-04 — implemented, and all three blockers answered

1. **The test vehicle exists, and it is not the one this entry asked for.**
   ~~`fl_guard_test` calls `SystemSources().ProcessIsOurOwn` directly~~ — **that seam was deleted by §S22(b)**; the live test is now "§S22(b) — the real ModuleIsOurOwn answers both directions" and calls `SystemSources().ModuleIsOurOwn`, both
   directions: this process (which runs the guard code from its own directory)
   is ours; a freshly spawned `System32\cmd.exe` is not. That tests the
   *predicate* against a real machine, which is what the exception rests on —
   loading `FrameLedger.Guard.dll` into the test just to make a module NAME
   appear would have proved the fragment matcher, which `fl_guard` already
   covers, and would have exercised the boundary logic not at all.
2. **The two-pid case is a fixture, and the thing that blocked it was the FAKE.**
   `FakeEnumModules` ignored its pid, so "two distinct FrameLedger processes,
   both carrying the DLL" could not be written down. With a per-pid map it is
   seven lines. That was ~10 lines of test-side code standing in front of a
   safety item for a day.
3. **Ratified: the Agent is the sole host.** `05_DETECTION` and `07_IPC` already
   assigned it ownership of `%LOCALAPPDATA%\FrameLedger`; the guard DLL now ships
   only to `FrameLedger.Agent` (and to `Infrastructure.Tests`, which P/Invokes
   it), via `/FrameLedger.Guard.targets` instead of a `None` item in
   `Infrastructure.csproj` that flowed to every referencing project.

> **The ratification does not retire the install-root mechanism, and it is worth
> saying why rather than leaving it to look like belt-and-braces.** With one host,
> process identity would be sufficient *today* — but "sufficient today" is what
> made `GetCurrentProcessId()` look right the first time. Directory identity
> covers every future FrameLedger process for free, costs one `OpenProcess` on a
> path that a measured machine reaches approximately never, and does not have to
> be revisited when a second host appears. What ratification bought is a smaller
> surface, which is the better half of the trade.

> **And the justification recorded in `19_SAFETY` was false.** It said our own
> module set "can produce false refusals and never a true one". Measured across
> 290 live processes, three carried a fragment-matching module, none of which
> needed write access to anything of ours — an AppInit DLL or an AV hook can put
> one in a FrameLedger process too. The exception is justified by *trust*, not by
> information: an attacker who can write to our install directory can already
> replace the guard itself. `19_SAFETY` now says that instead, with the unsigned
> -shipping residual stated.

Six fail-closed cases hold the exception narrow, and each was proven red against
a specific plausible mistake — dropping the target exclusion (`guard_test.cpp:516`),
suppressing regardless of the tri-state (`:527`), and an implementation that calls
every process ours (`:612`).

**Measured on three real titles, not only in fixtures** (`spike-notes.md` §7). A
stand-in for the Agent, built into the Agent's own output directory beside a real
`FrameLedger.Guard.dll`, loads it and launches the game — so our binary is the
game's ancestor and carries the offending module name. Deadly Heart Gambit, Lies
of P and Alan Wake 2 all go `SuspiciousUnsigned` → **`Allow`**. Evaluate only;
`FlGuardedInject` was never called.

That measurement also validated the assumption the fix rests on and the unit
tests structurally cannot reach: **the Agent's executable and the guard DLL land
in the same directory**, which is what makes "equality, not containment" the right
comparison. `12_BUILD` publishes both executables into one `out/app`, so the
shipped shape matches.

### S21 ✅ · The hard gate's data source was caller-nameable, and its completeness floor never read the values — **closed**

Found 2026-08-04 by an adversarial review over the next-phase option set, and it
outranked every item that review was convened to sequence. Recorded in full
because it is the first *fail-open* found in the guard since `EnumDeviceDrivers`,
and because both halves passed every gate in the repository.

Two facts that were individually defensible and jointly an override:

| | |
|---|---|
| `RulesFilePath` built the path from `_dupenv_s(&base, &len, "LOCALAPPDATA")` | The CRT environment is **inherited**. In launch mode the guard's host is started by a shortcut, a `.bat`, or `04_CAPTURE:60`'s Steam launch-option wrapper — all of which choose that variable |
| `IsCompleteEnoughToGate` checked three family **names** in the right **groups** | It never read `values`. "Is this shaped like a blocklist" was standing in for "is this a blocklist" |

So a twelve-line rules file naming `Easy Anti-Cheat`/modules, `BattlEye`/modules
and `Riot Vanguard`/drivers with junk values parsed `kOk`, matched nothing, and
`Evaluate` returned `Allow` on a machine running Vanguard. **No admin, no write
to our install directory, nothing left on disk.** That is the override CLAUDE.md
rule 2 says does not exist.

`05_DETECTION:75-77` asserted the source "is not a parameter and cannot be
redirected". True of the pipe — §S3 closed that — and false of the environment,
which nobody re-asked. **When a doc says an input cannot be redirected, the
question is "through which channel", and the answer has to enumerate the rest.**

**Fixed by the floor, not by the path.** `fl::guard::FloorFamilies` carries the
blocklist inside the binary; `ParseRules` seeds it before reading a byte and
nothing merges, rewrites or removes it. §S8's mechanism applied to data: a family
data cannot remove cannot be bypassed. Path resolution moved to
`SHGetKnownFolderPath` as well, and that is written down as a **narrowing, not a
guarantee** — a user can still move their own Local AppData; what is gone is the
per-launch vector.

> #### The floor shipped too narrow, and that is worth recording (2026-08-04)
>
> As first written it carried **exactly the three families `IsCompleteEnoughToGate`
> required**, kept minimal on the grounds that a larger hand-written table would
> be a second copy of the blocklist and drift from the data. Measured against the
> shipped seed, that bought **4 of 22 values, 2 of 5 groups and 0 of 5 name
> fragments**.
>
> So this entry closed *"a crafted rules file makes the guard allow everything"*
> and left open *"a crafted rules file removes most of the blocklist"* — Denuvo,
> GameGuard, Xigncode3, mhyprot, FACEIT, ESEA, PunkBuster, EAC's directories and
> services, BattlEye's directories, Vanguard's service, and the entire fuzzy tier.
> The write-up above read as though the floor bounded more than it did.
>
> That gap was tolerable only while nothing delivered a rules file to any machine.
> The §S20 review is what surfaced it, because a seeder turns a repo-only defect
> into a **push channel**.
>
> **The floor is now GENERATED from `rules/detection-rules.json` at build time**
> (`tools/gen-ac-floor.ps1`), which removes the objection that kept it small — a
> table derived from the data cannot drift from it. It carries the whole shipped
> blocklist and the name fragments; `trustedSigners` is deliberately excluded,
> because it is an ALLOW-widening list and "data may only add" has the wrong
> polarity there.
>
> Two consequences worth stating. A file family identical to a floor entry is
> **deduplicated**, or an unmodified seed would spend `kMaxFamilies` twice — so
> `rules-validate.ps1` now bounds the file at **half** the cap, which is the
> worst case of a drifted file duplicating none of the floor. And completeness is
> judged on what the file **supplied**, not on what was stored, because an
> unmodified seed now stores nothing.
>
> **It also delivers §S19(d)'s substance** — a rules file with no `heuristic`
> block can no longer make the fuzzy tier stop existing — without the new
> `ParseResult` cause that entry proposed, which would have made
> `kRulesIncomplete`'s signal a lie and driven `layer.cpp` to machine-wide inert
> passthrough. A floor needs neither.

Kept able to fail: the completeness check now runs over the **file's** families
only. Running it over the merged set would have retired a real refusal by
accident, since the floor satisfies it by construction — a gate that cannot fail,
arrived at while fixing a different bug.

**A second, independent total failure in the same six lines.** The path was ANSI
(`char[MAX_PATH]`, `CreateFileA`), so a profile directory the system code page
cannot spell became a path containing `?`, and the guard refused **every title
for that user, permanently**, naming no cause. Measured 2026-08-04, system ACP
**1252**:

```
C:\Users\田中\AppData\Local    ->  C:\Users\??\AppData\Local
C:\Users\Nguyễn\AppData\Local  ->  C:\Users\Nguy?n\AppData\Local
```

Note what that measurement says and does not say. The trigger is the **system**
code page, not the user's language — a Japanese install (ACP 932) spells 田中 and
still mangles Nguyễn. So the honest form is "broken for any user whose profile
path the machine's ACP cannot represent", which the `ja`/`vi` shipping locales
make likely and which an ASCII profile can never expose. Now `wchar_t` +
`CreateFileW` throughout, including the Vulkan layer's enable-list, which had the
same defect and would have made Vulkan Tier 1 silently never work for those users.

**Three resolvers became one.** The guard, the Vulkan layer's enable-list and
`fl-probe-vklayer` each resolved Local AppData their own way, and
`DetectionRulesFile` made a fourth on the managed side while its own comment
claimed to reach "the same directory the native guard uses". `fl_ac_rules.h`
calls this "the ONE location" and says a second reader would be "a second
blocklist by accident" — there were four. `FlGuardRulesFilePath` now exports the
guard's answer for **observation only** (no setter, no path parameter), and
`RulesPathAgreementTests` asserts the managed resolution matches it. That
assertion matters most for §S20: a seeder writing where the gate does not read
reports success, and its own test would agree with it.

Sharing mode unified too — all readers now `FILE_SHARE_READ | FILE_SHARE_WRITE |
FILE_SHARE_DELETE`. The guard denied delete sharing and the layer denied nothing.

> **The primitive this prescribed was wrong, and it is corrected rather than
> quietly edited.** It said §S20's replace must be temp-file +
> `MoveFileExW(MOVEFILE_REPLACE_EXISTING)`. Measured 2026-08-04 against a handle
> opened exactly as the guard opens it:
>
> | Call | Against a live reader |
> |---|---|
> | `MoveFileExW(MOVEFILE_REPLACE_EXISTING)` | `ERROR_ACCESS_DENIED (5)` |
> | `ReplaceFileW`, backup file named | succeeds |
>
> Delete sharing is **necessary and nowhere near sufficient**. The unification is
> still right — it is what lets `ReplaceFileW` proceed — but the named call was
> the one that fails on exactly the machines where the guard is busy, and its
> error goes nowhere, so the update would simply not have happened. Same shape as
> the two `layer.cpp` comments that documented measured-wrong designs: a reader
> designs a gate around them.

Proven red before being called done. Two of the four canaries named here were
rewritten when the floor became generated — `kFloorFamilyCount` no longer exists
and no floor value can be hand-pointed — so the current set is: a generator that
drops four of the five groups, a floor keeping one of five fragments, a
`kMaxFamilies` below twice the seed, and a `kMaxNameFragments` below twice its
fragments. Emptying `FloorFamilies` still makes the disarmed-rules test allow, and
running the completeness check from index 0 still lets a file with no BattlEye
through.

#### Measured end to end, not only in fixtures (2026-08-04)

- **The override, on two real binaries.** Same input to both: a rules file naming
  the three required families with junk values, and a real
  `EasyAntiCheat_x64.dll` loaded in the target. `bb0da0a` → **`Allow`**; after
  the floor → **`BlockedModule`, family `Easy Anti-Cheat`**. The pre-fix half took
  the crafted file through an inherited `LOCALAPPDATA`; the post-fix half had to
  place it at the real path, because the environment no longer selects it.
- **The ANSI half, on the real Win32 calls.** System ACP 1252. A rules file
  written into `…\田中\AppData\Local\…` and `…\Nguyễn\AppData\Local\…` exists on
  disk and `CreateFileW` opens it; **`CreateFileA` fails with
  `ERROR_INVALID_NAME (123)`**. The same file under an ASCII profile opens both
  ways — which is precisely why a dev box cannot see this.

#### Two residuals, measured rather than asserted

- **The known-folder path is still user-relocatable, and that was the claim.**
  `HKCU\…\Explorer\User Shell Folders\Local AppData` matches what the API returns,
  and the key is `FullControl` for the current user with no elevation. So the
  wording in `05_DETECTION` holds exactly as written: this removes the
  **per-launch, per-process** vector, not every redirection. Deliberately not
  tested by repointing it — that would change the machine's shell configuration
  for every application.
- **The Vulkan layer gained two DLL dependencies, and it is mapped machine-wide.**
  `dumpbin /dependents`: `KERNEL32` → `KERNEL32, ole32, SHELL32`. Measured
  residency across 189 inspectable processes: shell32 **62%**, ole32 **61%** — so
  for roughly a third of processes these are genuinely new. The exposure is
  narrower than that number suggests, because `enable_environment` means the
  layer only *maps* into processes the Agent launched (§S2), and a game is far
  more likely than a browser to have both already. Recorded rather than waved
  away; delay-loading would not help, since the layer resolves the path at init.

### S22 ✅ · The guard gated the TARGET and nothing gated the PAYLOAD — **both halves closed**

Found 2026-08-04 by a completeness critic over the next-phase plan, in code that
had been merged for days and passed every gate in the repository. Recorded in
full because it is the second *fail-open* found in the guard, and because the
reason it stayed invisible is instructive: every reader, every doc and every
review framed the guard as a gate whose subject is **the process we are entering**.
Nobody asked what we were putting there.

#### (a) ✅ Any DLL, into any process without anti-cheat — **closed**

`FlGuardedInject(targetPid, dllPath, out)` is an exported C ABI on the shipped
`FrameLedger.Guard.dll`. Between that boundary and `CreateRemoteThread` the only
thing ever asked of `dllPath` was `GetFileAttributesW` — *exists, and is not a
directory*. Measured through the shipped DLL with no test seam:
`C:\Windows\System32\winmm.dll` into a live process, verdict `reason=0 (Allow)`,
module present afterwards.

That is **§S9's user-runnable injector**, which this project refused to ship,
re-shipped as a documented export with a published calling convention. §S9's own
reasoning names the hazard exactly and then stops one step short — it rejected a
standalone injector as "a path into a game process **that the guard did not stand
in front of**", and `fl_guard_abi.h` concluded "there is no entry point here that
skips a check". Both true, and both about the target.

Note what the shape is, because it is this file's recurring one: not a wrong
assertion, a **missing** one, behind a scope sentence that reads as though it
covered everything. Every existing test passed `FL_OVERLAY_DLL` as the payload,
and the one negative case used a *missing* path — so the whole matrix exercised
"is there a file there" and nothing exercised "whose file is it".

**Closed by a new seam and a new reason.** `Sources::PayloadIsOurOwn` resolves
the payload — through symlinks, 8.3 names and junctions, via
`GetFinalPathNameByHandleW` — and requires its directory to be the one the
guard's own code was loaded from, compared by file id. Equality, not containment,
reusing §S18's `OwnDirectory`/`SameDirectory`, which until now existed only to
*relax* this gate. A null seam, a seam that cannot answer, and "not ours" are one
outcome: `Reason::kPayloadNotOurs`.

**What it does NOT cover, with the boundary stated rather than implied:**

- It is not proof the payload is `FrameLedger.Overlay.dll`. It is proof of
  **where the bytes live**. Anyone who can write to that directory can already
  replace `FrameLedger.Guard.dll` itself — the same trust base §S18 rests on, and
  the project ships unsigned (CLAUDE.md rule 9), so there is no integrity check on
  that directory's contents.
- It is not atomic with the load. The remote `LoadLibraryW` re-resolves the path,
  so a file swapped between check and call is not caught. Same trust base again.
- **Absent** and **foreign** are kept distinct on purpose: a damaged install and a
  misuse of the ABI need opposite responses, so a path naming no file still
  returns `kInjectionFailed`. The existence test therefore runs *before* the
  identity test — otherwise "not ours" absorbs "not there", since the identity
  seam works by opening the file. Caught by the pre-existing test that asserts
  exactly this distinction.

**A layout bug came with it, and it had to be fixed in the same change or the
gate could not pass.** Nothing in this repository copied `FrameLedger.Overlay.dll`
anywhere. The native build left it in `FrameLedger.Overlay/` while the guard
built into `FrameLedger.Injector/`, and `dotnet publish` produced an `out/app`
with the Agent, the guard and the rules seed **but no payload at all**. Invisible
while the Agent had no injector control; it would have surfaced as an
unexplained `kPayloadNotOurs` the first time one was wired.
`FrameLedger.Overlay.targets` now ships it beside the Agent, and
`src/native/tests` stages its own copy beside `fl_guard_test` — where the guard
is compiled *into* the test binary, so "the guard's directory" is the test's.

Proven red **and** green, and proven to recover: three canaries (the refusal
removed, a seam failure treated as ours, a null seam allowed through) each turn
`fl_guard` red. The green direction is asserted separately, because a gate that
refuses every payload carries as much information as one that accepts every
payload — the staged Overlay must be accepted, the *same file staged elsewhere*
must not, and the test refuses to run at all if those two paths ever resolve to
one directory.

> **The canary harness lied again, and the mechanism is worth writing down.**
> Restoring the file from a backup with `Copy-Item` preserved the backup's
> **old** timestamp, so ninja judged the source up to date and never rebuilt —
> leaving the last canary's binary in place while the tree read clean. It then
> segfaulted on the exact case that canary had disarmed, which is a symptom that
> invites diagnosing the code instead of the harness. Restores must stamp the
> file. That is the fourth time this session's own verification tooling was the
> broken thing.

#### (b) ✅ The §S18 exemption asked about the PROCESS, not the module — **closed**

Same probe, same binary, same target; only the caller's directory differs:

| Caller's location | Verdict |
|---|---|
| Beside `FrameLedger.Guard.dll` | `Allow` |
| Anywhere else | `SuspiciousUnsigned`, family `unknown`, signal `FrameLedger.Guard.dll` |

`SuppressFragmentTier` delegates to `ProcessIsOurOwn`, which compares the
scan-set **process image** directory to `OwnDirectory()`. The module that matched
is *our own guard DLL* — it contains the fragment `guard` — but the exemption
never asks about it. So any FrameLedger-family host that does not sit beside the
guard poisons its own scan set. The Agent works by accident:
`FrameLedger.Guard.targets` copies the DLL beside `FrameLedger.Agent.exe`.

This matters now rather than in the abstract, because every throwaway
inject-host proposal puts the tool in its own build directory — the arrangement
measured RED — and a launch-mode host is by construction in the §S16 scan set.
Whoever hits it will read the refusal as the fuzzy tier being over-eager (§S19
says so, in writing) and go rewriting a safety gate instead of moving a file.

**The right fix is to key the exemption on the MATCHED MODULE** — a scan-set
process is exempt when the module that tripped the fragment tier is ours by file
id. Strictly narrower than today's process-level exemption, and it fixes launch
mode. It also removes a real over-reach nobody had costed: today a genuinely
foreign suspicious module loaded into a FrameLedger process — an AppInit DLL, an
AV user-mode hook, an IME — is suppressed along with our own.

**Done, and it was not the size the plan assumed.** `NameSink` was
`bool(*)(void*, const char*)` fed by `GetModuleBaseNameA`, so the module's path
never reached the decision point — the constraint §S19(b) already recorded. What
it actually took:

- **A new sink type.** `ModuleSink` carries `(name, path)`; `EnumerateModules`
  takes it, drivers keep `NameSink`. `GetModuleFileNameExW` failing is **not**
  `kIncomplete` — the base name is what the blocklist matches on, so the scan is
  complete in the sense that matters. What is lost is only the ability to
  *exempt*, and a null path is treated as "not ours". The failure narrows what we
  allow rather than widening it.
- **The restructure §S19(b) predicted.** `NameSinkFn` latched the FIRST
  fragment-matching module and skipped the fragment test for every module after
  it. Harmless while any hit refused, because the latched name only had to be *a*
  reason. Per-module suppression turns it into a **fail-open reachable by load
  order**: our guard DLL matches first, is exempted, and a genuinely suspicious
  module loaded afterwards is never recorded. `ModuleSinkFn` therefore skips an
  exempt module and **keeps looking**, latching the first non-ours one — and
  still does not stop, because an exact blocklist hit later in the same process
  outranks the fuzzy tier.
- **`ProcessIsOurOwn` is gone**, along with its implementation. A seam nothing
  calls is dead weight, and keeping both forms would be two answers to one
  question.
- **One implementation, two seams.** `ModuleIsOurOwn` and `PayloadIsOurOwn` ask
  the identical question and `SystemSources()` wires both to `FileIsOurOwnImpl`.
  They are separate *pointers* only so the matrix can force each failure
  independently; a live test asserts the two pointers are equal, so a future
  divergence fails rather than quietly becoming a second matcher (§S15 item 1).

The module form is also **strictly narrower**, which nobody had costed: a
genuinely foreign suspicious module loaded into a FrameLedger process — an
AppInit DLL, an AV user-mode hook, an IME — used to be suppressed along with our
own, and now is not.

Five canaries, each proven red: the exemption stopping the scan (the load-order
fail-open), the target exclusion removed, an unlocatable module treated as ours,
the module seam's failure ignored, and — re-run precisely — the payload seam's
failure ignored.

> **Two of this item's own tests were passing for the wrong reason, and the
> canaries are what said so.**
>
> The first: `FakeModuleIsOurOwn` returned `kFailed` *without touching the
> out-param*, so "the seam cannot determine" was indistinguishable from "not
> ours" and the test held whether or not the caller read the return code. The
> fake now writes **true and then fails**, which is the realistic shape and the
> only one that can catch a caller which ignores the code.
>
> The second was found by a canary going GREEN: removing the guard's
> `modulePath == nullptr` clause changed nothing, because the fake *also*
> rejected null. The test was covering the fixture, not the code. The fake now
> answers "ours" for a null path — wrong on purpose — so the guard's own clause
> is the only thing standing between a sloppy seam and an exemption. **A
> redundant check in a fixture can make a real clause untestable, and the only
> way that surfaced was disarming the clause and watching nothing happen.**

> **The §S22(a) canary that ran a day earlier was mislabelled**, and it is
> corrected rather than left. It replaced the payload check's condition with
> `false && sources.PayloadIsOurOwn(...)`, which **short-circuits the call
> entirely** — so it proved "removing the seam call breaks the suite", not "a
> seam failure that is ignored breaks the suite". The precise form ignores only
> the failure code and leaves the call in place; it is red too.

### S14 ✅ · Pre-injection check 3 ~~is **unwired**, and has no "cannot determine" state~~ — both halves wired and seeded 2026-09-25

> **Closed 2026-09-25 (beta.8, owner decision: title lists yes, the AreWeAntiCheatYet bulk list no).** The store
> half reads the store's own metadata through `Sources::StoreIdentity`, from the install root the guard derives
> from the pid — the ABI is unchanged, which was reason (2). Reason (3) is answered by the matrix in `19_SAFETY`
> check 3: listed refuses, no store identity is not applicable, unreadable store metadata refuses. Both lists are
> seeded (30 Steam ids, 33 executables) and floored. What stays open is Epic, which has no reader and so carries no
> identity to this check. The body below is the history.

Found 2026-08-02 while hardening the rules toolchain. `19_SAFETY` §Pre-injection
checks lists a per-title blocklist as check 3, but `anticheat.blockedExecutables`
and `anticheat.blockedStoreIds` are **both empty arrays**, so the check matches
nothing.

> **Corrected 2026-08-03, and the true cause is worse than the recorded one.**
> The empty arrays are the *second* reason check 3 matches nothing. The first is
> that `MatchesBlockedExecutable` and `MatchesBlockedStoreId` have **no call site
> anywhere in the tree** — `EvaluateImpl` runs `LoadRules → CheckDrivers →
> CheckServices → CheckModules` and stops. Populating the data would change
> nothing. Check 3 is unwired, not unpopulated.
>
> The sentence below that read "checks 1, 2 and 4 run" was wrong twice: **check 4
> had no implementation either** — `Reason::kAntiCheatDirectory` and
> `kAntiCheatFile` were declared, named in `ReasonName` and mirrored into the
> managed enum, and nothing produced either.
>
> #### ◐ The EXECUTABLE half is wired, 2026-08-05. The store-id half is BLOCKED, and named.
>
> `CheckBlockedExecutable` runs inside `EvaluateImpl`, between the module scan and
> the static pre-scan, so checks 1, 2, 2b, **3 (exe)** and 4 now run. It needed a
> new seam — `Sources::ImageFileName` — because `ImageDirectory` deliberately
> resolves the *install root* and throws the file name away, which is the one fact
> check 3 matches on.
>
> **Unresolvable identity refuses** (`kProcessUnreadable`), per the owner decision
> and `19_SAFETY`'s "must read UNKNOWN, never clean": `kFailed`, `kIncomplete`, an
> empty name and a null seam all take the same path. The conversion to narrow is
> `WC_ERR_INVALID_CHARS` with no default character, so a name that cannot be
> represented exactly **fails** rather than becoming a string with `?` in it —
> §S21's ANSI defect was exactly a silent lossy conversion.
>
> **The list ships empty**, so nothing is refused today. Which titles to list stays
> the owner's product decision; what changed is that populating it would now *do*
> something.
>
> **The store-id half cannot be called, for three independent reasons**, and this
> is a limitation rather than an oversight:
>
> 1. **No producer.** Nothing parses Steam `.acf`, GOG `.info` or Epic `.item` —
>    the platform metadata extractors were never built, so `store_id` is null for
>    every title (`15_ROADMAP`).
> 2. **No channel, by design.** `FlGuardEvaluate` takes a pid and nothing else, and
>    `fl_guard_abi.h` says so deliberately: *"no way to hand in evidence — the guard
>    collects its own."* A caller-supplied store id is a caller asserting a safety
>    fact, which §S3 forbids in as many words.
> 3. **"Unknown refuses" applied to it is a gate that cannot pass.** If the guard
>    can never resolve a store id and an unresolved one refuses, every title on
>    every machine refuses.
>
> So `MatchesBlockedStoreId` stays implemented, tested and uncalled. **Do not
> "fix" (2) by widening the ABI.** The route is the metadata extractors plus a
> guard-side resolver, which is its own PR and its own fail-closed matrix row.
>
> **🅓 The store-id half is deferred to P4, rationale written 2026-09-06.** Its only
> in-policy route is the platform metadata extractors (Steam/GOG/Epic/itch — P4's library
> import) feeding a guard-side resolver, and a resolver with no extractor behind it would
> refuse every title (an unresolved id refuses, by the matrix). The executable half is
> wired and proven red; the data for both halves is empty by decision. Cost of waiting:
> none — no title is blocked by store id today because none is listed.
>
> Proven red: disarming the matcher makes the guard **allow** a blocked
> executable, and the test asserts `kBlockedExecutable` specifically rather than
> "it refused" — which is indistinguishable from the four refusals the guard
> already makes.

> **Check 4 is now implemented** (`fl_prescan.cpp`, inside `EvaluateImpl`), so
> ~~checks 1, 2, 2b and 4 run. **Check 3 remains unwired** and this item stays
> open on that.~~
>
> > **Superseded by the block above, which sits forty lines higher in this same
> > section.** For part of 2026-08-05 §S14 said both "the executable half is
> > wired" and "check 3 remains unwired", and a reader greping for the status
> > found whichever copy came first. Struck rather than deleted: two statements
> > of one status inside one section is the drift shape this file exists to
> > record, reintroduced inside the recorder. **`EvaluateImpl` runs five checks:
> > 1, 2, 2b, 3 (executable half) and 4.**
>
> The parser now reads both per-title arrays in their real object shape, so the
> data can be written before the wiring lands — it used to read them as bare
> strings, and the first entry ever added would have refused the whole rules
> file (§S17).

The gate is not currently weakened — checks 1, 2, 2b, 3 (exe) and 4 run, and
every family in the seed is caught by a module, driver, service or directory
signal — but a documented check that does nothing will read as "this title is
not a known online title" to the next person who trusts it. That still applies
to the store-id half, and to the executable half for as long as its list ships
empty.

Two decisions, both the owner's:

1. **Which titles.** Seeding a list of competitive/online games is a product
   decision with false-refusal consequences, not a mechanical fill-in.
2. **What "unknown" means.** A title the user added by exe path has no store id,
   and store metadata is opt-in (CLAUDE.md rule 8). "No store id" must not read
   as "not blocked", and a renamed exe defeats `blockedExecutables` the same
   way. Whatever ships, an unresolvable identity has to land on *unknown*, and
   the doc must say where unknown goes — which today it does not.

Until both are answered the inertness is recorded in the data's own `$comment`
and beside check 3, rather than being inferable only from two empty arrays.

### S6 ✅ · The 30 s scan window is the weakest part of the most important behavior — **the in-process half built 2026-09-06 (P1 item 1)**

> **Built, as the deferral below said it would be: one detour, both jobs.** `kernelbase.dll!LoadLibraryExW`
> is patched from `InitThread` after the present hooks; the body calls the original first, skips
> data-file / resource mappings, reads the mapped module's own file name into a stack buffer and compares
> its base name (a) against `FL_HOOK_INVENTORY` + `FL_RUNTIME_CENSUS` — a match bumps a 15-bit wake count
> and signals the watchdog's auto-reset event, so the lazy installers run **within milliseconds** rather
> than on the next 1 Hz tick — and (b) against the compiled floor's MODULE families (prefix, case-
> insensitive, the generated table the guard itself carries; zero allocation) — a match CASes the family's
> 1-based index into `earlyStopFamily`, and the present path (`MayObserve`) or the watchdog, whichever is
> first, calls `StopObserving(FL_STATUS_STOPPED_BLOCKLISTED)`. It supplements the poll and replaces
> nothing; it installs nothing inline (§H2). **Measured**, real Overlay in a real target (ctest `fl_guard`):
> a late `sl.interposer.dll` hooked **94 ms** after it appeared (bound 500 ms, which the tick alone cannot
> meet more than half the time); a decoy copied to `EasyAntiCheat_fl_fixture.dll` and loaded 2.5 s in
> stopped the Overlay with `earlyStopFamily` equal to the index the test computed from the same generated
> table, `unhookRequested` still 0, and `writeIndex` frozen thereafter. The CaptureHost reads both words
> (`Consume/EarlyStop.cs`), ends the session as `WriterStoppedBlocklisted` (exit 5, the safety-stop code)
> and names the family from the staged rules file.
>
> **What it does not do, stated so the disclosure stays honest:** the fragment tier and the signer tier
> are not in the detour, so a module the floor knows only that way waits for the host's 30 s scan; the
> Disclaimer's "within 30 seconds" is unchanged. **Owed:** the real-game leg of §H2 — a title that loads
> D3D12 or Streamline lazily, hooked through the wake rather than the tick — which the next captures
> report via the `loader detour:` line (`woke the watchdog N time(s)`).

Now disclosed honestly in the Disclaimer and README. The open question ~~is~~ was whether
to shrink it.

> **"Already installs" was false, and it changed how this item read.** Corrected
> 2026-08-04. `src/native/FrameLedger.Overlay/` contains two files and
> `dllmain.cpp` exports exactly one function; there is no `MH_CreateHook` outside
> `fl-probe-hookprofile` anywhere in the tree. So §S6 is **not** "the cheapest
> available improvement" — it is downstream of the entire P1 hook layer, and a
> reader was being told a mechanism sat there unused. `17_HOOK_ENGINE` §DLL entry
> words the same thing correctly, as *specification*. The sentence below is
> retained in its original form because the plan it describes is still the plan.
>
> > **Every factual clause in the paragraph above is now false** (2026-08-05,
> > after #40–#44), and it is corrected rather than deleted because the *reasoning*
> > it carried is what the next reader needs. `FrameLedger.Overlay/src/` holds
> > **one** file; `dllmain.cpp` exports **four** functions (`FlGetLayoutVersion`,
> > `FlGetBuildId`, `FlGetStatus`, `FlRequestUnhook`); and there are **three**
> > `MH_CreateHook` calls in it — slots 8, 13 and 22 on the shared `dxgi.dll`
> > class vtable.
> >
> > **What that changes for §S6, stated narrowly.** The premise of the correction
> > — "downstream of the entire P1 hook layer" — is now only partly true: MinHook
> > is initialised and a hook layer exists to hang a `LoadLibrary` hook on, so the
> > *distance* to §S6 is shorter than it was. It does **not** follow that §S6 is
> > now cheap or that it is scheduled. `17_HOOK_ENGINE` §DLL entry still specifies
> > a **deferred** install (§H2), the poll it would supplement still has no
> > production driver (nothing writes `guardTicks`), and no phase owns this item.
> > Its disposition — work, or deferral-with-rationale — is an open owner
> > decision, listed as such in §S24.

The Overlay is specified to install a `LoadLibrary` hook for lazily
loaded graphics DLLs (`17_HOOK_ENGINE` §DLL entry); the same hook could raise the
control-block flag the moment a blocklisted module name loads, turning a 30 s
poll into near-immediate detection. This is the cheapest available improvement to
the behavior the product treats as most critical.

**Needs:** confirmation that a name comparison against a small fixed table is
acceptable in that hook under the no-allocation rule (it should be), and a
decision on whether it supplements or replaces the poll. Supplements — the poll
also catches modules loaded before we hooked.

> #### 🅓 Deferred to P1, rationale written 2026-09-06 (owner: "theo bảng")
>
> The mechanism is a `LoadLibrary` hook inside the Overlay raising the control-block flag
> the moment a blocklisted module name loads, supplementing the 30 s poll. It is specified
> (`17_HOOK_ENGINE` §DLL entry, deferred install per §H2) and it needs the P1 hook layer's
> `LoadLibrary` detour to hang on — the same detour lazily loaded graphics DLLs need — so
> building it in P0 would mean building that detour twice. The cost of waiting is the one
> the Disclaimer already discloses: up to 30 s between a mid-session load and the stop.
> Not closed: the name comparison in that hook must be against the compiled floor
> families and allocation-free, and it supplements the poll rather than replacing it.

### S7 ✅ · Guard handle rights and WOW64 — **closed**

Measured unelevated 2026-08-02 by `src/native/tools/fl-probe-guard` (ctest
`fl_guard_apis`); evidence in `spike-notes.md` §1, rule now written into
`19_SAFETY` §Pre-injection checks item 1.

The read handle rights are sufficient. `LIST_MODULES_ALL` is mandatory: on a
live 32-bit target the default filter returned **7 of 15** modules *as a
success*, which is the under-report that would read as clean. And every failure
of the call — `ERROR_ACCESS_DENIED (5)` on a protected target,
`ERROR_PARTIAL_COPY (299)` on a suspended one — means REFUSE, never "no modules
found".

The one branch that could **not** be measured here is a service query returning
`ACCESS_DENIED`: a standard user holds `SERVICE_QUERY_STATUS` on the stock
service set, so it has to be driven from a unit-test fake. Recorded in
`spike-notes.md` §1 rather than left to look covered.

### S8 ✅ · The absence-of-override mechanism — **closed**

The intent was right and is kept; the mechanism was wrong and is replaced.
Rewritten in `14_TESTING` §Safety-guard tests.

The original — a `sealed` token type "only the guard can produce", passed as a
constructor argument — was disproved by compiling: C# accessibility flows
*inward*, so an enclosing type cannot reach a nested private constructor
(`CS0122`). (`internal` *does* hold across an assembly boundary — `CS1729` when
`Infrastructure` tries to forge one — but that was never the weak part.)

The real weakness is that **any token which escapes can simply be ignored**: a
caller can decline to ask for one. §S13(a) put the guard in C++, which makes the
stronger shape natural — the guard **owns the chokepoint** and calls the
injection primitive itself, the primitive has internal linkage in the guard's own
translation unit so no other TU has a symbol to call, and a build-time check
asserts nothing else references it. A token that escapes can be ignored; a symbol
that does not exist cannot be called.

### S9 ✅ · `FrameLedger.Injector.exe` — **closed: it does not exist**

Decided 2026-08-02, written into `12_BUILD` §Targets. `FrameLedger.Injector` is
a **static lib only**. A user-runnable `LoadLibraryW` injector is a path into a
game process that the guard does not stand in front of, and no invocation
contract makes that safe — a guard result crossing a process boundary is exactly
the forgeable clearance §S13(b) rejects. The Agent links the lib; the guard owns
the chokepoint inside it, so there is no entry point that skips the gate. Manual
testing uses `hook-harness`.

### S10 ✅ · Machine-wide layer registration without consent — **closed**

Written into `12_BUILD` §The Vulkan layer is not registered at install time.
The install-time registration is removed; the rule is now the one
`17_HOOK_ENGINE` §Vulkan always stated — **registered only while at least one
Vulkan game has hooking enabled**, unregistered when the last is disabled and on
uninstall, under `HKCU`. The `--register-vklayer` flag and the Settings button
are repair tools that reflect that state, not independent grants of machine-wide
reach.

Note this is a *consent and blast-radius* fix, not the §S2 guard fix. A
registered layer still loads into every Vulkan process while it is registered;
narrowing *when* it is registered reduces the window, and `enable_environment`
plus the in-layer scan (§S2) is what closes the hole.

### S11 ✅ · FR-2.2 / FR-2.3 interaction — **closed**

Specified in `19_SAFETY` §A game already enabled can become blocked later. The
pre-scan re-runs on every rules update and every exe change; a new match forces
`hook_enabled = 0` and sets `hook_blocked_reason` — **no schema change needed,
the column already exists** (`06_DATA_MODEL` §games) and a non-null value already
means "toggle disabled". `hook_consent_at` is preserved: the block is not a
withdrawal of consent. A rules update that removes a match does **not** re-enable
hooking on its own, because that would let the rules feed switch injection on for
a game with nobody looking.

