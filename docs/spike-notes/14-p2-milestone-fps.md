*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 14 · The P2 milestone and the FPS-impact run *(slots — the owner's runs)*

**Nothing below has been run** *(written 2026-09-10; §14.1's row half is read 2026-09-16 — see there — and §14.2–14.4 are
still unrun)*. The section exists because P2's code is finished and its two
remaining obligations are measurements a script cannot take: a real title, read against its own
settings menu, and a benchmark whose number only the operator can see. Written as empty slots with
the exact commands rather than left absent, so the next session cannot mistake "not measured" for
"not required" — the failure mode this file's own rules name first.

### 14.1 · The milestone session — Tier 1, a real title, against its menus

`15_ROADMAP` §P2: *"first real hooked session persisted with measured upscaler/RT data."*

**The technical half is met and is in the merge gate (2026-09-10, P2 PR-F):**
`AgentEndToEndTests` runs the shipped `FrameLedger.Agent.exe --console capture --exe hook-harness.exe`
and reads back one `sessions` row — tier 1, `d3d11`, > 100 frames, `guard_ticks_published ≥ 1`, the
blobs beside it, no `.partial` left. What the harness cannot supply is a *game*: it has no upscaler,
no frame generation and no ray tracing, so `upscaler`, `fg_mode` and `rt_flag` are honestly `N/A`
there. The milestone is the row where they are not.

**The run, when the owner takes it** (candidates from §9: Cyberpunk 2077, Lies of P, Cronos):

```
FrameLedger.Agent --console consent grant  --exe "<title>.exe"      # type the phrase
FrameLedger.Agent --console launch         --exe "<title>.exe" --seconds 600
FrameLedger.Agent --console sessions --last 1
```

**The row half is read — 2026-09-16, the shipped Agent, not the console.** The owner ran the App with its `--serve`
Agent for an afternoon (18 hooked sessions — **count note, 2026-09-27:** `CHANGELOG` 2026-09-17 says *all nine hooked
sessions of 2026-09-16*; neither count was re-made when the other was written, and the ledger as it stands today holds no
session from that day, so the discrepancy is recorded rather than resolved — attach mode, `late_attach = 1`, every one watcher-driven from a Steam
launch rather than the `launch` verb above — a Steam title started from its Shipping executable relaunches through
Steam, so the console recipe is the wrong vehicle for a Steam library and the product's own path is the right one).
`sessions.id = 10`, **Lies of P**, 16:11, 147 s, `d3d12`, 16,221 frames, `guard_ticks_published = 5`, Overlay
`dcf5796bf2a6-dirty`, read from the ledger (rebuilt that evening from its WAL — `06_DATA_MODEL` §Migrations carries
that incident; the row is the row):

| Field | Read from the row | The game's own menu | Agree? |
|---|---|---|---|
| `upscaler` | `fsr3` (`fg_source = api`) | *owner to read* | |
| `upscaler_quality` | `NULL` (0xFF → not stored, #183) | | *(`N/A` is the measured answer on every title so far — §9, P0 exit criterion 1; FSR 3 carries no Streamline `DLSSOptions` to chain, so it is the expected answer here too)* |
| `render_w` × `render_h` → `output_w` × `output_h` | **1506 × 848 → 2560 × 1440** (`upscale_ratio` 1.70 — FSR "Balanced" is 1.7×; "Quality" would be 1.5×, "Performance" 2.0×) | *owner to read* | |
| `fg_mode` / `fg_factor` | `fsrfg` / **2.04** (`native_fps` 57.1 → `displayed_fps` 116.4, `displayed_counted_by = hook`, no refusal) | *owner to read* | |
| `rt_flag` | `no` (`rt_source = measured`, `rt_frame_pct` 0.0) | *owner to read* | |
| `telemetry_source` | `l1+lhm+nvapi` | *(expect `l1+lhm+nvapi` on this box since PR-E2)* | ✅ |

**What this row does and does not settle.** It is the first hooked session the *shipped* Agent persisted with a
measured upscaler, a counted frame-generation factor and a measured RT state — the milestone's row half. The
"against its menus" column is empty because only the owner can read the game's settings screen, and the row is
kept here with that column blank rather than filled from memory. The same afternoon's DLSS-G rows (Onimusha ×2,
Hell Is Us, Black Myth: Wukong) are the *refused* shape — identity `dlssg` from the tags, `fg_factor` NULL,
`fg_refusal` = `multiple_streams` (RE Engine's second swapchain) or `non_uniform` (a bucket that departed: menus,
loading, alt-tab in a whole play session) — which is the guard working on real play rather than on a 40-second leg,
and the reason `fg_refusal_detail` (schema 0005) now stores the bucket's numbers. Onimusha's `rt_flag = yes` with
`rt_frame_pct` 25.0 / 17.8 and Wukong's `yes` at 16.2 are measured against nothing yet either.

**One launch per capture** (HANDOFF §Traps): a title measured off / ×2 / ×4 is three launches.

### 14.2 · §S1's launcher-fronted title — launch mode and the descendant election

The election is built (P2 PR-F, `DescendantElection`) and unit-tested against fabricated snapshots;
what has never happened is a real launcher spawning a real game under it. An Epic- or
Ubisoft-fronted title is the case: the consented executable exits or never presents, the guard
answers `LaunchTargetExited` / `LaunchNoPresentationRuntime` with nothing injected, and the Agent
then elects the newest tracked descendant and attaches.

**The elected image needs its own consent record** — a launcher's consent does not extend to what it
spawns — so the run is: `consent grant` the launcher, `launch` it, read the `election:` line, then
`consent grant` the game the election named and `launch` again.

*(2026-10-06, beta.15: before beta.15 the `election:` line could not appear — the launch's refused outcome carried no
launcher pid and the election returned before it logged anything. Run this table on beta.15 or later.)*

| | |
|---|---|
| Title / launcher | |
| Guard's answer for the launcher | |
| `election:` line | |
| Session row for the elected image | |

### 14.3 · FPS impact ≤ 0.5 % — `14_TESTING` §Hook overhead item 2

The last thing P1 owes (§R4 moved it here because it needs P2's drain and recorder). Run
`tools/fps-impact-runbook.ps1`; it prints the block that replaces this one.

```
./tools/fps-impact-runbook.ps1 -Exe "<title>.exe" -GameArgs "-benchmark" -Seconds 600
```

| Run | Leg | Game avg FPS | Agent CPU (% of a core) | Agent RSS (MB) |
|---|---|---|---|---|
| 1 | ON | | | |
| 2 | ON | | | |
| 3 | ON | | | |
| 1 | OFF | | | |
| 2 | OFF | | | |
| 3 | OFF | | | |

Gates: FPS delta ≤ 0.5 %, Agent CPU ≤ 1 % of a core, Agent RSS ≤ 150 MB. **Overlay resident ≤ 8 MB is
not measured by the runbook** and is left blank rather than guessed: it is a module inside the game
process, and reading the target's memory is what CLAUDE.md rule 4 forbids — an external tool that
inspects module sizes is the way to fill it.

### 14.4 · §H7's six overlays — still owner-only

Unchanged by P2 and repeated here because it is the other measurement no PR can close: the
compare-and-restore path is proven against a fixture (`ctest fl_unhook_inline`), and §H7 still asks
for one capture with each of the six overlays actually resident on the dev machine.

