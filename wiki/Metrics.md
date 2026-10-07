# Metrics

How every number is computed is [`docs/03_METRICS.md`](../docs/03_METRICS.md) — the source of truth; the user's side is
[Read your results](../guide/03-read-your-results.md). The calculators are in `src/FrameLedger.Domain/Metrics`, and the
frame-generation ladder in `src/FrameLedger.Application/Recording`.

## The basics

- **Frame time** — present to present, timed with the performance counter at the moment the game presents, per swap
  chain. A paused or interrupted stretch is a gap, and gaps are left out of every figure.
- **Average FPS** — frames divided by the time measured, never the mean of instantaneous rates.
- **1% low / 0.1% low** — 1000 divided by the 99th / 99.9th percentile of the game's own frame times. They need at least
  1,000 / 10,000 frames; with fewer, N/A.
- **Stutter** — a frame longer than twice the median of the 19 frames centred on it.

## The rules for showing a number

- **N/A means not measured.** Never 0, and never an estimate (`docs/02_SPEC.md`, FR-4.9).
- **Frame generation is never folded into one number** (rule 6). Measured: `Native → Displayed (×N FG)`, for example
  `62 → 118 FPS (×1.9 FG)`. Not measured: **Presented FPS**, with a chip saying whether a frame-generation runtime was
  loaded — the word *Native* never stands alone. A session whose frame generation changed shows the state that lasted
  longest, always with its share: `×1.9 FG · 75%`.
- **Ray tracing, path tracing, ray reconstruction** are Yes, No or N/A, each with where it came from (measured, the
  user's override, the game's default). Ray tracing is *Yes* on acceleration-structure builds or `DispatchRays`; *No*
  needs a capable device, the hook in place and no activity at all. Path tracing has no signature in the API, so it is
  only ever suggested.
- **Tier 1 and Tier 2 are never averaged together.** A query and a chart split by tier; comparing across tiers asks the
  user first.

## The game's memory and the sensors

- The game's video memory and RAM are read from outside the game, as Task Manager shows them, once a second, in both
  tiers.
- The PC's sensors come from three layers (Windows, LibreHardwareMonitor, NVAPI); what a layer cannot read is N/A.
  *GPU held back* exists on NVIDIA cards only. CPU temperature needs the agent elevated and PawnIO installed.

What is still unmeasured, or measured on one machine only, is in [`LIMITATIONS.md`](../LIMITATIONS.md) and
[`legal/ACCURACY.md`](../legal/ACCURACY.md).
