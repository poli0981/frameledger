*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 6 · RT detection

### ✅ The pre-flight, before a single hook was written — 2026-08-20

`hook-harness --probe-d3d12-vtable` (ctest `fl_d3d12_vtable_indices`) and
`--probe-dxr` (ctest `fl_dxr_probe`), on this machine: Windows 11 Insider 29648,
RTX 5080, MSVC 14.51.

**Adapters, and all three can ray-trace.**

| Adapter | D3D12 | `RaytracingTier` |
|---|---|---|
| NVIDIA GeForce RTX 5080 | yes | **12** (`TIER_1_1`) |
| Microsoft Basic Render Driver | yes | **12** |
| WARP (software) | yes | **12** |

So `HANDOFF` item 4's *"check first whether WARP on the CI runners supports DXR"*
is answered **yes on this machine's WARP**, which means a DXR fixture is not
condemned to a GPU box. CI still has to answer for its own `d3d10warp.dll`; the
tier is a property of that binary, and `fl_dxr_probe` prints it on every run.

**Slot indices, proved by behaviour on BOTH list types.** Slot 72 is
`BuildRaytracingAccelerationStructure` and slot 76 is `DispatchRays`: patched,
called by name through the interface, detour ran, slots restored and the restore
asserted. 60 consecutive runs of each probe, zero failures.

### 🔴 `Reset()` moves `DispatchRays` into the vendor driver, and the first version of the hook never fired

**THE FIRST THREE ANSWERS WERE MEASURED ON THE WRONG OBJECT, and two of them were
wrong because of it.** The probe originally read the vtable off a **freshly
created** command list and compared vtable ARRAYS. A game's list is Reset every
frame, and the Overlay patches the FUNCTION a slot points at rather than the slot
— so both choices measured something no hook depends on. Corrected here rather
than quietly re-run: the wrong answers are the useful part.

| Question | measured on a **reset** list, comparing **functions** |
|---|---|
| two DIRECT lists, one device | **same functions** — one inline patch covers every list. Their vtable ARRAYS differ (per-object after Reset), which is why a *slot* patch would have to be repeated and a *function* patch does not |
| a command allocator (the control) | different, as it must be |
| DIRECT vs COMPUTE | **same functions** — slot 72 `D3D12Core.dll` in both, slot 76 `nvwgf2umx.dll` in both. One detour per method covers both list types |
| a WARP list vs a hardware list | **DIFFERENT** — slot 76 is `nvwgf2umx.dll` on the RTX 5080 and `D3D12Core.dll` on WARP |
| a fresh list vs a reset list | **DIFFERENT** — slot 76 moves from `D3D12Core.dll` to `nvwgf2umx.dll`; slot 72 stays in `D3D12Core.dll` |

**The failure this produced, before the fix.** The Overlay read its targets off an
unreset throwaway list, so it hooked `D3D12Core.dll`'s `DispatchRays` — a function
no title on this GPU ever calls, because the first `Reset` hands the method to the
driver. The hook installed, published `FL_HOOK_RT_DISPATCH`, and never fired. The
injected fixture caught it exactly: `withAsBuild = 60`, **`withDispatch = 0`**,
`hooks = RT_DISPATCH | RT_AS_BUILD` — a mask bit with nothing behind it, which is
the honesty failure the whole entitlement machinery exists to prevent.

**Why one hook worked and the other did not, which is what made it hard to read.**
`BuildRaytracingAccelerationStructure` stays in `D3D12Core.dll` across the swap, so
the AS-build hook fired from the first run. A pair where one half works reads as a
bug in the other half's *detour*, not in the *acquisition* both share.

**The fix is one call**: put the throwaway list through the same lifecycle a game's
list goes through — `Close()`, `Reset()` — before reading its vtable. `--probe-dxr`
Q5 now prints the module on each side of the Reset, so a runtime that stops
swapping, or starts swapping the other slot, says so rather than being discovered
by a silent zero on a real title.

**And Q4 stops being permissive.** With the correction, a WARP list and a hardware
list resolve `DispatchRays` to *different* functions, so a throwaway-**device**
acquisition would silently miss every call on an NVIDIA GPU. The Overlay takes its
targets off a list created on the **game's own device** — which was already the
design, for the unrelated reason that this machine lost WARP's D3D12 path to an
Insider build for a fortnight (§Traps), and now has a second reason that is about
correctness rather than availability.

**Bundles are not a further case**, and this is a documented API constraint rather
than something measured here: `D3D12_COMMAND_LIST_TYPE_BUNDLE` does not permit
`DispatchRays`. Stated as an inherited claim so the next reader knows which of
these lines came from a run.

### ✅ Both hooks, proved by injection — 2026-08-20

`ctest fl_guard`, the case *"the injected Overlay records ray-tracing work, and an
AS-build-only title is not a negative"*. Two harness modes that differ by **one
recorded call**, sharing their acceleration structure, state object, swapchain and
loop, so any difference in the record is attributable to that call:

| Mode | records | `FL_MEASURED_RT` | `AsBuildObserved` | `DispatchObserved` | `dispatchRaysVolume` |
|---|---|---|---|---|---|
| `--hold-presenting-dxr` | 60 | every one | 60 | ≥ 8 | exact multiple of 64×32×1 |
| `--hold-presenting-rayquery` | 60 | every one | 60 | **0** | **0** |

The rayquery arm is what makes `03_METRICS:226` falsifiable instead of a sentence:
a writer with only the `DispatchRays` hook sees **nothing** there, and its silence
is indistinguishable from a real negative — `rtTier` is 12, the mask bit is set,
evidence is zero, and the `No` branch fires about a title that ray-traces every
frame. **It does not run a RayQuery shader**, and does not need to: the claim under
test is "AS-build catches a title `DispatchRays` misses", and the absence of a
dispatch is what tests it.

`DispatchRays` also needs a **bound raytracing state object** to be RECORDED at
all: measured 2026-08-20, it access-violates inside D3D12Core with no state object
set, with a well-formed shader table and with a zeroed one alike. That is why the
fixture carries a 1.2 KB DXIL raygen library (`dxr_raygen.hlsl`, compiled by hand
and checked in with its command line).

### ✅ Both hooks against four real titles' own settings menus — 2026-08-20

§8 carries the five captures in full. What belongs here is the RT half:

- **`Yes` twice, `No` twice, every verdict agreeing with the game's own menu.** Cyberpunk with
  path tracing on and Wukong at RT High both read `Yes`; Rune Factory, whose menu has no
  ray-tracing option, and Cyberpunk with every RT option switched off both read **`No`** — the
  branch that had never been reachable in this project.
- **`rt_frame_pct` reads 25.0% at ×4 on two titles**, which is the falsifier `03_METRICS`
  §RT/PT/RR pre-committed. It did not fire.
- **`faults = 0` on every run**, with both detours on the render thread of titles running at up
  to ~950 presents/second.
- **The dispatch extent is exact.** Alan Wake 2: `4,913,280` rays per RT-active present, and
  `3 × 1706 × 960` is `4,913,280` — three rays per render pixel, against a render resolution
  the menu states outright.

### Still open in §6
- The same measurements on an **AMD or Intel** GPU. Every vendor-specific result
  above is one driver's: `nvwgf2umx.dll` takes `DispatchRays` and leaves
  `BuildRaytracingAccelerationStructure` alone, and nothing says another UMD splits
  them the same way — or leaves either in `D3D12Core`:
- Recorded-vs-executed semantics and counter concurrency (§H6):

