# 18 — GPU telemetry

Telemetry is layered so that **no layer's licence can hold the project hostage**, and so that a missing vendor library degrades a few fields instead of the whole feature.

| Layer | Source | Licence | Covers |
|---|---|---|---|
| **L1 — baseline** | DXGI + Windows performance counters (PDH) | none needed (OS APIs) | GPU utilisation, adapter VRAM, per-process VRAM, driver/adapter identity — **all vendors** |
| **L2 — sensors** | LibreHardwareMonitorLib | **MPL-2.0** (GPL-compatible) | Temperatures, clocks, power, fan — **AMD + Intel + NVIDIA**, plus CPU/board when elevated |
| **L3 — NVIDIA extras** | NVAPI, headers vendored | **MIT** | Throttle reasons, per-domain utilisation, **Reflex / PC latency** — things no other layer exposes |

L1 always works. L2 fills in what L1 cannot see. L3 adds NVIDIA-only depth. **No AMD or Intel vendor SDK is used or bundled** — see §Vendor SDKs we deliberately do not use.

## Abstraction

```csharp
public interface IGpuTelemetrySource : IDisposable
{
    TelemetryLayer Layer { get; }           // L1 / L2 / L3, for the composite descriptor
    GpuCapabilities Capabilities { get; }   // which fields are real on this machine
    bool TryRead(out GpuSample? sample);    // 1 Hz, called by the Agent; false = nothing yet, or disabled
}
```

> **Written 2026-09-03 as `FrameLedger.Application.Telemetry`**, with two additions to what
> this block used to show: `Layer`, so the composite can record which layer supplied a
> value without reflecting on the type, and `GpuSample.AdapterName`, so a report on a
> multi-adapter machine can say which one a sample describes. `Capabilities` is
> **value-derived and monotonic**: a bit is set the first time its field carries a value
> and is never cleared by a later null tick; a sensor that exists in the tree and never
> reports sets nothing. That is the one direction a source must never get wrong — a
> capability bit over a null field is a zero presented as a measurement.

Implementations compose rather than compete:

- `BaselineTelemetrySource` (L1) — always constructed.
- `LhmTelemetrySource` (L2) — ~~constructed when LHM initialises~~ always constructed; a `Computer` that will not open disables the layer for the session (corrected 2026-10-04).
- `NvapiTelemetrySource` (L3) — ~~constructed only on NVIDIA hardware~~ **always constructed since 2026-09-10 (P2 PR-E2)** and disables itself when the bridge DLL or the NVIDIA driver is absent, so the composite's descriptor simply omits `nvapi` and no caller has to know the vendor before composing.
- `CompositeTelemetrySource` merges them with a fixed precedence per field (**L3 > L2 > L1**) and records which layer supplied each value.

`GpuSample`: `takenAt`, `layer`, `adapterName`, `tempCoreC`, `tempHotspotC`, `tempMemoryC`, `loadPct`, `vramAdapterMB`, `coreClockMhz`, `memClockMhz`, `powerW`, `fanRpm`, `throttleReasons`, `pcieGen/Width`. Every field nullable — `Capabilities` says what to trust, and the UI shows `N/A` rather than zero.

`sessions.telemetry_source` stores the **composite descriptor** (e.g. `l1+lhm+nvapi`), not a single name, so a user can see exactly why a field is missing.

