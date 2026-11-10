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

- **FrameLedger runs on .NET 11** (owner decisions D94 and D95, 2026-11-11). The betas are built on .NET 11.0.0 —
  released 2026-11-10 — where they were built on .NET 10: SDK 11.0.100, `net11.0-windows10.0.22621.0`, and the three
  libraries serviced with the runtime (`Microsoft.Extensions.Hosting`, `Microsoft.Data.Sqlite`,
  `System.Diagnostics.EventLog`) at 11.0.0. The runtime is inside the package as before; nothing is installed beside
  it. **What a user should know:** Microsoft supports .NET 11 on Windows 11, and on Windows 10 only in its LTSC and
  IoT editions — FrameLedger does not block Windows 10 22H2, and it is untested and unsupported there; **on a
  processor without SSE4.2 and POPCNT it no longer starts** (every processor Windows 11 accepts has them); the
  ledger's schema is unchanged, so installing 0.1.0-beta.36 again is a way back. The part of FrameLedger loaded into
  a game is native code and does not change. `docs/12_BUILD.md` §The main line's .NET says what decided the move
  and what was set aside.
- **The capture agent ends with a crash report when a piece of its own background work fails**, where it used to
  stop and exit as if nothing had happened: .NET 11 passes that failure on, and the Agent's existing crash path — a
  Fatal log entry and a crash dump — takes it. Nothing was added for it (`docs/10_LOGGING_AND_BUG_REPORTS.md` §Crash
  handling).
- **The NuGet licence texts follow the package set** (`legal/licenses/nuget/`, 69 packages where there were 78):
  nine `Microsoft.Extensions.*` libraries are part of the .NET 11 runtime, and SQLite's engine is SourceGear's
  `SQLite` package through SQLitePCLRaw 3.0.5.
- **For developers:** `release.yml` installs a .NET 10 runtime beside the SDK for `vpk` 1.2.161, which has no build
  that runs on .NET 11 alone; Qodana's container installs the .NET 11 SDK before it builds (an interim, until the
  Ultimate linter); two `string.StartsWith`/`EndsWith(char)` calls in the App's tests name the comparison .NET 11's
  analyzer asks for; `HostBackgroundFailureTests` holds .NET 11's `IHost.RunAsync` behaviour.
- **What an alpha is, now that .NET 11 has shipped** (owner decisions D97 and D98). Until 0.1.0-alpha.3 an alpha was
  "a beta's source built on a prerelease .NET", and every text said so. 0.1.0-alpha.4 runs on .NET 11's release — the
  betas' own from this version — so an alpha is now described as *a beta's source built to try a newer version of
  .NET, at times a prerelease one*: **EULA 1.7** (§2B), **Disclaimer 2.14** (§0, which also gains a paragraph on
  Windows and the processor), **Alpha notice 1.1** (what an alpha is, the hardware, and a new part on the pause), the
  accuracy statement's line, the guide and `LIMITATIONS.md`. The terms window shows the EULA and the Disclaimer once
  more; an alpha asks for its notice's tick again.
- **The alphas pause after 0.1.0-alpha.4, and a paused alpha says so** (D98). With the betas on the same .NET there is
  nothing for an alpha to try, so none is published until a newer .NET has a preview — and a paused alpha gets no
  update, the anti-cheat list included, which reaches FrameLedger only inside one. An alpha cut while the line rests
  shows, above every page, that the line is paused and to install the latest beta; the words "a prerelease .NET" now
  appear only in an alpha that really runs on one. For developers: `FlAlphaLineRests` in `Directory.Build.props`,
  stamped into every assembly, `Shared.AlphaLine`, `App/Services/AlphaTexts`; three new strings in en, vi and ja.
- **.NET 11's API where it reads better, and no C# 15 syntax yet** (developers only; owner decision D96). Sixteen
  `char` searches name their comparison through the `string` overloads .NET 11 adds
  (`StartsWith`/`EndsWith`/`LastIndexOf(char, StringComparison)`) where they went through a span to satisfy both .NET
  10 and 11, and the App finds the Agent it started with `Process.TryGetProcessById` instead of a caught exception;
  nothing changes in what the software does. **C# 15's constructs were asked for and are not used**: each was tried in
  a file of its own, and ReSharper's engine — what Qodana runs — reads none of the four (it does not parse `closed`
  or `union`, and calls collection expression arguments and labeled jumps preview features), so every file using one
  would be an error on a gate of zero; a union also draws a warning from the SDK's own analyzers.
  `docs/13_CI_CD.md` §C# 15 and the analyses holds the table and what unblocks each; `docs/spike-notes/29-…` where
  each would go. `codeql.yml` takes `workflow_dispatch`, so a branch can have a full analysis — a pull request's C#
  analysis is an overlay on `main`'s database.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
