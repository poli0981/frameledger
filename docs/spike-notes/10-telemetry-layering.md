*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 10 · Telemetry layering

- L1 baseline vendor-neutral:
- `D3DKMT` perf-data probe on Win 10 22H2 **and** Win 11 — stable enough to keep:
- L2: LHM fields returned per vendor (fill `18_GPU_VENDOR_APIS` §Capability
  matrix; use explicit "untested", never `?`, for absent hardware): **NVIDIA measured
  2026-09-03**, below. AMD and Intel stay `untested` (§R5/§R6).
- L2: **GPU sensors unelevated, without PawnIO** (§M5) — decides whether the
  default unelevated Agent has temperatures at all: **YES — row R1 of the table
  pre-committed in `20_OPEN_QUESTIONS` §M5, measured 2026-09-03, unelevated and
  elevated giving the identical field set.**

  Instrument: `FrameLedger.CaptureHost probe-lhm --seconds 3` (`LhmComputerAdapter`
  with `IsGpuEnabled` only, then the same tree read through `LhmTelemetrySource`).
  Machine: RTX 5080, **driver 616.56** (`nvidia-smi`; §Environment's 32.0.16.1088 /
  610.88 is what it was on 2026-08-02 and 2026-08-05), Windows 10.0.29648, LHM
  **0.9.6.0**, PawnIO installed on the box but **never opened** — only LHM's CPU and
  board groups use it, and the GPU group was the only one enabled.

  Unelevated (`process elevated: False`), every tick of three:

  | LHM sensor | Type | Value | → `GpuSample` |
  |---|---|---|---|
  | `GPU Core` | Temperature | 40.4 °C | `TempCoreC` |
  | `GPU Memory Junction` | Temperature | 50 °C | `TempMemoryC` |
  | `GPU Core` | Load | 0–5 % (idle) | `LoadPct` |
  | `GPU Memory Used` | SmallData | 3,071–3,089 MB | `VramAdapterMb` |
  | `GPU Core` / `GPU Memory` | Clock | 2,655–2,677 / 14,801 MHz | `CoreClockMhz` / `MemClockMhz` |
  | `GPU Package` | Power | 54.4–55.7 W | `PowerW` |
  | `GPU Fan 1..3` | Fan | ~1,200 rpm | `FanRpm` (fan 1) |
  | *(no `GPU Hot Spot` sensor at all)* | — | — | `TempHotspotC` = **N/A** |

  Also present and deliberately **not** mapped: `GPU Memory` *Load* (VRAM-in-use as a
  percentage, 18.9 %), `D3D Shared Memory Used` (279 MB of system RAM — the first draft
  of the fragment rule took it as VRAM; `SensorMapTests.SharedMemoryIsNotVram` pins the
  correction), three `Control` sensors (fan duty 30 %), `GPU Bus` / `GPU Video Engine` /
  fourteen `D3D *` engine loads, `GPU Core Voltage`, `GPU PCIe Rx/Tx`.

  Through the port: `TryRead=True`, `Capabilities = TempCore, TempMemory, Load,
  VramAdapter, CoreClock, MemClock, Power, Fan`, `faults=0`. **Elevated control run**
  (`process elevated: True`, launched through UAC): the same eight fields, the same
  values to within idle drift, the same verdict. Elevation adds nothing to GPU
  sensors on this vendor.

  **What it settles:** the unelevated Agent's Tier-2 session has GPU temperature, load,
  power, clocks, VRAM use and fan speed on NVIDIA. The three user-facing sentences stand
  (row R1: they *may* name the fields). **What it does not settle:** hotspot temperature —
  absent here, and whether that is the driver, the GPU generation or LHM 0.9.6 is
  unmeasured; and anything about AMD or Intel.
- L3: Reflex latency, throttle reasons, per-domain utilisation: **not yet** — but L3
  now initialises and is proven to degrade cleanly. Measured 2026-08-05 by
  `ctest fl_nvapi_probe` after vendoring: `NvAPI_Initialize` → `NVAPI_OK`, driver
  **610.88** (branch `r610_85`), **1** physical GPU, "NVIDIA GeForce RTX 5080".
  The absent-driver path is exercised by CI, where the probe prints
  `BRANCH: DEGRADED` and exits 0 — `nvapi64.lib` is a static stub library reaching
  `nvapi64.dll` through `nvapi_QueryInterface` at first call, so it is **not** a
  load-time dependency and a machine with no NVIDIA driver still loads the binary.
  The fields in this bullet need `NvapiTelemetrySource`, which does not exist.
- L3 · **a doc error the vendoring found, which reading vendor documentation would
  not have**: `18_GPU_VENDOR_APIS` §L3's table named `NvAPI_GPU_GetMemoryInfo`. The
  vendored headers mark it `__nvapi_deprecated_function` ("deprecated in release
  520 — use `NvAPI_GPU_GetMemoryInfoEx`"), so under `/W4 /WX` a call to it fails the
  native build. Table corrected. This is the class `17_HOOK_ENGINE` calls the highest
  false-confidence risk in the spike, in a document rather than in code.

