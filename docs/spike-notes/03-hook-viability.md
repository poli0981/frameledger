*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 3 · Hook viability on `hook-harness` only

**Already settled — the shared-memory layout.** ✅ `fl_shm.h` compiles under
MSVC 19.51 with `/W4 /WX`, every `static_assert` holds, and `fl-layout-dump`
output is **byte-identical between MSVC and gcc** across all four structs and
39 field offsets. All four regions are exactly 64 bytes with no implicit
padding, and the mapping is 512 KiB + 192 B of header at the default capacity.
This is what the corrected `07_IPC` layout was worth checking twice: the
earlier design put `FlControlBlock` at an offset that overlapped the header.

### ✅ H1 · `/guard:cf` with MinHook trampolines — **SAFE** (2026-08-02)

Probe: `src/native/tools/fl-probe-hookprofile`, built with the exact Overlay
flag set and run as ctest `fl_hook_profile`. MinHook v1.3.4, commit
`c3fcafdc`. Result: the indirect call from a `/guard:cf` module into MinHook's
runtime-allocated trampoline **succeeds**; `original=44 hooked=144`, and the
unhook path restores the original behaviour.

**A green probe is only worth what its setup proves, so the setup was verified
separately** rather than assumed — if CFG had not actually been enforcing, the
call would have succeeded for reasons that say nothing about H1:

| Evidence | Value |
|---|---|
| PE DLL characteristics | `Control Flow Guard` present |
| Guard CF function table | 84 entries |
| Guard Flags | `0x10014500` |
| Guarded indirect call sites in `.text` | **114** |
| `GetProcessMitigationPolicy` | `EnableControlFlowGuard = 1` |

Why it works: Windows marks memory allocated `VirtualAlloc(PAGE_EXECUTE_*)` as
a valid call target in the process CFG bitmap by default, so a trampoline is
callable without any `SetProcessValidCallTargets` dance.

⚠ **Measured with CFG strict mode OFF** (the probe reports it explicitly). A
host process that enables strict mode does not auto-validate dynamic code and
could still `__fastfail`. That is rare, and it is **not** catchable by
`FL_HOOK_GUARD` — see §H8. Treat it as a residual risk to watch for on real
titles, not as a solved problem.

### ✅ H3 · `-D_HAS_EXCEPTIONS=0` with `<atomic>` — **SAFE, with a sharp edge**

Compiles and runs under MSVC 19.51 with the full Overlay flag set, real
`fl_shm.h` included. `std::atomic_ref` is **lock-free at both 32- and 64-bit
widths** — the part that actually matters, since a non-lock-free atomic would
take a mutex and violate CLAUDE.md rule 5 in the present hook.

The sharp edge is what the define *does*, which is not "remove failure paths":

```
yvals.h:     #define _THROW(...) (__VA_ARGS__)._Raise()
             #define _RAISE(x)   _MSVC_STL_DOOM_FUNCTION(...)
__msvc_doom_core.hpp:  #define _MSVC_STL_DOOM_FUNCTION(mesg) __fastfail(5)
```

So a would-be exception becomes `__fastfail` — **an immediate, uncatchable
kill of the host game**. In an injected DLL that is arguably worse than an
exception, and SEH cannot intercept it (§H8 again).

It is safe here only because the hot path does not use throwing STL at all:
`<atomic>`, `<cstdint>` and `<type_traits>` contain **zero** throw-sites. That
makes the existing "no STL containers that allocate in hook paths" rule
load-bearing rather than stylistic — and arguably it is now a feature, since
violating it fails loudly in testing instead of propagating quietly.

### ◐ H2 · Hook installation under the loader lock — **safe path verified**

The deferred pattern holds: 20 MinHook enable/disable cycles (each suspending
every other thread to patch) while a worker thread ran **4,510**
`LoadLibrary`/`FreeLibrary` cycles. No deadlock, thread joined cleanly.

