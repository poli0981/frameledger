## 31 · Reflex route D on the harness — the driver's two getters, called from a probe *(2026-11-13, after 0.1.0-beta.46, owner decisions D132, D133; `20_OPEN_QUESTIONS` §M14)*

**Why this was measured.** §30 read the routes to Reflex latency per frame and left one that needs no patch: the
Overlay calling the driver's two getters, `NvAPI_D3D_GetLatency` and `NvAPI_D3D_GetSleepStatus`, with the device of
the swap chain it hooks. The owner said go on 2026-11-13 (D132) — **to a spike on the harness first** — and ruled
that such a call is inside rule 4 (`CLAUDE.md`, the rule's note). This is that spike. **No shipped code changed and
no game was touched**: the call sequence lives in an unshipped probe, and nothing loaded into a game includes it.

**The instrument.** `src/native/tools/fl-probe-reflex` (built with the Overlay's flags; ctest
`fl_reflex_probe_stub` for the half a build machine can run):

- `fl_reflex_candidate.h` — what would move into the Overlay: find `nvapi64.dll` **if the process already holds
  it** (never load it), ask its export `nvapi_QueryInterface` for the two getters by the ids of the vendored
  `nvapi_interface.h`, call them SEH-guarded, summarise the sixty-four frames. It does not link `nvapi64.lib`:
  the static library loads the DLL itself, from a trusted location and with a signature check.
- `--stub`: against `tools/vendor-stubs/stub_nvapi64.cpp`, a stand-in module with knobs for what a driver cannot be
  made to do on demand.
- `--real`: against the installed driver on this PC's NVIDIA adapter, **with the probe playing the game's part** —
  it loads `nvapi64.dll`, calls `NvAPI_Initialize`, and **sets latency markers on its own device** for a few
  hundred frames. That is a write, and it is said here plainly: a getter has nothing to return without it, the
  process is the probe's own, and no shipped binary contains the call (D108's read-only rule is about the Agent's
  bridge; rule 4's note is about a game). The owner was told and approved it with the plan.

### The decision table, written before any run

| # | Measured | Outcome → consequence |
|---|---|---|
| M1 | Stub: the module absent; a getter absent; an error; a report of nothing; sixty-four reports; both device kinds | Anything called or loaded when the module is absent, or a wrong classification → the candidate is wrong and is fixed before any driver run. Otherwise: these cases are harmless as written. |
| M2 | Stub: a call that does not return for 1.5 s, made on a thread of its own and made by a thread that has other work | Made by the busy thread, that thread stands still → **the call may not be made on the Overlay's watchdog** (it supervises, installs and flushes): a thread of its own, or the presenting thread if M6 allows. A thread of its own is a second background thread, and the Disclaimer's sentence about "one" changes. |
| M3 | Real: the getters **before** `NvAPI_Initialize` | A fault → a game that holds the module but has not initialised it would fault under us, and the Overlay cannot initialise NVAPI itself (not a getter): **route D stops** unless initialisation can be known without a call. An error status → harmless: read as "not available". |
| M4 | Real: initialised, 120 frames, **no marker** | Zero frames or an error → as the header says: a game that does not use Reflex reads N/A. Frames with content → record what the driver fills in by itself. |
| M5 | Real: 300 frames **with markers** | Frames with sensible spans → the report is readable by a third party in the process. Zero frames → markers alone are not enough (a sleep mode is needed too): route D reads only games that set one, and the note says so. |
| M6 | Real: the cost of one call (2000 calls, on the presenting thread) | NFR-1 allows 1 µs a present. Once a second on the presenting thread costs `call / presents-a-second` a present: at 60 a second a call may cost 60 µs. Median within that → the presenting thread, once a second, is allowed. Above it, or a worst case over 1 ms → **never on the presenting thread**; a thread of its own (M2). |
| M7 | Real: the getters on another thread four times a second for 60 s, beside a thread that presents and sets markers | **Any fault → route D stops** (pre-committed, §30). Error statuses or empty reports → recorded; they decide whether a read is trusted. Frames a second beside the poller more than 0.5 % below alone → the rate is too high or the route too costly: lower it and measure again, or stop. |
| M8 | Real, Direct3D 12: read with the device, read with the queue, markers on each | The device works → the Overlay already holds it (`g_d3d12Device`). **Only the queue works → Direct3D 12 is out of reach**: the Overlay does not hold the queue (`ResolveApi`: `GetDevice` does not return it) and may not go looking for it. Route D would then be Direct3D 11 only. |
| M9 | Real: the unit of the report's times | The gap between two frames' `simStartTime` against the probe's own frame time. Whatever it is, is recorded; a unit that cannot be told → the spans are not shown as times. |

Not in this spike, whatever it shows: anything in a game; where the numbers would go (shared memory, the Agent, the
page); the wording of the Disclaimer and the Privacy Policy. Those are a plan of their own, after the owner's go.

### Results

**Where.** The dev box of §Environment: Windows 11 Pro build 29648, RTX 5080, NVIDIA driver 32.0.16.2114
(`nvapi64.dll` of the same version), 2026-11-13. `--stub` on software devices; `--real` three times (and a fourth,
shorter, after a fix to the probe — row M6), on the NVIDIA
adapter, each with a 256×256 composition swap chain presented without a wait — some 14,000 to 17,000 frames a
second, which is what makes the cost of anything beside it visible and is nothing like a game.

| # | What was seen | Against the table |
|---|---|---|
| M1 | Stub, all 20 checks pass: with the module absent `Resolve` says so, calls nothing and loads nothing; a module without the getters is refused whole; an error comes back as the error; a report of nothing is zero frames; sixty-four reports are read as the header lays them out; the driver is handed the device and only the device, Direct3D 11 and Direct3D 12 alike; no device, no call. | Harmless as written. |
| M2 | A call held for 1.5 s on a thread of its own: another thread ticked 16 times in its first second. The same call made by that ticking thread: **0 ticks** in the second. | **Not on the watchdog.** A thread of its own, or the presenting thread (M6). |
| M3 | The getters **before** `NvAPI_Initialize`, with the module loaded: both resolve, `GetLatency` returns `NVAPI_OK` and sixty-four empty frames. No fault, three runs. | Harmless: an uninitialised NVAPI reads as "nothing reported". |
| M4 | Initialised, 120 frames, no marker: `NVAPI_OK`, zero frames with anything; the sleep status is `NVAPI_OK` with every field zero. | As the header says. A game that does not use Reflex reads N/A, and the read costs it nothing else. |
| M5 | 300 frames with markers, **no sleep mode set**: sixty-four frames, the newest one or two ids behind the last frame drawn. Per frame the driver fills simulation end, render submit start/end, present start/end, driver end, OS queue start/end and GPU start/end; `inputSampleTime` and `driverStartTime` stay zero (the probe sets no input marker). | **Readable by a third party in the process, from markers alone.** |
| M6 | 2000 calls back to back on the presenting thread: `GetLatency` median **0.50 µs** in the three runs (mean 0.53–0.54, worst 1.2 / 1.5 / 3.0 µs) and **1.50 µs in a fourth**, made that evening after a fix to the probe, with the PC doing other things (mean 1.54, worst 70 µs); `GetSleepStatus` median 0.10 µs in all four, worst 0.7 / 0.8 / 62 / 12 µs. | **Far within NFR-1 once a second**: at 60 presents a second a call may cost 60 µs, and the medians are 0.5–1.5. **Not within it at every present**: the fourth run's median alone is over the microsecond NFR-1 allows a present, and single calls of 62 and 70 µs were seen. Once a second is what is proposed. |
| M7 | 60 s, four reads a second from another thread beside the presenting, marker-setting thread, three runs: **228 reads each, none not OK, none faulted, none empty**; worst read 103 / 112 / 248 µs on the polling thread; no marker error. Frames a second beside the poller against alone: −1.3 % (one 60 s leg against one 5 s leg), then in six alternating 10 s legs −1.4 % and −0.6 % — **with single legs between +5.9 % and −9.9 %**. | **No fault: the route does not stop.** The half-percent line cannot be read off this: the loop's own drift from leg to leg is ten times it, in both directions. So a dose was measured instead — next row. |
| M7b | The same poller **never pausing**: 38.3 million reads in 30 s (**1.28 million a second**), none not OK, none faulted, worst read 2.7 ms; frames a second −3.3 % (legs −1.1 %, −5.3 %, −3.7 %). | Three hundred thousand times the proposed rate costs about three percent of an uncapped loop. Four reads a second is that divided by 320,000: not measurable, and not half a percent. **The driver also took 38 million reads beside some three million marker calls without an error** — the concurrency §30 could only infer. |
| M8 | Direct3D 12: markers set with the **device** — no error; read with the device and read with the queue both return the same sixty-four frames; the sleep status is `NVAPI_OK` with either; markers set with the queue work as well and are read back with the device. | **The device works**, and it is the object the Overlay already holds. |
| M9 | `simStartTime` of two neighbouring frames 91 and 43 apart in two runs where the probe measured 81 and 64 µs a frame, and the absolute values are the time of day (1 794 560 148 756 529 on 2026-11-13). | **Microseconds, on the clock of the Unix epoch.** Spans are differences of two of them. |

**One thing seen that is not understood.** In the first of the three runs the newest frame of the Direct3D 11
report had a `presentEndTime` 1 844 674 519 µs — half an hour — after its own `simStartTime`; the other two runs
show 36–72 µs there, and the Direct3D 12 reads 46–51 µs every time. The frame was the newest in the report, read
within microseconds of being presented. Whatever it was, **the newest frames of a report are not final**: a reader
must not take the last few as complete, and must refuse a span that no frame can have.

### What this settles, and what it does not

- **Settled, on this driver and this adapter**: the two getters can be found without loading anything and called
  with a Direct3D 11 device or the Direct3D 12 device the Overlay holds; they do not fault before initialisation,
  without markers, or beside a thread that sets markers at 16,000 frames a second; a call costs half a microsecond
  to a microsecond and a half;
  the report is readable from markers alone and its unit is known. None of §30's stop conditions fired: no fault or
  hang attributable to the call, no overhead near NFR-1, and the Direct3D 12 device is the one already held.
- **Decided by M2 and M6 together**: the call belongs on the **presenting thread, once a second** — a microsecond
  or so a second, with a worst case seen of 70 µs — not on the watchdog and not on a new thread. So the Disclaimer's "one background thread"
  would not change; what would change is its list of what the part inside the game does at a present.
- **Not settled, because only a game can say**: a game's own device — many threads, deferred contexts, a device a
  game is about to release; what a real title's report holds (input sample, the GPU times of a real frame, frame
  generation's `aiFrameTimeUs`); whether a title that uses Reflex through Streamline alone leaves `nvapi64.dll`
  findable by name (not looked at); a driver other than 32.0.16.2114, and any card but this
  one; a call that blocks — the stub shows what a block does to its thread, and nothing here made the driver block.
- **Not looked into**: Direct3D 11 in the Overlay, which keeps no D3D11 device today (`ResolveApi` releases it):
  reading there means holding one, a reference the Overlay does not take now; and the incomplete newest frame above.

### The question for the owner

**Go to one real title, or stop here?** A go means a plan of its own before any code: the two calls in the present
detour once a second, **only while a session asks for Advanced metrics** (D111), only where the game already
loaded `nvapi64.dll`, Direct3D 12 first (the device is already held); a place for the numbers — a series beside the
frames, since a report arrives after its frame's record is written (§30); `nvapi-readonly-check` extended to the
Overlay and taught that `Sleep` is a writer; the Disclaimer's and the Privacy Policy's wording; the first run on a
title of the owner's choosing that is not inside the anti-cheat exception's trial (D38), with what each outcome
means written before it. A no leaves `nvapi.reflex.*` and `sl.reflex.*` at N/A with the reason, and this note as
the record of why it was within reach.

**Answered 2026-11-13 (owner decision D136): go, to a plan.** The title is Cyberpunk 2077. No code before the plan
is approved, and no release that carries the calls before a stop while a game runs has been exercised on
0.1.0-beta.46 (owner-only item 44 (d)).

### The leg with a sleep (owner decision D139) — the table, written before the run

The plan's review found the largest thing the runs above never touched: **no run set a sleep mode or called
`NvAPI_D3D_Sleep`**, and a game that uses Reflex does both, every frame. If the driver makes a getter wait behind
the game's sleep, a read on the presenting thread holds a present for as long as the sleep lasts. The owner said:
measure it in the probe first (D139). The probe plays the game's part once more — `NvAPI_D3D_SetSleepMode` (low
latency on, a minimum interval of 4 166 µs, markers used) and `NvAPI_D3D_Sleep` at the start of every frame, on its
own Direct3D 12 device, in its own process. **Both are writes**, as the markers were; no shipped binary contains
either, and the sleep mode is switched back off before the device is released.

