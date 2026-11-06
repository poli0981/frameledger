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

- **DLSS 5 is named where the game ran it** (owner decision D84, amending D64). The owner's Hogwarts Legacy, Neural
  Rendering on, loaded DLSS 5's runtime (`nvngx_dlssnr.dll` 310.9.16.0, from the game's folder) and the session could say
  only *DLSS 5 runtime loaded* — nothing measured whether it ran. The Overlay's Streamline hook now decodes DLSS 5's
  feature (`kFeatureDLSS_NR`, Streamline 2.14) into its own per-frame fact, and the Agent keeps the share of the frames
  the game rendered through Streamline that carried it (`sessions.dlss_nr_pct`, **schema 0022** — a ledger this build
  migrated no longer opens in an earlier one). Seen running, the DLSS line reads *DLSS 5 (neural rendering on 97.3% of
  rendered frames)*, above every other generation; loaded and seen not running, *DLSS 5 runtime loaded, not seen
  running*; where nothing could tell (an earlier Overlay, a title outside Streamline), *DLSS 5 runtime loaded*, as
  before. Its library is listed with its version and source like the others, its Streamline plugin `sl.dlss_nr.dll`
  is recorded when loaded, and a game shipping either file shows *Supports DLSS 5 Neural Rendering* (rules
  `2026.11.2`, capability `dlss_nr`). The **Privacy Policy 2.14** names neural-rendering libraries among those whose
  versions are read; the **Disclaimer 2.13**'s accuracy block says when DLSS 5 is named. One hook-path change: one more
  compare in the `slEvaluateFeature` detour and one more test where a present is recorded (`spike-notes` §26).
- **Streamline's headers are `v2.14.16`'s** (developers): the vendored copy was `v2.12.0`, byte for byte; three of its
  eleven files change — `kFeatureDLSS_NR`, the uplift buffer types 70–72, `kStructVersion7`, the version — and
  nothing FrameLedger consumed otherwise (`src/native/third_party/streamline/README.md`).
- **A session under the NVIDIA App's *Dynamic* frame generation reads DLSS 4.5** (owner decision D83, amending D64).
  Dynamic and its ×6 mode exist only in DLSS 4.5, and the DLSS line named a generation from the measured multiplier
  alone: the owner's Phantom Blade Zero under *Dynamic, up to ×3* held ×2.0 and ×3.0 and read *DLSS 4 (a ×3.0
  frame-generation state)*. It reads *DLSS 4.5 (Dynamic multi frame generation, set in the NVIDIA App)* where the game's
  driver profile sets Dynamic, the driver reports its frame-generation override applied in the game's process, and DLSS
  Frame Generation was counted; a state measured at ×5 or more still ranks first, and a *Fixed* multiplier names nothing
  by itself (The Witcher 3 under Fixed ×6 still presented ×2.0). The game page's chip and the CSV's `# dlss:` line
  (`basis=dynamic_frame_generation`) follow; the **Disclaimer 2.13** says so in its accuracy block
  (`src/FrameLedger.Domain/Detection/DlssGeneration.cs`, `src/FrameLedger.App/Services/DlssText.cs`; `spike-notes` §26).
- **The guard's source reads in order again** (developers only; no behaviour changes): the note that says what runs only
  for a target the anti-cheat evaluation allowed sits above the function it describes again — beta.33's two isolation
  checks had come between them — and names that isolation check among what runs
  (`src/native/FrameLedger.Injector/src/fl_guard.cpp`).

### Fixed

- **An Xbox game's page no longer fails on a strange picture name** in the game's own `MicrosoftGame.config` (beta.33's):
  a name that points at a stream of a file (`a:b.png`) made the search for its scaled variants throw — the page then
  showed no picture and logged an error, and an action on the page that reloads it could end in the crash report. Such a
  name is no picture a package ships, so it is passed over like a name outside the package
  (`src/FrameLedger.Infrastructure/Art/XboxArtCache.cs`). Found by a self-review of #351 and #352 after their gates
  passed; no install of the owner's eleven titles has such a name.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
