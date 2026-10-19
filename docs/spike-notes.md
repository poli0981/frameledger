# P0 spike notes

P0's named deliverable (`15_ROADMAP` §P0). Where measured results go, not
predictions. (It began empty by design and is now the longest record here.)

Rules:

- Record what was **observed**, with the machine, driver version, game and SDK
  version it was observed on. A result without its context is not reusable.
- Record failures and dead ends too. "We tried X, it deadlocked, here is why" is
  worth more than silence, and prevents the next person retrying it.
- When an entry answers an item in `20_OPEN_QUESTIONS.md`, fix the owning doc,
  delete the question, and note both here.
- Nothing in P1 starts until the exit criteria at the bottom pass.

---

## How this file is split (2026-10-19, beta.24, owner decision D66)

Each section below lives in its own file under `docs/spike-notes/`, moved verbatim; code and documents cite them as `spike-notes §N`, which this table resolves. **A new measurement** gets the next number, its own file and a row here, written before the run as the sections above it were (the decision table first).

| Section | What was measured | File |
|---|---|---|
| §Environment | Environment | [`spike-notes/environment.md`](spike-notes/environment.md) |
| §0 | Licence checks (no hardware needed — do these first) | [`spike-notes/00-licence-checks.md`](spike-notes/00-licence-checks.md) |
| §1 | Guard (moved to first — 20_OPEN_QUESTIONS §R1) | [`spike-notes/01-guard.md`](spike-notes/01-guard.md) |
| §2 | Vulkan layer passthrough (moved earlier — §R2) | [`spike-notes/02-vulkan-layer.md`](spike-notes/02-vulkan-layer.md) |
| §3 | Hook viability on hook-harness only | [`spike-notes/03-hook-viability.md`](spike-notes/03-hook-viability.md) |
| §4 | Vendor symbol reality check | [`spike-notes/04-vendor-symbols.md`](spike-notes/04-vendor-symbols.md) |
| §5 | Proxy swapchains (§H5) | [`spike-notes/05-proxy-swapchains.md`](spike-notes/05-proxy-swapchains.md) |
| §6 | RT detection | [`spike-notes/06-rt-detection.md`](spike-notes/06-rt-detection.md) |
| §7 | First real injection (requires the guard, §1) | [`spike-notes/07-first-real-injection.md`](spike-notes/07-first-real-injection.md) |
| §8 | The accuracy question — why this rewrite exists | [`spike-notes/08-accuracy.md`](spike-notes/08-accuracy.md) |
| §9 | Frame generation | [`spike-notes/09-frame-generation.md`](spike-notes/09-frame-generation.md) |
| §10 | Telemetry layering | [`spike-notes/10-telemetry-layering.md`](spike-notes/10-telemetry-layering.md) |
| §11 | PresentMon / Tier 2 (§M2, §M6) | [`spike-notes/11-presentmon-tier2.md`](spike-notes/11-presentmon-tier2.md) |
| §12 | The gate's own inputs — §S21 and §S20 (2026-08-04) | [`spike-notes/12-gate-inputs.md`](spike-notes/12-gate-inputs.md) |
| §13 | Can the guard go RED against real anti-cheat? (2026-08-04) | [`spike-notes/13-guard-red.md`](spike-notes/13-guard-red.md) |
| §Exit criteria | Exit criteria | [`spike-notes/exit-criteria.md`](spike-notes/exit-criteria.md) |
| §14 | The P2 milestone and the FPS-impact run (slots — the owner's runs) | [`spike-notes/14-p2-milestone-fps.md`](spike-notes/14-p2-milestone-fps.md) |
| §15 | Unreal Engine versions in shipping executables (2026-09-27, beta.10) | [`spike-notes/15-unreal-versions.md`](spike-notes/15-unreal-versions.md) |
| §16 | A game process's own memory, read from outside it (2026-10-03, beta.12, owner decision D43) | [`spike-notes/16-game-memory.md`](spike-notes/16-game-memory.md) |
| §17 | Unity, Godot and RPG Maker versions in installed games (2026-10-12, beta.19) | [`spike-notes/17-unity-godot-rpgmaker.md`](spike-notes/17-unity-godot-rpgmaker.md) |
| §18 | The Ren'Py version in an installed game (2026-10-12, beta.20) | [`spike-notes/18-renpy-version.md`](spike-notes/18-renpy-version.md) |
| §19 | Anti-cheat beside the executable — Wuthering Waves (2026-10-13, beta.21, owner decision D58) | [`spike-notes/19-wuthering-waves.md`](spike-notes/19-wuthering-waves.md) |
| §20 | The launcher in front of four itch installs is malware (2026-10-13, beta.21, owner decision D57) | [`spike-notes/20-itch-malware.md`](spike-notes/20-itch-malware.md) |
| §21 | Opening pages and switching tabs in the App (2026-10-14, beta.23) | [`spike-notes/21-app-pages-and-tabs.md`](spike-notes/21-app-pages-and-tabs.md) |
