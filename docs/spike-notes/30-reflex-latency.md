## 30 · Reflex latency per frame — the routes there are, and what each would cost *(2026-11-12, written beside 0.1.0-beta.45, owner decision D130; `20_OPEN_QUESTIONS` §M14)*

**Why this was written.** Train C of Advanced metrics is "Reflex latency per frame" (`18_GPU_VENDOR_APIS` §The
trains). §M14 asks whether any route to it stays inside rules 4 and 5, and pre-commits the answer for the case
that none does. The owner decided on 2026-11-12 (D130) that this spike is written beside 0.1.0-beta.45 **as a
document**, and that no code of train C is written until the owner has read it and said go or no.

**What this is, and what it is not.** A reading — of the vendored NVAPI headers
(`src/native/third_party/nvapi/nvapi.h`, `nvapi_interface.h`), of Streamline's public headers and plugin source
at tag `v2.14.16` (`include/sl_reflex.h`, `include/sl_pcl.h`, `source/plugins/sl.reflex/reflexEntry.cpp`,
`source/plugins/sl.pcl/pclstats.h`, `pclImpl.h`; fetched through GitHub's API, not vendored), of this
repository's own hook engine, and of a copy of the owner's ledger; one file on the dev box was looked at, the
export table of the installed `nvapi64.dll`. **Nothing was run in a game and no shipped code changed.** Every
"unknown" below is unknown, not assumed either way.

### What Reflex's numbers are, and who holds each

| What | Where it lives | The catalog entry that wants it |
|---|---|---|
| **The per-frame report**: for each of the last 64 frames, the times of input sample, simulation start/end, render submit start/end, present start/end, driver start/end, OS render queue start/end, GPU render start/end, and two durations (`gpuActiveRenderTimeUs`, `gpuFrameTimeUs`) | The **driver**. A program gets it by calling `NvAPI_D3D_GetLatency(pDev, NV_LATENCY_RESULT_PARAMS*)` with the game's own device; Streamline's `slReflexGetState` makes that call and hands the same 64 reports back as `sl::ReflexState::frameReport` | `nvapi.reflex.frame_report`, `sl.reflex.state` |
| **The driver's low-latency state**: low-latency mode on, the last sleep interval, whether the game calls the sleep, the frame-generation multiplier last used | The driver: `NvAPI_D3D_GetSleepStatus(pDev, …)` | `nvapi.reflex.sleep_status` |
| **What the game asked for**: low latency or low latency with boost, the frame-rate limit, whether markers may steer the driver | Only the **arguments** of `NvAPI_D3D_SetSleepMode` or of Streamline's `slReflexSetOptions` — no getter returns boost or the limit | `nvapi.reflex.mode` |
| **The markers the game sets** each frame (simulation start … present end, the latency ping, the flash trigger) | The arguments of `NvAPI_D3D_SetLatencyMarker` / `slPCLSetMarker`; the PCL plugin also writes each as an ETW event | `sl.pcl.markers` |

`FlFrameRecord.reflexLatencyUs` (@48, "0 = unavailable") has waited for this since the record was designed, and
`03_METRICS` lists *PC latency* as "mean/p95 of `reflexLatencyUs` when Reflex reports it". **A report cannot go
into its own frame's record**: the driver completes a frame's report after that frame's GPU work ends, several
presents after the record was written, and a ring record is never rewritten. Whatever route is taken, latency
arrives as a series beside the frames, not as a column of them.

### What the owner's sessions already say

From 0.1.0-beta.44's `sl.feature.versions` / `sl.feature.requirements` (sessions 181–186 of the ledger's copy;
`docs/HANDOFF.md`, item 42):

| Game (session) | Interposer | Asked Streamline about `kFeatureReflex` | about `kFeaturePCL` |
|---|---|---|---|
| Phantom Blade Zero (181) | 2.14.1 | version and requirements | version and requirements |
| Clair Obscur: Expedition 33 (182) | 2.7.30 | version and requirements | version and requirements |
| Valor Mortis (183, 184) | 2.14.1 | version and requirements | version and requirements |
| Black Myth: Wukong (185) | 2.7.4 | version and requirements | version and requirements |
| Cyberpunk 2077 (186) | 2.14.1 | requirements only | not asked |

So **every Streamline game run asks about Reflex**, and the four Unreal Engine games about PCL as well. That a
game asks about a feature is not that it uses it; 0.1.0-beta.45's `slIsFeatureLoaded` and `slSetFeatureLoaded`
rows will say whether Reflex and PCL are loaded and switched, on the owner's next sessions. No session says
anything about a game that uses Reflex without Streamline: none of the hooks sees NVAPI.

