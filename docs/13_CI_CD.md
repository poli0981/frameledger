# 13 — CI/CD

FrameLedger uses the **`poli0981/.github` ops repo** where its templates fit, and repo-local workflows where they do not. `ci.yml` is repo-local **by necessity, not preference**: the ops repo's `reusable-desktop-csharp.yml` runs `dotnet restore` / `build` / `test` directly and exposes no input for a native pre-step, MSVC setup, or the Vulkan SDK. FrameLedger's build is mixed-toolchain and native-first, and `12_BUILD.md` §Local quality gate commits to CI running *the identical script* as local — a promise a pure-managed template cannot keep. CodeQL is repo-local too (its own heading below says why), and so is `release.yml` since 2026-09-14 — the same native pre-step and the identical gate make a caller stub impossible for it as well; this sentence said both "still call the ops repo" until then.

> ⚠ Known gotcha (learned on earlier migrations): caller stubs **must declare explicit `permissions:` blocks** — permissions do not inherit into reusable workflows. Every stub below lists its own.

## Workflows (`.github/workflows/`)

### `ci.yml` — push to `main` *(and to `alpha` since 2026-10-31, D73 — §The alpha line)* + all PRs + `workflow_dispatch` (added 2026-10-04) · **repo-local**
- `runs-on: windows-latest`, .NET SDK pinned by `global.json` via `actions/setup-dotnet`, MSVC via `ilammy/msvc-dev-cmd` (pinned to its v1.13.0 commit since beta.24 — the one third-party action, SonarCloud `githubactions:S7637`; Dependabot moves the pin), NuGet cached.
- Single step of substance: **`./build.ps1 check`** — the same script, with the same switches (none), a developer runs before pushing. The gate list lives in `12_BUILD` §Local quality gate, **once**, rather than being restated here where it goes stale. *(That is the `check` job. Since 2026-10-25 a second job, `sonarcloud`, runs after it — §SonarCloud below; it is not the gate and not a required context.)*
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
    expiry, and names the test projects that wrote no `results.trx`. *(Measured on xUnit v3 3.2.2. The suites moved to
    4.0.1 on 2026-09-21 as `xunit.v3.mtp-off`, still under VSTest; whether `--blame-hang` stops a hung 4.x executable is
    unmeasured, and the wall clock stays either way.)*
  - **Two more shapes had one cause, found and fixed 2026-09-16** after four failures in one evening's merge train:
    `AKilledHostLeavesAPartialThatRecoverTurnsIntoAnInterruptedSession` ("the process cannot access the file …
    `.partial`") and `WithNoConsentRecordTheHostRefusesAndNothingIsEverInjected` (a leftover consent record). The
    suite's `Kill` helper returned before the killed host had released its files, so the next line read a `.partial`
    the dead process still held, and the ledger delete in `Dispose` silently failed and left the record for the next
    case. `Kill` now waits for exit (bounded, asserted) and the delete retries. Not §S19(b) either — a test-harness
    race, and the third flake shape of the day (the registry-subtree race between `VkLayerRegistrationTests` and the
    GOG reader's test, fixed in #188) was the same kind.
  - **Two more on 2026-10-07 (beta.18), both re-run green and then fixed the same day.**
    `WhenTheTargetExitsTheHostStopsAndSaysWhy` ended **`TargetNotRunning`** (#286, run 37619719203): the host resolves its
    target once, as it starts, and a host slower than the harness's 6 s hold found it gone — the shape recorded since
    2026-09-21. The hold is 15 s: a margin, not a signal, because the host prints nothing until its session ends.
    `LaunchModeOnAVulkanTargetAttachesToTheLayersRingAndInjectsNothing` ended **`RefusedByGuard` / `ModuleScanFailed`**
    (#288, run 37627351438): the hosted runner has the Vulkan loader and no device, so the harness exits 77 with a
    `[SKIP]` line — but the runner is elevated, the host starts the harness through `CreateProcessWithTokenW`
    (`UnelevatedProcess`), the child gets no stdout, and the line never reached the host's output; the test then
    depended on the session reading `LaunchTargetExited`, and a scan that hit the dying harness failed it instead. The
    test now asks the harness directly, before the host runs (exit 77 = skip, with the harness's own line); hiding the
    Vulkan ICDs from that probe makes it skip in under a second, and on a machine with a device it runs in full.
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
- Uploads coverage artifacts — ~~Cobertura~~ Cobertura and OpenCover since 2026-10-25, one pair per test project, as the `coverage` artifact the `sonarcloud` job reads (§SonarCloud).
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
- **Placeholder guard:** fails if any `{{` token survives in `README.md` or `legal/*.md` *(other than `{{RELEASE_DATE}}`, which `release.yml` substitutes — added 2026-10-04)*. Those are shipped, legally operative documents (FR-11 displays them in the first-run Legal Gate); an unsubstituted `{{DEVELOPER_NAME}}` in an EULA is not a cosmetic defect.
- ~~`permissions: contents: read`.~~ `permissions: contents: read, pull-requests: read` — the second for the Changelog step's list of a pull request's files, read-only *(corrected 2026-10-04)*. *Since 2026-10-25 per job:* the workflow grants nothing (`permissions: {}`), `check` holds those two and `sonarcloud` `contents: read` alone — a workflow-level grant reaches every job (SonarCloud `githubactions:S8264` on the first analysis from CI).

### `codeql.yml` — push, PR, weekly cron · **repo-local**

> **Not a caller stub, which this heading said until 2026-08-06.** `codeql.yml`'s own header says
> the opposite: the ops repo has `codeql-mixed.yml`, but C++ needs manual build mode driven by the
> CMake preset and that input surface has not been verified. The paragraph below already anticipated
> exactly this outcome; the heading was never updated when it happened.
- Languages: `csharp` **and `cpp`** (manual build mode for C++, driven by the CMake preset). The native layer is where memory-safety bugs would live; excluding it would defeat the purpose. Verify the mixed template exposes a C++ build-command input; if it does not, this one goes repo-local too.
- **And `actions`, since 2026-10-25** (build mode `none`): CodeQL reads `.github/workflows` for injection from event data, untrusted checkouts and over-broad tokens — the class of finding SonarCloud raised once (`githubactions:S7631`, `wiki.yml`, beta.26). Its context, `analyze (actions, none)`, is not one of `main`'s required checks; adding it to them is the owner's setting.
- **Concurrency, since 2026-10-25:** a new push to a pull request cancels that pull request's run; a push to `main` and the weekly scan each run in a group of their own (the run id), so none is cancelled or left pending, and the default branch's alerts come from a finished analysis.
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
- **The build id from the clean checkout** (beta.18): `git describe --always --dirty --abbrev=12` before anything edits a
  tracked file — a dirty one or one over 31 characters stops the run — into `FL_BUILD_ID` for CMake. Until beta.18 the
  substitution below ran first and every release's natives called themselves `<tag>-dirty` (`12_BUILD` §Release-time
  token substitution).
- `{{RELEASE_DATE}}` substituted in `legal/*.md` with the run's UTC date **before the build**, because the App
  embeds those documents (FR-11); any `{{` token left anywhere after that fails the job.
- **The identical gate** — `./build.ps1 check`, native first, every tool including `versioninfo-check` against
  `VERSION`. A release never skips it.
- `dotnet publish` App + Agent, self-contained + ReadyToRun, `-p:Version=<tag>`, into one `out/app`.
- **The published tree asserted by name**: `FrameLedger.exe`, `FrameLedger.Agent.exe`, the ~~four~~ five shipped natives
  `versioninfo-check` lists (`FrameLedger.ProcessStats.dll` since beta.12 — corrected 2026-10-04), `rules/detection-rules.json`; `FrameLedger.CaptureHost.exe` absent (`12_BUILD`:
  exactly two roots — `package-closure-check` proves it statically, this reads the directory); `versioninfo-check`
  run again over `out/app`, because a `.targets`-staged DLL that failed to copy is a warning to `dotnet publish` —
  **since beta.14 (D50) with its managed half** (`-ManagedDir out/app -ExpectedProductVersion <tag>`): the eight managed
  binaries name FrameLedger, carry the tag's full version and the native binaries' copyright line (`12_BUILD` §Version,
  *Identity*). Both publishes take `-p:FrameLedgerSourceRef` since beta.14.
- **since beta.13, `vpk download github --pre` first**, so `vpk pack` finds the previous release's full package in
  `out/release` and writes a delta; after the pack, what `assets.win.json` names is kept and the rest (the previous full
  package) moves to `out/previous`, the feed and `RELEASES` are filtered to this build, and a downloaded previous package
  with no delta beside it fails the job. A failed download is a warning: the release is full-only, as every one before it
  was (`11_UPDATER` §Built).
- `vpk pack --packId FrameLedger.App` (the `vpk` tool pinned to the library's ~~1.2.0~~ 1.2.161 since beta.30, D71) with the release notes;
  `FrameLedger.App-win-Setup.exe` must come out, because README §Install names it. **Not `--packId FrameLedger`**:
  Velopack installs into and uninstalls `%LOCALAPPDATA%\<packId>`, which with that id is the data folder
  (`12_BUILD` §Publish & package). No `--icon` until `assets/icon.ico` exists.
- `SHA256SUMS.txt` over every asset, published beside them and printed into the release body (`11_UPDATER`
  §Unsigned releases).
- `gh release create` with the assets, `--verify-tag`, and `--prerelease` for a `-beta.N` tag.
- **The alpha line (D73, 2026-10-31):** the version gate reads `RELEASE_LINE` beside `VERSION` and refuses a tag that
  disagrees with it — on the `alpha` line only `vX.Y.Z-alpha.N`, and an `-alpha.` tag nowhere else. An alpha's download
  and pack take `--channel alpha`, so the feed, the asset list, the legacy index and the installer are named for that
  channel (`releases.alpha.json`, `assets.alpha.json`, `RELEASES-alpha`, `FrameLedger.App-alpha-Setup.exe`) and its delta
  is made from the previous alpha; the main line's pack is unchanged. The release is titled *(experimental alpha)*, and
  its notes — the alpha's own file, `docs/changelog/x.y.z-alpha.N.md` — open with the warning (§The alpha line).
- The assets and the notes are also uploaded as a workflow artifact for the smoke checklist (`14_TESTING`
  §Release smoke).
- ~~Optional final step: submit installer hash to VirusTotal~~ — not built; an upload of the installer to a third
  party is a decision the owner makes, not a step a workflow adds quietly.

### `rules-publish.yml` — ~~on change to `rules/detection-rules.json` in `main`~~ on a PR or push to `main` touching the rules file, its schema, `rules-validate.ps1` or this workflow (corrected 2026-10-04)
- Runs `tools/rules-validate` and fails on anti-cheat removals. ~~The raw file on `main` **is** the distribution endpoint (05_DETECTION), so this workflow only gates correctness.~~ *(Corrected 2026-10-04: there is no feed — §S20's feed half is deferred and nothing in `src` fetches. A rules change reaches users only inside a release, seeded by the Agent from the packaged copy; this workflow only gates correctness.)*

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
- **The `anticheat` block gets extra scrutiny:** schema-valid, non-empty, no entry removed without a justification in the PR body. Removing a blocklist entry is a safety change and requires the same review as a security fix. *(Added 2026-10-04: the PR-only removal step fails the job on any removal, a shrunk per-title list or an added trusted signer, whatever the body says — the justification is for the reviewer, and the job stays red.)*
- `permissions: contents: read`.

### `wiki.yml` — after a successful Release run, and by hand · **repo-local, built 2026-10-07 (beta.17, D53/D54)**
- **What:** the GitHub Wiki is a copy of the repository's own pages — `wiki/` (Home, the sidebar, the developer pages)
  and `guide/` (the user guide: the End-user part, the same pages the App shows) with `LIMITATIONS.md`.
  `tools/wiki-build.ps1` names each page (a guide page after its first heading, `guide/README.md` as *User-guide*,
  `LIMITATIONS.md` as *Limitations*), rewrites links between pages to page names and every other repository link to
  its address on GitHub **at the release's tag**, drops each page's first heading (the wiki prints the page name), and
  writes a footer naming the tag. The same assembly is `build.ps1`'s `wiki-check` gate (`12_BUILD` §Local quality gate,
  13d), so a dead link is red on the pull request, never on the published wiki.
- **When:** `workflow_run` on `Release`, completed — the job runs only when that run succeeded and was started by a
  pushed `v*` tag in this repository (`head_repository` is checked since beta.26: a fork cannot start the Release run,
  and the condition now says so — SonarCloud `githubactions:S7631`), so a `workflow_dispatch` rehearsal of
  `release.yml` never publishes. It checks out the run's
  `head_sha` (a `workflow_run` job runs `main`'s copy of this file) and links at `head_branch`, the tag. **Not after an
  alpha's release** (D73, 2026-10-31: a `-alpha.` tag is skipped): the wiki shows the newest beta. By hand:
  `gh workflow run wiki.yml -f ref=main -f publish=false` builds, checks and uploads the pages as an artifact; with
  `publish=true` it publishes them.
- **Why not a step of `release.yml`:** the release stays as it was, its rehearsal stays a pure rehearsal, and a wiki
  that fails to publish is a red run of its own that can be re-run — a step after `gh release create` could never be
  re-run, the release already existing. A release created with `GITHUB_TOKEN` fires no `release` event for other
  workflows; `workflow_run` fires because the Release run itself was started by a tag push.
- **Publishing:** `gh auth setup-git`, a clone of `<repo>.wiki.git`, everything but `.git` replaced, a commit as
  `github-actions[bot]` only when something changed, `git push origin HEAD:master` (the wiki's branch is `master`). A
  page edited on the Wiki tab is overwritten at the next release: edit `wiki/` or `guide/`.
- `permissions: contents: write` — the repository's default workflow token is read-only (read from the API,
  2026-10-07), and pushing to the wiki repository needs write.

## The alpha line — the `alpha` branch (owner decisions D73–D76, 2026-10-31)

What it is and what differs on the branch: `12_BUILD` §The alpha line. What CI and the release do with it:

- **`ci.yml`** runs `check` on a push to `alpha` and on every pull request into it, under the branch's own `global.json`
  — `setup-dotnet` installs the exact prerelease SDK it names whatever the `rollForward`. The `sonarcloud` job does not
  run for the branch (the organisation's plan analyses main alone, §SonarCloud), and neither does the `Changelog` step:
  what an alpha changes is written in its release's own file on main, never in an `[Unreleased]` entry on `alpha`, which
  would collide with main's at every sync.
- **`codeql.yml`** stays on main: the source is main's, and CodeQL's extractor reads C# 14 (CodeQL 2.25.6, June 2026)
  while the alpha's compiler is C# 15.
- **The branch is not protected** (an owner's setting; none was asked for): pull requests into it are how changes land
  (squash), and the sync from main is a pull request merged with a merge commit — main's linear rule is main's alone.
- **A release of the line**, in order: the alpha's notes on main (`docs/changelog/x.y.z-alpha.N.md` opening with the
  warning, its row in `CHANGELOG.md`'s Releases table, a line in HANDOFF's START HERE) → the sync pull request into
  `alpha` → a `release.yml` dispatch on `alpha` (`gh workflow run release.yml --ref alpha -f version=x.y.z-alpha.N`) →
  the signed tag on `alpha`'s head → the run (§release.yml: the line's tag rule, `--channel alpha`). **No wiki publish**
  (`wiki.yml` skips an `-alpha.` tag) and **no release line moves**: README, ACCURACY, the Disclaimer's copy and
  LIMITATIONS keep naming the newest beta.
- **The cadence:** one alpha per beta, cut from it. Velopack's GitHub source reads the newest ten releases (one page,
  `perPage` 10), so ten alphas in a row would hide every beta from the betas' own update check, and the other way round
  (`11_UPDATER` §Channels).

## SonarCloud — from CI, `ci.yml`'s `sonarcloud` job (owner decision D68, 2026-10-25)

**D68 — SonarCloud is analysed from CI.** The owner turned SonarCloud's Automatic Analysis off and set the
`SONAR_TOKEN` secret (2026-10-24), and asked for the CI-based analysis with the newest scanner, run until its quality
gate passed on issues and coverage both. It replaces D67 (the subsection below, as it stood): `.sonarcloud.properties`
is gone, `SonarQube.Analysis.xml` holds what it held, and the two rules broken by design everywhere are configuration
rather than marks in the UI.

**How it runs.** A second job of `ci.yml`, after `check` (`needs: check`), on every pull request from this
repository and every push to `main` (or a `workflow_dispatch` of `main`) — never another branch: the organisation's
plan refuses a branch analysis ("Organization is not allowed to access data from non main branches", a dispatch of
the feature branch, 2026-10-25), so a dispatch of any other branch runs `check` alone:

1. a full-depth checkout (blame dates the new code; the tags name the version), .NET, MSVC, the NuGet and
   `~/.sonar/cache` caches;
2. the gate's coverage — the `coverage` artifact `check` uploaded, not a second test run;
3. `cmake --preset x64-release -DCMAKE_EXPORT_COMPILE_COMMANDS=ON -DCMAKE_CXX_SCAN_FOR_MODULES=OFF` and the one
   header the build generates (`fl_ac_floor_gen`) — the compilation database, with nothing compiled. *Scanning off
   because the first run failed on it:* with C++20 and Ninja, CMake gives every compile command an
   `@<object>.modmap` response file that only the build writes, and the C/C++ analyser (CFamily 6.85) stops with
   `IllegalStateException: unexpanded response file`; no source here uses a module;
4. **SonarScanner for .NET 12.0.0** (2026-10-15, the newest), a local tool (`.config/dotnet-tools.json`, `dotnet tool
   restore`; Dependabot's nuget entry moves it), which provisions its own JRE: `begin` with `/s:SonarQube.Analysis.xml`
   → `dotnet build FrameLedger.slnx -c Release` → `end`.

**Why a job of its own, and a build of its own.** The scanner's targets turn `TreatWarningsAsErrors` off and empty
`WarningsAsErrors` in every build they instrument (`SonarQube.Integration.targets`, read in 12.0.0's source), so
wrapping the gate's build would quietly weaken the gate. And the job is **advisory**: `main`'s required contexts stay
`check`, `analyze (csharp, none)` and `analyze (cpp, manual)`. It waits for the quality gate
(`sonar.qualitygate.wait`), so a failed gate — or a SonarCloud outage — is a red job beside a green `check`, never a
blocked merge, and SonarCloud's app still posts its own *SonarCloud Code Analysis* check on the pull request.

**The C++.** The SonarScanner for .NET analyses only files an MSBuild project names: it drops `sonar.sources`, and
its scan of the files outside the projects (`sonar.scanner.scanAll`: PowerShell, YAML, JSON and a dozen more) has no
C or C++ in its list (`AdditionalFilesService`, 12.0.0). So `FrameLedger.Infrastructure.csproj` — the project that
reaches the native layer — names `src/native`'s `.cpp` and `.h` files, `third_party` excepted, as `FlNativeSource`
items, read through the scanner's `SQAdditionalAnalysisFileItemTypes`. The items exist only in a build the scanner
instruments (its ImportBefore targets set `SonarQubeTempPath`), so a normal build, an IDE and a publish never see them;
because they lie outside the project's folder, the scanner analyses them at the root, with the compilation database:
MSVC's own flags and defines, where Automatic Analysis guessed them. Native code has no coverage report (below).

**Coverage.** `build.ps1` collects `"XPlat Code Coverage;Format=cobertura,opencover"`: Cobertura for
`tools/coverage-gate.ps1`, as before, and OpenCover for SonarCloud, whose C# importer reads no Cobertura. The
reports' paths are absolute, and every hosted Windows runner checks out to the same path, so the reports `check`
wrote name the sources this job reads. Only `tests/*/TestResults/<guid>/` is read; the trx logger's second copy under
`In/` is not. **Coverage is the C#'s alone** (`sonar.coverage.exclusions`): SonarCloud counts the executable lines of
a file no report covers as uncovered — the first pull request analysis read `build.ps1`'s 232 lines to cover, every
one uncovered, and failed the gate at 0 % on the two it changed — so the PowerShell tools (proven by their own
`-SelfTest` cases, which no coverage tool reads) and the native code (Catch2 and the injection tests, with no coverage
collector) are left out of the coverage figures; they are analysed for issues as everything else is. Measuring the
native code would be the runner's Visual Studio coverage tool and `sonar.cfamily.*` coverage paths — not done.

**The version, and so the new code.** The project's new-code period is SonarCloud's *Previous version*. Until now
every analysis was "not provided", so the new code was everything since the first analysis (2026-09-27): 34,268 lines
on 2026-10-20. The job now passes `/v:` the newest `v*` tag the commit descends from, so new code is everything
since the last release — **from the second analysis with a version on**, measured on `main` (below): the first,
`main`'s first from CI, still counted from before any version existed; the second set the start at the last analysis
without one (2026-10-20 05:11; 257 new lines on 2026-10-25). An issue the CI analysis raises on an older line is
dated by blame — the newest line of its path — and is new code only when that line is. The gate is *Sonar way*: its
coverage condition (80 % on new code) was skipped while no analysis had coverage, and applies from the first analysis
from CI.

**What it reads** — `SonarQube.Analysis.xml`: the exclusions of `.sonarcloud.properties` (below) and what a build
writes into the checkout, since the scanner walks all of it (`build/`, with the sources CMake fetches there, `out/`,
every `bin/` and `obj/`, `TestResults/`) — SonarCloud also drops what `.gitignore` names (the first run: "63 files
ignored because of scm ignore settings"), and the patterns say it in the file rather than rely on that; the same
duplication exclusions; C++20; UTF-8; the OpenCover paths; the compilation database;
**`sonar.cs.roslyn.ignoreIssues`** — Roslynator, Meziantou, the VS threading and the .NET
analyzers already gate the build, warnings as errors, and what they report at a lower severity is this project's
choice, so only SonarCloud's own rules are imported; and **two rules ignored where they apply by design** — `S8417`
(sequentially consistent atomics) in `src/native`, any repository because a header is indexed as C, and
`csharpsquid:S6640` (unsafe code) in Infrastructure and Shared. Those are the optional rows of the owner's steps below,
which the UI's *Ignore issues on multiple criteria* would have held; a pull request adding atomics no longer fails the
gate on them.

**What it does not cover.** A fork's pull request and Dependabot's get no secrets, so the job is skipped for them;
Dependabot's would run if the owner added `SONAR_TOKEN` to the repository's *Dependabot secrets*. A pull request's
analysis lists only the issues on the lines it changed, and no other branch can be analysed (above), so what the
analysis from CI finds in the older code shows on `main`'s first analysis from CI and nowhere before it.

**The first analyses (2026-10-25, PR #334).** Run 40147463349: the scanner began, the build ran, the C# coverage was
imported (635 files) — and CFamily stopped on the `.modmap` response files (step 3). Run 40148541525, with scanning
off: everything analysed — 1,300 files indexed, 249 left out by the exclusions and 63 by `.gitignore`; of the
compilation database's 181 units, 32 analysed in full (none partly; 118 whose sources are not analysed — the vendor
stubs and what CMake fetched, Catch2 and MinHook —, 18 duplicates, 13 it does not support) in 139 s; 21 C and 32 C++
files indexed (a `.h` is C to SonarCloud) — and the
gate failed on two conditions, both fixed the same day: `githubactions:S8264` (a C security rating: workflow-level
permissions reaching both jobs) and 0 % coverage on new code (`build.ps1`'s lines, before the coverage exclusions).
A dispatch of the branch (40148544753) analysed it and was refused the gate, which is how the plan's limit was found.

**`main`'s first analysis from CI (run 40160302893, the merge of #334).** The gate failed on one condition, a C
reliability rating on new code, and passed the others (84.7 % coverage on new code). The 34 open issues were all C++,
raised there for the first time — the real MSVC flags let CFamily see them — and each dated by blame, 2026-08-02 to
2026-10-19. Two were new code: `hook-harness`'s null dereferences, whose paths reach lines changed on 2026-10-19 — two
MAJOR bugs, so C; the BLOCKERs were older lines. (A pull request's analysis never showed any of them: it reports no new
issue on an old line.) The next analysis, the merge of #335 at 05:20, moved the new code's start to 2026-10-20 05:11
and turned the gate green with nothing fixed; the merge of #336 at 06:17 closed 33 of the 34. The 34th, `S3519` in
`fl_patch_check.h`, stayed open: its `NOSONAR` sat on `p -= 5`, and SonarCloud reports the read on the line after —
moved there by the release pull request. The C# had none open. Beta.29 PR-3 settled them — fixed where the code was
wrong, `// NOSONAR` with its reason above it where the analyser was (five):

| Rule | Where | n | What it was | Done |
|---|---|---|---|---|
| `cpp:S2275` format/argument | `fl-probe-nvapi` | 19 | `%u` for `NvU32`, an `unsigned long` (32 bits on Windows, so nothing printed was wrong) | `%lu`; the machine line `CaptureHost` parses prints the same |
| `cpp:S2259` null dereference | `hook-harness` `main.cpp` ×2, `proxy_swapchain.h` | 3 | **real:** `--vulkan` and `--opengl` create no D3D11 objects, and `--opengl` alone ran the default probes through a null swapchain, as did any D3D11 mode beside either flag | `HaveD3D11`, asked first by every mode that drives the D3D11 swapchain; the duplicate mode flags are one |
| `cpp:S2259` | `fl_ring.h` `Publish` | 1 | the path runs on past a failed `REQUIRE(w.Init(...))` in `ring_test`, which ends the test case; every real caller publishes through a writer whose `Init` succeeded, before any hook is installed | `NOSONAR` — a comment on the per-present path, no code |
| `cpp:S3519` negative offset | `fl_patch_check.h` | 1 | by design: the 5 bytes before a hot-patchable function are its patch area, which the check reads under `StillOurs`' SEH guard | `NOSONAR`, no code |
| `cpp:S6232` union pun | `dllmain.cpp`, the log's QPC epoch | 2 | `LARGE_INTEGER f{}` makes the union's first member, the `LowPart`/`HighPart` struct, the active one, so reading `QuadPart` reads a member that is not — undefined in C++, whatever `QueryPerformanceFrequency` wrote | initialised through the member read, `{.QuadPart = 0}` |
| `cpp:S6232` | `guard_test.cpp` | 2 | a `ULARGE_INTEGER` to take a second off a `FILETIME` | 64-bit arithmetic on the two halves |
| `cpp:S5813` `strlen` | `guard_test.cpp` | 1 | `strlen` of a fixed-size rules value | `strnlen(…, kMaxValueLen)` |
| `cppsecurity:S9340` index from input | `layer.cpp`, `fl-baseline-probe` | 2 | the terminator written at the count `ReadFile` / `fread` returned, which never exceeds the request | the bound tested first (in the layer, its once-per-process enable-list read — whose `LARGE_INTEGER size{}` drew this PR's own `S6232` twice, and is initialised through `QuadPart` too) |
| `cppsecurity:S5145` log injection | `rules_budget_test.cpp`, `fl-baseline-probe` ×2 | 3 | console output of a test and a probe: four numbers, and the ids of the seed rules file fixed at build time | `NOSONAR` |

**The C# gate is the server's profile, not the analyser's defaults.** SonarCloud applies *Sonar way core* here (166 C#
rules, 176 C++). A local build with the `SonarAnalyzer.CSharp` package and its own default rules reports about 240
findings in this code, nearly all from rules that profile does not activate (`S3358`, `S6667`, `S3267`, `S108`,
`S3236`, `S2094`, `S927` among them); chasing them changes code for nothing the gate reads. What counts is
`api/issues/search` for `main` or the pull request.

### Until 2026-10-25 — Automatic Analysis, no workflow (beta.24, owner decision D67)

*The text from here to the end of this section stood as written; what changed is marked.* ~~The project
`poli0981_frameledger` is analysed by SonarCloud's GitHub app in **Automatic Analysis** mode: on every~~ The project
`poli0981_frameledger` was analysed by SonarCloud's GitHub app in **Automatic Analysis** mode: on every
push to `main` and on every pull request, with no workflow in this repository and no logs published. Its check on a
pull request (*SonarCloud Code Analysis*) is advisory — it is not a required status.

**What it reads is set by `.sonarcloud.properties`** at the root, the only file Automatic Analysis reads, from the
default branch. It excludes what is not FrameLedger's code or not code: the vendored SDK headers under
`src/native/third_party/` (51 headers, 66 thousand lines — before this file they were 41.7 thousand of the project's
94 thousand counted lines, classed C), the generated `Strings.Designer.cs` and `dxr_raygen_dxil.h`, the vendor stubs
(fixtures that carry the vendors' export names on purpose), `tests/fixtures/`, the licence texts, the generated
`docs/vendor-exports.json` and the SQLite migrations (a SQL analyser reads them as another dialect). It reports C++
against C++20, the standard `src/native/CMakeLists.txt` sets. `build/`, `bin/`, `obj/` and `presentmon/` are
git-ignored and never reach it.

**What it cannot do.** `sonar.issue.ignore.*` is not read by Automatic Analysis, so a rule this project breaks by
design is recorded here and marked in SonarCloud's own UI — by the owner, who holds the account. A CI-based scan (the
only way to import coverage or analyse one build variant of the C++) would mean turning Automatic Analysis off, a
`SONAR_TOKEN` secret, OpenCover output beside today's Cobertura and a full-depth checkout; ~~it is not done~~ *done
2026-10-25 (D68, above)*.

**The triage of 2026-10-19** — the analysis of `main` at `372389c` (2026-09-28; nothing had been analysed since),
341 open issues, none resolved. Rules broken by design are *Accept*ed and false positives marked as such, each with
the comment below; the rest are fixed in the same train (beta.24 PR-2).

| Rule | Issues | Verdict | Comment for the UI |
|---|---|---|---|
| `cpp:S8417` atomics should be sequentially consistent | 225 | **Accept** — by design | The SPSC ring and the hook counters use explicit acquire/release and relaxed orders on purpose (`07_IPC`); sequential consistency would add fences on the per-present path. |
| `csharpsquid:S6640` unsafe code blocks | 31 | **Accept** — by design | Interop with the native layer and the shared-memory mirror, confined to `Infrastructure` and `Shared` (CLAUDE.md: the native layer is reachable only through `Infrastructure`). |
| `cpp:S6232` union type-punning | 24 | **False positive** | `LARGE_INTEGER.QuadPart` is how the Windows API returns the value. |
| `cpp:S5813` bounds when computing string length | 19 | **Accept** for the 17 below; the 2 with a buffer bound at hand were bounded (beta.24 PR-2: `FlGuardRulesFilePath` reads `wcsnlen(out, cap)`, the NVAPI bridge `strnlen` over the driver's `NvAPI_ShortString`) | Every string measured is NUL-terminated by construction — see the list under this table. |
| `csharpsquid:S2077` dynamic SQL | 3 | **False positive** | A `PRAGMA` argument cannot be bound; the values are an `int` and closed sets of constants (`wal_checkpoint` mode, the tri-state column), never user text. |
| `cpp:S935` `FlGetBuildId` returns no value | 1 | **False positive** | `FL_BUILD_ID` is defined by the build (`12_BUILD` §Version); the analysis compiles without it. |
| `cpp:S3519` out-of-bounds `memcpy` | 1 | **False positive** | `fl_patch_check.h` reads the prologue of a function this DLL hooked, under SEH, by design (`17_HOOK_ENGINE` §Unhooking). |
| `cpp:S836` garbage value returned | 1 | **False positive** | `hook-harness`'s `VtableOf` reads the vtable of a COM object it created itself. |
| `csharpsquid:S3869` `DangerousGetHandle` | 1 | **Accept** | `ParentDump` passes the dump file's handle to `MiniDumpWriteDump` inside the `using` that owns the stream, on the same thread, so it cannot be released during the call; CsWin32 generates no `SafeHandle` overload here (`NativeMethods.json`: `useSafeHandles: false`). |
| `cpp:S1669`, `cpp:S6191`, `csharpsquid:S4036`, `githubactions:S7637`, `csharpsquid:S2190`, `S3427`, `S8949` | 35 | **Fixed** in beta.24 PR-2 — 5 of the 23 `S1669` were in the vendored Vulkan headers and leave with the exclusions | — |

**`cpp:S5813`, the 17 accepted, and why each string is terminated** (lines as of beta.24):

- `fl_ac_rules.cpp` — `IStartsWith`/`IContains` (`:38`, `:43-44`) and `TokenIs` (`:74`) measure rule fragments and
  literals the guard itself holds; `:151` and `:288` measure a value `CopyToken` has just written, which refuses a token
  that does not fit and always writes the terminator; `CopyLiteral` (`:176`) measures a compiled literal; `:834`
  measures `SHGetKnownFolderPath`'s answer, terminated by the API.
- `fl_guard.cpp:109` — the guard's own DLL path, built and terminated by its caller before injection.
- `fl_guard_sources.cpp` — `:345` measures the executable's path the guard read itself and walked to its terminator
  in the loop just above; `:753` the install root the guard resolved from it; `VdfValue` (`:582`) measures the
  compiled keys its callers pass (`"installdir"`, `"appid"`), and `AllDigits` (`:617`) a value `VdfValue` copied with
  its own bound.
- `fl_prescan.cpp` — `IsKernelDriverName` (`:40`) measures a name the directory walk handed over terminated;
  `SegmentIs` (`:181`) a literal; `:337` the executable's folder the pre-scan built itself.
- `layer.cpp:99` (the Vulkan layer) — a leaf of `GetModuleFileNameA`'s result, which the line above has already
  checked is shorter than `MAX_PATH`, so it is terminated.
- `fl-baseline-probe/main.cpp:55` — a literal (a developer probe, not shipped).

**The six left open after the trains (2026-10-19, beta.26)** — the analysis of `main` at `e35b971` (0.1.0-beta.25),
after the owner's marking: 366 issues resolved (284 fixed — the exclusions and beta.24 PR-2 —, 49 accepted, 33 false
positives) and six open, all in the new-code period, which is why its quality gate read C for reliability and security.
Five were written after the analysis of `372389c` the table above was built from.

| Rule | Issues | Verdict | Comment for the UI |
|---|---|---|---|
| `csharpsquid:S3869` `DangerousGetHandle` | 1 | **Accept** — the row above | as above |
| `csharpsquid:S1244` floating-point equality | 4 | **False positive** | Exact on purpose. `ColorMath.Hsv` compares `max` with `r` and `g` — `Math.Max` returned one of them, the same double; `MarkdownView`'s `next == _wheelTarget` is true only when `next` was assigned `_wheelTarget` (the snap that ends the easing); `SensorsChart.Line`'s `scale == 1.0` compares a parameter with its own default literal to skip a copy. |
| `githubactions:S7631` untrusted code from a fork | 1 | **False positive**, and hardened in beta.26 | `wiki.yml` checks out `workflow_run.head_sha` only after a successful Release run started by a pushed `v*` tag, which a fork cannot start; since beta.26 it also requires `workflow_run.head_repository` to be this repository. |

**Marked by the owner the same day** (2026-10-19, 14:27–14:29 UTC), each as the table says: the four `S1244` and
`S7631` as false positives, `S3869` accepted. `main`'s next analysis (`6b07eb2`) has no open issue and its quality gate
passes.

**The owner's steps** after the next analysis of `main`: for each rule above, open
`https://sonarcloud.io/project/issues?id=poli0981_frameledger&resolved=false&rules=<rule>`, select all, *Bulk change*,
choose *Accept* or *False positive* and paste the comment. Optionally, *Administration ▸ General Settings ▸ Analysis
Scope ▸ Ignore issues on multiple criteria*: `cpp:S8417` and `cpp:S6232` on `src/native/**/*`, `csharpsquid:S6640`
on `src/FrameLedger.Infrastructure/**/*` and `src/FrameLedger.Shared/**/*`, so new instances of the by-design rules
stop reappearing on pull requests. *(Since D68 the `S8417` and `S6640` rows are `SonarQube.Analysis.xml`'s, in the
repository; `S6232`'s are not — a union read outside `LARGE_INTEGER` would be a finding — and its 24 stay marked.)*

## Dependabot (`.github/dependabot.yml`) and the NuGet audit

```yaml
version: 2
updates:
  - package-ecosystem: nuget              # also .config/dotnet-tools.json (the SonarScanner, since 2026-10-25)
    directory: /
    schedule: { interval: weekly }
    cooldown: { default-days: 7 }         # since 2026-10-25
    groups: { minor-and-patch: { update-types: [minor, patch] } }
    # ignore: WPF-UI*, FluentAssertions majors — the file says why
  - package-ecosystem: github-actions
    directory: /
    schedule: { interval: weekly }
    cooldown: { default-days: 7 }         # since 2026-10-25
    groups: { actions-minor-and-patch: { update-types: [minor, patch] } }
```

Central package management makes Dependabot PRs single-file diffs.

- **Cooldown (2026-10-25):** a version update waits seven days after its release is published, so a release that is
  pulled or found compromised in its first week never becomes a pull request here. Security updates do not wait — the
  option does not apply to them. The actions' minor and patch releases come as one pull request; a new major comes
  alone, because it is the one that changes inputs.
- **No `dotnet-sdk` entry, on purpose:** `global.json` is a floor (10.0.100) with `rollForward: latestFeature`, so a
  developer's machine and the runner already build with the newest 10.0 SDK they have (the runner installed 10.0.402
  on 2026-10-20). Raising the floor is a decision about what a contributor must install, not an update.
- **Dependabot has never proposed a NuGet library update here — measured 2026-10-25.** Every NuGet pull request it
  opened, #66 (2026-08-09) through #314 (2026-10-18), updated analyzers only (Meziantou, Roslynator, the VS threading
  analyzers), while Dapper 2.1.89 (published 2026-09-23), coverlet 10.1.0 (09-27), xunit.v3.mtp-off 4.0.2 (10-09) and
  the 10.0.13 servicing packages (10-13) were never proposed. Its update runs are GitHub Actions runs of their own (`gh
  run list`, event `dynamic`), and the run of 2026-10-18 (39134906593) logs the test projects' resolution failing —
  `Requested dependency resolution to include coverlet.collector/10.1.0 but it was instead resolved to 10.0.1`, `The
  package FluentAssertions.7.2.2 is not compatible. Incompatible project frameworks: net10.0-windows10.0.22621` — and
  opens nothing for them. The run of 2026-10-25 (40160307095), the first with the cooldown, says it of the shipped
  libraries too, the versions in use among them — `The package Dapper.2.1.86 is not compatible. Incompatible project
  frameworks: net10.0-windows10.0.22621`, and the same of Hosting, Sqlite, EventLog, Markdig and Velopack. Why its
  resolver reads a package that targets `netstandard2.0` as incompatible with this framework is **unmeasured**. So a
  quiet Dependabot is not evidence that the packages are current: `dotnet list FrameLedger.slnx package --outdated` is,
  and the updates it had missed were made by hand in beta.29 PR-2 (#335) — `coverlet.collector` 10.1.0 excepted,
  whose reason `Directory.Packages.props` gives beside it.
- **beta.30 (2026-10-26), by hand under the same cooldown** (the owner: "check the dependencies and conflicts, update if
  needed"): Velopack and `vpk` 1.2.0 → 1.2.161 (the owner's decision, D71 — never 1.2.158, NuGet-deprecated), Markdig
  1.4.0 → 1.4.2 (parser fixes; its licence text unchanged at the packaged commit), and **the `SQLitePCLRaw` pin
  dropped**: `Microsoft.Data.Sqlite` 10.0.13 declares the patched 2.1.13, and the 3.0.5 pin had put it on SQLitePCLRaw's
  3.x line — a cross-major resolution NuGet does not report, because the declared range has no upper bound. The engine
  is now 2.1.13's, SQLite 3.53.3 (it was 3.53.4 under the pin; 2.1.14 carries 3.54.0 and comes with Microsoft's next
  servicing). Not taken: the 10.0.14 servicing packages (published 2026-10-25, the out-of-band Brotli fix — which is
  the RUNTIME's, shipped by the SDK the release builds with, not in a package referenced here), Markdig 1.5.0
  (2026-10-24), CsWin32 0.3.366 (it changes how SafeHandles are generated; 0.3.367, its fix, is from 2026-10-22), and,
  as before, `coverlet.collector` 10.1.0 and FluentAssertions 8. Catch2 3.16, NVAPI R615 (its licence file changed),
  the Vulkan and Streamline headers and clang-format (paired with Visual Studio's) were read and not needed.
- **beta.31 (2026-10-26), the owner's request — inside the cooldown, on purpose** ("làm thêm bản beta 31 với các deps
  còn lại (.net 10.0.14) và 1 số thư viện khác"; owner decision D72): `Microsoft.Extensions.Hosting`,
  `Microsoft.Data.Sqlite` and `System.Diagnostics.EventLog` 10.0.14 (published the day before; `Microsoft.Data.Sqlite`
  10.0.14 still declares `SQLitePCLRaw` 2.1.13), Markdig 1.5.0 (its licence text again byte-identical at the packaged
  commit), CsWin32 0.3.367 (0.3.366 changed how SafeHandles are generated, 0.3.367 fixed its overflow; nothing here
  needed a change), Meziantou.Analyzer 3.0.317 (no new finding), Catch2 3.16.1 for the native tests (clean under
  `/W4 /WX`, the `/EHsc` fix kept), and `coverlet.collector` 10.1.0 — `NoSecondMatcherTests` now leaves out the
  `Coverlet.*` types the collector injects, whose constant reflection cannot read; they were never ours. Still not
  taken: FluentAssertions 8 (its licence), and the vendored native SDKs (NVAPI R615's licence file changed; the Vulkan
  and Streamline headers follow the measured loader and titles).
- **The NuGet audit** (`Directory.Build.props`, written down 2026-10-25): `NuGetAudit` on, `NuGetAuditMode` `all`
  (transitive packages too), `NuGetAuditLevel` `low` — the SDK's defaults for net10.0, stated so that a later SDK's
  defaults cannot move them. With `TreatWarningsAsErrors` a vulnerable package is NU1901–NU1904 and the restore, so
  the gate, is red — what `SQLitePCLRaw` 2.1.11 did (`Directory.Packages.props`). `dotnet list FrameLedger.slnx
  package --vulnerable --include-transitive` read none on 2026-10-25.
- **The repository's own alerts:** Dependabot alerts and security updates are enabled, and so are secret scanning and
  its push protection (read from the API, 2026-10-25); no alert of either kind, and no code-scanning alert, was open.

## Branch & release policy

- `main` protected: CI + CodeQL required, linear history, no direct pushes.
- Release: in ONE commit, bump `VERSION` and move the `[Unreleased]` entries of `CHANGELOG.md` under `## [X.Y.Z] - date` (Keep a Changelog format) *(since beta.24, D66: into their own file, `docs/changelog/X.Y.Z.md`, which opens with that heading, plus its row in `CHANGELOG.md`'s Releases table — `release-notes.ps1` reads the version's own file)* → tag `vX.Y.Z` → `release.yml` does the rest (and refuses a tag that disagrees with either file) *(corrected 2026-10-04: for a pre-release tag `vX.Y.Z-beta.N`, do not bump `VERSION` — it already holds the numeric core the gate compares, and it read `0.1.0` through eleven betas; the section is `## [X.Y.Z-beta.N] - date`, the full version. Bump `VERSION` only when the numeric core moves)* → smoke-test the produced installer on a clean Win 10 VM + Win 11 (manual checklist in 14_TESTING §Release smoke).
- Pre-releases: `vX.Y.Z-beta.N` tags mark the GitHub release as pre-release; the App's `stable` channel ignores them and its `beta` channel (Settings ▸ Updates, `update.channel`) includes them — built with P4 PR-5, so ~~explicit beta channel is v2 backlog~~ is struck. The managed assemblies carry the full tag version; the native VERSIONINFO blocks carry the numeric core (`12_BUILD` §Version).
- **Alphas (D73, 2026-10-31):** `vX.Y.Z-alpha.N`, tagged on the `alpha` branch only — `release.yml` refuses one anywhere
  else, and any other tag there — published as GitHub pre-releases on Velopack's `alpha` channel. §The alpha line above.

## Repo hygiene checklist

> Rewritten 2026-09-16 (P5 prep). The list below had every box unticked while three of them were done in the
> tree, and its first box asked for caller stubs this file's own opening paragraph says were never the design.
> In-tree items are stated as done with their location; the rest are GitHub settings only the owner can set,
> listed as such rather than as work a PR could close.

**In the tree — done:**

- [x] Repo-local `ci.yml`, `codeql.yml`, `release.yml`, `rules-publish.yml` (all four repo-local by decision, §Workflows); ~~caller stubs pointing at `poli0981/.github`~~ — never the design, see the first paragraph *(and `wiki.yml` since beta.17, repo-local too: five)*
- [x] Issue forms `bug_report.yml`, `feature_request.yml`, `safety_gap.yml`; `ISSUE_TEMPLATE/config.yml` pointing a vulnerability at private reporting; PR template referencing CLAUDE.md's definition of done
- [x] `.github/dependabot.yml` (nuget + github-actions, weekly; a seven-day cooldown and the actions grouped since 2026-10-25 — and a NuGet library update it never proposed is `dotnet list package --outdated`'s to find, §Dependabot and the NuGet audit)
- [x] `SECURITY.md` — the private route for a vulnerability, the public route for a safety gap, and the honest response intent (2026-09-16)
- [x] A release rehearsal: `release.yml` `workflow_dispatch` runs every step but the Release itself (§release.yml)

**GitHub settings — the owner's (`HANDOFF` §Owner-only):**

- [x] Branch protection as §Branch & release policy states *(marked 2026-10-04, read from the API: required checks `check`, `analyze (csharp, none)` and `analyze (cpp, manual)`; linear history; a pull request required; force pushes off; enforced for admins)*
- [ ] `Rules / validate` as a required check (§S23-2, after its skip-shim)
- [x] Dependabot alerts + security updates (the manifest is in the tree; the alerts are a setting) *(marked 2026-10-25, read from the API: both enabled, with secret scanning and its push protection)*
- [x] Private vulnerability reporting enabled (Settings ▸ Security), so `config.yml`'s link resolves *(marked 2026-10-04: GitHub reports it enabled)*
- [ ] Wiki editing restricted to collaborators (Settings ▸ General ▸ Features ▸ Wikis ▸ *Restrict editing to collaborators only*): otherwise anyone with a GitHub account can change a page until the next release overwrites it (§wiki.yml, beta.17)
- [ ] Repo topics: `windows`, `wpf`, `benchmark`, `fps`, `game-performance` (~~`presentmon`~~ — dropped 2026-08-27, §G)
