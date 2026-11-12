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

- **Advanced metrics, inside the game: what the game tells NVIDIA Streamline about each frame** (the part inside the
  game; what a session shows of it comes with the same release). With **Advanced metrics** on, FrameLedger now also
  reads, from a call a Streamline game makes every frame, the size of the jitter, the motion-vector scale, the depth
  range, the range of the field of view, the aspect ratio and seven on/off settings (inverted depth, how motion
  vectors are stored, whether the upscaler's history was reset). It never reads where the game's camera is, which
  way it looks, or its matrices, which sit in the same data — a check in the build refuses any code that names
  them. This is the first reading that needs a hook of its own, and that hook is put into a game only once
  Advanced metrics has been asked for: with the option off, nothing in the game is touched for it. Measured cost:
  about 20 billionths of a second per frame while the option is on, under one billionth while it is off.

### Fixed

- **FSR upscaling is counted once per frame whichever way the game's FidelityFX loader passes it on.** Newer
  FidelityFX loaders hand a call on to the library behind them in a way that brought the same upscale to
  FrameLedger twice; with frame generation off, the count of frames the game rendered would have doubled — Native
  FPS twice too high. Seen in how Cyberpunk 2077's loader passes on its frame-generation calls; no game tested so
  far upscales through such a loader, so this is a precaution. The upscale is now counted where the game calls it,
  and nowhere else. (Owner decision D125. Measured cost: none that can be told from noise.)

- **A game is no longer left unrecorded because it was still loading when FrameLedger looked at it.** Before it
  hooks a game, and again right after, FrameLedger lists the modules the game has loaded and checks them against
  the anti-cheat list. A game that is starting loads and unloads modules for several seconds, and when the list
  changed in the middle of being read FrameLedger treated it as unreadable and stopped: the session ended as a
  safety stop with nothing recorded, and the game ran on unmeasured until it was restarted. It now reads the list
  again — up to three more times, a quarter of a second apart — and only stops if the list never holds still. What
  stops a session has not changed: an anti-cheat module found in any read stops it at once, a protected process
  still stops it at once, and a list that cannot be read in the end is still a stop, never a pass. (Owner decision
  D123; seen five times in one PC's logs in a week.)

- **A test of the Overlay's log reads its lines without `strlen`.** The helper that looks for a line in the log
  took its text as C strings and measured one with `strlen`; it takes strings now. Test code only: nothing in the
  installed software changes. (Found by SonarCloud after 0.1.0-beta.42 was merged.)

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