**Added 2026-11-13, from the first sessions on 0.1.0-beta.45** (190–196): Streamline answers `eOk` for
`kFeatureReflex` in all five Streamline games run, and `slIsFeatureLoaded` says it is loaded in the three that ask
(Phantom Blade Zero, Cyberpunk 2077, Hogwarts Legacy); Cyberpunk 2077 and Dying Light: The Beast switch it off and
on through `slSetFeatureLoaded`. `kFeaturePCL` is supported in the four games that ask about it. Loaded is still
not in use: whether a game sets markers is what no hook sees.

### The routes

**A — hook NVAPI's own functions** (`SetLatencyMarker`, `SetSleepMode`; what `17_HOOK_ENGINE` §Memory / latency
still names). The `nvapi64.dll` on the dev box (driver 32.0.16.2114, its export table dumped for this note)
exports two functions, `nvapi_QueryInterface` and `nvapi_Direct_GetMethod` — §M14 said one — and no latency
function: each of those is an address `nvapi_QueryInterface` returns for an id (`nvapi_interface.h`:
`NvAPI_D3D_GetLatency` is `0x1a587f9c`). A hook on one is a patch at an address
learned at run time — the class refused on 2026-08-09 — or a wrapper returned from a hooked
`nvapi_QueryInterface`, which is a write to what the game receives (rule 4). **Refused, as §M14 pre-committed.**

**B — hook Streamline's Reflex and PCL functions** (`slReflexSetOptions`, `slPCLSetMarker`, `slReflexGetState`).
None is an export of `sl.interposer.dll`. The game gets each through the exported `slGetFeatureFunction(feature,
name, &fn)`, and Streamline's own header does that **once**: `SL_FEATURE_FUN_IMPORT_STATIC` keeps the pointer in
a function-local static after the first call. Two consequences. A hook on `slGetFeatureFunction` could only see
the game's calls by handing back a wrapper — train D's question (§M15), which needs rule 4 amended. And a hook
that exists only for Advanced metrics is installed after the attach (D111), by which time a game that has
presented a frame has fetched its functions: **the same reason `slInit` is not hooked (D129) applies to the
fetch.** Not a route for train C; it stays train D's, and this finding weakens train D as well.

**C — the Overlay calls `slReflexGetState` itself**, having asked the interposer's exported, documented-thread-safe
`slGetFeatureFunction` for it. No patch, no wrapper, and Streamline uses the device it already holds. But the
header says of `slReflexGetState`: *"This method is NOT thread safe"*, and the source shows why — the function
**writes** two fields of the plugin's context (`lowLatencyAvailable`, `latencyReportAvailable`) from the result
of the two driver calls it makes, with no lock, and then calls into the PCL plugin. The game calls the same
function from a thread FrameLedger cannot know (the call is not hooked), so the Overlay cannot keep its call
from overlapping the game's. It would be FrameLedger breaking the vendor's stated contract inside the game, and
writing plugin state the game reads back. **Recommended refused**, on the header's own words.

**D — the Overlay calls the driver's two getters itself**: `NvAPI_D3D_GetLatency` and `NvAPI_D3D_GetSleepStatus`,
resolved through `nvapi_QueryInterface` of an `nvapi64.dll` **the game already loaded** (never loaded by
FrameLedger), with the device of the swap chain the Overlay already hooks. This is §M14's "route to try first".
What it would be:

- **No patch and no new hook.** Two calls, both getters; `tools/nvapi-readonly-check.ps1`'s list (D108) would
  gain them, and the check would have to cover the Overlay as well as the bridge. The Overlay would not link
  `nvapi64.lib`: it needs the two ids and the two structs from the vendored MIT headers.
- **It works without Streamline** — any Direct3D game that uses Reflex — and not on Vulkan (the Vulkan layer has
  no NVAPI device; Reflex there is a Vulkan extension, a different study).
- **It gives** the report (`nvapi.reflex.frame_report`) and the sleep status (`nvapi.reflex.sleep_status`), and
  through them most of `sl.reflex.state`. **It does not give** what the game asked for (boost, the limit:
  `nvapi.reflex.mode` stays N/A, with the reason) or a count of markers.
- **First of its kind in two ways**, and both are the owner's to rule on. The Overlay would call a *vendor's
  driver interface on its own initiative* inside the game — today it only forwards the game's calls and reads
  their arguments. And it would use *the game's device object* for a call the game did not make: rule 4 allows
  "COM/handle objects we legitimately own", and the device is the game's. The nearest precedent is the read of
  `IDXGISwapChain::GetLastPresentCount` on the hooked chain (rule 6, 2026-09-06), a getter on the game's swap
  chain inside the present detour.
- **Unknown, and what the spike would have to measure**: which object NVAPI wants for a Direct3D 12 game (the
  device behind the queue the swap chain was made on — not verified); whether the getters are safe on a thread
  that is not the game's — the header is silent; Streamline's own plugin calls them on whatever thread the game
  asks for its state while the render thread sets markers, which suggests the driver tolerates it, and is an
  inference; what a call costs, on the presenting thread or on the Overlay's own; what it returns in a game that
  never set a marker (the header: valid only after markers with rising frame ids); whether a call that blocks
  in the driver can be bounded — an SEH guard does not bound a wait.
