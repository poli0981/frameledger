<!-- Moved verbatim from CHANGELOG.md on 2026-10-19 (beta.24, owner decision D66): part 4 of 5 of the ledger kept before the first tag, newest first. CHANGELOG.md lists every part. -->

## Ledger before 0.1.0-beta.1 (continued, part 4 of 5)

### Added

- **`capture` can be bounded, because a real-title measurement otherwise could not end.**
  `CaptureLoop` honoured `CaptureOptions.MaxDuration` and tests covered it — **nothing could
  set it**, so `capture` against a running game ran until the game was closed, and the report
  only prints at the end. `--seconds <n>` is a **duration bound, not a safety bypass**: the
  options this host refuses (`--pid`, `--payload`, `--force`, `--yes`) each widen *what* is
  injected or skip a check, while this only shortens an already-consented, already-guarded
  session. A non-positive or non-numeric value is an **error, never a silent 0** — 0 means
  *unbounded*, so leniency would produce the opposite of what the operator asked for.
  `TheAcceptedOptionsAreExactlyThese` went red, which is the gate working.
- **The session report could not tell "no hook ran" from "the hook came up late".** Both
  produce N/A and the same wording, and against Cyberpunk 2077 it said *"no upscaler hook
  ran"* while the hook was live for 97% of a 10,169-present session — feature hooks install
  lazily on a 1 Hz watchdog, and the consumer requires the bit on *every* record. The host now
  prints `hooksInstalledMask`, `apiMask`, `rtTier`, the per-bit record counts, and the modal
  raw `renderW/H` / `quality` / `upscaler`, so a partial count reads as *came up mid-session*
  and a verification run can be checked against the game's own settings file.

- **`upscalerQuality` gets a real preset, and `sl_dlss.h` is vendored with its consumer** —
  the last piece of `docs/HANDOFF.md` item 2b that could be built. The same bounded `inputs`
  walk now also matches `sl::DLSSOptions` and reads `mode`.
  - **Vendored the way the process requires, not approximately.** Upstream
    `NVIDIA-RTX/Streamline@main`, 8,489 bytes, **CRLF preserved**,
    `git hash-object 3aac47be62c9322aef119b88926602d37655d3ed`. Its include closure is
    **empty** — verified on the upstream file — so it adds no other header. None of
    `18_GPU_VENDOR_APIS` §Checklist step 2's four needles appear in it, so step 1 applies and
    step 3 is unreachable. Notices, the vendoring README and `17_HOOK_ENGINE` are corrected in
    the same commit: nine headers → ten.
  - **`DLSSMode::eOff` is 0, and `upscalerQuality` reserves 0 for "nobody looked."** Storing
    the vendor enum verbatim would make *"the title turned DLSS off"* and *"no hook ran"* the
    same byte — and 0 decodes as NGX MaxPerf, i.e. it would publish **"DLSS Performance"** as
    a measurement. `QualityFromMode` maps `eOff` and anything at or beyond `eCount` to `0xFF`.
    **Exactly the collision `D3D12_RAYTRACING_TIER_NOT_SUPPORTED` had against `rtTier`**, in a
    different vendor's enum, resolved the same way: at the writer, while the two are still
    distinguishable.
  - Asserted **exhaustively** rather than by example: no `DLSSMode` value, in range or out,
    may map to 0. A single mapping mistake publishes a wrong preset rather than an absence.
  - The walk no longer returns on its first hit. Extent and quality arrive in **different**
    structures and a title may chain them in either order, so an early return made quality
    depend on chain order — a property of the title, not of what it is doing.


- **The LOCAL tags too — `slEvaluateFeature`'s `inputs` walk, hardened against input a title
  should never send.** `sl_core_api.h:258` is explicit that buffer tags passed to
  `slEvaluateFeature` are *"local"* and *"do NOT interact with same tags sent in the global
  scope using slSetTag API"*. The two are alternative integration styles, not layers, so a
  locally-tagging title yields nothing from the `slSetTag` hook and vice versa. Both are now
  read; the local one wins when present, because it is scoped to the evaluation rather than to
  the viewport and cannot be older than the frame being measured.
  - **In a header, `fl_sl_inputs.h`, precisely so it can be tested without a game.** Every
    branch dereferences caller-supplied pointers, and a fault lands in `FL_HOOK_GUARD` and
    burns one of the three that self-disable the Overlay — so a malformed input that faults is
    a **bug**, not degradation. `ctest fl_sl_inputs` drives 12 shapes in microseconds: null
    array, zero count, null element mid-array, wrong buffer type, wrong struct GUID,
    `structVersion` below `kStructVersion1`, whole-resource extent, a tag reached through
    `next`, a **self-referential** `next`, a **two-node cycle**, and a `numInputs` of
    `0xFFFFFFFF`.
  - **The cycle handling is the depth cap, and that is deliberate**: a visited-set would
    allocate, which a hook path may not. A cycle is worse than a fault — no exception, no
    self-disable, just a frozen game with our DLL in it.
  - **What the input cap does not buy, stated rather than implied.** It turns
    `numInputs = 4 billion` into 32 reads. It does **not** protect against a count of 5 with a
    2-element array: nothing in the ABI carries the allocation's length, so that direction is
    unprotectable from here. Same for a struct whose GUID matches but whose allocation is
    short. Both are the vendor's contract to keep, and both are written down.

- **`FL_MEASURED_UPSCALER_PARAMS` gets its first producer: render resolution, from the global
  resource tags** — `docs/HANDOFF.md` item 2b, and one of the five values P0 exit criterion 1
  names. A second inventory row detours `sl.interposer.dll!slSetTag`, reads the
  `kBufferTypeScalingInputColor` extent, and publishes `renderW/H`.
  - **`slSetTag` was scheduled for deferral and is included instead**, on measured grounds:
    `sl_core_api.h` documents the local tags in `slEvaluateFeature`'s `inputs` as merely
    *allowed* — *"they do NOT interact with same tags sent in the global scope"* — and only
    **four of the ten** Streamline titles installed here route DLSS super-resolution through
    Streamline at all. An inputs-only producer could have shipped with a hit rate of zero.
  - **The installer had to be restructured first, and it was a latent mis-bind.** The
    expansion over `FL_HOOK_INVENTORY` ignored the `family` column, stopped at the first row
    that resolved, and hooked it with `Hook_SlEvaluateFeature`. Correct with one row; with two
    it would have detoured `slSetTag` with a body that reads argument 1 as a feature id. Each
    row now carries its own detour, family bit and latch, and a row whose family has no detour
    installs **nothing** rather than borrowing a neighbour's. The install-after-stop guard is
    factored out of the single installer for the same reason.
  - **Two conditions to publish, and the second is the one that is easy to drop:** the params
    hook live, **and** an evaluation seen *this frame*. A tag is viewport state that outlives a
    frame, so publishing on the tag alone would report a render resolution for every frame
    after a title stopped upscaling — stale state dressed as a measurement.
  - `upscalerQuality` is **`0xFF`, never 0** — 0 is NGX MaxPerf, a real preset, so it would
    publish "DLSS Performance" as a measurement. `upscalerSharpness` is `0xFF` **permanently**:
    `DLSSOptions::sharpness` is deprecated as unsupported and `optimalSharpness` is
    Streamline's recommendation, not what the title applied.
  - **`claimedParams == 0` is inverted, not deleted** — that was the honest assertion while
    `renderW/H` had no source, and it is the line a reviewer should see change. It now asserts
    the **exact** tagged extent, the honesty invariant on every claiming record, and the
    reverse direction (a value set while the bit is clear).
  - Two fixture bugs the test found rather than reasoning: tagging **once** at startup landed
    before injection and left the bit set on **0 of 43** records — and `eValidUntilPresent`
    means a real title re-tags every frame anyway, so tagging once was simply wrong about the
    vendor contract. And a floor of `> size - 3` hid a real 6-record window where identity was
    live and params was not; the drain now waits for **both** families and asserts equality.
  - Proved red by canary: hardcoding a plausible 1920×1080 trips `wrongExtent` on all 40
    records, and it compiles.
  - No `#pragma warning` for C4996 on the deprecated `slSetTag`, and that is measured:
    `/std:c++20` **without** `/Zc:__cplusplus` makes MSVC report `199711L`, so the
    `#if __cplusplus >= 201402L` guard on the attribute never opens. A pragma would suppress a
    warning that is not emitted. The condition that would change it is recorded at the
    declaration.

### Fixed

