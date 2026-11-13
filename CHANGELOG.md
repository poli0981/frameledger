# Changelog

All notable changes to FrameLedger are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — `MAJOR`
bumps for a database schema or IPC protocol break (`docs/11_UPDATER.md`).

`release.yml` reads the section for the tag being released (`tools/release-notes.ps1`) and
uses it as the GitHub release body. A missing or empty `## [x.y.z]` section is a **red
release**, not an empty note — so cutting a release means moving the `[Unreleased]` entries
under a `## [x.y.z] - date` heading in the same commit that bumps `VERSION`, then tagging
(`docs/13_CI_CD.md` §Branch & release policy). *(For a pre-release, `vX.Y.Z-beta.N`, `VERSION` already holds the
numeric core and does not move; the heading is the full version — corrected 2026-10-04.)*

> **Built 2026-09-14 (P4 PR-5).** From 2026-08-06 until then this paragraph said the workflow
> "will" exist and that a missing section "will mean an empty release note"; the second half
> was the wrong design and is replaced rather than implemented. ~~This file still has no
> `## [x.y.z]` heading: the first one is the owner's, in the commit that cuts the first tag.~~
>
> **The first heading is `## [0.1.0-beta.1]` (2026-09-16), and it is a summary rather than the moved
> entries.** The instruction above says "move the `[Unreleased]` entries under the heading"; by the first
> tag those entries were 370 KB — every PR since 2026-08-04 — and a GitHub release body holds 125,000
> characters. So the versioned section is a written summary of what the pre-release is and is not, and
> the entries it summarises stay below it, verbatim, under a heading that is not a version
> (`## Ledger before 0.1.0-beta.1`), where `release-notes.ps1` stops reading. From the second tag on,
> the entries under `[Unreleased]` are short enough to move as written.

> **Split 2026-10-19 (beta.24, owner decision D66).** Each released section now lives in its own file under
> `docs/changelog/`, moved verbatim, and the ledger kept before the first tag in five parts under
> `docs/changelog/ledger-before-0.1.0-beta.1/`; the table below lists them all. This file keeps `[Unreleased]`, where
> every pull request adds its entry (`tools/changelog-check.ps1` looks for this file). `tools/release-notes.ps1` reads
> `docs/changelog/<version>.md` for the tag being released, so cutting a release moves the `[Unreleased]` entries into
> that new file — under its `## [x.y.z] - date` heading — and adds its row to the table, in the commit that is tagged.

## [Unreleased]

### Changed

- **The log of the part inside a game says how a group of hooks went in or came out** (owner decision D137). Since
  0.1.0-beta.46 hooks are put into a game in groups, the game's threads stopped once for a group. In the owner's
  first sessions two groups took much longer than the others (0.6 s and 1.3 s against 0.1-0.3 s), and the log
  could not tell one long stop from a group that had to be finished one hook at a time. The `PATCH_BATCH` line now
  reads `install` or `restore` for a group that went in one go, and `install singly` or `restore singly`, with a
  count, for one that did not. Nothing else about hooking changed, and what a game is measured by is the same.

### Fixed

- **For developers: a native test that is cut short no longer counts as passed.** The quality gate's native tests
  were judged by their exit code alone, and a test program that was stopped early with a clean exit code read as
  *Passed* - seen once, when security software removed the test program while the gate ran. Every native test now
  has to print the line it ends on, a test added without one stops the build's configure step, and four tests
  exist only to prove the rule goes red when it should. Nothing a user installs changed.

### Added

- **Not in a release yet: the part inside a game can read NVIDIA's own latency report** (owner decisions D132, D136,
  D138-D141). While a session records Advanced metrics, and only then, it asks the graphics driver about once a
  second for its report of a frame's stages - simulation, render submit, present, driver, the operating system's
  queue, the graphics card - and for its low-latency state. It is a call FrameLedger makes itself, on the thread
  that presents the game's frames, through NVIDIA's `nvapi64.dll` only where the game had already loaded it;
  Direct3D 12 only. A copy of that library sitting in the game's own folder is refused. A call that faults or takes
  two milliseconds is the last one until the game is closed. Nothing shows these numbers yet: the Agent and the App
  come next, with the legal texts.

- **For developers: room in the shared memory for NVIDIA Reflex latency** (owner decisions D136, D138). Ten reserved
  words of the block that carries Advanced metrics from a game are now named for the driver's latency report -
  where the read stands, and a sum for each stage of a frame. Nothing writes them yet and nothing reads them; the
  shared memory's layout version did not change.

