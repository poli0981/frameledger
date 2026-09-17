# 13 — CI/CD

FrameLedger uses the **`poli0981/.github` ops repo** where its templates fit, and repo-local workflows where they do not. `ci.yml` is repo-local **by necessity, not preference**: the ops repo's `reusable-desktop-csharp.yml` runs `dotnet restore` / `build` / `test` directly and exposes no input for a native pre-step, MSVC setup, or the Vulkan SDK. FrameLedger's build is mixed-toolchain and native-first, and `12_BUILD.md` §Local quality gate commits to CI running *the identical script* as local — a promise a pure-managed template cannot keep. CodeQL is repo-local too (its own heading below says why), and so is `release.yml` since 2026-09-14 — the same native pre-step and the identical gate make a caller stub impossible for it as well; this sentence said both "still call the ops repo" until then.

> ⚠ Known gotcha (learned on earlier migrations): caller stubs **must declare explicit `permissions:` blocks** — permissions do not inherit into reusable workflows. Every stub below lists its own.

## Workflows (`.github/workflows/`)

### `ci.yml` — push to `main` + all PRs · **repo-local**
- `runs-on: windows-latest`, .NET SDK pinned by `global.json` via `actions/setup-dotnet`, MSVC via `ilammy/msvc-dev-cmd`, NuGet cached.
- Single step of substance: **`./build.ps1 check`** — the same script, with the same switches (none), a developer runs before pushing. The gate list lives in `12_BUILD` §Local quality gate, **once**, rather than being restated here where it goes stale.
- ~~**`-SkipIntegration` is the one place local and CI deliberately differ**~~ — **gone since 2026-09-06.** From 2026-08-05 CI passed it for a measured reason: §S16 puts the injecting process's ancestors in the scan set, a .NET test host loads `System.Security.Cryptography.ProtectedData.dll`, and the guard's `protect` fragment refused our own harness (§S19(b)). The signer half now verifies that module's embedded signature offline and reads `O=Microsoft Corporation` on the compiled-in bound, so the `Category=Integration` cases run here — **nine** when this was written, **20 across six classes** as of 2026-09-13 (Agent 6, CaptureHost 7, Infrastructure 7); count them with `grep -rl 'Category", "Integration' tests` rather than trusting either number — and a green CI IS evidence for the managed drain and the capture host's end-to-end behaviour. The switch remains in `build.ps1`, loud, for a developer who must.
- **The hosted runner refuses guard scans intermittently — THREE failures in eight gate runs, measured
  2026-09-10 while merging P2's stack.** Every one was an `Category=Integration` case, every one passed on
  a re-run of the identical commit, and none was caused by the code under review. Two distinct shapes, and
  the second is not §S19(b):
    - `ShmDrainIntegrationTests.AnUnhookRequestStopsTheCaptureSideAndTheDrainCanSeeIt` at
      `GuardedInjectAsync(...).IsAllowed` — the guard refusing our own harness, §S19(b)'s shape.
    - `CaptureHostEndToEndTests.WhenTheTargetExitsTheHostStopsAndSaysWhy` with
      **`ProcessTreeUnavailable — could not establish the scan set`**: the guard could not walk the
      ancestors at all. That is a *different* failure from the fragment/signer one, and §S19(b) does not
      cover it. A runner under load whose process enumeration fails is the obvious hypothesis and it is
      **untested** — nobody has instrumented it.
  So: §S19(b)'s refusal is **reduced, not gone**, and it is not the only way a scan fails here. The
  earlier version of this bullet said "one run in the low tens", written after the first failure and
  wrong by the end of the same evening; it is corrected rather than quietly deleted, because a frequency
  claim made from one observation is exactly the shape this file keeps catching.
  **Re-run before investigating**; if it reproduces, read the module list and the scan-set reason, not the test.
  - **A hang, not a refusal — the first tag run (2026-09-16, run 35111939019).** Every suite green except
    `Infrastructure.Tests`, which printed its "test run for …" line and nothing else for 58 minutes until the job's
    60-minute limit cancelled the run; the same suite took 42 s in the rehearsal an hour earlier. The log named no
    test. ~~`build.ps1` now passes `--blame-hang --blame-hang-timeout 15m --blame-hang-dump-type mini` to `dotnet test`,
    so the next hang fails the run in 15 minutes with the test's name in `TestResults\…\Sequence.xml` and a mini
    dump beside it — the diagnostic this run lacked. Which test hung is **unmeasured**; the re-run is the measurement.~~
    **Found and fixed 2026-09-17.** Reproduced locally one run in eight; the xUnit verbose reporter of a hung run showed
    thirteen tests started and none finished, one of them `CrashDumpWriterTests.WritesAMinidumpOfThisProcess…` — which
    called `MiniDumpWriteDump` on its OWN process. That call suspends every other thread of the caller, and one holding
    the heap or loader lock deadlocks the dumper: the whole test process froze, including xUnit's long-running-test
    timer, which is why nothing was ever named. 40 runs without that test: no hang. **The same call was the product's
    crash path**, in the App and the Agent — a real crash could have hung instead of dumping. Dumps are written by a
    child process now (`Infrastructure.Diagnostics.ParentDump`, `FrameLedger.Agent.exe --write-crash-dump`; `10_LOGGING`
    §Crash handling). And **`--blame-hang` did not stop the hung xUnit v3 executable** — still alive four minutes after
    a 60 s hang timeout — so `build.ps1` runs `dotnet test` under a 25-minute wall-clock limit instead, kills the tree on
    expiry, and names the test projects that wrote no `results.trx`.
  - **Two more shapes had one cause, found and fixed 2026-09-16** after four failures in one evening's merge train:
    `AKilledHostLeavesAPartialThatRecoverTurnsIntoAnInterruptedSession` ("the process cannot access the file …
    `.partial`") and `WithNoConsentRecordTheHostRefusesAndNothingIsEverInjected` (a leftover consent record). The
    suite's `Kill` helper returned before the killed host had released its files, so the next line read a `.partial`
    the dead process still held, and the ledger delete in `Dispose` silently failed and left the record for the next
    case. `Kill` now waits for exit (bounded, asserted) and the delete retries. Not §S19(b) either — a test-harness
    race, and the third flake shape of the day (the registry-subtree race between `VkLayerRegistrationTests` and the
    GOG reader's test, fixed in #188) was the same kind.
- **A re-run ERASES the evidence, which is why the count above had to be kept by hand.**
  `gh run rerun --failed` updates the original run's conclusion, so `gh run list` showed **18 success, 1
  cancelled, 0 failed** across the twenty runs that contained all three failures above. Anyone measuring
  this flake rate from the run history will measure zero. Read the job logs, or keep the tally as it
  happens.
- ~~**The step that would diagnose it runs AFTER the thing it diagnoses, so a gate failure skips it.**
  The signer probe below is step 8; the quality gate is step 7, and the job stops there. On the
  2026-09-10 failure the probe therefore printed nothing, which is the one run where its output would
  have named the refusing module. Moving it before the gate (or giving it `if: always()`) is a one-line
  change nobody has made, recorded here rather than done quietly in an unrelated PR.~~ **FALSE when
  written, corrected 2026-09-13.** The probe step has carried `if: always()` since #137 (`c5932cc`,
  2026-09-06) — four days *before* the failure this bullet describes — so a red gate does not skip it.
  Why the probe's output was not read on 2026-09-10 is therefore **unmeasured**: the run was re-run
  (`gh run rerun --failed`, next bullet), which replaced its logs, so the original step output is gone.
  The next Integration failure is the measurement: read the probe step *before* re-running. Struck rather
  than deleted because a session planned a workflow change on this bullet's word.
- **Signer probe step** (2026-09-06): `fl-probe-signer` runs as its own step after the gate — `continue-on-error`, a measurement and not a gate — because `ctest fl_signer_probe` passes on a regular expression and prints nothing, so §S19(b)'s CI leg was never readable from the log. Its answers are read off this step.
- Uploads coverage artifacts.
- **Changelog gate**, on pull requests only: the changed-file list comes from GitHub's own view of the PR — not `git diff`, so a shallow checkout cannot silently produce a short one — and an empty list is refused rather than read as "no `src/` changes". It is a **step of the required `check` job** and deliberately not a job of its own: `main`'s required contexts are exactly `check` and the two `analyze` jobs, so a new job would go red while the merge button stayed green, which is what already makes `Rules / validate` advisory (§S23-2).

> **Three claims in this section were false and are corrected 2026-08-06.** There is no Vulkan SDK
> step and there will not be one — the Khronos headers are vendored. `resx-audit` ~~does not exist and
> is skipped loudly, so no artifact of it is uploaded~~ (exists since 2026-09-13, P3 PR-2, as a hard step
> of the gate — red on a missing key, never an artifact; `12_BUILD` line 15). And the struct-mirror parenthesis — *"that
> gate does not exist and `build.ps1` skips it loudly"* — has been false since 2026-08-05:
> `build.ps1` implements it as a hard throwing gate that reads the run's `.trx` and fails when
> `ShmLayoutMirrorTests` did not execute. `12_BUILD` carried the identical stale sentence and is
> corrected with it; restating the gate list in two documents is what let one of them rot.
- **Licence guard:** `tools/license-check` fails the build if a vendored dependency is missing its licence copy, or if Intel IGCL / AMD ADLX headers appear anywhere in the tree (`docs/18_GPU_VENDOR_APIS.md` §Vendor SDKs we deliberately do not use), or — since P4 PR-6 — if `legal/licenses/nuget/` is not what `tools/license-gather.ps1` writes for the NuGet packages this build ships. A Dependabot bump therefore goes red until someone re-runs the script and commits the texts, which is the point: the new version's licence is read by a person before it ships. Licensing regressions are silent and hard to unwind later — catch them at PR time.
- **Placeholder guard:** fails if any `{{` token survives in `README.md` or `legal/*.md`. Those are shipped, legally operative documents (FR-11 displays them in the first-run Legal Gate); an unsubstituted `{{DEVELOPER_NAME}}` in an EULA is not a cosmetic defect.
- `permissions: contents: read`.

### `codeql.yml` — push, PR, weekly cron · **repo-local**

> **Not a caller stub, which this heading said until 2026-08-06.** `codeql.yml`'s own header says
> the opposite: the ops repo has `codeql-mixed.yml`, but C++ needs manual build mode driven by the
> CMake preset and that input surface has not been verified. The paragraph below already anticipated
> exactly this outcome; the heading was never updated when it happened.
- Languages: `csharp` **and `cpp`** (manual build mode for C++, driven by the CMake preset). The native layer is where memory-safety bugs would live; excluding it would defeat the purpose. Verify the mixed template exposes a C++ build-command input; if it does not, this one goes repo-local too.
- `permissions: security-events: write, contents: read`.

### `release.yml` — on tag `v*` · **repo-local, built 2026-09-14 (P4 PR-5)**

> **Exercised by a tag on 2026-09-16 — `v0.1.0-beta.1`, run 35111939019 — and the first attempt did not finish.**
> The version gate, the substitution and the quality gate ran; the quality gate then sat in `Infrastructure.Tests` for
> 58 minutes (every other suite green; the same suite 42 s in the rehearsal an hour before) until the job's 60-minute
> limit cancelled it, with no test named. `gh run rerun --failed` on the same tag: the gate in 11 minutes
> (Infrastructure 305 in 17 s), publish (252 MB tree), licences, the tree assertion, `vpk pack` (`Setup.exe` 110 MB,
> portable zip and full nupkg 105 MB), `SHA256SUMS.txt`, and the pre-release created with the section's notes and the
> checksums appended — <https://github.com/poli0981/frameledger/releases/tag/v0.1.0-beta.1>. Two things the tag taught:
> a tag run can be re-run in place after a flake (the Release step is idempotent up to the release existing — it did
> not, so nothing had to be deleted), and a hang eats the whole job budget silently, which is why #196 added
> `--blame-hang` to the gate. The paragraph below is the history as it stood.
>
> This heading said **PLANNED, NOT PRESENT** from 2026-08-06 until the workflow existed, with the note that
> "nothing in this repository has ever run `dotnet publish`". ~~Still true of the *repository's history*: the
> workflow has not been exercised by a tag yet~~ (struck 2026-09-16, above) — its publish, tree assertion and `vpk pack` steps were run by hand on
> 2026-09-14 (`12_BUILD` §Publish & package has the sizes), the release upload was not. The first tag is the rest of the measurement; until then every other claim below is
> what the file says, not what a run showed.
>
> **A dry run exists since 2026-09-16 (P5 prep).** `workflow_dispatch` with a `version` input runs the identical
> job — gate, publish, runtime licences, tree assertion, `vpk pack`, checksums, the artifact upload — and skips only
> `gh release create`; no tag is made. Two things differ from a tag run, both deliberate: the version comes from the
> input (its numeric core must still equal `VERSION`), and a missing `## [x.y.z]` section is a printed warning with
> placeholder notes rather than a stop, because that section is written in the tag commit and a rehearsal precedes
> it. The artifact is `release-<version>-dry-run`. Run it with `gh workflow run release.yml -f version=0.1.0-beta.1`
> (on the branch that carries the file, `--ref <branch>`); the result is what the first tag will do, minus the
> upload. The rehearsal's outcome is recorded in `CHANGELOG.md` when it has run.

- `runs-on: windows-latest`, the same .NET / MSVC / clang-format pins as `ci.yml`, `permissions: contents: write`
  (the Release API) and nothing else.
- **Version gate first.** The tag must be `vMAJOR.MINOR.PATCH[-prerelease]`; its numeric core must equal the
  `VERSION` file (`12_BUILD` §Version); and `CHANGELOG.md` must carry a **non-empty** `## [x.y.z]` section for the
  full version, extracted by `tools/release-notes.ps1` (self-tested on every `build.ps1 check`). A missing
  section is a red release, not an empty note — the ledger's header said the opposite for five weeks.
- `{{RELEASE_DATE}}` substituted in `legal/*.md` with the run's UTC date **before the build**, because the App
  embeds those documents (FR-11); any `{{` token left anywhere after that fails the job.
- **The identical gate** — `./build.ps1 check`, native first, every tool including `versioninfo-check` against
  `VERSION`. A release never skips it.
- `dotnet publish` App + Agent, self-contained + ReadyToRun, `-p:Version=<tag>`, into one `out/app`.
- **The published tree asserted by name**: `FrameLedger.exe`, `FrameLedger.Agent.exe`, the four shipped natives
  `versioninfo-check` lists, `rules/detection-rules.json`; `FrameLedger.CaptureHost.exe` absent (`12_BUILD`:
  exactly two roots — `package-closure-check` proves it statically, this reads the directory); `versioninfo-check`
  run again over `out/app`, because a `.targets`-staged DLL that failed to copy is a warning to `dotnet publish`.
- `vpk pack --packId FrameLedger.App` (the `vpk` tool pinned to the library's 1.2.0) with the release notes;
  `FrameLedger.App-win-Setup.exe` must come out, because README §Install names it. **Not `--packId FrameLedger`**:
  Velopack installs into and uninstalls `%LOCALAPPDATA%\<packId>`, which with that id is the data folder
  (`12_BUILD` §Publish & package). No `--icon` until `assets/icon.ico` exists.
- `SHA256SUMS.txt` over every asset, published beside them and printed into the release body (`11_UPDATER`
  §Unsigned releases).
- `gh release create` with the assets, `--verify-tag`, and `--prerelease` for a `-beta.N` tag.
- The assets and the notes are also uploaded as a workflow artifact for the smoke checklist (`14_TESTING`
  §Release smoke).
- ~~Optional final step: submit installer hash to VirusTotal~~ — not built; an upload of the installer to a third
  party is a decision the owner makes, not a step a workflow adds quietly.

### `rules-publish.yml` — on change to `rules/detection-rules.json` in `main`
- Runs `tools/rules-validate` and fails on anti-cheat removals. The raw file on `main` **is** the distribution endpoint (05_DETECTION), so this workflow only gates correctness.

  > **There is no `rulesVersion` bump check, and this line used to claim one.**
  > `rules-validate.ps1` checks the field's *format* and nothing compares it
  > against the merge base, so a blocklist edit ships with the version untouched —
  > measured on this repository's own history, that is what every commit that
  > changed the `anticheat` block actually did, while the one commit that bumped
  > `rulesVersion` changed the block not at all.
  >
  > Recorded rather than built, because nothing depends on the ordering: §S20's
  > seeder deliberately uses **provenance** (a hash of what it installed) rather
  > than version comparison, precisely because that history falsifies the
  > comparison. A monotonicity gate is still worth having before anything else
  > starts trusting the field — an unbuilt gate described as existing is the
  > defect this note replaces.

  > **The validator does not evaluate rules**, and this line used to say it ran
  > "against fixtures". It checks the schema, the imperative constraints, the
  > parser's capacity bounds, and **fixture coverage** — that every rule id has a
  > fixture and every fixture has a rule. The evaluation is
  > `RuleFixtureCorpusTests` in `ci.yml`'s `check` job, which drives the real
  > evaluator through the real probe. Both halves run on the same PR; only the
  > coverage half fires on a rules-only change, which is why the two are
  > described separately rather than as one gate.
- **The `anticheat` block gets extra scrutiny:** schema-valid, non-empty, no entry removed without a justification in the PR body. Removing a blocklist entry is a safety change and requires the same review as a security fix.
- `permissions: contents: read`.

## Dependabot (`.github/dependabot.yml`)

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule: { interval: weekly }
    groups: { minor-and-patch: { update-types: [minor, patch] } }
  - package-ecosystem: github-actions
    directory: /
    schedule: { interval: weekly }
```

Central package management makes Dependabot PRs single-file diffs.

## Branch & release policy

- `main` protected: CI + CodeQL required, linear history, no direct pushes.
- Release: in ONE commit, bump `VERSION` and move the `[Unreleased]` entries of `CHANGELOG.md` under `## [X.Y.Z] - date` (Keep a Changelog format) → tag `vX.Y.Z` → `release.yml` does the rest (and refuses a tag that disagrees with either file) → smoke-test the produced installer on a clean Win 10 VM + Win 11 (manual checklist in 14_TESTING §Release smoke).
- Pre-releases: `vX.Y.Z-beta.N` tags mark the GitHub release as pre-release; the App's `stable` channel ignores them and its `beta` channel (Settings ▸ Updates, `update.channel`) includes them — built with P4 PR-5, so ~~explicit beta channel is v2 backlog~~ is struck. The managed assemblies carry the full tag version; the native VERSIONINFO blocks carry the numeric core (`12_BUILD` §Version).

## Repo hygiene checklist

> Rewritten 2026-09-16 (P5 prep). The list below had every box unticked while three of them were done in the
> tree, and its first box asked for caller stubs this file's own opening paragraph says were never the design.
> In-tree items are stated as done with their location; the rest are GitHub settings only the owner can set,
> listed as such rather than as work a PR could close.

**In the tree — done:**

- [x] Repo-local `ci.yml`, `codeql.yml`, `release.yml`, `rules-publish.yml` (all four repo-local by decision, §Workflows); ~~caller stubs pointing at `poli0981/.github`~~ — never the design, see the first paragraph
- [x] Issue forms `bug_report.yml`, `feature_request.yml`, `safety_gap.yml`; `ISSUE_TEMPLATE/config.yml` pointing a vulnerability at private reporting; PR template referencing CLAUDE.md's definition of done
- [x] `.github/dependabot.yml` (nuget + github-actions, weekly)
- [x] `SECURITY.md` — the private route for a vulnerability, the public route for a safety gap, and the honest response intent (2026-09-16)
- [x] A release rehearsal: `release.yml` `workflow_dispatch` runs every step but the Release itself (§release.yml)

**GitHub settings — the owner's (`HANDOFF` §Owner-only):**

- [ ] Branch protection as §Branch & release policy states; `Rules / validate` as a required check (§S23-2, after its skip-shim)
- [ ] Dependabot alerts + security updates (the manifest is in the tree; the alerts are a setting)
- [ ] Private vulnerability reporting enabled (Settings ▸ Security), so `config.yml`'s link resolves
- [ ] Repo topics: `windows`, `wpf`, `benchmark`, `fps`, `game-performance` (~~`presentmon`~~ — dropped 2026-08-27, §G)