- **The Overlay would hook a Streamline module of the wrong generation, and read its
  arguments through the wrong signature.** The inventory scopes `slEvaluateFeature` to
  `sl.interposer.dll` — correct, and not enough. **The Witcher 3 ships that module at
  1.5.6**, a different API generation: it exports `slInit`, `slEvaluateFeature`, `slSetTag`
  and `slShutdown` so every name check passes, plus `slGetHooks`, `slIsFeatureEnabled` and
  `slSetFeatureConstants` which Streamline 2 does not have, and **none** of
  `slSetD3DDevice` / `slIsFeatureLoaded` / `slGetNewFrameToken`.
  - **The name survived the version bump and the signature did not**, which is why no gate
    saw it. `docs/vendor-exports.json` records one copy per module *name*, so
    `hookinventory-check` Pass A resolves against one machine's 2.7.4 and says nothing about
    a 1.5.6 in a game. A detour typed with SL2's `PFun_slEvaluateFeature`, called with SL1's
    argument list, reads argument 1 as a feature id when it is not one — a **wrong upscaler
    name**, not a crash, which `17_HOOK_ENGINE` calls the highest false-confidence risk in
    the spike.
  - `ResolveScoped` now refuses a `sl.interposer.dll` that does not export the three
    SL2-only entry points. No hook is installed, `FL_HOOK_UPSCALER_IDENTITY` is never
    published, and the record says `FL_UPSCALER_NOT_REPORTED` — true, rather than a guess.
  - **Scoped to the interposer by name, and the first version got that wrong.** Those three
    are *interposer* exports; a real `sl.common.dll` is a plugin and exports none of them, so
    an `sl.*` prefix test refused modules whose generation it had no business judging. The
    existing decoy fixture caught it.
  - New fixture `stub_sl_interposer_v1.cpp` — right module name, right symbol name, SL1 ABI —
    and `ctest fl_sl_abi_guard`. It is the complement of the decoy: scoping catches a wrong
    module *name*, and cannot see a wrong *signature*. Its own ctest and its own process,
    because both fixtures are called `sl.interposer.dll` and `GetModuleHandleExW` resolves by
    name, so sharing one would have the two silently test each other.
  - Proved red by canary: neutering the check leaves exactly one assertion failing, and it
    compiles, so it is a canary rather than a build failure wearing one.
  - The SL2 stub gains the three markers, so it honestly looks like the generation it stands
    in for. Found because the guard correctly refused it otherwise.

### Added

- **`FlWriterState.rtTier` gets a producer, and the vendor enum it copies had a collision in
  it** — `docs/HANDOFF.md` queue item 4, the one conjunct that needs no hook. `ResolveApi`
  already obtained an `ID3D12Device*` on the first present of a D3D12 swapchain and released
  it three lines later without asking it anything; it now asks
  `CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS5)` first. No hook, no MinHook, no vtable —
  a capability query on a device DXGI handed us for a swapchain we were called on.
  - **`D3D12_RAYTRACING_TIER_NOT_SUPPORTED` is 0, and `rtTier`'s 0 already meant NOT
    QUERIED.** Storing the enum verbatim — the obvious implementation, and the one
    `fl_shm.h`'s own field comment described — would have published "nobody looked" about
    every non-RT device: the affirmative-negative collision layout v3 exists to prevent,
    reached by copying a vendor enum rather than by a guess. New `FlRtTier` carries three
    states: `NOT_QUERIED = 0`, `UNSUPPORTED = 1`, and the D3D12 value verbatim otherwise.
  - **Measured against the Windows SDK header rather than remembered:** `NOT_SUPPORTED = 0`,
    `TIER_1_0 = 10`, `TIER_1_1 = 11`, **`TIER_1_2 = 12`** — the enum is already "tier ×10", so
    nothing multiplies it and nothing names the individual tiers. A tier newer than the SDK
    this was built against arrives intact instead of being clamped to what the build knew.
  - **Both directions are asserted, in `ctest fl_guard`.** The D3D12 case asserts the field
    holds a legal `FlRtTier` value; the D3D11 case asserts it is exactly `NOT_QUERIED`,
    because a writer that stored `UNSUPPORTED` unconditionally would pass the first on its
    own. What the D3D12 case does **not** assert is *which* tier: the fixture's device is
    WARP, and whether WARP supports DXR is the open question `HANDOFF` item 4 says to check
    rather than assume — so the value is `CAPTURE`d and the test records the answer instead
    of depending on it.
  - **It does not make RT reachable yet, and the consumer comment now says so precisely.**
    `MeasuredFacts.RayTracingOf`'s two conjuncts both have producers now, but
    `FL_MEASURED_RT` still has none, so RT is `N/A` on every session. The gap moved; it did
    not close.
- **`hookinventory-check` grows a third pass, over the one failure the other two cannot
  see** — and the document that already claimed this pass existed is corrected in the same
  commit. Passes A and B are source checks: they see what the Overlay *resolves*. Neither
  sees what it *links*. Taking the address of an `SL_API` declaration in evaluated code makes
  `sl.interposer.dll` a **load-time dependency** of `FrameLedger.Overlay.dll`, which then
  fails to load in every game that ships no Streamline — in the loader, before `DllMain`,
  with no message anywhere. **Pass C reads the binary's own dependency list** and fails on
  `^(sl\.|_?nvngx|libxess|ffx_|amd_fidelityfx)`.
  - **`src/native/third_party/streamline/README.md` had asserted this gate since
    2026-08-09.** It did not exist: the script's only mention of `dumpbin` was a comment
    about a different tool. Found by an audit that went looking for the code instead of
    trusting the sentence — the shape this project keeps hitting, and worse here because the
    failure Pass C catches has no symptom to notice.
  - **It refuses rather than passes whenever it cannot look.** A zero-length import list is a
    failure, not a clean result — every way that parse can break produces the same empty list
    as a binary with no vendor imports. The list must also contain `kernel32.dll` before any
    verdict is formed, the same discrimination rule the oracle probe already follows.
  - **It runs only under `-RequireBinaries`.** The first version read whatever binary was in
    the build tree, so `check -SkipNative` printed a skip line *and* ran the pass anyway,
    against an artefact the run did not produce. Reporting on the wrong binary is worse than
    saying nothing.
  - Proven on real PEs as well as fixtures: out of `AlanWake2.exe`'s 47 imports and
    `Cyberpunk2077.exe`'s 36, it names exactly `sl.interposer.dll` and — for Cyberpunk —
    `libxess.dll`, `libxess_fg.dll`, `ffx_fsr3_x64.dll`, `ffx_backend_dx12_x64.dll`, and
    nothing else. Self-test is 17 cases, both directions, including that the match is
    anchored so an innocent name merely *containing* a vendor prefix passes.
  - Pass B's stray-literal sweep gains `xefg[A-Z]`, which `xess[A-Z]` does not cover:
    `libxess_fg.dll`'s 31 measured exports include 28 `xefgSwapChain*` names and no `xess*`
    name at all. Widened before the FG hooks land rather than after.
- **§H5 case 3 gets an answer, and a second finding cost a crash to get** —
  `fl-probe-interposer` reported INCONCLUSIVE because it never called `slInit`, blaming a
  licence question over `sl::Preferences`. #64 vendored Streamline under MIT and removed that
  blocker; the probe now runs the sequence a real title runs — `slInit` →
  `D3D12CreateDevice` **through the interposer** → `slSetD3DDevice` — with every entry point
  resolved by `GetProcAddress`, never linked.
  - **MEASURED: the swapchain class is not ours.** With `slInit` returning `eOk`, the
    interposer hands back a swapchain whose vtable sits inside `sl.interposer.dll` while
    `dxgi.dll`'s own route yields `dxgi.dll`'s. Reproduced on Alan Wake 2 (SL 2.7.0) and
    Cyberpunk 2077 (SL 2.7.1). **It does not follow that we miss the present** — §H5's
    `--probe-proxy` result stands, a forwarding proxy is caught one layer down — and the
    probe says so rather than converting a premise into a verdict.
  - **The Witcher 3 ships Streamline 1.5.6, and it crashed the probe.** A different API
    generation: `slGetHooks`, `slIsFeatureEnabled`, `slSetFeatureConstants`, and **no**
    `slSetD3DDevice` or `slIsFeatureLoaded`. `slInit` exists in both with a different
    `sl::Preferences` layout, so the vendored 2.x struct access-violates. Now version-guarded
    on the SL2-only exports, skipping with a reason.
  - **That reaches the hook inventory.** `docs/vendor-exports.json` records one copy per
    module *name*, so its `sl.interposer.dll` is one machine's 2.7.4 and says nothing about a
    1.5.6 a title ships. Pass A would accept `slEvaluateFeature` against such a title — the
    name exists in both generations — while the **signature** differs. Today's hook reads
    only `feature` and is probably unharmed; **item 2b's `inputs`/`numInputs` walk is not**,
    and needs its own version guard before dereferencing anything.
  - **The engagement read was a race first.** Plugin load is deferred: the first run reported
    both features unloaded while Streamline's own log — flushed after ours — showed six
    plugins verifying. Now polled for the state, bounded by a wall clock.
  - `ctest fl_vtable_identity_control` (Part 1, the control) is unchanged and still runs on
    CI; the probe imports `d3d11.dll` and `KERNEL32.dll` only, so linking Streamline could
    not have broken it.

