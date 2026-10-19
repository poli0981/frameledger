# Open questions — closed H-series entries

*Moved verbatim from `docs/20_OPEN_QUESTIONS.md` on 2026-10-19 (beta.24, owner decision D66): the entries §S24 marks resolved (✅), with their reasoning, in their original order — never deleted. The table at the end of the H-series series there lists each one. Nothing below was edited, so its "below" and "above" point inside the original file.*

### H5 ✅ · Proxy swapchains defeat the dummy-vtable assumption — **the DLSS / DLSS-G question closed 2026-09-06 (owner)**

> **CLOSED 2026-09-06, 12:17, on Cronos: The New Dawn demo with frame generation ON (`spike-notes` §9).**
> The second Streamline 2.8.0 title, UE: `Native FPS: 86.8 -> Displayed FPS: 172.72 (x1.99 FG)`,
> `unseen=0 over samples=10063`, `frame generation: DlssG — the NVIDIA driver agrees`, `upscaler: Dlss
> (driver-reported)`, RT Yes. **So the pacer that bypasses the patched `Present` bodies is Dying Light:
> The Beast's integration, not SDK 2.8.0's** — 2.8.0 on UE presents its generated frames through the body
> the inline patches cover, exactly as 2.7.x and 2.10.x do. Every measured title now prints the rule-6 trio
> or a counted `none`, on NVIDIA through one of three producers: the hooked bodies (Cyberpunk, Hell Is Us,
> Expedition 33, Wukong, Rune Factory, Onimusha, Cronos), DXGI's own counter where the hook cannot count
> (DL:TB), or a withheld `N/A` only when that counter was never read. Identity comes from the tags (DLSS-G)
> and the hooks, and from the driver's per-process word where no hook can see it (the NGX-direct titles),
> with the negative measured. **What stays unlocalised and is not needed for any number the report
> prints:** which body DL:TB's pacer reaches. The owner closed the item on this reading; the sub-items
> below are its history.