- **How often**: one call returns 64 frames. At 240 presents a second that is four calls a second to lose none;
  once a second keeps the newest 64 and says how many it skipped.
- **What else moves**: a series through region 5 (a layout change, or the reserved words), the Agent's sampler,
  the page; the Disclaimer's sentence about what the Overlay's own thread does (a version of its own, the
  owner's wording); the Privacy Policy (what is stored: times, no content).

**E — from outside the game: the PCL statistics ETW provider.** Streamline's PCL plugin (and the Reflex SDK's
`pclstats.h` in a game without Streamline) registers `PCLStatsTraceLoggingProvider`
(`{0d216f06-82a6-4d49-bc4f-8f38ae56efab}`) and writes an event for every marker (`PCLStatsEvent`: `Marker`,
`FrameID`), which is what NVIDIA's own overlay listens to. No injection, so it would also be the only route for
a game the guard refuses. What it costs:

- **It gives the game-side markers only** — simulation, render submit, present, the ping. The driver, OS queue
  and GPU times are not in these events; NVIDIA's overlay has them from the driver.
- **A trace session needs administrator rights** (or membership of *Performance Log Users*); the Agent is
  unelevated by default (D34).
- **Listening changes what the game does.** Enabling the provider sets the game's `g_PCLStatsEnable`, and its own
  ping thread then posts a registered window message — or, where the game chose a key, `F13`–`F15` key messages
  — to the game's window every 100–300 ms. It is the game's code and the vendor's design, and it is what any
  listener causes; FrameLedger would still be the cause, in a game it is not even injected into.
- **It reopens a closed decision**: ETW left the product on 2026-08-27/28 (Tier 2 is not a measurement;
  `20_OPEN_QUESTIONS` §G keeps the question of a no-injection measurement).

**F — NVAPI from the Agent.** `NvAPI_D3D_GetLatency` takes the game's device. Another process has none. Not a
route.

### What each catalog entry would become

| Entry | Route A, B | C | D | E |
|---|---|---|---|---|
| `nvapi.reflex.frame_report` | refused | (refused) | **read**, Direct3D | game-side times only |
| `nvapi.reflex.sleep_status` | refused | part | **read**, Direct3D | — |
| `nvapi.reflex.mode` | refused | — | N/A: no getter returns it | — |
| `sl.reflex.state` | refused | (refused) | the same numbers, under the NVAPI entries | — |
| `sl.pcl.markers` | refused | — | — | counted |

### What this reading settles, and what it leaves to the owner

- **Settled by reading**: A and B are the refused classes; C breaks the vendor's own contract; F does not exist.
  §M14's pre-committed "no route without a patch at a run-time address" does **not** fire — D has no patch and E
  has no injection — so the entries do not go to N/A on this document alone.
- **The owner's**, because each crosses a line the rules draw today:
  1. **Route D — go to a spike, or no?** A go means: `hook-harness` and a stub `nvapi64.dll` first (the call
     made, the report read, the overhead measured against NFR-1, a blocked call and a missing module shown to be
     harmless), then one real title of the owner's choice, never first in a game inside the anti-cheat
     exception's trial (D38). Pre-committed for that spike: a fault or a hang attributable to the call, an
     overhead above NFR-1, or a Direct3D 12 device that cannot be obtained without reading what rule 4 forbids
     → Reflex latency stays N/A and the page says why. A go also needs the owner's ruling that calling a
     driver getter with the game's device is inside rule 4, asked with the rule quoted, as §M15 asks for its
     own amendment.
  2. **Route C — recorded as refused?** (Recommended: yes.)
  3. **Route E — leave §G closed, or reopen it for latency markers?** (Recommended: leave it; the route gives
     the smaller half of the number and makes a game post messages to itself.)
- **Not looked into**: Reflex on Vulkan; AMD Anti-Lag 2 and Intel XeLL, which have their own interfaces; what
  `nvapi_Direct_GetMethod` is (the vendored headers do not declare it; the vendored import library names it; nothing here
  would use it); what a
  real game's interposer and its Reflex and PCL plugins export (the above is the headers' and the source's word,
  not a dump of a game's files).

### The owner's answers (2026-11-13, D132 and D133)

1. **Route D: go — to the spike on `hook-harness` and a stub first.** With it the ruling a go needed: calling a
   driver's read-only getter with the device of the swap chain the Overlay hooks is inside rule 4 (`CLAUDE.md`
   carries the note). No real game until the owner has seen the spike's result; no shipped code changes for it.
2. **Route C: refused.**
3. **Route E: left closed**; §G is not reopened.

The spike is [`spike-notes` §31](31-reflex-route-d-harness.md), run on 2026-11-13: none of the stop conditions
above fired on the harness, and it ends in the question of one real title.