> **Written 2026-09-09 (P2 PR-E1): `BaselineTelemetrySource` (L1), `CompositeTelemetrySource`
> and the 1 Hz thread (`TelemetryPoller`, `fl-telemetry`).** Three things this block did not say
> and the code now does. (1) The port gained `IsDisabled`: a layer that has fallen to
> `Capabilities.None` because it was disabled and one that has simply not read yet were
> indistinguishable through the port, and the **descriptor lists the layers still standing**
> — `TelemetryLayerNames.Describe`, lowest first — so a layer disabled mid-session drops out of
> the string the session is stored under. (2) The composite is **not a layer**: its `Layer` is
> `None`, a merged sample's `Layer` is the highest layer that contributed anything, and
> `LayerOf(field)` answers per field. It also applies the two-fault rule one level up — a layer
> whose `TryRead` throws (which the port forbids) is excluded on the second throw, the others
> untouched. (3) Identity is its own record, `GpuAdapterIdentity` (name, LUID, ids, memory sizes,
> user-mode driver version), read once per session from DXGI; ~~the Agent hands the handshake's
> `adapterLuid` to `SelectAdapter` so samples describe the adapter the game presented on.~~ *(Corrected 2026-10-04:
> nothing calls `SelectAdapter` outside tests. L1 stays on the first hardware adapter in DXGI's high-performance order,
> as `HardwareSnapshotSource` does, and the handshake's `adapterLuid` is read by no managed code — so on a
> multi-adapter machine L1 can describe an adapter the game did not present on. The game's own memory, D43, is
> summed over every adapter and is not affected.)* **Built in beta.13:** the recorder hands the handshake's
> `adapterLuid` to the poller at the first tick that carries one (`CaptureProgress.AdapterLuid`, noted in the
> `.partial`), and `CompositeTelemetrySource.SelectAdapter` points L1 at it (`IGpuAdapterSelector`) and tells the layers
> that read their own GPU its vendor (`IGpuVendorFollower`): L2 maps that vendor's GPU node instead of the first one
> LibreHardwareMonitor lists (on a laptop often the integrated GPU), and L3 contributes nothing when the game's adapter
> is not NVIDIA's — N/A rather than another card's temperature. **What stays first-found:** a session before the game's
> first present, a Tier-2 session (no handshake), a game presenting through OpenGL or Vulkan (they publish no LUID), two
> GPUs of one vendor (L2 and L3 take that vendor's first), and the session's hardware snapshot
> (`HardwareSnapshotSource`, taken before the first present). Every
> poller sample is stamped with QPC (`TelemetrySample.QpcTicks`), the ring's clock, and
> `QpcClockTests` pins `Stopwatch` / `TimeProvider.GetTimestamp` to the real counter.

> ~~Per-process VRAM comes from the **Overlay** (`IDXGIAdapter3::QueryVideoMemoryInfo` inside the game), not from here.~~
> **Corrected 2026-10-03 (beta.12, D43): the game process's memory is read from OUTSIDE the game** — §The game process's
> memory below; the Overlay never called `QueryVideoMemoryInfo`. Adapter-wide VRAM is a separate series with a different
> label — they answer different questions and users will otherwise think one of them is broken.

### The game process's memory (beta.12, owner decision D43)

`FrameLedger.ProcessStats.dll` — the third native facade the Agent loads beside itself (`NativeProcessStats`, after the
guard's and the NVAPI bridge's), never into a game — reads one process's memory per call:

- **Video memory:** the documented performance counters `\GPU Process Memory(pid_<pid>_luid_…_phys_<n>)\Dedicated
  Usage` and `\Shared Usage` (`PdhAddEnglishCounterW` with a `*` instance, so a localised counter name does not matter;
  `PdhGetFormattedCounterArrayW` after each collection), every instance naming the pid **summed** — each adapter and
  physical index the process allocated on. This is the per-process figure, so the adapter-wide rule below ("filter to our
  adapter, don't sum blindly") does not apply to it. **No handle to the game is needed.**
- **System memory:** `GetProcessMemoryInfo` with `PROCESS_MEMORY_COUNTERS_EX2` (its `PrivateWorkingSetSize` is Task
  Manager's "Memory"), retried with `PROCESS_MEMORY_COUNTERS_EX` where the OS refuses the larger structure; an EX2
  answer whose private working set was left as written (a sentinel) or reads zero beside a nonzero working set is N/A,
  never a measured zero. It needs `PROCESS_QUERY_LIMITED_INFORMATION` and
  nothing more: the session's held handle when it has one, else a handle opened for the read and closed after it, whose
  creation time must be the watcher's (a reused pid is refused, not read).
- **Not `D3DKMTQueryStatistics`.** It answers the same per-process segment figures, but Microsoft documents it as
  "Reserved for system use"; the counters are the documented face of the same accounting.
- Measured on the dev box (`spike-notes` §16): one collect + read costs 0.1 ms (max 0.22 ms), a 512 MiB allocation reads
  +512.0 MiB, the private working set equals `\Process V2(…)\Working Set - Private`, 600 collections leak nothing. One
  `GameMemoryReader` per session, on the session's `fl-telemetry` thread (`TelemetryPoller`'s third source).

## L1 — baseline (no licence, all vendors)

- **DXGI:** `IDXGIFactory6::EnumAdapterByGpuPreference` → adapter description, LUID (matches the swapchain adapter from the Overlay handshake), dedicated/shared memory sizes. ~~`IDXGIAdapter3::QueryVideoMemoryInfo` for adapter-wide usage/budget.~~ **Measured wrong, 2026-09-09:** `DXGI_QUERY_VIDEO_MEMORY_INFO.CurrentUsage` is the *calling process's* usage by the structure's own definition ~~— which is exactly why the Overlay reads it inside the game for `vram_proc` —~~ *(corrected 2026-10-04: the Overlay never read it, and `vram_proc` was never produced — §The game process's memory)* and from the Agent it read **0 bytes** beside a 16 GB adapter. Adapter-wide usage is the PDH counter below, as this list always said; DXGI is identity only (`DxgiAdapters`, the first CsWin32 consumer in the tree).
- **PDH performance counters** — the same source Task Manager uses, fully documented, vendor-neutral:
  - `\GPU Engine(*)\Utilization Percentage` (sum per engine type: 3D, Compute, Copy, VideoDecode)
  - `\GPU Adapter Memory(*)\Dedicated Usage`
  - `\GPU Process Memory(*)\Dedicated Usage` ~~(cross-check against the Overlay's figure)~~ — **read since 2026-10-03
    as the game process's own figure** (§The game process's memory; the Overlay never produced one)
  - Instance names embed the LUID — parse and filter to our adapter, don't sum blindly across GPUs.

  > **Built 2026-09-09 (PR-E1), one counter of the three:** `PdhAdapterMemoryCounter` binds
  > `\GPU Adapter Memory(luid_0x<High>_0x<Low>_phys_0)\Dedicated Usage` by the selected
  > adapter's LUID — addressed directly, never a wildcard summed — and it is L1's one live field
  > (`GpuSample.VramAdapterMb`). Measured on the dev box: **1301 MB** in use on the RTX 5080 at an
  > idle desktop, moving tick to tick. A machine without the counter set fails the open and the
  > field is N/A with the layer standing — not a fault. **`Utilization Percentage` is deliberately
  > not read** (`20_OPEN_QUESTIONS` §M10, decided): `LoadPct` is L2's vendor-reported load and is
  > labelled as such. ~~`GPU Process Memory` is not read either — the Overlay's in-process figure is
  > the one the pipeline stores, and a cross-check is a diagnostic, not a field.~~ **Since 2026-10-03 (D43) `GPU Process
  > Memory` IS read** — by `FrameLedger.ProcessStats.dll`, as the game process's dedicated and shared GPU memory; there was
  > never an in-process figure to cross-check.
- **Driver version:** ~~`SetupAPI` / registry adapter properties.~~ **`IDXGIAdapter::CheckInterfaceSupport(IDXGIDevice)`** — its documented second output is the user-mode driver version for that interface, four 16-bit fields of one 64-bit value (`32.0.16.1664` on the dev box, 2026-09-09; the WARP adapter answers with the OS build). Neither SetupAPI nor the registry is walked: both need a device-instance enumeration nothing else here has a reason to carry. Feeds the hardware snapshot and the trend-chart change markers (`06_DATA_MODEL`). This is the four-part form the vendor's own tooling shows, not the marketing number (`561.09`), which only L3 knows.
- **Optional probe, P0 evaluation only:** `D3DKMTQueryAdapterInfo` with `KMTQAITYPE_ADAPTERPERFDATA` reportedly exposes temperature, power and fan for any vendor. The D3DKMT structures are **not fully documented and have changed across Windows builds**. Treat as an experiment: if it proves stable on both Win 10 22H2 and Win 11 during P0, keep it as an L1 extra behind a capability flag; if it looks fragile, drop it and let L2 handle temperatures. Never let it be load-bearing.

  > **🅓 The two-OS requirement is deferred to Win 11 only — owner decision, 2026-08-05.**
  > There is one machine and it is Windows 11 (`spike-notes.md` §Environment). Win 10 22H2
  > is a **shipped configuration** — CLAUDE.md pins `SupportedOSPlatformVersion` to
  > `10.0.19045.0` and the owner confirmed that floor **stays** — so this is not the same
  > kind of gap as AMD/Intel, where the hardware simply does not exist to be tested on. It
  > is a gap that could be closed with a VM and is being left open on purpose.
  >
  > **What makes that acceptable is the last sentence above, and it is now enforced rather
  > than hoped for:** the probe must never be load-bearing. Concretely — every field it
  > would fill (core temp, power, fan) is also reachable through **L2**, and
  > `CompositeTelemetrySource`'s precedence must treat a D3DKMT value as an *extra* behind
  > a capability flag, never as the only source. If that ever stops being true, this
  > deferral stops being valid and the Win 10 measurement becomes a release blocker.
  >
  > **Recorded because it was previously invisible.** This requirement was written in two
  > documents, satisfiable on neither machine anyone has, and — unlike §R5/§R6 — carried
  > **no written deferral at all**, which is the state P0 exit criterion 2 exists to
  > forbid.

L1 gives no temperatures on its own (unless the D3DKMT probe pans out). That is what L2 is for.

## L2 — LibreHardwareMonitorLib (MPL-2.0, all vendors)

`LibreHardwareMonitorLib` ≥ 0.9.6, consumed as an **unmodified NuGet package**. It already implements per-vendor GPU sensor access internally — which means the vendor-interop licensing problem is one LHM has solved upstream, under a licence that works for us. That is the entire reason this layer exists.

- `Computer` opened with `IsGpuEnabled` always; ~~`IsCpuEnabled` + `IsMemoryEnabled` only when the Agent is elevated and PawnIO is available~~ nothing else, ever — the CPU temperature uses a separate `Computer` (below) (corrected 2026-10-04). *(2026-11-11: still true of THIS layer's `Computer`. Advanced metrics opens a third one, of its own — the graphics card, and as administrator with PawnIO the processor, memory and motherboard groups; D103, §Advanced metrics. Storage, the power supply, controllers, the battery and the network are opened by nothing.)*
- Poll on a dedicated thread: `computer.Accept(updateVisitor)` then read mapped sensors. Never faster than 500 ms; default 1000 ms.
- Sensor mapping by `SensorType` + name heuristics per vendor (`GPU Core`, `GPU Hot Spot`, `GPU Memory`, `GPU Package Power`, …), kept in `SensorMap.cs` with unit tests against captured sensor-tree fixtures.
- **P0 verification items:** (a) which fields LHM actually returns per vendor on real hardware — fill the capability matrix below — **NVIDIA filled 2026-09-03, AMD/Intel deferred (§R5/§R6)**; (b) ~~whether GPU-only usage works **without** elevation and without PawnIO (expected yes, since GPU sensors go through user-mode vendor paths, but confirm — it determines whether the default unelevated Agent has temperatures at all)~~ — **confirmed 2026-09-03, §M5 row R1**: eight fields unelevated, the same eight elevated, PawnIO never opened; (c) ~~confirm LHM's sources are not marked with MPL-2.0 Exhibit B~~ — **done, 2026-08-02: clear.** No source file applies the notice; the only repository hit is the `LICENSE` template itself, and every file we depend on carries the permissive Exhibit A. Method and evidence in `docs/spike-notes.md` §0. Re-check on every version bump, and check *source headers*, never the LICENSE file — MPL-2.0's own text contains Exhibit B as a template, so grepping the licence finds it in every MPL project ever published.

CPU and motherboard sensors remain LHM-only, elevated-only, PawnIO-dependent, and optional.

> **The machine beside the GPU — built 2026-09-21, and deliberately NOT a fourth layer.** `ISystemTelemetrySource`
> (`Telemetry.SystemTelemetrySource`) is read by `TelemetryPoller` on the same tick as the composite and rides each
> `TelemetrySample` as a `SystemReading` — outside `GpuSample`, because the per-field capability bits and layer
> attribution above mean nothing for "how busy was the CPU". Two unprivileged Win32 calls, no process opened:
> `GetSystemTimes` (busy = (kernel + user) − idle over the interval; kernel INCLUDES idle in that API; the first tick has
> no interval and is null) and `GlobalMemoryStatusEx` (physical memory in use). **CPU temperature is the one privileged
> reading**: `LhmCpuTemperatureReader.TryOpen` opens a SEPARATE `Computer` (~~CPU group only~~ CPU and memory groups, GPU off — corrected 2026-10-04 — L2's stays GPU-only and
> unelevated by design, so the unprivileged layer's behaviour never depends on privilege) only when the process is
> elevated AND `PawnIo.IsInstalled`; it takes the package sensor (`CPU Package`, `Core (Tctl/Tdie)`) and falls back to the
> hottest core. **Unmeasured on real hardware**: exercised against a fake `ILhmComputer` only; the first elevated run
> with PawnIO is the measurement — **which the user can now choose to make (beta.10, the admin mode: Settings ▸ Capture
> agent ▸ Run the agent as administrator, `01_ARCHITECTURE` ADR-9)**; until beta.10 an elevated Agent existed only when an
> App started as administrator happened to start it, and `PrivilegeFootprintTests` now asserts on the elevated CI runner
> that opening this CPU group enables no privilege. `HelloAck.cpuTempAvailable` reports those same two conditions (it was a constant
> `false`). A tick only the machine answered is still queued, under a placeholder `GpuSample` with
> `TelemetryLayer.None` and no field. The `.partial` sensor chunk carries the three values in bits 12–14 of the
> existing 16-bit presence mask, so an older file reads back with an empty reading.

## L3 — NVAPI (MIT, NVIDIA only)

**Integration: vendored headers + import library, linked normally. ✅ Vendored 2026-08-05** at `src/native/third_party/nvapi/` — nine headers (`nvapi.h`'s include closure plus `nvapi_interface.h`), `License.txt`, and `amd64/nvapi64.lib`; **x64 only**, `x86/nvapi.lib` deliberately not taken. Consumed through the `fl_nvapi` INTERFACE target. NVIDIA publishes the NVAPI SDK — headers, interface definitions, and `nvapi64.lib` — under the **MIT licence** (verified 2026-08-02 and re-verified on the vendored copy: `amd64/nvapi64.lib` is a tracked file in the MIT repo and `License.txt` names the import libraries as the subject of the grant, so the binary is covered and not only the headers), so the SDK material can live in our GPL-3.0 repository with only the usual MIT attribution obligation. The runtime implementation lives in `nvapi64.dll`, which ships with the installed NVIDIA driver.

> **This section claimed the vendoring in the present tense while it had not happened**, and CLAUDE.md's pinned stack said the same — `src/native/third_party/` held `vulkan-headers` and nothing else. `legal/THIRD_PARTY_NOTICES.md` had already caught the identical claim about *itself* and gained a bidirectional licence gate for it; nothing propagated here. That gate is what turned the vendoring into a two-step with a free canary: adding the material while the notice still read "Not yet vendored" **failed the build**, in the present-but-marked-absent direction that was structurally invisible before the check was made bidirectional.

> This supersedes the earlier design's dynamic-loading-by-ordinal approach. That was a workaround for a licensing constraint that no longer exists; ordinal resolution is fragile across driver versions and should not be reintroduced. Vendored headers are simpler, type-safe, and maintainable.

Still resolve **entry points defensively at runtime**: `nvapi64.dll` may be absent (no NVIDIA GPU) or older than our headers. `NvAPI_Initialize` failing is a normal condition that must disable L3 cleanly, not throw.

> **Built 2026-09-10 (P2 PR-E2).** NVAPI is a C API behind a static import library, so the managed
> side reaches it through a native DLL of our own: **`FrameLedger.NvapiBridge.dll`**
> (`src/native/FrameLedger.NvapiBridge/`, C ABI in `include/fl_nvapi_bridge.h`) — the **only shipped
> binary that links `nvapi64.lib`**, read-only by construction (every export is a query), loaded by the
> Agent into **its own process** by absolute path and **never into a game**. Its name carries none of
> the words the guard's §S18 heuristic scans a launcher's ancestors for. The ABI is a fixed struct with
> a `present` bitmask per field (`FlNvReadSample`), a refcounted `FlNvInit` / `FlNvShutdown`, the driver
> version, the per-pid NGX words (`FlNvNgxState`), and size / ABI-version / build-id queries so a mirror
> that drifts is refused (`FL_NV_BAD_SIZE`) rather than read. On the managed side, all in
> `Infrastructure.Telemetry`: `NativeNvapiBridge` (the second P/Invoke facade after the guard's — same
> absolute-path rule, through the assembly's one `DllImport` resolver, `Infrastructure.Native.BesideThisAssembly`,
> because the runtime allows exactly one per assembly and two static constructors setting one is an
> exception in whichever runs second), `NvapiTelemetrySource` (a clear bit is `null`, never 0; an
> `Init` that does not answer 0 disables the layer with **zero faults**, because a machine without the
> driver is a normal condition; two throws disable it for the session), and `NvapiNgxStateProbe`
> (`INgxDriverProbe` in-process — the capture host **no longer spawns `fl-probe-nvapi.exe`**, and the
> probe target is no longer staged beside it). Verified by ctest `fl_nvapi_bridge`
> (`src/native/tests/nvapi_bridge_test.cpp`, `BRANCH: AVAILABLE|DEGRADED` required the way
> `fl_nvapi_probe` requires it) and by `NvapiBridgeMirrorTests`, which compares `Marshal.SizeOf` of both
> mirrors against the DLL's own answers and **fails rather than skips** when the DLL is not staged.
>
> **Not in this build, stated so nobody infers it from the table below:** VRAM (`FlNvSample.vram` is
> reserved and its bit is never set — see the Memory row), power, hotspot temperature, per-domain
> utilisation beyond the GPU domain, and anything Reflex (§M8: a hook fact, not a poller field).
> *(2026-11-11: still true of `FlNvSample`, the 1 Hz telemetry layer's struct. The memory figures and the other
> three utilisation domains are read since ABI 3 — below — into structs of their own, for Advanced metrics only.)*
>
> **ABI 2 (2026-09-25, beta.8): the driver profile, read-only.** The owner asked whether the NVIDIA App overrides a
> game's DLSS (frame generation, ray reconstruction, the model preset). The NVIDIA App writes those overrides into the
> driver's own settings store (DRS), so `FlNvDriverProfile(exePath, ids, count, out)` reads them there: a DRS session
> is created, the settings loaded, the profile the driver applies to that path found (`NvAPI_DRS_FindApplicationByName`
> — a fully qualified path returns the profile the driver will apply on launch; none named → the current global
> profile), each id read with `NvAPI_DRS_GetSetting` (value, where it comes from, whether someone set it), and the
> session destroyed. **No DRS setter exists in the bridge** — not `SetSetting`, not `SaveSettings`, not
> `RestoreProfileDefault`, not `NvAPI_NGX_SetNGXOverrideState`: the overrides are the user's, and this only reports
> them. A path longer than NVAPI's string is refused, never truncated (a truncated path names another program).
> The ids and their values' meanings are NVIDIA's `NvApiDriverSettings.h` (github.com/NVIDIA/nvapi, MIT), written into
> `Application.Capture.NvidiaDriverSettings` as eighteen constants rather than vendored as a header; Smooth Motion
> (`0xB0D384C0`) is not in NVIDIA's header and is documented by community profile tools only, which every surface
> that shows it says. Managed: `NvapiDriverProfileSource` (`IDriverProfileSource`) — the Agent's recorder reads the
> profile beside every session, both tiers, and stores it in `sessions.driver_profile` (schema 0012). Verified by
> ctest `fl_nvapi_bridge` (argument refusals, DEGRADED before init, the global profile of the test's own executable on
> a GPU machine) and `NvapiBridgeMirrorTests` (`FlNvDriverProfileSize`, ABI 2).
>
> **ABI 3 (2026-11-11, owner decision D101): what "Advanced metrics" reads.** Five reads, each a struct with its
> size first and a `present` mask, and each call behind it a query: `FlNvReadFacts` (what is true of the card whatever
> is running — its type, bus, PCI ids, VBIOS version, architecture, core counts, the base and boost clocks, the
> sensor's range, `NvAPI_GPU_GetMemoryInfoEx`'s four sizes, the frame-buffer sizes, the driver's package and base
> branch, NVAPI's interface version, the chipset), `FlNvReadLive` (the same memory call's free figure with evictions
> and promotions, the frame-buffer, video and bus utilisation, the video clock, the P-state, the board and
> power-supply temperatures where the card has them, the throttle word, the encoder's session count and averages),
> `FlNvReadDisplays` (per connected display: HDR formats and luminances, variable-refresh support, the adaptive-sync
> flags), `FlNvSessionDisplay` (the display a GDI device name names: its variable-refresh state and the colour space
> the driver reports for one process) and `FlNvProfileSettings` (every setting of the global profile, or only what a
> game's own profile sets — a game no profile names is answered `GLOBAL` with nothing listed, never with the PC's
> settings). No struct of ABI 2 changed size; `FL_NV_FIELD_VRAM` stays clear.
>
> **No identifier has an entry point** (D104): not `NvAPI_GPU_GetBoardInfo`, `NvAPI_GPU_GetUUID`, an EDID read, or
> `NvAPI_GPU_GetEncoderSessionsInfo`, which lists WHICH programs encode. A display is passed on by its place in the
> driver's list; NVAPI's display id is a handle of this boot and is neither stored nor shown.
>
> **Read-only is now a gate** (D108): `tools/nvapi-readonly-check.ps1` holds every `NvAPI_*` call of the bridge's
> source to `tools/nvapi-allowed-calls.txt` in both directions — forty-four names — and refuses a name that sets,
> saves, restores, registers or controls something, or that returns an identifier, **even when the list names it**.
> Managed: `INvapiAdvancedBridge` (a port of its own beside `INvapiBridge`; `NativeNvapiBridge` implements both, and a
> DLL that predates ABI 3 answers `Unavailable` rather than throwing) and `NvapiAdvancedReader`, which turns the five
> structs into the catalog's facts and readings — every `nvapi.` entry whose reach is `Agent`, and no other, held by
> `NvapiAdvancedReaderTests` in both directions. **Nothing composes the reader yet**: no Agent code calls it.
>
> **Measured on the dev box the day it was written** (RTX 5080, a driver of base branch `R620`, ctest `fl_nvapi_bridge`'s
> AVAILABLE branch — `20_OPEN_QUESTIONS` §M12): all twenty facts answer (`present = 0xFFFFF`: discrete, desktop, PCI
> Express, 84 ray-tracing and 336 tensor cores, base 2295 / boost 2670 MHz, 16,303 MiB dedicated, a DCH Game Ready
> driver); eight of the ten live groups answer (`0x33F`) — **the board and power-supply temperatures do not exist on
> this card**, as the memory temperature does not, and read N/A; the one display answers all four groups; the global
> profile lists 24 settings. One thing the header had wrong: it misspells `NV_ENCODER_STATISTICS_VER` as
> `NNV_ENCODER_STATISTICS_VER`, so the bridge names `NV_ENCODER_STATISTICS_VER1`.
>
> **The ctest could not fail on a GPU machine until the same day.** `fl_nvapi_bridge` was registered with
> `PASS_REGULAR_EXPRESSION` alone, which makes ctest ignore the exit code: the AVAILABLE branch's reference-counting
> check (two references, two releases, then a read expected to work) failed on every machine with an NVIDIA GPU while
> ctest reported Passed, and CI — no driver — never takes that branch. The check is corrected and the registration now
> carries a `FAIL_REGULAR_EXPRESSION` on Catch2's failure lines.

| Purpose | Function |
|---|---|
| Init / enumerate | `NvAPI_Initialize`, `NvAPI_EnumPhysicalGPUs`, `NvAPI_GPU_GetFullName` |
| Temperatures | `NvAPI_GPU_GetThermalSettings` (+ extended thermal query for hotspot/memory where available). **Bridge (2026-09-10):** the public enumeration only — core is the `GPU` target; memory is the `MEMORY` target when the driver lists one, and on the RTX 5080 it does not (bit clear → `null`); hotspot is not read |
| Utilisation | `NvAPI_GPU_GetDynamicPstatesInfoEx` (GPU / FB / VID / BUS domains). **Bridge (2026-09-10):** the GPU domain only, as `loadPct` |
| Clocks | `NvAPI_GPU_GetAllClockFrequencies` — **bridge (2026-09-10):** current graphics and memory clocks |
| Memory | `NvAPI_GPU_GetMemoryInfoEx` — **not `NvAPI_GPU_GetMemoryInfo`**, which this table named until 2026-08-05. The vendored headers mark it `__nvapi_deprecated_function` ("deprecated in release 520"), so under `/W4 /WX` a call to it fails the native build. **Neither is called (2026-09-10):** ~~the vendored `nvapi.h` (`cd6918f6`) does not declare `GetMemoryInfoEx` at all — the deprecation message names a function the same header set does not ship~~ — so the bridge's `vram` field is **reserved**, its bit is never set, and VRAM stays L1's PDH adapter counter. ~~Taking a newer header set is the way to change that, not a call to the deprecated one~~ **Corrected 2026-11-11: the vendored set does declare it** — `nvapi_lite_common.h:600`, which `nvapi.h` includes, with `NV_GPU_MEMORY_INFO_EX_V1` above it and its id in `nvapi_interface.h`; the search that found nothing looked in `nvapi.h` alone. It was not called because of that sentence, from 2026-09-10 until now. The bridge calls it from ABI 3 for the advanced memory rows (§Advanced metrics); `FlNvSample.vram` stays reserved, so which layer fills the base `vram_adapter` series does not change |
| **Throttle reasons** | `NvAPI_GPU_GetPerfDecreaseInfo` — not available from L1 or L2. **Bridge (2026-09-10):** the raw reason mask, and 0 is a measurement (no reason), not a clear bit |
| Driver version | `NvAPI_SYS_GetDriverAndBranchVersion` — **bridge (2026-09-10):** `FlNvDriverVersion`, surfaced as `616.64 (r616_41)` |
| **NGX per-process state** | `NvAPI_NGX_GetNGXOverrideState` (R570+) — the driver's feedback word per DLSS feature for a PID, read OUT OF PROCESS by `fl-probe-nvapi --ngx-state` and, since 2026-09-06, by the capture host beside each module snapshot — **in-process through the bridge since 2026-09-10** (`NvapiNgxStateProbe`; the spawn is retired and the words, the merge and the printed block are unchanged). Measured: the SR word's `CREATED \| EVALUATE` is set without an NVIDIA-app override; the FG word follows DLSS-G once created (`CREATED \| EVALUATE` at ×4, corrected the same day — the first reading was blind because it was early), the multiplier bits stay clear; `scalingRatio` is 0 even under an override. Identity only, SR and FG (`03_METRICS` §Upscaling) — and, since beta.24 (D63), the mode an NVIDIA App override set where the word says it applied; its `performanceMode` is in NGX's own numbering (Silent Hill 2, 2026-10-13: the App's Ultra Performance, profile value 5, reported as 3). *(Beta.27: never read after the game has gone — that reading, the last, had cleared `CREATED \| EVALUATE` on every stored session — and the two bits stand once a reading carried them: `03_METRICS` §The driver-reported rung.)* |
| **Reflex / PC latency** | `NvAPI_D3D_GetLatency` → per-frame sim/render/present/driver/GPU timestamps; hooking `NvAPI_D3D_SetSleepMode` / `SetLatencyMarker` in the Overlay tells us the game enabled Reflex (`17_HOOK_ENGINE`) |

Reflex latency is the single strongest reason L3 exists — it is genuinely unavailable anywhere else.

⚠ Any function not present in the public headers is an **optional extra**: feature-flagged, guarded, never load-bearing. If a call misbehaves twice, disable L3's affected field for the session rather than retrying in a loop.

## Vendor SDKs we deliberately do not use

Recorded so nobody re-adds them later without redoing the analysis.

**Intel Graphics Control Library (IGCL)** — *rejected, licence incompatible.* Distributed under the Intel Software License Agreement, not an open-source licence. Three clauses conflict with GPL-3.0 if the headers were vendored into this repository:

1. Use and redistribution are permitted **"solely for use on Intel platforms"** — a field-of-use restriction. GPL-3.0 §10 forbids imposing further restrictions on downstream recipients, and §7's list of permitted additional terms does not cover this.
2. A **broad indemnification** obligation covering any claims arising from use. GPL-3.0 §7(f) permits indemnity terms only in a narrow form tied to contractual assumptions of liability by the conveyor.
3. An **additional termination trigger** on breach, plus a no-reverse-engineering clause on binary components — both extend beyond GPL's own termination and study rights.

Writing our own IGCL declarations to avoid vendoring the headers is **not an approved workaround**: struct layouts must match byte-for-byte, so an "independent" implementation is indistinguishable from a copy, and the legal position is unsettled. Intel GPU telemetry comes from L1 + L2 instead.

**AMD ADLX / ADL** — *not used.* Not evaluated in depth because L2 already covers AMD sensors under MPL-2.0, so there is no need to take on another vendor licence. If someone later proposes adding it, it must pass the checklist below first, and the proposal must explain what it delivers that L2 does not.

**NVIDIA NGX / Streamline, AMD FidelityFX, Intel XeSS runtimes** — not loaded and not redistributed by us at all. We observe the calls the *game* makes to the copies it ships (`17_HOOK_ENGINE`). No vendor code is distributed by FrameLedger.

**AMD FidelityFX SDK headers** — *checklist run 2026-09-04, CLEAR; **vendored the same day at
tag `v2.3.0`**, five headers, types only (`src/native/third_party/fidelityfx/README.md`).*
Read off the upstream repository through the GitHub API, not from memory:

- **Tag `v1.1.4`** (the last 1.x): the root `LICENSE.txt` is the MIT grant verbatim, and every
  header of interest carries the same grant inline — the `ffx-api/include/ffx_api/` set and the
  FSR 3.0 host API under `sdk/include/FidelityFX/host/`. None of step 2's needles appear. **This
  paragraph said "vendor from this tag", and its own reversal condition fired the same day** —
  see the third bullet. ~~The host API is still only here: `main` dropped `ffx_fsr3.h`, so the FSR
  3.0 route (Cyberpunk's `ffx_fsr3_x64.dll`, named exports) vendors from `v1.1.4` when it is built,
  and its closure is **ten** files, not the nine first listed — `ffx_interface.h` includes
  `ffx_message.h` — with `<mutex>`/`<shared_mutex>` reached through `ffx_types.h`.~~ **Wrong tag,
  corrected 2026-09-05 when the route was built.** Cyberpunk's `ffx_fsr3_x64.dll` exports twelve
  `ffxFsr3*` names (`vendor-exports.json`), which is exactly tag **`fsr3-v3.0.4`**'s declaration
  list; `v1.1.4` declares fifteen and appends `upscaleSize`, `flags` and `frameID` to
  `FfxFsr3DispatchUpscaleDescription` *after* `renderSize` — a detour typed against it would read
  the wrong bytes off the module the title ships. At `fsr3-v3.0.4` the root `LICENSE.txt` is MIT
  verbatim, every header carries the grant inline, and **`ffx_types.h` includes `<stdint.h>` and
  nothing else** — the `<mutex>` block exists only from 1.1.x behind `#ifndef FFX_MUTEX`. The
  closure is ten files with **no `ffx_message.h`** (nine host headers plus
  `gpu/fsr3upscaler/ffx_fsr3upscaler_resources.h`). Vendored at
  `src/native/third_party/fidelityfx-fsr3/` in its own directory because the licence *shape*
  differs from `v2.3.0`'s exception list; `license-check.ps1` §2e asserts it per file.
- **`main` == tag `v2.3.0`** (`60f4ea81`, "AMD FSR SDK 2.3.0", 2026-06-24) is a different shape:
  its `docs/license.md` is a **binary-only, no-reverse-engineering** default licence that
  "applies to all files except as noted below", followed by an MIT exception list of 845 paths.
  **Read off the tree API: the repository at that commit holds exactly 845 blobs, and every one
  of them is on the list** — `Kits/FidelityFX/signedbin/*.dll` included — so the default licence
  governs no file that is actually in the tree, and the headers carry the grant inline as well.
  MIT by exception, for the whole tree. `license-check.ps1` §2d asserts it per vendored file
  rather than trusting this sentence: every path under `fidelityfx/Kits/` must be on the list
  inside the vendored `license.md`, every `.h` must carry the grant, and no binary or source may
  be present.
- **Why `v2.3.0` and not `v1.1.4`, decided by the owner 2026-09-04.** The reversal condition —
  *"unless a title ships an API version that needs 2.x"* — is met on this machine: Hell Is Us
  ships `amd_fidelityfx_upscaler_dx12.dll` 4.0.3 and `amd_fidelityfx_framegeneration_dx12.dll`
  4.0.0, Expedition 33 and Dying Light: The Beast ship upscaler 4.0.2 with frame generation 3.1.5
  (all SDK 2.x effect DLLs), and the SDK's own sample dispatches
  `ffxDispatchDescFrameGenerationPrepareV2` (`0x0002000c`), which `v1.1.4` does not declare.
  Every value and layout the Overlay reads is identical in both tags (`UPSCALE 0x00010001`,
  `FRAMEGENERATION 0x00020003`, `PREPARE 0x00020004`, `ffxApiHeader`, `FfxApiResource`,
  `ffxDispatchDescUpscale`, the `Prepare` prefix through `renderSize`), so the 1.1.x monolith
  decodes with the 2.3.0 constants. Vendored: `Kits/FidelityFX/api/include/{ffx_api.h,
  ffx_api_types.h}`, `upscalers/include/ffx_upscale.h`, `framegeneration/include/{ffx_framegeneration.h,
  ffx_framegeneration_api_types.h}`, in upstream's layout because they include each other by
  relative path. Excluded, each with a reason in the README: `signedbin/`, `ffx_api_loader.h`
  (a `GetProcAddress` helper), the two `dx12/` backend headers (no consumer yet), the `.hpp`
  helpers and every source file.
- Types-only, never linked, with a consumer in the same commit — the Streamline rules
  (`src/native/third_party/streamline/README.md`). **`hookinventory-check` Pass C already
  forbade `amd_fidelityfx*` / `ffx_*` imports** — the sentence here that said the list "must
  gain" them was written before checking, and the self-test at line 242 had covered them since
  2026-08-14 — and now reads the built Overlay's **export table** too, because `ffx_api.h`
  declares its entry points `__declspec(dllexport)` with no import switch and a definition
  inside the Overlay would announce a vendor API it does not implement.

**Intel XeSS SDK** — *checklist run 2026-09-04, REJECTED; the same class as NGX.* The repository
<https://github.com/intel/xess> ships `LICENSE.txt` = **Intel Simplified Software License
(October 2022)**: the grant is for the software *"provided in binary form only"*, it forbids
*"reverse engineering, decompilation, or disassembly … nor any modification"*, and it carries
its own **termination** clause — two of step 2's needles. The headers under `inc/` (`xess/xess.h`,
`xess/xess_d3d12.h`, `xess_fg/xefg_swapchain.h`, `xess_fg/xefg_swapchain_d3d12.h`, `xell/*.h`)
carry no MIT text; each opens *"Intel copyrighted materials … you may not use, modify, copy,
publish, distribute … without Intel's prior written permission."* Step 3 therefore binds:
**do not vendor, and do not re-declare the API.** What remains in policy for Intel is the
census: module presence by name, nothing more. **A signature-free call counter — a
register-preserving thunk on a named export that increments and jumps — was considered and
refused the same day (delegated by the owner, decided 2026-09-04).** It declares no type and
reads no argument, but installing it inline-patches Intel's code inside the game process, and
the same licence forbids *"any modification"*; the census is the answer NGX got and Intel gets
the same one. Reversal condition: Intel publishing the headers under a licence that clears the
checklist, at which point this paragraph is re-run, not argued around. **Re-run 2026-09-06 against
<https://raw.githubusercontent.com/intel/xess/main/LICENSE.txt> and `inc/xess/xess.h`: unchanged** —
the same licence, the same three needles, the same header banner. Not fired. Intel's README does
state that XeSS-FG runs on *"non-Intel GPUs with SM 6.4 support"*, so an XeFG session is measurable
on the NVIDIA dev box even though nothing in it may be hooked or declared.

### Checklist before adding any vendor SDK

1. Is it MIT / BSD / Apache-2.0 / MPL-2.0? → proceed, add the licence copy, done.
2. Otherwise, search the licence text for: **"solely for"**, **"indemnify"**, **"terminate"**, **"reverse engineer"**, **"Intel platforms"/"AMD products"**-style field-of-use wording.
3. Any hit ⇒ **do not vendor it into this repository**, and do not work around it by re-declaring the API. Find an alternative layer, or document the gap honestly.
4. Record the decision in this section either way — including rejections, with reasons.

## Runtime policy

- Poll at 1 Hz on a dedicated Agent thread. Never from the game process, never from the Overlay.
- Any layer that throws or hangs twice is disabled for the session and its fields report `N/A`, rather than being retried in a loop.
- **Read-only, always.** These libraries can also set clocks, fan curves and power limits. FrameLedger never calls a setter — not in v1, not in the backlog. A measurement tool that can modify hardware state is a different product with a different risk profile. *(Since beta.8 the same holds for the driver's settings store: the bridge reads DRS profiles and has no entry point that writes, saves or restores one — §L3, ABI 2.)*

## Capability matrix (fill during P0 on real hardware)

**Restructured to vendor × layer on 2026-08-05**, before anything was filled in, because
the note below said in this document's own words that the old single-axis table *"cannot
currently express"* the AMD/Intel deferral and that adding the axis is *"a prerequisite of
filling it, not a tidy-up afterwards"*. Filling the NVIDIA half into the old table would
have overwritten cells that were simultaneously making an unverified claim about hardware
nobody here has.

**Legend, and the distinction the restructure exists to preserve:**

| Mark | Meaning |
|---|---|
| ✓ / ✗ | **Measured** on that vendor, available / not available |
| `arch` | Not available **by architecture** — no measurement needed or possible |
| `?` | **Not yet measured, and measurable here.** A to-do |
| `untested` | **Cannot be measured on this machine.** Not a to-do — a deferral (§R5/§R6) |

`?` and `untested` are what the old table could not tell apart. The UI consults this before
advertising a capability, and "we have not got to it" and "we cannot, here" must not render
as the same thing.

### L1 — DXGI + PDH (vendor-neutral by construction)

| Field | NVIDIA | AMD | Intel |
|---|---|---|---|
| GPU utilisation | **deferred by decision** (§M10, 2026-09-09) — L2's vendor-reported load is what `LoadPct` carries, labelled as such; the engine counters are not read | untested | untested |
| Adapter VRAM | ✓ **measured 2026-09-09** — PDH `GPU Adapter Memory … Dedicated Usage`, 1301 MB at an idle desktop, RTX 5080 (`QueryVideoMemoryInfo` measured **not** adapter-wide: 0 bytes from the Agent) | untested | untested |
| Per-process VRAM | ~~`arch` for L1 — the Overlay's in-process `QueryVideoMemoryInfo` is the figure stored; `GPU Process Memory` is not read~~ ✓ **measured 2026-10-03** — `GPU Process Memory` Dedicated/Shared by pid from outside the game, +512.0 MiB for a 512 MiB buffer (`spike-notes` §16) | untested | untested |
| Driver version | ✓ **measured 2026-09-09** — `CheckInterfaceSupport(IDXGIDevice)` → `32.0.16.1664` | untested | untested |
| Core temp | ? (D3DKMT probe, Win 11 only — see below) | untested | untested |
| Power | ? (D3DKMT probe) | untested | untested |
| Fan | ? (D3DKMT probe) | untested | untested |
| Hotspot / memory temp, Clocks, Throttle, Reflex, CPU temp | `arch` | `arch` | `arch` |

L1 is vendor-neutral **by construction** — DXGI and PDH expose the same counters whatever
the adapter is — so the AMD/Intel `untested` cells are expected to match NVIDIA and are
still not claims. **One cell genuinely cannot be filled here even on NVIDIA:** multi-adapter
LUID instance filtering, because this machine has exactly one adapter and a `KF` CPU with no
iGPU. It must be written and can only be reasoned about.

### L2 — LibreHardwareMonitor

| Field | NVIDIA | AMD | Intel |
|---|---|---|---|
| GPU utilisation, Adapter VRAM | ✓ (`GPU Core` load; `GPU Memory Used`, MB) | untested | untested |
| Driver version | `arch` — LHM publishes no such sensor; L1 owns it | `arch` | `arch` |
| Core temp, memory temp | ✓ (`GPU Core`; `GPU Memory Junction`) | untested | untested |
| Hotspot temp | **✗ on this box** — no `GPU Hot Spot` sensor at all (RTX 5080, 616.56, LHM 0.9.6); driver, generation or library unmeasured | untested | untested |
| Clocks, Power, Fan | ✓ (`GPU Core` / `GPU Memory` MHz; `GPU Package` W; `GPU Fan 1..3` rpm) | untested | untested |
| Per-process VRAM | `arch` | `arch` | `arch` |
| Throttle reasons, Reflex / PC latency | `arch` | `arch` | `arch` |
| CPU temp | ✓ elevated + PawnIO | ✓ elevated + PawnIO | ✓ elevated + PawnIO |
| **GPU sensors unelevated, no PawnIO (§M5)** | **✓ — row R1, 2026-09-03.** Unelevated and elevated return the identical eight fields; PawnIO never opened (GPU group only) | untested | untested |

§M5 is the row that decides whether the **default, unelevated** Agent has temperatures at
all. **Measured 2026-09-03 — yes, on NVIDIA.** The instrument is
`FrameLedger.CaptureHost probe-lhm`, the raw tree and the elevated control are in
`spike-notes` §10, and the decision table it was read against was pre-committed in
`20_OPEN_QUESTIONS` §M5 before the run. The NVIDIA column above is that one machine's
answer; the `untested` cells are exactly as untested as before.

### L3 — NVAPI (NVIDIA only, `arch` elsewhere)

| Field | NVIDIA | AMD | Intel |
|---|---|---|---|
| **Initialises at all** | ✓ **measured 2026-08-05** | `arch` | `arch` |
| **Driver version** | ✓ **measured** — `610.88`, branch `r610_85`; **through the bridge 2026-09-10:** `616.64`, `r616_41` | `arch` | `arch` |
| **GPU enumeration + name** | ✓ **measured** — 1 GPU, "NVIDIA GeForce RTX 5080" | `arch` | `arch` |
| **Degrades cleanly when absent** | ✓ **measured** — see below | `arch` | `arch` |
| Core / hotspot / memory temp | **core ✓ measured 2026-09-10** — 35 °C at an idle desktop; **memory: bit clear** on this GPU (no `MEMORY` thermal target enumerated → `null`, not 0); hotspot not read | `arch` | `arch` |
| Per-domain utilisation | **GPU domain ✓ measured 2026-09-10** — 0 % at an idle desktop, present bit set (a measured zero); FB / VID / BUS not read | `arch` | `arch` |
| Clocks, Power | **clocks ✓ measured 2026-09-10** — 2670 MHz graphics, 15001 MHz memory; power not read | `arch` | `arch` |
| Throttle reasons | ✓ **measured 2026-09-10** — mask 0 at an idle desktop, present bit set | `arch` | `arch` |
| Fan | **bit clear at an idle desktop 2026-09-10** — the tach reading is refused while the fans are stopped, and the bridge reports that as absent rather than 0 RPM; a reading under load is the owner's to take | `arch` | `arch` |
| PCIe width | ✓ **measured 2026-09-10** — ×16; generation not read | `arch` | `arch` |
| Reflex / PC latency | not a poller field (§M8) | `arch` | `arch` |

The four measured rows come from `ctest fl_nvapi_probe` (`src/native/tools/fl-probe-nvapi`),
which exists because vendoring added 2.4 MB of headers and an import library that **nothing
else compiles against yet** — an unconsumed vendored dependency is one whose header closure
could be short by a file with every gate still green. **The 2026-09-10 rows** come from one
`FlNvReadSample` through `FrameLedger.NvapiBridge.dll` on the dev box (RTX 5080, driver 616.64),
read from the staged DLL by hand after `NvapiBridgeMirrorTests` had passed against it; the
bridge is the second consumer of the vendored material and the first that ships.

**"Degrades cleanly when absent" — what is observed, and what is inferred.** `nvapi64.lib`
is a *static* library of stubs that reach `nvapi64.dll` through `nvapi_QueryInterface` at
first call, so linking it does **not** make the DLL a load-time dependency: a machine with
no NVIDIA driver loads the binary and `NvAPI_Initialize` returns an error instead. The probe
prints **`BRANCH: AVAILABLE`** or **`BRANCH: DEGRADED`** and exits 0 either way, and ctest
requires one of those two strings — so a probe gutted to `return 0` fails, which exit code
alone could never catch.

> **Stated precisely because the obvious sentence would be an overclaim.** *Observed on CI:*
> the probe builds, links and passes on a hosted runner in ~0.01 s, against ~1.4 s on the dev
> box. *Inferred, not observed:* that the runner took the DEGRADED branch — hosted Windows
> runners have no NVIDIA driver and the timing matches an immediate `NvAPI_Initialize`
> failure, but **ctest prints a passing test's output nowhere**, so the CI log shows that a
> branch was reached and not which one. The load-time-dependency claim is what the run does
> prove: a load-time dependency on an absent `nvapi64.dll` would have failed to start at all.

> ⚠ **`NvAPI_GPU_GetMemoryInfo` was named in §L3's function table below and must not be
> used.** The vendored headers carry
> `__nvapi_deprecated_function("...deprecated in release 520. Instead, use
> NvAPI_GPU_GetMemoryInfoEx.")` on it, so under `/W4 /WX` a call fails the native build.
> Corrected in the table. Recorded because it is the class `17_HOOK_ENGINE` calls the
> highest false-confidence risk in the spike — a name that resolves to nothing degrades to
> "unknown", which reads as "working, no data" — found by vendoring the material and
> compiling against it rather than by reading a vendor's web documentation.

> **🅓 The AMD and Intel results are deferred as `untested` — owner decision,
> 2026-08-05** (`20_OPEN_QUESTIONS` §R5/§R6). Not deferred for cost: the dev machine
> has one adapter, an RTX 5080, and a `KF` CPU with no iGPU (`spike-notes.md`
> §Environment), so there is no measurement to take. **AFMF is unreachable for the
> same reason** — it is an AMD driver-side feature.
>
> The rule that travels with the deferral: an unmeasured cell is **`untested`**, never
> `?` and never a checkmark inferred from a vendor's documentation. `?` reads as "we
> have not got to it yet"; `untested` reads as "we cannot, here" — and this table is
> what the UI consults before advertising a capability to a user.
>
> ~~**This table cannot currently express that.** Its columns are the three layers, with
> no vendor axis, so "measured on NVIDIA, untested on AMD/Intel" has nowhere to go —
> and every `?` below is therefore ambiguous between the two meanings. **Adding the
> vendor axis is a prerequisite of filling it**, not a tidy-up afterwards, because a
> single-axis table forces whoever fills the NVIDIA half to overwrite cells that are
> also making a claim about hardware they never had. P0 item 8 owns this.~~
>
> > **Done 2026-08-05, before anything was filled in**, which was the point of calling it
> > a prerequisite. Three tables, one per layer, with a vendor axis and a four-symbol
> > legend that separates `?` (a to-do, measurable here) from `untested` (a deferral,
> > not measurable here) from `arch` (not available by architecture, so neither).
>
> The NVIDIA half is **not** deferred and ~~none of it exists in code yet — no PDH, no
> DXGI telemetry, no LibreHardwareMonitor, no NVAPI~~ — **L2 exists as of 2026-09-03**
> (`LhmTelemetrySource`, `SensorMap`, the port in `FrameLedger.Application.Telemetry`);
> ~~L1 and L3 are still unwritten~~ **L1 and the composite exist as of 2026-09-09**
> (`BaselineTelemetrySource`, `DxgiAdapters`, `PdhAdapterMemoryCounter`,
> `CompositeTelemetrySource`, `TelemetryPoller`); ~~L3 is P2's PR-E2~~ **L3 exists as of 2026-09-10**
> (`NvapiTelemetrySource` over `FrameLedger.NvapiBridge.dll`, §L3). §M5 in particular (do LHM GPU
> sensors work unelevated, without PawnIO?) decided whether the default unelevated
> Agent has temperatures at all — **it does, on NVIDIA; row R1 above.**

## Advanced metrics (owner decisions D101–D108, 2026-11-11)

The owner asked for a setting, *"Thông số nâng cao"* (Advanced metrics), with a navigation item of its own, that shows
as much as the Streamline and NVAPI headers and PawnIO make available — numbers, tables, charts — **with each value
assigned to what it belongs to: one game session, or the PC**. Asked while planning, the owner chose: everything
that can be reached inside a game as well, in stages, the route refused on 2026-08-09 re-examined; stored in full with
the session; off by default, and off means nothing more is read; and the page never asks for administrator rights
itself. This section is the design those answers gave, the decisions taken with it, and the inventory.

**Nothing here is built by the pull request that wrote it** except the catalog
(`FrameLedger.Application.Advanced.AdvancedMetricCatalog`) and this section. Each later pull request of the train
states here what it built.

### The decisions

- **D101 — the option.** `telemetry.advanced`, off by default, read by the Agent at each session's start. **Off means
  not read**: no additional call to NVAPI or LibreHardwareMonitor, nothing more stored, and the navigation item is
  hidden. A session recorded while it was off shows N/A for everything here — it is not filled in afterwards.
- **D102 — where it is stored.** In two tables of their own (schema 0023), not in new columns of `sessions`: every
  read of a `sessions` row, the JSON export and the bug bundle carry the whole row, and a driver profile does not
  belong in a bug bundle. `machine_profiles` holds the facts of the PC, one row per distinct set, and a session points
  to the row that was true when it ran — the trend's hardware-change markers and `hardware_snapshots` are untouched.
  `session_advanced` holds one session's facts, its series' statistics and their directory. The series themselves go
  to `sensor_blobs` under names that start with `adv.`, **with NaN for a tick that has no reading**: the fixed series
  store −1 there and their readers drop every negative value, which would silently eat a −12 V rail.
- **D103 — LibreHardwareMonitor's other groups.** CPU, memory and motherboard (its sensor chip and embedded
  controller) are opened **only when the Agent already runs as administrator and PawnIO is installed** — the existing
  admin mode (D34); the page says what is missing and links to that setting. **Refused**: the power-supply and
  fan-controller groups, which make the library search every USB HID device. **Deferred**: the battery group — and
  **storage**. The plan had storage in, temperature only and internal drives only, with one outcome written down
  before any run: *if the group cannot be opened without touching removable drives, it stays closed*. It cannot: the
  library's `StorageGroup` (0.9.6, read 2026-11-11) lists every drive through DiskInfoToolkit's `ReloadStorages` with
  no filter by bus, and subscribes to drives being plugged in afterwards. So `lhm.storage.*` is `Deferred`, a drive's
  temperature reads N/A, and `LhmGroups` — the only way this code can name a group to open — has no member for it
  (`20_OPEN_QUESTIONS` §M13). This amends §L2's "nothing else, ever" from the pull request that opened the three.
- **D104 — identifiers are never read.** The card's board serial number and unique id, a display's EDID, and the list
  of programs that are encoding. They are named in the inventory so the page can say so; a test holds that no stored
  JSON has their keys. This is the only part of "everything" left out on purpose.
- **D105 — the navigation item** is the last of the pane's items; Ctrl+1 to Ctrl+5 keep their pages and **Ctrl+6**
  opens it only while the option is on.
- **D106 — the `.partial` file skips what it does not know.** A chunk of an unknown type whose checksum is right is
  passed over; until now it ended the read, so a build one release older lost everything after the first new chunk.
- **D107 — with no game running**, the Agent reads sensors only while the page is open, through one reader shared
  with the sessions, closed again some seconds after the last use. A PC idling with FrameLedger in the tray reads
  nothing (NFR-2's "~0% idle").
- **D108 — read-only, by a gate.** §Runtime policy's "never call a setter" becomes a build check: every `NvAPI_*`
  call in the bridge's source is compared with a committed list, in both directions, and a name that changes
  something or returns an identifier is refused whatever the list says. An entry point that both reads and writes by
  a command argument — the colour and info-frame controls — is refused like a writer: the check reads names, not
  arguments, and nothing on the list is one. *(Built with ABI 3: `tools/nvapi-readonly-check.ps1`.)*

### The reader (built 2026-11-11; composed by the Agent since the switch)

`Application.Advanced.IAdvancedHardware` is the port: `Acquire()` keeps the reader running until the handle is let
go, `Latest` is the last tick without waiting, `ReadSessionFacts` reads one session's facts on the caller's thread.
`Infrastructure.Telemetry.AdvancedHardwareHub` implements it — **one reader for the whole Agent, on a thread of its
own (`fl-advanced`), running only while somebody holds it** and for a linger after, so that two sessions in a row,
or a session ending as the page opens, do not close and reopen the sensors (D107). The Agent records several
sessions at once, each with a poller; a reader per session would open the motherboard's sensor chip as many times.

What runs on that thread is `AdvancedHardwareReader`: NVAPI through `NvapiAdvancedReader`, and
`LhmAdvancedReader` over a `Computer` of its own, opened with `LhmAdvancedReader.GroupsFor(elevated, pawnIo)` —
`LhmGroups`, an enum that has a member for the four groups this project may open and for no other. Each sensor is a
member of a catalog family by the device at the top of its tree and its type (`LhmAdvancedMap.FamilyOf`); its series
is named from the library's own identifier (`/lpc/nct6798d/0/voltage/3` → `adv.lhm.board.voltage.lpc_nct6798d_0_3`)
and labelled with the hardware's words. A sensor with no family is not read.

- **A source that is not there is a state** (`AdvancedSources`): no NVIDIA driver, not administrator, PawnIO
  missing. Never an exception, and never a value in its place.
- **A device that throws twice is left alone** until the reader opens again; the others go on. The library's own
  traversal stops at the first exception, so each top-level device is refreshed in a guard of its own.
- **NVAPI answering with no field, twice running, after it had answered**: released and initialised again, once
  (`20_OPEN_QUESTIONS` §M12).
- **The cadence was set by a measurement, before anything composed the reader.** `FrameLedger.CaptureHost
  probe-advanced` runs this reader on the caller's own rights and prints what it read and what it cost. On the dev
  box (RTX 5080, unelevated, 2026-11-11): 35 facts and 58 readings a tick — 17 from NVAPI, 41 from the card's sensors;
  **one NVAPI live read costs 0.4 ms of processor time; one refresh of LibreHardwareMonitor's graphics-card group
  costs 14 ms (62 ms elapsed)**. Once a second that refresh alone is 1.4% of one core, above NFR-2's 1% for the
  whole Agent. So NVAPI is read every tick and **the sensor library every fifth** (0.28%), and on the ticks between a
  sensor has no reading — its last value is not repeated. The same refresh is what the L2 telemetry layer does once
  a second in every session: that cost is not this feature's, and is `20_OPEN_QUESTIONS` §M16 — measured in a real
  Agent on 2026-11-11 (1.93 % of one core off, 2.04 % on) and answered by amending NFR-2 to 2.5 % (D117).

### From the reader to the ledger (built 2026-11-11; fed since the switch)

- **The poller attaches, it does not read.** `TelemetryPoller` takes an optional `IAdvancedHardware`, holds it from
  `Start` to `Dispose`, and at each poll takes `Latest` — a read of one reference. A snapshot it has not taken yet
  rides on that poll's `TelemetrySample.Advanced`; one it has already taken is not attached again, because a
  reading is of one tick. Null — the option off — composes nothing, and a sample is what it always was.
- **The `.partial`** carries those readings in a chunk of their own after the sensor chunk (type 9) and the
  session's context in another (type 10), so a recovered session keeps both; with the option off neither is
  written. An unknown chunk type is now skipped rather than the end of the file (D106). `06_DATA_MODEL` §The
  `.partial` file.
- **The finalizer** (`AdvancedSessionBuilder`, both tiers) turns the samples' readings into one series each,
  aligned to `t_ms` with NaN for a tick with none, and builds the session's row: facts, sources, statistics,
  directory. Schema 0023 and the rule for NaN are `06_DATA_MODEL` §Blob encoding and §Migrations.

### The switch (built 2026-11-11, Advanced PR-5)

`telemetry.advanced` (`SettingsRegistry.TelemetryAdvanced`, Boolean, default `0`, read by the Agent) becomes
`RecorderOptions.AdvancedMetrics` through `SettingsRecorderPolicy`, at each session's start like the other recording
settings; exactly `1` is on. From there:

- **The process has one sensor library** *(2026-11-12)*. One *reader* was never the whole of it: a session's L2
  source and its CPU-temperature reader each open a LibreHardwareMonitor `Computer` of their own beside the
  Advanced reader's, and the library keeps part of its state — the driver it reads the processor through, its bus
  mutexes — per process. `Computer.Close()` lets go of that part whoever else is still reading. The first sessions
  recorded as administrator with PawnIO showed it (`20_OPEN_QUESTIONS` §M13): a processor device that faulted out
  some seconds after each session's sources closed, and one session whose reader could not open at all. Every
  `Computer` is now opened and closed through `Infrastructure.Telemetry.LhmLibrary` (`LhmComputerAdapter` is its
  user): opens and closes never run at once, and a `Computer` closed while another is in use is **parked** — left
  open, read by nobody, handed to the next user of the same groups — until the last user lets go, when all are
  closed. The reads are not serialised: the readers keep their own threads, so a sensor that hangs in one still
  holds up neither the other nor the pipe.
- **The Agent has one reader** — `AgentServices` registers `AdvancedHardwareHub` as `IAdvancedHardware`, a tick a
  second and a linger of ten — and nothing starts it but a holder. `SessionRecorder` holds it for a session's length
  **only when the option is on**, and `AgentRecording` gives the session's `TelemetryPoller` the reader on the same
  condition. Off: not held, not asked, no chunk 9 or 10, no row (`SessionRecorderTests.WithAdvancedMetricsOff…`).
- **A session's facts are read once, off the capture loop.** Fifteen seconds after the game's process is known — by
  then it has a window — or at the session's end if that comes first, `IAdvancedHardware.ReadSessionFacts` runs on a
  pool thread: asking the driver for a profile takes some hundred milliseconds. The finalize waits at most five
  seconds for a read still in flight and stores the session without its facts rather than hold it. A read that
  throws costs the facts, never the session.
- **Which display is the game's** comes from Windows, not from the game: `WindowGeometryProbe.MonitorDeviceOf` — the
  monitor the process's largest visible window is on, as its GDI device name — which the reader hands to
  `FlNvSessionDisplay`. No window, or a display another adapter drives: the session's display facts are absent.
- **The context reaches the crash file as it changes** (chunk 10), from `IAdvancedHardware.Latest` — a read of one
  reference on the capture loop's task — so a recovered session keeps its machine facts, its sources and its
  directory of series.
- **A session recorded with the option on always stores a row**, if only one that says every source is `Unknown`:
  no row means "the option was off when this was recorded", which the page (PR-7) shows as such.

What elevation adds changed with this: an Agent running as administrator with PawnIO reads the processor, memory
and motherboard groups in a session with the option on. The texts that state what the admin mode adds say so —
Disclaimer 2.15 §5 and §5A (the owner's answer of 2026-11-11), the admin mode's disclosure (`agent-admin-dialog/2`),
Settings, the first run, the guide and `LIMITATIONS.md` — and Privacy Policy 2.15 names what is read and stored.

### The page's feed (built 2026-11-11, Advanced PR-6; shown by the page since PR-7)

With no game running the Agent reads sensors **only while the page is open** (D107), and nothing tells the Agent
that a page opened or closed:

- **`GetAdvancedMachine`** (`07_IPC` §Messages) is answered by `Application.Ipc.AdvancedMachineResponder` from
  `IAdvancedHardware.Latest` alone. The first request acquires the shared reader and each one renews a five-second
  lease; a lease after the last request the hold is let go, and the reader closes its sources ten seconds later
  unless a session holds it. A page that closes, an App that exits and a pipe that drops all look the same: no
  request. With the option off the answer is `Error AdvancedOff`, and nothing is held.
- **`App.Services.AdvancedMachineFeed`** asks once a second while a page holds `Watch()`, and keeps the facts (asked
  for once, by their hash), **the last value of every series with the tick it was read on** — a sensor read every
  fifth tick is "1400, as of then", never shown as new — five minutes of each series for the live charts, and the
  sources' states. It stores nothing. Its states are the page's own words for why there is nothing to show: waiting
  for the first read, off, no agent (a viewer never has one), an older agent.

### The page (built 2026-11-11, Advanced PR-7)

`08_UI` §Advanced is the screen. What this document's model becomes there:

- **A machine fact** is under *This PC* ▸ *About this PC* while the Agent answers, and under *Session* as it was
  when that session was recorded (`machine_profiles`, one row per distinct set).
- **A session fact** is under *Session* only.
- **A series** is a row of *Right now* and a line of *Over time* while live — never stored then — and a row of the
  session's statistics and a line of its chart once a session stored it.
- **An entry that is not read** is on *Sources* with its reason, in the same words as §Left out, by name.

`ISessionRepository.ListAdvancedAsync` lists the sessions that have a row; `SessionSeriesLoader.LoadAdvancedAsync`
decodes their series against `t_ms`, leaving a NaN tick out and keeping a negative reading.

### What belongs to what

| Scope | What it is | Stored | Shown |
|---|---|---|---|
| `MachineFact` | true of the PC whatever is running: the card's firmware and architecture, the driver's package, what a display can do | `machine_profiles`, once per distinct set; a session points to its row | This PC |
| `SessionFact` | true of one session: the game's display state, the settings the driver's profile for that game sets | `session_advanced.facts_json` | Session |
| `Series`, during a session | a sensor of the PC sampled while the game ran | `sensor_blobs` (`adv.*`), statistics in `session_advanced.stats_json` | Session |
| `Series`, with no session | the same sensor, read because the page is open | nowhere | This PC |

A third tab, **Sources**, lists every row of the inventory with its state on this PC — read, or N/A with the reason:
not an NVIDIA card, the Agent is not administrator, PawnIO is not installed, the option was off when the session was
recorded, the hook is not built, the route is refused, a vendor's licence. A PC without an NVIDIA card shows N/A for
every `nvapi.` row; nothing is estimated in its place (FR-4.9).

### The trains

| Train | Release | What | Its gate |
|---|---|---|---|
| A | 0.1.0-beta.39 | read by the Agent only — NVAPI and LibreHardwareMonitor — storage, the page. **Nothing changes in code loaded into a game** | this section |
| B | 0.1.0-beta.40, ~~.41, .42~~ ~~.42, .43~~ .42, .43, .44 *(three releases since D113, 2026-11-11; one was planned. Renumbered 2026-11-12, D118: 0.1.0-beta.41 is a fix release for the sensor library, not part of the train. **Four since D122, the same day**: the second step is beta.42 — no new patch site — and beta.43, the two new hooks; the third step is beta.44)* | inside the game: more fields of the Streamline and FidelityFX calls already hooked, and new hooks on functions `sl.interposer.dll` **exports** (`17_HOOK_ENGINE` §Hook inventory's ⏳ rows); shared-memory layout 5 — §Train B below | a plan of its own; overhead measured; each release after the first waits for the owner's run of the one before |
| C | ~~0.1.0-beta.41~~ ~~0.1.0-beta.43~~ ~~0.1.0-beta.44~~ 0.1.0-beta.45 *(D118, D122)* | Reflex latency per frame | a spike, then the owner's go or no |
| D | ~~0.1.0-beta.42~~ ~~0.1.0-beta.44~~ ~~0.1.0-beta.45~~ 0.1.0-beta.46 *(D118, D122)* | the Streamline feature state behind `slGetFeatureFunction` — DLSS frame generation's, DLSS's, Reflex's | a spike, then the owner's go or no |

Rules 4 and 5, the hook inventory, the measured overhead and the anti-cheat guard bind B, C and D exactly as they bind
every hook. `sl::Constants` is read without its camera position, axes and matrices: where the camera is, is where the
player is (rule 4). NGX stays closed by its licence (`17_HOOK_ENGINE` §NGX).

**Train D re-examines a refusal; it does not lift it.** `handoff/history/p0-queue-1-6.md` refused, on five grounds,
hooking `slGetFeatureFunction` and patching the function it returns. The route to try is a different one:
`slGetFeatureFunction` is itself an export (`vendor-exports.json`), so its hook is an ordinary inventory row, and
what it hands back could be a wrapper for a fixed list of names instead of a patch at an address learned at run time
— which would answer grounds 1 to 4, and turn ground 5 into "measured only when injected at launch". Two things it
raises are the owner's: replacing the pointer the game receives is a **write**, which rule 4 forbids as written; and
a wrapper must outlive the Overlay's own unhook. If the spike cannot answer all of it, the route stays refused and
the page says so.

### Train B (planned 2026-11-11, owner decisions D110–D116)

What the catalog marks `HookedCall` and `NewHook`. Planned the day train A shipped, the owner's decisions asked while
planning, the design reviewed against the code before it was approved.

- **D111 — off means off inside the game too.** The Agent writes one word of the control block
  (`advancedRequested`, ~~0 or 1~~ 0 or that session's request, unconditionally at attach) when the session records Advanced metrics. The Overlay
  reads a new field, and installs a new hook, only while it is set; with the option off it hooks and reads exactly
  what 0.1.0-beta.39's does.
  **Built 2026-11-12 (0.1.0-beta.40, PR-3), with one thing the plan did not have:** "on" is not a 1 but a
  *request* — a value no session of that ring used before. A game outlives a session, and the Overlay's summaries
  with it; a second session of a game that kept running would have read the first one's field of view and counts.
  The watchdog starts its summaries afresh at a request it has not seen and names the request in every publication;
  the Agent reads only what answers its own (`07_IPC` §Region 5). The detours consult one process-local flag, the
  watchdog is the region's one writer, and `fl_guard` shows the off half on a target that sends everything: every
  byte of the region zero until asked, untouched after an "off", a count that starts again at the next request.
  **The Agent and the App, 2026-11-12 (PR-4).** A session that records Advanced metrics says so through
  `ICaptureObserver.WantsInGameAdvanced`; `CaptureSession` writes the word either way as the ring is attached, before
  its first guard result, and from then on carries each publication of region 5 on `CaptureProgress.InGameAdvanced`
  — one instance a publication, the same one on the ten ticks between two. `Application.Capture.InGameAdvancedSampler`
  turns a publication into what a session stores: the session facts of the thirteen `Game` entries below (nineteen since 0.1.0-beta.43), in the
  vendor's own words (a preset is its letter, a member its name), and two series —
  `adv.ffx.upscale.frame_time_delta`, the mean of one second's dispatches, and `adv.ffx.memory_usage`, a point for
  each second the game asked. The facts are one list until a field they are made of moves, so the recorder's context
  is rewritten when a range widens and not once a second; the readings ride the newest telemetry sample of the tick.
  **No schema change**: the facts go into `session_advanced.facts_json` beside the driver's, the series into
  `sensor_blobs` under their `adv.` names, the `.partial`'s chunks 9 and 10 carry both. `AdvancedSources.InGame`
  (optional; absent in every row 0.1.0-beta.39 wrote) says what became of those entries in the session: `Reading`,
  `NothingSeen` — measured inside the game, and the game made none of the calls where they can be seen — or
  `NotHooked`. The page says it in the session's sources line and, on Sources, that such an entry is read inside a
  game FrameLedger measures when the game makes the call. The aliasable part of FSR's memory is published and not
  shown: the catalog has one memory entry.
  **The second step, 2026-11-12 (0.1.0-beta.43, PR-4).** Six more entries have reach `Game`: `sl.constants.jitter`,
  `.motion_vectors`, `.depth`, `.projection`, `.resets` and `ffx.framegen.config`, from the two calls hooked only
  for Advanced metrics (`17_HOOK_ENGINE` §Hook inventory, `slSetConstants` and `ffxConfigure`). The sampler makes
  them from members region 5 already had (layout 5 as it was; schema 23 as it was): a switch is `on`, `off` or
  `on_and_off` from the two words of what was seen, the flags are AMD's own names, the rectangle is the last one
  the game gave and is left out where the capture side could not state it, the field of view is a range (D114).
  **A field of view over π is said as given (owner decision D128, 0.1.0-beta.44).** Both vendors' headers say
  radians; Cyberpunk 2077 gives Streamline 51–90, and the row read `51.0055 – 90 rad (2922.4° – 5156.6°)`. Such a
  value is now the number alone, with a second row (`cameraFOV unit`, token `not_radians`) that says what it is
  not; it is never converted, which would store this project's guess at the unit as the game's word. A
  frame-generation configure that set no flag has a `flags` row of `none`, as an upscale does.
  **A count is not part of what makes the facts new** — `sl.constants.resets` grows all session, and a context
  rewritten on each would be one a second — so `InGameAdvancedSampler.Conclude` brings it up to the last
  publication once, when the session ends. `AdvancedSources.StreamlineConstants` and `.FsrFrameGeneration`
  (`AdvancedInGameCall`; null in every row written before, and in a session not measured inside the game) say for
  each call which of three things happened: `Read`; `NeverCalled` — the capture side published the hook's family
  (`FlHookFamily.SlConstants`, `.FfxConfigure` in the writer state's mask) and no publication carried the group; or
  `NotHooked` — the family was never published, which is a game that did not load the library, a Streamline 1.x
  interposer, or a patch that was refused. The page says it in one more sentence of the session's sources line.
- **D113 — three releases, because a new patch site cannot be proven in CI.** A crash in a game under the user-mode
  anti-cheat exception's trial ends that exception for good (D38), and every release already means restarting the
  game. **beta.40**: more fields of calls *already* hooked — `sl::DLSSOptions` where the game chains it into
  `slEvaluateFeature`, the FSR upscale dispatch, the FSR memory query — shared-memory layout 5, the Agent and the
  page; **no new patch site**. **~~beta.41~~ beta.42**: `slSetConstants` and `ffxConfigure`. **~~beta.42~~
  beta.43**: `slGetFeatureVersion` and `slGetFeatureRequirements` — the owner's words: a release of their own, still
  hooked. Each starts when the owner says the one before passed its owner-only item.
- **D122 — the second step is two releases** (owner decision 2026-11-12, after the owner's sessions on beta.40
  and beta.41 and a review of the draft against the code). **0.1.0-beta.42 adds no patch site**: the session's
  upscaler label (D120, `03_METRICS` §Upscaling), a patch registry that refuses what it cannot register, a native
  log that survives the game's exit and says which descriptor types each ffx module saw — because Cyberpunk 2077,
  set to FSR upscaling, was named "unknown" and nothing in the field could say why (D121) — and FSR's infinite depth
  plane (D119). **0.1.0-beta.43 is `slSetConstants` and `ffxConfigure`**, begun only when that log was read. The
  third step (`slGetFeatureVersion`, `slGetFeatureRequirements`) is beta.44, train C beta.45, train D beta.46.
- **D118 — 0.1.0-beta.41 is a fix release, and the train's numbers move by one** (owner decision 2026-11-12). The
  owner's first evening with beta.40 showed the sensor library being closed under its other users (the paragraph on
  `LhmLibrary` below; `20_OPEN_QUESTIONS` §M13); told that the fix would take the number D113 had given the train's
  second release, the owner chose to release it at once. So: second release beta.42, third beta.43, train C
  beta.44, train D beta.45. Text written before that day says beta.41 and beta.42 for the second and third — in
  the released changelogs and the moved HANDOFF sections, which are not edited, and in the comments of `fl_shm.h`
  and `ShmLayout.cs`, which the release that fills each member rewrites (they were left so that the code loaded
  into a game stayed 0.1.0-beta.40's, line for line, in a release that fixes something else).
- **D112 — `slInit` is not hooked in train B.** A game calls it before FrameLedger attaches, so its preferences
  would almost never be seen. If it is ever hooked: `applicationId`, `projectId` and the paths are never read.
- **D114 — the field of view is a session's range**, lowest to highest, never a series: a series would say when the
  player aimed.
- **D115 — a tail member is read only where it is proven to be there** (`17_HOOK_ENGINE` §Which struct version
  carries which field): the table from the upstream tags, not the one header in the tree.
- **D116 — "never the camera" is a build check**, as "never a setter" is (D108): `tools/overlay-fields-check.ps1` —
  every read of a vendor struct in one header, each allowed member named and used, the camera's names refused
  anywhere in the Overlay and the Vulkan layer, no copy of `sl::Constants` by value. *(Built 2026-11-12:
  twenty-eight members on `tools/overlay-allowed-fields.txt`; `12_BUILD` gate 14f. Fifty-six since 0.1.0-beta.44,
  with the two answers about a feature.)*
- **What will read N/A, and says why.** Many titles set their DLSS options through `slDLSSSetOptions`, which is not
  an export (train D), so `sl.dlss.*` is filled only for a title that passes its options with the evaluate call.
  `slGetFeatureVersion` and `slGetFeatureRequirements` are calls a game makes while starting.
- **Privacy Policy 2.17** at beta.43 (approved with that release's plan): 2.16 listed DLSS's and FSR's upscaling
  parameters by name, and Streamline's per-frame constants and the frame-generation configuration are in neither
  list, so they are named — and §4 says the two calls are intercepted only once a session with the option on has
  started in the game.
- **Privacy Policy 2.16** at beta.40, once for the three releases: with Advanced metrics on, for a game FrameLedger
  injects into, the parameters the game gives its upscaler and frame generation through the intercepted calls are
  *stored* — sizes, presets, exposure, sharpening, the jitter's, the depth's and the field of view's ranges, flags,
  versions — and never the camera's position, axes or matrices.

### Left out, by name

Present in the vendored `nvapi.h`, readable from the Agent, and not in the inventory — each is either not a
measurement of how a game runs or exists on professional cards only: ECC status and errors, NVLINK, illumination
(the card's lighting zones), licensable features, virtualization and GSP information, Quadro status, VR-ready data,
the IRQ and bus slot numbers, logical GPUs and SLI state, HDCP support, scan-out configuration, the colour and
info-frame controls. `NvAPI_DISP_GetTiming` calculates a timing from an input and reads nothing.
`NV_GET_ADAPTIVE_SYNC_DATA`'s last-flip count and timestamp mean nothing sampled once a second.
`NvAPI_GPU_ClientRegisterForUtilizationSampleUpdates` registers a callback, which is not a read.

### The inventory

One row per entry of `AdvancedMetricCatalog`, in its order; `AdvancedMetricCatalogTests` fails when this table and
the catalog differ in either direction. **Reach** is where a value can be read from, or why it is not read: `Agent`
(the Agent's own process), `AgentElevated` (the same, as administrator with PawnIO), `Game` (read inside a game
FrameLedger measures, from the arguments of a call the Overlay hooks, while the session records Advanced metrics —
since 0.1.0-beta.40), `HookedCall` (an argument of a call the Overlay already hooks, not read yet), `NewHook` (a hook
that is not built — Train B), `Spike` (Trains C and D), `Refused`, `Deferred`, `Identifier` (D104), `PlayerState`
(rule 4), `Licence`, `NotInHeaders`. ~~Only the first two are read by a build of Train A.~~ The first three are read
(`AdvancedMetric.IsRead`).

**NVAPI, the graphics card: facts**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.gpu.type` | Integrated or discrete graphics | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.system_type` | Laptop or desktop, as the driver sees the PC | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.external` | The card is an external one | MachineFact | Nvapi | Agent | Flag |
| `nvapi.gpu.bus_type` | The bus the card sits on | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.pci_ids` | PCI device, subsystem and revision ids - the model, not the unit | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.vbios_version` | Video BIOS version | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.architecture` | Chip architecture, implementation and revision | MachineFact | Nvapi | Agent | Text |
| `nvapi.gpu.core_count` | Graphics cores | MachineFact | Nvapi | Agent | Count |
| `nvapi.gpu.rt_cores` | Ray-tracing cores | MachineFact | Nvapi | Agent | Count |
| `nvapi.gpu.tensor_cores` | Tensor cores | MachineFact | Nvapi | Agent | Count |
| `nvapi.gpu.ram_bus_width` | Width of the card's memory bus | MachineFact | Nvapi | Agent | Bits |
| `nvapi.gpu.clock_base_core` | Base core clock | MachineFact | Nvapi | Agent | Megahertz |
| `nvapi.gpu.clock_base_memory` | Base memory clock | MachineFact | Nvapi | Agent | Megahertz |
| `nvapi.gpu.clock_boost_core` | Boost core clock | MachineFact | Nvapi | Agent | Megahertz |
| `nvapi.gpu.clock_boost_memory` | Boost memory clock | MachineFact | Nvapi | Agent | Megahertz |
| `nvapi.gpu.temp_default_max` | The core sensor's default maximum temperature | MachineFact | Nvapi | Agent | Celsius |

**NVAPI, the card's memory**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.vram.dedicated` | Dedicated video memory on the card | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.dedicated_available` | Dedicated video memory available for surfaces | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.system` | System memory the driver reserved at load | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.shared` | Shared system memory the driver may commit | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.framebuffer_physical` | Physical frame buffer size | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.framebuffer_virtual` | Virtual frame buffer size | MachineFact | Nvapi | Agent | Mebibytes |
| `nvapi.vram.available` | Dedicated video memory available now, all programs | Series | Nvapi | Agent | Mebibytes |
| `nvapi.vram.in_use` | Dedicated video memory in use now, all programs: available for surfaces less available now | Series | Nvapi | Agent | Mebibytes |
| `nvapi.vram.evictions_size` | Memory released by evictions since the driver loaded | Series | Nvapi | Agent | Mebibytes |
| `nvapi.vram.eviction_count` | Evictions from dedicated video memory since the driver loaded | Series | Nvapi | Agent | Count |
| `nvapi.vram.promotions_size` | Memory moved in by promotions since the driver loaded | Series | Nvapi | Agent | Mebibytes |
| `nvapi.vram.promotion_count` | Promotions into dedicated video memory since the driver loaded | Series | Nvapi | Agent | Count |

**NVAPI, the card over time**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.load.framebuffer` | Frame-buffer (memory controller) utilisation | Series | Nvapi | Agent | Percent |
| `nvapi.load.video` | Video-engine utilisation | Series | Nvapi | Agent | Percent |
| `nvapi.load.bus` | Bus-interface utilisation | Series | Nvapi | Agent | Percent |
| `nvapi.clock.video` | Video clock | Series | Nvapi | Agent | Megahertz |
| `nvapi.pstate` | The card's current performance state (P-state id) | Series | Nvapi | Agent | Count |
| `nvapi.temp.board` | Board ambient temperature, where the card has the sensor | Series | Nvapi | Agent | Celsius |
| `nvapi.temp.power_supply` | The card's power-supply temperature, where it has the sensor | Series | Nvapi | Agent | Celsius |
| `nvapi.limit.ac_battery` | Held back by a switch to battery on the tick: 100 or 0 | Series | Nvapi | Agent | Percent |
| `nvapi.limit.api` | Held back at a program's request on the tick: 100 or 0 | Series | Nvapi | Agent | Percent |
| `nvapi.limit.insufficient_power` | Held back by a missing power connector on the tick: 100 or 0 | Series | Nvapi | Agent | Percent |
| `nvapi.encoder.sessions` | Active hardware-encoder sessions, all programs | Series | Nvapi | Agent | Count |
| `nvapi.encoder.fps` | Average encoding rate of the active sessions | Series | Nvapi | Agent | FramesPerSecond |
| `nvapi.encoder.latency` | Average encoding latency | Series | Nvapi | Agent | Microseconds |

**NVAPI, the driver**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.driver.package` | Game Ready, Studio or an RTX enterprise branch | MachineFact | Nvapi | Agent | Text |
| `nvapi.driver.dch` | The driver is a DCH package | MachineFact | Nvapi | Agent | Flag |
| `nvapi.driver.base_branch` | The driver's base branch | MachineFact | Nvapi | Agent | Text |
| `nvapi.driver.interface_version` | NVAPI's own interface version | MachineFact | Nvapi | Agent | Text |
| `nvapi.driver.chipset` | Chipset vendor and name, as the driver reports them | MachineFact | Nvapi | Agent | Text |
| `nvapi.driver.global_profile` | Every setting of the driver's global profile, read-only | MachineFact | Nvapi | Agent | Table |
| `nvapi.session.driver_profile` | The settings the driver's profile for this game sets itself, read-only | SessionFact | Nvapi | Agent | Table |

**NVAPI, displays**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.display.hdr_formats` | HDR formats a display accepts: HDR10, HDR10+, HDR10+ Gaming, Dolby Vision, EDR | MachineFact, one per device | Nvapi | Agent | Text |
| `nvapi.display.luminance_max` | A display's maximum luminance for HDR content | MachineFact, one per device | Nvapi | Agent | Nits |
| `nvapi.display.luminance_min` | Its minimum luminance | MachineFact, one per device | Nvapi | Agent | Nits |
| `nvapi.display.luminance_max_frame_average` | Its maximum frame-average light level | MachineFact, one per device | Nvapi | Agent | Nits |
| `nvapi.display.vrr_support` | Variable refresh: supported, G-SYNC module or adaptive sync, ULMB, usable at the current mode | MachineFact, one per device | Nvapi | Agent | Text |
| `nvapi.display.adaptive_sync_off` | Adaptive sync is switched off on the display | MachineFact, one per device | Nvapi | Agent | Flag |
| `nvapi.display.max_frame_interval` | The longest frame interval adaptive sync is set to hold | MachineFact, one per device | Nvapi | Agent | Microseconds |
| `nvapi.session.vrr_enabled` | Variable refresh was active on the game's display | SessionFact | Nvapi | Agent | Flag |
| `nvapi.session.vrr_requested` | Variable refresh was requested for it | SessionFact | Nvapi | Agent | Flag |
| `nvapi.session.vrr_possible` | Variable refresh was possible on it | SessionFact | Nvapi | Agent | Flag |
| `nvapi.session.vrr_display_mode` | The display itself was in variable-refresh mode | SessionFact | Nvapi | Agent | Flag |
| `nvapi.session.source_colorspace` | The colour space the game presented in, as the driver reports it for the process | SessionFact | Nvapi | Agent | Text |

**LibreHardwareMonitor, the graphics card (every vendor, unelevated)**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `lhm.gpu.temperature` | Every temperature sensor of the card | Series, one per device | Lhm | Agent | Celsius |
| `lhm.gpu.load` | Every load of the card: its engines, its bus, its memory controller | Series, one per device | Lhm | Agent | Percent |
| `lhm.gpu.memory` | The card's memory counters: total, free, used, Direct3D dedicated and shared | Series, one per device | Lhm | Agent | Mebibytes |
| `lhm.gpu.clock` | Every clock of the card | Series, one per device | Lhm | Agent | Megahertz |
| `lhm.gpu.power` | Every power reading of the card | Series, one per device | Lhm | Agent | Watts |
| `lhm.gpu.fan` | Every fan of the card | Series, one per device | Lhm | Agent | Rpm |
| `lhm.gpu.fan_control` | Each fan's duty | Series, one per device | Lhm | Agent | Percent |
| `lhm.gpu.voltage` | Every voltage of the card | Series, one per device | Lhm | Agent | Volts |
| `lhm.gpu.current` | Every current of the card | Series, one per device | Lhm | Agent | Amperes |
| `lhm.gpu.throughput` | PCI Express traffic to and from the card | Series, one per device | Lhm | Agent | BytesPerSecond |

**LibreHardwareMonitor through PawnIO (the Agent as administrator, D103)**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `lhm.cpu.temperature` | Processor temperatures: the package and each core | Series, one per device | Lhm | AgentElevated | Celsius |
| `lhm.cpu.load` | Processor load: the total and each core | Series, one per device | Lhm | AgentElevated | Percent |
| `lhm.cpu.clock` | Each core's clock, and the bus clock | Series, one per device | Lhm | AgentElevated | Megahertz |
| `lhm.cpu.power` | Processor power: the package, the cores | Series, one per device | Lhm | AgentElevated | Watts |
| `lhm.cpu.voltage` | Processor voltages | Series, one per device | Lhm | AgentElevated | Volts |
| `lhm.board.model` | The motherboard's maker and model | MachineFact | Lhm | AgentElevated | Text |
| `lhm.board.sensor_chip` | The board's sensor chip | MachineFact | Lhm | AgentElevated | Text |
| `lhm.board.temperature` | The board's temperature sensors | Series, one per device | Lhm | AgentElevated | Celsius |
| `lhm.board.fan` | Each fan header's speed | Series, one per device | Lhm | AgentElevated | Rpm |
| `lhm.board.fan_control` | Each fan header's duty | Series, one per device | Lhm | AgentElevated | Percent |
| `lhm.board.voltage` | The board's voltage rails | Series, one per device | Lhm | AgentElevated | Volts |
| `lhm.memory.modules` | The memory modules: size, speed, maker's part name - never a serial number | MachineFact | Lhm | AgentElevated | Table |
| `lhm.memory.load` | Memory in use: physical and virtual | Series, one per device | Lhm | AgentElevated | Percent |
| `lhm.memory.temperature` | Each memory module's temperature, where it has a sensor | Series, one per device | Lhm | AgentElevated | Celsius |

**What the Agent could reach and does not**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `lhm.psu` | Power-supply sensors: reading them makes the library search every USB device | MachineFact | Lhm | Refused | Table |
| `lhm.controller` | Fan-controller sensors: the same search of USB devices | MachineFact | Lhm | Refused | Table |
| `lhm.battery` | Battery sensors | MachineFact | Lhm | Deferred | Table |
| `lhm.storage.models` | The drives' model names: the library cannot list drives without touching removable ones | MachineFact | Lhm | Deferred | Table |
| `lhm.storage.temperature` | Each drive's temperature: the same, so the storage group is not opened | Series, one per device | Lhm | Deferred | Celsius |
| `nvapi.gpu.board_serial` | The card's board serial number | MachineFact | Nvapi | Identifier | Text |
| `nvapi.gpu.uuid` | The card's unique id | MachineFact | Nvapi | Identifier | Text |
| `nvapi.display.edid` | A display's EDID block, which carries its serial number | MachineFact, one per device | Nvapi | Identifier | Text |
| `nvapi.encoder.session_processes` | Which programs are encoding | Series | Nvapi | Identifier | Table |
| `nvapi.gpu.power` | Card power through NVAPI (LibreHardwareMonitor reads it) | Series | Nvapi | NotInHeaders | Watts |
| `nvapi.gpu.fan_duty` | Fan duty through NVAPI (LibreHardwareMonitor reads it) | Series | Nvapi | NotInHeaders | Percent |
| `nvapi.gpu.voltage` | Card voltage through NVAPI (LibreHardwareMonitor reads it) | Series | Nvapi | NotInHeaders | Volts |
| `nvapi.gpu.ram_type` | The card's memory type | MachineFact | Nvapi | NotInHeaders | Text |

**Inside the game: calls the Overlay already hooks**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `sl.dlss.output_size` | The output size the game gave DLSS | SessionFact | Streamline | Game | Text |
| `sl.dlss.presets` | The DLSS model preset per quality mode | SessionFact | Streamline | Game | Table |
| `sl.dlss.hdr_input` | The game told DLSS its colour buffers are HDR | SessionFact | Streamline | Game | Flag |
| `sl.dlss.auto_exposure` | DLSS auto-exposure | SessionFact | Streamline | Game | Flag |
| `sl.dlss.alpha_upscaling` | DLSS upscales the alpha channel | SessionFact | Streamline | Game | Flag |
| `sl.dlss.exposure` | Pre-exposure and exposure scale given to DLSS | SessionFact | Streamline | Game | Table |
| `ffx.upscale.sharpening` | FSR sharpening: on or off, and its strength | SessionFact | FidelityFx | Game | Table |
| `ffx.upscale.jitter` | The largest jitter offset the game gave FSR | SessionFact | FidelityFx | Game | Table |
| `ffx.upscale.exposure` | Pre-exposure given to FSR | SessionFact | FidelityFx | Game | Text |
| `ffx.upscale.depth` | Near and far planes and the range of the field of view given to FSR | SessionFact | FidelityFx | Game | Table |
| `ffx.upscale.flags` | The flags of the game's FSR dispatch | SessionFact | FidelityFx | Game | Text |
| `ffx.upscale.frame_time_delta` | The frame time the game reported to FSR | Series | FidelityFx | Game | Milliseconds |
| `ffx.memory_usage` | FSR's own video memory, when the game asks it | Series | FidelityFx | Game | Mebibytes |

**Inside the game: hooks that are not built**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `sl.constants.jitter` | The jitter offsets the game gave Streamline | SessionFact | Streamline | Game | Table |
| `sl.constants.motion_vectors` | Motion-vector scale, and whether they are 3D, dilated, jittered | SessionFact | Streamline | Game | Table |
| `sl.constants.depth` | Near and far planes, and whether depth is inverted | SessionFact | Streamline | Game | Table |
| `sl.constants.projection` | Field of view, aspect ratio, orthographic or not | SessionFact | Streamline | Game | Table |
| `sl.constants.resets` | How often the game reset the upscaler's history | SessionFact | Streamline | Game | Count |
| `ffx.framegen.config` | The game's FSR frame-generation configuration | SessionFact | FidelityFx | Game | Table |
| `sl.feature.versions` | Each Streamline feature's version and its NGX model version | SessionFact | Streamline | NewHook | Table |
| `sl.feature.requirements` | What each Streamline feature requires: operating system, driver, buffers | SessionFact | Streamline | NewHook | Table |
| `sl.init.preferences` | The preferences the game started Streamline with | SessionFact | Streamline | NewHook | Text |
| `sl.constants.camera` | Camera position, axes and matrices | SessionFact | Streamline | PlayerState | Text |

**Inside the game: routes a spike decides**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `nvapi.reflex.mode` | Reflex: low-latency mode, boost, the frame-rate limit | SessionFact | Nvapi | Spike | Text |
| `nvapi.reflex.sleep_status` | What the driver's low-latency state is | SessionFact | Nvapi | Spike | Text |
| `nvapi.reflex.frame_report` | Latency per frame: simulation, render submit, present, driver, OS queue, GPU render | Series, one per device | Nvapi | Spike | Microseconds |
| `sl.dlss.optimal_settings` | The render size and sharpness DLSS recommends | SessionFact | Streamline | Spike | Text |
| `sl.dlss.vram_estimate` | DLSS's own estimate of its video memory | Series | Streamline | Spike | Mebibytes |
| `sl.dlssg.options` | DLSS frame generation: mode, frames to generate | SessionFact | Streamline | Spike | Text |
| `sl.dlssg.state` | DLSS frame generation: status, the most frames it can generate | SessionFact | Streamline | Spike | Text |
| `sl.dlssg.frames_presented` | Frames DLSS frame generation presented, by its own count | Series | Streamline | Spike | Count |
| `sl.dlssg.vram_estimate` | DLSS frame generation's estimate of its video memory | Series | Streamline | Spike | Mebibytes |
| `sl.dlssd.options` | Ray reconstruction's options | SessionFact | Streamline | Spike | Text |
| `sl.nis.options` | NVIDIA Image Scaling's options | SessionFact | Streamline | Spike | Text |
| `sl.deepdvc.options` | RTX Dynamic Vibrance's options | SessionFact | Streamline | Spike | Text |
| `sl.reflex.state` | Reflex through Streamline: availability and its frame reports | SessionFact | Streamline | Spike | Table |
| `sl.pcl.markers` | PC-latency markers the game sets | Series | Streamline | Spike | Count |

**Closed by a licence**

| Id | What it is | Scope | Source | Reach | Unit |
|---|---|---|---|---|---|
| `ngx.parameters` | NGX parameters: the quality value, the DLSS hints | SessionFact | Ngx | Licence | Table |
| `xess.options` | XeSS's options | SessionFact | Xess | Licence | Table |
| `sl.nvperf` | Streamline's performance counters (NSight Perf SDK licence) | Series | Streamline | Licence | Table |