**Be precise about what this does not show.** It does not prove the naive
inline install deadlocks. A probe that deadlocks cannot report its own result,
and hanging CI to demonstrate a hazard we have already decided to avoid buys
nothing. So: the safe path is *verified*; the case against installing from
inside the `LoadLibrary` hook rests on the documented mechanism — suspending a
thread that holds the loader lock — and remains an argument, not a measurement.
H2 stays open in `20_OPEN_QUESTIONS` for that reason.

### ✅ H4 · Vtable indices — **all three proved, and CI can prove them too**

`hook-harness --probe-vtable`, ctest `fl_vtable_indices`.

A vtable slot carries no identity, so "check that slot 8 is Present" is not a
question the runtime can answer — which is why the old "verified at runtime
against the dummy object" wording was unimplementable. What *is* answerable is
behaviour: patch the slot, call the method, see whether the detour ran.

| Slot | Claim | Result |
|---|---|---|
| 8 | `IDXGISwapChain::Present` | ✅ detour ran on `Present()` |
| 13 | `IDXGISwapChain::ResizeBuffers` | ✅ detour ran on `ResizeBuffers()` |
| 22 | `IDXGISwapChain1::Present1` | ✅ detour ran on `Present1()` |

Also confirmed: `IDXGISwapChain` and `IDXGISwapChain1` return the **same vtable
pointer** — one concrete object with the table extended past the base
interface — so slot 22 is reachable from either interface pointer. And slot 8
resolves inside a mapped image, with `dxgi.dll` loaded at the expected base.

**The CI half of §H4 is answered too.** The worry was that a hosted runner has
no GPU, no DXGI output and no interactive window station, so index probing
would be dev-machine-only and the test would have to be skipped. Two choices
remove that: **WARP** (software rasteriser, no adapter needed) and
**`CreateSwapChainForComposition`** (no `HWND` at all). Feature level `0xB000`
obtained headless. These run as ordinary ctests.

### ◐ H5 · Proxy swapchains — **less dangerous than assumed, not yet cleared**

`hook-harness --probe-proxy`, ctest `fl_proxy_swapchain`. The harness builds a
real forwarding `IDXGISwapChain` wrapper — what `sl.interposer` and ReShade
hand the application — and presents through it while our hook sits on the
**real** vtable.

```
real vtable  00007FFE3C87C688
proxy vtable 00007FF701716F20   (different, as expected — we never patched it)
proxy saw 1 present;  hook on the REAL vtable saw 1
```

**The hook still fires.** The proxy forwards with `real_->Present(...)`, an
ordinary virtual dispatch through the real vtable, so patching the real vtable
catches it one layer down. The naive fear — "a proxy has its own vtable, so we
miss the present entirely" — does not hold for a forwarding wrapper.

Why this is not yet a clearance for §H5:

1. **A forwarding proxy is the easy case.** DLSS-G does not merely forward; it
   presents *interpolated* frames the application never submitted. Whether
   those reach a real-vtable hook at all is the question that matters for the
   FG counting in `03_METRICS`, and this harness cannot answer it.
2. **We see the post-proxy call.** Parameters the proxy rewrote are what we
   observe, not what the game passed.
3. **A proxy that owns a different swapchain** — rather than wrapping ours —
   would still be invisible.

So: layering does not inherently break the strategy, which is genuinely good
news for the vtable approach. A real Streamline/DLSS-G title is still required
before §H5 closes.

### 🔴 Guard · The documented driver scan is blind unelevated — **fail-open found**

Measured 2026-08-02, Windows 11 26300, standard user (the default Agent
configuration under ADR-9). Independently reproduced twice.

| API | Result unelevated |
|---|---|
| `EnumDeviceDrivers` (what `19_SAFETY` specified) | `ok=True`, 258 drivers, **0 usable base addresses**, **1** recoverable name (`ntoskrnl.exe`) |
| `NtQuerySystemInformation(SystemModuleInformation)` | `STATUS_SUCCESS`, 258 modules, **258 distinct full paths**, real driver names legible |