- **For developers, nothing a user installs: a probe for NVIDIA Reflex latency** (owner decision D132). An
  unshipped tool, `fl-probe-reflex`, and a stand-in `nvapi64.dll` among the test fixtures measure what it would take
  to read the driver's latency report from inside a game - on FrameLedger's own test program and against the
  installed driver, never in a game. What it found is `docs/spike-notes/31-reflex-route-d-harness.md`. A further
  leg (owner decision D139) measured the same reads while the probe itself used Reflex's sleep, as a game does:
  the reads do not wait behind it. The owner has said to go on to a real game (D136); that work is not in any
  release yet. Nothing in the App, the Agent or the part that runs inside a game changed.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
| `0.1.0-beta.46` | 2026-11-13 | [`docs/changelog/0.1.0-beta.46.md`](docs/changelog/0.1.0-beta.46.md) |
| `0.1.0-beta.45` | 2026-11-12 | [`docs/changelog/0.1.0-beta.45.md`](docs/changelog/0.1.0-beta.45.md) |
| `0.1.0-beta.44` | 2026-11-12 | [`docs/changelog/0.1.0-beta.44.md`](docs/changelog/0.1.0-beta.44.md) |
| `0.1.0-beta.43` | 2026-11-12 | [`docs/changelog/0.1.0-beta.43.md`](docs/changelog/0.1.0-beta.43.md) |
| `0.1.0-beta.42` | 2026-11-12 | [`docs/changelog/0.1.0-beta.42.md`](docs/changelog/0.1.0-beta.42.md) |
| `0.1.0-beta.41` | 2026-11-12 | [`docs/changelog/0.1.0-beta.41.md`](docs/changelog/0.1.0-beta.41.md) |
| `0.1.0-beta.40` | 2026-11-12 | [`docs/changelog/0.1.0-beta.40.md`](docs/changelog/0.1.0-beta.40.md) |
| `0.1.0-beta.39` | 2026-11-11 | [`docs/changelog/0.1.0-beta.39.md`](docs/changelog/0.1.0-beta.39.md) |
| `0.1.0-beta.38` | 2026-11-11 | [`docs/changelog/0.1.0-beta.38.md`](docs/changelog/0.1.0-beta.38.md) |
| `0.1.0-alpha.4` | 2026-11-11 | [`docs/changelog/0.1.0-alpha.4.md`](docs/changelog/0.1.0-alpha.4.md) |
| `0.1.0-beta.37` | 2026-11-11 | [`docs/changelog/0.1.0-beta.37.md`](docs/changelog/0.1.0-beta.37.md) |
| `0.1.0-beta.36` | 2026-11-10 | [`docs/changelog/0.1.0-beta.36.md`](docs/changelog/0.1.0-beta.36.md) |
| `0.1.0-beta.35` | 2026-11-09 | [`docs/changelog/0.1.0-beta.35.md`](docs/changelog/0.1.0-beta.35.md) |
| `0.1.0-alpha.3` | 2026-11-06 | [`docs/changelog/0.1.0-alpha.3.md`](docs/changelog/0.1.0-alpha.3.md) |
| `0.1.0-beta.34` | 2026-11-06 | [`docs/changelog/0.1.0-beta.34.md`](docs/changelog/0.1.0-beta.34.md) |
| `0.1.0-alpha.2` | 2026-11-03 | [`docs/changelog/0.1.0-alpha.2.md`](docs/changelog/0.1.0-alpha.2.md) |
| `0.1.0-beta.33` | 2026-11-03 | [`docs/changelog/0.1.0-beta.33.md`](docs/changelog/0.1.0-beta.33.md) |
| `0.1.0-alpha.1` | 2026-10-31 | [`docs/changelog/0.1.0-alpha.1.md`](docs/changelog/0.1.0-alpha.1.md) |
| `0.1.0-beta.32` | 2026-10-31 | [`docs/changelog/0.1.0-beta.32.md`](docs/changelog/0.1.0-beta.32.md) |
| `0.1.0-beta.31` | 2026-10-26 | [`docs/changelog/0.1.0-beta.31.md`](docs/changelog/0.1.0-beta.31.md) |
| `0.1.0-beta.30` | 2026-10-26 | [`docs/changelog/0.1.0-beta.30.md`](docs/changelog/0.1.0-beta.30.md) |
| `0.1.0-beta.29` | 2026-10-25 | [`docs/changelog/0.1.0-beta.29.md`](docs/changelog/0.1.0-beta.29.md) |
| `0.1.0-beta.28` | 2026-10-20 | [`docs/changelog/0.1.0-beta.28.md`](docs/changelog/0.1.0-beta.28.md) |
| `0.1.0-beta.27` | 2026-10-20 | [`docs/changelog/0.1.0-beta.27.md`](docs/changelog/0.1.0-beta.27.md) |
| `0.1.0-beta.26` | 2026-10-19 | [`docs/changelog/0.1.0-beta.26.md`](docs/changelog/0.1.0-beta.26.md) |
| `0.1.0-beta.25` | 2026-10-19 | [`docs/changelog/0.1.0-beta.25.md`](docs/changelog/0.1.0-beta.25.md) |
| `0.1.0-beta.24` | 2026-10-19 | [`docs/changelog/0.1.0-beta.24.md`](docs/changelog/0.1.0-beta.24.md) |
| `0.1.0-beta.23` | 2026-10-14 | [`docs/changelog/0.1.0-beta.23.md`](docs/changelog/0.1.0-beta.23.md) |
| `0.1.0-beta.22` | 2026-10-13 | [`docs/changelog/0.1.0-beta.22.md`](docs/changelog/0.1.0-beta.22.md) |
| `0.1.0-beta.21` | 2026-10-13 | [`docs/changelog/0.1.0-beta.21.md`](docs/changelog/0.1.0-beta.21.md) |
| `0.1.0-beta.20` | 2026-10-12 | [`docs/changelog/0.1.0-beta.20.md`](docs/changelog/0.1.0-beta.20.md) |
| `0.1.0-beta.19` | 2026-10-12 | [`docs/changelog/0.1.0-beta.19.md`](docs/changelog/0.1.0-beta.19.md) |
| `0.1.0-beta.18` | 2026-10-07 | [`docs/changelog/0.1.0-beta.18.md`](docs/changelog/0.1.0-beta.18.md) |
| `0.1.0-beta.17` | 2026-10-07 | [`docs/changelog/0.1.0-beta.17.md`](docs/changelog/0.1.0-beta.17.md) |
| `0.1.0-beta.16` | 2026-10-06 | [`docs/changelog/0.1.0-beta.16.md`](docs/changelog/0.1.0-beta.16.md) |
| `0.1.0-beta.15` | 2026-10-06 | [`docs/changelog/0.1.0-beta.15.md`](docs/changelog/0.1.0-beta.15.md) |
| `0.1.0-beta.14` | 2026-10-06 | [`docs/changelog/0.1.0-beta.14.md`](docs/changelog/0.1.0-beta.14.md) |
| `0.1.0-beta.13` | 2026-10-04 | [`docs/changelog/0.1.0-beta.13.md`](docs/changelog/0.1.0-beta.13.md) |
| `0.1.0-beta.12` | 2026-10-04 | [`docs/changelog/0.1.0-beta.12.md`](docs/changelog/0.1.0-beta.12.md) |
| `0.1.0-beta.11` | 2026-10-03 | [`docs/changelog/0.1.0-beta.11.md`](docs/changelog/0.1.0-beta.11.md) |
| `0.1.0-beta.10` | 2026-09-28 | [`docs/changelog/0.1.0-beta.10.md`](docs/changelog/0.1.0-beta.10.md) |
| `0.1.0-beta.9` | 2026-09-26 | [`docs/changelog/0.1.0-beta.9.md`](docs/changelog/0.1.0-beta.9.md) |
| `0.1.0-beta.8` | 2026-09-25 | [`docs/changelog/0.1.0-beta.8.md`](docs/changelog/0.1.0-beta.8.md) |
| `0.1.0-beta.7` | 2026-09-23 | [`docs/changelog/0.1.0-beta.7.md`](docs/changelog/0.1.0-beta.7.md) |
| `0.1.0-beta.6` | 2026-09-23 | [`docs/changelog/0.1.0-beta.6.md`](docs/changelog/0.1.0-beta.6.md) |
| `0.1.0-beta.5` | 2026-09-22 | [`docs/changelog/0.1.0-beta.5.md`](docs/changelog/0.1.0-beta.5.md) |
| `0.1.0-beta.4` | 2026-09-21 | [`docs/changelog/0.1.0-beta.4.md`](docs/changelog/0.1.0-beta.4.md) |
| `0.1.0-beta.3` | 2026-09-21 | [`docs/changelog/0.1.0-beta.3.md`](docs/changelog/0.1.0-beta.3.md) |
| `0.1.0-beta.2` | 2026-09-17 | [`docs/changelog/0.1.0-beta.2.md`](docs/changelog/0.1.0-beta.2.md) |
| `0.1.0-beta.1` | 2026-09-16 | [`docs/changelog/0.1.0-beta.1.md`](docs/changelog/0.1.0-beta.1.md) |

The ledger kept before the first tag (2026-08-04 → 2026-09-16), every pull request in full, newest first:

- [`docs/changelog/ledger-before-0.1.0-beta.1/part-1.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-1.md) — 455 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-2.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-2.md) — 805 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-3.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-3.md) — 954 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-4.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-4.md) — 990 lines
- [`docs/changelog/ledger-before-0.1.0-beta.1/part-5.md`](docs/changelog/ledger-before-0.1.0-beta.1/part-5.md) — 1218 lines

[Unreleased]: https://github.com/poli0981/frameledger/commits/main