- **The upscaler identity hook, and a fixture that can prove a wrong symbol name wrong** —
  `docs/HANDOFF.md` queue item 2. `FrameLedger.Overlay` gains **module-scoped symbol
  resolution** (it had none: `GetProcAddress`/`GetModuleHandle` appeared nowhere in the
  target) and one MinHook detour on `sl.interposer.dll!slEvaluateFeature`, installed
  lazily by the existing 1 Hz watchdog so a title that loads Streamline at device
  creation is still caught without a `LoadLibrary` hook — which keeps §S6 separable.
  - **`FlWriterState.hooksInstalledMask` gets its first producer anywhere in the tree.**
    It had none — not even `FL_HOOK_PRESENT`, which the present hook was always entitled
    to — and `MeasuredFacts.RayTracingOf` already said so in its own comment while
    reaching `N/A` on every session because of it.
  - **Never `FL_UPSCALER_NONE`.** A Streamline-only writer cannot see FFX, XeSS or
    NGX-direct, and `NONE` is the only one of the three states `fl_shm.h` allows to be
    aggregated as a negative. An unrecognised feature id reports `UNKNOWN` — "a hook ran
    and could not identify what it saw" — which is a state this codebase could not
    previously produce. A ctest drives a target evaluating `0xF00D` to prove it.
  - **Ray Reconstruction's OBSERVED bit is gated on having seen an SL evaluation *this
    frame*, not on the hook being installed.** Found by an adversarial refuter over the
    design: the consumer returns `Tri.No` when OBSERVED is set on every record and no
    record carries the fact bit, so a writer setting OBSERVED whenever the hook was live
    would publish **"Ray Reconstruction: No" on an NGX-direct DLSS-RR title**, which
    never calls `slEvaluateFeature` at all. The honest-looking choice was the wrong one.
  - **`FL_MEASURED_UPSCALER_PARAMS` is deliberately unset, and the reason is a licence.**
    `17_HOOK_ENGINE` recommended hooking `NVSDK_NGX_Parameter_SetUI` for quality and
    render size; that needs NGX declarations, and the NGX/DLSS SDK is the proprietary
    **NVIDIA RTX SDKs License** — verified upstream, hitting three of
    `18_GPU_VENDOR_APIS` §Checklist step 2's four needles — so step 3 forbids vendoring
    it **and** re-declaring it. The document recommended a path its own rule forbids and
    nothing had noticed, because no code had gone near it. Corrected in place.
- **NVIDIA Streamline MIT headers vendored** at `src/native/third_party/streamline/` —
  the nine-file include closure of `sl.h`, one atomic commit with the licence copy, the
  notices row and both of `license-check.ps1`'s arrays. It buys the vendor's own
  `PFun_slEvaluateFeature`: five parameters, every one integer-class, so **nothing
  travels in XMM** — the exact residual hazard nobody could rule out while the signature
  was a guess, and a guess wrong by one argument corrupts the stack *inside the original
  function*, where `FL_HOOK_GUARD`'s `__try` cannot reach. `sl_nvperf.h` is excluded (the
  same `license.txt` carries a second, proprietary NSight Perf block naming it) and all
  of `external/` is excluded (upstream's `external/ngx-sdk` is the RTX licence, so a
  recursive copy does the forbidden thing by default). `license-check` asserts all three,
  proven red in four directions. This also answers the question `spike-notes` §5 recorded
  as open: §H5 case 3 was *"blocked on a licence decision, not on hardware"*, and the
  decision is taken.
- **`tools/hookinventory-check.ps1`** — every vendor symbol the Overlay resolves by name
  must exist, **in the module it takes it from**, in `docs/vendor-exports.json`. Wired
  into `build.ps1` in both halves. **Prevention: it fixed nothing**, and its own docstring
  says so, because a gate whose write-up implies it caught something cannot be audited
  later. Five red cases proven; the load-bearing one is an **oracle-discrimination
  canary that runs before any verdict**, since every failure mode of such a lookup
  produces the same answer as "absent".

- **The params half is designed and not built**, and the design is in `docs/HANDOFF.md` §2b
  rather than here, because it is a sequencing decision and not a change. Recorded because the
  panel **rejected the route the documents make obvious**: hooking `slGetFeatureFunction` and
  MinHook-patching the returned `slDLSSSetOptions` would need an "indirect" inventory class that
  is an unconditional escape hatch from `hookinventory-check` (its oracle would be "a `PFun_*`
  exists in a vendored header", and `sl_core_api.h:63` declares one for a symbol **zero measured
  modules export**); it would reopen the install-after-stop window; MinHook has no function-length
  oracle for a runtime-returned address, so a short thunk gets patched into its neighbour inside
  vendor code where `FL_HOOK_GUARD` cannot reach; `GetModuleHandleExW` in a detour takes the
  loader lock on the game's thread; and it structurally misses the game's FIRST
  `slDLSSSetOptions` call, which for a benchmark configured before launch is often the only one.
  The chosen route extends the hook the Overlay already owns — a bounded walk of
  `slEvaluateFeature`'s `inputs` chain, which it already receives and ignores.

### Fixed

- **A failed `REQUIRE` terminated the whole native test binary, hiding every test after
  it.** Catch2 compiled itself with `CATCH_CONFIG_DISABLE_EXCEPTIONS`: the top-level
  CMakeLists strips `/EHsc` from `CMAKE_CXX_FLAGS` deliberately, and `tests/CMakeLists.txt`
  re-added it **only to the test binaries**, never to the Catch2 library that decides the
  mode — a comment whose premise was right and whose effect was not. On a box where WARP's
  D3D12 path is broken this silently removed the end-to-end injection cases from `ctest`,
  **including the honesty assertion §S29(a) names as the merge gate's coverage**, which
  passes when run alone. `ctest` reported "1 test failed" where the truth was "and 2 never
  ran". Recovered **70 test cases and 1051 assertions**.
- **`MeasuredFacts.IsHonest` compared `measuredMask` against a hardcoded constant**, which
  was correct for a present-only writer and became wrong the moment a feature hook landed:
  an honest record claiming `FL_MEASURED_UPSCALER` counted as a violation. Widening the
  constant would have made the check a statement about one build rather than about
  honesty, so entitlement is now **derived from `hooksInstalledMask`** — a writer may
  claim a measurement only where it installed a hook capable of taking it — plus the
  reverse direction that was missing, a *value* set while its mask bit is clear.
- **`docs/12_BUILD.md` said the NVAPI SDK "is not vendored yet"** and that
  `src/native/third_party/` held only `CMakeLists.txt` and `vulkan-headers`. Both false
  since #55. That sentence was itself a correction of the opposite error, so this file has
  now been wrong in both directions about one fact — `legal/` is gated bidirectionally and
  caught its own version, `docs/` is not.
- **`docs/HANDOFF.md` item 2 named `NVSDK_NGX_EvaluateFeature`, which no measured module
  exports**, and said four modules where `NVSDK_NGX_D3D12_EvaluateFeature` has **seven**.
  The argument survived both errors; the data was wrong in the bullet whose subject is
  that a wrong symbol name degrades silently.

- **The consent store, and the first production driver of the guard loop** — `docs/HANDOFF.md`
  queue item 1. `HookedCaptureGate`'s three inputs (`hook_enabled`, `hook_consent_at`,
  `hook_blocked_reason`) have a real source for the first time, and
  `FlControlBlock.guardTicks` is now advanced by a **non-test binary**. Both ends of that
  field existed and were tested since #46/#50; only the loop was missing, and a missing loop
  reads as a missing subsystem.
  - **`FrameLedger.CaptureHost` is a separate project that nothing publishes**, and
    `tools/package-closure-check.ps1` is what keeps that true rather than remembered. §S27 was
    closed on the strength of there being no injecting entry point on any shipped binary; the
    host **is** one, so what keeps §S27 closed is its absence from the package. The gate walks
    the transitive `ProjectReference` closure of the two roots `12_BUILD` publishes and names
    the edge that reached anything else. **Proven:** a reference from `FrameLedger.Agent` leaves
    the build green and turns the gate red with `FrameLedger.Agent → FrameLedger.CaptureHost`.
    Both halves are wired — the self-test (5 cases, 4 RED) **and** the live pass, because a gate
    wired self-test-only never reads the repository, which is the defect it exists to prevent.
  - **`HookRequest` can no longer be synthesised, and that is what actually closed the hole.**
    It was a `record` with `required`/`init` members, so
    `new HookRequest { HookEnabled = true, ConsentedAt = DateTimeOffset.UtcNow }` passed every
    check in the gate and reached `GuardedInjectAsync` — **verbatim the expression §S27 named and
    rejected**. A store, a record and a provenance flag do not close that; they add an honest path
    beside it. It is now get-only with a private constructor and one factory, so the dishonest
    path is a compile error rather than a discouraged idiom. Found by a safety refuter over the
    design, before any of it was built.
  - **`GameConsentRecord.Stored` is `internal`**, because `FrameLedger.Domain` is inside both
    publish closures: a public minting factory there would be a blessed, *shipped* API for
    producing consent nobody gave, and the closure gate cannot see it — it walks project
    references, and Domain legitimately belongs to both. The `InternalsVisibleTo` list is the
    reviewable artifact; a test asserts the public factory does not exist.
  - **An acknowledgement cannot clear a block.** `RecordOperatorAcknowledgementAsync` takes an
    `OperatorAcknowledgement`, which carries neither `BlockedReason` nor `PreScanUnverified`, so
    the store's merge is their only source. Taking a whole record — the first shape — meant
    `consent grant` on a title the pre-scan had blocked would write `BlockedReason = null` and
    the gate's `PreviouslyBlocked` branch would stop firing: the "I understand, continue anyway"
    button CLAUDE.md rule 2 forbids, arrived at by omission.
  - **The pre-scan's third state gets its own field.** `05_DETECTION` makes "could not verify"
    neither a hit nor a pass, while `hook_blocked_reason` is two-state by definition. It is
    refused in the loop, never mapped to `PreviouslyBlocked` and never cleared — routing it
    through the gate would force one of the two collapses that document forbids.
  - **A consent record carries a disclosure provenance and a wording version**, neither of which
    `06_DATA_MODEL` has. A bare timestamp cannot say whether anything was disclosed, and
    `ConsentProvenance.NotRecorded = 0` means a record nobody filled in refuses. **FR-2.1's value
    is deliberately not declared**: no `.resx` exists anywhere in this tree, `09_I18N` reviews
    `Safety_*` keys as legal text, and a declared-but-producerless value is the "reads as
    sanctioned" shape §S29(c) was raised for. The version is carried from the *first* record
    because it cannot be added later — retrofitting one means treating unversioned consent as
    either current or stale, and both are wrong about some record.
  - **The command surface has no `--pid`, `--payload`, `--force`, `--yes` or `--diag`**, and a
    test pins it. §S27's gap was a user-named pid on a binary with no consent record; resolving
    the target from the same normalised path the record is keyed on makes "consent for A,
    injection at B" inexpressible rather than discouraged. Two processes of one image refuse as
    ambiguous rather than picking one.
  - **`consent grant` refuses redirected stdin**, so no script can acknowledge on a human's
    behalf, and the disclosure states in its first line that it is *not* FR-2.1 consent.