The first one **succeeds while telling you nothing**. A guard built on it would
report "no anti-cheat driver present" on a machine running Riot Vanguard. That is
a fail-open in the hard gate, in the default configuration — the single most
serious defect found in this project so far, and it was in a shipped doc.

`19_SAFETY` §Pre-injection checks now specifies the `Nt*` route. Caveats that
must not be lost: the API is documented-as-unsupported, and
`RTL_PROCESS_MODULE_INFORMATION`'s layout is version-sensitive — my own quick
probe mis-computed the path offset by two bytes (`INDOWS\system32\...`), which is
exactly the class of error that must fail closed rather than silently match
nothing. Assert the struct offsets; treat any parse failure as *refuse*.

### ✅ H7 · Unhook does not clobber a later hooker — **closed**

`hook-harness --probe-unhook`, ctest `fl_unhook_preserves_foreign`. The foreign
hooker is simulated rather than depending on RTSS being installed, so the test
is deterministic and runs on CI instead of only on this machine.

```
[PASS] the foreign hook chained through ours (it saved our detour as its original)
[PASS] our unhook DECLINED to restore, because the slot is no longer ours
[PASS] the foreign hook is still installed - we did not silently remove it
[PASS] the foreign hook still fires after our unhook
[PASS] with the slot untouched, our unhook DOES restore
[PASS] the slot holds the pristine entry again
```

The mechanism is the interesting part: a later hooker saves **our detour** as
*its* original and chains through it, so an unconditional restore does not
"clean up after ourselves" — it deletes their hook, silently, and their overlay
stops working with no error anywhere. `17_HOOK_ENGINE`'s "cleaner uninstall"
claim was backwards and is corrected.

Both halves are asserted deliberately. A compare-and-restore that never restores
would pass a one-sided test and leave our detour permanently in every process.

### ✅ Per-present cost — **8.4 ns, against a 1,000 ns budget**

`hook-harness --probe-cost`. 20,000 presents × 5 runs, hooked and unhooked runs
**interleaved** so scheduler or thermal drift during the run is not attributed
to the hook, medians compared.

| | ns / present |
|---|---|
| unhooked | 317.9 |
| hooked | 326.3 |
| **delta** | **8.4** (budget 1,000 — NFR-1, `14_TESTING` §Hook overhead item 1) |

**Read this narrowly.** The detour is an atomic increment plus a call through the
saved pointer, i.e. the *floor* for any vtable hook — it bounds the mechanism,
not the product. The Overlay's real per-present cost is `14_TESTING` item 2,
measured on a real game, and that number is not this one.

Not registered as a ctest: a timing threshold on a shared CI runner fails for
reasons that have nothing to do with the code. Run it deliberately.

### ✅ The Overlay is a real capture side now — and what it does NOT measure (2026-08-05)

Recorded here retrospectively: PRs #40–#44 changed 8 files, all under `src/native/`,
and touched no documentation, so none of the below was written down at the time.

| | |
|---|---|
| Hooks installed | **three**, on the shared `dxgi.dll` class vtable: `Present` (8), `ResizeBuffers` (13), `Present1` (22) |
| Acquisition | throwaway WARP D3D11 device + `CreateSwapChainForComposition`, vtable read, dummy released — no HWND, runs headless |
| Ring | `Local\FrameLedger.Ring.<pid>`, DACL = current user's SID only, `ERROR_ALREADY_EXISTS` refuses |
| ctests | **15**, incl. four `[inject][shm]` cases that inject the **real** Overlay into `hook-harness` cross-process |

**The `api` field, and a Windows API that does not do what the docs imply.**
`IDXGISwapChain::GetDevice` on a **D3D12** swapchain returns the **device**, not the
command queue — even though `CreateSwapChainForComposition` is *given* the queue. DXGI
resolves the queue to its owning device before storing it. The obvious implementation
(query the result for `ID3D12CommandQueue`) made every record from a real D3D12 target
come back `FL_API_UNKNOWN`. Both queries are kept: a DXGI that *did* hand back the queue
would otherwise regress to `UNKNOWN` silently.

