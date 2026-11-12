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

### `codeql.yml` — push, PR, weekly cron, `workflow_dispatch` (since 2026-11-11) · **repo-local**

> **Not a caller stub, which this heading said until 2026-08-06.** `codeql.yml`'s own header says
> the opposite: the ops repo has `codeql-mixed.yml`, but C++ needs manual build mode driven by the
> CMake preset and that input surface has not been verified. The paragraph below already anticipated
> exactly this outcome; the heading was never updated when it happened.
- Languages: `csharp` **and `cpp`** (manual build mode for C++, driven by the CMake preset). The native layer is where memory-safety bugs would live; excluding it would defeat the purpose. Verify the mixed template exposes a C++ build-command input; if it does not, this one goes repo-local too.
- **And `actions`, since 2026-10-25** (build mode `none`): CodeQL reads `.github/workflows` for injection from event data, untrusted checkouts and over-broad tokens — the class of finding SonarCloud raised once (`githubactions:S7631`, `wiki.yml`, beta.26). Its context, `analyze (actions, none)`, is not one of `main`'s required checks; adding it to them is the owner's setting.
- **Concurrency, since 2026-10-25:** a new push to a pull request cancels that pull request's run; a push to `main` and the weekly scan each run in a group of their own (the run id), so none is cancelled or left pending, and the default branch's alerts come from a finished analysis.
- `permissions: security-events: write, contents: read`.
- **On .NET 11 since beta.37 (D94) — measured on the pull request that moved the line (#376, run 42916491104, CodeQL
  2.27.4).** CodeQL's documentation lists C# up to 14 and .NET up to 10, and `analyze (csharp, none)`, a required
  context, had never met `net11.0`. With build mode `none` the extractor reads the source with its own compiler front
  end and uses a `dotnet` only for references: it found the 11.0.100 SDK `setup-dotnet` had installed and used that (on
  .NET 10 it downloaded 10.0.100 for itself), restored, and reported 363 resolved references and none unresolved, 9
  types it could not resolve (main's last run on .NET 10, 42862718138: 23, in three `Windows.Win32` namespaces), and
  **`CodeQL scanned 1220 out of 1220 C# files`** — main's line, word for word — in 4 min 5 s. The source it read used
  no C# 15 construct. **What it reads of C# 15 is measured construct by construct before one is used** (D96): a
  construct in whose file the extractor reports an error is not used on main until CodeQL reads it, and the same holds
  for Qodana's engine (§Qodana).
  *(Corrected the same day, two pull requests later: that run was an **overlay** — on a pull request the C# analysis
  extracts the files it changed on top of the database cached from `main`, then a .NET 10 one — so its "1220 out of
  1220" was `main`'s baseline figure and its other numbers the changed files'. The whole source on `net11.0` was first
  read by `main`'s own run after the merge (42921492229, "Overlay mode not enabled"): 1,221 source files, 363
  references resolved and none unresolved, 23 types unresolved in the three `Windows.Win32` namespaces — the figure it
  had on .NET 10 — and "CodeQL scanned 1221 out of 1221 C# files", in 4 min 45 s. So CodeQL reads `net11.0` as it
  read `net10.0`; and for a construct it has not met, a pull request's run is not the evidence — a
  `workflow_dispatch` on the branch is, §C# 15 and the analyses.)*

### `qodana.yml` — push to `main`, pull requests into `main`, `workflow_dispatch` · **repo-local, built 2026-11-10 (D93); Qodana for .NET since 2026-11-11 (D99)**
- ~~One job, `qodana`, on `ubuntu-latest` — the only Linux job in this repository, because the linter is a Linux
  container~~ Two jobs, on `windows-latest`, each a report of its own: **`native`** (*Qodana (native)*) has CMake
  write a Visual Studio solution and reads the C++ of `src/native` with `qodana-native.yaml`'s settings; and
  `managed` (*Qodana (managed)*): `JetBrains/qodana-action` (pinned to
  `v2026.2.2`'s commit; Dependabot moves the pin) runs **Qodana for .NET** in native mode over `FrameLedger.slnx`
  with `qodana.yaml`'s settings, after a `dotnet build`. No job here runs on Linux any more. §Qodana below says what
  it reads, what was changed from JetBrains' template and where an inspection is switched off.
- **Advisory, with a gate of zero problems:** one problem turns the job red; it is not a required context.
- `permissions: contents: read, checks: write, pull-requests: write` — the annotations and the one comment; granted to
  the job, the workflow grants none.

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
  channel (`releases.alpha.json`, `assets.alpha.json`, `RELEASES-alpha`, `FrameLedger.App-alpha-Setup.exe`, and the
  packages themselves, `FrameLedger.App-<version>-alpha-full.nupkg` / `-alpha-delta.nupkg` — measured on the first alpha's
  rehearsal, after which the delta check reads the name from `assets.<channel>.json` rather than building it) and its delta
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
  and `guide/` (the user guide: the End-user part, the same pages the App shows) with `LIMITATIONS.md` and, since
  beta.35 (D88), `ACCESSIBILITY.md`. `tools/wiki-build.ps1` names each page (a guide page after its first heading,
  `guide/README.md` as *User-guide*, `LIMITATIONS.md` as *Limitations*, `ACCESSIBILITY.md` as *Accessibility*), rewrites links between pages to page names and every other repository link to
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

## The alpha line — the `alpha` branch (owner decisions D73–D76, 2026-10-31; resting since D98, 2026-11-11)

What it is, what differs on the branch and why it rests since 0.1.0-alpha.4: `12_BUILD` §The alpha line. What CI and the
release do with it:

- **`ci.yml`** runs `check` on a push to `alpha` and on every pull request into it, under the branch's own `global.json`
  — `setup-dotnet` installs the exact SDK it names whatever the `rollForward`, a prerelease one included. The
  `sonarcloud` job does not run for the branch (the organisation's plan analyses main alone, §SonarCloud), and neither
  does the `Changelog` step: what an alpha changes is written in its release's own file on main, never in an
  `[Unreleased]` entry on `alpha`, which would collide with main's at every sync.
- **`codeql.yml` and `qodana.yml`** stay on main: the source is main's, and it is analysed there. *(Until beta.37 the
  reason given here was the alpha's compiler — C# 15, where CodeQL's extractor read C# 14. Main compiles C# 15 since
  beta.37; what CodeQL reads of it is §`codeql.yml`'s to say.)*
- **The branch is not protected** (an owner's setting; none was asked for): pull requests into it are how changes land
  (squash), and the sync from main is a pull request merged with a merge commit — main's linear rule is main's alone.
- **A release of the line**, in order: the alpha's notes on main (`docs/changelog/x.y.z-alpha.N.md` opening with the
  warning, its row in `CHANGELOG.md`'s Releases table, a line in HANDOFF's START HERE) → the sync pull request into
  `alpha` → a `release.yml` dispatch on `alpha` (`gh workflow run release.yml --ref alpha -f version=x.y.z-alpha.N`) →
  the signed tag on `alpha`'s head → the run (§release.yml: the line's tag rule, `--channel alpha`). **No wiki publish**
  (`wiki.yml` skips an `-alpha.` tag) and **no release line moves**: README, ACCURACY, the Disclaimer's copy,
  LIMITATIONS and ACCESSIBILITY keep naming the newest beta.
- **The cadence while the line tries a .NET:** one alpha per beta, cut from it. Velopack's GitHub source reads the
  newest ten releases (one page, `perPage` 10), so ten alphas in a row would hide every beta from the betas' own update
  check, and the other way round (`11_UPDATER` §Channels). **While it rests** no alpha is cut, and that limit has a
  consequence the alpha's notes state: once ten betas have followed alpha.4, a copy of alpha.3 no longer finds it.

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
duplication exclusions *(and, since 2026-11-12, `src/FrameLedger.Domain/Metrics/HookFamilies.cs`: the Domain's
mirror of `FlHookFamily`, the same enum twice by design and held together by `MetricEnumMirrorTests`. The six
members 0.1.0-beta.45 added to both were 3.7 % duplication on new code against the gate's 3 %, and failed the gate
on the release's commit with no issue open)*; C++20; UTF-8; the OpenCover paths; the compilation database;
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
| `csharpsquid:S3869` `DangerousGetHandle` | 1 | **Accept** | `ParentDump` passes the dump file's handle to `MiniDumpWriteDump` inside the `using` that owns the stream, on the same thread, so it cannot be released during the call; CsWin32 generates no `SafeHandle` overload here (`NativeMethods.json`: `useSafeHandles: false`). **Raised again 2026-11-12:** #398 moved that call into `TryWriteOnce` (the dump is now tried three times), and SonarCloud tracks an issue by where it is — the accepted one closed and a new one opened on the same expression, as a blocker on new code. Nothing about the call changed; it is to be accepted again (HANDOFF owner-only item 38 (j)). |
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

## Qodana — `qodana.yml`, a workflow of its own (owner decisions D93, 2026-11-10; D99 and D100, 2026-11-11)

**Since 2026-11-11 the linter is Qodana for .NET, on the Ultimate licence (D99).** The owner took an Ultimate licence for
the Qodana Cloud organisation and asked for Qodana over every language. So the linter is **Qodana for .NET**
(`qodana-dotnet`: Rider's engine) where it was the Community one, in **native mode** on a Windows runner: no container,
the projects built and loaded with the SDK the job installs from `global.json`, and no `EnableWindowsTargeting`.
`qodana.yaml` names the linter and the mode (`withinDocker: false`); nothing is an action argument, so `qodana scan` on
a developer's PC reads the same file. The job is `managed` — *Qodana (managed)* in a pull request's checks: checkout,
`setup-dotnet`, the NuGet cache restored, `dotnet build -c Release` first — so that a build that fails is one red step
with the compiler's own message, not a report of unresolved symbols — then the action, pinned by commit. No MSVC step,
on purpose: it exports `Platform=x64`, which MSBuild reads as a solution platform. The linter's log says what the token
bought: "Qodana license plan: Ultimate", and the Qodana Cloud project it is linked to (`frameledger`). Still advisory,
still a gate of zero, still `main` and its pull requests alone. From here down to *As it was built* is the job as it
is; below that is D93's own account, which is now history.

**Two reports (D99).** Asked how to gather the C# and the C++, the owner chose two reports from the start, and made a
second Qodana Cloud project (`frameledger-cpp`, the `QODANA_TOKEN_NATIVE` secret). The managed report leaves
`src/native` out; the native one (*The native report*, below) reads nothing else, so every file is in exactly one.

**What is read, and by what.**

| What | Read by | |
|---|---|---|
| The C#, XAML and `.resx` of the thirteen projects | `managed` | ReSharper's engine, as under D93 |
| Markdown and XML around the solution | `managed` | the IntelliJ platform's own inspections — 23 and 10 results on the first run |
| JetBrains' security rules over the C# — pattern rules for a process start, an SQL text and the like | `managed` | new with this linter (its OpenGrep plugin) — 36 results on the first run, every one read; the files they were read in are listed in `qodana.yaml`, so a new one is reported |
| The C++ of `src/native` | `native` | ReSharper's C++ engine and the clang-tidy checks it bundles, over a Visual Studio solution CMake writes for it |
| JSON and YAML, the workflows among them | nothing reported | whether the profile inspects them and found nothing, or does not inspect them, was not established; CodeQL's `actions` language reads the workflows |
| PowerShell (`build.ps1`, `tools/`), CMake files, the HLSL shader | no linter of JetBrains' reads them here | SonarCloud reads the PowerShell |

**The first runs on this linter, and what was done (2026-11-11).** Run 42923079119 — the job with no settings beyond the
solution: **3,953 problems** in 92 inspections, and 3,607 of them **hints**: the lowest level an IDE has, which the
editor offers without underlining anything (this linter files them as `TYPO`, Qodana as *Low*). Meziantou's "name the
parameter" 1,084 times, a trailing comma 979, a XAML binding whose `DataContext` the engine cannot know 414, the MVVM
Toolkit's partial-property proposal 224, "invert `if`" 114 — and every other Roslyn analyzer message below the level
this build makes an error. **Hints are off as a severity**, one group in `qodana.yaml` (`severity:TYPO`): the Community
linter never reported them, and D93's rule is about warnings and suggestions. With that, run 42928814104: **347**.

| What | n | Done |
|---|---|---|
| `OverriddenWithSameValue` — a vi or ja value equal to the English one | 271 | off in `.editorconfig` (`[*.resx]`): product and technology names, units and format strings are not translated, and a ja safety string is the English verbatim until a reviewer signs it |
| `csharp_injection_rule-CommandInjection` — a process started with something that is not a literal | 35 | **read, one by one** (below): 11 places in `src`, 24 in tests; one place was hardened. The rule stays on, and the files read are left out of the plugin's inspection by path in `qodana.yaml` (*How the security rules are scoped*, below), so a new file that starts a process is reported |
| `MarkdownIncorrectTableFormatting` | 20 | off in `qodana.yaml`: Rider wants columns padded to one width; these tables are written to be diffed |
| `XmlHighlighting` in `src/FrameLedger.App/app.manifest` | 10 | that file ignored for that inspection: the linter has no schema for a Windows application manifest |
| `UnusedVariable.Compiler` and `RedundantUsingDirective` in `PrimaryDisplay.cs` | 6 | **the linter's own fault**, and the file is left as it is: resolving `PInvoke.EnumDisplaySettings(null, …)` — the `null`-to-`PCWSTR` conversion — the engine of this linter's build (QDNET-262.12299) fails an internal assertion (`IsStandardImplicitConversionFromExpression(conversion.Kind)`, in its `backend.log`), loses the method, and reports its three locals and the file's three usings as unused. The compiler builds the file with warnings as errors, and InspectCode 2026.2.3.1 and 2026.3.0 report nothing in it. Those two inspections are excluded for that one file in `qodana.yaml`, to be removed when a linter build reads it |
| `MarkdownUnresolvedHeaderReference` | 3 | off in `qodana.yaml`: all three resolve on GitHub, whose anchor rule the linter does not follow (`#display-mode--both-tiers-beta10`) |
| `csharp_injection_rule-SQLInjection` | 1 | the same: the one place assigns a constant command text and binds every value as a parameter, as every statement in this repository does; its file is listed with the others |
| `RedundantBoolCompare`, in a test | 1 | fixed — the expression also carried a clause its first half implied |

With those settled, run 42940926340 (2026-11-11, this pull request's branch, the whole solution): **no problem
reported** — the gate's zero, on this linter.

**How the security rules are scoped — by the plugin's one inspection, not by a rule.** Every result of the
OpenGrep plugin's several hundred rules is produced by one inspection, `OpenGrepInspection`; a rule's own id
(`csharp_injection_rule-CommandInjection`) is in the report and is honoured nowhere. Three runs measured it — a
profile entry's `ignore` (42932842462), an `exclude` path under the rule's id (42935752891), `enabled: false`
on the rule (42938295264): all 36 results stood each time, and the linter's log shows why — the plugin hands its
server every rule file whatever the profile says. What the plugin's inspection does honour is a path: an
`exclude` entry named `OpenGrepInspection` (its scopes are in the report's `log/effective.profile.xml`). So the
eleven files of `src` the results were read in, and `tests`, are listed there — and in the run above the
inspection was performed on 943 files, where it had been 1,308, and reported nothing: it is on, and it reads
what is not listed. The price is stated in `qodana.yaml`: a listed file is left out of **all** of the plugin's
rules, not only the one that fired. The plugin is also most of the job's time — about six of its fifteen
minutes.

**A second tripwire, in the gate: `ProcessStartSitesTests`.** Qodana is advisory, and its rule is a pattern for
`Process.Start`. The test sweeps `src` for every way a process is started here — `Process.Start`, a
`ProcessStartInfo`, `CreateProcessWithTokenW` through CsWin32, and the registration of the logon task — and fails
on a file that is not in its list, until that file has been read and listed. `Startup/UnelevatedProcess`, which
the rule never saw, is in it.

**Every place that starts a process, as read on 2026-11-11.** The rule is a pattern, not a data flow: it reports any
`Process.Start` whose argument is not a literal. What matters at each is what is started and where its path and
arguments come from.

| Place | What it starts | Where the path and the arguments come from |
|---|---|---|
| `App/Services/AgentLauncher` | `FrameLedger.Agent.exe` | the file beside the App (`AppContext.BaseDirectory`); arguments the App builds (`--serve`, the data folder) |
| `App/Services/AgentTool` | the same Agent, with one maintenance flag | a constant at each of its two callers (`--install-task`, the Settings page's flags) |
| `Infrastructure/Startup/AgentElevation` | the Agent, elevated (`runas`) | the Agent's own path; `--serve --for-user <this user's SID>` |
| `Infrastructure/Startup/LogonTask` | nothing itself — it registers the logon task, whose action Windows starts at sign-in | the Agent's path and constant arguments (`--serve`), into a Task Scheduler definition for this user, at the lowest privileges |
| `Infrastructure/Diagnostics/CrashDumpWriter` | this process's own executable, as the dumper | `Environment.ProcessPath`; an `ArgumentList`, no shell |
| `Infrastructure/Capture/ProcessLauncher` | the game | the executable the user added and enabled hooking for, and the launch arguments the user wrote for it; no shell |
| `App/Services/ShellStarter` | a game, directly or through its store (`steam://…`, `explorer.exe shell:AppsFolder\<AUMID>`) | the launch `GamePlayer` resolves from the record of a game the user added (D59, D80) |
| `App/Services/Explorer` | `explorer.exe` on a folder or a file | a quoted path of FrameLedger's own: the data folder, the logs, a bug report's zip, the licences folder |
| `App/Services/ShellUrlOpener` | the browser or the mail client | the project's own pages, or a Markdown link `MarkdownLinks` has already held to `http`, `https` and `mailto`. **Hardened here**: the opener now refuses any other scheme itself (`ShellUrlOpenerTests`) — the shell runs whatever it is handed, and a `file:` address is a program |
| `App/ViewModels/SettingsViewModel` | the browser | one constant address |
| `Infrastructure/Startup/UnelevatedProcess` | the Agent or a game with the shell's token, from an elevated process (`CreateProcessWithTokenW`) | its two callers above, `AgentLauncher` and `ProcessLauncher` — not a `Process.Start`, so the rule did not see it; the sweep does |

None takes a value from outside the PC or from a file FrameLedger did not write, except the game's own path and
arguments — which starting the game the user chose is.

**The native report — `qodana-native.yaml`, the job `native` (D99, 2026-11-11).** The C++ of `src/native`, read by
the same linter — Qodana for .NET, whose C++ is ReSharper C++ — in native mode on a Windows runner. It is the only
linter of JetBrains' that can: the native layer is MSVC-only, and the two C/C++ linters (`qodana-cpp`,
`qodana-clang`) are Linux images. It reads C++ through a Visual Studio solution, and `src/native` is a CMake tree, so
the job has CMake write one first: the preset **`x64-vs-analysis`** (generator "Visual Studio 18 2026", tests and
tools on) puts `FrameLedgerNative.slnx` under `build/native/x64-vs-analysis`, and the job builds from it the one
target whose output is a header (`fl_ac_floor_gen`, the blocklist floor). Nothing else is compiled, and nothing from
that tree ships. Then the action, with `--config qodana-native.yaml`: the solution, `RelWithDebInfo` and `x64` — the
flags the shipped binaries are built with — and, left out by path, what is not FrameLedger's code (the build tree,
`third_party`, the vendor stubs, a compiled shader as a byte array). Its own results folder, cache keys and artifact
(`qodana-report-native`), and the Qodana Cloud project `frameledger-cpp` through `QODANA_TOKEN_NATIVE`. It posts no
comment and no annotations — a second job of the same linter would overwrite the managed job's — so its result is the
job's own status and its report. Advisory, and a gate of zero, like the other.

**What it runs** is ReSharper's C++ inspections and, through them, the clang-tidy checks and clang diagnostics that
engine bundles. clang never builds this code — MSVC does, with `/W4 /WX` — so what clang says is read as advice. Every
one of them has a key in `.editorconfig` (`resharper_cpp_…_highlighting`; a clang-tidy check's is
`resharper_cpp_clang_tidy_<check>_highlighting`), so D100's rule holds without a second file: no `.clang-tidy`.

**Its first runs, and what was done.** Run 42923079119: **4,695 problems**, 4,040 of them hints, which are off as on
the managed side. Run 42928814104: **655** — **494 distinct**, because a source file compiled into three targets is
reported three times.

| What | distinct | Done |
|---|---|---|
| A choice this code made, off for the native tree: where the compiler pads a struct (46); MSVC's own keywords, `__try` among them (54); a function pointer got by name cast to its real type, through `void*` (55); `reinterpret_cast` where memory is read as something else (5); `std::cmp_less` proposed for comparisons that already cast (20); a `default:` under a full enum switch (3); a copied verdict (4); the moves of a type that owns a handle (11); a zeroed out-parameter enum (8); an enum's width (5); "can be made const / constexpr / static" (52); "possibly unused `#include`" (15) | 278 | off in `.editorconfig`, each with its reason. Of the fifteen includes two were unused and were removed; the others are a file including what it uses although another header already brings it |
| A newer idiom proposed for working code — a range-for, `std::ranges`, `any_of`, `starts_with`, a raw string, an `if` with an initialiser | 53 | off: the native code is written to be read beside the hook path, an index and a plain loop |
| **The engine misreading**: `std::atomic_ref<T>::fetch_or` as an expression without side effects (7); a Win32, D3D12 or ffx-api structure zero-initialised and then filled field by field as "always rewritten" and "assigned, never used" (15); a test of a `constexpr` function as a redundant argument (3) | 25 | off, with what the engine gets wrong written beside the keys |
| Fields nothing names, in a structure Windows defines and the SDK's headers do not give (`RTL_PROCESS_MODULE_INFORMATION`, `PROCESS_MEMORY_COUNTERS_EX2`) and in a test's stand-in for a Vulkan object | 25 | off for those four files: the fields are what puts the others at their offsets |
| `%lu` with an `NvU32`, in `fl-probe-nvapi` | 19 | off for that file: NVAPI's header makes `NvU32` an `unsigned int` under clang and an `unsigned long` under MSVC — the linter's compiler, not the build's |
| The probes, the harness and the tests: an unchecked `printf` (35), `atoi` on the harness's own arguments (20), a `&pointer` handed to a `void*` parameter (7), a printf-shaped helper (4), a fake handle built from a number (4), a test's fixtures (4), `layout_test`'s run-time checks of compile-time facts (3), a macro that pastes a name (1) | 78 | off for `tools` and `tests` |
| Code loaded into a game (below) | 7 | kept as it is, off for those files; one of them is dead code, recorded for the owner |
| **Fixed**: `sizeof(a) / sizeof(a[0])` → `std::size(a)` (4); a multiplication done in a narrower type than the one it is stored in or compared with (2: a constant in the guard's sources, a test); a member function, and a type alias, nothing used (3) | 9 | in the code; none changes what a shipped binary does |

With those settled, run 42942068024 — the first with these settings — reported **one** problem: `LogNoteW`, now by
ReSharper's own inspection where clang's had said it before (below). And run 42943094415 (2026-11-11, this pull
request's branch): **no problem reported** — the gate's zero, in the native report too.

**Code loaded into a game — the Overlay, the Vulkan layer, the shared-memory headers, the guard's rule matcher.**
D100: there only a defect is fixed, in a pull request of its own with the hook's overhead measured (rule 5,
`14_TESTING` §Hook overhead), and a proposal about style is not acted on. About a quarter of the 655 results were in
those files; nearly all of them are the tree-wide kinds above. **The report found no defect there.** What is
particular to them, read and kept: two `if` chains whose arms have the same body and are different cases, each with
its comment (`dllmain.cpp`); MinHook's `MH_ALL_HOOKS`, which is `NULL` by its header; a retired enumerator kept so
that its bit is not reused; `slSetTag`, deprecated by a header whose replacement no measured module exports;
`ResolveTolerance` clearing its out-parameter with `memset`, as `ResetRules` does — a `Tolerance` is flags whose
default initialisers are zero. And one thing that is not as it should be: **`LogNoteW` in `dllmain.cpp`, which nothing
calls** — dead code. It is not a defect, so D100 leaves its removal to the owner (HANDOFF); until then that one file
is not read for an unused function (`.editorconfig` says so, in a section to be removed with it).

**"Zero" has to mean "analysed".** A report with no problem cannot tell a clean tree from one the linter did not
read. So the settings were proved on a branch that puts back three of the things fixed here — the unused alias, the
unused member function, the narrow multiplication. Run 42942390715 reported exactly those three, each by the
inspection that had found it the first time (`CppTypeAliasNeverUsed` in `tools/fl-probe-interposer/main.cpp`;
`clang-diagnostic-unused-member-function` and `bugprone-implicit-widening-of-multiplication-result` in
`tests/ring_test.cpp`), and `LogNoteW`, and nothing else. The branch was deleted. Do the same before trusting a
zero after a change to the preset, the settings or the linter's version.

**What it does not read.** CMake files and PowerShell have no inspection in this linter (SonarCloud reads the
PowerShell); the HLSL shader and the `.def` file are not C++. CodeQL (`analyze (cpp, manual)`, a required check) and
SonarCloud go on reading the C++ as before: this is a third reader, and the only one of the three that is ReSharper's.

**Where an inspection is switched off: by the engine that raised it (D100, amending D93's "never in `qodana.yaml`").**
What Qodana reports is fixed, unless the inspection argues with a choice this project made; then it is off, with the
reason on the line above — in the place the engine that raised it reads:

- **ReSharper's** — the C#, the XAML, the `.resx`, and the C++ of the native report with the clang-tidy checks it
  runs — in **`.editorconfig`** (`resharper_*_highlighting`), where Rider and ReSharper in the editor read it too:
  sixty-four keys for the managed code, fifty-four lines for the C++, some of them for a folder or a file alone.
- **What has no key there** — in **`qodana.yaml`**: a whole severity (the hints) and the IntelliJ platform's Markdown
  and XML inspections under `profile.inspections`; under `exclude`, by inspection and path, the files JetBrains'
  security rules were read in and a file the linter itself fails on.
- **What is not FrameLedger's code** is left out by path (`exclude`): `SonarQube.Analysis.xml`'s list, for its reasons.

**What it does not use, and what happens if the licence lapses.** Quick-fixes (the linter would push commits — the job
has no `contents: write`, and a change nobody read is not wanted), code coverage in Qodana (the gate's coverage and
SonarCloud's already read the same reports) and FlexInspect are Ultimate features this job leaves alone. Without a
valid licence the linter does not start, so the job is red — advisory, never a blocked merge; the way back is
`linter: qodana-cdnet` with the job on `ubuntu-latest`, as it ran until beta.37.

**Running it on a developer's PC.** `qodana scan` in the repository root reads `qodana.yaml`, downloads the linter and
needs the project's token in `QODANA_TOKEN` — the Ultimate linter checks the licence. Without a token, InspectCode
still gives ReSharper's part (*As it was built*, below), with `--toolset-path` at the 11.0 SDK's `MSBuild.dll`; the
Markdown, XML and security results are the Rider linter's alone.

**As it was built — the Community linter, 2026-11-10 to beta.37 (D93).** Kept as written; where it says *is*, read *was*.

**D93 — Qodana reads the C# and the XAML from CI.** The owner made the project on Qodana Cloud, set the `QODANA_TOKEN`
secret (2026-11-10) and asked for Qodana in CI from the workflow JetBrains gives, changed where it had to be, with what
it reports fixed. Asked which licence the Qodana Cloud organisation holds, the owner answered: a 30-day trial. So the
linter is **Qodana Community for .NET** (`qodana-cdnet`) — ReSharper's InspectCode in a Linux container, which runs
under every licence — and not Qodana for .NET (`qodana-dotnet`, Rider), which needs Ultimate and would turn the job red
the day the trial ends. JetBrains calls the Community linter Early Access.

**How it runs.** `.github/workflows/qodana.yml`, on every pull request into `main` from this repository, every push to
`main`, and by hand: a full-depth checkout of the pull request's own commit, then `JetBrains/qodana-action` pinned to
the commit of `v2026.2.2` (the newest; the template names `v2026.1`, and every action that is not GitHub's own is pinned
by commit here). The action brings the Qodana CLI of its own version, the CLI the linter's image
(`jetbrains/qodana-cdnet:2026.2` — InspectCode 2026.2.3.1 on the image's .NET SDK 10.0.403), and the linter reads
`qodana.yaml`: the solution (`FrameLedger.slnx`), the configuration (Release), the gate. It builds the solution itself,
then inspects it — the whole solution every time (`pr-mode: false`), so a pull request and `main` are judged by one
list. The report goes to Qodana Cloud, a summary to the pull request as a comment and annotations, and the SARIF and
the linter's logs to the run as the `qodana-report` artifact.

**What had to change from the template, and why.**

- **`EnableWindowsTargeting=true`, passed to the container** (`env-vars-to-linter`). Every project here targets
  `net10.0-windows`, and the .NET SDK on Linux refuses such a project unless that property is set. With it, the
  container restores, builds and loads all thirteen projects — WPF's markup compiler and CsWin32's generator are managed
  code — which the first run showed (below). It is the workflow's, not `Directory.Build.props`': no build of the product
  is meant to run anywhere but Windows. JetBrains' own page says a project with an OS-specific target needs Qodana for
  .NET in native mode; that is the Ultimate linter, and the Community one did the work.
- **`ubuntu-latest`, the one Linux job here** — the Community linter ships as a Linux image only.
- **Permissions:** `contents: read`, `checks: write` (the annotations), `pull-requests: write` (the comment). The
  template's `contents: write` is for pushing quick-fixes, an Ultimate feature this job never uses.
- **`main` only.** The `alpha` branch is main's source under another release line (§The alpha line); main's is what
  is analysed. *(Until beta.37 the reason was the alpha's prerelease .NET, which the image does not carry.)*
- **Interim since beta.37 (D94, 2026-11-11): the .NET 11 SDK is installed into the container first.** Main builds on
  .NET 11 and the image carries the SDKs 8 to 10, so `qodana.yaml` has the `bootstrap` line JetBrains' page gives for
  an SDK the image lacks — `dotnet-install.sh` with `global.json`, into `/usr/share/dotnet`. It lasts until the
  Ultimate linter's pull request replaces this job with one on Windows, where `setup-dotnet` does it. Measured on its
  first run (#376, run 42916491047): the script installed SDK 11.0.100 beside the image's own, InspectCode 2026.2.3.1
  loaded and inspected all thirteen `net11.0-windows` projects, and it reported one problem — a redundant `!` in the
  test that pull request added, fixed there. So the 63 keys and the zero held across the move.
- **Not a fork's pull request, nor Dependabot's:** neither gets the secret or a token that can write the comment.
- **Caches are saved from `main` only** (`cache-default-branch-only`); a pull request reads them.

**What it reads, and what it does not.** The C# of the thirteen projects, their XAML and `.resx`. **Not the C++:**
`src/native` is a CMake tree no solution names, and it is MSVC-only, which no Linux container compiles — CodeQL and
SonarCloud read it. The native DLLs are absent in the container, so the `.targets` that copy them warn and the build
goes on, as on a fresh clone (measured here the same day: `dotnet build` of a clean worktree, 15 warnings, 0 errors).

**The gate is zero, and it is advisory.** `qodana.yaml` sets `failureConditions.severityThresholds.any: 0`: one
problem and the job is red. `main`'s required contexts stay `check`, `analyze (csharp, none)` and
`analyze (cpp, manual)`, so a problem Qodana finds — or a Qodana Cloud outage — is a red job beside a green `check`,
never a blocked merge. That it can fail was shown before it was
relied on: run 42847369329 had the settings and none of the fixes, found 409 problems and ended red — "Detected 409
problems across all severities, fail threshold 0".

**Where an inspection is switched off: `.editorconfig`, never `qodana.yaml`.** InspectCode reads its severities from
`.editorconfig`'s `resharper_*_highlighting` keys, and so do Rider and ReSharper in the editor — one list for the job
and for the person at the keyboard, beside the analyzer severities already kept there, and testable without CI. Each
key has its reason on the line above it. The rule: **what Qodana reports is fixed, unless the inspection argues with a
choice this project made** — then it is off, and the reason says which choice. Sixty-three keys, in these groups:

| Group | Inspections | The choice it argues with |
|---|---|---|
| The compiler's missing-XML-comment warning | `CS1591` | already a suggestion in this file; InspectCode reports suggestions — 2,050 of the first run's 3,679 |
| Naming | `InconsistentNaming` | names follow this file's rule and the .NET analyzers'; ReSharper's scheme renames `OpenGL`, `XeSS`, `Presents5s` and every `Chart_*` resource key |
| Unwrapping a nullable | `ConvertTypeCheckPatternToNullCheck`, `PatternAlwaysMatches` | `x is long v` and `x is > 0 and int v` name the type, as `csharp_style_var_for_built_in_types = false` asks |
| What a guard is for | three `…AccordingToAPIContract` inspections | the compiler's nullable analysis is the gate; a dependency property, a JSON document and a WPF event hand over null whatever an annotation promises |
| Said on purpose | `RedundantExtendsListEntry`, `RedundantArgumentDefaultValue`, `RedundantEmptySwitchSection`, `MemberHidesStaticFromOuterClass`, `CompareOfFloatsByEqualityOperator` | a code-behind names its base; an argument repeats a default it relies on; `default: break;` says the rest is left alone; exact float comparisons are with a copied value or a sentinel |
| Not this code's to change | `UnusedParameterInPartialMethod`, `Xaml.StaticResourceNotResolved`, `Xaml.PossibleNullReferenceException`, `LocalizableElement` | CommunityToolkit's generated signatures; WPF UI's run-time resources (`PagesLoadTests` fails a wrong key); a binding through null shows nothing, by design; exception and log text is English (`09_I18N`), and `resx-audit` gates what a user reads |
| Records | `NotAccessedPositionalProperty.*` | a positional property is part of equality and of what the record prints |
| Style proposals | twenty, from `ConvertToPrimaryConstructor` to `UseAwaitUsing` | the style is this file, `dotnet format` and the analyzers; `await using` under CA2007 reads worse than what it replaces |
| Accessibility proposals | `MemberCanBePrivate.*`, `PropertyCanBeMadeInitOnly.*`, `AutoPropertyCanBeMadeGetOnly.*` | a member's accessibility is what its type offers, not a count of today's callers |
| "Never used" across the solution | eight `.Global` inspections | enum values mirror the native layout and stored columns, exception types carry CA1032's constructors, rows and messages are read by Dapper and System.Text.Json; the private kind (`.Local`) stays on — and what this group named on 2026-11-10 is listed below |
| In `tests/` only | eleven | an assertion lambda runs inside the `using` it captures from; a test that dereferences null fails, which is its job; a test double keeps the shape of what it stands for |

Five places carry a `// ReSharper disable once …` comment with its reason instead, where the inspection is right in
general and wrong once: the tolerance channel closed early in `CaptureSession`, the volatile read in
`SessionEventPublisher`, the awaited `Task.Run` in `CaptureOrchestrator`, the window read lazily in
`SessionSummaryOpener`, and a test row that is never on.

**The first run, and what was done (2026-11-10, PR #373).** Run 42842741359, the template's shape with no settings:
the container built the solution and inspected it in under six minutes, and reported **3,679 problems** — 858 warnings
and 2,821 suggestions, in 93 inspections. About 400 were fixed in the code, in 181 files, none changing what the
software does:

| What | n | Done |
|---|---|---|
| Redundant qualifiers, casts, `using` directives, `!` suppressions, `unsafe` modifiers, tuple names | 290 | removed, by script from the report's own positions; the build (warnings as errors, Release and Debug) and `dotnet format` held every one — one cast of a pair in `SqliteGameMerge` was needed after all, and stayed |
| `var` for a built-in type, a block body for a one-line method | 52 | this file's own style (`csharp_style_var_for_built_in_types`, `…expression_bodied_methods`), applied by `dotnet format` |
| XAML bindings the inspection could not resolve | 35 | every item template names its `DataType`; a binding that reached the page through `DataContext.ViewModel` with `AncestorType=Page` names the page's type and its `ViewModel` — the same object at run time, and now navigable in the editor (`ViewerModeTests` pins the new text) |
| Left over in the code | 12 | `LogsViewModel`'s unread `_strip` field and `PipeServer.Client`'s unread `_run`; two initialisers a constructor overwrote; `GameFileProbe.UnityDataFile`'s unused parameter; `SpokenTextAutomationPeer` no longer assumes `GetNameCore` returns a string; a redundant setter and two grid positions in XAML; a `$` on a string with no hole; `!Any(==)` as `All(!=)`; a `foreach` variable's type |
| In the tests | 14 | unused variables, lambda parameters and an accessor; a linear `Contains` on a set; three simplifications |

The rest was settled by the settings above — 2,050 of it the one inspection the project had already made a
suggestion. A suggestion to use `m[^1]` inside an expression tree was wrong (CS8790) and its inspection is off.

**The runs (PR #373).** 42842741359 — the template's shape, no settings: 3,679 problems, the job green (no gate yet).
42847369329 — the settings and the gate, none of the fixes: 409 problems, every one in an inspection left on, the job
red; so `.editorconfig`'s keys are read in the container as they are on Windows, and the 3,270 others were the
settings'. 42849331796 — the fixes: **0 problems**, the job green; and `main`'s first run, the merge of #373
(run 42852304026): 0 problems. A job takes about ten minutes.

**Named by the "never used" group and left as it is (2026-11-10).** Nothing in the solution, its tests included, uses
these; whether each is vocabulary, a piece not wired yet, or dead is the owner's to say, so none was removed:
`AtomicFileWrite` (the class and `PublishAsync`), `ExecutableMarkerScan` and `IExecutableMarkerScan.Scan`,
`FpsPresentation.GeneratedLine`, `IPageNavigator.GoBack`, `OperatorDisclosure.VersionOf` and `ProvenanceOf`,
`CrashReporterCatalog.AlwaysOn`, `ProcessWatcher.Tracked`, `LedgerWalCheckpoint.Complete`,
`LhmTelemetryOptions.EnableCpuAndMemory`, `MeasuredFacts.StreamlineNoneWithheldFrom`, `ILegalAcceptanceStore.ListAsync`
and `ITelemetryPoller.Dropped` (only their implementations are called), `DocumentBrowser.PageChanged` and
`UrlOpener`'s getter, the charts' `PercentilePlot`, `NoteText` and `TempsPlotView`, `GameDetailViewModel`'s
`TrendEmptyText`, `LatencyHeader` and `SensorsEmptyText`, `GamesViewModel.SortLabel`, `TriStateChipModel.IsNo`,
`AboutWindow`'s `AppTab` and `ThirdPartyTab`, `NvapiNgxWords.Degraded`, `IpcMessages`' `TelemetryUnavailable` and
`DbWriteFailed`, `IpcProtocol.Keepalive`, `ShmLayout.GuardTickDeadlineMs`, `HarnessOverlayLogSweep.Own`; and the
getters of 49 auto-properties, among them view-model texts no page binds and columns of stored rows. The list is
`UnusedMember.Global` and its siblings in run 42842741359's report.

**Running it on a developer's PC.** The same InspectCode is a .NET tool:
`dotnet tool install JetBrains.ReSharper.GlobalTools --version 2026.2.3.1 --tool-path <somewhere>`, then, after
`dotnet build FrameLedger.slnx -c Release`:
`jb inspectcode FrameLedger.slnx --no-build --format=Sarif --output=<file> --caches-home=<folder> --properties:Configuration=Release --toolset-path="C:\Program Files\dotnet\sdk\<11.0.x>\MSBuild.dll" --dotnetcore="C:\Program Files\dotnet\dotnet.exe"`.
Two things measured on the owner's PC: without `--toolset-path` it picked Visual Studio 18 Insiders' MSBuild, which
crashed at start ("No files to inspect were found"); and a `--caches-home` a failed run had written left one project
unloaded on the next (2,875 false compile errors, all in `Infrastructure.Tests`) — a fresh folder loaded all thirteen.
A clean run there reports what the container does, to within a handful of findings that differ between Windows and
Linux (3,679 in the container, the same inspections on Windows with counts a few apart).

**What it does not cover, and what would change it.** No C++ (above); no `alpha`; no pull request from a fork or from
Dependabot; no quick-fixes, coverage or licence audit (Ultimate). **If the organisation gets an Ultimate licence** —
JetBrains has a programme for open-source projects — Qodana for .NET runs natively on Windows: `linter: qodana-dotnet` in
`qodana.yaml`, `runs-on: windows-latest`, `args: --within-docker false`, .NET set up from `global.json` first, and
`EnableWindowsTargeting` dropped. Until then the trial's end changes nothing here: the token only names the project
the report is sent to.

## C# 15 and the analyses (owner decision D96, 2026-11-11)

The main line compiles C# 15 since beta.37 — `LangVersion latest` on the .NET 11 SDK — and the owner asked for its
constructs to be used. **None is used yet, and this section is why.** The rule the plan was approved with: a construct
is used on main only where (1) the build, its analyzers and `dotnet format` take it with no analyzer switched off,
(2) CodeQL extracts its file without an error, and (3) Qodana reports nothing that is only its engine not knowing the
syntax — the first two are required checks, and Qodana's gate is zero. Each construct was written once, in a file of its
own, and shown to each tool (`spike-notes` §29, the canary):

| Construct | Build and analyzers | ReSharper's engine — InspectCode 2026.2.3.1, which Qodana 2026.2 runs, and 2026.3.0 | Used on main |
|---|---|---|---|
| Collection expression arguments, `[with(…), …]` | taken | an error: "Feature 'collection expression arguments' is not available. Please use language version 'preview'." | no |
| Closed hierarchies, `closed` | taken | not parsed ("Cannot resolve symbol 'closed'") | no |
| Unions, `union` | CA1815, from the SDK's own rules, on every union | not parsed ("Cannot resolve symbol 'union'") | no |
| Labeled `break` / `continue` | taken; `dotnet format` moves the label one indent to the left | not parsed by 2026.2.3.1; "not available… language version 'preview'" in 2026.3.0 | no |

**CodeQL's reading of them is not established**: its documentation lists C# up to 14, and a pull request's C# analysis
is an overlay that prints no per-file result (§`codeql.yml`). A full analysis of a branch is a `workflow_dispatch` of
`codeql.yml` on it.

**What moves this:** a ReSharper engine that reads C# 15 as a released language, in the Qodana linter this repository
runs; then the canary again, and a dispatched CodeQL run. `spike-notes` §29 lists where each construct would go.
**What main does take from .NET 11:** its API — the `string` overloads that name the comparison of a `char` search, and
`Process.TryGetProcessById`. The two preview features (runtime-async, the updated memory-safety rules) stay out, as D76
kept them out of the alphas.

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
- **No `dotnet-sdk` entry, on purpose:** `global.json` is a floor (11.0.100 since beta.37, D94; 10.0.100 before) with
  `rollForward: latestFeature`, so a developer's machine and the runner already build with the newest SDK of that band
  they have (the runner installed 10.0.402 on 2026-10-20, 10.0.404 on 2026-11-10). Raising the floor is a decision
  about what a contributor must install, not an update — the owner's, as D94 was.
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
  (transitive packages too), `NuGetAuditLevel` `low` — the SDK's defaults for net10.0 when they were written down,
  stated so that a later SDK's defaults cannot move them (and net11.0's did not). With `TreatWarningsAsErrors` a vulnerable package is NU1901–NU1904 and the restore, so
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

- [x] Repo-local `ci.yml`, `codeql.yml`, `release.yml`, `rules-publish.yml` (all four repo-local by decision, §Workflows); ~~caller stubs pointing at `poli0981/.github`~~ — never the design, see the first paragraph *(and `wiki.yml` since beta.17, repo-local too: five; and `qodana.yml` since 2026-11-10, D93: six)*
- [x] Issue forms `bug_report.yml`, `feature_request.yml`, `safety_gap.yml`; `ISSUE_TEMPLATE/config.yml` pointing a vulnerability at private reporting; PR template referencing CLAUDE.md's definition of done *(and since 2026-11-09, beta.35, D88: `accessibility.yml`, the form `ACCESSIBILITY.md` sends a barrier to, and the PR template's user-interface section)*
- [x] The labels the forms apply exist *(2026-11-09: `accessibility`, and `safety` and `priority-high`, which `safety_gap.yml` had named since it was written (2026-08-02) while the repository had neither — GitHub applies only a label that exists, so no Safety gap report was ever labelled)*
- [x] `ACCESSIBILITY.md` — the accessibility statement: WCAG 2.2 AA through WCAG2ICT as a goal, never a claim; how a barrier is reported and handled (2026-11-09, D88)
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
