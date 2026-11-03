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

- **Games from the Xbox app and the Microsoft Store** (the owner's request of 2026-11-03, owner decisions D77–D81; PC Game
  Pass games included): **Import library** finds the games those apps installed into a games folder — each drive's
  `.GamingRoot` names the folders, each game's own `MicrosoftGame.config` and `appxmanifest.xml` say what it is, and
  Windows says whether its package is installed for you — and lists them as **Xbox**, with the process that actually
  draws (an Unreal title's shipping executable, not the stub its package launches; never the GDK's `gamelaunchhelper.exe`).
  A game's page shows its own square logo and splash image, read from its folder, and its package version (*Xbox version
  1.3.5124.0*). Only games built on Microsoft's GDK are listed; an older Store app (UWP) is not (`docs/05_DETECTION.md`
  §Platform signatures & metadata, `LIMITATIONS.md`, the user guide). Everything was measured first, on the owner's eleven
  titles: the layout, and what Windows lets another program read there — every game executable refuses to be read, so no
  Unreal Engine version is shown for these games (`docs/spike-notes/25-xbox-gdk.md`).
- **The detection rules know Xbox games** (rules `2026.11.1`, so every game in the library is read again once): an `xbox`
  platform, first in the list — a GDK FINAL FANTASY ships Steam's DLL and read *Steam* — decided by a new signal,
  `root_file`, from the install folder's own listing, so a very large install whose deep walk ran short still reads as
  its store (`docs/05_DETECTION.md`).

### Changed

- **The Privacy Policy is 2.13**, shown once at the next start: it names what the Xbox library reads — the games folders
  `.GamingRoot` names, each game's package files, Windows' answer whether its package is installed for you, and the
  pictures a game ships in its own folder — that Play asks Windows to start such a game, and that before any injection
  FrameLedger reads whether a game runs in an app container and which of Windows' code protections it runs under. The
  EULA and the Disclaimer do not change.

### Fixed

- **The anti-cheat check saw only part of an Xbox game's folder** (safety): no install-root boundary matched the
  `<games folder>\<Game>\Content\` layout, so for an Unreal title's executable, three levels down, check 4 walked only its
  `Binaries\WinGDK` folder, and anti-cheat at the root of `Content` would have read clean. A GDK package root — the folder
  holding `MicrosoftGame.config` beside `appxmanifest.xml` — is now a boundary for the guard and for detection alike, used
  only where no store boundary matched, so it can only widen what is walked. The guard also gained `GamesInstalled\<X>`,
  which detection had had alone since #17; one case list read by both test suites now holds the two resolvers together
  (`docs/19_SAFETY_AND_ANTICHEAT.md`, `tests/fixtures/install-roots/cases.txt`).
- **The second alpha's release would have stopped at its delta check** (for developers): Velopack names a non-default
  channel's packages with the channel (`FrameLedger.App-<version>-alpha-delta.nupkg`), and `release.yml` looked for the
  default channel's name. It now takes the delta the pack listed in `assets.<channel>.json`; the betas' packages are named
  as before (seen on the `0.1.0-alpha.1` rehearsal, which had no delta to make).

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
