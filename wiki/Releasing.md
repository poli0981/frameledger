# Releasing

The full documents are [`docs/13_CI_CD.md`](../docs/13_CI_CD.md) and [`docs/11_UPDATER.md`](../docs/11_UPDATER.md).

## The workflows

| Workflow | Runs on | Does |
|---|---|---|
| `.github/workflows/ci.yml` | pushes to `main` and `alpha`, pull requests | `./build.ps1 check`, and on a pull request the changelog check; then, in a job of its own, the SonarCloud analysis with the gate's coverage (settings: `SonarQube.Analysis.xml`). |
| `.github/workflows/codeql.yml` | pushes and pull requests to `main`, weekly | CodeQL for C#, C++ and the workflows themselves. |
| `.github/workflows/qodana.yml` | pushes and pull requests to `main`; by hand | Qodana Community for .NET (ReSharper's inspections) over the C# and the XAML; fails on any problem, and blocks nothing (settings: `qodana.yaml`, and `.editorconfig` for the inspections). |
| `.github/workflows/rules-publish.yml` | a change to the detection rules or their validator | Fails when an anti-cheat entry is removed. |
| `.github/workflows/release.yml` | a pushed `v*` tag; by hand, a rehearsal | Builds, gates and publishes a release. |
| `.github/workflows/wiki.yml` | a successful release; by hand | Builds this wiki from `wiki/` and `guide/` and publishes it to the Wiki tab. |

## Cutting a release

1. One pull request moves the `[Unreleased]` entries of [`CHANGELOG.md`](../CHANGELOG.md) into a new file,
   `docs/changelog/<version>.md`, under a `## [x.y.z]` or `## [x.y.z-beta.N]` heading with an *Updating from* note,
   adds its row to the Releases table in `CHANGELOG.md`, and updates the release lines in `README.md`, the accuracy
   statement (`legal/ACCURACY.md` and its copies), `LIMITATIONS.md` and `ACCESSIBILITY.md`. `VERSION` changes only with
   the numeric core.
2. After it merges, a signed tag `vx.y.z` (or `vx.y.z-beta.N`) on the merge commit starts `release.yml`:
   - it checks the tag against `VERSION` and that the changelog has the section;
   - reads the build id from the clean checkout, before anything is edited;
   - writes the release date into the legal documents;
   - runs the whole gate;
   - publishes the App and the agent (self-contained, ReadyToRun) and checks the published tree, the build id in the
     native binaries included;
   - packs them with Velopack, with a delta from the previous release;
   - writes `SHA256SUMS.txt`;
   - creates the GitHub release from the changelog section, marked pre-release for a beta.
3. The wiki follows: `wiki.yml` runs when the release run succeeds.
4. Check the published assets against `SHA256SUMS.txt`, and the update feed.

Releases are not code-signed, so every release says so and publishes its checksums.

## Cutting an alpha

An alpha is a beta's source built to try a newer .NET — at times a prerelease — from the long-lived `alpha` branch, which
differs from `main` only where [`docs/12_BUILD.md`](../docs/12_BUILD.md) §The alpha line lists (its `RELEASE_LINE` says
`alpha`). **The line rests since its first alpha on .NET 11's release** (2026-11-11): the betas build on the same .NET 11, the branch differs
from `main` in `RELEASE_LINE` alone, and no alpha is cut until a newer .NET has a preview. While the line tries a .NET,
one alpha follows each beta, never several in a row ([`docs/13_CI_CD.md`](../docs/13_CI_CD.md) §The alpha line):

1. A pull request on `main` adds the alpha's notes, `docs/changelog/<version>.md`, opening with the experimental-build
   warning and naming the beta it is cut from, and its row in the Releases table. No release line moves: README, the
   accuracy statement, `LIMITATIONS.md` and `ACCESSIBILITY.md` keep naming the beta.
2. A pull request merges `main` into `alpha` with a merge commit; CI runs the gate there on the SDK the branch pins.
3. A rehearsal of `release.yml` on `alpha`, then a signed tag `vx.y.z-alpha.N` on its head. The workflow refuses any
   other tag on that branch, and an alpha's tag anywhere else; it packs on Velopack's `alpha` channel, so no beta is
   offered the alpha and no alpha a beta.
4. The wiki does not change: it shows the newest beta.

## How users get it

Velopack reads GitHub Releases. Settings ▸ Updates ▸ Channel is *Automatic* by default: a pre-release looks for
pre-releases, a release for releases. An alpha reads only alphas, and its channel is locked. A new version downloads in the background and installs only when FrameLedger
restarts — never while a game is being recorded.