- **Session end has a signal (§S29(e) closed).** A held process handle, opened before the
  injection and never re-resolved — pids recycle and the ring is named after one.
  - **It was first closed on a handle that was never held, and the correction is the
    interesting part.** The first version used `Process.GetProcessById` and read
    `HasExited`. **Measured** on .NET 10.0.10 by a probe over the live object:
    `_haveProcessHandle == false` and `_processHandle == null` both after construction and
    after reading `HasExited` — `GetProcessById` opens nothing and `HasExited` opens a
    transient handle and releases it in its own `finally`. Three source comments, a ledger
    entry and a changelog line all asserted the pid was pinned; nothing checked it, and it
    was not. `Infrastructure.Io.HeldProcessHandle` now opens `SYNCHRONIZE |
    PROCESS_QUERY_LIMITED_INFORMATION` and keeps the `SafeProcessHandle`, a pid that
    cannot be opened is a refusal (`TargetCannotBePinned`) rather than a session, and the
    test asserts the *property* — it answers about a fully exited process at a pid
    `Process.GetProcessById` will not resolve, which nothing holding a handle-less object
    can do. The ninth entry for "measure Windows APIs, don't trust them", and the first
    where the API misled by doing **less** than its name implies.
  `SessionEndClassifier` takes no elapsed-time parameter, so **a frozen `writeIndex` can never
  end a session**, which is the whole of the defect: `ShmRingReader` holds the section open, so
  an exited game leaves `status` `READY` and `writeIndex` frozen, byte-for-byte a loading screen.
  §S26 made it strictly worse by dropping `DXGI_PRESENT_TEST`, removing the accidental heartbeat
  an occluded title used to emit. It also separates the two stops the mapping cannot: `StopObserving`
  stores `FL_STATUS_UNHOOKED` for the safety stop **and** for supervision loss, so only the side
  that caused one knows which — and `legal/DISCLAIMER.md` §2 discloses them differently.

- **A throwaway consumer**: `measuredMask` → rule 7 tri-state, segmentation, and the one number a
  present-only writer may publish. In the unshipped host, deliberately not in
  `FrameLedger.Domain.Metrics.*` — P2 owns the real calculators and `coverage-gate` carries a
  separate 95% floor for that namespace.
  - **Segmentation is stream-first, settings-second**, and reversing it manufactures a segment per
    present. Two axes exist and neither document mentions the other: `03_METRICS` §Upscaling
    splits on a settings change, `fl_shm.h` says the Agent "segments by [`swapchainId`] and reports
    the dominant stream". One vtable patch sees every swapchain in the process, so a title with a
    UI or video swapchain interleaves two streams in one ring.
  - **`frameIndex` is process-global, so it cannot detect a gap within a stream.** The first design
    excluded any interval whose index did not advance by one — which would have excluded *every*
    interval in any multi-swapchain title and reported no duration at all, including for Displayed
    FPS. `dllmain.cpp` assigns `g_frameIndex++` four lines before `swapchainId`; the overflow
    harness interleaves 17, so the dominant stream's indices step by ~17. Found by a feasibility
    refuter over the design.
  - Everything unmeasurable is `null` or `N/A` with **no fallback**: `fg_factor` is never `1.0`,
    `fgMode` is never `"none"`, the upscale ratio is not computed at all (`renderW/H` are 0), and
    the retired `FlUpscaler` value 2 is never decoded as `dlss_rr` — `03_METRICS` §Upscaling
    listed it as an upscaler value until this PR, which removes it and records why. The
    renderer is asserted too: rule 6 is a rule about
    *showing* a single inflated number, and a test matches the rendered text against `×N`.

- **`--present-interval-ms` on `hook-harness`**, because one test could not be written without it.
  The Agent's drop accounting fires when the writer laps the reader — 8192 records — and at the
  default ~120/s that is **68 seconds** of a reader deliberately not draining, so the branch
  `04_CAPTURE` calls "the Agent stalled for over ~16 s" had never run against the real Overlay in
  either language. Measured: uncapped, the harness does **171,636 presents in 3 s**, so the ring
  laps in well under a second.

### Changed

- **`HookedCaptureGate.ShouldUnhookAsync` is deleted (§S29(c) closed).** It was a second in-session
  re-scan that published no tick and did not latch — the two properties `GuardSupervisor` exists to
  guarantee — and it was the *more discoverable* of the two, because a drain loop already holds the
  gate. Its polarity was inverted from the survivor's, too: `true` meant STOP where
  `ScanOnceAsync`'s `true` means MAY CONTINUE. Deleted rather than routed: it had **zero production
  callers**, its body was strictly weaker than `ScanOnceAsync`, and routing would have given the
  gate per-session state on a class whose contract is that it "adds no judgement of its own".
  - **The two Facts that covered it are replaced by one that is stronger.** They asserted the
    boolean and never the tick or the latch, so they certified the API as sanctioned while saying
    nothing about what was wrong with it. `TheGateExposesNoSecondInSessionRescanPath` pins the
    gate's public instance surface to exactly `{StartAsync}` and is red on unmodified `main`.

- **`ShmDrainIntegrationTests`' honesty helper is split.** `AssertRecordsAreHonest` mixed
  fixture-independent invariants with assertions about attach *timing* and a single swapchain, and
  a fixture that deliberately stalls the reader satisfies neither — reusing it produced a red test
  whose failure message was about frame indices, indistinguishable from a real regression.

### Fixed

- **`ShmRingReader.SetPaused` had no test at all.** Both halves of `pauseRequested` existed —
  `MayObserve()` has read the flag since #46 — and the managed writer had never been driven in
  either direction. Now both: a merge-gated round trip proving the byte lands at
  `ControlOffset + 0` and that `unhookRequested` four bytes away is untouched, plus an integration
  case proving the Overlay acts on it, **ticking throughout**, because the defect #46 fixed only
  appeared on frames where `guardTicks` had changed. A pause is invisible in the record stream —
  `MayObserve()` returns false before `frameIndex` is assigned — which the consumer must not read
  as one enormous frame time.

- **`ShmHandshakeValidator` never compared `handshake.Pid`**, so a ring left behind by a finished
  session, or one under a recycled pid, validated `Ok` on build id, layout and capacity alone.
  Not an ABI change: `ShmAttachRefusal` is managed-only with no native mirror.

- `NativeAntiCheatGuard`'s §S21 rationale was attached to `NativeCheckRules` by two stacked
  `<summary>` blocks while `NativeRulesFilePath`, which it describes, had none.

### A fifth rate-sized budget, and a correction to what I said about WARP

`TheGuardInjectsTheOverlayAndTheReaderDrainsRealFrames` — pre-existing, from #51 — drained for a
fixed twelve iterations and then asserted a floor sized on "the harness presents at ~120/s". It waits
for the record count now, bounded by a wall clock, and gained an assertion that the loop supervised
more than once so the shortened path cannot pass vacuously. **This is explicitly not presented as the
fix for the one failure observed**: it failed once in twelve runs with its message uncaptured, and
after #62 — two rounds of the wrong remedy — the rule is that a defect class is a hypothesis about
the next failure, not a diagnosis of it. The budget was removed because that is defensible on its own
terms. Ten consecutive full runs clean.