| # | What is measured | What it decides |
|---|---|---|
| S1 | The sleep mode takes: `SetSleepMode` returns OK, the sleep status then reads low-latency on, and a frame takes about the interval | Not OK, or the status does not change → the leg did not exercise a sleep; said so, nothing concluded from S2–S4. |
| S2 | The two getters on the thread that sleeps and presents: one `GetLatency` after each of 2000 frames, and 2000 calls back to back | **Median above 60 µs → stop** (NFR-1 at 60 presents a second). **Any call of 2 ms or more → stop**: on a presenting thread that is the latch of D141, in every game that uses Reflex. |
| S3 | The getters on **another** thread four times a second for 20 s, and without a pause for 10 s, while the first thread sleeps and presents — against the same two runs on the same device **before** the sleep mode was set | **A fault → stop** (§30). Reads of 2 ms or more that the control does not have → the getter waits behind the sleep: **stop**, and the note says the route reads only games that do not sleep. Reads of 1 ms or more at the same rate as the control → the scheduler, not the sleep. |
| S4 | What the report holds once the game sleeps: frames with anything, and the sleep status's fields | Recorded: it is what a real title's report would look like more closely than M5's. An empty report → a sleep mode changes what markers alone gave; recorded, the reader's "nothing reported" covers it. |

