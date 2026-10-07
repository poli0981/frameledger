# Releasing

The full documents are [`docs/13_CI_CD.md`](../docs/13_CI_CD.md) and [`docs/11_UPDATER.md`](../docs/11_UPDATER.md).

## The workflows

| Workflow | Runs on | Does |
|---|---|---|
| `.github/workflows/ci.yml` | pushes to `main`, pull requests | `./build.ps1 check`, and on a pull request the changelog check. |
| `.github/workflows/codeql.yml` | pushes and pull requests to `main`, weekly | CodeQL for C# and C++. |
| `.github/workflows/rules-publish.yml` | a change to the detection rules or their validator | Fails when an anti-cheat entry is removed. |
| `.github/workflows/release.yml` | a pushed `v*` tag; by hand, a rehearsal | Builds, gates and publishes a release. |
| `.github/workflows/wiki.yml` | a successful release; by hand | Builds this wiki from `wiki/` and `guide/` and publishes it to the Wiki tab. |

## Cutting a release

1. One pull request moves the `[Unreleased]` entries of [`CHANGELOG.md`](../CHANGELOG.md) under a `## [x.y.z]` or
   `## [x.y.z-beta.N]` heading with an *Updating from* note, and updates the release lines in `README.md`, the accuracy
   statement (`legal/ACCURACY.md` and its copies) and `LIMITATIONS.md`. `VERSION` changes only with the numeric core.
2. After it merges, a signed tag `vx.y.z` (or `vx.y.z-beta.N`) on the merge commit starts `release.yml`:
   - it checks the tag against `VERSION` and that the changelog has the section;
   - writes the release date into the legal documents;
   - runs the whole gate;
   - publishes the App and the agent (self-contained, ReadyToRun) and checks the published tree;
   - packs them with Velopack, with a delta from the previous release;
   - writes `SHA256SUMS.txt`;
   - creates the GitHub release from the changelog section, marked pre-release for a beta.
3. The wiki follows: `wiki.yml` runs when the release run succeeds.
4. Check the published assets against `SHA256SUMS.txt`, and the update feed.

Releases are not code-signed, so every release says so and publishes its checksums.

## How users get it

Velopack reads GitHub Releases. Settings ▸ Updates ▸ Channel is *Automatic* by default: a pre-release looks for
pre-releases, a release for releases. A new version downloads in the background and installs only when FrameLedger
restarts — never while a game is being recorded.
