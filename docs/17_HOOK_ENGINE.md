# 17 — Hook engine (`FrameLedger.Overlay`, C++20)

The injected DLL. Its whole job: install hooks, write 64-byte records into a ring, stay invisible in the frame budget, and never take the game down.

## Build profile

- C++20, MSVC v143+, x64 only, `/MT` (static CRT — never drag a VC runtime dependency into a host process we don't control).

> **x64-only is a scope decision with a user-visible consequence, stated here so
> it is not rediscovered during implementation.** An x64 DLL cannot be loaded
> into a 32-bit process, and Direct3D 9 titles — the visual-novel, JRPG and older
> indie catalogue — are almost entirely 32-bit. **D3D9 is therefore not a Tier-1
> API in v1**, and 32-bit games of any API are Tier 2 — which since 2026-08-28 means
> **not measured at all**, not "measured by a lesser route". Shipping a second 32-bit
> Overlay and injector would restore them, at the cost of doubling the native
> build matrix and adding a second struct-mirror surface; that trade is recorded
> in `20_OPEN_QUESTIONS` §Scope rather than left implicit in a build flag.
- `/GS /guard:cf /Qspectre`, `/O2`, no RTTI (`/GR-`), C++ exceptions disabled in this target (`-D_HAS_EXCEPTIONS=0`); error handling is return codes + SEH. **Both of the contentious flags are verified, not assumed** (`20_OPEN_QUESTIONS` §H1/§H3, `spike-notes.md` §3): `/guard:cf` coexists with MinHook trampolines because Windows treats `VirtualAlloc`'d executable memory as a valid CFG call target, and `std::atomic_ref` is lock-free at both widths under `-D_HAS_EXCEPTIONS=0`. The `fl_hook_profile` ctest re-checks both on every build.

  > ⚠ Know what `-D_HAS_EXCEPTIONS=0` actually buys. MSVC's STL does not delete its failure paths under that define — it rewrites `_THROW(x)` to `(x)._Raise()` and `_RAISE` to `__fastfail(5)`. Inside an injected DLL that means **a would-be exception kills the host game**, uncatchably, which SEH cannot intercept. It is safe here only because the hot path uses no throwing STL at all (`<atomic>`, `<cstdint>` and `<type_traits>` have zero throw-sites). That makes "no STL containers that allocate in hook paths" a load-bearing rule, not a style preference.
  >
  > ⚠ The CFG result was measured with **strict mode off**. A host that enables CFG strict mode does not auto-validate dynamically generated code and could still `__fastfail`. Rare, but real, and not something the fault policy can catch — see §Fault policy and `20_OPEN_QUESTIONS` §H8.
- No STL container that allocates in a hook path. `std::atomic` and fixed arrays are fine.
- VERSIONINFO populated (`ProductName=FrameLedger`, real company/version) — being identifiable is required by `19_SAFETY`.
- Exports: `FlGetLayoutVersion()` (added to this line 2026-10-04), `FlGetBuildId()`, `FlRequestUnhook()`, `FlGetStatus()`. Real names, no ordinal-only tricks.

## DLL entry

`DllMain(DLL_PROCESS_ATTACH)` does **only**: `DisableThreadLibraryCalls`, then `CreateThread` → `InitThread`. Nothing else — loader lock rules. All real work (dummy-device creation, MinHook init, hook installation, shm creation) happens on `InitThread`.

`InitThread` order:
1. Open/create shared memory `Local\FrameLedger.Ring.<pid>`; write the handshake header (layout version, build id, pid, api=unknown).
2. `MH_Initialize()`.
3. Resolve which graphics APIs are present (`GetModuleHandleW` on `d3d11.dll`, `d3d12.dll`, `dxgi.dll`, `opengl32.dll`) — and register a `LoadLibrary` hook so APIs loaded **later** still get hooked (many games load D3D12 lazily). **The `LoadLibrary` hook must not install hooks inline:** it runs under the loader lock, and MinHook suspends all threads to patch, which deadlocks against any thread waiting on that lock. It enqueues the request and signals the init thread, which installs outside the lock (`20_OPEN_QUESTIONS` §H2).

   > **Built 2026-09-06 (P1 item 1), with two differences from the sentence above.** The detour is on
   > `kernelbase.dll!LoadLibraryExW` — the one body every `LoadLibrary*` variant of `kernel32` reaches —
   > and it is installed **after** the present hooks, from `InitThread`, outside any loader lock. It
   > **enqueues nothing**: the module's real file name (`GetModuleFileNameW` on the handle the original
   > returned, into a stack buffer — never the caller's string, which may be relative or an API set) is
   > compared against two fixed tables and the detour then does one of two things, both a single store
   > plus `SetEvent` on the watchdog's auto-reset wake event. **(a)** A name that is in `FL_HOOK_INVENTORY`
   > or `FL_RUNTIME_CENSUS` bumps a saturating 15-bit wake count and wakes the watchdog, whose next
   > iteration runs the same lazy installers it always ran — so the install happens on the watchdog
   > thread, as before, but **within milliseconds of the module rather than on the next 1 Hz tick**
   > (measured in ctest `fl_guard` `[loader]`: 94 ms from the module appearing to the hook family being
   > published, against a 500 ms bound that discriminates the wake from the tick). **(b)** A name that
   > matches a MODULE family of the compiled anti-cheat floor (`fl_ac_floor.generated.h`, the same table
   > the guard carries — prefix, case-insensitive, allocation-free) records the family's 1-based index in
   > `earlyStopFamily` and wakes the watchdog; the **present path** stops the Overlay on its next
   > `MayObserve` and the **watchdog** stops it on its next iteration, whichever comes first, with the new
   > status `FL_STATUS_STOPPED_BLOCKLISTED` (= 4) — `19_SAFETY` §During a session, the in-process half;
   > `20_OPEN_QUESTIONS` §S6. The words are `FlWriterState.loaderSignals` @56 (bit 15 = installed, bits
   > 0..14 = wake count) and `earlyStopFamily` @58, both carved from `reserved[]` so the layout version did
   > not move. The original is always called first and its result returned untouched, and
   > `LOAD_LIBRARY_AS_DATAFILE` / `_EXCLUSIVE` / `AS_IMAGE_RESOURCE` loads are skipped — no code runs from
   > them and the loader does not list them. Body under `FL_HOOK_GUARD`, no allocation, no lock, no
   > logging (rule 5). Exercised by `hook-harness --load-after-ms N PATH` (up to four), against the real
   > Overlay in a real target, both jobs.
   >
   > **D33 (2026-09-26): one family may be tolerated.** Before `InstallLoaderHook`, `InitThread` reads
   > `Local\FrameLedger.Tolerate.<pid>` once (`fl_tolerance.h`; absent = nothing tolerated, the ordinary case),
   > resolves each name against its OWN compiled floor, honours only a family whose whole footprint there is
   > user-mode (`fl::guard::FamilyIsUserModeOnly`) and keeps a 64-bit mask over the floor's module families. The
   > detour's only change is one bit test before (b): a tolerated family's module loading late is not an early stop.
   > **Overhead:** one relaxed-acquire `uint64` load, a shift and a compare, on the floor-match branch alone — a branch
   > that fires approximately never — and nothing on the present path; the tolerance is read on the init thread, not in
   > a hook body (and logged there as `TOLERANCE`). ctest `fl_guard` `[D33]` loads a decoy `NEP2.dll` 2.5 s in: the
   > Overlay keeps recording when the mapping names NetEase Yidun, and stops when it names Easy Anti-Cheat.
4. Install present hooks for what exists (below).
5. Install feature hooks (upscaler / RT / PSO) lazily — the first time their module appears.
6. Publish `status = ready` in the handshake block.

## Getting vtable addresses (COM)

Never hardcode vtable indices. Create a throwaway object, read the pointer, hook the pointer:

```cpp
// D3D11/DXGI: create a 1x1 swapchain on a dummy HWND (or DXGI_SWAP_EFFECT_FLIP_DISCARD w/ CreateSwapChainForComposition)
// D3D12: D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_11_0, ...) then dummy command queue + swapchain
void** vtbl = *reinterpret_cast<void***>(dummySwapChain);
MH_CreateHook(vtbl[kPresentIndex], &Hook_Present, reinterpret_cast<void**>(&Orig_Present));
```

Index constants (`IDXGISwapChain::Present = 8`, `Present1 = 22` on `IDXGISwapChain1`, `ResizeBuffers = 13`) live in ~~`HookIndices.h`~~ `FrameLedger.Overlay/include/fl_dxgi_vtable.h`, shared with the harness; the D3D12 command-list slots in `fl_d3d12_vtable.h` (corrected 2026-10-04: no `HookIndices.h` exists).

> ⚠ **"Verified at runtime against the dummy object" is not implementable, and
> must not be reintroduced.** A vtable slot holds a bare function pointer: no
> name, no signature, nothing identifying which method it is. Reading `vtbl[8]`
> tells you an address, not that the address is `Present`. The only runtime
> checks available are structural — non-null, inside a mapped image, table long
> enough — and none of them distinguishes slot 8 from slot 9.
>
> **What validates the indices is behaviour, and it is now a test.** `hook-harness
> --probe-vtable` (ctest `fl_vtable_indices`) patches each slot, calls the
> method, and asserts the detour ran. Verified on MSVC 19.51 / Windows 11
> 26300: **slot 8 = `Present`, slot 13 = `ResizeBuffers`, slot 22 = `Present1`**.
> `IDXGISwapChain` and `IDXGISwapChain1` return the same vtable pointer, so slot
> 22 is reachable from either interface pointer.
>
> The harness runs **headless on a GPU-less CI runner** — WARP for the device
> and `CreateSwapChainForComposition` so no `HWND` or interactive window station
> is needed. That was the obstacle `20_OPEN_QUESTIONS` §H4 raised to testing
> this anywhere but a dev machine, and it is closed.
>
> Release the dummy objects immediately after reading vtables.

> **Patching a vtable entry patches every instance.** The table lives in
> `dxgi.dll`'s read-only data and is shared by all objects of that concrete
> class, so `VirtualProtect` + write affects the whole process. That is *why*
> the dummy-object technique works at all — and it is also why §H7's
> restore-only-if-unchanged rule matters, since another overlay may have
> patched the same shared slot after us.

~~Prefer hooking the **vtable entry** over inline-patching for COM methods (cleaner uninstall, no trampoline hazards);~~ *(corrected 2026-10-04: the Overlay swaps no vtable entry. It reads the slot off a throwaway object and MinHooks the function it points at, so one patch covers every instance (§Ray tracing); §Compare-and-restore explains why the swap is not the cleaner uninstall.)* Use MinHook inline hooks for flat C exports too (`wglSwapBuffers`, the Streamline and FidelityFX entry points ~~, NGX/FFX/XeSS~~ — NGX and XeSS have none).

## Hook inventory

Every hook must be listed here with a purpose. Anything not on this list is not allowed to exist (`19_SAFETY` review checklist).

> **This table is a SPECIFICATION. ~~As of 2026-09-04 the Overlay installs TWELVE DETOURS —
> the eight of 2026-09-03 plus four `ffxDispatch` trampolines, one per AMD module a game
> calls — and the FidelityFX row below is the ninth ✅.~~** *(Corrected 2026-10-04: as of 2026-09-06 it installs up
> to SIXTEEN — the nine `FL_HOOK_INVENTORY` rows (four Streamline including `slSetTagForFrame`, four `ffxDispatch`,
> `ffxFsr3ContextDispatchUpscale`), the three DXGI slots, the two `ID3D12GraphicsCommandList4` methods,
> `wglSwapBuffers` and `LoadLibraryExW`; four hooks landed without this banner moving.)* *(And since beta.25,
> 2026-10-19: up to TWENTY — four `ffxQuery` rows join them, on the same four AMD modules, D65.)* The counts differed until 2026-09-03, because a
> row is a *capability* and a detour is a *patch*, and the FG-count row rode on the
> evaluate detour; it has its own now, `sl.interposer.dll!slGetNewFrameToken`. The AMD row
> is the opposite shape — **one row-family, four modules, four patches from one
> template body** — because the three leaves and the SDK 2.x loader export the same five
> names and each must be patched at its own address; and its family is published only once **every leaf the
> process has loaded** is patched, which is `PublishHookFamily`'s own rule and was measured
> mattering before it was applied (a fixture caught the window between the two 2.x leaves'
> patches, in which UPSCALE dispatches were counted and the PREPARE and FRAMEGENERATION
> dispatches on the other leaf were not).
> `Present` (slot 8), `ResizeBuffers` (13) and `Present1` (22) go on the shared
> `dxgi.dll` class vtable — indices proved by behaviour, never hardcoded (ctest
> `fl_vtable_indices`); `sl.interposer.dll!slEvaluateFeature` and
> `sl.interposer.dll!slSetTag` are resolved module-scoped and installed lazily by the
> watchdog. The sixth ✅ row, **FG evaluations per present, has no detour of its own**:
> it is the same `slEvaluateFeature` patch answering a second question, which is why its
> inventory row carries a compound family and a `static_assert` pinning it.
> `ID3D12GraphicsCommandList4::DispatchRays` and `::BuildRaytracingAccelerationStructure`
> are the two newest, and they are of a **third kind again**: the target address is read off
> a command list created on the game's own device and RESET — see §Ray tracing, where the
> Reset is load-bearing — and no vendor symbol is resolved by name, so `FL_HOOK_INVENTORY`
> and `hookinventory-check` do not cover them at all.
> **Every other row below is unwritten**, including
> ~~`SetFullscreenState`~~ (polled instead since 2026-09-27, its row below), `SetColorSpace1`, `CreateSwapChain*`, ~~`wglSwapBuffers`~~ (built 2026-09-06, #141, its row below),
> `ID3D12Device5::CreateStateObject`, §Pipeline and §Memory/latency.
>
> Stated here because the distinction is invisible from the table. What a writer may then
> claim is **derived from `hooksInstalledMask`, not asserted per build**: a present-only
> writer sets `measuredMask = FL_MEASURED_OUTPUT_RES | FL_MEASURED_PRESENT_ARGS` and
> `rtFlags = 0` (v3: the bits are *observed*, so zero is honest), and the fields the
> unwritten rows would fill are marked *not measured* rather than defaulted to "none"
> (`fl_shm.h` §FlMeasured, CLAUDE.md rules 6 and 7). Both sides check that derivation —
> `MeasuredFacts.EntitledBy`/`IsHonest` and `guard_test.cpp`'s twin of them, as a SUBSET
> test in both places.
>
> **This banner said "three" while five were built, and `slSetTag` had no row at all.**
> NFR-11 makes this table the enumeration of the hook set, and `hookinventory-check`
> never reads this file — it checks `FL_HOOK_INVENTORY` against `vendor-exports.json` —
> so a shipped hook missing from here is invisible to every gate. Flip the row **and this
> banner** in the same PR that builds a hook.

### ~~The one system-module detour~~ The two system exports patched by name — a hook, and the asymmetry stated *(heading corrected 2026-10-04: `wglSwapBuffers` above is the other)*

| Hook | Purpose |
|---|---|
| ✅ `opengl32.dll!wglSwapBuffers` (2026-09-06, P1 item 4) | The OpenGL present hook (§Presentation). A system module, not a vendor one, so — like the row below — it is deliberately **not** in `FL_HOOK_INVENTORY` and `hookinventory-check` does not cover it; listed here so the table above and these two rows together are the whole inventory |
| ✅ `kernelbase.dll!LoadLibraryExW` (2026-09-06, P1 item 1) | The wake for lazily loaded runtimes and the in-process anti-cheat stop — §DLL entry step 3 above carries the mechanism. **Rule 4:** it reads one argument the API was given (`flags`) and one property of a handle the loader just returned to *its own caller* (the module file name); nothing of the game's. **~~It is the only hook on a system module, and~~ Like `wglSwapBuffers` above it, it is deliberately not in `FL_HOOK_INVENTORY` (corrected 2026-10-04):** that table is *vendor* symbols resolved by name and gated by `hookinventory-check` against `vendor-exports.json`, and `LoadLibraryExW` is neither vendor nor uncertain. It is listed here instead so the table above and this row together are the whole inventory. It **never** installs, uninstalls or patches anything itself (§H2) |

### Watchdog observations — not hooks, and listed here so nobody mistakes them for one

| Observation | Mechanism | Rule 4 | Why |
|---|---|---|---|
| **Runtime census** (`FlWriterState.runtimeCensus`, 2026-09-03) | Once a second on the watchdog: `GetModuleHandleExW(UNCHANGED_REFCOUNT)` for each name in `FL_RUNTIME_CENSUS` (`fl_hook_inventory.h`); OR-ed into the writer state. No enumeration, no reference, no patch, nothing on the present path | A module-name question to the loader — the same class `ResolveScoped` already asks before installing a hook. Reads no game memory | Rung 0 of `03_METRICS`' FG ladder is where every title lands today, and without this a 2D title and a DLSS-G title on an unhooked route printed the same line. **Never a producer of `NONE`** — statically linked FSR has no module to see. Names gated by `hookinventory-check` Pass D against the measured module list |

### Presentation
| Hook | Purpose |
|---|---|
| ✅ `IDXGISwapChain::Present`, `Present1` | Frame boundary, QPC, sync interval, present flags. **`DXGI_PRESENT_TEST` calls are dropped without a record** — the occlusion probe submits nothing, and a minimised game emits a stream of them (`07_IPC` §Protocol rules). **Since 2026-09-05 the hook also reads `IDXGISwapChain::GetLastPresentCount` on the chain it just saw** (rule 4: the same COM object `FindOrAdd` already calls `GetDesc` on, no new hook) and publishes `dxgiPresentSamples` / `dxgiPresentsUnseen` — DXGI's count of presents on that chain that this hook never saw, the in-process half of `20_OPEN_QUESTIONS` §H5 Leg 1. One virtual call per hooked present, costed by `hook-harness --probe-frames`. **And per record since the same night: `dxgiUnseen` @52 under `FL_MEASURED_DXGI_PRESENTS`**, so the consumer's window counts `Displayed = hooked + unseen` per bucket (`03_METRICS` §Displayed may be DXGI-COUNTED) — measured 2.90 / 2.95 on DL:TB at Streamline 2.8.0, 0 on 2.7.3 and 2.10.3. **And since 2026-09-06 (P1 item 2) the same read, at the FIRST hooked present on the first hooked chain, is published once as `FlWriterState.dxgiPresentsBeforeHook` @60** — DXGI's count of presents that ran before the hook was in, launch mode's cost meter (`20_OPEN_QUESTIONS` §S1); 0xFFFFFFFF until a present is seen, set before the present hooks go in. **Corrected 2026-09-14 (`fl_dxgi_count.h`, ctest `fl_dxgi_count`):** a reading that went *backwards* — a chain released and re-created at the same address inherits the slot's old count — used to wrap `now − previous` to ~4e9, saturate the record byte at 255 (the `DxgiSaturated` refusal) and add the wrap to the session total: the owner's ledger held `dxgi_unseen_total = 4294967243`. A regression is now a reset (no delta claimed, nothing added, the slot re-armed) and the total saturates at `UINT32_MAX` rather than wrapping. Cost: one compare on the same virtual call; measured per `14_TESTING` §Hook overhead in the PR |
| ✅ `IDXGISwapChain::ResizeBuffers` · ⏳ `ResizeTarget` | Output resolution changes mid-session. `ResizeBuffers` re-reads the swapchain description *after* the original returns; `ResizeTarget` is not hooked |
| ~~⏳ `IDXGISwapChain::SetFullscreenState`~~ **not hooked — polled (2026-09-27, beta.10)** | ~~Fullscreen ↔ borderless transitions~~ **Region 4, `FlDisplayState` (`07_IPC`, layout 4).** DXGI leaves exclusive fullscreen by itself — focus lost, Alt+Enter inside its own window hook — without the title calling this method, so a hook would miss exactly the transitions a mode share must count. Instead the present hook asks, **at most every 500 ms per chain**, two getters on the chain the title passed: `GetFullscreenState(&fs, nullptr)` (no `IDXGIOutput` taken) and `GetDesc` (the window, the back buffer, the swap effect). Rule 4: the same object `FindOrAdd` already calls `GetDesc` on and the counter reads — no new hook, no read beyond it; the window's geometry and the classification are the Agent's, out of process. `GetDesc` again also refreshes the size every record carries, which closes `IDXGISwapChain3::ResizeBuffers1` (unhooked: a D3D12 title resizing through it kept its old size until now). Published through a seqlock the writer never waits on (a taken lock skips the sample). OpenGL publishes the window and client size `ReadGlSize` already holds, with no exclusive bits. Cost per `14_TESTING` §Hook overhead in the PR |
| ⏳ `IDXGISwapChain3::SetColorSpace1` | HDR output detection. **`IDXGISwapChain3`, not 4** — 4 adds only `SetHDRMetaData` (`20_OPEN_QUESTIONS` §H9 — *answered and removed with H1/H3/H4 in the P0 spike (`6b1fa83`); this row used to say `IDXGISwapChain4`, and the corrected interface here IS the answer, so the citation points at history rather than at a live entry*). Unbuilt, which is why `colorSpace` reads `NOT_REPORTED` and `FL_MEASURED_HDR` stays clear. **When it is built, the writer must initialise `colorSpace = FL_COLOR_SPACE_SDR` at swapchain identification** — an SDR title never calls `SetColorSpace1`, so "hook live, no call" would otherwise sit at `NOT_REPORTED` forever and HDR's definite `No` would be unreachable. DXGI documents G22/Rec.709 as the default, which makes SDR a *measured* default rather than an affirmative negative |
| ⏳ `IDXGIFactory::CreateSwapChain*` | Capture swapchain desc (format, buffer count, swap effect, flags) at creation. Unbuilt — the swapchain description is currently read on demand in the present hook via `GetDesc` |
| ✅ `wglSwapBuffers` (2026-09-06, P1 item 4) | OpenGL titles: one record per `opengl32!wglSwapBuffers`, `api` = OpenGL, the output size from `GetClientRect` on the window the HDC belongs to (first sighting and every 256th present, so a resize is caught without a second hook), **no `FL_MEASURED_PRESENT_ARGS`** — the call has no sync interval and no flags. A MinHook inline patch on the flat export (measured 2026-08-05: a `jmp` thunk into the vendor ICD, relocated like any first instruction), installed at init when `opengl32.dll` is already mapped and lazily on the watchdog otherwise, with the `LoadLibrary` detour waking it for `opengl32.dll`. Exercised by `hook-harness --opengl --hold-presenting N`, which calls **gdi32's `SwapBuffers`** the way a title does — measured to reach the hook (ctest `fl_guard` `[opengl]`, on Microsoft's generic renderer, so it runs on CI too). The one system-module row below carries the inventory asymmetry |
| ✅ Vulkan `vkQueuePresentKHR` (2026-09-06, P1 item 3) | via layer, not hook (below). Frame boundary and QPC, one record per present; `vkCreateSwapchainKHR` / `vkDestroySwapchainKHR` intercepted beside it for the output size (`imageExtent`, the API's own argument — rule 4). **No `FL_MEASURED_PRESENT_ARGS`**: `vkQueuePresentKHR` carries no sync interval and no flags, which is why that bit exists. §S2's in-layer supervision check landed with it, on the present path. Measured under the real loader on the harness's `--vulkan` mode: 241 records in 4 s, status READY, `apiMask` = Vulkan (ctest `fl_vklayer_real`) |

### Upscaling / frame generation (the accuracy problem this rewrite exists to solve)
| Hook | Yields |
|---|---|
| NGX: `NVSDK_NGX_D3D11/D3D12/VULKAN_CreateFeature`, `EvaluateFeature`, `ReleaseFeature` | Which NGX feature is *actually created and evaluated per frame*: SuperSampling (DLSS), RayReconstruction (DLSS-D), FrameGeneration (DLSS-G) |
| NGX parameter accessors (`NVSDK_NGX_Parameter_SetI/GetI/SetUI`) — **exported by `sl.common.dll` only; see below** | `Width`/`Height` (render) vs `OutWidth`/`OutHeight` (output), `PerfQualityValue` (quality preset), sharpness |
| ✅ Streamline: `slEvaluateFeature` · ~~⏳ `slInit`~~ *(not hooked, and not to be: owner decision D129, 2026-11-12 — its row below says why)*, ~~`slSetFeatureLoaded`~~ *(its own row below since beta.45)*, ~~`slSetConstants`~~ *(its own row below since beta.43)*, ~~`slGetFeatureRequirements`~~ *(its own row below since beta.44, with `slGetFeatureVersion`)* | Feature set actually active when the game goes through SL rather than NGX directly (`kFeatureDLSS`, `DLSS_G`, `DLSS_RR`, `Reflex`, `NIS`; since beta.34 `kFeatureDLSS_NR` — DLSS 5's neural rendering, its own fact bit and family `FL_HOOK_SL_NEURAL_RENDERING`, D84; rule 4: the `feature` argument of the API hooked, nothing else). **Built 2026-08-09**, identity only: one MinHook detour on `sl.interposer.dll!slEvaluateFeature`, resolved **module-scoped** and installed lazily by the watchdog. Sets `FL_MEASURED_UPSCALER` + `FL_HOOK_UPSCALER_IDENTITY`; decodes `kFeatureDLSS`/`kFeatureNIS` and reports `FL_UPSCALER_UNKNOWN` for anything else. **Never `FL_UPSCALER_NONE`** — a Streamline-only writer cannot see FFX, XeSS or NGX-direct, and `NONE` is the only one of the three states that may be aggregated as a negative. **Since 0.1.0-beta.40 (Advanced metrics train B, D111), and only while the Agent asks** (`FlControlBlock.advancedRequested`): the `sl::DLSSOptions` a title chains into `inputs` — the struct the quality mode is read from — is read further: `outputWidth`/`outputHeight`, `preExposure`, `exposureScale`, `colorBuffersHDR`, the five presets of version 1, and where the struct's own `structVersion` proves them present `ultraQualityPreset` and `useAutoExposure` (≥ 2) and `alphaUpscalingEnabled` (≥ 3) — §Which struct version carries which field. Rule 4: more members of the same argument of the same call; `fl_adv_fields.h` is the one place, `tools/overlay-fields-check.ps1` holds it. No new patch site. **Measured** (the header's own functions, `/O2 /guard:cf /Qspectre /MT`, 20 M calls × 3): +9.5–11.7 ns per evaluation that chains the options while asked; with the word at 0, one relaxed load and a branch not taken — below what the bench resolves. Most titles set these through `slDLSSSetOptions`, which is not an export, and read N/A (train D) |
| ✅ Streamline: `slSetConstants` (0.1.0-beta.43, Advanced metrics train B; owner decisions D111–D116, D122) | **What the game tells Streamline about a frame**, once per frame and viewport, read from the `sl::Constants` it passes — rule 4: an argument of the call hooked, and of it only the members named here. **Read:** `jitterOffset`, `mvecScale`, `cameraNear`, `cameraFar`, `cameraFOV`, `cameraAspectRatio` and the seven `Boolean`s (`depthInverted`, `cameraMotionIncluded`, `motionVectors3D`, `reset`, `orthographicProjection`, `motionVectorsDilated`, `motionVectorsJittered`). **Never read:** the five matrices (bytes 32..351 of the struct), `cameraPinholeOffset`, and the camera's position and three axes (bytes 368..423) — `tools/overlay-fields-check.ps1` refuses their names anywhere in code loaded into a game, and ctest `fl_adv_fields` publishes two structs that differ in every one of those bytes and gets the same bytes out. **The one row that is not installed unless asked for** (D111): `InstallByFamily` returns before it resolves the symbol in a process the Agent has never asked for Advanced metrics — resolving takes a reference on the module — so with the option off the export's first bytes are the file's (`fl_guard` `[advanced]` reads them from the target). Once asked, the patch goes in on that watchdog tick and stays for the life of the process; the detour forwards without looking while the request is off, and after a stop (`MayObserve`). **Every call** checks the struct's GUID and a `structVersion` of at least 1 before any member is read, and the viewport handle's the same way; every member read is one version 1 carries, so a version-1 struct laid against unreadable memory is read whole without a fault (the same ctest). **Summaries are the first viewport's**: a title with two viewports sets constants for each every frame, and "the last value" across them would flip twice a frame; the call count is of every viewport. `FLT_MAX` is Streamline's "not set" here and is kept nowhere (D119's plane at infinity is FidelityFX's). Family `FL_HOOK_SL_CONSTANTS` (bit 13), published with the patch: it entitles no field of a frame record, and tells "hooked, never called" from "not hooked". **Measured overhead** (the detour's body on the headers, the Overlay's flags, 20 million calls a run, three runs): not asked **+0.57 / +0.60 / +0.58 ns**; asked, the summarised viewport **+19.61 / +19.54 / +19.79 ns**; asked, another viewport +5.83 / +6.01 / +5.62 ns. |
| ✅ Streamline: `slGetFeatureVersion` and `slGetFeatureRequirements` (0.1.0-beta.44, Advanced metrics train B's third step; owner decisions D111–D116, D122) | **What Streamline answers when the game asks about a feature** — which version it is, and what it requires. Two rows, two patches, two detours of one shape: **the original runs first, exactly once, on every path, with both arguments forwarded untouched**, and the struct the game passed is read only when the original said `eOk` — a refusal leaves the struct as the game made it, which is not an answer. Rule 4: an argument of the call hooked, read after the call, and of it only the members named here. **Read from `sl::FeatureVersion`:** `versionSL`, `versionNGX`. **Read from `sl::FeatureRequirements`:** `flags`, `maxNumCPUThreads`, `maxNumViewports`, `numRequiredTags`, `osVersionDetected`, `osVersionRequired`, `driverVersionDetected`, `driverVersionRequired`. **Never read:** `requiredTags` — the pointer the count belongs to — and everything after `driverVersionRequired`: the Vulkan queue counts and the pointers to the names of extensions and features. **No pointer of the game's is followed** (`tools/overlay-fields-check.ps1`, parameters `fv` and `fr`; ctest `fl_adv_fields`: two answers that differ only in those bytes publish the same region, and an answer cut off where the Vulkan members begin, laid against unreadable memory, is read without a fault; the injected `fl_guard` case's stub hands back wild pointers and no fault is counted). **Installed only where asked for** (D111), as `slSetConstants` is: `InstallByFamily` returns before it resolves either symbol in a process the Agent has never asked for Advanced metrics; once in, a patch stays for the life of the process and its detour forwards without looking while the request is off. **They are the first two rows of the table**: a game asks these while it starts, the watchdog patches rows in the table's order, and each patch stops the game's threads for a moment (0.1–0.25 s apart in the owner's logs of 2026-11-12, where `slSetConstants` was the fifth Streamline patch in, 1.9–7.8 s after the attach). *(Since 0.1.0-beta.46 the rows a tick finds go in together, under one stop of the threads — §Unhooking, Batches — so the order no longer decides which row waits behind which; it is still the order of their log lines.)* In a process never asked they are passed over and the order changes nothing. ~~**Most sessions will still have missed the calls** — an attach after the game's start-up is the usual case — and the session then says the call was hooked and the game did not ask again.~~ *(Measured 2026-11-12, the owner's eight sessions on beta.44: both calls were read in six of six Streamline sessions on five games — Unreal's Streamline plugin asks again after the attach, Cyberpunk 2077 asks again for requirements and not for versions. A game that asks only while starting still reads "hooked, not asked again".)* Three conditions before a read, as for `slSetConstants`: the Agent is asking, observing has not stopped, and the struct carries its own GUID and a version of at least 1 on this call (a struct with another GUID is not kept: the injected case sends one). Every member read is version 1's (§Which struct version carries which field). **Twelve slots** in region 5 (`FlAdvancedState.features`, layout 5 as it was), one per feature in the order first asked about; the last answer about a feature is the one kept; a thirteenth feature is not kept. **A new request does not empty them**: what version a feature is and what it requires are facts about the process, and a second session of a game that kept running would otherwise have nothing where the first had the answer — the one group `Reset` leaves, with its bit of the mask. Streamline 1.x is refused before anything is resolved (`SpeaksStreamline2`). Families `FL_HOOK_SL_FEATURE_VERSION` (bit 15) and `FL_HOOK_SL_FEATURE_REQUIREMENTS` (bit 16), each published with its patch; neither entitles a field of a frame record. **Measured overhead** (the detours' bodies on the headers, the Overlay's flags, 20 million calls a run, three runs, eight features asked about in turn): the version — not asked **+0.58 / +0.61 / +0.58 ns**, asked **+14.82 / +14.54 / +14.47 ns**; the requirements — not asked **+0.58 / +0.63 / +0.61 ns**, asked **+20.88 / +21.01 / +20.96 ns**. A game makes these calls a handful of times. |
| ✅ Streamline: `slIsFeatureSupported`, `slIsFeatureLoaded`, `slSetFeatureLoaded` (0.1.0-beta.45; owner decisions D129–D131) | **Whether a feature is supported, whether it is loaded, and the game switching it on and off.** Three rows, three patches. Rule 4: bare arguments and results of the calls hooked, and no vendor struct at all — the feature's id; the `sl::Result` `slIsFeatureSupported` returned, **every result**, since a refusal is the answer to that question (the original runs first, both arguments forwarded untouched); the `bool` `slIsFeatureLoaded` wrote, read only when it returned `eOk`; the `bool` the game passed to `slSetFeatureLoaded`, noted before the call. **The adapter `slIsFeatureSupported` is asked about is never read** (D131): `sl::AdapterInfo` holds the adapter's LUID or a Vulkan physical device; the detour forwards the reference and `tools/overlay-fields-check.ps1` refuses the names of its members (`deviceLUID`, `deviceLUIDSizeInBytes`, `vkPhysicalDevice`) anywhere in the Overlay and the Vulkan layer — the fixture's adapter carries a wild pointer. **Installed only where asked for** (D111), as the four rows before them. `slIsFeatureSupported` and `slIsFeatureLoaded` follow the two rows of beta.44 at the head of the table; `slSetFeatureLoaded` follows `slSetConstants`. **Kept in word 1 of the feature's slot** (region 5, layout 5 as it was): bit 2 and the result in bits 8..15, bits 3 and 4, bits 5 and 6 — the last two a session's, cleared by a new request; the rest the process's, kept (`fl_adv_fields.h` `kFeatureSessionBits`). A refused feature therefore has a slot now, with nothing in it but that word. The declarations of the three functions are the same from Streamline v2.0.0 to v2.14.16, and `sl::Result` is the same forty codes in the same order at all sixteen tags (`include/sl_result.h`, `05a3c7e3889c` … `8a844b68ff55`), so a code means the same whatever interposer answered. Families `FL_HOOK_SL_FEATURE_SUPPORTED` (bit 17), `FL_HOOK_SL_FEATURE_LOADED` (18), `FL_HOOK_SL_SET_FEATURE_LOADED` (19), each published with its patch. **Measured overhead** (the detours' bodies on the headers, the Overlay's flags, 20 million calls a run, three runs, eight features in turn): `slIsFeatureSupported` — not asked **+1.50 / +1.48 / +1.50 ns**, asked **+11.97 / +11.98 / +12.08 ns**; `slIsFeatureLoaded` — **+1.79 / +1.77 / +1.89 ns**, **+12.35 / +12.31 / +12.33 ns**; `slSetFeatureLoaded` — **+1.44 / +1.47 / +1.80 ns**, **+11.28 / +11.54 / +11.45 ns**. **`slInit` is not a row and will not be** (D129): a game calls it once, before the Overlay is in the process or before a patch can land — launch mode waits for the presentation runtime before it injects, nothing is ever patched on a game's thread (§H2), and a row that exists only for Advanced metrics waits for the Agent's request. **A stop takes about 160 ms for every patch it restores** (measured on the fixture, 0.1.0-beta.45: each `MH_DisableHook` snapshots the system's threads): twelve patches took 1.9 s, fifteen 2.4 s, and the watchdog's wait for the stop's log lines, 2 s until then, is now a quarter of a second for every patch the registry can hold. **Since 0.1.0-beta.46 (D134) a stop restores its patches under one stop of the threads**: eighteen in 76–90 ms on the fixture, where one by one they took 1.37–1.56 s (§Unhooking, Batches). |
| ✅ Streamline: `slAllocateResources`, `slFreeResources`, `slShutdown` (0.1.0-beta.45; owner decision D130) | **The game allocating and freeing a feature's resources, and shutting Streamline down — counted, and nothing else.** Three rows, three patches. Rule 4: the feature's id is the only argument looked at; the command buffer and the viewport of the two resource calls are forwarded and never read (the fixture's command buffer is a wild pointer), and `slShutdown` has no argument. **`slFreeResources` and `slShutdown` are on a game's way out**: each detour notes the call first and then calls the original, exactly once, and runs nothing of ours after it returns; the fixture goes on calling the stub after `slShutdown`, through every detour. **Installed only where asked for** (D111); the last three Streamline rows of the table. **Kept** in word 1 of the feature's slot as two four-bit counts — allocations in bits 16..19, frees in 20..23, each stopping at 15 and never carrying into its neighbour — and, for `slShutdown`, in `FlAdvancedState.slShutdowns` (@308, the first of three reserved words; layout 5 as it was) with `FL_ADV_VALID_SL_SHUTDOWN` (bit 6 of the mask). All three are a session's: a new request clears them. Families `FL_HOOK_SL_ALLOCATE_RESOURCES` (bit 20), `FL_HOOK_SL_FREE_RESOURCES` (21), `FL_HOOK_SL_SHUTDOWN` (22). The three declarations are the same from Streamline v2.0.0 to v2.14.16. **Measured overhead** (the detours' bodies on the headers, the Overlay's flags, 20 million calls a run, three runs): `slAllocateResources` — not asked **+0.70 / +0.72 / +0.80 ns**, asked **+10.43 / +10.53 / +10.74 ns**; `slFreeResources` — **+0.61 / +0.84 / +0.58 ns**, **+10.33 / +10.57 / +10.44 ns**; `slShutdown` — not asked within the bench's noise (+0.06 / −0.12 / +0.04 ns), asked **+3.39 / +3.37 / +3.52 ns**. Inventory 23 → 26 rows; a Streamline title with Advanced metrics asked for now carries thirteen patches on the interposer, and a stop restores each in about 160 ms (the row above). **In real games (2026-11-13: six sessions, five games)** `slAllocateResources` was not called once after the attach, `slFreeResources` once in one game (Phantom Blade Zero, frame generation's) and `slShutdown` once in one (Dying Light: The Beast); no session counted a fault, and the two games that fault while closing did so at the offsets they had before these hooks. Installing the thirteen interposer patches took 0.7 s to 2.8 s of a game's start, 57 to 215 ms a patch. The owner's word on both (2026-11-13): `slAllocateResources` keeps its patch and is looked at again with more sessions (D135); installs and restores are to be batched, one stop of the threads for a batch (D134, 0.1.0-beta.46). |
| ✅ Streamline: `slSetTag` — **and, since 2026-09-05, every tag's TYPE** (`slTagCensus`; a HUD-less / UI tag marks the present `FL_FG_DLSS_G`, rule 4: the same argument) | The **global** resource tags, where a title states the size of the buffer it is upscaling *from*: `kBufferTypeScalingInputColor`'s extent → `renderW/H` — **or, since 2026-09-05, the size the tagged `sl::Resource` declares when the extent is zero** (`TagSize` in `fl_sl_inputs.h`, shared by all three tag routes; GUID-checked; a Resource declaring no size is still the honest unknown). **Built 2026-08-15** (`FL_HOOK_UPSCALER_PARAMS`), and it is an ALTERNATIVE to the local tags in `slEvaluateFeature`'s `inputs`, not a layer over them — `sl_core_api.h:258` says global and local tags "do NOT interact", so a title using one yields nothing from the other and both are read. Published only on a frame where an evaluation was also seen, because a tag is viewport state that outlives any one frame. This row was MISSING from this table while the hook shipped, which is the gap the banner above now names |
| ✅ Streamline: `slSetTagForFrame` — **and every tag's TYPE, as above** | The same global tags through **Streamline 2.8's frame-based entry point**, which deprecates `slSetTag`: `(const FrameToken&, const ViewportHandle&, const ResourceTag*, uint32_t, CommandBuffer*)` — the token first, then exactly `slSetTag`'s list, handed to the same tag walk. **Built 2026-09-04** (`FL_HOOK_UPSCALER_PARAMS`, its own trampoline; the family is now **published when whole** — once every tag export the loaded interposer has is patched — because publishing on the first row entitled records the second was not yet producing, 38 of 41 on the fixture: the ffx leaves' #110 defect again, fixed the same way). Measured need: Dying Light: The Beast (SL 2.8.0) published `Dlss` identity on every batch and the params bit on none across three captures, because a 2.8 title that tags per frame never calls the deprecated export. The token is not read; the frame count stays with `slGetNewFrameToken`. The installer picks this detour by SYMBOL (`kSymbolSlSetTagForFrame`, bound to the row by `static_assert`), not by family, since the two rows share a family bit and differ in signature |
| ✅ **FidelityFX ffx-api: `ffxDispatch` on the FOUR modules a game calls** — `amd_fidelityfx_dx12.dll` (SDK 1.1.x monolith), `amd_fidelityfx_upscaler_dx12.dll`, `amd_fidelityfx_framegeneration_dx12.dll`, **and `amd_fidelityfx_loader_dx12.dll`** (added the evening of 2026-09-04: three loader-shipping titles at FSR produced zero dispatches at any leaf export, so the loader is where their calls arrive) · ⏳ FSR 3.0 host route `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale` / `ffxFsr3DispatchFrameGeneration` · ~~`ffxFsr2ContextCreate`~~ · ~~`ffxCreateContext`~~ | **Built 2026-09-04** (`HANDOFF` item 7c). One observer behind three trampolines reads the DESCRIPTOR the title passed — head type first, body only after the type matched a constant from the vendored MIT headers (`third_party/fidelityfx/`, tag v2.3.0): **UPSCALE** → identity (`FSR3` from the monolith, `FSR_UNVERSIONED` from the 2.x upscaler DLL, which hosts FSR 3.1 and FSR 4 behind one type) + `renderSize` → `renderW/H` (quality and sharpness `0xFF`: the dispatch carries neither — *since beta.25 the quality byte is the mode the `ffxQuery` row below names for the dispatched size, `0xFF` when none does*) + one COUNT *(since 0.1.0-beta.43, owner decision D125: **counted at the first source that saw an UPSCALE and at no other** — `g_ffxUpscaleOwner`, `fl::adv::Owns`. Cyberpunk 2077's loader 2.3.0 hands a dispatch on to the leaf's export — its frame-generation dispatch types are logged on the loader and on the leaf — so an UPSCALE through such a loader reaches two hooked modules, and one count per module read K = 1 as 2.0 on the fixture bound that way (`--ffx-topology 2x-export`: 60 presents, 120 counted). No title run so far upscales through one; it is a precaution. Measured on the header with the Overlay's flags: +0.02 to +0.03 ns on the module that is counted, about 4 ns less on the forwarded copy)*; **PREPARE / PREPARE_V2** → `frameID` counted on a *new* index only — this vendor's `slGetNewFrameToken`, the `fgEvaluations` producer once a title has ever prepared, with the UPSCALE count as the fallback for a title that never does; **FRAMEGENERATION** → `fgMode = FSR_FG` on that present. Rule 4: the argument of the API we hooked, never `context`, `pNext`, a command list or a resource. **Why all four:** a UE5 title ships the two leaves with no loader and calls their exports; a loader-shipping title calls the loader's export and the signed loader reaches its providers through an object, not the leaves' exports (measured: Dying Light: The Beast, KCD2, Wukong). The morning's "never the loader" rested on the export tables alone and was reversed by the run. What guards against a loader that DID re-enter a leaf's export is the K = 1 control with all four hooked and the consumer's `frames/upscale-drained`, both of which read 2× in that case. `ffxCreateContext` is deliberately NOT a row — nothing it carries fills a field the dispatch does not, and its honest family would collide with `slEvaluateFeature`'s under the installer's equality binding. The family is compound (`IDENTITY \| PARAMS \| FG_EVALUATIONS`), pinned by `static_assert`s that also bind the leaf table to the rows both ways and refuse the loader as a row. Streamline keeps precedence for the count once it has ever issued a token, so every §S31-validated title is byte-identical. Second count: the consumer prints `frames/upscale-drained`, the AMD `tokens/batch`. **FSR 3.0 — BUILT 2026-09-05 as a fifth AMD target:** `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale` (Cyberpunk's copy; named export, its own descriptor, vendored from tag **`fsr3-v3.0.4`** — the tag whose twelve `ffxFsr3*` declarations match the shipped module, not the `v1.1.4` first named). Rule 4: the detour reads `renderSize` of the descriptor the title passed and nothing else — never the context, a resource or the command list; the offset is pinned to a literal (1256) so a re-vendoring that moved it fails to compile. Same family and publish point as the four rows (`PublishFfxFamilyIfWhole` waits for a loaded host too), keyed by module in the installer; identity `FSR3` as a fact. `ffxFsr3DispatchFrameGeneration` is deliberately not a row (§H11 carries the reversal condition); the `ffx_fsr3upscaler_x64.dll` leaf is the pre-committed alternative if the facade reads silent (row B2) — never both, or `frames/upscale-drained` reads 2.00. **Since 0.1.0-beta.40 (Advanced metrics train B, D111), and only while the Agent asks:** the UPSCALE arm reads the rest of `ffxDispatchDescUpscale` — `enableSharpening`, `sharpness`, `jitterOffset`, `preExposure`, `cameraNear`, `cameraFar`, `cameraFovAngleVertical`, `flags`, `reset`, `frameTimeDelta` — into process-local summaries (an "ever seen" mask, a largest magnitude, a lowest and highest, a count and a sum; one module's dispatches are counted, so a dispatch that reaches the loader and a leaf is one). Never on the FSR 3.0 host's legacy descriptor, which is vouched for through `renderSize` and no further; never a PREPARE descriptor's camera arrays (`tools/overlay-fields-check.ps1` refuses their names anywhere in the Overlay). `cameraNear`/`cameraFar`/`cameraFovAngleVertical` are the depth range and the field of view, not where the camera is; the field of view is kept as a session's range, never a series (D114). **Measured** (as the row above): +22–23 ns per UPSCALE dispatch, i.e. per application frame, while asked; ≤ 0.7 ns, inside the bench's noise, with the word at 0 |
| ✅ **FidelityFX ffx-api: `ffxQuery` on the same four modules** (beta.25, owner decision D65) — `amd_fidelityfx_dx12.dll`, `amd_fidelityfx_upscaler_dx12.dll`, `amd_fidelityfx_framegeneration_dx12.dll`, `amd_fidelityfx_loader_dx12.dll` | **Built 2026-10-19.** The mode an FSR title runs at reaches the vendor only as an argument of `ffxQuery` — "what render size (`…GETRENDERRESOLUTIONFROMQUALITYMODE`, `0x00010003`) or ratio (`…GETUPSCALERATIOFROMQUALITYMODE`, `0x00010002`) does this mode give at this display size?" — asked before the title sizes its targets; the per-frame dispatch carries the size alone. The detour calls the original FIRST, exactly once, and only when it returned `FFX_API_RETURN_OK` reads the descriptor's type, display size and mode and the values the vendor wrote through the title's own out pointers, into a five-slot table (one per `FfxApiUpscaleQualityMode`); the `ffxDispatch` UPSCALE arm names the ONE mode whose kept answer fits the dispatched `renderSize` (and `upscaleSize`, where stated) as `0x10 + mode` in `upscalerQuality`, else `0xFF` (`03_METRICS` §Upscaling). **Rule 4:** the arguments of the API we hooked — the descriptor the title passed and the out values of that same call — never `context`, a resource or anything the title did not hand the hooked export; nothing of the title's is written, and every read is under the hook's SEH guard. Both query layouts and type ids are pinned to literals by `static_assert`s, identical at SDK v1.1.4 and v2.3.0. **Its own family**, `FL_HOOK_UPSCALER_QUALITY` (bit 11 of `hooksInstalledMask`, mirrored in `FlHookFamily` and `HookFamilies`): `InstallByFamily` binds a row by equality, and the query entitles no measured field by itself — the byte rides under `FL_MEASURED_UPSCALER_PARAMS` from the dispatch arm — so the bit says only that the query was hooked, which tells "never asked" from "could not have seen it ask". Published like the dispatch family, once every loaded leaf is patched (`PublishFfxQueryFamilyIfWhole`); the FSR 3.0 host has no `ffxQuery` and no row (`static_assert`). A loader that passes a query on to a leaf's export refreshes one kept answer twice; nothing is counted, so nothing is counted twice. **Measured overhead** (a copy of the two bodies, `/O2 /guard:cf /Qspectre /MT`, 20 M calls × 3 runs, every slot holding both a size and a ratio — the worst case): 7.6–8.2 ns added per UPSCALE dispatch, i.e. per application frame, and 1.6 ns per query beside the trampoline; the per-present path gains one load. Harness: `--ffx-query sizes|ratios`; `fl_guard` "FSR's quality mode". **Since 0.1.0-beta.40 (Advanced metrics train B, D111), and only while the Agent asks:** when the query the vendor answered OK is the upscaler's memory — `FFX_API_QUERY_DESC_TYPE_UPSCALE_GPU_MEMORY_USAGE` (`0x00010008`, ffx-api 1.1.2) or `…_V2` (`0x00010009`, 2.0) — the `FfxApiEffectMemoryUsage` it wrote through the title's own out-pointer is kept (total and aliasable bytes), one module's queries counted. Only a title that asks is answered: FrameLedger never calls `ffxQuery` itself. The frame-generation memory queries (`0x00020007`, `0x0002000b`) are not read. +4.3–6.1 ns per such query while asked. Harness: `--ffx-memory-query v1|v2`; `fl_guard` "region 5 stays empty until the Agent asks". |
| ~~XeSS: `xessD3D12CreateContext`, `xessD3D12Init`, `xessD3D12Execute`~~ **Closed at the census by licence, 2026-09-04** (`20_OPEN_QUESTIONS` §Scope decisions, `18_GPU_VENDOR_APIS` §Intel XeSS SDK): no hook on any `libxess*.dll` export *(corrected 2026-10-04)* | ~~`outputResolution`, `qualitySetting`, XeSS version; `xess_fg` variants for XeFG~~ |
| ✅ **FidelityFX ffx-api: `ffxConfigure` on the same four modules** (0.1.0-beta.43, Advanced metrics train B; owner decisions D111–D116, D122) | **How the game configures FSR frame generation** — a title calls `ffxConfigure` with a frame-generation descriptor, usually every frame. Rule 4: an argument of the call hooked, and of it only what is named here. **Read**, and only when the header's type is `FFX_API_CONFIGURE_DESC_TYPE_FRAMEGENERATION` (`0x20002`): `frameGenerationEnabled`, `allowAsyncWorkloads`, `flags`, `onlyPresentGenerated`, `generationRect`. **Never read:** `swapChain`, the two callbacks and their contexts, `HUDLessColor`, `frameID`; and of every other configure type — key-value pairs, the distortion field, the camera info — nothing but the header's type. ctest `fl_adv_fields` publishes two descriptors that differ in all of those and gets the same bytes out, and hands the detour a key-value descriptor, which is shorter, without a read past it. **Installed only where asked for** (D111), as `slSetConstants` is: `InstallByFamily` returns before it resolves anything in a process never asked for Advanced metrics, so with the option off the export's first bytes in the target are the file's (`fl_guard` `[advanced]`). Published when every ffx-api module the process loaded is patched (`FL_HOOK_FFX_CONFIGURE`, bit 14), as the dispatch and query families are. **Counted at the first module that saw a frame-generation configure** — an owner slot of its own: the fixture's loader has always handed `ffxConfigure` on to the leaf's export, and Cyberpunk 2077's loader does the same with dispatches, so one call reaches two hooked modules (the injected case reads one configure a frame through the loader, not two). The rectangle is stored in one word, sixteen bits a side, so a publication never carries half of one rectangle beside half of the next; a side that is negative or past fifteen bits makes it four `-1`. The descriptor's type is kept for the log whatever it is (`FFX_CONFIGURE`, §Native logging). The detour consults `MayObserve()` first. **Measured overhead** (the detour's body on the headers, the Overlay's flags, 20 million calls a run, three runs): not asked **+1.72 / +1.68 / +1.78 ns** (the type kept for the log); asked, the counted module **+15.02 / +15.55 / +15.11 ns**; asked, the forwarded copy +2.70 / +2.81 / +2.65 ns; asked, another configure type +2.43 / +2.55 / +2.47 ns. |
| ✅ **Application frames per present** (`fgEvaluations`) — **`sl.interposer.dll!slGetNewFrameToken` since 2026-09-03** | Native vs Displayed frame counts at Tier 1 — see `03_METRICS` §Frame Generation. **The producer is the frame-token export**: Streamline hands a title one token per application frame and the title must ask; the detour counts distinct tokens between two presents (pointer changes, nothing dereferenced), `FL_HOOK_FG_EVALUATIONS` is its family, and it installs on every Streamline 2 title including the ones that evaluate nothing through `slEvaluateFeature`. Rule 4: the out-parameter of an API we hooked. Premise and its gate in `03_METRICS`; pre-committed table in `20_OPEN_QUESTIONS` §S31. *The row as it stood before:* ~~NGX/SL/FFX/XeSS FG feature evaluations per present. **Built 2026-08-15 for Streamline, and MEASURED TO YIELD NOTHING ON THAT ROUTE**~~ — `slEvaluateFeature(kFeatureDLSS_G)` is never called by Cyberpunk 2077 (0 across ~14,000 batches, four FG settings), so this row installs, is honest, and counts zero; see `docs/HANDOFF.md` item 3. Built on the detour that was already there: `kFeatureDLSS_G` contributes to a saturating COUNT in the same word the feature bits live in (`fl_sl_seen.h`) rather than to a bit, because a bit collapses several evaluations between two presents into one — and under multi-frame generation that is the common case, not the edge. The row's family is therefore compound (`FL_HOOK_UPSCALER_IDENTITY \| FL_HOOK_FG_EVALUATIONS`), pinned by a `static_assert` because `hookinventory-check` reads that column as an opaque identifier. **Counts EVALUATIONS, not generated frames** — the 2026-08-14 owner ruling; the multiplier lives in `sl::DLSSGOptions`, which is only reachable by the route §2b refused. ~~Non-NVIDIA FG vendors are **deferred with a written rationale**: `amd_fidelityfx_framegeneration_dx12.dll` 3.1.5 exports only the five generic `ffx*` entry points, identical to five sibling modules, so identity is in the arguments and we have no headers~~ — **AMD has a producer since 2026-09-04** (the FidelityFX row above: the PREPARE dispatch's `frameID`, or the UPSCALE count on a title that never prepares, feeds `fgEvaluations` when Streamline has never issued a token); Intel stays at the census (`20_OPEN_QUESTIONS` §Scope decisions, by licence); the FSR 3.0 host DLL's UPSCALE (`ffxFsr3ContextDispatchUpscale`, since 2026-09-05) counts like an ffx-api UPSCALE when neither Streamline nor a PREPARE has spoken |

> `GetFrameStatistics().PresentCount` is **not** in this inventory and must not be
> re-added as an FG signal. It counts presents the application submitted through
> the swapchain — the same events our present hook already intercepts — so the
> difference is structurally zero, not merely unreliable. An earlier revision
> used it to claim driver-level FG (AMD AFMF) detection; that claim was wrong and
> would have read as "no frame generation" rather than as a failure.

> ⚠ **Symbol names above were vendor SDK conventions. They are now measured** — `tools/vendor-exports.ps1` resolves them against the DLLs installed titles actually ship, and the result is committed as `docs/vendor-exports.json` (34 distinct modules across 162 files on the dev machine). Resolve everything dynamically by name with a null-check + capability flag; a missing export must degrade to "unknown", never crash. **A wrong name degrades silently to `unknown`, which reads as "working, no upscaler detected" — the highest false-confidence risk in the spike, which is why this is data and not prose.**

### The NGX parameter surface splits into two hook classes — measured

This is the finding P0 item 5 existed to produce, and it changes what the feature-hook phase has to build.

| Module | Exports the parameter **accessors** (`NVSDK_NGX_Parameter_SetI/GetI/…`) | Exports the parameter-object **factories** (`AllocateParameters`, `GetCapabilityParameters`, `GetParameters`) |
|---|---|---|
| `sl.common.dll` (Streamline) | **yes** — all 16 | yes |
| `nvngx.dll` / driver-store `_nvngx.dll` (NGX core) | **no** | yes, for D3D11/D3D12/Vulkan/CUDA |
| `nvngx_dlss.dll`, `nvngx_dlssg.dll`, `nvngx_dlssd.dll` | no | no |

So:

- **Streamline-shimmed titles** (9 of the installed titles here) expose the accessors as ordinary exported functions on `sl.common.dll`. An inline hook on `NVSDK_NGX_Parameter_SetUI` yields `PerfQualityValue` and the render/output pair directly. This is the case §Hook inventory already describes.

  > ⛔ **AND IT IS BLOCKED ON LICENCE GROUNDS — measured 2026-08-09, and this
  > document recommended it for months without noticing.** Hooking
  > `NVSDK_NGX_Parameter_SetUI` needs NGX declarations: the `NVSDK_NGX_Parameter`
  > handle, the parameter name strings, the `PerfQuality` enum. The NGX/DLSS SDK
  > ships under the **NVIDIA RTX SDKs License**, which hits three of
  > `18_GPU_VENDOR_APIS` §Checklist step 2's four needles verbatim — *"defend,
  > **indemnify** and hold harmless"*, *"may not **reverse engineer**, decompile or
  > disassemble"*, *"will **terminate** automatically without notice"*. Step 3
  > therefore binds: **do not vendor it, and do not work around it by re-declaring
  > the API.**
  >
  > **The trap is that the symbol lives in a Streamline module.** `sl.common.dll`
  > is Streamline's, Streamline is MIT, and neither fact relicenses the NGX API
  > those exports implement — upstream keeps it in `external/ngx-sdk/` under the
  > RTX licence.
  >
  > So `FL_MEASURED_UPSCALER_PARAMS` has **no in-policy producer on this route**.
  > The licence-clean alternative is Streamline's own MIT surface — `slSetTag`'s
  > resource extents (`kBufferTypeScalingInputColor` / `…OutputColor` give
  > render → output directly) and the DLSS options struct reached through
  > ~~`slGetFeatureFunction`~~ — that route is refused on five grounds
  > (`docs/HANDOFF.md` §2b); the one taken reads `slSetTag`'s extents and
  > `sl::DLSSOptions` chained into `slEvaluateFeature`'s own `inputs`. That was the
  > params PR's job, and it needed `sl_dlss.h`
  > vendored alongside the nine headers already taken.
  >
  > Reversing this is an owner decision about the checklist, not a coding task.
- **NGX-direct titles** call the core, which exports only the **factories**. The accessors are function pointers *inside* the `NVSDK_NGX_Parameter` object those factories return — so there is no symbol to hook. Reaching them means hooking `NVSDK_NGX_D3D12_AllocateParameters` / `GetCapabilityParameters` and reading or wrapping the returned object's function-pointer table.

**The second path needs its own justification against CLAUDE.md rule 4 before it is built, and it survives one.** Rule 4 permits "arguments passed to APIs we hooked and COM/handle objects we legitimately own". The parameter object is a handle returned by an API we hooked, in the same sense as an `IDXGISwapChain` — reading its function-pointer table is the same operation as reading a COM vtable, which §Getting vtable addresses already does. What it is **not** is pattern-scanning for game internals, and the distinction must stay in writing.

> `nvngx_dlss.dll` itself is a dead end for this purpose and should not be hooked for it: 59 exports, none of them parameter-related. It is the feature payload, not the API surface.

> ~~**Not built, and not gated.**~~ **Gated 2026-08-09.** `tools/hookinventory-check.ps1`
> checks every symbol the Overlay resolves by name against `vendor-exports.json`,
> **module-scoped** — "does *this* module export *this* symbol", never "does
> anything export it" — and runs in `build.ps1` in both halves (`-SelfTest` and a
> live pass). It is **prevention and it fixed nothing**: no drift existed when it
> was written, and saying so matters, because a gate whose write-up implies it
> caught something cannot be audited later.
>
> Five red cases are proven and restored: a misspelt symbol, the right symbol from
> the wrong module, an emptied inventory, a stray vendor literal in an Overlay
> source (a second resolver written outside the table), and a broken oracle
> lookup. The last is the important one — every failure mode of a lookup like this
> produces the same answer as "absent", so it proves the oracle discriminates
> *before* forming any verdict.
>
> A misspelt symbol is caught **earlier still**, by the compile-time binding
> between the inventory and the stub fixtures: `static_assert(InventoryHas(...))`
> makes it a build error, so the typo never reaches the gate. Stronger, and not
> the script's credit to take.
>
> **A THIRD PASS landed 2026-08-14, and it covers the failure the other two
> structurally cannot see.** Passes A and B are source checks: they see what the
> Overlay *resolves*. Neither sees what it *links*. Taking the address of an
> `SL_API` declaration in evaluated code makes `sl.interposer.dll` a **load-time
> dependency**, and the Overlay then fails to load in every game that ships no
> Streamline — inside the loader, before `DllMain`, with no message anywhere and
> nothing in the ring to explain it. **Pass C reads the built binary's own
> dependency list** (`dumpbin /dependents`) and fails on any module matching
> `^(sl\.|_?nvngx|libxess|ffx_|amd_fidelityfx)`.
>
> Two properties worth stating because both were mistakes first. It **refuses
> rather than passes** when it cannot look — a zero-length import list, a missing
> binary under `-RequireBinaries`, or an absent `dumpbin` are all failures, and
> the list must contain `kernel32.dll` before any verdict is formed, for the same
> discrimination reason as the oracle probe above. And it runs **only** under
> `-RequireBinaries`, because without it any binary in the build tree is stale by
> definition, and a gate reporting on the wrong artefact is worse than one that
> says it did not look.
>
> **`third_party/streamline/README.md` asserted this pass existed for five days
> before it did**, which is why it is described here rather than only there.

### Which struct version carries which field (owner decision D115, 2026-11-11)

**A field of a vendor struct is read only where it is proven to be there.** A game hands a hook a struct of the
version it was built against; a member a later version added lies past the end of what an older game allocated, and
reading it is reading game memory outside the argument (rule 4) — not merely a wrong value. So every read of a tail
member is gated on the struct's own version, and the gate comes from this table, not from the one header in the tree
— which declares only the newest shape, and for `sl::DLSSOptions` says nothing about which version added what.

Established 2026-11-11 from the upstream tags, through GitHub's API: each tag's header was fetched by blob and the
struct's declarations — type, name and order — compared across tags. The members' own types were checked at the
oldest and the newest tag (`enum Boolean : char`, `enum class DLSSMode : uint32_t`, `enum class DLSSPreset :
uint32_t`, `float2`/`float3`/`float4x4`; no `#pragma pack` in either).

**Streamline** (`NVIDIA-RTX/Streamline`; tags v2.0.0, v2.1.0, v2.2.0, v2.4.0, v2.4.15, v2.7.2, v2.7.30, v2.7.32,
v2.8.0, v2.9.0, v2.10.0, v2.10.3, v2.11.1, v2.12.0, v2.14.1, v2.14.16):

| Struct | `structVersion` | First tag (blob of the header) | Members that version adds, in order |
|---|---|---|---|
| `sl::DLSSOptions` (`include/sl_dlss.h`) | 1 | v2.0.0 (`7de7411e507d`) | `mode`, `outputWidth`, `outputHeight`, `sharpness`, `preExposure`, `exposureScale`, `colorBuffersHDR`, `indicatorInvertAxisX`, `indicatorInvertAxisY`, `dlaaPreset`, `qualityPreset`, `balancedPreset`, `performancePreset`, `ultraPerformancePreset` |
| | 2 | v2.1.0 (`60882f60e6f4`) | `ultraQualityPreset`, `useAutoExposure` |
| | 3 | v2.4.0 (`27d352db364f`) | `alphaUpscalingEnabled` |
| `sl::Constants` (`include/sl_consts.h`) | 1 | v2.0.0 (`db6f37adcf66`) | every member through `motionVectorsJittered` — the matrices, `jitterOffset`, `mvecScale`, `cameraPinholeOffset`, the camera's position and axes, `cameraNear`, `cameraFar`, `cameraFOV`, `cameraAspectRatio`, `motionVectorsInvalidValue`, the seven `Boolean`s |
| | 2 | v2.4.0 (`bba15338a623`) | `minRelativeLinearDepthObjectSeparation` |
| `sl::FeatureVersion` (`include/sl.h`; `include/sl_core_types.h` from v2.7.2) | 1 | v2.0.0 (`fc2d6ee39fea`) | `versionSL`, `versionNGX` |
| `sl::FeatureRequirements` (the same headers) | 1 | v2.0.0 (`fc2d6ee39fea`) | `flags`, `maxNumCPUThreads`, `maxNumViewports`, `numRequiredTags`, `requiredTags`, `osVersionDetected`, `osVersionRequired`, `driverVersionDetected`, `driverVersionRequired`, then ten Vulkan members from `vkNumComputeQueuesRequired` to `vkFeatures13` |
| | 2 | v2.1.0 (`79037e0974d2`) | `vkNumOpticalFlowQueuesRequired` |

`sl::DLSSOptions` keeps those seventeen declarations, same types and order, through v2.14.16 (`9a875bd4f843`); v2.8.0
only re-spelled the deprecated `sharpness`. `sl::Constants` is unchanged from v2.4.0 through v2.14.16
(`15589e577df9`). Streamline 1.x is refused before any read (`SpeaksStreamline2`). **Since 0.1.0-beta.43 the
Overlay reads `sl::Constants`, and only members of version 1** — nothing of version 2 — so no version test beyond
"at least 1" is needed, and the read ends before `minRelativeLinearDepthObjectSeparation`, the member version 2
added.

**The two answers about a feature** (established 2026-11-12, the same way, over the same sixteen tags):
`sl::FeatureVersion` is two members at every tag, and `sl::FeatureRequirements` is the nineteen of v2.0.0 plus the
one v2.1.0 added, unchanged through v2.14.16 (`a255f5c5e99a`). `sl::Version` is three `uint32_t` — `major`, `minor`,
`build` — in `include/sl_version.h` from v2.0.0 (`deec7cd04363`) to v2.14.16 (`dc3041ae8b44`), and the two functions'
declarations are the same at both ends: `sl::Result slGetFeatureVersion(sl::Feature, sl::FeatureVersion&)` and
`sl::Result slGetFeatureRequirements(sl::Feature, sl::FeatureRequirements&)`. **Since 0.1.0-beta.44 the Overlay reads
both, and only members of version 1**: the read of the requirements ends at `driverVersionRequired`, before the first
Vulkan member, so no version test beyond "at least 1" is needed.

**FidelityFX** (`GPUOpen-LibrariesAndSDKs/FidelityFX-SDK`; tags v1.1.0 — FSR 3.1.0, the first ffx-api — v1.1.1,
v1.1.2, v1.1.3, v1.1.4, v2.0.0, v2.1.0, v2.2.0, v2.3.0, v2.4.0). The descriptor header carries a `type` and no size
or version, so the question is whether the declaration ever changed:

| Struct | Tags | Result |
|---|---|---|
| `ffxDispatchDescUpscale` | v1.1.0 (`68860479bb94`) … v2.4.0 (`9d4a3e5f6c60`) | **one declaration in all ten**: twenty-three members, `flags` the last |
| `ffxConfigureDescFrameGeneration` | v1.1.0 (`0ecdd4b4e59b`) … v2.4.0 (`823a3b4720f1`) | **one declaration in all ten**: thirteen members |
| `FfxApiResource`, `FfxApiResourceDescription`, `FfxApiFloatCoords2D`, `FfxApiDimensions2D`, `FfxApiRect2D` | v1.1.0 … v2.4.0 | one declaration each in all ten |
| `ffxQueryDescUpscaleGetGPUMemoryUsage` (`0x00010008`), `FfxApiEffectMemoryUsage` | from v1.1.2 | one declaration since; a game that sends the type knows the struct |
| `ffxQueryDescUpscaleGetGPUMemoryUsageV2` (`0x00010009`) | from v2.0.0 | one declaration since |

**What follows for a hook** (Advanced metrics, train B):

- `sl::DLSSOptions`: a member is read at `structVersion >=` the row that adds it — never "at least 1" for the tail.
- `sl::Constants`: everything this project may read is version 1's. The camera's position, axes and matrices are
  never read at any version (rule 4; the catalog's `PlayerState`).
- `ffxDispatchDescUpscale`, `ffxConfigureDescFrameGeneration`: every member may be read on the ffx-api route, since
  no ffx-api release has a shorter one. `ffxDispatchDescFrameGenerationPrepareV2`'s camera arrays are never read.
- **Not established, so nothing more is read there**: the FSR 3.0 host's legacy descriptors (`fsr3-v3.0.x`) beyond
  the `renderSize` the existing hook pins; any struct of a Streamline or FidelityFX release newer than the last tag
  above — a re-vendoring re-runs this comparison (`third_party/*/README.md` §Checklist).

### Ray tracing
| Hook | Yields |
|---|---|
| `ID3D12Device5::CreateStateObject` | RT pipeline creation; read `D3D12_RAYTRACING_PIPELINE_CONFIG.MaxTraceRecursionDepth`, shader counts |
| ✅ `ID3D12GraphicsCommandList4::DispatchRays` | **Per-frame proof that rays are dispatched**, plus dispatch W×H×D (→ rays-per-pixel ratio, the main PT heuristic input) |
| ✅ `ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure` | AS build/update activity — **catches inline RayQuery too**, which `DispatchRays` alone misses |
| Vulkan `vkCmdTraceRaysKHR`, `vkCmdBuildAccelerationStructuresKHR` | Same, via layer |

> **✅ Both command-list rows are BUILT as of 2026-08-20, and the trap they hit is the part worth reading** (`spike-notes.md` §6, ctests `fl_dxr_probe` / `fl_d3d12_vtable_indices` / the injected `fl_guard` case).
>
> **A COMMAND LIST'S VTABLE IS NOT THE ONE YOU READ OFF A FRESH ONE.** The first `Reset()` replaces the class vtable with a **per-object** one in which the vendor driver has taken methods over. Measured on an RTX 5080: `DispatchRays` moves from `D3D12Core.dll` into `nvwgf2umx.dll`; `BuildRaytracingAccelerationStructure` stays in `D3D12Core.dll`. Every game resets its command lists every frame, so the addresses in an **unreset** list's vtable are ones no title ever calls for the moved methods.
>
> The first version of this hook did exactly that. It installed, published `FL_HOOK_RT_DISPATCH`, and never fired — the injected fixture recorded `withDispatch = 0` beside `hooks = RT_DISPATCH | RT_AS_BUILD`, a mask bit with nothing behind it. Because the *other* hook worked, it read as a bug in the dispatch detour rather than in the acquisition they share. **The rule this generalises to: put a throwaway object through the same lifecycle the game's objects go through, or it is not a sample of them.** §H5 says the same thing about swapchains one layer up.
>
> **What is patched, and what is not.** The Overlay MinHooks the FUNCTION a slot points at, never the slot. That matters because after `Reset` each list has its own vtable array, so a slot patch would have to be repeated per list, while one inline patch covers every list — measured, and also why DIRECT and COMPUTE need only one detour per method: they resolve to the same functions. The installer still compares the two and **refuses to install if they differ**, because one trampoline cannot serve two implementations and forwarding through the wrong one calls the wrong function inside the game. Degrading to `N/A` is the safe direction. Bundles are not a further case: `D3D12_COMMAND_LIST_TYPE_BUNDLE` does not permit `DispatchRays`, which is a documented API constraint rather than something measured here.
>
> **Where the list comes from.** Created on the **game's own device** — the `ID3D12Device` `ResolveApi` already receives from `IDXGISwapChain::GetDevice`, an object we legitimately own (CLAUDE.md rule 4) — closed, reset, read, and released immediately, exactly as `InstallPresentHooks` releases its dummy swapchain. **Not a throwaway WARP device**, and that is now a correctness requirement rather than a preference: a WARP list resolves `DispatchRays` to `D3D12Core.dll` while a hardware list resolves it to the driver, so a WARP-acquired target would silently miss every call on an NVIDIA GPU.
>
> **Every vendor-specific result here is ONE DRIVER'S.** Nothing says an AMD or Intel UMD splits the two methods the same way, or leaves either in `D3D12Core`. `--probe-dxr` Q5 prints the module on each side of the `Reset` so the next machine answers for itself instead of inheriting this.
>
> Slot numbers live in `fl_d3d12_vtable.h`, one header and two consumers, and `ctest fl_d3d12_vtable_indices` proves each by **behaviour** on both list types — the §S29(b) rule applied to a second interface.
>
> **These are vtable-derived hooks, so `FL_HOOK_INVENTORY` does not cover them and neither does `tools/hookinventory-check.ps1`.** No vendor symbol is resolved by name, so Pass A has no row to check and Pass B's stray-literal sweep is silent. Anyone reading §Hook inventory as "every hook we install is in that table" needs to know the other kind exists and that `fl_d3d12_vtable_indices` is its gate.

### Pipeline / stutter attribution
| Hook | Yields |
|---|---|
| `ID3D12Device::CreateGraphicsPipelineState` / `CreateComputePipelineState`, `ID3D12Device2::CreatePipelineState` | PSO compilations per frame → correlate with frame-time spikes = **shader-compilation stutter attribution** (a Tier-1-only feature; ETW cannot attribute this) |
| `ID3D11Device::CreateVertexShader`/`CreatePixelShader` | D3D11 equivalent, coarser |

### Memory / latency
| Hook / call | Yields |
|---|---|
| ~~`IDXGIAdapter3::QueryVideoMemoryInfo` (we **call** it, not hook it, once per second from our own thread)~~ | ~~**Per-process VRAM** `CurrentUsage` + `Budget` — previously impossible; now exact~~ **Never built; superseded 2026-10-03 (D43):** the game's memory is read from OUTSIDE the game by the Agent (`18_GPU_VENDOR_APIS` §The game process's memory), so nothing in the Overlay calls it. Only the budget would need it, and that stays open (`20_OPEN_QUESTIONS` §M11) |
| NVAPI Reflex: ~~hook `NvAPI_D3D_SetSleepMode`, `NvAPI_D3D_SetLatencyMarker`~~; call `NvAPI_D3D_GetLatency` *(**never built.** The two hooks cannot be: neither function is an export — `20_OPEN_QUESTIONS` §M14. What a call-only route would take is read through in `spike-notes` §30, 2026-11-12, and waits for the owner's go or no)* | Reflex on/off + PC latency breakdown. Declarations come from the vendored MIT NVAPI headers (`18_GPU_VENDOR_APIS` §L3); the Overlay links them like any other header. NVIDIA-only — the whole block compiles out to a no-op capability flag elsewhere |

### Explicitly not hooked
Input APIs, file I/O, network, memory allocators, window messages beyond what the overlay needs, anything game-specific. Rule 4 in CLAUDE.md.

## Vulkan: layer, not hook

Vulkan gets an **implicit layer** (`FrameLedger.VkLayer`), not injection — it is the mechanism Khronos supports, it is far more robust than hooking dispatch tables, and it is how OBS and RTSS do it.

- Manifest JSON registered under `HKCU\SOFTWARE\Khronos\Vulkan\ImplicitLayers` (per-user, no admin).
- Layer name **`VK_LAYER_FRAMELEDGER_overlay`** — uppercase vendor tag, per the
  loader's `VK_LAYER_<VENDOR>_<name>` convention. This is not pedantry: a
  non-conforming name makes the Vulkan loader emit a policy warning
  (`LLP_LAYER_3`) into every Vulkan application's log on the machine. Observed
  on the dev box, where GOG Galaxy's `GalaxyOverlayVkLayer` does exactly that
  and gets named in the warning three times. `19_SAFETY` requires us to be
  plainly identifiable and well behaved; a layer that announces itself as
  malformed is the wrong kind of visible. Every conforming layer on that
  machine uses an uppercase tag — `VK_LAYER_KHRONOS_*`, `VK_LAYER_LUNARG_*`,
  `VK_LAYER_NV_*`, `VK_LAYER_VALVE_*`, `VK_LAYER_OBS_HOOK`, `VK_LAYER_RTSS`.
- Declare an API version at least as high as the applications we expect to
  layer. The same machine shows the loader warning that `VK_LAYER_OBS_HOOK` and
  `VK_LAYER_RTSS` declare 1.3 against a 1.4 application, "may cause issues".
- **`enable_environment` (`FRAMELEDGER_ENABLE_VK_LAYER=1`)**, plus
  `disable_environment` so a user can force us off — the latter is Vulkan
  convention and stays. Measured against loader 1.4.357 (`spike-notes.md` §2):
  with the variable unset the loader locates our manifest and **never maps the
  DLL**, and it compares the variable's **value**, so a stray
  `FRAMELEDGER_ENABLE_VK_LAYER=0` does not enable us. **This makes Vulkan
  Tier 1 launch-mode-only** — the Agent sets the variable when it starts an
  opted-in game, so a Vulkan title launched from Steam or GOG is not ~~hooked~~ observed by the layer *(corrected
  2026-10-04: attach mode has no Vulkan rule, so such a title gets the Overlay injected like a D3D one, and what its
  DXGI hooks record there is unverified; `kTargetIsVulkanLayered` applies to launch mode only)*.
  It is a *loading* gate, not a security gate: anything running as the user can
  set the variable.

### Never decline to load — accept, then be inert

> **Measured, and it would have been catastrophic.** Returning
> `VK_ERROR_INITIALIZATION_FAILED` from
> `vkNegotiateLoaderLayerInterfaceVersion` — the obvious way to say "this
> process did not opt in, skip me" — does **not** make loader 1.4.357 skip the
> layer. It **access-violates the application** (`spike-notes.md` §2,
> reproduced every time). Every Vulkan application on the machine outside our
> enable-list would have crashed: a far larger blast radius than the one we
> were reducing, inflicted on programs with nothing to do with FrameLedger.

So the layer **always accepts negotiation and always forwards**. Returning
`nullptr` from `vkGetInstanceProcAddr` as a way to opt out is the same class of
failure and is equally forbidden — handing the loader a null `vkCreateInstance`
breaks the application just as thoroughly.

The enable-list decides **what we intercept**, never **whether we load**. Being
present and inert costs a pointer forward per call; being absent by erroring out
is not something this loader supports.

> **We will not be the only layer, and probably not the only present hook.** A
> representative dev machine carries six machine-wide implicit layers already:
> Steam Overlay, Steam Fossilize, EOS Overlay, GOG Galaxy Overlay, OBS, and
> RTSS. RTSS and the Steam overlay also hook D3D presentation in-process. Design
> for coexistence, not for an empty process — see `20_OPEN_QUESTIONS` §H5 and
> §H7, both of which are testable on such a machine today.
>
> Note also that all six of those register under **HKLM** and therefore needed
> admin. Registering under HKCU (below) is the less common choice and the better
> one: no elevation, and per-user scope.
- Intercepts `vkQueuePresentKHR`, `vkCreateSwapchainKHR`, `vkCmdTraceRaysKHR`, `vkCmdBuildAccelerationStructuresKHR`, `vkCreateGraphicsPipelines`.

  > **Built 2026-09-06 (P1 item 3): the first two, plus `vkDestroySwapchainKHR`; the three feature entries are still ⏳.** How the capture side works, stated once:
  > - **The ring is the same ring.** `fl_shm_host.h` (header-only, in `FrameLedger.Shm`) now holds the user-only DACL, the `Local\FrameLedger.Ring.<pid>` name, the mapping and the handshake for **both** capture sides; the Overlay calls it too. **One ring per process: whichever side creates the mapping first owns it, the other sees `ERROR_ALREADY_EXISTS` and stays inert** — and stays *forwarding*: the layer registers every device's next pointers before it tries the ring, because a hook with nothing to forward to failed the application's present (measured, `VK_ERROR_DEVICE_LOST` on the harness, the first time the Overlay got there first). That is also why the launch-mode guard **does not inject a presenter that mapped `vulkan-1.dll`** (`Reason::kTargetIsVulkanLayered`, `fl_guard.cpp`) — with or without a D3D runtime beside it, because **an NVIDIA Vulkan ICD maps `dxgi.dll` + `d3d12.dll` itself and presents the Vulkan swapchain through a DXGI flip chain** (measured on the harness's `--vulkan` mode: classified as D3D, the injected Overlay recorded 295 DXGI presents of a Vulkan swapchain and the layer forwarded into nothing). The layer is the capture side for anything that maps the Vulkan loader; a D3D title that happens to is the rarer shape and the one this rule gets wrong.
  > - **It comes up at the first admitted `vkCreateDevice`**, never at load and never at `vkCreateInstance`, so a process either gate refused never has a mapping with its pid on it. Init is not a hook path; the present hook is, and it follows the Overlay's rules — SEH-guarded, allocation-free, lock-free, three faults → `SELF_DISABLED`.
  > - **§S2 part three, the in-layer supervision, runs on the present path**, because the layer may not own a thread (the loader owns its mapping). `guardTicks` not advancing for `FL_GUARD_TICK_DEADLINE_MS` → `FL_STATUS_UNHOOKED`; `unhookRequested` → the same within one present; `pauseRequested` → no record, still forwarded. "Stops" means **passthrough forever with the reason on the mapping** — a layer cannot leave a running game's loader chain. A title that stops presenting is not stopped by the deadline, and has nothing to be observed either. Proven with a test-only flavour of the DLL whose deadline is 1.5 s (`fl_vklayer_shortdeadline`, never shipped; ctest `fl_vklayer_supervision`).
  > - **No registry for the operator host.** Loader 1.4.357 honours `VK_ADD_IMPLICIT_LAYER_PATH` (measured 2026-09-06, `spike-notes` §2): the CaptureHost's `launch` writes a manifest under `%LOCALAPPDATA%\FrameLedger\vklayer`, points the launched process at it, sets the enable variable and adds the image name to the enable-list for the session — discoverable by that process alone, gone when it ends. The Agent's HKCU registration (`12_BUILD`) is unchanged and remains P2's.
- The layer respects the same guard, in two steps at init: it checks the enable-list, and it scans **its own process** against the anti-cheat blocklist. Either one saying no means fully passthrough. **A layer is machine-wide by nature — these checks are mandatory, not optional.**
- The self-scan uses the **same matcher and the same rules file as the injection guard** (`fl_ac_rules.h`, compiled into both targets). Not a copy: a layer with its own blocklist would be a second matcher that can disagree with the first. Every uncertainty — rules unreadable, malformed, enumeration failed, a truncated list — resolves to passthrough, which is the opposite polarity from the injection guard and the same principle: leave the host alone.
- Registered only while at least one Vulkan game has hooking enabled; unregistered on uninstall (Velopack hook) and when the last such game is disabled. **Never at install time** — `12_BUILD` §The Vulkan layer is not registered at install time.

  > **Built 2026-09-14 (P3 PR-8b), as the repair tools and no more.** `Infrastructure.Vulkan.VkLayerRegistration`
  > writes the one DWORD under `HKCU\SOFTWARE\Khronos\Vulkan\ImplicitLayers` named by the manifest's path
  > (`%LOCALAPPDATA%\FrameLedger\vklayer\VkLayer_FRAMELEDGER_overlay.json`, written by the same code the launch
  > path uses); the Agent's `--register-vklayer` refuses unless a game has hooking enabled, `--unregister-vklayer`
  > also removes a stale value naming our manifest elsewhere, and `HelloAck.vulkanLayerRegistered` reads the key
  > at the Agent's start. Two things this build does **not** do, stated rather than implied: ~~(1) the automatic
  > register-on-first-enable / unregister-on-last-disable needs to know which enabled game is a *Vulkan* title,
  > and the ledger does not hold that fact until P4's capability flags — so the check is "any enabled game"~~
  > **— closed 2026-09-14 (P4 PR-2).** The fact is static and lives in `capability_flags` under the id `vulkan`:
  > `GameFileProbe` reads the executable's import and delay-load tables (`Infrastructure.Detection.PeImports`, bounded,
  > read-only) and looks for `vulkan-1.dll` as an ASCII or UTF-16 string in the bounded scan (a `LoadLibraryW`
  > loader — the harness's shape and many titles'); null when the file could not be read, never "no". A static
  > fact was chosen over the runtime one on purpose: the NVIDIA ICD maps `dxgi.dll` + `d3d12.dll` itself (the
  > 2026-09-06 note above), so runtime module presence is the weaker discriminator, and the automation has to act
  > at consent time, before the title ever runs. `Application.Vulkan.VkLayerReconciler` computes the desired
  > registration from the ledger — hook-enabled games whose flags carry `vulkan` — and moves the HKCU value to it,
  > called after every consent change the pipe handler and the console verbs make and after every detection
  > sweep (so a guard block, the crash policy's auto-disable, and a Vulkan fact that arrives after the consent all
  > converge within one 15 s interval). `--register-vklayer` runs the same reconciler and refuses when it finds no
  > such game; the Settings button follows the same rule (`HookedGameViewModel.UsesVulkan`). Not staged = nothing
  > to do — and **an Agent off the profile ledger (`--data-dir`) gets an inert registrar**, so the e2e suites'
  > consent for `hook-harness` (which `LoadLibraryW`s the loader and therefore reads as Vulkan) never reaches the
  > developer's HKCU (D17's shape). `VkLayerReconcilerTests`, `PeImportsTests` (a synthetic PE32+, both directories, the wide
  > literal, a malformed file). (2)
  > a registration buys nothing today: the layer gates on `FRAMELEDGER_ENABLE_VK_LAYER=1`, which only a launch
  > sets, and a launch already reaches the layer through `VK_ADD_IMPLICIT_LAYER_PATH`. Registration becomes
  > useful the day background capture sets the variable for a tracked Vulkan title started elsewhere, and not
  > before; until then the Settings button and the flag reflect and repair a state nothing needs.

### The enable-list

Referenced everywhere, specified nowhere until now (`20_OPEN_QUESTIONS` §S4).
It is read inside a process we do not own, by code that runs before we know
anything, so its failure modes matter more than its format.

| | |
|---|---|
| **Location** | `%LOCALAPPDATA%\FrameLedger\vklayer\enabled.txt` — per-user, no admin, same trust boundary as the rules copy |
| **Format** | UTF-8, LF, one lowercased process image name per line (`witchfire.exe`), `#` comments, blank lines ignored |
| **Bounds** | ≤ 64 KiB and ≤ 1024 entries. A file larger than that is treated as corrupt |
| **Matching** | Exact, case-insensitive, on the image name only — never a path, never a prefix, never a substring |
| **Sole writer** | The Agent. The layer only ever reads it |
| **ACL** | Inherited from `%LOCALAPPDATA%`: the current user's SID. **The Agent must not widen it** |

**Every failure is passthrough.** File missing, unreadable, oversized, malformed,
or the current process simply absent from it ⇒ the layer does nothing and
forwards. That is the opposite direction from the injection guard, and
deliberately so: here "do nothing" *is* the safe outcome, because the risk being
managed is our code running somewhere it was not invited.

> **The ACL is the whole mechanism, and it is not a strong one.** Anything
> running as the user can add a line to this file, exactly as anything running
> as the user could once redirect the rules path (§S3). The difference is what
> it buys an attacker: a line here causes FrameLedger to *observe* a Vulkan
> process it would otherwise ignore. It grants no injection — the Vulkan path
> has no injection — and it cannot disable the blocklist scan the layer runs on
> itself. Treat it as reducing blast radius, not as authorisation.

## Ring writer (hot path)

```cpp
struct alignas(64) FlFrameRecord {   // 64 bytes exactly, static_assert'd
    uint64_t qpc;                    // @0  present entry timestamp
    uint32_t frameIndex;             // @8
    uint32_t presentFlags;           // @12
    uint16_t syncInterval;           // @16
    uint16_t renderW, renderH;       // @18 0 = unknown
    uint16_t outputW, outputH;       // @22
    uint8_t  api;                    // @26 d3d11|d3d12|vulkan|opengl
    uint8_t  upscaler;               // @27 0 = NOT_REPORTED; dlss|fsr2..4|xess|nis|none(8)|unknown(0xFF)
    uint8_t  upscalerQuality;        // @28 vendor enum (FSR: 0x10 + mode, beta.25); 0xFF = a hook ran and could not tell
    uint8_t  fgMode;                 // @29 0 = NOT_REPORTED; dlss_g|fsr_fg|xefg|none(4)|unknown(0xFF)
    uint8_t  rtFlags;                // @30 bit0 asBuildOBSERVED, bit1 dispatchOBSERVED, bit2 psoCreatedEver
    uint8_t  colorSpace;             // @31 0 = NOT_REPORTED, 1 SDR, 2 HDR10, 3 scRGB
    uint32_t dispatchRaysVolume;     // @32 Σ (W×H×D) over DispatchRays calls this frame
    uint16_t psoCreatedThisFrame;    // @36
    uint8_t  maxTraceRecursionDepth; // @38 from the live RT PSO config, 0 = none
    uint8_t  featureFlags;           // @39 FlFeatureFlags: facts + their OBSERVED companions
    uint16_t measuredMask;           // @40 FlMeasured, 16-bit since v3
    uint8_t  upscalerSharpness;      // @42 percent; 0xFF = the API reports none
    uint8_t  fgEvaluations;          // @43 FG feature evaluations observed this frame
    uint32_t vramUsedMb;             // @44 MiB, matching vramBudgetMb
    uint32_t reflexLatencyUs;        // @48 0 = unavailable
    uint8_t  dxgiUnseen;             // @52 DXGI-counted presents the hook did not see (2026-09-05; this listing
                                     //     showed a uint32 reserved here until 2026-10-04)
    uint8_t  reserved[3];            // @53..55 must be zero
    uint32_t seq;                    // @56 seqlock counter (see 07_IPC §Protocol rules)
    uint32_t swapchainId;            // @60 which swapchain this present came through; 0 = unidentified
};
static_assert(sizeof(FlFrameRecord) == 64);
```

> **Layout version 3, 2026-08-05 — and the reason for the whole revision is the
> zero value.** `FlFrameRecord rec{}` zero-initialises, so whatever 0 means is
> what a writer publishes when it FORGETS. In v2, 0 meant `FL_UPSCALER_NONE`,
> `FL_FG_NONE` and an `rtFlags` with no evidence bits — three measured negatives
> about a title nobody had examined. `measuredMask` made that safe by CONVENTION;
> v3 makes it safe by CONSTRUCTION. Every enum's 0 is now `NOT_REPORTED`, and
> `rtFlags`' polarity is flipped so its bits mean *observed*.
>
> Four answers items 4/6/7 owe had no home and now do: DLSS-SR **and** Ray
> Reconstruction concurrently (`featureFlags`, since RR was a mutually exclusive
> `upscaler` value), `upscalerSharpness`, and — in `FlWriterState`, because they
> are session facts and not per-frame — the device **RT tier** without which
> `03_METRICS`' definite RT `No` had no producer at all, plus `rtStateObjectsCreated`
> and `rasterPsoCreated`.
>
> Paid for by narrowing `vramUsedBytes` (u64 bytes → u32 MiB, matching the
> `vramBudgetMb` it is compared against and the `vram_mb` every consumer exports)
> and `fgEvaluations` (u32 → u8; ×4 multi-frame generation is 3). `seq` @56 and
> `swapchainId` @60 did not move, so `fl_ring.h`'s two pins and the seqlock's
> payload spans are untouched.

Field notes — each of these was a defect in an earlier revision:

- **`dispatchRaysVolume` is `uint32`, and it is a volume, not a call count.** A
  single 3840×2160 primary-ray dispatch is 8,294,400 rays — 126× a `uint16`'s
  range, so a counter of that width saturates on every RT title at 1080p or
  above. That is precisely the regime `03_METRICS` §RT reads: its path-tracing
  heuristic keys off "rays dispatched per output pixel ≥ ~1.0", which is
  uncomputable from a pinned counter. `rays_per_pixel` divides this by output
  pixels.
- **`maxTraceRecursionDepth`** is the second of the four stated inputs to
  `pt_confidence`. It ~~is~~ would be read at `CreateStateObject` (§Hook inventory) but
  previously had no transport to the Agent and no column to land in. *(Corrected 2026-10-04: `CreateStateObject` is
  not hooked, so nothing writes it yet.)*
- **`_pad0` and `_pad1` were explicit holes and are now carrying data.** Both were
  named only so every offset could be asserted; spending them costs nothing,
  because both bytes ranges were already inside the record and already written
  every frame. Neither needs a `FL_SHM_LAYOUT_VERSION` bump — and both had to be
  decided BEFORE a writer or a C# mirror exists, because after that the same
  change is user-visible: the Agent refuses to attach and tells the user to
  restart the game.
  - **`measuredMask` (@39)** distinguishes *"we looked and there was none"* from
    *"we did not look"*. The zero-defaults are affirmative negatives —
    `FL_UPSCALER_NONE`, `FL_FG_NONE`, `rtFlags = 0` — so a present-only writer
    with no feature hooks would otherwise assert "no upscaler, no FG, no ray
    tracing" as measured fact, 118 times a second, on the exact title chosen to
    prove ADR-7. `03_METRICS` would then produce `fg_factor 1.0`, the single
    inflated number CLAUDE.md rule 6 forbids, and map a whole session to a
    definite RT `No`, which rule 7 forbids.
  - **`swapchainId` (@60)** says which swapchain the present came through.
    Patching a vtable slot patches the SHARED `dxgi.dll` class vtable — measured
    across five configurations, D3D11 and D3D12, WARP and hardware, composition
    and HWND, all identical — so one hook sees every swapchain in the process. A
    title with a separate UI or video swapchain inflates `F_disp`, and
    `03_METRICS` has no way to tell the streams apart without this.
- **There is no `gpuBusyUs`.** Its only possible source is GPU timestamp queries
  injected into the game's command lists, which `15_ROADMAP` defers to v2 as
  "more invasive than anything in v1". A version-locked 64-byte record must not
  reserve space for a field v1 cannot fill; the bytes now carry
  `fgEvaluations`, which v1 genuinely produces.
- **`fgEvaluations`** is what makes Native-vs-Displayed computable at Tier 1 —
  see `03_METRICS` §Frame Generation.
- **`api` no longer lists `d3d9`.** D3D9 titles are almost entirely 32-bit and
  the Overlay is x64-only; they are Tier 2 in v1 — i.e. **unmeasured** (`20_OPEN_QUESTIONS` §Scope).

- **SPSC lock-free ring**, capacity a power of two (default 8192 records = 512 KB), overwrite-oldest.
- Writer/reader seqlock protocol, including the rule that the payload write must
  **exclude** the `seq` field and that `seq` is never reset: `07_IPC` §Protocol rules.
- Header: ~~three~~ **four** separate 64-byte lines before the ring — and, since layout 5 (2026-11-11), region 5's twenty-three, which the watchdog alone writes and only while the Agent asks (`07_IPC` §Region 5) — *(corrected 2026-10-04: since layout 4, 2026-09-27,
  beta.10, `FlDisplayState` at 0xC0 — Overlay-written at most every 500 ms; the ring starts at 0x100)* — `FlShmHandshake` (write-once),
  `FlWriterState` (Overlay-written: `writeIndex`, `status`, `apiMask`, `faultCount`),
  and `FlControlBlock` (**Agent**-written: `pauseRequested`, `unhookRequested`,
  `overlayEnabled`, `guardTicks`). The control flags are *not* in the Overlay's
  header — they are written by the other process, and mixing them into an
  Overlay-written line reintroduces exactly the false sharing the split exists to
  prevent. Layout is normative in `07_IPC` §A + B.
- **The hot path performs: one QPC read, a few field reads from cached state, one 60-byte store, two relaxed atomic stores and two compiler fences.** No syscall, no allocation, no lock, no logging. Target ≤ 1 µs per present. *(Added 2026-10-04: the summary leaves out the per-present `IDXGISwapChain::GetLastPresentCount` call (§H5, the `dxgiUnseen` count) and the display sample every 500 ms at most (region 4).)*
- Per-frame mutable state (current upscaler, render res, dispatch counts) lives in a small struct updated by the feature hooks and *read* by the present hook; counters reset at present.

## The watchdog thread — the only thread we add to the game

One thread, started on the init thread *after* the hooks are installed, sleeping
`1000 ms` at a time. It evaluates `unhookRequested` and the `guardTicks` deadline,
calls `StopObserving` when either fires, and **exits** once stopped.

> **Corrected 2026-10-04 — this describes the thread as it was first written.** Since the lazy installers landed
> (`slEvaluateFeature`, 2026-08-09) the loop also installs feature hooks, runs the runtime census, publishes counters,
> appends the native log, and stops on the loader detour's early-stop family; it waits on that detour's wake event with
> a 1000 ms timeout rather than sleeping. `legal/DISCLAIMER.md` §2 says so to users since beta.12 (Disclaimer 2.10).

**Why it exists.** Both stops used to be evaluated only on the present path, and
`MayObserve` is reachable only from `RecordPresent`, which is reachable only from
the two present hooks. In a process that had stopped presenting — hung,
alt-tabbed, sitting in a menu — neither check ever ran and the hooks stayed
patched in for the life of the process. `fl_shm.h` says over
`FL_GUARD_TICK_DEADLINE_MS`, in capitals, that the deadline must **not** be driven
by the present hook, *"because the clock would stop when presents stop, which is
the exact scenario this exists for"*. The code did what its own normative comment
forbade. Measured: `unhookRequested` set on a `hook-harness --hold` target left
`status` at `READY` indefinitely.

**It supplements the present path, it does not replace it.** `07_IPC` requires the
safety stop within one frame, and a one-second watchdog cannot promise that, so
the present path keeps its `unhookRequested` check. Same shape as §S6's
`LoadLibrary` hook supplementing the 30 s poll.

**Why a thread here, when `20_OPEN_QUESTIONS` §S2 rejected one for the Vulkan
layer.** All three of §S2's reasons are properties of the *layer*: the Vulkan
loader owns the layer's mapping, so a thread outliving it access-violates a host
we do not own; the re-scan it would have run allocates ~1.15 MB transiently; and
it would have called `NtQuerySystemInformation` and probed the SCM from inside a
game, which is the behavioural signature of anti-analysis code (CLAUDE.md rule 3).
None applies to the Overlay, which is loaded by documented `LoadLibraryW` and is
never `FreeLibrary`'d from a live process. ~~**This thread enumerates nothing,
probes nothing and allocates nothing** — it reads two `uint32`s from our own
mapping and sleeps.~~ *(Corrected 2026-10-04: it enumerates no process, module list or service — the rule-3 line, which
holds — but it asks the loader for fixed module names, allocates through MinHook and, for ray tracing, creates one
command allocator and list on the game's own D3D12 device, and writes the log file. The justification below is
restated for that scope: what it must never start doing is scanning.)* That distinction is the whole justification and must survive
any future change: a watchdog that starts scanning is a different object under
rule 3.

Failing to create it is **not** fatal — the present-path checks still work, and an
Overlay that reacts only while presenting is strictly better than none. The
failure is recorded in `faultCount` so the Agent can see it rather than infer it.

`legal/DISCLAIMER.md` §2 discloses this thread to the user in plain language,
because "what runs inside the game" is exactly what that document exists to state.

## Fault policy

Every hook body is wrapped:

```cpp
#define FL_HOOK_GUARD(body, fallback)                    \
    __try { body }                                       \
    __except (FlFilter(GetExceptionCode())) { fallback }
```

- Any SEH exception inside our code ⇒ increment `faultCount`, record which hook, **return control to the original function** so the game continues.
- 3 faults total ⇒ set `status = self_disabled`, ~~uninstall all hooks (`MH_DisableHook(MH_ALL_HOOKS)`)~~ restore each patch's prologue only while its bytes are still our jump (compare-and-restore per patch through `StopObserving`, §Unhooking, §H7 — `MH_ALL_HOOKS` would have removed an overlay chained after us; corrected 2026-09-27; since 0.1.0-beta.46 the patches that are still ours come out together, under one stop of the threads — §Unhooking, Batches), stop writing, stay dormant. The Agent sees the flag and finalizes the session as `degraded`.
- Never `__try` around the call to the original function — only around *our* code, so we never mask a game bug as ours or vice versa.
- The trampoline call to the original is always executed exactly once, on every path, including error paths.

## Unhooking

> **Built 2026-09-06 (P1 item 4) as compare-and-restore PER INLINE PATCH.** The Overlay swaps no vtable
> entries — every hook it installs is a MinHook inline patch on a function's first bytes — so the sentence
> below applies in its inline form: `StopObserving` walks a registry of every patch this DLL made (target,
> detour, name; filled by `PatchTarget` and the two direct sites) and calls `MH_DisableHook` on a target
> **only while the bytes there are still the jump MinHook wrote for us** — `fl_patch_check.h`
> `StillOurs`: `E9 rel32` → a `FF 25 00000000 <addr>` relay whose address is our detour, or the
> hot-patch `EB F9` form of the same. `MH_DisableHook(MH_ALL_HOOKS)`, which this replaced, wrote every
> saved prologue back without looking, and an overlay that patched the same function after us chains
> through our jump — that write would have removed their hook silently (§H7). A patch that is no longer
> ours is left in place, logged `UNHOOK_DECLINED`, and our detour stays in their chain forwarding (every
> body consults `g_observing`). Proven both directions against a real MinHook patch and a simulated
> foreign one in ctest `fl_unhook_inline` (the vtable form stays in `fl_unhook_preserves_foreign`), and
> against the injected Overlay in ctest `fl_guard` `[log]`, where every patch reads `UNHOOK_RESTORED`
> because the harness is alone in its process.
>
> **The registry refuses what it cannot hold, and a patch is registered before it can run (0.1.0-beta.42,
> `fl_patch_registry.h`).** The registry was an array and a counter, and two things were wrong with it — found by
> reading the code for the plan of the next two hooks, not by a failure. **Full meant silent:** `RegisterPatch`
> wrote its entry only while it fitted, after `PatchTarget` had already enabled the hook, so a 49th patch would
> have been live and unknown to the stop, with no line anywhere. (Twenty patch sites exist; the rule no longer
> depends on that count being right, and a `static_assert` now checks the capacity against the inventory's own row
> count plus the seven sites that are not rows.) **Enabled before registered:** `PatchTarget` enabled the hook,
> re-read `g_observing`, and registered last; a stop between the re-read and the registration walked a table
> that did not hold the patch yet, and the patch stayed in after "stopped". The DXGI three and the loader detour
> registered after enabling too, with no re-read at all.
>
> One sequence now, `fl::patch::Install`: **take a slot — no slot, no patch, nothing created — create, enable,
> commit the slot, then re-read the flag.** The stop clears the flag and then walks the committed slots. Each side
> writes and then reads what the other wrote, sequentially consistent, so at least one sees the other: the walk
> restores the patch, or the installer finds the flag clear and takes its own patch out (and retires the slot,
> so the walk does not report it as somebody else's). A slot whose patch failed is free again; one that was ever
> live is never reused — the walk reads it without a lock. Nothing is created once observing is off. A patch
> that did not go in is logged once per target and reason, `PATCH_FAILED a=<reason>` (`2` stopped, `3` registry
> full, `4` create failed, `5` enable failed, `6` the stop arrived meanwhile); until now every such failure was
> silent. The three DXGI patches are still enabled together and keep the same rules by hand; when any step of
> theirs fails, whatever a partial enable left in is taken out — it used to stay, unregistered, with init
> reporting failure. And two stores that could overwrite a stop's reason no longer do: `READY` is written only
> over `INIT`, and init's failure path stores nothing. No hook body changed. ctest `fl_unhook_inline` drives
> the shipped sequence against real MinHook patches: a full registry leaves the target's bytes untouched and
> MinHook unasked; a stop arranged between the enable and the re-read ends with the function unpatched; a failed
> patch gives its slot back; a slot is invisible to the walk until committed.
>
> **Batches, 0.1.0-beta.46 (owner decision D134): one stop of the game's threads for a group of patches.** MinHook
> writes a jump with every other thread of the process stopped — a snapshot of the system's threads, then a
> suspend, a context read and a resume for each of the process's — and `MH_EnableHook` / `MH_DisableHook` for one
> target do all of that for one jump. The owner's logs of 2026-11-13 put a number on it: the thirteen Streamline
> patches took 0.74 s to 2.8 s of a game's start, 57 to 215 ms each, and a stop restored its patches the same way.
> MinHook 1.3.4 can queue — `MH_QueueEnableHook` / `MH_QueueDisableHook` mark a hook, `MH_ApplyQueued` executes
> every mark under one stop — and `fl_patch_registry.h` now has the two sequences that use it:
> `fl::patch::Batch` (`Add`: slot and create, nothing marked; `Commit`: enable them all) and
> `fl::patch::RestoreAll`, which is the stop's walk. `InstallUpscalerHooks` queues every row a tick finds
> something for and commits once, before the families that are published "when whole"; `InstallRtHooks` batches
> its two; `StopObserving` calls `RestoreAll`. A patch's `HOOK_INSTALLED` line, family bit and latch still come in
> that order and only once it is in. The loader detour, OpenGL and the three DXGI patches of init are as they were.
>
> **What the queue makes dangerous, and what the code therefore never does** (the plan was reviewed against
> `hook.c` before a line was written, and its first form was refused). `MH_ApplyQueued` applies **every** hook
> whose mark differs from its state — the process has one table, not one per caller. So (1) **no mark is ever left
> standing**: a hook that failed to enable has its mark removed, or the next apply — the stop's own — would enable
> it; (2) **only a patch that is still ours is ever marked for a restore** (`StillOurs`, as before; §H7), and
> nothing is marked "all" — the refused draft marked everything and then un-marked what it must not restore, and
> un-marking a hook that is already out marks it for *enabling*: the stop would have put a patch back in after the
> stop; (3) **mark, apply and commit happen inside a gate** (`MinHookEngine::BeginQueue`, a slim lock taken
> exclusively) so the watchdog's apply cannot execute the stop's marks nor the stop's the watchdog's. The gate is
> held for one apply and never across a symbol lookup, which takes the loader's lock. It is the one lock in the
> Overlay, and it is on no hook body's path: the only way from a hook to it is the stop itself, which already
> waited for MinHook's own lock and a stop of the threads for every patch. Its owner re-enters it without
> waiting. **The verdict on an install is the engine's, not the bytes'**: a foreign overlay patching over us a
> moment after the apply would read as "not in", and treating that as a failure would mark a restore nobody had
> compared. `MH_ApplyQueued` stops at the first hook that fails and says only that one did; the ones after it
> are then enabled — or, in the stop, restored — one at a time, so one bad patch still costs one patch.
> The write-then-read pair is unchanged: the stop clears the flag and then walks; `Commit` commits its slots and
> then re-reads the flag, and takes out — compared first — what it put in when the flag is clear. A stop that
> lands before the commit enables nothing.
>
> **Measured** on `hook-harness`'s Streamline target, the development PC, three runs before and after (`fl_guard`
> `[advanced]` prints them): the nine Advanced-only patches went in one by one over 0.60 s / 0.70 s / 0.60 s
> from the first log line to the ninth, 76–88 ms each; as one batch they take **81 / 90 / 78 ms**. The stop restored
> eighteen patches in 1.37 s / 1.56 s / 1.37 s; under one apply, **77 / 90 / 76 ms** — and from the Agent's request
> to `UNHOOKED`, 1.27–1.48 s became 94–110 ms. One stop of the threads costs what it cost for one patch, whether it
> covers four, nine or eighteen. A new log line says so in the field: `PATCH_BATCH install` / `restore`, `a` the
> microseconds, `b` the patches. ~~**Not measured:** a real game — how long one stop of the threads is with a few
> hundred of them, and whether any title minds patches arriving together (owner-only item 44).~~ **In real games**
> (the owner's five sessions of 2026-11-13 on 0.1.0-beta.46 — Hogwarts Legacy, Phantom Blade Zero, Black Myth:
> Wukong, Cyberpunk 2077, Clair Obscur: Expedition 33; eighteen install batches of two to sixteen patches): every
> batch went in whole, with the hooks beta.45 installed and no `PATCH_FAILED`; **sixteen batches cost 81–262 ms
> whatever they held**, which is what one patch cost in the same game before. Streamline's thirteen, first line to
> last on beta.45 against the one batch now: Phantom Blade Zero 0.74–0.85 s → 88 ms, Cyberpunk 2077 1.18 s → 93 ms
> (with three more patches in the batch), Black Myth: Wukong 1.65 s → 188 ms (twelve), Hogwarts Legacy 2.80 s →
> **1.26 s**. That last one and a batch of three in Cyberpunk 2077 (**0.61 s**) are not explained: the cost does
> not follow the count — sixteen patches in 93 ms, three in 609 ms — and **the log cannot tell one long stop from an
> apply that failed part-way and was finished one patch at a time**, because a batch writes a line only for a patch
> that fails for good. **Since the release after 0.1.0-beta.46 the line says which** (owner decision D137): `install` or `restore` is a batch that went under its one apply; `install singly` or `restore singly` is one whose apply said it had failed, and the high half of `b` then counts the patches handed to MinHook one at a time (`b=0x0003000d`: thirteen in, three of them singly) — attempts, not stops: MinHook makes no stop for a patch the failed apply had already done. So a long `a=` on a plain `install` line is one long stop of the threads. **Still not measured: the restore in a real game** — no session has been stopped while its
> game ran (owner-only item 44 (d)). One more thing the
> queue changes for the better: MinHook's single-target disable does not move a thread that is suspended inside
> the patched bytes (`hook.c` passes the enable action to its thread fix-up in both directions); the queued apply
> does. ctest `fl_unhook_inline` drives `Batch` and `RestoreAll` against real MinHook (three in under one apply,
> a duplicate target refused without holding the others up, a foreign patch left byte for byte) and against a
> model of MinHook's table for what real MinHook cannot be made to do on demand: an apply that fails halfway or
> not at all, a stop inside the apply, a committed patch that is already out — the refused draft's failure — and
> that no mark is ever standing or made outside the gate.

~~`FlRequestUnhook()` (or the control flag) ⇒ `MH_DisableHook(MH_ALL_HOOKS)`, restore vtable entries **only where they are still ours**, flush the ring, set status.~~ *(Corrected 2026-10-04: both reach `StopObserving` — compare-and-restore per patch as the note above says, then the status; there is no `MH_ALL_HOOKS`, no vtable entry and no ring flush. The watchdog flushes the native log on its way out.)* **The DLL is not `FreeLibrary`'d from the live process** — a thread could still be inside a trampoline. It goes dormant and unloads with the process. This is deliberate and documented.

### Compare-and-restore, never unconditional restore

This section used to call vtable swapping a "cleaner uninstall" than inline
patching. **In the multi-overlay case that is backwards** (`20_OPEN_QUESTIONS`
§H7), and the multi-overlay case is the normal state of a gamer's machine — the
dev box alone has RTSS, OBS, Steam Overlay, Steam Fossilize, EOS and GOG Galaxy
resident (`spike-notes.md` §Environment).

If another overlay hooked the same slot *after* us, it saved **our detour** as
its original and chains through it. Writing the pristine address back then
removes their hook silently: their overlay stops working, with no error
anywhere, and we caused it.

So the rule is **compare-and-restore**:

```
if (slot != our_detour) -> leave it alone, go dormant
else                    -> restore the original
```

Re-check the comparison *under* the `VirtualProtect` write window, not only
before it. And when the slot has changed, going dormant costs nothing: we stay
loaded anyway, and our detour remains in someone else's chain doing nothing
harmful.

> **Said plainly, 2026-11-13: for the inline patches that re-check has never been made.** MinHook owns the write
> window — it stops the threads, changes the protection and writes — and `StillOurs` is asked before it is called,
> not inside. Between the two a foreign overlay could patch over us, on a thread MinHook has not stopped yet. The
> batches of 0.1.0-beta.46 neither close that window nor widen it: the comparison and the write are apart by one
> stop of the threads, as they were for each patch, once instead of once a patch.

Verified by `hook-harness --probe-unhook` (ctest `fl_unhook_preserves_foreign`),
which simulates the foreign hooker rather than depending on RTSS being
installed, so it is deterministic and runs on CI. Both halves are asserted: we
decline when the slot changed, **and** we do restore when it did not — a
compare-and-restore that never restores is not a fix.

## Native logging

No logging in hook bodies. A small fixed-size in-memory ring of structured events (hook installed, symbol missing, fault, unhook) is flushed to `logs\overlay-<pid>-*.log` by the init thread at session end, on unhook, or on Agent request — never mid-frame.

> **Built 2026-09-06 (P1 item 4), as specified, with the details a bug report needs.** A ring of 256 ×
> ~~64-byte events (`qpc`, code, two operands, a 40-char name)~~ 96-byte events (`qpc`, code, two operands, a
> 72-char name — 64 bytes and 40 characters until beta.42) appended with one `fetch_add` from init and
> watchdog paths and from a hook's **SEH handler** — `NoteFault` records the exception code and the hook's
> `__func__` — never from a hook body. Codes: `RING_CREATED`, `PRESENT_HOOKS`, `HOOK_INSTALLED` (one per
> patch, with the target address), `SYMBOL_MISSING` (once per pair, and only when the module IS mapped),
> `FAULT`, `STOP` (the reason), `UNHOOK_RESTORED` / `UNHOOK_DECLINED` (one per patch), `LOADER_DETOUR`,
> `SUPERVISION_LOST`, since beta.24 `STOP_FLUSHED` (the last line after a stop: `a=1` when the stop had finished
> writing), and since beta.42 `PATCH_FAILED` (a patch that did not go in, once per target and reason — §Unhooking)
> and `FFX_DISPATCH`, `FFX_QUERY`, `UPSCALE_ROUTE`, `RUNTIME_CENSUS` (the note at the end of this section), and
> since beta.43 `FFX_CONFIGURE` — each configure type a hooked ffx module was handed, in a process where
> `ffxConfigure` is hooked at all (one asked for Advanced metrics) — and since beta.46 `PATCH_BATCH`: one line an
> apply, `install` or `restore`, `a` the microseconds it took and `b` the patches it covered (§Unhooking, Batches).
> **Three flush points — four since beta.42, the same note —, none on a present path:** the end of `InitThread` (so the
> file exists from the start of the session and a crash mid-session still leaves it), the watchdog when
> `FlControlBlock.logFlushRequested` changes (a counter the Agent bumps at session end; nothing clears it),
> and the watchdog on its way out after any stop. Appended, never recreated; events overwritten before a
> flush are counted in a `#` line.
>
> **The stop and the last flush, beta.24 (`fl_guard`'s native-log case, red under load since beta.9).** A stop sets
> `g_observing` to 0 first and writes its `STOP` / `UNHOOK_*` lines after, and the watchdog's loop returns the moment
> it reads 0. When the stop ran on a presenting thread — `MayObserve`'s one-frame safety stop — the watchdog's one
> final flush could run while that thread was still writing, and the lines after it (the kernelbase loader detour is
> the last patch) reached the ring after the last flush ever made. The stop now says when its last line is in, and
> wakes the watchdog; the final flush waits for that, bounded at 2 s *(a quarter of a second for every patch the registry can hold since beta.45; the stop is one apply since beta.46 and the bound is for its fallback)*, then writes `STOP_FLUSHED`. And each slot has a
> commit word written after its fields: `LogNote` claims a slot with one `fetch_add` and fills it afterwards, so a
> flush on another thread could print a slot still being filled. A flush now prints only slots whose writer has
> finished — an ordinary flush stops at the first that is not, for the next one; the final flush names it in a `#`
> line. Nothing on a present path changed. Path: `%LOCALAPPDATA%\FrameLedger\logs\overlay-<pid>-<yyyymmdd-hhmmss>.log`,
> resolved by `SHGetKnownFolderPath` (§S21's rule, not the environment). No allocation: static ring,
> static 64 KiB format buffer, static path. The CaptureHost prints the file and its `FAULT` / `STOP` /
> `UNHOOK_*` / `SYMBOL_MISSING` / `PATCH_FAILED` / `SUPERVISION_LOST` lines at the end of every session
> (`Consume/OverlayLog.cs`).
>
> **The log outlives the game, and says what each ffx module was handed (0.1.0-beta.42, owner decision D121).**
> Cyberpunk 2077, set to FSR upscaling with FSR frame generation, was recorded with frame generation measured and
> the upscaler "unknown": PREPARE and FRAMEGENERATION dispatches arrived and no UPSCALE ever did. The log could
> not say why. **It ended half a second into the session** — written at the end of init, then only when the Agent
> asked or a stop ran, and a game that exits does neither: the watchdog dies with the process. And **it could not
> have said much**: the two one-target rows logged their symbol and every other row the word `hook`, so four ffx
> modules were one indistinguishable line each. Four changes, none of them a new patch site:
>
> 1. **A fourth flush point: the watchdog, on any tick that finds something new in the ring** (`FlushLogIfNew`:
>    one load on an uneventful tick, no file opened). Events are few and bounded — installs, first sights, three
>    faults at most — so this is a handful of appends a session. The init thread's flush moved to **before** the
>    watchdog is created: `FlushLog`'s state belongs to one thread at a time, and with the watchdog flushing on its
>    first tick the old order could run two at once.
> 2. **Every install line names its module and its symbol** — `amd_fidelityfx_dx12.dll!ffxDispatch` — built at
>    compile time from the inventory row's own two spellings (`fl::inventory::NameOfRow`), so no second list of
>    names exists. The same name is on the `UNHOOK_*` and `PATCH_FAILED` lines. The event's text grew from 40 to
>    72 characters for it (an event is 96 bytes; the ring 24 KiB, static), and `PatchTarget` lost its default
>    name, so a new call site cannot log `hook`.
> 3. **`FFX_DISPATCH` / `FFX_QUERY`: each descriptor type a hooked ffx-api module was handed, once**
>    (`fl_ffx_types.h`). Text = the module, `b` = the type (`0x10001` UPSCALE, `0x20004` / `0x2000c` PREPARE,
>    `0x20003` FRAMEGENERATION, …); `a` ≠ 0 with `b=0` says what did not fit (`1` a ninth distinct type, `2` a
>    type of zero). The detour keeps the value of `type` — the member it reads already to choose an arm — in the
>    first free of eight slots per module and entry point; the watchdog writes it. A query's type is kept whatever
>    the vendor answered. `UPSCALE_ROUTE` names each source an UPSCALE came from, once (the FSR 3.0 host has no
>    descriptor type, so this is the only line that says its export was called), and `RUNTIME_CENSUS` repeats the
>    census word (`b`) whenever it gains a bit — both from words the detours kept before, at no cost to a hook.
> 4. **`PATCH_FAILED`** (§Unhooking) is the fourth: a patch that did not go in used to leave no line at all.
>
> **This is a diagnostic, not a measurement, and it is not behind the Advanced-metrics switch** — like
> `HOOK_INSTALLED`, it reaches the log file and nothing else: no record, no region of the shared memory, no session.
> Rule 4: the only thing read is `type`, of a descriptor handed to a hooked call. Rule 5: no allocation, no lock,
> no logging in the body; a type already kept costs loads and no write.
>
> **Measured overhead** (on `fl_ffx_types.h` itself with the Overlay's compile flags — `/O2 /GS /guard:cf
> /Qspectre`, 50 million calls a run, three runs, i7-14700KF): a frame's three dispatch types in turn, **+3.8 ns a
> dispatch** (3.79 / 3.87 / 3.81); a type in the first slot +0.9 to +1.0 ns; in the eighth, the worst steady
> state, +8.1 to +8.4 ns. A new type costs one compare-exchange, once per type and module.
>
> ctest `fl_ffx_types` (the header, including eight threads meeting the same six types at once: each kept exactly
> once) and `fl_guard` `[log]`: a harness on the 1.1.x monolith, generating frames and asking a ratio query, is
> supervised and told **nothing else** — no flush request, no stop — and its log holds the two install lines by
> module and symbol, the three dispatch types and the query type once each, the route and the census; the process
> is then killed and the file is as it was, one header, no `STOP`.

> **Tests and the real logs folder (2026-09-15).** The injection tests use the shipped Overlay, so their harness
> logs land in the user's real `logs\` too: measured that day, 2506 overlay logs, **2492 of them hook-harness's**,
> left by test runs since 2026-09-06, beside the owner's 14 from real titles. **The path stays not-a-parameter**: an
> environment variable or any other switch in the injected DLL would reopen the per-launch vector §S21 closed, and a
> test build of the Overlay would stop testing the binary that ships. Instead **each test binary removes what it
> caused** when its run ends: an `overlay-*.log` created after the run started whose first line names a hook-harness
> image that binary owns. Natively a Catch2 listener in `guard_test.cpp` over `src/native/tests/overlay_log_sweep.h` (the image
> must be this build's `FL_HARNESS_EXE`); in managed code `tests/Shared/HarnessOverlayLogSweep.cs`, an xUnit assembly
> fixture that `FrameLedger.DrainFixtures.targets` compiles into every test project staging the harness (the copy
> beside the test binary, plus directories a test registers — `PipeEndToEndTests` runs a renamed copy from its
> scratch directory). Test assemblies run in parallel with their own copies, so none removes a log another is about
> to read; a game's log names the game. **`tools/test-artifacts-check.ps1` makes a missing sweep red**: a harness log
> created during the gate and still there fails it. The logs from before this change are the owner's to delete.

## Test harness

`src/native/tools/hook-harness` — a ~~minimal D3D11~~ D3D11/D3D12 app (plus `--vulkan` / `--opengl` modes — corrected 2026-10-04) that presents at a controlled rate. It lets CI and local dev exercise hook paths with **no game and no anti-cheat surface at all** (`14_TESTING`).

Two choices make it run on a hosted CI runner, which is what made vtable-index
verification a dev-machine-only affair before:

- **WARP** (`D3D_DRIVER_TYPE_WARP`) — a software rasteriser, so no adapter is required.
- **`CreateSwapChainForComposition`** — no `HWND`, so no dependency on a window station or an interactive session.

Current modes: `--probe-vtable` (§H4, ctest `fl_vtable_indices`), `--probe-proxy` (§H5, ctest `fl_proxy_swapchain`), `--probe-unhook` (§H7, ctest `fl_unhook_preserves_foreign`), `--probe-cost` (NFR-1, measurement only), **`--probe-frames`** (ctest `fl_frame_identity` — what counts as a frame, against `GetLastPresentCount`), **`--probe-d3d12`** (ctest `fl_d3d12_acquisition` — device → command queue → swapchain), `--present N`, `--hold N`, **`--real`** and **`--plus-ui K``**, **`--vulkan --hold-presenting N`** (P1 item 3, real loader, exit 77 = cannot run here) and **`--opengl --hold-presenting N`** (P1 item 4, gdi32 `SwapBuffers` on a hidden window, Microsoft's generic renderer — runs on CI). *(Added 2026-10-04, each with its own ctest: `--probe-d3d12-vtable`, `--probe-dxr`, `--probe-dxr-inputs`, `--probe-upscaler-resolve`, `--probe-sl-abi`, `--probe-ffx-resolve`, `--probe-sl-inputs`, `--probe-sl-seen`, `--probe-dxgi-count`; and the `--hold-presenting-*` injection fixtures with `--load` / `--load-after-ms`.)* *(Added 2026-11-12, Advanced metrics train B: `--probe-adv-fields`, ctest `fl_adv_fields` — `fl_adv_fields.h` alone: a struct of a lower version is not read past its end, the vendors' sentinels never leave as values, the summaries, the publication, a new request starting afresh; and `--advanced` / `--ffx-memory-query v1|v2`, which make the `--hold-presenting-upscaled` and `--hold-presenting-ffx` fixtures send what Advanced metrics reads.)*

> **Every present here used to carry `DXGI_PRESENT_TEST`, which submits nothing.** Measured: 500 of them leave `GetLastPresentCount` at 0 while 37 real presents move it by 37. "N presents → N records" was therefore satisfiable only by a writer counting non-frames. `--real` issues real presents; the test-present path is kept as a named mode because it is what an alt-tabbed title actually runs.

Still to add as the hook layer grows: ~~Vulkan devices, RT PSO creation and ray dispatch, stub upscaler exports with the real vendor names,~~ *(the struck three exist — corrected 2026-10-04)* a PSO-compile spike generator, and a fault-injecting hook body for the self-disable path (`14_TESTING` §Integration tests).