**And the WARP note in `HANDOFF` §Traps said "a reboot clears it", which is false.** That sentence was
written on plausibility and never tested. The machine rebooted at 13:58 and the identical failure
reproduced at 14:17. Measured properly this time: `D3D12CreateDevice` on the WARP adapter returns
`DXGI_ERROR_DRIVER_INTERNAL_ERROR` at **every** valid feature level, while `D3D11CreateDevice` on the
**same adapter object** succeeds at FL 11_0 and D3D12 on the RTX 5080 succeeds; `d3d10warp.dll`,
`d3d12.dll` and `D3D12Core.dll` all match the OS build (Windows 11 Insider 26300/29639), so nothing is
corrupt. It reads as an Insider-build regression in WARP's D3D12 path and it is persistent. CI is not
an Insider build and passes the same suite, so `main` is unaffected — but the native suite has a hard
dependency on WARP D3D12 that a dev box can lose on its own, which is now written down.

### A fourth failure that was not a race at all — an off-by-one wearing a flake's costume

`APausedSessionStopsRecordingAndResumesWhereItLeftOff` failed roughly one run in five, which is
exactly what the three fixed above looked like. It was **`EstablishRecordingAsync`'s loop bound and
its assertion disagreeing by one**: the loop ran while `seen < 10` and so exited at *exactly* 10,
and the assertion demanded `> 10`. It passed only when a single drain happened to bring in eleven or
more at once — so the timing decided whether the off-by-one was visible, and two rounds of budget
tuning made it rarer without touching it. One constant now serves both.

**The lesson is the diagnosis, not the fix.** Having just repaired three genuine races, the fourth
failure in the same file was assumed to be a fourth race, and it was treated with the remedy for the
previous three. What settled it was reading the failure message instead of the pattern: *"Expected
seen to be greater than 10 … but found 10"* names the defect exactly and took one run to obtain.

Swept alongside it, since the class was the thing under review: the pause test now waits for the
writer to **settle** — two reads 100 ms apart with nothing between them — rather than assuming a
fixed 250 ms covers a present already in flight, and it keeps ticking throughout, because the pause
path is only reachable on a frame where `guardTicks` changed (the defect #46 fixed, and the reason
this test exists). The resume-drain budget goes 1 s → 3 s for the contention reason.

Ten consecutive full runs clean afterwards.

### Three racy assertions that #60 merged, caught by the post-merge run

`docs/HANDOFF.md` says to run `./build.ps1 check` with no switches **after** every PR. This is what
that found, and all three are the same shape: an assertion that reads a state once, at a moment when
the state is legitimately still in transit.

- **`Status == Ready` read immediately after the first guard tick.** `InitThread` publishes
  `layoutVersion` at step 2 and sets `READY` at step 6 — after `InstallPresentHooks` creates a
  throwaway WARP device, tens of milliseconds. `TryAttach` succeeds as soon as `layoutVersion` lands
  and the host publishes its first tick immediately **by design**, because the Overlay's 65 s
  supervision clock starts at mapping publish. A tick and `INIT` are therefore a legitimate
  simultaneous state. Polled now, with `INIT` past the budget still failing — that is
  `WriterNeverInstalledHooks` and must not read as a passing session.
- **`ApiMask` asserted as soon as `Status` became `Ready`.** "Hooked and recording" is two events:
  `apiMask` is set inside `FindOrAdd`, on the first present the hook actually *sees*, so `READY` with
  `apiMask == 0` is another legitimate window. Failed once in five full-suite runs and never once in
  six isolated ones — the signature of a window widened by contention.
- **QPC ascending asserted across a deliberate lap, and it is unassertable in EITHER direction.**
  Measured both ways: under the full suite the drop test's first drained record came back ~148 ms
  *later* than the second; run alone the same batch was perfectly ascending. `Drain` resumes at
  `writeIndex - capacity`, the oldest survivor, and whether the writer has overwritten that slot when
  the copy arrives is a race decided in microseconds — the seqlock catches a tear *during* a copy, not
  a slot cleanly overwritten *before* it. Ascending qpc is a property of a reader that **kept up**, so
  it now lives only with the other attach-timing assertions. What matters downstream is enforced where
  it belongs: `MeasuredFacts` skips non-positive deltas, and `04_CAPTURE` requires a non-zero drop
  count to be surfaced as a session warning.

Six consecutive full runs clean afterwards. **Two native cases stay red on this machine for an
unrelated reason**, recorded in `HANDOFF` §Traps: `D3D12CreateDevice(WARP)` returns
`DXGI_ERROR_DRIVER_INTERNAL_ERROR` (0x887A0020) while the same call on the real adapter succeeds. CI
runs the same suite on WARP and passes, and nothing in #60 touches that path.

### What the adversarial review of this diff caught before it landed

An adversarial pass over the working tree raised 23 candidates; 17 survived a second agent told
to refute them. Beyond the process handle above, the ones that were real defects rather than
documentation drift:

- **"Could not read the executable" passed the fingerprint check instead of refusing.**
  `observed ?? record.Fingerprint` made `FromConsent`'s mismatch comparison compare the record
  against **itself**, so an unreadable binary decided as "this is the consented one" — the one
  polarity everything else here is built to avoid. Now `ExecutableUnreadable`.
- **`Enum.TryParse` accepts numeric strings**, so `"provenance": "1"` in the consent file yielded
  `UnshippedHostOperator` and `"42"` an undeclared value cast to the enum — an end-run around the
  two-member count a test pins. The comment claimed the opposite in as many words. Now
  `Enum.IsDefined` plus an ordinal name comparison, with the numeric cases tested.
- **An unreadable consent file silently cleared a persisted guard block.** "There is no file" and
  "I could not read the file" both produced an empty store, and every write is a
  read-modify-write over that — so the merge that carries `BlockedReason` forward carried nulls,
  and the write republished a file containing only the new entry, dropping every other game's
  record. The two outcomes are now distinct and an unreadable store refuses every write.
- **A guard exception mid-session discarded every record already drained.** Not advancing the tick
  is correct and required; losing the session's data with the stack was neither. Now
  `SupervisionFaulted`, with the final drain still running — and the *first* scan still throws,
  because there is no session to preserve yet.
- **`StreamSegmenter` cut a spurious 0×0 segment** when a stream's first records had no measured
  size: `w`/`h` were both the "no baseline yet" sentinel and a real resolution, so the split fired
  on the first measured record — cutting a segment on the writer's silence, which the surrounding
  comment forbids.
- **`TargetResolver` narrowed to a false single match.** Its comment claimed skipping an unreadable
  process avoided that; skipping is what caused it, because `matches.Count == 1` could not tell
  "one candidate" from "one readable candidate and others invisible". Skips are counted now.
- **`ConsentWriteOutcome.StaleFingerprint` had no producer** — a declared-but-producerless value,
  the shape this same PR invokes two files away to justify `ConsentProvenance` having no FR-2.1
  member. It is produced now, and only where it matters: a re-grant against a different binary
  cannot inherit an existing block, while an ordinary re-consent after a patch still works.
- **Three of the new tests could not fail for the property they named.**
  `ATerminalAttachRefusalIsNotRetriedIntoATimeout` asserted only the final reason, which is
  identical whether the refusal returns immediately or is retried to the budget — it counts
  attempts now, and gained the green half. `TheFirstTickIsPublishedBeforeAnyDrain` recorded
  publishes and drains in two independent lists, so no ordering was observable — one ordered event
  list now. And the end-to-end refusal case called `File.Delete` on a path whose parent directory
  only exists once some test has written a record, so on a clean build output it threw and its
  verdict depended on which sibling ran first — in the very test whose comment says a test whose
  verdict depends on what ran before it is one this repository does not accept.

- **`docs/HANDOFF.md` — one file to pick the work up from, and it carries no status.**
  Sequencing, the decisions that live in no other file, and the traps that cost a wrong
  diagnosis rather than a build cycle. Status stays in `20_OPEN_QUESTIONS` §S24,
  `spike-notes`, `15_ROADMAP` and this file, and the handoff points at them.
  - **The rule is written into the file because the alternative is what keeps
    happening.** A handoff that summarises status becomes the next stale copy within a
    day — #45's note did, `15_ROADMAP`'s status block did, and `spike-notes` §8 went
    stale *in the same file* that was correcting an earlier stale claim. It also would
    have been a fifth statement of the gate's composition, which `rules-validate` now
    refuses in the shipped data.

