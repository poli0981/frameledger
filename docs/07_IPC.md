# 07 — IPC

~~Three~~ Four channels, each chosen for its constraints *(corrected 2026-10-04: §D, the tolerance mapping, arrived
in beta.9 without a row here)*:

| Channel | Between | Transport | Why |
|---|---|---|---|
| **A. Frame ring** | Overlay DLL → Agent | Shared memory, lock-free SPSC | Hot path: no syscall allowed |
| **B. Control block** | Agent ↔ Overlay DLL | Same shared memory, atomics | Tiny, infrequent, must work even if the game is unresponsive |
| **C. Command pipe** | UI ↔ Agent | Named pipe, JSON | Rich, versioned, human-debuggable |
| **D. Tolerance mapping** | Agent → Overlay DLL, once, before injection | Its own shared memory, read-only to the Overlay | Must exist before the Overlay's loader detour does (§D, D33) |

## A + B — shared memory (`Local\FrameLedger.Ring.<pid>`)

Created by the Overlay on init, opened by the Agent. Name is deliberately plain and identifiable (`19_SAFETY` — no obfuscated object names).

Layout — **~~four~~ ~~five~~ six regions** (layout 4 since 2026-09-27, beta.10: region 4 is new and the ring moved
from `0xC0` to `0x100`; **layout 5 since 2026-11-11**, Advanced metrics train B: region 5 is new — twenty-three cache
lines, where the four before it are one each — and the ring moved from `0x100` to `0x6C0`):

```
[0x0000] FlShmHandshake  64 B  write-once by the Overlay at init
[0x0040] FlWriterState   64 B  Overlay-written (writeIndex touched every present)
[0x0080] FlControlBlock  64 B  Agent-written
[0x00C0] FlDisplayState  64 B  Overlay-written, at most every 500 ms per chain (layout 4)
[0x0100] FlAdvancedState 1472 B  Overlay-written by its watchdog, once a second, only while the Agent asks (layout 5)
[0x06C0] FlFrameRecord[capacity]   (64 B each, power-of-two capacity)
```

The header is split in two on purpose. The immutable handshake and the
per-present writer state have completely different access patterns, and keeping
the Agent's control line separate from both is what makes the no-false-sharing
claim true rather than aspirational: the Overlay writes line 2 every frame while
the Agent writes line 3 every second, and neither invalidates the other's line.

```cpp
struct alignas(64) FlShmHandshake {  // exactly 64 B, no padding holes
    uint32_t layoutVersion;      // @0  must equal FL_SHM_LAYOUT_VERSION on both sides
    uint32_t recordSize;         // @4  sizeof(FlFrameRecord); belt-and-braces against struct drift
    uint32_t capacity;           // @8  power of two
    uint32_t pid;                // @12
    char     buildId[32];        // @16 native DLL build id
    uint64_t qpcEpoch;           // @48 session time base, shared with sensor samples
    uint64_t adapterLuid;        // @56 which GPU the swapchain is on (multi-GPU selection)
};

struct alignas(64) FlWriterState {   // Overlay-written
    uint64_t writeIndex;         // @0  monotonic; release-store via std::atomic_ref
    uint32_t status;             // @8  init|ready|self_disabled|unhooked
    uint32_t apiMask;            // @12 which graphics APIs got hooked
    uint32_t faultCount;         // @16 hook faults so far (17_HOOK_ENGINE §Fault policy)
    uint32_t vramBudgetMb;       // @20 IDXGIAdapter3 Budget, refreshed at 1 Hz
    uint32_t rtTier;             // @24 D3D12_RAYTRACING_TIER x10; 0 = NOT QUERIED
    uint32_t hooksInstalledMask; // @28 FlHookFamily bits; monotonic, set-only
    uint32_t rtStateObjectsCreated; // @32 session count, published at 1 Hz
    uint32_t rasterPsoCreated;   // @36 session count, published at 1 Hz
    uint32_t runtimeCensus;      // @40 FlRuntimeCensus bits; watchdog-published, OR-only (2026-09-03, took reserved[0])
    uint32_t slTagCensus;        // @44 FlSlTagType bits per route (2026-09-05)
    uint32_t dxgiPresentsUnseen; // @48 presents DXGI counted beyond the hooked ones (2026-09-05, saturating)
    uint32_t dxgiPresentSamples; // @52 hooked presents on which that counter was read
    uint16_t loaderSignals;      // @56 the LoadLibrary detour's installed bit + wake count (2026-09-06)
    uint16_t earlyStopFamily;    // @58 1-based floor family index of an early stop; 0 = none (2026-09-06)
    uint32_t dxgiPresentsBeforeHook; // @60 presents before the first hooked one (2026-09-06) -- the LAST word:
                                 //     no slack left, so layout 4 added a region rather than a field
};

> **This block showed `reserved[5]` at @44 until 2026-09-27** — five words the header had spent by 2026-09-06
> (`slTagCensus`, the two DXGI counters, the loader words, `dxgiPresentsBeforeHook`). `fl_shm.h` is normative and the
> mirror test reads the header, so nothing broke; a reader of this page was told there was room where there was none.

struct alignas(64) FlControlBlock {  // Agent-written
    uint32_t pauseRequested;     // @0
    uint32_t unhookRequested;    // @4  19_SAFETY: set when the guard fires mid-session
    uint32_t overlayEnabled;     // @8  in-game overlay draw toggle (v1.1)
    uint32_t guardTicks;         // @12 completed guard evaluations, NOT a timer
    uint32_t logFlushRequested;  // @16 a COUNTER: bumped by the Agent at session end; the watchdog flushes the native log on a change (17_HOOK_ENGINE §Native logging, 2026-09-06)
    uint32_t advancedRequested;  // @20 0, or this session's request for Advanced metrics (D111): non-zero, new to this ring; written at attach, either way
    uint32_t reserved[10];       // @24..63 must be zero
};

struct alignas(64) FlDisplayState {  // Overlay-written (layout 4, 2026-09-27, beta.10)
    uint32_t seq;                // @0  seqlock: odd while a writer is inside; the writer CASes and SKIPS if taken
    uint32_t flags;              // @4  FlDisplayFlags: SAMPLED | EXCLUSIVE_KNOWN | EXCLUSIVE | OPENGL
    uint64_t hwnd;               // @8  OutputWindow (DXGI) or WindowFromDC (OpenGL); 0 = a composition chain
    uint32_t bufferWidth;        // @16 the back buffer (DXGI) / client area (OpenGL)
    uint32_t bufferHeight;       // @20
    uint32_t swapEffect;         // @24 DXGI_SWAP_EFFECT + 1 (DISCARD is 0); 0 = not read
    uint32_t swapFlags;          // @28 DXGI_SWAP_CHAIN_DESC.Flags
    uint32_t chainId;            // @32 the FlFrameRecord.swapchainId described
    uint32_t samples;            // @36 samples published (wraps)
    uint64_t sampleQpc;          // @40 QPC of the last sample
    uint32_t reserved[4];        // @48..63 must be zero
};

static_assert(sizeof(FlShmHandshake) == 64);
static_assert(sizeof(FlWriterState)  == 64);
static_assert(sizeof(FlControlBlock) == 64);
static_assert(sizeof(FlDisplayState) == 64);
static_assert(sizeof(FlAdvancedState) == 1472);          // region 5: fl_shm.h is its declaration, member by member
static_assert(FL_SHM_DISPLAY_OFFSET == 0xC0 && FL_SHM_ADVANCED_OFFSET == 0x100 && FL_SHM_RING_OFFSET == 0x6C0 && FL_SHM_RING_OFFSET % 64 == 0);
```

