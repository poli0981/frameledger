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

### Added

- **Every frame-generation state a session held, in its summary** (owner request 2026-10-19, decision D62). Under the
  figures, *Frame generation in this session* lists each state — `×2.0`, `×3.0`, `×4.0`, `×6.0`… — with its own
  Native → Displayed and its share of the time to two decimals, then the time with no frame generation and the time no
  state explains (*Not classified*: switching, loading, a multiplier that changed faster than five seconds), above a bar
  of those shares. A line above them says how frame generation was set: in the NVIDIA App (*Dynamic, up to ×6, target:
  max refresh rate*, *Fixed ×4*, from the driver profile stored with the session) or in the game. Each state is read
  over the same five-second windows as the steady state and lasts at least 10 seconds; nothing is averaged across
  states (rule 6). Stored with the session (schema 0020, `sessions.fg_mix`): a session recorded before has none, and
  its summary says so in one line. The CSV export gains a `# fg_mix:` line; the JSON carries the column. The FG×
  column's two formats are translated strings now, and the driver-profile line's automatic dynamic target reads as the
  NVIDIA App says it: *max refresh rate*.

### Changed

- **SonarCloud analyses only FrameLedger's own code** (developers only; owner request 2026-10-19, decision D67). A
  `.sonarcloud.properties` at the root keeps the vendored SDK headers, generated files, test fixtures, licence texts and
  the SQLite migrations out of the analysis and reports the native code against C++20; `docs/13_CI_CD.md` §SonarCloud
  records the triage of the 341 issues the last analysis left — the rules this project breaks by design and why, the
  false positives, and the ones the next change fixes — with the steps to mark them in SonarCloud.
- **The long documents are split** (developers only; owner request 2026-10-19, decision D66). `CHANGELOG.md`
  keeps `[Unreleased]` and a table of the releases; each release's section now lives in its own file,
  `docs/changelog/<version>.md`, and the ledger kept before the first tag in five parts — moved verbatim: the notes
  `tools/release-notes.ps1` takes for each of the 23 releases are byte for byte what they were, and it reads the
  version's own file. `docs/HANDOFF.md` (2,844 lines, now 299) keeps what to do next, the open owner-only items and an
  index of every owner decision; the history of each phase and train, the passed owner-only items and the traps
  moved to `docs/handoff/`, and a table there resolves every section the code and the documents cite. A new gate,
  `tools/docs-split-check.ps1`, keeps each index and its files naming each other.
- **The open questions and the measurement notes are split too** (developers only, D66). `docs/20_OPEN_QUESTIONS.md`
  (4,467 lines, now 2,008) keeps its rules, §S24 and every entry not resolved; the 30 entries §S24 marks resolved
  moved, with their reasoning and in their order, to `docs/open-questions/closed-s.md` and `closed-h.md`, and a table
  at the end of each series lists them. `docs/spike-notes.md` (2,816 lines) is now the index of its 24 sections, each
  in its own file under `docs/spike-notes/`; a new measurement gets the next number, its own file and a row. Both
  rebuild their originals byte for byte, and the same gate holds their indexes to their files.

### Fixed

- **Opening a folder in Explorer** — the logs, the data folder, the licences, a bug report's zip and the folder named
  after a failed start — starts `explorer.exe` from the Windows folder by its full path, never a program of that name
  found on the search path, and always hands it the path in quotes (three of the five places passed it bare).
- **What SonarCloud found that was real** (developers only): the one third-party GitHub Action is pinned to its
  commit; two string reads in native code are bounded by their buffer (the guard's rules-file path handed to the Agent,
  and the driver's branch name the NVAPI bridge copies); the pipe server's accept loop states that it ends only when
  cancelled; a launch's own session names its absent stop token (`CancellationToken.None`, with the reason) where it
  wrote `default` — the same value, now said on purpose; an unused overload of
  the process launcher that hid the other is gone; the identifier `module` (a C++20 keyword in some positions) is
  renamed in the hook layer — a rename only, no code changes; and a hook-overhead probe no longer uses a compound
  assignment on a `volatile`, which C++20 deprecates. The analyzers Dependabot updates in #314 flag the first-start
  window's own answer task and one test fake's synchronous wait; both are suppressed with their reasons, so that update
  can merge.
- **The game-side log keeps every line a stop writes.** When hooking stopped on a game's own drawing thread — the
  capture agent's safety stop takes effect within one frame there — the Overlay's last write of its log could happen
  while that thread was still listing the hooks it removed, and the last of them never reached the file
  (`overlay-*.log`, the file a bug report carries). The last write now waits, at most two seconds, for the stop to
  finish and ends with a `STOP_FLUSHED` line; a line still being written is never printed half-done. Nothing changes
  on the per-frame path. This was the native test that went red under load since beta.9 (`fl_guard`'s log case),
  now run 20 times with every processor busy.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