- **Shared-memory layout v3: the zero value of every enum in the record is now "nobody
  said", not a fact.** `FL_SHM_LAYOUT_VERSION` 2 → 3, spent deliberately in the last window
  where it costs nothing — nothing has shipped and no user has a session, and after the
  first release the same edit is a SemVer MAJOR that makes the Agent refuse to attach and
  tell the user to restart the game.
  - **The generator of the defect, not four instances of it.** `FlFrameRecord rec{}`
    zero-initialises, so whatever 0 means is what a writer publishes when it **forgets**. In
    v2, 0 meant `FL_UPSCALER_NONE`, `FL_FG_NONE`, and an `rtFlags` with no evidence bits —
    three measured negatives about a title nobody examined, which `03_METRICS` turns into
    `upscaler none` and `fg_factor 1.0`, the single inflated number rule 6 forbids.
    `measuredMask` made that safe **by convention**; v3 makes it safe **by construction**,
    and the mask becomes corroboration rather than the sole defence. `rtFlags`' polarity is
    flipped so every bit means *observed*.
  - **Four answers items 4/6/7 owe had no home.** DLSS super-resolution **and** Ray
    Reconstruction concurrently — RR was a mutually exclusive `upscaler` *value* while
    `03_METRICS` makes it an independent tri-state axis — plus `upscalerSharpness`, and, in
    `FlWriterState` because they are session facts rather than per-frame, the device
    **RT tier** without which rule 7's definite `No` had no producer at all, with
    `rtStateObjectsCreated` and `rasterPsoCreated`.
  - **Paid for by two narrowings that are corrections, not sacrifices.** `vramUsedBytes`
    (u64 bytes → u32 MiB) carried 64 bits of byte precision that every consumer divided
    away, to feed a comparison against `vramBudgetMb` — **already MiB** — that was
    unit-mismatched at the point of use. `fgEvaluations` (u32 → u8): ×4 multi-frame
    generation is 3, so saturating at 255 would mean 256× frame generation.
  - **`seq` @56 and `swapchainId` @60 did not move**, so `fl_ring.h`'s two pins hold
    verbatim, the seqlock's payload spans are unchanged, and the ring needed no edit.
  - **Three mask bits split producers that do not arrive together** — the same defect three
    times. `FL_MEASURED_UPSCALER_PARAMS`, because an NGX-direct title exports only the
    parameter-object *factories* so a writer knowing *which* upscaler ran knows nothing
    about quality (publishing `0` = "DLSS Performance" as measurement);
    `FL_MEASURED_PRESENT_ARGS`, because `wglSwapBuffers` and `vkQueuePresentKHR` have no
    such arguments and `syncInterval = 0` is a *real* DXGI value with no in-band sentinel;
    and `FL_MEASURED_FG_COUNTS`, because identity and per-present counts are two hook rows
    and a writer with only the first would publish `fg_factor 1.0` having counted nothing.
  - `FL_RT_PSO_ALIVE` → `FL_RT_PSO_CREATED_EVER`: creation is observed at
    `CreateStateObject` and destruction is COM `Release`, which is not in the inventory and
    must not be added — so the bit latches and could only ever mean "created ever".
  - **`03_METRICS`' RT `No` is now three conjuncts**, and the second is the one that is easy
    to drop: `hooksInstalledMask` must contain the **AS-build** hook. A writer with only
    `DispatchRays` sees nothing on an inline-RayQuery title, and its silence would otherwise
    be indistinguishable from a real negative.
  - **`pt_confidence` loses its fourth input rather than substituting one.** "The ratio of
    RT to raster work" has no cheap denominator — counting raster work means a per-draw hook
    — and §H6 records that a command-list count measures *recorded* rather than *executed*
    work anyway. The score may only ever *suggest*, so a weaker score is not a fabrication.
  - **The design came from a four-way panel, three judges and three refuters**, and the most
    useful thing the refuters found was not in any proposal: see the `FL_MEASURED_OUTPUT_RES`
    fix under Fixed. They also killed a claim this entry would otherwise have made — that
    reserved bytes let a future field skip the version bump. They do not: `recordSize` and
    `layoutVersion` are compared before a reader looks at anything, so an old reader refuses
    a new writer and never reaches an unknown field. What the reserve buys is that existing
    offsets do not move.
  - **Hook-path cost:** `RecordPresent` gains one comparison of two `uint16`s already in
    registers, and `Publish`'s two memcpy spans are `offsetof`-derived and byte-identical.
    **Not carried forward: the 8.4 ns figure.** That is `--probe-cost`'s empty-detour floor,
    which the tool says of itself; no instrument in the tree measures `RecordPresent`.