> ### CASE 3 HAS A CANDIDATE TITLE — Dying Light: The Beast, Streamline 2.8.0, DLSS Frame Generation ON, 2026-09-05
>
> **The numbers.** Five 58 s captures at DLSS with DLSS Frame Generation on (the owner's
> setting, confirmed after the runs): `presents = tokens = batches` on every one —
> 4706 / 4258 / 4560 / 4311 / 3441 — so the report printed `FPS: 80.41 / 72.73 / 77.69 /
> 73.64 / 58.87` and **`frame generation: none`**, with `sl.dlss_g.dll` and `nvngx_dlssg.dll`
> (310.6) in the census. The fifth ran on the #116 build, 2026-09-05 00:53. **Owner decision
> the same night: a separate session and a separate PR own this; nothing below is started
> until the discriminator has a reading.**
> **The same title with FSR frame generation on** read presents 8255 against 4129 tokens
> (×2, the loader row, §H11 R6). **Cyberpunk 2077 on Streamline 2.7.1 with the same DLSS-G
> generation (310.8), same GPU,** read presents 13706 against 3439 tokens (×3.99) on 2026-09-03.
>
> **What that means, stated as the two readings the record cannot separate.** Either (a)
> Streamline 2.8's DLSS-G presents its generated frames on a path the class-vtable hook on
> `Present`/`Present1` does not see — case 3 of this entry, realised, with the application
> frames still counted by `slGetNewFrameToken` and the generated ones simply absent — or (b)
> the title was not generating in those runs despite the setting. **The discriminator is the
> owner's counter reading**: an in-game or Steam frame counter near 2× our `FPS:` line is (a);
> one near our line is (b). It is pre-committed here so the next run answers it rather than
> being read into whichever reading is more comfortable.
>
> **Why this is a rule-6 exposure and not a curiosity.** `none` there is computed —
> `presents == application frames` — and under reading (a) that equality holds *because*
> the generated presents are missing, so the report states the absence of frame generation on
> a title that is generating. The consumer cannot tell this from a real off. Until the
> discriminator runs, a `none` on a Streamline 2.8 title with DLSS-G in the census is a
> **count**, not a verdict, and `03_METRICS` §FG has no wording for that case yet. This is the
> §H5 outcome the entry was written to fear, on a newer Streamline than the one that measured
> it not happening; whether the fix is a wording, a census-version gate (the interposer's file
> version is ours to read — it is a module in our process, not game memory), or a second
> present sink is the owner's decision after the reading. **What is NOT in doubt:** the
> Streamline tag row (`slSetTagForFrame`, #115) is unrelated — this is about presents.
>
> ### THE READING, 2026-09-05 — reading (a), and what landed on it
>
> The owner, verbatim: *"Trong game là DLSS X 4, steam đã hiện dlss, frame ghi nhận được
> native/frame gốc chưa frame gen."* — the title is at DLSS ×4 (multi-frame generation), the Steam
> overlay shows DLSS, and the frames this hook records are the native ones, before frame
> generation. That is reading **(a)**: case 3 realised on Streamline 2.8.0. The numeric counter
> value was not part of the reading and is the first thing the session below writes down.
>
> **The record already corroborated it, in a field nobody had read for this purpose.** The RT
> census on the DLSS ×4 runs reads **97.1 % of all presents RT-active**; the same title with FSR
> frame generation ×2 reads **54.6 %** — generated presents carry no ray-tracing work, so at ×2
> half of them are RT-silent. A window in which every present carries RT work is a window that
> holds no generated presents. Both readings of the count predicted that; only the owner's
> reading says which.
>
> **What landed (PR-A1, C# only, nothing on the present path):**
>
> - The capture host takes an **out-of-process module snapshot** beside every guard scan —
>   `Process.Modules`, the loader's list through documented PSAPI, the same read the guard
>   already performs on every target — and the file's version resource on the path it names.
>   The report prints one `module:` line per census-named module (version, path). The census
>   cannot carry a version without a layout change; this can, at no cost in the hook.
> - `MeasuredFacts` **withholds `none`** when `sl.dlss_g.dll` is in the census beside an
>   interposer whose file version is ≥ 2.8.0 or could not be read, and the report prints
>   `Presented FPS` with a qualifier: the number counts APPLICATION frames; whether frames were
>   generated, and the Displayed rate, are unknown. **The key is the Streamline plugin, not
>   `nvngx_dlssg.dll`:** every validated `none` on this machine — Cyberpunk off / ×3 / ×4 at 2.7.1
>   (census `0x5793D`), Expedition 33 and Lies of P with frame generation off — has the NGX
>   module loaded and the plugin bit CLEAR; every DL:TB run (`0x25F0F` / `0x25F4F`) has it SET.
>   The gate leaves each validated result byte-identical, and eight consumer cases pin that.
> - **Cost, pre-committed:** DL:TB at DLSS with frame generation **off** also reads withheld if
>   the plugin is a startup-time load on this title. Leg 0 below measures which.
>
> **The session, pre-committed before it runs.** One owner session, one launch per leg, the
> capture host as built plus **PresentMon 2.5.1 console, elevated, concurrently** (it is retired
> as a shipped rung — §S31 row P2 — and is still the one instrument on this box that counts
> presents from the kernel's side; on Cyberpunk it counted every generated present at the same
> rate as this hook), plus the title's own FPS counter and the Steam overlay counter read by
> eye mid-capture and written down **as numbers**. Read off our report: `FPS:` / `Presented FPS`,
> `FG counts` (`presents=`, `tokens=`, `streams=`), the `module:` lines. Read off PresentMon:
> rows over span, the number of distinct `SwapChainAddress` values, `PresentRuntime` and
> `PresentMode`.
>
> | Leg | Title / setting | What it settles |
> |---|---|---|
> | 0 | DL:TB, DLSS, DLSS FG **off** | whether `sl.dlss_g.dll` is in the census with FG off (per-setting vs startup); ours ≈ PresentMon ≈ counter ≈ 1× |
> | 1 | DL:TB, DLSS, DLSS FG **×4** | the discriminating leg |
> | 2 | Cyberpunk 2077, DLSS, MFG ×4 (Streamline 2.7.1) | control on the same build: ours ≈ PresentMon ≈ 4× tokens, `x3.99 FG` printed, `module: sl.interposer.dll 2.7.1.0`, plugin bit clear |
>
> **Leg 1, the counter half, READ 2026-09-05** (owner's run on #118, `dyinglightbeast-141045`):
> DLSS FG ×4, our line `Presented FPS: 76.7` with `none` withheld exactly as built, the title's own
> counter **~300** — 3.9× ours. Reading (a) now has its number. The same day's `-105050` run read
> 93.24 on the same shape. **Still owed from this table:** the PresentMon leg beside it (which
> decides P1 against P3, i.e. whether the generated presents are kernel-visible at all), Leg 0
> (whether `sl.dlss_g.dll` is a startup load), and Leg 2's control. Also the same day, Cyberpunk
> at DLSS + RR + MFG ×3 read `62.86 → 187.49 (×2.98 FG)`, `frame generation: Active (technology not
> identified)` — the count is right and the technology is unnamed, because `kFeatureDLSS_G` is
> never evaluated through the export this build hooks; and the four UE titles at DLSS read
> `upscaler: N/A` (Lies of P, Hell Is Us, Expedition 33, Wukong: NGX-direct super-resolution, the
> Streamline interposer loaded and idle for it). **These three shapes — hidden presents on SL 2.8,
> an unnamed DLSS-G, and an unnamed NGX-direct DLSS — are one question with one candidate answer
> that is not a hook:** `NvAPI_NGX_GetNGXOverrideState` (NVAPI, MIT, vendored; R570+) takes a
> PROCESS ID and returns the driver's own feedback bits per feature — SR / RR / FG each with
> `DLL_LOADED`, `CREATED`, `EVALUATE`, `FG_MODE`, `FG_MULTI_FRAME` — plus `scalingRatio`,
> `performanceMode` (the quality mode), `renderPreset`, `frameGenerationCount` and
> `frameGenerationMode`. Out of process, from the driver's bookkeeping, whatever route the title
> took to NGX. **Unmeasured, and measured before anything is built on it:** whether the bits are
> populated for a process with no NVIDIA-app override configured (the API's name says "override"),
> and whether `frameGenerationCount` is the title's multiplier or only an override target.
> `fl-probe-nvapi --ngx-state <pid>` prints the raw words and every decoded bit; the owner runs it
> against Lies of P (DLSS, no FG), Hell Is Us (DLSS + FG ×4), Dying Light: The Beast (DLSS FG ×4)
> and Cyberpunk (MFG), each while the title runs — no capture, no injection, no consent needed.
> **The decision it opens is the owner's:** a driver-reported fact is neither a hooked argument
> (rule 4) nor a static hint (`05_DETECTION`); if the probe answers, `03_METRICS` needs a
> *driver-reported* rung with its own wording ("reported by the NVIDIA driver, not counted"), and
> the rule-6 pair on the hidden-presents shape could then read *application frames counted ×
> multiplier reported* — labelled as such, never as a measured Displayed.
>
> **MEASURED 2026-09-06 morning, three titles, driver 616.64 (`spike-notes` §9): the bits ARE populated
> without an override, for super resolution only.**
>
> | Title (override?) | SR feedback | RR feedback | FG feedback | ratio / mode / preset / fgCount |
> |---|---|---|---|---|
> | Hell Is Us (none), DLSS + **DLSS-G ×4 running** | `0x80605` INITIALIZED DLL_EXISTS **CREATED EVALUATE** ERR_NOT_FOUND | INITIALIZED DLL_EXISTS ERR_NOT_FOUND | `0x5` INITIALIZED DLL_EXISTS — **no CREATED / EVALUATE / FG_MODE** | 0 / 0 / 0 / 0 |
> | Expedition 33 (NVIDIA-app override ON), FG off | `0x8067F` … ENABLED DLL_LOADED DLL_SELECTED PRESET PERF_MODE **CREATED EVALUATE** ERR_NOT_FOUND | INITIALIZED DLL_EXISTS ERR_NOT_FOUND | `0x1F` INITIALIZED ENABLED DLL_EXISTS DLL_LOADED DLL_SELECTED | 0 / **2** / **11** / 0 |
> | Lies of P (none), DLSS, no FG | `0x605` INITIALIZED DLL_EXISTS **CREATED EVALUATE** | INITIALIZED ERR_FAILED ERR_NOT_FOUND | INITIALIZED ERR_FAILED ERR_NOT_FOUND | 0 / 0 / 0 / 0 |
>
> Four readings. **(1) `CREATED | EVALUATE` on the SR word is set on both no-override titles** — the
> driver keeps per-process bookkeeping of the NGX super-resolution feature whatever route the title took,
> which is exactly the identity `upscaler: N/A` lacks on the NGX-direct titles (all three print it). **(2)
> The FG word does NOT reflect DLSS-G activity:** Hell Is Us was generating at ×4 through Streamline
> (`83.21 → 332.82 (×4 FG)`, `DlssG`, the same session) and its FG word says INITIALIZED | DLL_EXISTS and
> nothing more — the word tracks the override machinery's DLL, not the plugin's evaluations. Frame
> generation stays with the tags and the count. **(3) `frameGenerationCount` is 0 on every title,
> including the one at ×4 — it is the override target the header names, never the title's multiplier**,
> so the "application frames counted × multiplier reported" pair above has no producer and is dropped.
> **(4) `scalingRatio` is 0 everywhere, override included; `performanceMode` / `renderPreset` are
> populated only under the override (E33: 2 / 11) and are then the OVERRIDE's values, not the title's** —
> so this route gives an identity and nothing about the render size or quality. `ERR_NOT_FOUND` sits
> beside CREATED | EVALUATE on two titles and is absent on the third; the header does not say what it
> refers to (a DRS profile, most likely), and it is not read as anything.
>
> **What a driver-reported rung may therefore claim — the owner's decision, not yet built:** on a
> session where every hook says `upscaler: N/A`, `Dlss (driver-reported: the NVIDIA driver reports an
> NGX super-resolution feature created and evaluated in this process — not counted by this hook)`. It
> may name the vendor and the feature; it may not state a quality, a ratio, a render size, a frame
> generation mode or a multiplier. On a hooked-identity session (Cyberpunk, DL:TB, Onimusha) the word
> is a consistency check the report prints beside the hooked name. **Owed before it is built — the
> negative:** the probe on Lies of P or Hell Is Us with DLSS switched OFF in the menu, expected
> `CREATED` clear; a rung that has only ever seen the positive would be the census's mistake again.
> Plumbing when decided: the probe already builds against `fl_nvapi`; the capture host would call it
> beside each module snapshot (out of process, no consent needed) or, if it goes in-process, through
> `Infrastructure` only (CLAUDE.md: no P/Invoke elsewhere).
>
> **In-process since 2026-09-10 (P2 PR-E2):** the same words through `FrameLedger.NvapiBridge.dll`
> (`Infrastructure.Telemetry.NvapiNgxStateProbe`, the `INgxDriverProbe` the Agent will take from DI);
> the spawn below is retired and `fl-probe-nvapi.exe` is no longer staged beside the host. The readings,
> the merge and the printed block are unchanged, and `NvapiNgxStateProbeTests` maps the bridge's three
> answers to the three outcomes the machine line used to carry.
>
> **BUILT 2026-09-06, on the owner's decision ("nối probe để bắt đầu").** The capture host spawns
> `fl-probe-nvapi --ngx-state <pid>` beside every module snapshot (the probe prints one machine line,
> `NGXSTATE status=… sr=0x… …`, and is staged beside the host by the project file), merges the readings
> (the last answered word is the state; a change between readings is printed, never averaged) and
> prints `NVIDIA driver NGX state:` in the runtime block. `03_METRICS` §Upscaling carries the rung and
> its limits. **Pre-committed, read off the next run:**
>
> | Row | Title / setting | Expected | Else |
> |---|---|---|---|
> | **N1** | Lies of P, Hell Is Us, Expedition 33 at DLSS | `upscaler: Dlss (driver-reported: …)` | a `CREATED \| EVALUATE` word beside `N/A` is a parser or plumbing defect — localise |
> | **N2** | Cyberpunk, DL:TB, Onimusha (hooked `Dlss`) | `upscaler: Dlss — the NVIDIA driver agrees …` | a disagreement WARNING is printed as such; it is not resolved by preferring either |
> | **N3** | **the negative, still owed:** Lies of P with DLSS switched OFF in the menu | `CREATED` clear → `upscaler: N/A (…) — the NVIDIA driver reports no NGX super-resolution feature created …` | `CREATED \| EVALUATE` still set with DLSS off → **the rung is withdrawn** (the bit is a session-lifetime latch, not a state) and the line goes back to N/A |
> | **N4** | a title at FSR / XeSS with no NGX feature | `Fsr3` / `N/A` with the driver's negative beside it | `CREATED` set on a title with no DLSS in its menu → withdraw |
>
> Lies of P has no DLSS Frame Generation (owner, 2026-09-06), which is why its FG word reads
> `ERR_FAILED ERR_NOT_FOUND`: nothing to load.
>
> **LANDED 2026-09-06, 10:39–11:36 (`spike-notes` §9): N1, N2 and N3 as pre-committed, N4 in a shape the
> row did not foresee, and one correction.** N3 first, because it decides: the standalone probe on Lies of
> P with upscaling OFF read `SR 0x5` — `CREATED` clear — and with DLSS on `0x605`; **the rung stands.** N1:
> Lies of P, Expedition 33, Hell Is Us and the new Cronos: The New Dawn demo all print
> `upscaler: Dlss (driver-reported: …)` where they printed `N/A`. N2: Onimusha and DL:TB print
> `upscaler: Dlss — the NVIDIA driver agrees`. N4 arrived as DL:TB at **FSR upscaling + DLSS Frame
> Generation**: `upscaler: FSR (3.1 or 4 …)`, `2560x1440 -> 2560x1440`, `SR 0x205` — CREATED without
> EVALUATE, so the driver's word correctly does NOT claim DLSS beside FSR (the rung needs both bits).
> **The correction:** the FG word is NOT blind to DLSS-G. It read `CREATED | EVALUATE` on Hell Is Us,
> Onimusha and DL:TB at ×4, and *"the words CHANGED between readings"* on all three — the morning's
> `INITIALIZED | DLL_EXISTS` on Hell Is Us was a first reading taken before the feature came up.
> `FG_MODE` / `FG_MULTI_FRAME` stay clear and `fgCount` is 0 at ×4, so it names the feature and never the
> multiplier: printed beside `frame generation:` as agreement, promoted to `DlssG (driver-reported)` only
> where the count is active and no tag named the technology. Also on this run: Cronos is a **second
> Streamline 2.8.0 title** (UE, FG off, `sl.dlss_g.dll` absent from its census — per-setting there — `FPS
> 105.01`, `none` with DXGI agreeing); its FG-on capture would say whether the 2.8.0 pacer bypass is the
> SDK's or Techland's. And the FSR + DLSS-G session's factor was refused (bucket 8 of 8 at 1.96 against
> 2.85 — the guard working) while the qualifier said *"no evaluation was observed"* beside 4,415 tokens; the
> qualifier now says the factor was counted and refused, and that DXGI counted 8,174 presents this hook
> never saw so the Displayed rate is above the printed one.
>
> **The last row, 12:17 — Cronos with frame generation ON: `86.8 → 172.72 (×1.99 FG)`, `unseen=0`, `DlssG`
> with the driver agreeing.** The 2.8.0 pacer bypass is Techland's, not the SDK's. Closed above.
>
> ### THE STREAMLINE DOCS, READ 2026-09-05 (v2.8.0 and main), AND WHAT THEY CHANGED
>
> The owner asked for the vendor's own guides before any rewrite. Three things in them decide this item.
>
> 1. **DLSS-G presents through the "SL pacer"** (DLSS-G guide §14.1): an asynchronous presentation
>    mechanism, with *"specialized hardware to delay the image after Present() has been called"* and a
>    note that `MsBetweenPresents` in third-party tools *"indicates when the internal Present() call has
>    happened"*. So the generated frames ARE presented by internal `Present()` calls on 2.7.1 — which is
>    what this hook counts on Cyberpunk — and whether 2.8's pacer on Blackwell still reaches the body we
>    patch is exactly what Leg 1's PresentMon half measures. The docs do not say; they say FrameView 16.1
>    is the recommended tool because it reads `MsBetweenDisplayChange`, i.e. the display side.
> 2. **`slDLSSGGetState` is the documented way to get the presented count** (§14.0: `actualFPS = myFPS *
>    state.numFramesActuallyPresented`) — and it is **refused here**, with the reason in `03_METRICS` §FG:
>    the header defines the field as *"number of frames presented since the last `slDLSSGGetState` call"*,
>    so every call RESETS the plugin's counter, and a title that reads it for its own FPS display would
>    read our calls' remainder. It is also marked *"NOT thread safe"*. A measurement that alters the
>    host's measurement is not observing.
> 3. **§5.0 "Tag all required resources": DLSS-G requires the HUD-less and UI buffers to be tagged every
>    frame** through `slSetTag` / `slSetTagForFrame` / `slEvaluateFeature`'s inputs — the three routes this
>    build already hooks and read ONE tag type from. **Built the same day:** every tag's type is recorded
>    (`FlWriterState.slTagCensus`, per route), a HUD-less or UI tag drained by a present marks it
>    `FL_FG_DLSS_G`, and the consumer names `DlssG` beside an active count. Identity, not a count: the
>    count still decides `none`, so a title tagging the inputs with the feature off prints `none` with the
>    inputs noted, and the withheld shape keeps its N/A.
>
> **Pre-committed for the next run, per title:** Cyberpunk at DLSS + MFG → `frame generation: DlssG`,
> tag census `global=[depth, mvec, hudless, ui-color-alpha, …]` (2.7.1 tags globally; the scaling input
> arrives locally). Hell Is Us / Expedition 33 / Wukong at DLSS + FG ×4 → `DlssG` **if** the UE Streamline
> plugin tags through the exports (their `UpscalerParams = 0` says no scaling-input tag arrived, which is
> consistent with NGX-direct super-resolution and says nothing about the DLSS-G inputs); if the census
> reads `global=[-] frame=[-] local=[-]` on those titles, they tag through neither export and the identity
> stays `Active (technology not identified)` honestly. DL:TB at DLSS FG ×4 → the census line says whether
> the title feeds DLSS-G through a route this build sees; the qualifier then says *"FEEDING frame
> generation"* beside the withheld count. Lies of P at DLSS (no Streamline tokens at all) → unchanged:
> the super-resolution identity on NGX-direct titles is what the NVAPI probe above is for.
>
> **LANDED 2026-09-05 evening, on the owner's run on #121 (`spike-notes` §9):** `frame generation: DlssG` on
> Hell Is Us (×4, twice), Expedition 33 (×1.97), Wukong (×4), Rune Factory (×3.98) and Cyberpunk — every
> Streamline title tags the DLSS-G inputs through a route this build hooks (the UE plugin and Cyberpunk 2.7.1
> globally, DL:TB 2.8 through `slSetTagForFrame`), so the identity is measured six of six and *"technology
> not identified"* is gone from all of them. DL:TB's withheld line now says *"FEEDING frame generation"*
> beside the count it cannot make; its census also shows a `scaling-in` tag on the frame route with
> `UpscalerParams` still 0 — the tag carries no size on this title, which closes 7a's last question without
> the layout bump it was thought to need. Cyberpunk's factor was refused on that run (bucket 1 at 3.0 against
> 1.67 overall, `tokens/batch 2.97`): a mixed session, refused as designed rather than averaged. **What is
> left on NVIDIA is one thing: `upscaler: N/A` on the NGX-direct titles** — the probe above.
>
> **BUILT 2026-09-05, late — A1.7 promoted from "not in A1" to the in-process half of Leg 1.** The owner
> read that Streamline ≥ 2.8 "needs `sl.interposer.dll` and `sl.common.dll` loaded before an injection
> succeeds". Checked against the source, not the claim: the interposer's DXGI proxy
> (`source/core/sl.interposer/d3d12/dxgiSwapchain.cpp`) is **byte-for-byte the same file at v2.7.32 and
> v2.8.0** — plugins' before-hooks receive `m_base` and may `skip` the base call, exactly as before — and
> 2.8's `sl.common` gains `resourceTaggingForFrame.cpp` (the `slSetTagForFrame` route #121 already hooks).
> Injection into DL:TB already succeeds (every capture there has `streams=1`, a full `runtime census`, a
> tag census), so "loaded before injection" is a launcher's problem, not this hook's. What 2.8 does NOT
> show is where its closed `sl.dlss_g` pacer presents. So the Overlay now asks DXGI itself: on every hooked
> present it reads `IDXGISwapChain::GetLastPresentCount` on the chain it just saw (rule 4: the same object
> `FindOrAdd` already calls `GetDesc` on) and publishes two words on the 1 Hz watchdog —
> `dxgiPresentSamples` (hooked presents that read it) and `dxgiPresentsUnseen` (presents DXGI counted
> between two hooked ones, beyond the one seen). The report prints
> `DXGI present counter: unseen=M over samples=N (r unseen per hooked present)` and the withheld qualifier
> carries the reading. The harness's own presents are the negative control (`fl_guard` `[fg]`:
> `unseen == 0`, `samples >= drained`); the positive cannot be fixtured — nothing in this repo presents
> through a body the inline patches miss — which is why the rows below are written before the run.
> Cost: one virtual call per hooked present, measured by `hook-harness --probe-frames` (number in the PR
> body); `--probe-cost` is the empty-detour floor and does not include it.
>
> Leg 1's in-process rows, read off the new line on a DL:TB DLSS FG ×4 capture (r = unseen / samples):
>
> | Row | Reading | Meaning | Action |
> |---|---|---|---|
> | **P1-DXGI** | r ≈ 3 (×4) — `unseen ≈ 3 × samples` | the pacer presents on the SAME chain through a body the inline patches do not cover; the generated presents ARE DXGI presents | the next PR promotes `Displayed = hooked + unseen`, labelled **DXGI-counted** (a count DXGI made, not this hook), `presents/batch` from it, and `none` withdraws by count; the pacer's entry is then localised (a second `CDXGISwapChain` class, or a path that bypasses `Present`'s body) at leisure |
> | **P3-DXGI** | r ≈ 0 | DXGI counted nothing this hook did not: the generated frames are displayed below DXGI (driver pacing / flip metering) or on an object this hook never saw | the withheld wording stays; no in-process sink exists; only PresentMon (kernel) and the NVAPI corroboration remain, and if PresentMon also reads 1× the frames never were kernel presents |
> | **P5-DXGI** | 0 < r < 2.5, or r ≠ 0 with `streams > 1` | a mixed path or a second chain | stop and localise; the number is printed, never averaged into a factor |
> | **P2/P4** | r ≈ 0 with ours ≈ 4× tokens, or the count itself moved | as the kernel rows say: not generating, or the title changed | the kernel rows above decide |
>
> **LANDED 2026-09-05 night, on the owner's run on #124 (`spike-notes` §9): row P1-DXGI, twice.** DL:TB at
> DLSS FG ×4 read `unseen=12204 over samples=4136 (2.95)` at 21:56 and `unseen=12741 over samples=4392
> (2.90)` at 22:06 — DXGI counted 3.9× the presents this hook did, on the SAME chain, which is the owner's
> ~300 against ours ~75 from the afternoon, now measured in-process. The two controls read 0.00: Hell Is Us
> (Streamline **2.7.3**, `79.89 → 308.14 (×3.86)` through the patched bodies, 18,207 samples) and the
> owner's new title, the **Onimusha: Way of the Sword demo on Streamline 2.10.3** (`60.04 → 220.33
> (×3.67)`, `DlssG`, `upscaler: Dlss`, 12,914 samples, 0 unseen). So **2.8.0's pacer is the odd one** —
> 2.7.x before it and 2.10.x after it present the generated frames through `Present`'s body — and the
> "`sl.interposer.dll` + `sl.common.dll` must be loaded on ≥ 2.8" lead is closed by 2.10.3, which injects,
> tags on the frame route and counts exactly like 2.7. **The pre-committed action is built:** the record
> carries `dxgiUnseen` (@52, `FL_MEASURED_DXGI_PRESENTS`), the window counts `Displayed = hooked +
> unseen` and prints *"Displayed is DXGI-COUNTED"* on its own line, `presents/batch` takes the same
> numerator, and `none` withdraws on DL:TB by count (`03_METRICS` §Displayed may be DXGI-COUNTED). Expected
> on DL:TB's next capture: the trio at ≈ ×3.9 with the label, no withheld line, `frame generation: DlssG`.
> Which body the 2.8.0 pacer reaches stays unlocalised, as the row said.
>
> **LANDED 2026-09-06 morning, on the owner's run on #125 (`spike-notes` §9): the promotion, and Leg 0.**
> DL:TB at DLSS FG ×4 (07:31): `Native FPS: 70.52 -> Displayed FPS: 282.08 (x4 FG) over 16516 present(s)`,
> *"Displayed is DXGI-COUNTED: 12387 of those present(s)"*, `unseen=12483 over samples=4202 (2.97)`,
> `frame generation: DlssG`, uniform across every bucket, no withheld line — the trio rule 6 asks for, on
> the title that could not print it. **Leg 0, twice (07:45, 07:55): frame generation OFF**, census still
> `0x25F0F` — `sl.dlss_g.dll` is a **startup-time load**, the cost paragraph's fear measured true —
> presents = tokens, `unseen=0 over samples=3659` and `4519`, tag census `0x23600` (depth, mvec,
> scaling-in, scaling-out, other: **no hudless / UI tag with FG off**, so the DLSS-G identity is
> per-setting on this title too), and the report withheld `none` exactly as pre-committed. **The gate is
> narrowed rather than withdrawn:** DXGI's counter is the discriminator it lacked — the 2.8.0 pacer's
> presents are DXGI presents on this chain (P1-DXGI), so a counted 1.0 beside a READ counter with zero
> unseen is `none`, printed with DXGI's agreement; withheld only when the counter was not read. Control:
> Expedition 33 FG off (Streamline 2.7.30) reads `FPS: 85.86`, `none` by count, tag census `0x0` — the UE
> plugin sends no tags at all with FG off.
>
> Leg 1's rows (ours = `presents / span`; PM = PresentMon rows / span; C = the counter; ≈ = within 5 %):
>
> | Row | Reading | Meaning | Action |
> |---|---|---|---|
> | **P1** | PM ≈ 4× ours, C ≈ PM | generated presents reach the kernel on a path **outside** the `CDXGISwapChain::Present/Present1` bodies the Overlay inline-patches — case 3 realised | gate stays; a second present sink is costed (candidates below) and the route is the owner's; PresentMon's `SwapChainAddress` count says whether it is a second chain (2 addresses against our `streams=1`) or the same chain by another entry (1) |
> | **P2** | PM ≈ ours ≈ C ≈ 1× | the title was not generating despite the setting | **withdraw the gate** (keep the `module:` lines); `none` was right; case 3 stays unrealised; the owner checks the setting |
> | **P3** | PM ≈ ours ≈ 1×, C ≈ 4× | frames displayed without a kernel-visible present per frame (driver pacing / flip metering), or the counter reports a target rather than a measurement | keep the wording ("Displayed unknown"); no in-process or kernel sink would help; only a vendor read could corroborate |
> | **P4** | ours ≈ 4× tokens on the rerun | the five earlier runs were not generating, or the title changed — compare the `module:` versions and the exe fingerprint against the 00:53 run | withdraw the gate; localise what changed |
> | **P5** | PM ≈ 2×, or two `SwapChainAddress` values with ours `streams=1` | a second chain or a mixed path | stop and localise before deciding |
> | **P6** | elevation refused / a leg unrun | nothing measured | not a result — P2 is not inferred from silence |
>
> Leg 0's own row: if `sl.dlss_g.dll` is **absent** with FG off, the census bit is per-setting on
> this title and the gate is sharper than its cost paragraph feared (consider dropping the version
> conjunct); if present, the interim cost stands as written. **Landed 2026-09-06: PRESENT, twice** —
> and the cost stood for one morning, because the DXGI counter (above) narrowed the gate the same day.
>
> **Second-present-sink candidates for P1 / P5, one line each, none chosen:** MinHook inline
> patches on the `gdi32.dll` D3DKMT thunks (`D3DKMTPresent`, `D3DKMTPresentMultiPlaneOverlay3`,
> `D3DKMTSubmitPresentToHwQueue`) — in-process, no elevation, argument reads only, but
> semi-documented structs that move between Windows builds and more patched bytes on the most
> anti-cheat-sensitive path; an ETW `Microsoft-Windows-DxgKrnl` consumer in the capture host
> (what PresentMon reads) — exact per-pid kernel count, costs **elevation**, which is §G's Tier-2
> question re-entering; an `IDXGIFactory2::CreateSwapChain*` hook (`17_HOOK_ENGINE` ⏳) — sees a
> second chain at creation only, i.e. launch mode (§S1); in-hook `IDXGISwapChain::GetLastPresentCount`
> sampling — a same-chain localiser only, and a hot-path virtual call with two reserved words
> behind it. NVAPI has no present count outside present barrier; `NvAPI_NGX_GetNGXOverrideState`
> (R570, by PID) reports the driver's DLSS override state and multi-frame target, which is
> corroboration rather than a count — an optional `fl-probe-nvapi --ngx-state <pid>` reading on
> Legs 2 then 1, never a gate input.

> ### NARROWED, NOT CLOSED — five real-title captures, 2026-08-15
>
> **What is now measured.** Cyberpunk 2077, SL 2.7.1, RTX 5080, D3D12, 2560×1440, DLSS
> Balanced, Ray Reconstruction on. Five 40 s captures at four frame-generation settings,
> Overlay payload hash-verified against the just-built DLL before each run:
> **off → `presents/batch` 1.000 · ×2 → 2.000 · ×4 → 4.000** (×4 three times independently).
> Every run: one identified swapchain, one segment, 0 gaps, 0 dropped.
>
> **What that settles.** The presents a multi-frame-generation configuration adds are
> **visible to a hook on `dxgi.dll`'s shared class vtable**. The ratio tracks the configured
> MULTIPLIER, not merely the on/off state — which a two-point sweep could not have shown,
> and which is why §S30's oracle paragraph pre-committed three points. `fg_factor`
> structurally 1.0 on every Streamline title, the outcome this entry exists to fear, does
> **not** occur here.
>
> **What it does NOT settle, and the distinctions are load-bearing:**
>
> - **Case 3 is narrowed, not answered.** Nothing went missing *in aggregate*, but these
>   runs cannot separate "the interposer forwards through the chain we already see" from
>   "the interposer presents on its own chain that our shared-vtable patch also catches".
>   One identified stream in every run is what makes the second reading unlikely rather than
>   excluded.
> - **Case 2 is made WORSE by this result, not resolved by it.** If three of every four
>   presents at ×4 originate in the vendor's swapchain, then ~75% of records carry
>   `syncInterval` / `presentFlags` that **no application call produced** — and `dllmain.cpp`
>   sets `FL_MEASURED_PRESENT_ARGS` on every one of them unconditionally, as "the one thing a
>   DXGI present hook always has". Nothing has compared observed present args against what
>   the title passed. Any consumer of those two fields inherits this.
> - **`presents/batch` is a PROXY and its denominator is unverified.** A batch is "a present
>   that drained a Streamline evaluation". It equals an application frame here only because
>   Ray Reconstruction is evaluated once per application frame on this title, which no
>   independent oracle has confirmed — `fl-baseline-probe` at the three settings and the
>   game's own frame counter are both pre-committed in §S30 and both **unrun**.
> - **Scope.** One title, one SL build, one GPU, D3D12, RR **on**, MFG only. Says nothing
>   about RR-off, about other SL 2.x builds, about SL 1.x titles (refused before any hook is
>   installed), or about XeFG and FSR3-FG.
>
> **A separate finding from the same runs, which belongs to HANDOFF item 3 rather than
> here:** `slEvaluateFeature(kFeatureDLSS_G)` is **never called** — 0 across ~14,000 batches.
> `UNDECODED` is also 0, and that zero is now meaningful rather than merely observed: the
> injected `--hold-presenting-upscaled-unknown` case asserts the bucket reads non-zero for an
> id outside the decoded set, proven red. So a vendored `kFeatureDLSS_G` constant not matching
> the runtime id is excluded, and the honest statement is that the id does not reach the
> hooked export. **Where frame generation IS driven from was not measured** — an absence at
> one export of one module does not locate a mechanism, and the NGX tier
> (`NVSDK_NGX_D3D12_EvaluateFeature`, seven exporting modules) is hooked nowhere.

**Partly answered, and the news is better than expected.** `hook-harness
--probe-proxy` builds a real forwarding `IDXGISwapChain` wrapper — what
`sl.interposer` and ReShade hand the application — and presents through it with
our hook on the **real** vtable. The hook still fires: the proxy forwards via
`real_->Present(...)`, an ordinary virtual dispatch, so patching the real vtable
catches it one layer down. A proxy having its own vtable does *not* by itself
make us miss the present.

> **Case 3 is now probed, bounded, and blocked on a LICENCE rather than on
> hardware** (2026-08-05, `fl-probe-interposer`, `spike-notes.md` §5). Loading a
> real `sl.interposer.dll` — Cyberpunk 2077 and Black Myth: Wukong — into our own
> process shows it forwards to `dxgi.dll` and leaves both the factory and the
> swapchain vtable untouched **until `slInit()` runs**, which the probe proves by
> enumerating its own modules and finding no `sl.*` plugin mapped. So the
> comparison is currently measuring passthrough, and the probe reports
> **inconclusive** rather than a verdict.
>
> Recorded because the first version of that probe did **not**: it printed "the
> vtable is THE SAME — a hook DOES catch Streamline presents" for both titles,
> which would have closed §H5 case 3 on a measurement of nothing. The tell was in
> its own output — an interposing Streamline cannot leave the *factory* vtable
> unwrapped, since wrapping the factory is how it reaches the swapchain.
>
> ~~Reaching the wrapped path needs `slInit`'s `sl::Preferences`, i.e. vendor ABI,
> i.e. the question `legal/THIRD_PARTY_NOTICES.md` answers for Intel IGCL and
> nobody has asked for NVIDIA.~~ **Unblocked by #64 and MEASURED 2026-08-14.**
> Streamline is MIT and `sl::Preferences` is vendored, so `fl-probe-interposer` now
> calls `slInit` → `D3D12CreateDevice` *through the interposer* → `slSetD3DDevice`,
> every entry point resolved with `GetProcAddress`. **The premise holds:** with
> `slInit` returning `eOk`, the interposer hands back a swapchain whose vtable is
> inside `sl.interposer.dll` while `dxgi.dll`'s own route yields `dxgi.dll`'s.
> Reproduced on Alan Wake 2 (SL 2.7.0) and Cyberpunk 2077 (SL 2.7.1), RTX 5080.
> **The risk is also narrower than the entry
> implies:** NGX-direct titles never wrap the swapchain, so the vtable premise is
> only in question for Streamline-shimmed ones.
>
> > **What that does and does not settle, because the difference is the whole
> > metric.** It settles that the game's swapchain is **not** an instance of the
> > class whose shared vtable we patch. It does **not** settle whether we miss the
> > present: §H5's own `--probe-proxy` result stands — a *forwarding* proxy calls
> > `real_->Present(...)`, an ordinary virtual dispatch, caught one layer down.
> > **Different class ≠ missed present.** Deciding which needs presents driven
> > through this proxy with our hook installed. No feature plugin reported loaded in
> > these runs, so the DLSS-G question — whether GENERATED presents reach the same
> > vtable — is untouched.
> >
> > **A second finding, which cost a crash to get.** The Witcher 3 ships
> > `sl.interposer.dll` **1.5.6**, a different API generation: it exports
> > `slGetHooks`, `slIsFeatureEnabled` and `slSetFeatureConstants`, and exports
> > **neither** `slSetD3DDevice` nor `slIsFeatureLoaded`. `slInit` exists in both
> > with a **different `sl::Preferences` layout**, so calling it with the vendored
> > 2.x struct access-violates (`0xC0000005`). The probe now version-guards on the
> > SL2-only exports and skips with a reason.
> >
> > **That generalises past the probe and touches the hook inventory.**
> > `docs/vendor-exports.json` records **one copy per module name**, so its
> > `sl.interposer.dll` is one machine's 2.7.4 and says nothing about a 1.5.6 a title
> > may ship. `hookinventory-check` Pass A would pass `slEvaluateFeature` against
> > such a title — the **name** exists in both generations — while the **signature**
> > differs, so a detour typed with the 2.x `PFun_slEvaluateFeature` would read 1.x
> > arguments. Today's hook reads only `feature`, the first argument, and is
> > probably unharmed. **Item 2b's plan to walk `inputs`/`numInputs` is not**, and
> > needs a version guard of its own before it dereferences anything.
>
> What the probe *did* settle is the premise underneath everything else, and it
> is now ctest `fl_vtable_identity_control`: two independently created swapchains
> share one vtable, and a different interface does not — both directions, so a
> regression in either fails the build.

**What still has to be measured on a real title:**

1. A forwarding proxy is the easy case. DLSS-G presents *interpolated* frames
   the application never submitted — whether those reach a real-vtable hook is
   what actually matters for the FG counting in `03_METRICS` §Frame Generation,
   and the harness cannot simulate it.
2. We observe the post-proxy call, so parameters the proxy rewrote are what we
   see, not what the game passed.
3. A proxy that owns a *different* swapchain, rather than wrapping ours, would
   still be invisible.

Needs a real Streamline/DLSS-G title, ideally with RTSS and the Steam overlay
also active (`spike-notes.md` §Environment — that machine already has both).

### H6 ✅ · D3D12 command-list hooks count recorded, not executed, work — **answered 2026-08-20 with the RT hooks** *(marked 2026-10-04: per-present atomics drained once per present; the unit is "recorded between two presents", `03_METRICS` §RT)*

`DispatchRays`, `BuildRaytracingAccelerationStructure` and pipeline creation are
recorded into command lists, possibly on many threads, possibly re-executed or
never executed. `17_HOOK_ENGINE` §Ring writer keeps per-frame counters in "a
small struct updated by the feature hooks and *read* by the present hook" with no
synchronisation specified — a data race as written, and semantically it counts
recording rather than execution.

**Needs:** a specified concurrency model (per-thread counters aggregated at
present is the obvious one) and an explicit statement that RT activity means
"recorded this frame", with the accuracy budget in `03_METRICS` adjusted to say
so.

### H7 ✅ · Vtable restore on unhook clobbers later hookers — **closed**

Fixed and specified in `17_HOOK_ENGINE` §Compare-and-restore, never
unconditional restore. Verified by `hook-harness --probe-unhook`, ctest
`fl_unhook_preserves_foreign`.

The "cleaner uninstall" claim was backwards: a later hooker saves **our detour**
as its original and chains through it, so writing the pristine address back
removes their hook silently. Both halves of the contract are asserted — we
decline to restore when the slot changed, **and** we do restore when it did not,
because a compare-and-restore that never restores is not a fix.

Simulated rather than depending on RTSS being installed, so it is deterministic
and runs on CI. Confirming against the six overlays actually resident on the dev
machine (`spike-notes.md` §Environment) is still worth doing once the Overlay has
real hooks, but the mechanism no longer rests on that.

### H8 ✅ · "Never crash the game" — **closed**

NFR-3 reworded in `02_SPEC` to what the mechanism can actually support: faults
*originating in our own hook bodies* are contained, counted and self-disable
after three. The absolute promise is gone, and stays out of user-facing text —
the Disclaimer's existing "injection carries risk" wording is the honest one.

Recorded alongside it, because it is the sharpest instance: `-D_HAS_EXCEPTIONS=0`
turns a would-be STL throw into `__fastfail`, which SEH cannot intercept
(`spike-notes.md` §H3). The "no throwing STL in hook paths" rule is therefore
load-bearing rather than stylistic.

