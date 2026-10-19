*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 16 · A game process's own memory, read from outside it *(2026-10-03, beta.12, owner decision D43)*

**Why this was measured.** Per-process VRAM had storage since 0001 and no producer: `17_HOOK_ENGINE` §Memory planned
`IDXGIAdapter3::QueryVideoMemoryInfo` inside the game, `20_OPEN_QUESTIONS` §M7 (2026-09-09) recorded that its
`CurrentUsage` is the calling process's own (0 bytes from the Agent) and concluded the figure could only be read in the
game, and §H10 left open where in the game. The owner chose the other road (D43): the documented performance counters
`\GPU Process Memory(pid_<pid>_luid_…_phys_<n>)\Dedicated Usage` / `Shared Usage` — the numbers Task Manager's Details tab
shows — need no handle to the game at all, and `GetProcessMemoryInfo` needs only `PROCESS_QUERY_LIMITED_INFORMATION`.
§M7's conclusion holds for `QueryVideoMemoryInfo` and not in general.

**Already seen before this section was written**, stated so the table below is not read as having preceded them:
(1) the counter set exists on the dev box, 37 instances at an idle desktop (`Get-Counter`, 2026-10-03); (2) `Dedicated
Usage` is not `Local Usage` — dwm.exe 858 MB against 310 MB — so "dedicated" is defined as Task Manager's column and never
compared with a DXGI budget; (3) `ctest fl_process_stats` on the RTX 5080 while the reader was being written: a 256 MiB
DEFAULT buffer moved this process's dedicated figure 12.9 → 269.0 MiB (+256.1), and `PROCESS_MEMORY_COUNTERS_EX2` answered
(private working set 5.2 MiB) on Windows 11 29648.

**The decision table, written before the runs below** (instrument: `fl-probe-processstats`, `--hold-vram` in one
process and `--read` against it in another):

| # | Measurement | Outcome → what it means |
|---|---|---|
| M1 | Cost of one collect + read, 60 samples at 1 s | median ≤ 5 ms → 1 Hz with the sensors; 5–20 ms → still 1 Hz, noted in `18_GPU_VENDOR_APIS`; > 20 ms → every 5 s, and said so |
| M2 | Δ dedicated of a second process holding 512 MiB | within ±2 % → "Dedicated GPU memory, as Task Manager shows it" may be written; off by > 10 % → stop, the video half does not ship |
| M3 | dedicated against that process's own `QueryVideoMemoryInfo(LOCAL).CurrentUsage` | recorded in `03_METRICS`, whichever way it falls — the source does not change |
| M4 | `PrivateWorkingSetSize` (EX2) against `\Process V2(<name>:<pid>)\Working Set - Private` | equal → "the same as Task Manager's Memory column"; EX2 refused → the EX fallback, and Windows 10 stays unmeasured |
| M5 | How soon a new process's counter instance appears | recorded; the first seconds of a session reading N/A is then expected, not a fault |
| M6 | The probe's own private bytes after 600 collections | Δ < 1 MiB → keep one query per session; more → open and close per collection, then measure again |

**Results (2026-10-03, the same evening; RTX 5080, driver 617.14, Windows 11 29648.1000, `fl-probe-processstats`
from this PR's build):**

| # | Measured | Row taken |
|---|---|---|
| M1 | 60 samples at 1 s: collect + read **median 0.098 ms, max 0.217 ms**; 600 back to back: median 0.028 ms, max 0.190 ms | ≤ 5 ms → 1 Hz with the sensors |
| M2 | holder at 8 MiB: dedicated **21.0 MiB**; holder at 512 MiB: **525.0 MiB** → Δ **512.0 MiB** for 512 MiB allocated (0.0 %); the in-process ctest earlier: +256.1 MiB for 256 | within ±2 % → "Dedicated GPU memory, as Task Manager shows it" |
| M3 | the holder's own `CurrentUsage(LOCAL)`: 16.5 MiB (8 MiB holder) and 520.5 MiB (512 MiB holder) against dedicated 21.0 and 525.0 — **dedicated reads 4.5 MiB more, the same 4.5 MiB at both sizes** | recorded in `03_METRICS`: the counter includes a constant per-device amount the DXGI budget group does not; the difference does not grow with what the game allocates |
| M4 | private working set (EX2) **9.1 MiB** = `\Process V2(fl-probe-processstats:<pid>)\Working Set - Private` **9.1 MiB**, in all 60 samples | equal → "the same as Task Manager's Memory column" |
| M5 | the holder's instance was present at the reader's first poll, ~50 ms after the holder started (its device and buffer had already been created) | an instance exists from the process's first GPU allocation; a game that has made none reads N/A, as specified |
| M6 | 600 collections: the probe's private bytes moved by **0 bytes** (−44 KB over the 60 slow ones) | < 1 MiB → one query per session |

**What stays unmeasured:** Windows 10 22H2 (no machine; the reader falls back to `PROCESS_MEMORY_COUNTERS_EX` where EX2 is
refused, and the private working set then reads N/A), AMD and Intel GPUs, a laptop with two adapters (the reader sums
every adapter's instances for the pid), and a real game — the owner's beta.12 check (HANDOFF owner-only item 9) compares
a session's figures with Task Manager's Details tab at the same moment.