- **NVAPI is vendored, and the capability matrix gets the axis it could not be filled
  without.** `src/native/third_party/nvapi/` now holds nine headers (`nvapi.h`'s include
  closure plus `nvapi_interface.h`), `License.txt` and `amd64/nvapi64.lib`, from
  `github.com/NVIDIA/nvapi` @ `cd6918f6` (MIT). **x64 only** — `x86/nvapi.lib` is 438 KB of
  material we could never link and would still have to disclose. `NvApiDriverSettings.{c,h}`
  and the HLSL-extension headers are left behind for the same reason.
  - **Why that repository and not the SDK installer** is the whole licence argument:
    `amd64/nvapi64.lib` is a *tracked file in the MIT repo* and `License.txt` names the
    import libraries as the subject of the grant, so the **binary** is covered. The same
    file from the SDK installer arrives under NVIDIA's own agreement and could not be
    vendored into a GPL-3.0 tree.
  - **The canary came for free, and it ran in the order that makes it evidence.**
    `license-check.ps1` was made bidirectional after it was found unable to see
    *claimed-but-absent* material. Vendoring while `THIRD_PARTY_NOTICES.md` still said
    "Not yet vendored" failed the build — the *present-but-marked-absent* direction, which
    was structurally invisible before. The notice was flipped afterwards, not before.
  - **`ctest fl_nvapi_probe` exists because an unconsumed vendored dependency is
    unverified.** Nothing else compiles against `fl_nvapi` yet, so a short include closure
    or a wrong-architecture `.lib` would sit there with every gate green. The probe's
    *compile* is half the test.
    - **It is green on both kinds of machine, and exiting 0 is not what makes it green.**
      `nvapi64.lib` is a **static** stub library reaching `nvapi64.dll` through
      `nvapi_QueryInterface` at first call, so it is not a load-time dependency: a CI runner
      with no NVIDIA driver loads the binary and `NvAPI_Initialize` returns an error, which
      is exactly the degradation §L3 requires. Both branches exit 0 — so ctest additionally
      requires the string `BRANCH: (AVAILABLE|DEGRADED)`. **Canary: a probe gutted to
      `int main(){return 0;}` compiles, exits 0, and is now RED** at
      *"Required regular expression not found"*, with the native build and the other 15
      ctests still green. The alternation has to stay: pinning one branch would turn the
      other kind of machine red for being itself.
    - **What that still does not give you, said rather than implied:** ctest prints a
      passing test's output nowhere, so a CI log shows a branch was reached and **not
      which**. On a hosted runner it is inferable — no NVIDIA driver exists there, and the
      probe returns in ~0.01 s against ~1.4 s here — but inferable is not observed, and
      `18_GPU_VENDOR_APIS` §L3 now separates the two rather than claiming the stronger
      thing. What the run *does* prove is the load-time claim: a load-time dependency on an
      absent `nvapi64.dll` would not have started at all.
    - It refuses a **zero** GPU count rather than letting the name loop run no iterations,
      because every assertion inside a loop that never runs is vacuous.
  - **Measured on this machine:** driver `610.88` (branch `r610_85`), 1 physical GPU,
    "NVIDIA GeForce RTX 5080".
  - **A doc error the vendoring found and reading vendor documentation would not have:**
    §L3's function table named `NvAPI_GPU_GetMemoryInfo`. The headers mark it
    `__nvapi_deprecated_function` ("deprecated in release 520 — use
    `NvAPI_GPU_GetMemoryInfoEx`"), so under `/W4 /WX` a call to it **fails the native
    build**. Corrected. This is the class `17_HOOK_ENGINE:128` calls the highest
    false-confidence risk in the spike, sitting in a document instead of in code.
  - **The capability matrix is now vendor × layer**, restructured *before* anything was
    filled in — `18_GPU_VENDOR_APIS:137` said in its own words that the single-axis table
    "cannot express" the AMD/Intel deferral and that the axis is "a prerequisite of filling
    it, not a tidy-up afterwards". The legend separates `?` (a to-do, measurable here) from
    `untested` (a deferral, not measurable here) from `arch` (not available by
    architecture), which is precisely what the old table could not distinguish and what the
    UI consults before advertising a capability.
  - **The D3DKMT probe's two-OS requirement is deferred to Win 11, with a rationale.** One
    machine, and it is Win 11; Win 10 22H2 **stays a supported floor** and is explicitly
    unmeasured. Unlike the AMD/Intel gap this one *could* be closed with a VM and is being
    left open deliberately — so the deferral is conditional on the probe staying
    non-load-bearing, which the doc now states as the condition rather than as advice.

> **These five entries were written retrospectively.** PRs #40–#44 changed 8 files,
> every one under `src/native/`, and touched no documentation at all — so for a day the
> repository's own ledger described an Overlay with no hooks. CLAUDE.md's "any deviation
> from a doc updates that doc in the same PR" did not catch it, because none of the five
> deviated from a doc: they implemented specifications that were already written, and
> the staleness landed in the *status* claims of other files. `legal/DISCLAIMER.md` §Accuracy
> audit records the same drift from the user-facing side.
>
> > **And then it happened again, immediately, seven more times.** #45 — the PR that wrote
> > the five entries above and complained about the gap — was followed by #46 through #52
> > with **no changelog entry for any of them**, including the C# struct mirror, the ring
> > reader, the handshake validator, the closed write-read loop and check 3's call site.
> > The entries below were written retrospectively too, on 2026-08-05, from the diffs.
> >
> > **A retrospective note is not a mechanism, which is the actual finding.** #45 recorded
> > the drift in prose and nothing about the repository changed, so the same failure ran a
> > second time at greater length. `ci.yml` now carries a `changelog` job that fails a pull
> > request touching `src/` without touching this file. Prose asking people to remember is
> > what was already tried.
> >
> > **One of the seven is worse than missing.** `bd6d367` (#48) carries, verbatim, the
> > commit body of `bff0f6a` (#47) — a squash whose `--body-file` came from the wrong
> > branch. The subject line describes the occlusion-probe fix; every paragraph beneath it
> > describes the struct mirror. Its entry below was reconstructed from the diff, because
> > the commit message is evidence about a different PR.

- **Check 3 gets a call site, and the half that cannot have one** (#52). `19_SAFETY` has
  listed the per-title blocklist as check 3 from the beginning and it had **no call site**:
  `MatchesBlockedExecutable` was implemented, tested, and asked by nobody, so "check 3
  passed" read as "this title is not a known online title" while nothing had looked.
  `CheckBlockedExecutable` now runs inside `EvaluateImpl`, between the module scan and the
  pre-scan — one `OpenProcess` and a string compare, ahead of the only check that touches
  the filesystem.
  - **It needed a new seam.** `Sources::ImageDirectory` deliberately resolves the *install
    root* — Unreal puts the exe three levels below it, measured on Lies of P — and the file
    name is exactly what it discards. `ImageFileName` is a different fact, so it is a
    different seam and a new row in the fail-closed matrix.
  - **Unresolvable identity refuses** (`kProcessUnreadable`): `kFailed`, `kIncomplete`, an
    empty name and a null seam all take one path. The narrow conversion uses
    `WC_ERR_INVALID_CHARS` with **no** default character, so a name that cannot be
    represented exactly fails rather than becoming a string containing `?` — §S21's ANSI
    defect was exactly a silent lossy conversion.
  - **The store-id half cannot be called**, for three independent reasons: nothing produces
    a `store_id` (the platform metadata extractors were never built), `FlGuardEvaluate`
    takes a pid and nothing else *by design* (§S3 forbids a caller asserting a safety
    fact), and "unknown refuses" applied to it would refuse every title on every machine.
    It stays implemented, tested and uncalled. **Do not "fix" the second by widening the ABI.**
  - **The list ships empty**, so nothing is refused today; what changed is that populating
    it would now do something. The acceptance criterion asserts `kBlockedExecutable`
    *specifically*, because "it refuses" is indistinguishable from the four refusals the
    guard already makes.

- **The write-read loop closes, against our own harness and nothing else** (#51). Real
  guard, real injection, real Overlay, real reader: `ShmDrainIntegrationTests` seeds the
  rules with the product's own `RulesSeeder`, starts `hook-harness --real --hold-presenting`,
  injects through `NativeAntiCheatGuard`, and drains records while driving
  `GuardSupervisor.ScanOnceAsync` and `PublishGuardResult`. A second case proves the safety
  stop: >10 records flowing, then `unhookRequested`, then `writeIndex` frozen 700 ms later.
  - **CI found a hard-gate defect the dev box structurally could not.** Running the test,
    the guard refused our own harness — `SuspiciousUnsigned unknown
    System.Security.Cryptography.ProtectedData.dll`. §S16 puts the target's **ancestors**
    in the scan set and the .NET test host is the harness's parent, which is the
    launch-mode arrangement. A .NET host that loads that assembly poisons its own scan set.
    **A gate that cannot pass**, and §S19(b)'s "plausible and unmeasured" is superseded.
  - **So the drain tests are `Category=Integration` and CI runs `-SkipIntegration`.** The
    skip is loud, names §S19(b) and says how to run them; a developer running
    `./build.ps1 check` with no arguments still gets everything. Proven both directions —
    76 Infrastructure tests by default, 73 with the switch.
  - **What that costs, stated rather than left to be discovered:** the only end-to-end proof
    of the capture path does not run in the merge gate. See §Known issues.

- **The Agent can read the ring, and the pointer trap that would have hidden it** (#50).
  `ShmRingReader` mirrors `fl_ring.h`'s `RingReader::Drain` rather than re-deriving it —
  where the two disagree the header is right and the C# is the bug.
  - **Map at offset 0, and this is measured** (.NET 10, Win 11 26300): a view created at a
    non-zero offset is mapped from the 64 KiB allocation-granularity boundary *below* it,
    and `AcquirePointer` returns **that** base. So the obvious way to reach the control
    block — map region 3 at `0x80`, write `*(uint*)(p + 12)` — writes
    `FlShmHandshake.pid` instead of `FlControlBlock.guardTicks`. It does not fault, and
    `Read<T>` does not bounds-check it either, because the handle legitimately spans from
    byte 0. The supervision counter would have landed silently on the field a reader
    validates first. `Bind` now asserts `PointerOffset == 0` rather than assuming it.
  - **Seed the read index from the writer.** `fl_ring.h` starts its reader at 0 because it
    is created alongside its writer; an Agent attaching to a ring already in flight is a
    different situation. Canary: seeded at 0 against a ring at `writeIndex` 1,000,000 the
    first drain reports **999,992 drops** — and `04_CAPTURE` defines any non-zero drop count
    as "the Agent stalled for over ~16 s". It also re-ingests up to `capacity` stale records
    from a finished session as current frames. Records already published at attach are
    reported separately as `RecordsBeforeAttach`, which is not a stall.
  - **Bound the capacity against the mapping.** The validator accepted any power of two —
    every value up to 2³¹ — and returned `Ok` having never related that claim to the section
    it describes. The reader indexes by raw pointer arithmetic (the seqlock needs ordering
    `Read<T>` does not provide) and so gives up that API's bounds check.
    `CapacityExceedsMapping` is taken from `ByteLength`, never from `DefaultCapacity`.
  - **Publish order is a safety property and it is the reverse of the natural one** — flag
    first, tick second, because a fresh tick *resets* the Overlay's supervision clock.
    **The suite does not verify that order**, and says so: it asserts both values land,
    which a publisher writing them the wrong way round also satisfies. What holds the
    property is `PublishGuardResult` being the only writer of either field.

- **The Agent gets a build id of its own, so refuse-to-attach can run** (#49). `07_IPC`
  makes a `buildId` mismatch a hard refuse-to-attach and `04_CAPTURE` says the Agent
  compares it "against its own" — and the Agent had no own value. Two documents specified a
  check that could not run in either direction (§S23-1).
  - `FlGuardBuildId` is observation-only and **refuses rather than truncating**: a shortened
    id is not a partial answer, it is a *different* id, and returning one would produce a
    permanent mismatch caused by this function rather than by any real version skew.
  - **Why the guard carries it and not the Overlay:** reaching `FlGetBuildId` means
    `LoadLibraryW` on `FrameLedger.Overlay.dll`, which starts its init thread and creates a
    ring under the *Agent's* own pid. The payload is not something its own host may load.
  - `ShmHandshakeValidator` is the comparison, as a pure function, so every refusal is
    drivable without a live target. `layoutVersion == 0` is **`Incomplete`, not a mismatch**
    (it is published last behind a release fence, so zero means "retry", not "restart the
    game"); the version is checked **before** the fields it vouches for; and the default is
    `NotEvaluated`, never `Ok`.
  - **The fail-open, measured.** The naive implementation compares two strings, and with
    neither side carrying an id `string.Equals("", "")` is **true** — so the gate reported
    `Ok` for every process on the machine. **The test for it was nearly missed:** the first
    draft asserted the two halves as separate cases and never put both in one call, which
    is the only arrangement that reaches the defect.

- **The C# mirror exists, and the gate that guards it stops being a `Test-Path`** (#47).
  CLAUDE.md calls the struct mirror the mechanism protecting the shared-memory ABI; nine
  files described it in the present tense and none of it existed (§R10).
  - `ShmLayout.cs` is driven by `tools/fl-layout-dump`'s JSON, never a transcribed table —
    a hand-written offset table is a *second* statement of the layout, and two statements of
    one fact drift, which would be the defect the mirror exists to catch, reintroduced
    inside the catcher.
  - **Blittability is asserted, and that is not decoration.** Measured: swapping the
    `fixed byte BuildId[32]` for the `[MarshalAs(ByValTStr)] string` idiom this repository
    already uses correctly elsewhere keeps `Marshal.SizeOf` at 64 with **every offset
    assertion still passing**, while `Unsafe.SizeOf` collapses to **40**. That mirror looks
    correct by every obvious check and cannot be read out of a memory-mapped view.
  - **Both directions**, and the reverse walk caught something on its first run:
    `fl-layout-dump` was not emitting the `reserved` tails, so the C# declared members the
    dump could not confirm. Fixed in the dump rather than excluded from the check.
  - **The gate was the bigger finding.** `build.ps1`'s struct-mirror step was
    `Test-Path ShmLayout.cs`, then printed "covered by dotnet test" — it never looked for
    the test. An *empty* `ShmLayout.cs` would have turned an honest loud skip into a silent
    green, and deleting the test afterwards would have kept it green forever. It now reads
    the run's `.trx` and requires the named class to have **executed**. `dotnet test` makes
    a regression red; this makes *deleting the regression test* red.
  - Three canaries proven red, and **the harness was broken twice before it proved
    anything** — a renamed class tripped a file-name analyzer and the run died at *build*;
    a `sed` rewrite gave the file LF endings and the run died at *`dotnet format`*. Both
    printed a failure, and a failure upstream of the gate proves nothing about the gate.

- **`api` is resolved per swapchain, and `GetDevice` does not return what the docs imply**
  (#44). `api` was hardcoded to `FL_API_D3D11` on every record — a guess written into a
  field `03_METRICS` consumes and `06_DATA_MODEL` persists. One hook on the shared
  `dxgi.dll` class vtable catches D3D11 and D3D12 alike, so the present call cannot tell
  them apart; the Overlay now asks the swapchain which device created it, once per
  swapchain, and caches it.
  - **Measured, and the obvious implementation was wrong.**
    `CreateSwapChainForComposition` takes a **command queue** for D3D12, so the first
    version queried the returned device for `ID3D12CommandQueue` — and every record from
    a real D3D12 target came back `FL_API_UNKNOWN`. DXGI resolves the queue to its owning
    device before storing it, so `ID3D12Device` is what answers. The queue query is kept
    anyway: it costs one failed QI per swapchain, and a DXGI that *did* hand back the
    queue would otherwise regress to `UNKNOWN` silently.
  - `apiMask` now records what was **seen presenting**, not what the process loaded — a
    title can link `d3d12.dll` and present through D3D11.
  - Both directions, which is what makes either assertion mean anything: a new
    `--hold-presenting-d3d12` harness mode builds device → command queue → swapchain, and
    the D3D11 case gained the mirror assertion, so a resolver that always answered D3D12
    fails there.
  - **OpenGL is not attempted.** `wglSwapBuffers` is a flat export and the hook is small,
    but `hook-harness` has no OpenGL mode, and shipping an unexercised hook into a game
    process is not something this project does. The harness mode is the prerequisite.

- **The safety stop and supervision loss, on the present path** (#43). `19_SAFETY` calls
  the mid-session stop the single most important runtime behaviour in the capture layer,
  and `legal/DISCLAIMER.md` promises both to the user. Until now neither existed —
  `FlRequestUnhook` set a status field while the hooks kept running.
  - `unhookRequested` → hooks out, status `UNHOOKED`. `guardTicks` stalled past
    `FL_GUARD_TICK_DEADLINE_MS` → the same. The clock starts when the **mapping is
    published**, not at first present, because `07_IPC` is explicit that "never advanced"
    and "stopped advancing" are the same state.
  - **Stopping is one-way.** Resuming ticks does not resume recording; a capture side
    that can un-stop itself is one whose stop is advisory.
  - **Two of three canaries red, and the third is recorded rather than hidden.** Removing
    `g_observing = false` and leaving only `MH_DisableHook` kept the suite **green** — so
    "the flag is necessary" is *not* a property this suite proves. The flag is kept
    deliberately (it closes the window for a thread already inside the hook body, and it
    is the only thing that holds if `MH_DisableHook` fails) and the comment now says so.
  - **Not covered, and stated rather than left to look covered:** the 65-second expiry in
    its real configuration. The canary proves the comparison fires, not that 65000 is the
    number on the shipped path.

- **The present hook, and a harness that presents while we inject** (#42). MinHook on the
  shared `dxgi.dll` class vtable, read off a throwaway WARP composition swapchain that is
  released immediately — slots 8 `Present`, 13 `ResizeBuffers`, 22 `Present1`, proved by
  behaviour in ctest `fl_vtable_indices`, never hardcoded.
  - **`--hold-presenting` had to exist first.** `--hold` presents 240 frames and *then*
    sleeps; those are over in milliseconds while `fl_guard_test` injects ~800 ms later, so
    an Overlay injected into `--hold` observes exactly **zero**. Every "N presents → N
    records" assertion written against it would have been vacuous — the same shape as the
    `DXGI_PRESENT_TEST` defect this harness was already fixed for once. The handshake test
    now asserts `writeIndex == 0` against `--hold` deliberately, so the trap has a test on
    it instead of a comment.
  - **Honesty, which is why #36 spent two bytes.** A present-only writer sets
    `measuredMask = FL_MEASURED_OUTPUT_RES` and `rtFlags = FL_RT_NOT_MEASURED` and claims
    nothing else. Leaving the mask at 0 with the zero-defaults would assert "no upscaler,
    no frame generation, no ray tracing" as measured fact ~118 times a second — producing
    `fg_factor 1.0` (rule 6) and a definite RT `No` (rule 7) about a title nobody looked at.
  - `FL_HOOK_GUARD` wraps **only our code, never the call to the original** — otherwise a
    game's own fault inside the trampoline is counted as ours. Three faults →
    `MH_DisableHook(MH_ALL_HOOKS)` and `status = self_disabled`.
  - `status` reaches `READY` only when hooks are actually installed; a failed
    `MH_Initialize` leaves it `INIT`, because `READY` would claim a capture side that does
    not exist and the Agent's degradation path is what should run.
  - Four canaries, each proven red: claiming everything measured, asserting a definite RT
    `No`, a `frameIndex` that stops advancing, and a swapchain never identified.

- **The DLL gets inside, maps its ring, and publishes a handshake** (#41). The first real
  code in `FrameLedger.Overlay`, which until then was a 30-line scaffold exporting one
  function. `DllMain` does **only** `DisableThreadLibraryCalls` and `CreateThread`;
  everything else runs on the init thread, outside the loader lock (§H2).
  - The mapping carries a DACL granting only the current user's SID.
    `BuildUserOnlySecurity` returns false rather than falling back to a default DACL — a
    mapping the machine can write is not a degraded version of this one, because the
    Agent's control block is in it and `unhookRequested` is the safety stop.
    `ERROR_ALREADY_EXISTS` also refuses.
  - `layoutVersion` is published **last**, behind a release fence, because it is the field
    a reader validates first: a reader that saw the version while `capacity` was still
    zero would compute a ring of no slots and read garbage.
  - **`status` is `INIT`, not `READY`, and that is the point** — nothing was hooked in that
    slice, so no record could ever arrive.
  - Three canaries, each proven red: claiming `READY` with no hooks, leaving `capacity`
    unpublished, and corrupting `buildId`.

- **The SPSC ring, and an honest account of which half the suite proves** (#40).
  `fl_ring.h` implements `07_IPC` §Protocol rules and nothing else; where the two
  disagree the document wins and the header is the bug. Drop accounting lives on the
  **reader**, the only side that knows what it consumed.
  - **Two of four canaries came back GREEN, and that is the finding.** *"The payload write
    steps over `seq`"* and *"the reader re-reads `seq`"* both survive, because the damage
    is observable only inside a 64-byte memcpy and neither a 1024-slot nor an 8-slot
    concurrency case lands in it across 200,000 records. **Those two properties are
    therefore unverified**, and the file header says so rather than letting nine
    assertions imply coverage.
  - **One test was wrong in a way that looked like the code was wrong**: "the payload write
    never touches `seq`" asserted on slot 1 because the record's `frameIndex` was 1 — but
    the slot is chosen by the publish counter, which starts at 0. It failed against a
    correct writer.
  - Claims `FL_MEASURED_HDR` (bit 7) in the last free window. `hdr` had no "not measured"
    state; #36 fixed that class for five other fields and missed this one. The byte is
    already written every frame, so it costs no layout change now — and after the C#
    mirror exists the identical edit is user-visible and a SemVer MAJOR.

- **`fl-probe-interposer` — the vtable premise, proven, and the Streamline
  question narrowed to a licence decision** (`20_OPEN_QUESTIONS` §H5 case 3,
  `spike-notes.md` §5, previously an empty template). It runs in **our own
  process**: no game, no injection, no guard, and it needs **no vendor headers**,
  only `GetProcAddress` and DXGI types from the Windows SDK.
  - **ctest `fl_vtable_identity_control`** asserts both directions of the property
    the whole hook design rests on — two independently created composition
    swapchains share one vtable, and a different interface does not. A comparison
    never shown to detect a *difference* carries no information when it reports
    "same", so the negative control is not optional.
  - **The interposer half is INCONCLUSIVE, and that is the result.** Loaded from
    Cyberpunk 2077 and Black Myth: Wukong, `sl.interposer.dll` forwards to
    `dxgi.dll` and leaves the factory *and* swapchain vtables untouched until
    `slInit()` has run. The probe enumerates its own modules, finds no `sl.*`
    plugin mapped, and exits 2 rather than rendering a verdict.
  - **Its first version got this wrong and the fix is the interesting part.** It
    printed *"the vtable is THE SAME — a hook DOES catch Streamline presents"* for
    both titles, which would have closed §H5 case 3 on a measurement of
    passthrough. The tell was already in its own output: an interposing
    Streamline cannot leave the **factory** vtable unwrapped, because wrapping the
    factory is how it reaches the swapchain. "Could not look" must not read as
    "looked and it was clean" — the guard's tri-state discipline, applied to a
    probe.
  - **The blocker is now named:** reaching the wrapped path needs `slInit`'s
    `sl::Preferences`, i.e. vendor ABI — the question `THIRD_PARTY_NOTICES.md`
    answers for Intel IGCL and nobody has asked for NVIDIA. That is a licence
    decision, not absent hardware. And the exposure is narrower than §H5 implied:
    NGX-direct titles never wrap the swapchain.

