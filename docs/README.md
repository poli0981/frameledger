# FrameLedger developer documentation

This folder is for people who build, change or review FrameLedger. **If you only want to use it, read the
[user guide](../guide/README.md) instead** — it is also in the app, under Help ▸ User guide.

## Where to start

| You want to… | Read |
|---|---|
| Pick up work where it stopped | [`HANDOFF.md`](HANDOFF.md) — sequencing, decisions (D1…D46) and the traps that each cost a cycle. It carries no status on purpose |
| Learn the system | [`../CLAUDE.md`](../CLAUDE.md) (the rules and the layout), then the reading order below |
| Build it | [`12_BUILD.md`](12_BUILD.md) — `./build.ps1 check` is the whole gate, the same one CI runs |
| Contribute or fork | [`../CONTRIBUTING.md`](../CONTRIBUTING.md), [`../FORKING.md`](../FORKING.md), [`../NOTICE`](../NOTICE) |

**Status lives in four places, and nowhere else** — check a status claim against the code before you plan on it:

| Question | File |
|---|---|
| What is unresolved, and what does it block? | [`20_OPEN_QUESTIONS.md`](20_OPEN_QUESTIONS.md) |
| What was measured, on what machine? | [`spike-notes.md`](spike-notes.md) |
| Which phase is where? | [`15_ROADMAP.md`](15_ROADMAP.md) |
| What landed, in which release? | [`../CHANGELOG.md`](../CHANGELOG.md) |

What the software promises users is the accuracy block in [`../legal/ACCURACY.md`](../legal/ACCURACY.md), copied
verbatim into the README and the Disclaimer and bound to its source by `tools/accuracy-check.ps1`.

## Reading order

1. [`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) — the guard, the refusal list, what will not be built. It
   constrains everything else.
2. [`01_ARCHITECTURE.md`](01_ARCHITECTURE.md) — processes, the injection flow, lifecycles, the ADRs.
3. [`02_SPEC.md`](02_SPEC.md) — the FR / NFR ids used everywhere.
4. [`03_METRICS.md`](03_METRICS.md) — the metric math; the source of truth for every number shown.
5. [`17_HOOK_ENGINE.md`](17_HOOK_ENGINE.md) — the C++ hook layer inside the game.
6. [`04_CAPTURE.md`](04_CAPTURE.md) — capture orchestration on the Agent's side.
7. [`05_DETECTION.md`](05_DETECTION.md) — engine, upscaler, frame-generation and ray-tracing facts.
8. [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) — layered telemetry, the game process's memory, vendor-SDK licence rules.
9. [`06_DATA_MODEL.md`](06_DATA_MODEL.md) · [`07_IPC.md`](07_IPC.md) · [`08_UI.md`](08_UI.md) ·
   [`16_WPFUI_SYNTAX.md`](16_WPFUI_SYNTAX.md)
10. The rest: [`09_I18N.md`](09_I18N.md), [`10_LOGGING_AND_BUG_REPORTS.md`](10_LOGGING_AND_BUG_REPORTS.md),
    [`11_UPDATER.md`](11_UPDATER.md), [`12_BUILD.md`](12_BUILD.md), [`13_CI_CD.md`](13_CI_CD.md),
    [`14_TESTING.md`](14_TESTING.md), [`15_ROADMAP.md`](15_ROADMAP.md).

**Before you write native or capture code, read [`20_OPEN_QUESTIONS.md`](20_OPEN_QUESTIONS.md):** its S-series items
are safety items, and something listed there as unresolved should not be built the way an older document describes it.

## Every document, in one line

| Document | What it is for |
|---|---|
| [`01_ARCHITECTURE.md`](01_ARCHITECTURE.md) | The processes (App, Agent, Overlay, Vulkan layer), how injection flows, the architecture decisions |
| [`02_SPEC.md`](02_SPEC.md) | Functional and non-functional requirements, by id |
| [`03_METRICS.md`](03_METRICS.md) | How every number is computed: frame times, lows, stutter, frame generation, display mode, the game's memory, sensor statistics, the export schema |
| [`04_CAPTURE.md`](04_CAPTURE.md) | The Agent's capture loop, the tiers, finalizing and recovery, the threading model |
| [`05_DETECTION.md`](05_DETECTION.md) | Detecting engines, platforms and capabilities; the detection rules |
| [`06_DATA_MODEL.md`](06_DATA_MODEL.md) | The SQLite schema, every migration, who writes what, retention |
| [`07_IPC.md`](07_IPC.md) | The shared-memory ring between the game and the Agent, and the named pipe between the Agent and the App |
| [`08_UI.md`](08_UI.md) | Every screen of the App, as specified and as built |
| [`09_I18N.md`](09_I18N.md) | English / Vietnamese / Japanese strings, the glossary, the safety-string review rule |
| [`10_LOGGING_AND_BUG_REPORTS.md`](10_LOGGING_AND_BUG_REPORTS.md) | Logs in every process, the bug-report bundle, redaction |
| [`11_UPDATER.md`](11_UPDATER.md) | Velopack updates from GitHub Releases, and when an update may apply |
| [`12_BUILD.md`](12_BUILD.md) | The toolchain, the build, the quality gate step by step, publishing and packaging |
| [`13_CI_CD.md`](13_CI_CD.md) | The CI and release workflows |
| [`14_TESTING.md`](14_TESTING.md) | The test strategy, hook overhead, the hardware matrix |
| [`15_ROADMAP.md`](15_ROADMAP.md) | The phases and where each stands |
| [`16_WPFUI_SYNTAX.md`](16_WPFUI_SYNTAX.md) | WPF UI (lepoco) rules, theming, rendered documents, gotchas |
| [`17_HOOK_ENGINE.md`](17_HOOK_ENGINE.md) | The hooks inside the game: inventory, fault policy, the ring writer, Vulkan |
| [`18_GPU_VENDOR_APIS.md`](18_GPU_VENDOR_APIS.md) | Telemetry layers (DXGI/PDH, LibreHardwareMonitor, NVAPI), the game process's memory, licences |
| [`19_SAFETY_AND_ANTICHEAT.md`](19_SAFETY_AND_ANTICHEAT.md) | The anti-cheat guard, what a finding does, the user-mode exception, what will never be built |
| [`20_OPEN_QUESTIONS.md`](20_OPEN_QUESTIONS.md) | Every unresolved question, defect and measurement, with its status marker |
| [`HANDOFF.md`](HANDOFF.md) | Sequencing, the owner's decisions, traps |
| [`spike-notes.md`](spike-notes.md) | What was measured, how, on which machine, and what each result decided |
| [`LIST_GAME_TESTED.md`](LIST_GAME_TESTED.md) | The real games FrameLedger has been run with, and what each session measured (changes need the owner's review) |
| [`legal-drift-history.md`](legal-drift-history.md) | How the legal documents once drifted from the software, and the maintainers' notes moved out of them |
| [`vendor-exports.json`](vendor-exports.json) | The vendor DLL exports the hooks are allowed to bind, checked by `tools/vendor-exports` |

## Beside this folder

- [`../guide/`](../guide/README.md) — the user guide (players, English).
- [`../legal/`](../legal/) — the documents a user accepts, the accuracy statement, the third-party notices and licence
  texts, the trademark note.
- [`../LIMITATIONS.md`](../LIMITATIONS.md) — what the whole software cannot do, in a player's words.
- [`../rules/detection-rules.json`](../rules/detection-rules.json) — engine, platform and capability signatures and the
  anti-cheat blocklist, validated by `tools/rules-validate.ps1`.
