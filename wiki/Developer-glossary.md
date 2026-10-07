# Developer glossary

The words the code and the documents use. The player's glossary is [Words FrameLedger uses](../guide/10-glossary.md).

- **Agent** — `FrameLedger.Agent.exe`, the capture orchestrator; `--serve` is its normal mode, `--console` the
  developer's.
- **Census** — the frame-generation runtimes found loaded in a game; it decides the qualifier beside Presented FPS.
- **Consent** — a user's per-game agreement to hooking, typed in the consent dialog, stamped with where it came from.
- **D-numbers** (D1, D2, …) — the owner's decisions, recorded in [`docs/HANDOFF.md`](../docs/HANDOFF.md).
- **Drain** — the agent reading the ring, every 100 ms.
- **FR-x / NFR-x** — requirement ids from [`docs/02_SPEC.md`](../docs/02_SPEC.md).
- **Guard** — the native anti-cheat check that runs before every injection and every 30 seconds after; one
  implementation, in `src/native/FrameLedger.Injector`.
- **Handshake** — the first region of the shared memory: layout version, record size and build id, checked before
  anything is read.
- **Hook harness** — `src/native/tools/hook-harness`, a dummy Direct3D 11, Direct3D 12, Vulkan and OpenGL program the
  tests hook instead of a game.
- **Kill switch** — `hooking.kill_switch`, *Disable all hooking*: nothing is injected, whatever each game says.
- **Ledger** — `ledger.db`, the SQLite file.
- **Overlay** — `FrameLedger.Overlay.dll`, the component injected into a game. It draws nothing; the name is historical.
- **`.partial`** — a session in progress, written in checksummed chunks so a crash leaves a usable prefix.
- **Pre-scan** — the guard's check of a game when it is added, so the game's page can say what was found before it runs.
- **Ring** — the single-producer, single-consumer queue of fixed-size frame records in shared memory.
- **Safety unhook** — a finding during a session: the hook goes dormant and the session says so.
- **Steady state** — the frame-generation state a session spent most of its time in, shown with its share.
- **Tier 1 / Tier 2** — measured / recorded only. There is no third tier.
- **Trial** — the first two hooked sessions under a user-mode exception; one that goes badly ends the exception for
  good.
- **User-mode exception** — the one narrow exception to the guard's refusal, for anti-cheat that runs entirely in user
  mode ([`docs/19_SAFETY_AND_ANTICHEAT.md`](../docs/19_SAFETY_AND_ANTICHEAT.md)).
- **Viewer** — the App started with `--data-dir` over a copy: no agent, nothing changed on the PC.
- **WARP** — Windows' software renderer, which lets the hook harness run on CI without a GPU.
