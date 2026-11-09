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

- **An accessibility statement** (owner request 2026-11-09, decision D88). `ACCESSIBILITY.md` says what FrameLedger aims
  for — WCAG 2.2 at level AA as W3C's WCAG2ICT applies it to desktop software, a goal and not a claim of conformance —
  what works with the keyboard alone, a screen reader, a contrast theme or Magnifier, how it is checked (the project's
  tests, and the owner's pass of 2026-11-09: keyboard only and Narrator on 0.1.0-beta.34 and 0.1.0-alpha.3, no fault),
  what does not work yet and what to do instead, and how a barrier is reported and handled — by severity, with no
  promised times. The app shows it under **Help ▸ Accessibility**, and after Limitations in the user guide's window; the
  wiki gets an *Accessibility* page.
- **An *Accessibility problem* issue form** (`.github/ISSUE_TEMPLATE/accessibility.yml`) asking what was tried, what
  happened and what the reporter uses — never anything about the reporter themself — and a user-interface section in the
  pull-request template. The `accessibility` label exists, and so do `safety` and `priority-high`, which the *Safety gap*
  form had named since its first day without either existing — so GitHub had never applied them.

- **A screen reader reads what the window shows** (decision D88's goal; measured in the UI Automation tree WPF builds on
  every page, window and dialog, every tab — `PagesLoadTests.Automation`). Drop-down options were read as an internal
  record (*Choice { Value = dark, Label = Dark }*), lists and table rows by their program names, table cells drawn by a
  template as *Item: …, Column Display Index: n*, and the terms window's list by each document's whole text; each now
  reads the words it shows, and an empty cell reads *empty*. The frame-rate readout says Native, Displayed and the
  factor together (CLAUDE.md rule 6, in speech), a ray-tracing chip its value and where it came from, a change badge its
  number. The safety notices' title and message reach a screen reader (they were hidden inside the notice's template),
  and the notice's button says which notice it answers. A button repeated on each row names its row (*Open: …*, *Play:
  …*, *Turn off: …*). Tab no longer stops on pictures, lists, Settings' rows, the FPS readout, the chips or the dialog
  host, where it found nothing to say; an expander's button is named after its header.

- **A screen reader is told what appears** (owner decision D89): a safety notice as it arrives, the capture agent going
  offline and coming back, an update's steps (not each percent), every short message at the bottom of the window, a
  notice on a game's page and each result of Tools ▸ Database maintenance — in the words shown, and only while
  FrameLedger's window is the active one, so a game that has the screen is never interrupted. The database line's live
  region, which announced nothing, is gone.

- **Headings and charts a screen reader can use** (owner decision D90). Every page's title is a first-level heading and
  each section title a second-level one, so a screen reader's heading keys move between sections. Every chart says what
  it is and where its numbers are written as text; its keys — arrows to move, Ctrl with an arrow to zoom, A to see all of
  it — now stay with the chart instead of also scrolling the page or moving the focus on, and its focus is drawn. No
  chart tells two series apart by colour alone any more: Compare's five sessions, the GPU's temperature and its hot spot,
  the two kinds of stutter, the two lows and the two limits each have their own line pattern, marker or hatch.

### Fixed

- **A double-click on a chart shows all of it again**, as the user guide said; it toggled ScottPlot's render-time overlay.

### Changed

- **Documents corrected**: charts do take focus and keys (`docs/08_UI.md` said neither — `ChartKeyboardTests` measures
  it); the database-maintenance result's live region announced nothing, since WPF raises one only when the app does; the
  "no Narrator pass" gap is the owner's pass now, beside what that pass could not show; the open question "Accessibility
  / DPI: no design anywhere" is closed; NFR-9 names screen readers and contrast themes (D88–D91). `LIMITATIONS.md` gains an
  *Accessibility* section and loses its one struck sentence — a screen reader reads struck text as current, so no page
  the app shows strikes text through. The guide's menu table names its menu on every row, read one row at a time.

## Releases

Newest first. Each file holds that release's section exactly as it stood here.

| Version | Date | Notes |
|---|---|---|
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