**Region 5 — what a hooked game told its upscaler (layout 5, 2026-11-11; Advanced metrics train B, owner decisions
D111 and D113; written by the Overlay's watchdog since 0.1.0-beta.40).** `18_GPU_VENDOR_APIS` §Train B is the design.
What the region is, as a contract between the two sides:

- **Off means nothing is read and nothing is written.** `FlControlBlock.advancedRequested` is the Agent's word that
  this session records Advanced metrics — written at attach, either way, because the control block outlives a session
  (`ShmRingReader.SetAdvancedRequested`). While it is 0 the Overlay reads no field and installs no hook beyond what
  it did before, and writes nothing here: a ring nobody ever asked is zero from end to end (`fl_guard` checks every
  byte, with a target that sends everything), and `validMask == 0` means "nothing was read", never "the game used
  none of it".
- **"On" is a request, not a 1** *(2026-11-12)*. A game outlives a session, and so do the summaries the Overlay keeps
  inside it — a range, a count, a mask of what was ever seen. So the word is a value no session of this ring used
  before (above the word as it stands and above the request the region last answered; never 0), the watchdog starts
  its summaries afresh at a value it has not seen, and every publication names the request it answers
  (`FlAdvancedState.request`, the word that was `reserved0`). The reader returns only a publication that names the
  value it wrote: what a second session of the same game finds in the region before the first tick is the first
  session's, and is not read. After an "off" the region keeps its last publication, untouched.
- **One writer, once a second.** The detours keep process-local accumulators; the watchdog copies them here at its
  tick. No game thread writes the region, so the seqlock has a single writer: `seq` odd, the fields, `seq` even.
- **The reader keeps its last consistent copy** (`ShmRingReader.AdvancedState`): null until something was published
  for its request;
  the same copy while `seq` has not moved, without copying 1472 bytes again; and that copy still when the writer is
  inside on every attempt — a watchdog killed with its process leaves `seq` odd for good, and what it published
  before is still what the game said.
- **Zero is "not read", never a value.** A Streamline `Boolean` and a DLSS preset are stored as the vendor's value
  plus one; a float as its bits, with `FL_ADV_FLOAT_NONE` for "none seen" — and, in `ffxNearBits` / `ffxFarBits` alone, `FL_ADV_FLOAT_INFINITE` (`0x7F800000`, +infinity's own bits) for a plane at infinity *(0.1.0-beta.42, owner decision D119: an engine with an infinite depth hands FSR `FLT_MAX` or +infinity for one plane; for Streamline `FLT_MAX` is "not set" and stays refused)* — the vendors' own sentinels are refused in
  the detour and never reach the region; a size of 0 is "not given".
- **A per-frame value is summarised where it is seen**: a flag is an "ever seen" mask, a jitter the largest magnitude,
  a field of view its lowest and highest (D114), the reported frame time a count and a sum the watchdog drains.
- **Sized once for the train's three releases.** The members ~~0.1.0-beta.41 and beta.42~~ ~~0.1.0-beta.42 and
  beta.43~~ 0.1.0-beta.43 and beta.44 fill *(renumbered twice on 2026-11-12 — D118: a fix release took beta.41;
  D122: the second step became two releases, and beta.42 fills none of them. The comments in `fl_shm.h` and
  `ShmLayout.cs` carry the new numbers since beta.42)* (`ffxFg*`,
  `sl*`, `features`) are declared now and stay zero until then, so those releases bump no layout. **The `sl*` group
  is filled since 0.1.0-beta.43** by the `slSetConstants` hook (`17_HOOK_ENGINE` §Hook inventory), under
  `FL_ADV_VALID_SL_CONSTANTS`: the call count of every viewport, and for the first viewport seen the resets, the
  jitter's largest magnitude, the motion-vector scale, the two depth planes, the field of view's range, the aspect
  ratio, and one bit per `Boolean` in each of two words — seen true, seen false. `FlHookFamily` gains
  `FL_HOOK_SL_CONSTANTS` (bit 13): the hook is in, which it is only in a process that was asked at least once.
  **The `ffxFg*` group is filled since the same release** by the `ffxConfigure` hook, under
  `FL_ADV_VALID_FFX_FG_CONFIG`: the frame-generation configures of the one module counted, three switches as "seen
  off / seen on" bit pairs (enabled, asynchronous, generated frames only), every flag any configure carried, and
  the last generation rectangle as left, top, width, height — **four `-1` for a rectangle with a negative side or
  one past 32,767**, never a clipped one. `FL_HOOK_FFX_CONFIGURE` (bit 14) is published when every ffx-api module
  the process loaded has its `ffxConfigure` patched.
- **The feature slots are written since 0.1.0-beta.44**: `features` is twelve slots of twenty-four words — word 0
  the `sl::Feature` id plus one (0 is an empty slot), word 1 which of the two questions has an answer (bit 0 the
  version, bit 1 the requirements), then the two versions, the flags, three counts and four more versions, as
  `fl_shm.h` lays them out. Slots fill from 0 in the order the game first asked about each feature; **a new request
  does not empty them** — the one group that is a fact about the process and not about a session — and
  `FL_ADV_VALID_SL_FEATURES` stays set with them. `FL_HOOK_SL_FEATURE_VERSION` (bit 15) and
  `FL_HOOK_SL_FEATURE_REQUIREMENTS` (bit 16) are published with their patches, in a process that was asked.
- **Word 1 of a slot carries more since 0.1.0-beta.45** (no layout change): bit 2 `slIsFeatureSupported` answered,
  with the `sl::Result` it gave in bits 8..15 (0 is `eOk`; a refusal is kept, it is the answer; `0xFF` a code a byte
  cannot carry); bit 3 `slIsFeatureLoaded` answered and bit 4 what it said; bit 5 the game switched the feature off
  through `slSetFeatureLoaded`, bit 6 on. **Bits 5 and 6 are a session's and a new request clears them**; every
  other bit of the word is a fact about the process and stays, as the slot does. A feature Streamline refused has
  a slot with only that word in it. `FL_HOOK_SL_FEATURE_SUPPORTED` (bit 17), `FL_HOOK_SL_FEATURE_LOADED` (18) and
  `FL_HOOK_SL_SET_FEATURE_LOADED` (19) are published with their patches.
- **And two counts** (the same release): bits 16..19 of word 1 are how many times the game allocated the feature's
  resources (`slAllocateResources`), bits 20..23 how many times it freed them (`slFreeResources`) — each stops at
  15 — and `slShutdowns` (@308, taken from the three reserved words that ended the `sl::Constants` line; two remain)
  counts `slShutdown` calls, with `FL_ADV_VALID_SL_SHUTDOWN` (bit 6 of the mask) set once one was seen. All three
  are a session's and a new request clears them. `FL_HOOK_SL_ALLOCATE_RESOURCES` (bit 20),
  `FL_HOOK_SL_FREE_RESOURCES` (21) and `FL_HOOK_SL_SHUTDOWN` (22) are published with their patches.
- **Reflex latency words (train C; written by the Overlay since 0.1.0-beta.47; owner decisions D136, D138).** The
  ten reserved words that ended the first line (@24..63) are `latState`, `latReads`, `latFrames` and seven sums:
  simulation, render submit, present, driver, OS render queue, GPU render, and the total from simulation start to
  GPU render end. **The layout is still 5**: no offset and no size moved, and the handshake's build id refuses a
  mismatched pair before either side reads the other. Unlike every other group here they are **not what the game
  passed to a call the Overlay hooks**: the Overlay makes a call of its own, about once a second, on the presenting
  thread, to the driver's two read-only getters, through an `nvapi64.dll` the game had already loaded
  (`17_HOOK_ENGINE` §Memory / latency). `latState`'s low byte is a `FlLatencyState` — no device, no module, refused
  (a module in the executable's own folder, D140), no getter, reading, error, slow (D141), faulted, stalled — and
  above it the driver's sleep status as switches *seen on* and *seen off* and the last frame-generation multiplier.
  `latFrames` and the sums are **of the newest read not yet published** — zero when no read was made since the
  publication before — so a reader divides one publication's sum by its frames and never adds publications up. A
  sum of all ones is a span some summed frame did not report. `FL_ADV_VALID_LATENCY` (bit 7 of the mask) is set once
  a read returned OK for the request; it is not one of "the game's calls were read", and `latState` is published
  whether or not it is set.
- **Four more latency words (0.1.0-beta.48; owner decisions D142, D144).** The first line was full, so they are
  four of the seven reserved words that ended the `sl::DLSSOptions` line (@100..115; three remain, @116..127) —
  nothing of DLSS's, and **the layout is still 5** for the same reasons. `latGpuActiveSumUs` and `latGpuFrameSumUs`
  are the two times the driver works out itself — the graphics card's render time *excluding the idles in
  between* (`gpuActiveRenderTimeUs`) and one frame's GPU end to the next's (`gpuFrameTimeUs`) — summed over the
  frames of the newest read that gave one; `latGpuCounts` holds how many did, the first in its low half and the
  second in its high half, because a frame can give one and not the other. A frame that gives zero (an older
  driver leaves both zero) or more than a second is in neither the sum nor the count, and is still summed for the
  seven spans. `latMultipliersSeen` is the session's: bit n is set once the sleep status gave frame-generation
  multiplier n since the request, bit 31 standing for 31 and more; `latState` still carries the last one.
- The Vulkan layer never writes it. `ShmLayoutMirrorTests.TheAdvancedStateMirrorMatches` checks every member's
  offset against `fl-layout-dump`; `ShmRingReaderTests` the three reads and that the request is stored either way.

**Region 4 — the presenting swap chain's display facts (layout 4, 2026-09-27, beta.10).** The owner asked for the game's
window size and display mode (exclusive fullscreen / borderless / windowed) and the share of a session each took. The two
facts only the process knows — whether the chain is in exclusive fullscreen, and which window it presents to — are
getters on the chain the title passed to the hooked Present (`GetFullscreenState`, `GetDesc`), asked at most every 500 ms
per chain; everything else — the window's rectangle and style, its monitor, the classification — is the Agent's, read
out of process with user32 at the drain tick, exactly as the foreground sample is (`03_METRICS` §Display mode). The
region describes ONE chain: the largest back buffer, and a chain silent for 2 s gives way; `chainId` names it. It is a
seqlock the writer never waits on — a writer that finds `seq` odd (another presenting thread inside) skips the sample and
the next present retries — and `ShmRingReader.DisplayState` takes a copy between two reads of an even, unchanged `seq`
(eight attempts, then null for that tick). The Vulkan layer never writes it (all zeros: nothing sampled); an OpenGL stream
writes the window and client size with no exclusive bits, which a reader treats as *unknown*, never as *windowed*. A
minimised game presents only `DXGI_PRESENT_TEST`, which the hook drops before sampling, so a `sampleQpc` older than 2 s
leaves the exclusive bits unknown.

The cross-process fields are declared as plain integers and accessed through
`std::atomic_ref` rather than as `std::atomic<T>` members. `std::atomic<T>` has
no guaranteed layout, cannot be memcpy'd, and cannot be mirrored by a C#
`[StructLayout(LayoutKind.Sequential)]` struct — and CLAUDE.md §Struct mirroring
requires exactly that mirror, with a test asserting every field offset on both
sides. `std::atomic_ref<uint32_t>` requires 4-byte alignment and
`std::atomic_ref<uint64_t>` 8-byte, which every offset above satisfies.

**Offset asserts apply to all three header structs, not just the record.** The
Agent reads `layoutVersion`, `recordSize`, `buildId`, `writeIndex` and
`faultCount` across the process boundary; a silent drift in any of them is the
same class of bug as record drift.

> Why this is spelled out in such detail: an earlier revision of this document
> declared a single 88-byte `FlShmHeader` while mapping `FlControlBlock` to
> `0x0040`. In code, `unhookRequested` would have aliased `faultCount` — so the
> safety stop would fire on any hook fault, and the fault counter would be
> clobbered by the Agent's heartbeat. Header layout in this file is normative
> and must be checked with `offsetof`, not read as illustration.

**Security:** the mapping is created with a DACL granting access only to the **current user's SID**; `Local\` namespace keeps it session-scoped. No `Global\` objects (would need admin and would be visible across sessions for no benefit).

**Protocol rules**

- **Writer (game) — seqlock per record.** `seq` lives at offset 56 *inside* the
  record, so "fill 64 bytes" would overwrite the very field guarding the write.
  The payload write must cover bytes `[0,56)` and `[60,64)` and **never touch
  `seq`**:

  ```cpp
  FlFrameRecord* slot = &ring[idx & (capacity - 1)];
  std::atomic_ref<uint32_t> seq{slot->seq};
  const uint32_t s = seq.load(std::memory_order_relaxed);  // sole writer of seq
  seq.store(s + 1, std::memory_order_relaxed);             // odd = writing
  std::atomic_thread_fence(std::memory_order_release);
  /* write payload — bytes [0,56) and [60,64), never slot->seq */
  std::atomic_thread_fence(std::memory_order_release);
  seq.store(s + 2, std::memory_order_relaxed);             // even = complete
  writeIndex.store(idx + 1, std::memory_order_release);    // via atomic_ref
  ```

  `seq` is **monotonic per slot and never reset**, so a full lap of the ring
  always changes it — otherwise a reader stalled for exactly one lap would
  validate a completely different frame as unchanged. Both fences compile to
  nothing on x86-64, so this costs zero against the ≤ 1 µs budget.

- **Reader (Agent).** `writeIndex.load(acquire)`; for each slot, load `seq`
  (acquire), skip if odd, copy, `atomic_thread_fence(acquire)`, then re-load
  `seq` and accept only if unchanged.

- **The ring carries FRAMES, and `DXGI_PRESENT_TEST` is not one.** The writer drops
  occlusion probes before allocating a slot, so `writeIndex` counts frames and no
  consumer has to remember to filter. Decided 2026-08-05; before that the question
  was assigned to nobody here and `03_METRICS` was silent about it.

  > A probe runs the presentation test and submits nothing — measured, 500 of them
  > leave `GetLastPresentCount` at 0 while 37 real presents move it by 37. An
  > application issues them while minimised or fully occluded, so a backgrounded
  > game emits a steady stream of non-frames: **142 of them reached the ring in one
  > 2-second canary run** against a writer without the filter. `03_METRICS` derives
  > Displayed FPS from `count(F_disp)/D` and frame times from consecutive `qpc`, so
  > recording them makes a minimised game report a frame rate it is not rendering.
  >
  > The filter sits **after** the safety checks, so a probe-only process still
  > evaluates the stop rather than going unsupervised because it stopped drawing.

- **A skipped torn record is a data gap, not a missing frame.** Dropping it
  silently makes the two surrounding frame times merge into one double-length
  interval — i.e. it *manufactures a stutter* in the metric the whole product
  exists to report. The drain must emit an explicit gap marker at that index;
  `03_METRICS` excludes gap-adjacent intervals from frame-time statistics and
  counts them toward the session's data-quality warnings.

- **Dropped records are computed by the Agent, not the writer.** The writer has
  no reader index and therefore cannot know whether the slot it is about to
  overwrite was ever consumed — the field was unimplementable as originally
  specified. The Agent computes it from the write index it already tracks:
  `if (writeIndex - readIndex > capacity) dropped += (writeIndex - readIndex) - capacity`,
  then resumes at `writeIndex - capacity`. This keeps the hot path free of an
  extra atomic and puts the accounting where the information actually exists.

  > **The formula above needs a starting value, and this document did not give it
  > one.** Added 2026-08-05, after #50 implemented it and changed no documentation
  > at all — so for a day the decision existed only as a comment in
  > `ShmRingReader.cs`.
  >
  > **The reader seeds `readIndex` from the writer's index at attach, not from 0.**
  > `fl_ring.h` starts its reader at 0 because it is created alongside its writer;
  > an Agent attaching to a ring already in flight is a different situation, and
  > the formula above does not distinguish them. Measured: seeded at 0 against a
  > ring at `writeIndex` 1,000,000, the first drain reports **999,992 drops** —
  > and `04_CAPTURE` defines any non-zero drop count as *"the Agent stalled for
  > over ~16 s"* and requires a session warning. The warning's verdict would have
  > been set by how long the target had been presenting before anyone attached.
  >
  > It also **re-ingests up to `capacity` stale records as current frames**:
  > stopping is one-way, so a ring left behind by a finished session still holds
  > its records with `writeIndex` frozen.
  >
  > Records already published at the moment of attach are reported separately as
  > `RecordsBeforeAttach`. **That is not a stall and must not be counted as one** —
  > it is the ordinary consequence of attaching to a running game.

- **The reader maps the whole section at offset 0, and the alternative is a silent
  memory-safety bug rather than a style choice.** Measured on .NET 10 / Windows 11
  26300: a view created at a non-zero offset is mapped from the 64 KiB
  allocation-granularity boundary *below* it, and `AcquirePointer` returns **that**
  base. So mapping region 3 at `0x80` and writing `*(uint*)(p + 12)` writes
  `FlShmHandshake.pid` — the field a reader validates first — instead of
  `FlControlBlock.guardTicks`. It does not fault, and `Read<T>` does not bounds-check
  it either, because the handle legitimately spans from byte 0. Mapping at zero makes
  the offsets in `fl_shm.h` usable as written, which is what the native
  `RingWriter::Init` and `RingReader::Init` already do. A reader must **assert**
  `PointerOffset == 0` rather than assume it.

- **`capacity` must be bounded against the mapping, not merely checked for
  power-of-two.** A reader that indexes by raw pointer arithmetic — which it must,
  because the seqlock needs ordering `MemoryMappedViewAccessor.Read<T>` does not
  provide — gives up that API's bounds check at the same time. So the handshake
  gained two refusals beyond the three above: `CapacityInvalid` (not a power of two)
  and **`CapacityExceedsMapping`**, taken from the mapped section's own byte length
  and never from a compiled-in default, which would be checking the handshake
  against an assumption instead of against the section it describes.
- Version handshake: Agent compares `layoutVersion` + `recordSize` + `buildId` against its own. **Mismatch → refuse to attach**, tell the user to restart the game (this happens when the app updates while a game is running).

  > **Implemented 2026-08-05** as `FrameLedger.Shared.ShmHandshakeValidator`, a pure
  > function so every refusal is drivable without a live target. The Agent's own build
  > id comes from `FlGuardBuildId` on the guard DLL — **not** from the Overlay, whose
  > `FlGetBuildId` the Agent cannot reach without `LoadLibraryW`-ing the payload into
  > itself and creating a ring under its own pid (§S23-1).
  >
  > Three things the check does that "compare three fields" does not imply:
  > `layoutVersion == 0` is **`Incomplete`, not a mismatch**, because the Overlay
  > publishes that field last behind a release fence, so zero means "not ready yet" and
  > the Agent should retry rather than tell the user to restart; the version is checked
  > **before** the fields it vouches for, since a record size read under a layout we do
  > not know names the wrong cause; and an **absent** id on either side refuses rather
  > than matching — with neither side carrying one, `"" == ""` is true and the gate
  > would permit attaching to anything, which is the shape it shipped in.
- **`guardTicks` counts completed guard evaluations, not seconds.** It was
  specified as "Agent bumps every second", and a timer-driven tick is the wrong
  signal: it attests that the Agent *process* is alive, while the guard loop can
  be dead — a swallowed exception, or blocked in a service query, or stalled on
  one unreadable process in the §S16 scan set. A consumer reading "supervised"
  would then keep observing *because* the thing supervising it had stopped. The
  Agent increments it at exactly one site, after `Evaluate` returns a verdict.
  A refusal counts too, and also sets `unhookRequested`, so no consumer has to
  infer a verdict from a counter.
- **Supervision loss means stop observing.** If `guardTicks` has not advanced
  within **65 seconds** (`FL_GUARD_TICK_DEADLINE_MS`, `fl_shm.h`), the capture
  side stops recording.

  > **Evaluated OFF the present path, by a watchdog thread** (`17_HOOK_ENGINE`
  > §The watchdog thread). For one day it was evaluated only inside the present
  > hook, which made it unreachable in a hung or alt-tabbed game — the exact
  > scenario it exists for, and the one `fl_shm.h` warns about in capitals over
  > the constant. `20_OPEN_QUESTIONS` §S25 records the measurement.
  >
  > **The safety stop (`unhookRequested`) is checked in BOTH places**, and the
  > asymmetry is deliberate: this rule tolerates a second of latency, while the
  > safety stop is required within one frame, which only the present path can
  > deliver while frames exist.

  > **This sentence had no number until 2026-08-05.** One grep over `docs`, `src`
  > and `tools` for "deadline" returned exactly one line — this one, the sentence
  > that depends on it. A rule with no value is not implementable, and this had
  > been read as delivered.
  >
  > **65 s is two missed scans.** The guard re-scan runs every 30 s, so a ~35 s
  > deadline would stop a session on a single late tick — and a tick is late
  > whenever the machine is busy, which during a benchmark is always. Two
  > consecutive misses is a signal; one is noise. The cost is that the worst-case
  > window in which an unsupervised hooked process keeps observing doubles, from
  > the 30 s `legal/DISCLAIMER.md` discloses to 65 s, and that document now says
  > 65 rather than being left to imply 30.

  This used to read "the
  Overlay keeps writing (harmless)" — which described an *unsupervised hooked
  process* as harmless, and the 30 s re-scan `19_SAFETY` calls the most
  important runtime behaviour is exactly what has stopped in that state.
  One field cannot mean opposite things in two hosts, so the Overlay and the
  Vulkan layer share this rule.

  > The two are not equally exposed and the asymmetry is worth stating rather
  > than hiding: the Overlay entered a process that passed a full pre-injection
  > guard, the layer never did. That argues for the layer being *stricter*, not
  > for the Overlay being laxer.

  "Never advanced" and "stopped advancing" are the same state. The clock starts
  when the mapping is published, so a capture side that is never adopted by an
  Agent is inert from the beginning rather than enjoying a grace window.
- If `unhookRequested` is set, the Overlay disables hooks within one frame and sets `status = unhooked`. This path must be the fastest, most-tested code in the DLL: it is the safety stop.

## D — the tolerance mapping (`Local\FrameLedger.Tolerate.<pid>`), D33 (2026-09-26)

Agent → Overlay, once, BEFORE the injection it concerns: the anti-cheat family NAMES a game's user-mode exception
covers (`19_SAFETY` §The user-mode exception). It cannot live in A + B: the Overlay creates that mapping on init and the
Agent opens it afterwards, while the Overlay's loader detour must know the tolerance from the moment it exists.

```cpp
struct FlTolerance {            // fl_tolerance.h; 16 + 8 x 64 = 528 B, no padding
    uint32_t magic;             // @0  FL_TOLERANCE_MAGIC ("FLTL")
    uint32_t version;           // @4  FL_TOLERANCE_VERSION (1)
    uint32_t count;             // @8  names used, <= 8
    uint32_t reserved;          // @12 zero
    char     families[8][64];   // @16 NUL-terminated ASCII family names
};
```

Created by the Agent for the game's pid, current user only, held for the session; read once by the Overlay's
`InitThread` before `InstallLoaderHook`, which resolves every name against its own compiled floor and honours only a
user-mode family. Absent, malformed or naming anything else: nothing is tolerated. Mirrored as `FlTolerance` /
`ToleranceLayout` in `FrameLedger.Shared` and held to the native layout by `ShmLayoutMirrorTests` through
`fl-layout-dump`. *(Built in beta.9: PR-1, the Overlay reads it; PR-2, the Agent writes it — `Infrastructure.Capture.OverlayToleranceChannel`, security descriptor `O:<user>D:P(A;;GR;;;<user>)` stated rather than defaulted (an elevated Agent's default DACL omits the user, and the Overlay in an unelevated game could not open it), a name that already exists refused as not ours, and a channel that cannot be published is no exception: the session then names no family and the blocked game is refused as before.)*

## C — command pipe (`\\.\pipe\FrameLedger.v2`)

Unchanged in spirit from v1, bumped to `v2` for the new message set.

> ~~**P3, not P2** (HANDOFF §P2 decision D10, restated 2026-09-10 with PR-F): the Agent's `--serve` is
> watcher-driven and logs to `logs\agent-*.log`; nothing below exists in code yet, and the P2 Agent has no
> client. The pipe reader joins the threading model as one more producer when it is written (`04_CAPTURE`).~~
>
> **The READ half is built, 2026-09-13 (P3 PR-1; HANDOFF §P3 decisions D11–D13).** `FrameLedger.Shared.Ipc`
> holds the envelope, the message set below (read half only), `IpcJsonContext` and `IpcFraming`;
> `Infrastructure.Ipc.PipeServer` is the Agent's end and `PipeClient` the UI's; `PipeServerHostedService` runs
> the server under `--serve` only — `--console` composes it and never starts it, so it publishes to nobody.
> The events come from `Application.Ipc.SessionEventPublisher`, the recorder's second observer
> (`ISessionObserver`), on each session's own task; `SessionProgress` is computed once a second and only
> while a client is connected. ~~**The command half — `SetWatchlist`, `SetHookEnabled`, `LaunchGame`,
> `PauseCapture`/`ResumeCapture`, `StopSession`, `UpdateRules`, `Shutdown` — is PR-1b**: none of those types
> is declared in `Shared.Ipc` until its handler exists, and the Agent answers them `Error UnknownType` today.~~
> **The command half is built the same day (P3 PR-1b): `Application.Ipc.AgentCommandHandler`**, every row
> below with its ack, and the one rule §The pipe is not a trust boundary states is what each handler does —
> the Agent establishes the fact itself. Two things a reader must not assume: `SetHookEnabled true` **stamps
> nothing yet** (the pre-scan runs, a block is recorded, and a clean scan is answered `Error
> DisclosureUnavailable` until FR-2.1's reviewed text ships in PR-4), and `SetWatchlist` **removes nothing**
> (removal is FR-1.4's, with its "keep the sessions?" question — never a bulk message's).

- Message mode, single server (Agent), max 2 clients (UI + future CLI), `PIPE_REJECT_REMOTE_CLIENTS`. *(Built:
  `CreateNamedPipe` through CsWin32 with these flags stated in code, two instances, a semaphore slot per client
  so a third connection waits rather than a third instance existing — `PipeServerClientTests`.)*
- ACL: current interactive user's SID + Administrators. Reject clients whose token user differs. *(Built as the
  SDDL `D:P(A;;GA;;;<user SID>)(A;;GA;;;BA)` on the instance — `PipeAccessControl.Sddl`, what `20_OPEN_QUESTIONS`
  §G asked for — and the token check by impersonation on the client's FIRST frame, because the kernel refuses
  `ImpersonateNamedPipeClient` before the client has written; a stranger is disconnected without an answer and
  counted. ~~The client side uses `PipeOptions.CurrentUserOnly`, the mirror check on the server's owner.~~)*

  > **Amended 2026-09-17: the owner is stated, and the client compares it with its token USER.** The SDDL is now
  > `O:<user SID>D:P(A;;GA;;;<user SID>)(A;;GA;;;BA)`. Left to default, a pipe's owner is its creator token's default
  > owner, and an elevated token's is `BUILTIN\Administrators` — measured on the owner's machine that morning: the log
  > files the elevated Agents created are owned by Administrators, the unelevated ones' by the user.
  > `PipeOptions.CurrentUserOnly` compares the pipe's owner with the client token's default OWNER
  > (`NamedPipeClientStream.ValidateRemotePipeUser` in dotnet/runtime), so an App started as administrator refused the
  > user's own unelevated Agent, silently, as though none were there, and started another on every connect round
  > (`01_ARCHITECTURE` §Lifecycle). The client connects without that option and calls
  > `PipeAccessControl.RequireOwnedByCurrentUser` before it writes a byte; `PipeServerClientTests` pins the owner and
  > drives both directions on a token that owns as Administrators (CI's elevated runner — an unelevated run skips it).
  >
  > **Accept failures back off.** An instance that cannot be created is retried 250 ms doubling to 10 s and logged on
  > the first failure, on a change of error and at 10, 100, 1000… in a row, with the Win32 error. The three extra
  > Agents of that morning logged the bare "accept failed" every 260 ms for four hours, 2 MB each. The error is read
  > with `GetLastSystemError`: CsWin32 0.3.298 declares `CreateNamedPipeW` without `SetLastError`, so
  > `GetLastPInvokeError` read 0.
- Framing: 4-byte LE length + UTF-8 JSON, max 1 MB. `System.Text.Json` source-generated contexts in `FrameLedger.Shared`.
  *(Header and body go out in one write, so on the message-mode pipe one frame is one message; the reader
  reassembles by length alone. Over the cap, or a stream ending inside a frame, is `IpcFramingException`.)*
- Envelope: `{ "type": …, "id": …, "payload": … }`; `Ack` correlates by `id`; events have no `id`. *(camelCase
  on the wire, nulls omitted, unknown fields skipped both ways — `IpcCodecTests` pins the last, which is what
  makes an additive field safe.)*
- A request the Agent cannot answer gets `Error { code, message }` with the request's `id`: `UnknownType`,
  `Malformed` (no `id` when the envelope itself could not be read), `ProtocolMismatch`, `HandlerFaulted`. The
  connection survives all four. *(Added 2026-10-04: `SetHookEnabled` and `SetAntiCheatException` also answer
  `Error UnknownGame` — no row has that id — and `Error ExecutableUnreadable` — nothing scanned, stamped or granted;
  the App branches on the second.)*

### Messages

| Type | Dir | Payload / notes |
|---|---|---|
| `Hello` | UI→A | `{ appVersion, protocol: 2 }` → `HelloAck { agentVersion, protocol, pid, elevated, overlayBuildId, vulkanLayerRegistered, telemetrySource /* composite, e.g. l1+lhm+nvapi */, cpuTempAvailable, disclosureVersion /* P3 PR-4, D14: the FR-2.1 text this Agent stamps against */, exceptionDisclosureVersion /* D33, 2026-09-26: the user-mode exception's disclosure this Agent grants against */, elevationOutcome /* beta.10, D34: the admin mode's answer at this start — granted, declined, other-account, failed — or null when nobody asked */, ~~etwAvailable~~ }` — `etwAvailable` struck 2026-09-13 with the tier it described (2026-08-28); `pid` added. ~~`vulkanLayerRegistered` is **false by construction** today (the layer is per-launch, never registered)~~ `vulkanLayerRegistered` is the HKCU implicit-layer registration as the Agent found it at start *(corrected 2026-10-04: since P3 PR-8b; true while `VkLayerReconciler` keeps the layer registered for a hook-enabled Vulkan game, P4 PR-2)* and `cpuTempAvailable` false unelevated; `telemetrySource` is the composite stood up once on the first `Hello` |
| `GetStatus` | UI→A | → `StatusAck { state, activeSession?, tier? }` — and `activeSessions[]`, one `{ sessionGuid, gameId, gameName?, pid, tier, startedAt, hold? }` per running session. **`hold` added 2026-09-23, optional:** present on a session held unhooked (Tier 2), with the same words `SessionStarted.hold` carries; a session listed at tier 2 WITHOUT a `hold` has not attached yet — it is starting, not held. The App reads this list on every (re)connect (§Client behavior) |
| `SetWatchlist` | UI→A | `{ entries: [{ gameId?, exePath }] }` — ~~full replace~~ **ensure, never remove** (built 2026-09-13, PR-1b): each entry's path is normalised by the Agent, its row ensured with hooking OFF when new, and the ack is `WatchlistAck { games: [{ gameId, exePath, name }], unreadable: [exePath] }`; `gameId` in a request is a hint and never trusted over the path. UI re-sends on connect. **Carries no hooking state** (see §The pipe is not a trust boundary). *Since the App writes `games` rows itself under `06_DATA_MODEL` §Writer ownership (HANDOFF §P3 D15), this is the pipe's way of adding a row, not the only one; the watcher reads the table at 1 Hz either way.* |
| `LaunchGame` | UI→A *(the App sends it since beta.21: Play in its Start directly mode, and in the store mode where no store installed the game — `App.Services.GamePlayer`, D59)* | `{ gameId, arguments? }` → ~~suspended-launch~~ launch mode as built ("inject late", `04_CAPTURE` §Launch mode) → `LaunchAck { gameId, accepted, outcome, sessionGuid? }` — `outcome` is `Accepted` · `UnknownGame` · `SessionRunning` (one per game at a time) · `Unavailable` · `RecordingOff` (2026-09-23: the entry's recording switch is off, FR-1.6 — FrameLedger does not start what it will not record) · `PackagedGame` (beta.33, D80: the executable's install root is a GDK package root — a game the Xbox app or the Microsoft Store installed, which Windows starts through its package and never by its path, `spike-notes` §25; nothing is started, and the App says to start it from the Xbox app or the Start menu. The App hands such a game to Windows itself and sends this only when it found no package record for it. Additive: an older App says the unknown outcome as a failure). *(Built 2026-09-13, PR-1b: `CaptureOrchestrator.LaunchAsync` over an `ILaunchRecorderFactory` the Agent composes with the Vulkan layer's environment — or none when FR-2.4's switch is on — and the launcher election runs after, as the console's `launch` does; the elected session is not addressable by the launch's guid.)* |
| `SetHookEnabled` | UI→A | `{ gameId, enabled, disclosureVersion? }` — Agent re-runs the static AC pre-scan and may reply `Refused`. **The Agent stamps the consent timestamp itself.** *(Built 2026-09-13, PR-1b, as far as it can be: `enabled: false` → `RevokeAsync` → `HookEnabledAck { gameId, enabled: false, outcome }`; `enabled: true` → the pre-scan of the executable's directory, a refusal or a "could not verify" recorded through `RecordGuardBlockAsync` and answered `Refused { gameId, reason, family?, signal? }`; a clean scan → ~~**`Error DisclosureUnavailable`, nothing stamped** — the reviewed FR-2.1 text does not exist, and a client's `disclosureVersion` is a claim, not a disclosure shown. PR-4 replaces that last branch with the stamp~~ **since PR-4 (2026-09-13): the client's `disclosureVersion` compared with the Agent's own `SafetyDisclosure.Version`; equal → the stamp from the Agent's clock with `ConsentProvenance.ConsentDialog`, answered `HookEnabledAck { enabled, outcome, prescan: "clean" }`; anything else → `Error DisclosureVersionMismatch`, nothing written (D14). `DisclosureUnavailable` remains for a composition that wired no version, which `--serve` never is.)* **Since beta.8 (2026-09-25):** the pre-scan is of the executable (the guard resolves the install root, PR-2 of that train), and BEFORE it an executable known not to run as x64 is answered `Refused { gameId, reason: "ExecutableNotX64", signal: "x86" | "anycpu32" | "arm64" | "arm" }` with nothing scanned and nothing written — not a guard reason and not a block (`05_DETECTION` §What the files say about themselves). **Since D33 (2026-09-26):** for a blocked game whose user-mode exception is in force for the executable on disk (the option on, a grant made on these bytes, the block naming that family) the pre-scan NAMES the family, and a pass under it is stamped as ever and answered `prescan: "exception"`; a finding about the game under it also ends the exception. |
| `SetAntiCheatException` | UI→A | **Added 2026-09-26 (D33).** `{ gameId, enabled, disclosureVersion? }` — grant or withdraw a game's user-mode anti-cheat exception (`19_SAFETY` §The user-mode exception). `enabled: false` → the exception ends (`Withdrawn`), hooking goes off while the block stands → `AntiCheatExceptionAck { gameId, granted: false, outcome, family? }`. `enabled: true` → `Error DisclosureVersionMismatch` unless `disclosureVersion` is the Agent's `AntiCheatExceptionDisclosure.Version`; `Error ExceptionsOff` while `hooking.usermode_ac_exceptions` is off; `Refused { reason: "BlockNotExceptionable" }` for a title list or an unreadable block; then the Agent's OWN facts — ~~the successful Tier-1 sessions (`Refused { reason: "TooFewSessions", signal: <count> }` under two)~~ *(D38, 2026-10-03: no session is needed)*; a game an exception of which ended during its trial is refused before anything is scanned (`Refused { reason: "TrialFailed", family, signal: <unix ms> }`); a tolerant pre-scan of the executable on disk naming the block's family (`Refused { reason, family, signal }` unless the guard let exactly that family through) — and the store writes the grant only over facts that make the game eligible, and never over a failed trial → `AntiCheatExceptionAck { granted, outcome, family }`. **A grant turns nothing on**: hooking is FR-2.1's `SetHookEnabled`, afterwards. Not answered (`UnknownType`) by an Agent composed without the exception. |
| `PauseCapture` / `ResumeCapture` | UI→A | global → `PauseAck { paused }`; `StatusAck.paused` reports it. *(Built 2026-09-13, PR-1b: one in-memory flag, `CapturePause`, read by every session loop at its tick and told to the ring once per transition; supervision — the guard scan, the kill switch, the safety stop — never pauses. Not persisted: an Agent that restarts resumes.)* |
| `StopSession` | UI→A | `{ sessionGuid }` — graceful ~~unhook~~ stop + finalize → `StopAck { sessionGuid, accepted, reason? }`. *(Built 2026-09-13, PR-1b: the orchestrator names every session it starts and cancels THAT session's stop token; the loop reads it at its next tick, drains once more, and ends as `SessionEndReason.StoppedByUser` — `exit_status = normal`, the row stored like any other, the target left running. A guid nobody runs is `accepted: false`. Distinct from the host's cancellation, which leaves a `.partial`.)* |
| `UpdateRules` | UI→A | **no payload** — a trigger, not a source. The Agent re-reads only its own `%LOCALAPPDATA%\FrameLedger\rules\` copy → `UpdateRulesAck { outcome }`. *(Built 2026-09-13, PR-1b: the seeder's `EnsureSeededAsync` outcome; a payload naming a path is ignored, and a test sends one to prove it.)* |
| `Shutdown` | UI→A | graceful stop → `ShutdownAck`, then the host stops (sessions finalize inside its grace window). *(Built 2026-09-13, PR-1b: `IAgentLifetime`, the host's under `--serve`, a logged no-op under `--console`.)* |
| `DeleteSessions` | UI→A | **Added 2026-10-03 (beta.11, D40).** `{ gameId? }` — every session of that game, or of every game when it is null, with their segments, frame and sensor series and annotations (they cascade) → `DeleteSessionsAck { gameId?, games, sessions }`. `Error SessionRunning` and nothing deleted while a session of that game — of any game, for all — is being recorded (`CaptureOrchestrator.IsRecording` / `IsRecordingAny`). The client names no rows beyond the game: they are the Agent's (§Writer ownership). Additive: an older Agent answers `Error UnknownType`, which the App says as "restart the agent". Carries no safety fact. |
| `SweepRetention` | UI→A | **no payload** — a trigger, like `UpdateRules`: the Agent applies `06_DATA_MODEL` §Retention to every game with the `retention.raw_sessions_per_game` it reads itself → `SweepRetentionAck { keep, games, sessions }` (`keep` 0 = unlimited, and then nothing was swept). *(Built 2026-09-15, P4 PR-7, for Tools ▸ Database maintenance: `Application.Recording.RetentionSweep` over `ISessionRepository.SweepRetentionAllAsync`, composed into `AgentCommandHandler` under `--serve`. The client names no number because the rows are the Agent's (§Writer ownership). Additive: an older Agent answers `Error UnknownType`, which the dialog reads as "restart the Agent". Carries no safety fact.)* |
| `GetAdvancedMachine` | UI→A | **Added 2026-11-11 (Advanced PR-6, owner decisions D101/D107).** `{ factsHash? }` → `AdvancedMachineAck { ready, takenAtUnixMs, factsHash?, factsJson?, readings[{ series, value }], series[{ series, metricId, label }], nvapi, nvapiCode?, sensors, elevatedSensors }` — what the Agent's shared advanced reader last read about this PC (`18_GPU_VENDOR_APIS` §The page's feed). **The handler reads no sensor**: the answer is the reader's last snapshot, one reference, because the pipe's read loop awaits its handler and a driver call there would hold up `Ping` and `StopSession`; the one thing it awaits is the option, a `settings` row asked for at most every two seconds. **A request is the page's hold on the reader**: the first acquires it and each renews a five-second lease, so the App asks once a second while the page is open and nothing has to say that it closed (`Application.Ipc.AdvancedMachineResponder`). `ready: false` — held, not yet read — carries nothing else. `readings` are that tick's only: a slow source is read every few ticks and the client keeps the last value it saw; a value that is not a number is left out. `factsJson` is the PC's facts as the ledger stores them (`AdvancedJson.OfFacts`), left out when the request's `factsHash` is the Agent's own. The three states are `AdvancedSourceStatus` names; a name a client does not know reads as unknown. `Error AdvancedOff` while `telemetry.advanced` is off — nothing is read, and a hold a previous request took is let go at once. Additive, protocol 2: an older Agent answers `Error UnknownType`, which the page shows as such. Carries no safety fact and changes nothing. |
| `SessionStarted` | A→UI | `{ sessionGuid, gameId, gameName, pid, tier, startedAt, hold? }` — **published once per session**: at the attach (tier 1, `startedAt` the attach), or — **since 2026-09-23** — at the first tick of a session held unhooked (tier 2, `startedAt` the session's start, `pid` 0 when the loop never opened the process, as a hooking-off hold never does). ~~a session that never attaches has no `SessionStarted`, only its `SessionCompleted` (tier 2)~~ — that was the beta.5 wire, and every hooking-off, refused or unreadable game read "Nothing is being captured." for as long as it ran. **`hold` (optional, tier 2 only)** is why nothing is measured: `{ reason, guardReason?, family?, signal?, hookingTurnedOff }` — the loop's `SessionEndReason` and, when the guard said anything but allow, its reason, family and signal: the same words the row's `capture_notes` will carry, so the live card and the summary read one sentence (`Formats.Tier2HoldReason` / `Tier2Reason`). The refusal's own event (`CaptureRefused` and the rest, below) goes out with this start rather than when the game exits, and is not repeated at the end |
| `SessionHeld` | A→UI | **Added 2026-09-23.** 1 Hz while a client listens, for a session held unhooked only: `{ sessionGuid, elapsedS, gpuTempC?, cpuTempC?, gpuLoadPct?, cpuLoadPct?, gameVramDedicatedMb?, gameRamPrivateMb? }` — how long it has run and the machine's sensors (and since beta.12 the game process's own memory, read from outside it — optional, an older client ignores them). **Never `SessionProgress`**: its `presents5s`, `presentedQualifier` and `fgMode` are required, and zeros there would put measured-looking numbers on the wire for a session that measures nothing (rule 6 has nothing to show and shows nothing) |
| `SessionProgress` | A→UI | 1 Hz: `{ sessionGuid, elapsedS, presents5s, presentedFps5s?, presentedQualifier, nativeFps5s?, displayedFps5s?, fgFactor?, fgMode, upscaler?, upscalerQuality?, upscalerDriverReported?, renderW/H?, outputW/H?, rtActive?, gpuTempC?, cpuTempC?, cpuLoadPct?, vramProcMb?, latencyUs?, fgRefusal?, fgRuntimeCensus?, gameVramDedicatedMb?, gameRamPrivateMb? }` (the last two added 2026-10-03, beta.12, optional: the game process's dedicated GPU memory and private working set, MiB, from the newest telemetry tick; `vramProcMb` stays reserved for the in-process figure and has no producer) — a 5 s rolling window over the ring through the SAME Domain calculators the row uses (`SessionProgressCalculator`, D13). **`upscalerQualityOverride` added 2026-10-19 (beta.24, D63), optional:** the mode an NVIDIA App override set when the driver reports it applied in the target and DLSS super resolution ran there — `performance`, `balanced`, `quality`, `ultra_performance`, `ultra_quality`, `dlaa`, or `override` when the driver's report alone does not settle the mode (the profile is the stored session's); absent otherwise. **`upscalerDriverReported` added 2026-09-21, optional:** `dlss` when the NVIDIA driver reports an NGX super-resolution feature created and evaluated in the target (`03_METRICS` §The driver-reported rung), so the live card says `DLSS (driver-reported)` where no hook can name the upscaler; identity only. **`cpuLoadPct` added 2026-09-21, optional** (CPU time busy over the last telemetry tick, 0–100; `cpuTempC` has a producer from the same date, present only when the Agent is elevated with PawnIO). **Two fields added 2026-09-14, both optional (an older client ignores them, §C):** `fgRefusal` is the row's `fg_refusal` (schema 0003) — present when `fgMode` names a technology and `fgFactor` is absent, so the live card can say "DLSS-G active — factor not counted" rather than N/A; `fgRuntimeCensus` is the raw census so the `fg_runtime_loaded` chip can name the module. **Rule 6 at the wire (2026-09-13):** `nativeFps5s` / `displayedFps5s` / `fgFactor` are present only when frame generation was measured in the window; otherwise `presentedFps5s` stands alone with `presentedQualifier` (`census_not_run` · `no_fg_runtime` · `fg_runtime_loaded` · `none_withheld`, the row's `presented_qualifier` from the same function). Every unmeasured field is absent, never 0. Suppressed when no client is connected |
| `SessionCompleted` | A→UI | `{ sessionGuid, sessionId?, exitStatus, tier, finalize, reason, gameId?, gameName? }` — UI loads the full row from SQLite; `sessionId` is null and `finalize` is `discarded` when the row was not stored (under the minimum length), or `game_removed` when its entry was removed with its sessions while it ran (2026-09-23), or **`faulted` when the session's task threw before it could finalize** (2026-09-23: it follows the `CaptureError SessionFaulted`, with `exitStatus` `interrupted` and `reason` `SessionFaulted`, so the live card and the tray let go of a session that no longer runs; the `.partial` stays for recovery); `exitStatus` is the row's vocabulary (`normal` · `crashed` · `unhooked_safety` · `degraded`); `reason` is the loop's `SessionEndReason`. **`gameId` / `gameName` added 2026-09-23, optional:** the entry the session was stored under — the finalizer's owner when it re-keyed a session whose entry was removed, else the one it started under — so the notice names the session that completed, not whatever the live card last showed |
| `CaptureRefused` | A→UI | `{ gameId, gameName, reason, family?, signal?, hookingTurnedOff, guardReason? }` — **`guardReason` (beta.13, optional)** is the guard's own reason ~~when it named a family~~ **whenever it refused (beta.18; beside a family only until then)**: `BlockedService` / `BlockedDriver` is a finding on the PC, not in the game, and the notice says so (§S23-3), and a refusal no family names is said in the guard's words (`Formats.GuardSentence`) — it printed the end's name, "reason RefusedByGuard"; `reason` stays the session's end reason — **guard fired**; `hookingTurnedOff` (2026-09-22) is true when the finding turned the game's hooking off (`19_SAFETY` §What a finding does to the game); UI shows the plain-language explanation — ~~and the Tier-2 offer~~; there is no alternative measurement to offer, only the option to record the session unmeasured (`08_UI` §Safety events). *(Built: for `RefusedByGuard`, `RefusedPreviouslyBlocked`, `RefusedKillSwitch`, `PreScanCouldNotVerify` and, since 2026-09-21 (beta.4), `TargetUnreadable` — a protected process the Agent could not open (added 2026-10-04) — `PreScanCouldNotVerify` with no family, because "could not verify" is neither a hit nor a pass; `family`/`signal` are the verdict's. A game merely not enabled or not consented raises nothing: that is an ordinary Tier-2 row, not a refusal to explain.)* |
| `CaptureDegraded` | A→UI | `{ sessionGuid, from: 1, to: 2, reason }` — Tier 1 → Tier 2 mid-flight, or overlay self-disabled. **Under a two-rung ladder this means measurement STOPPED**, not that it continued at lower fidelity, and the notice must say so. *(Since beta.18 it says the end in words, `Formats.EndReasonText`; it printed `reason` raw — "The hook stopped (WriterSelfDisabled); the session continues without measuring" — and a stop ends the session, it does not continue it.)* *(Built: `SupervisionLost`, `WriterSelfDisabled`, `WriterStoppedBlocklisted`, `WriterNeverInstalledHooks`, `SupervisionFaulted`, `KillSwitchEngaged` — `RecordedSessionEvents.Classify` is the table.)* |
| `SafetyUnhook` | A→UI | `{ sessionGuid, family?, signal?, hookingTurnedOff, guardReason? }` — `guardReason` as on `CaptureRefused` (beta.13) — anti-cheat appeared mid-session; prominent UI notice; `hookingTurnedOff` as on `CaptureRefused`. *(Built: the verdict that FIRED, not the pass that started the session — `CaptureOutcome.Verdict` carries the supervisor's last verdict on this reason since 2026-09-13.)* |
| `GameRemoved` | A→UI | `{ gameId, gameName?, exePath, reason }` — **since beta.19 (D55):** the Agent removed a library entry by itself; `reason` is `uninstalled` (its executable gone for ten minutes from a folder that is still there, no session, no anti-cheat block or exception — `06_DATA_MODEL` §Writer ownership). Not a session event. The Games page on screen reloads and says so; a game page showing that entry reloads into its not-found state. Additive: a client that does not know the type ignores it (§Versioning). |
| `HookingTurnedOff` | A→UI | `{ gameId, gameName?, reason, family?, signal? }` — **since 2026-09-25 (beta.8):** the Agent's pre-scan of the library (`19_SAFETY` §What a finding does to the game) found anti-cheat in a game whose hooking the user had turned on, and turned it off. Not a session event — no session is running — so the UI's notice carries no "record without measuring" action, only Dismiss (`SafetyNoticeKind.HookingOff`). A finding on a game whose hooking was already off sends nothing: its page and its library card change, nothing the user switched on did. Additive: a client that does not know the type ignores it (§Versioning). |
| `CaptureError` | A→UI | `{ sessionGuid?, code, message }` — `InjectFailed`, `RingVersionMismatch`, `AttachRefused`, ~~`EtwAccessDenied`~~ (struck 2026-09-13 with the tier), ~~`PresentMonMissing`~~ (retired with the tool, 2026-08-27 — a code naming an implementation nobody chose), `TelemetryUnavailable`, `DbWriteFailed`, `SessionFaulted` (the session's task threw before finalize; the `.partial` stays for recovery, and ~~no `SessionCompleted` follows~~ a `SessionCompleted` with `finalize: faulted` follows since 2026-09-23). *(Built: `RingVersionMismatch` for a layout / record-size / build-id refusal — the "restart the game after an update" case — `AttachRefused` for the rest of the ring's refusals, `InjectFailed` for a target that could not be started, pinned, read or singled out.)* |
| `Ping`/`Pong` | ~~both~~ UI→A | 15 s keepalive, **client-driven** (built 2026-09-13): the client pings, the Agent answers; the Agent initiates nothing on an idle connection |

## The pipe is not a trust boundary

The ACL restricts the pipe to the current user, and that is worth having — but
it is not a guarantee, because **everything the user runs is also the user**.
The gate must therefore survive a hostile client on this pipe, and three
messages were written as though it would not have to. All three are corrected
above; the reasoning is recorded here so they are not reintroduced as
conveniences.

**`UpdateRules { path }` was a documented override of the hard gate (§S3).** It
let any client hand the Agent an arbitrary filesystem path to load detection
*and anti-cheat* rules from — i.e. replace the blocklist with an empty one.
That is precisely the config-file flag `19_SAFETY` §The anti-cheat guard says
does not exist. It is now a bare trigger; the rules **source** is not a
parameter.

**`SetHookEnabled` took `consentAt` from the client.** The per-game informed
consent that FR-2.1 and `19_SAFETY` §User-facing consent make the basis of every
injection was therefore client-asserted, and a forged timestamp would have made
a game look consented-to that the user never saw a dialog for. The Agent stamps
`games.hook_consent_at` from its own clock when it accepts the enable, or
refuses. A client can *request*; it cannot *attest*.

**`SetWatchlist` carried `hookEnabled` and re-sent it on every connect.** The
adjacent `SetHookEnabled` re-runs the static anti-cheat pre-scan and may reply
`Refused`; `SetWatchlist` did not, while setting the same state in bulk. That
asymmetry is a bypass — and a self-healing one, because the UI re-sends the full
list on reconnect, so a value forced once would be re-asserted forever.
`SetWatchlist` now carries identity only. Enabling hooking has exactly one
message, and that message always re-scans.

> The general rule: **no inbound message may assert a safety fact.** It may ask
> for a state change, and the Agent then establishes the fact itself. Any new
> message that carries a verdict, a clearance, a consent record or a rules
> source is the same bug in a new costume.

## Client behavior (UI)

- **The running sessions are the status's, then the events' (2026-09-23).** Every list of running sessions the App
  keeps is seeded from `StatusAck.activeSessions` on every (re)connect and moved by `SessionStarted` /
  `SessionCompleted` after that; a lost Agent empties it. The Dashboard reads the App-lifetime `LiveSessions`
  service (it is rebuilt per visit, so a list of its own would start empty); the tray and the updater seed their
  own from the same status. Before this a Dashboard opened after a game started read "Nothing is being captured.",
  the tray stayed idle for a session that began before the App connected, and the updater held an update at
  *Deferred* on a status read hours earlier.
- Connect with 250 ms × 8 backoff; on failure start the Agent, retry; then show an Agent status banner with ~~Repair~~ Retry *(corrected 2026-10-04)*.

  > **Amended 2026-09-17: never a second Agent.** A round that ends without a connection starts one only when no
  > process holds the user's data folder (`AgentInstanceLock`, probed through `IAgentLauncher.RunningAgent`) and the
  > Agent this App started last has exited. Otherwise it starts nothing and logs why the connect failed and that it
  > is not starting another — once per distinct reason, not once per round (`AgentConnection.LastConnectFailure`;
  > before this the reason was swallowed, and a refusal looked exactly like an absent Agent). The Agent it does start
  > is watched until it exits and its exit code logged, so exit 10 — the folder was already claimed — is visible in
  > the App's log. `AgentConnectionTests` pins both.
  > **Amended 2026-09-27 (beta.10, the admin mode, D34): a start may ask Windows first.** With `capture.run_elevated` on
  > and this App a standard user's, the start is a UAC prompt owned by the App's window (`IAgentLauncher.WillAskForElevation`
  > turns the pill into *Waiting for administrator permission*), and while it is up the folder's `ElevationMarker`
  > (`Local\FrameLedger.Agent.Elevating.<hash>`) is held — `RunningAgent` reads it as "an Agent is coming", so the App never
  > prompts beside the logon task's Agent, which holds the same marker while its own prompt waits on the taskbar. A declined
  > prompt starts the standard user's Agent with `--elevation declined`; an elevated Agent that exits 11 (another account
  > answered) makes the App start standard-user Agents until it restarts.
- **Named objects an elevated process creates state their DACL (beta.10).** An elevated token's default DACL grants
  Administrators and SYSTEM and not the user, so an object an elevated Agent creates with it cannot be opened by the same
  user's unelevated App or game: the pipe (`PipeAccessControl`), the tolerance mapping (§D) and — since beta.10 — the
  ledger's migration lock (`Infrastructure.Startup.SharedMutex`: owner the user, full access to the user, Administrators
  and SYSTEM) each carry an explicit descriptor. The instance lock and the elevation marker are only ever probed, and a
  probe that is refused reads as "held".
- Treat the pipe as unreliable: library, history and charts must all work with the Agent offline. Only live status degrades.
- **A request the Agent never answered is neither a failure nor a refusal (beta.22).** `IAgentRequests.RequestAsync` fails
  in one of four ways: `InvalidOperationException` — not connected, nothing was sent; `IpcRequestException` — the Agent
  answered `Error`; `TimeoutException`; and `IOException` — the connection closed before the answer, whatever closed it
  (`AgentConnection.AnsweredAsync` turns the `IpcFramingException` and `ObjectDisposedException` that `PipeClient` can fail a
  request in flight with into it, so a caller handles one). The last two mean *whether it was carried out is not known*
  (`AgentAnswers.IsUnanswered`): the caller says so in words (`Agent_Unanswered`) and reads what is stored again — deleting
  sessions, the hooking switch, the anti-cheat exception and Play alike. Until beta.22 deleting sessions caught no
  `IOException` (the App's crash dialog) and showed a timeout's own message, and the hooking switch and the exception
  showed `IOException` or `TimeoutException` as their notice.
- SQLite is the source of truth for anything persisted; pipe events are refresh signals (except `SessionProgress`, which is live-only by design).
- `CaptureRefused`, `SafetyUnhook` and (since 2026-09-25) `HookingTurnedOff` are **never** collapsed into a generic error toast — they get dedicated, explanatory UI (`08_UI` §Notifications).

## Versioning

`protocol` bumps only on breaking changes; additive fields are always allowed and unknown fields must be ignored (tested on both sides). `FL_SHM_LAYOUT_VERSION` bumps whenever `FlFrameRecord` or the header changes — and because the DLL lives inside a running game, the Agent must handle mismatch gracefully rather than assuming lockstep.