A stop here means no Overlay code is written and the owner is asked again.

### The leg with a sleep — results

Three runs of `fl-probe-reflex --real 10` on 2026-11-13 (RTX 5080, driver 32.0.16.2114), the leg after R5.
**No stop condition fired.**

| # | What was seen | Reading |
|---|---|---|
| S1 | `SetSleepMode` returned OK in all three runs. A frame then took 4 159 µs (the interval asked for is 4 166), 4 037–4 041 of them inside `NvAPI_D3D_Sleep`; no sleep error. The sleep status afterwards: interval 4 166 µs, `bUseGameSleep` 1 — and **`bLowLatencyMode` 0**, although the mode was set with it on. | The sleep was exercised: the thread stood inside it 97 % of the time. **The status's low-latency field is not the flag the game set**, on this driver; why is not known. It is shown as the driver's word, never as "the game switched Reflex on". |
| S2 | One `GetLatency` after each of 2000 sleeping frames, on the thread that sleeps and presents: median **2.5 / 2.6 / 2.6 µs**, mean 2.7–2.8, worst 66.9 / 16.8 / 8.0 µs, none of 2 ms. Back to back: median 0.7 µs (worst 10.2 / 81.6 / 4.1); `GetSleepStatus` median 0.2 µs. | Far inside both limits. A read after a frame that slept costs four or five times a read made back to back — a cold cache, by the look of it — and that is the Overlay's case: **about 2.6 µs, once a second**. |
| S3 | From another thread while the first slept and presented at 240 frames a second. Four reads a second for 20 s: 80 reads a run, none not OK, none faulted, worst 64 / 82 / 12 µs (the control before the sleep mode: 92 / 209 / 119 µs). Without a pause for 10 s: **10.26 / 10.26 / 10.43 million reads**, none not OK, none faulted, worst 442 / 635 / 286 µs, **none of a millisecond** (the control: 9.97 / 9.94 / 9.89 million, worst 1 605 / 1 390 / 203 µs, one read of a millisecond in two of the three runs). | **The getter does not wait behind the sleep.** Thirty-one million reads were made while another thread was inside the sleep nearly all the time, and none took a millisecond; the slow reads there are belong to the control. |
| S4 | The report: sixty-four frames with content. `driverStartTime` is filled in all sixty-four — it was zero in M5, without a sleep mode; `inputSampleTime` in none (the probe sets no input marker). Simulation start to present end 102–151 µs, to GPU end 150–191 µs; `gpuActiveRenderTimeUs` 0. | With a sleep mode the driver span is reported. A reader that treats a zero end point as "span not reported" covers both shapes. |

**What it does not say**: a game's own sleep, on a game's own threads, with a real frame's work between the sleep
and the present; frame generation's pacer; another driver. Those are what the first real title is for (D136).