**What the Overlay measures today is 8 of 23 record fields**, and the other 15 are
reported as *not measured* rather than defaulted:

| Written | Left at "not measured" |
|---|---|
| `qpc`, `frameIndex`, `presentFlags`, `syncInterval`, `api`, `swapchainId`, `outputW/H` | `renderW/H`, `upscaler`, `upscalerQuality`, `fgMode`, `fgEvaluations`, `rtFlags`, `dispatchRaysVolume`, `maxTraceRecursionDepth`, `psoCreatedThisFrame`, `vramUsedBytes`, `reflexLatencyUs`, `hdr` |

`measuredMask = FL_MEASURED_OUTPUT_RES` and `rtFlags = FL_RT_NOT_MEASURED` on every
record. Without those two bytes a present-only writer would assert "no upscaler, no
frame generation, no ray tracing" as *measured fact* ~118 times a second — `fg_factor`
1.0 (rule 6) and a definite RT `No` (rule 7) about a title nobody looked at.

**Two fields have a producer and no consumer, and one has neither:**
`FlWriterState::vramBudgetMb` is never written (no `QueryVideoMemoryInfo` hook);
`FlControlBlock::overlayEnabled` is never read; and `FlControlBlock::pauseRequested` is
never written by anything, while its only reader ~~is unreachable on any frame where
`guardTicks` changed~~.

> **Half of that last clause was fixed five lines below it, in the same PR, and this
> sentence was left in the present tense.** The reader is no longer unreachable — #46
> moved the deadline onto the watchdog thread, which removed the early return that
> jumped the pause check. What *is* still true is the first half: nothing writes
> `pauseRequested`. `ShmRingReader.SetPaused` is the intended writer and it has **no
> caller and no test at all** — three symbol sites in the whole tree, none of them a
> test — so the round trip has never been driven end to end even though both halves
> exist and the harness that would drive it is already staged.

**Two gaps found the same day by tracing call paths, and one is still open.**

- ✅ **Both runtime stops were unreachable in a non-presenting process**, and
  `pauseRequested` was unreachable on any frame where `guardTicks` changed — one root
  cause, `MayObserve()` having exactly one caller. Measured in both directions:
  `unhookRequested` against a live `--hold` target left `status` at `READY` through 10 s
  of polling, and a paused session leaked **12 records across 12 guard ticks, exactly one
  per tick** (`writeIndex` 9 → 21). Closed by the watchdog thread (§S25).
- 🔴 **The three-fault self-disable still has no test at all.** `NoteFault` discarded
  `MH_DisableHook`'s return and set no `g_observing`; that is fixed, without a regression
  net. The blocker is the vehicle — a fault seam compiled into a DLL that ships into
  games is rejected on sight, and the `VirtualProtectEx` alternative cannot locate the
  target's view base. Both approaches and their failure modes are written into
  `src/native/tests/CMakeLists.txt`.
- The 65-second value **in its real configuration** is also unverified: the stop is proven
  to fire, but a suite that runs on every build cannot wait 65 s, so what is tested is the
  mechanism, not the number.

### Still open in §3

Both remaining items need something this harness cannot synthesise:

- **§H2** — the deferred `LoadLibrary` install pattern is verified against heavy
  loader contention, but the case against the naive inline install still rests
  on the mechanism, not a measurement. Exercising it needs a real game that
  loads D3D12 lazily.
- **§H5** — a forwarding proxy does not defeat a real-vtable hook. DLSS-G
  presenting *interpolated* frames the application never submitted is the case
  that matters for FG counting, and it needs a real Streamline title.

Everything else in §3 is answered: H1, H3, H4, H7 and the per-present cost.

