# HANDOFF — corrections and traps

*Moved verbatim from `docs/HANDOFF.md` on 2026-10-19 (beta.24, owner decision D66). Code and documents cite these as `HANDOFF §Traps`.*

## Two corrections carried forward, because both changed what got built

- **§S29(a) was wrong and had already re-ordered the work.** It claimed the assertion
  catching "a mask bit set with no hook behind it" lives only in
  `ShmDrainIntegrationTests`, which CI skips. It does not: `guard_test.cpp`'s *"the
  injected Overlay records real presents into the ring"* asserts it, as ctest
  `fl_guard`, **20.58 s on CI**. `fl_guard_test.exe` is a *native* host and never loads
  the `protect`-matching .NET assembly — the same §S19(b) mechanism cutting the other
  way. Corrected in place.
- **`README:14`'s RayQuery claim is not a rule-7 contradiction**, which an audit
  asserted. `03_METRICS:128` says AS-build hooking is what makes inline RayQuery
  detectable *at all* (line numbers as of 2026-08; both passages have since moved — noted 2026-10-04). The real contradiction is between CLAUDE.md rule 7 and
  `03_METRICS` — recorded as §S29(f).

---

## Traps that cost a cycle each, in this repo

The mechanical ones live in the toolchain notes; these are the ones that cost a *wrong
diagnosis*.

- **A VERB THAT PRINTS WAS A WRITER UNTIL 2026-09-16, AND THE OWNER'S LEDGER IS NOT A FIXTURE.** `--console
  sessions` against `%LOCALAPPDATA%\FrameLedger\ledger.db` applied two migration scripts, because every verb opened
  through the one migrating path. The print verbs open read-only now (`Program.PrintsOnly`; a schema older than the
  build is refused, exit 7) — but the rule that outlives the fix is: **nothing from a session is run against the
  owner's data directory unless the owner asks for exactly that**; `--data-dir` exists for everything else — and since
  beta.15 for the App too (`FrameLedger.exe --data-dir <copy>`, a viewer, D52), so a UI change can be looked at over a
  copy of the owner's ledger made with SQLite's backup (`12_BUILD` §Debugging). The same
  day's ledger held a whole day of rows only in a WAL a fresh connection discarded (`06_DATA_MODEL` §Migrations
  carries the shape); if `ledger: wal at open` in `agent-*.log` or `ui-*.log` reports frames that a passive checkpoint
  did not move while no other FrameLedger process is running, stop and read that note before touching the file.
- **THE TESTS WRITE INTO THE REAL DATA FOLDER, AND THAT IS NOT A SWITCH TO ADD.** The Overlay's log path is
  `SHGetKnownFolderPath`, never the environment (§S21), and the tests inject the shipped DLL, so 2492 hook-harness
  logs had piled up in the owner's `%LOCALAPPDATA%\FrameLedger\logs` by 2026-09-15. Each test binary now sweeps the
  harness logs it caused (`17_HOOK_ENGINE` §Native logging) and the `test-artifacts` gate is red for one left behind.
  A new native test executable that starts hook-harness needs the Catch2 listener from `guard_test.cpp`; do not "fix"
  a red `test-artifacts` by pointing the Overlay somewhere else.

- **A CHROMIUM-BASED TITLE IS SEVERAL PROCESSES WITH ONE IMAGE PATH.** *Flower in Us* (NW.js /
  RPG Maker) returned `TargetAmbiguous`, exit 6, on 2026-09-03: three processes match the
  consented path. The presenting one is the GPU process, which owns no window — so do not "fix"
  it by picking the windowed one. **Fixed the same day by Chromium's own `--type=gpu-process`
  flag** — and that fix did not fire, because **NW.js has no GPU process**: the GPU runs
  in-process in the browser, the one process Chromium leaves untyped. Second rule: one
  untyped candidate among typed siblings is the browser and the target. **Read the tree before
  guessing the rule** — the refusal line prints it now. ~~Whether the Overlay can load inside
  that browser process is the next thing the title measures.~~ *(Measured 2026-09-04, `spike-notes` §9 run 3: it
  loads and captures — marked 2026-10-04.)*
- **A TITLE ASKS STREAMLINE FOR A FRAME TOKEN SEVERAL TIMES PER FRAME AND GETS A DIFFERENT
  OBJECT EACH TIME.** Cyberpunk 2077: 3 to 4.6 requests per application frame, distinct pointers,
  same index. A count keyed on the pointer reads the request rate and the `off` leg comes out
  at 0.22 (§S31 row P4). Key on the frame index, **and on a NEW index only** — frames in flight
  interleave their requests across threads, so "differs from the last" still over-counts. The stub
  and harness now reproduce both shapes, so the K = 1 control catches a pointer-keyed writer and
  a last-index-keyed one.
- **THE CENSUS IS A STARTUP PROPERTY ON UE5.** Hell Is Us published the identical census word
  at off, FSR and DLSS + FG ×4; Lies of P likewise across its three runs. The plugins load at
  init whatever the settings menu says, so the census can never separate "loaded and off" from
  "loaded and driven through an unhooked path". It was designed not to try; on 2026-09-03 that
  stopped being a design note and became a measurement.
- **STREAMLINE LOADED + ZERO `slEvaluateFeature` CALLS DOES NOT MEAN "UPSCALING OFF".** Black
  Myth: Wukong has `sl.interposer.dll` and `sl.dlss_g.dll` loaded, DLSS-G demonstrably running,
  and never calls the one export this writer hooks. **Hell Is Us made it three of five on
  2026-09-03**: DLSS on, frame generation ×4, 17,920 records carrying the identity bit, zero
  batches drained. So a title with upscaling switched off in
  its settings and a title driving DLSS through an unhooked path produce byte-identical
  records — every one `UNKNOWN` — and the consumer may not turn that into `none` for either.
  The 2026-09-03 census can say which vendor runtimes are *present*; it cannot say what the
  title did with them, and it is written so it never claims to.
- **A vendor keeps the SYMBOL NAME and changes the SIGNATURE, and no gate we have can see
  it.** The Witcher 3 ships `sl.interposer.dll` **1.5.6**: it exports `slInit`,
  `slEvaluateFeature`, `slSetTag` and `slShutdown`, so every name check passes, and it exports
  **none** of `slSetD3DDevice` / `slIsFeatureLoaded` / `slGetNewFrameToken`. `slInit`'s
  `sl::Preferences` has a different layout, so calling it with the vendored 2.x struct
  **access-violates**. `docs/vendor-exports.json` records **one copy per module NAME**, so
  `hookinventory-check` Pass A resolves against one machine's 2.7.4 and says nothing about a
  1.5.6 in a game directory. The guard is `SpeaksStreamline2` in `fl_hook_inventory.h`;
  **do not weaken it to reach an older title.**
- **A vendor enum's zero is not your zero, and this has now happened twice.**
  `D3D12_RAYTRACING_TIER_NOT_SUPPORTED` is 0 against `rtTier`'s "not queried";
  `sl::DLSSMode::eOff` is 0 against `upscalerQuality`'s "not measured". Copying the vendor
  value verbatim publishes an affirmative negative in both cases — and for `upscalerQuality`,
  0 decodes as "DLSS Performance". **Assume the next one has the same shape**; resolve it at
  the writer.
- **`main` has `strict: true` branch protection, so merging ANY pull request makes every
  other one `BEHIND`.** A batch of ready PRs cannot be merged in a loop: each needs
  `gh pr update-branch` after the previous one lands, which re-runs CI (~6–11 min each). And
  every PR that touches `CHANGELOG.md` under `[Unreleased]` — which is every PR touching
  `src/` — conflicts with the one before it. Budget the batch accordingly, or merge one at a
  time and expect to resolve the same conflict repeatedly.
- **`gh pr update-branch` can COMMIT literal conflict markers, and GitHub then reports the PR
  as `BLOCKED` rather than `DIRTY`.** Measured 2026-08-15 on #73: the update-branch merge left
  `<<<<<<<` / `=======` / `>>>>>>>` inside two source files and pushed them, so CI failed with
  `error C2059: syntax error: '<<'` and a redefinition. **`DIRTY` is the status that means
  "conflict", and this was not it** — the PR read as an ordinary red build, so a "re-run CI"
  reflex would never have reached the cause. After any `update-branch`, grep the branch for
  markers *before* reading the checks; the whole tree is one command and it is cheaper than
  one CI round. **Anchor the pattern to the line start** — this very bullet contains all three
  markers inline, so an unanchored sweep reports this file and teaches you to ignore it.
- **Never run a git-manipulating script in the background against the working tree you are
  working in.** A merge helper doing `git checkout` between branches moved the tree under an
  in-flight edit, and a documentation commit meant for one PR landed on another branch and was
  pushed there. Recovery cost a cherry-pick, a `--force-with-lease`, and a re-verification of
  the *wrong* PR to prove none of its own work had been lost. This is wrong by design rather
  than by bad luck — the tree has one HEAD, and a background job racing you for it will
  sometimes win. Use a separate worktree, or keep it in the foreground.
- **CI's Integration cases fail on the runner about one run in three, and it is never the code.**
  Measured 2026-09-10 while merging P2's stack: **three failures in eight gate runs**, every one an
  `Category=Integration` case, every one green on a re-run of the identical commit. Two shapes were guard
  scans failing — `GuardedInjectAsync(...).IsAllowed` false (§S19(b)'s fragment/signer shape) and
  `ProcessTreeUnavailable — could not establish the scan set`, which §S19(b) does **not** cover — and one
  was a fixture bug this repo owned (a pooled SQLite handle on `ledger.db` outliving `DisposeAsync`, so a
  recursive `Directory.Delete` threw and xUnit blamed an unrelated test). **Re-run first. If it
  reproduces, read the module list and the scan-set reason rather than the test.**
  Two traps inside this one: `ci.yml`'s signer probe — the step that would name the refusing module —
  runs *after* the gate, so the one run that needed it printed nothing; and `gh run rerun --failed`
  rewrites the original run's conclusion, so the run history shows those three failures as **successes**
  and the flake rate reads zero (`13_CI_CD`).
- **MA0004 and xUnit1030 are all-or-nothing PER METHOD, in opposite directions.** A test method may
  not `ConfigureAwait(false)` (xUnit1030) and, once ANY await in it carries a `ConfigureAwait`, MA0004
  demands one on every other await too — so a helper's `.ConfigureAwait(false)` copied into a test
  method turns every neighbouring `await` red. Rule: helpers `false`, test methods either none at all
  or `true` on every await; measured 2026-09-10 across three rebuilds of one test file.
- **A Bash heredoc rewrites `\n` inside a Python string even when the heredoc is quoted.** The
  replacement text arrived with real newlines, the assert that it matched the file failed, and the
  diagnosis pointed at the file. Write patch scripts with the editor tool and run them by path.
- **Anything that writes a file with a script writes LF, and `.gitattributes` hides it until
  it does not.** `dotnet format --verify-no-changes` is the documented victim, but a Python
  or PowerShell `write` is the usual cause. `git commit` prints *"LF will be replaced by
  CRLF"* and then normalises for you — so the COMMIT is fine and the WORKING TREE is not,
  which is the confusing half. Normalise explicitly after any scripted edit.
- **Never run `cmake`/`ctest` directly — always `./build.ps1`.** Without the MSVC
  environment it imports, configure fails, the build never runs, and ctest executes the
  **stale binary** and prints `Passed`. Cost two wrong diagnoses in one session.
- **Read the build's exit code before the test's**, every time. A canary that fails to
  compile reports "All tests passed" from the previous binary.
- **A canary that dies before reaching the gate looks exactly like a canary that
  worked.** Check *which* step failed.
- **In PowerShell, a backtick inside a double-quoted string is an escape.** A search
  needle containing `` `blockedExecutables` `` silently became something else, `.Replace`
  matched nothing, the file was unchanged — and the validator correctly passed. Print
  the mutated text before validating.
- **`./build.ps1 check` with no switches** after every PR. ~~CI runs `-SkipIntegration`,~~ CI runs the same since 2026-09-06,
  so a green CI is evidence for the managed drain too (§S19(b) BUILT).
- **A canary that does not compile is not a canary, and this repo's analyzers make that
  easy to hit.** Removing a call to prove a gate goes red can leave a constructor
  parameter unread — `CS9113` under `TreatWarningsAsErrors` — so the build fails, the
  test run prints "All tests passed" from the **previous binary**, and the canary looks
  like it worked. Measured this way on 2026-08-06 while proving the consent gate.
  Add `_ = param;` to keep it compiling, and read the build's exit code first.
- **A document can go stale by NOT being touched.** This file's queue item 1 described
  work that had just landed, in a PR that changed 69 other files — the failure was the
  edit that was never made. `CHANGELOG.md` and §S24 are gated; this file is not, so it
  is the one that needs a deliberate pass at the end of every item.
- **ONE GAME LAUNCH PER CAPTURE. A capture that ENDS kills the Overlay in that process 65 s
  later, permanently.** When the capture host exits nothing publishes `guardTicks` any more, the
  Overlay's watchdog hits `FL_GUARD_TICK_DEADLINE_MS` and calls `StopObserving` — which clears
  `g_observing` one-way and disables every hook for the life of the process. A second capture
  against the same running game returns `SupervisionLost` with zero records.

  **This is designed behaviour, not a defect** (`19_SAFETY` §During a session), and it is
  recorded here because it changes how a measurement sweep has to be planned: §S31's off / ×2 /
  ×4 comparison needs **three game launches**, not three captures in one. Measured 2026-08-20;
  the second capture of the session hit it within minutes of the first.

- **A LAUNCHER CAN UPDATE THE GAME BETWEEN THE CONSENT GRANT AND THE CAPTURE, and the refusal
  then says something false.** Alan Wake 2, 2026-08-20: consent granted at 09:12Z, the executable
  went from 62,026,752 to 62,304,768 bytes when the title was launched, and the capture refused
  with `ConsentMissing` — signal *"the per-game consent dialog has not been accepted"*, which the
  operator had done forty minutes earlier. `HookRequest.FromConsent` is right to null
  `consentedAt` and right to leave the stored record untouched; the *message* collapses three
  situations that need three different actions. `Program.WhyConsentMissingAsync` now prints
  which one it was. **Re-verify the fingerprint after launching a title, before blaming the
  gate.**

- **`D3D12CreateDevice(WARP)` can fail on a dev box while the real GPU's D3D12 works, and it takes
  two ctests down with it — `fl_d3d12_acquisition` and `fl_guard`'s D3D12 case.** The harness prints
  only `[FAIL] D3D12CreateDevice(WARP)`, which reads like a code regression.

  **Measured on this machine 2026-08-06, and the second measurement contradicted the first
  write-up.** `EnumWarpAdapter` succeeds and returns *Microsoft Basic Render Driver*;
  `D3D12CreateDevice` on it returns **`DXGI_ERROR_DRIVER_INTERNAL_ERROR` (0x887A0020)** at **every**
  valid feature level (12_2 through 11_0; 10_1 and below return `E_INVALIDARG`, which is correct
  because D3D12 has an 11_0 floor). On the **same adapter object**, `D3D11CreateDevice` succeeds at
  FL 11_0, and `D3D12CreateDevice(nullptr, …)` on the RTX 5080 returns `S_OK`.

  > **"A reboot clears it" was written here and is FALSE.** I asserted a remedy I had not tested.
  > The machine rebooted at 13:58 and the identical failure reproduced at 14:17, 19 minutes into the
  > new boot. `d3d10warp.dll`, `d3d12.dll` and `D3D12Core.dll` are all `10.0.29639.1000` and match the
  > OS — **Windows 11 Insider Preview build 26300/29639** — so nothing is mismatched or corrupt, and
  > the system log has no display errors since boot. It reads as an Insider-build regression in
  > WARP's **D3D12** path, and it is persistent.

  > **AND IT STOPPED REPRODUCING, measured 2026-08-20 — which does not retire the trap, it dates
  > it.** `hook-harness --probe-d3d12` passes and `./build.ps1 check` is fully green here, 20/20
  > native tests. **No change in this repository caused that**: the machine moved from Insider
  > build **26300/29639** to **29648**, and `d3d10warp.dll` / `D3D12Core.dll` are both
  > `10.0.29648.1000`. So the fix is upstream and the window was 2026-08-06 → some point before
  > 2026-08-20.
  >
  > Both dates are recorded because the useful fact is not "it works now" — it is that **this
  > dependency broke and healed under the machine without anyone touching the code**, twice
  > giving a native suite a colour that said nothing about `main`. Do not delete this entry when
  > it is green; check which state your build is in.

  **What that does and does not mean for the project.** CI (`windows-latest`, not an Insider build,
  no GPU) runs the same suite on WARP and passes, so `main` was never broken and no code change was
  indicated. What it does mean is that **the native suite has a hard dependency on WARP D3D12 that a
  dev box can lose on its own**, and that a red `fl_d3d12_acquisition` is not evidence about the
  code until the two adapters have been probed separately. That probe is ~40 lines of P/Invoke and
  settles it in one run; do that before reading the harness's output as a finding.
- **A NATIVE test can go red because the RUNNER did not schedule two threads, and it looks
  like a code regression.** `ring_test.cpp`'s *"a tiny ring, so the writer laps the reader
  constantly"* ends with `CHECK(dropped > 0)` — an **anti-vacuity** assertion, not a property
  of the ring. Its own comment says why: *"with 8 slots the writer MUST have lapped us, so a
  run reporting no drops means the threads never actually overlapped and the case proved
  nothing."*

  It fired on CI on 2026-08-28, on a pull request that **touched no native source and no
  test** — `ctest` reported `1 tests failed out of 23`, `build.ps1` threw `ctest failed (exit
  8)`, and the run before and the run after were both green. Eleven of the twelve CI runs
  around it passed, two of them on `main` within minutes.

  **The tell is which assertion fired.** A ring defect fails `CHECK_FALSE(mixed)` or
  `CHECK_FALSE(poisonAccepted)` — the properties. A hosted runner that did not overlap the
  threads fails only the guard at the end. Read the assertion before reading the diff: this
  is the integration-budget trap (*"no budget may be sized on the harness's measured rate"*)
  one layer down, in the **native** suite, where §Traps did not have it.

  Do **not** relax it to `>= 0`. The guard is what stops the case passing while proving
  nothing, and `ring_test.cpp`'s own header already records that two of its four properties
  have never been shown red.

  > **Amended 2026-10-13 (beta.23 PR-1, #308): both guards now measure overlap.** The hammering case's own guard,
  > `accepted > kTotal / 2`, failed 5 of 40 runs and then 5 of 200 on the owner's hybrid i7-14700KF — `mixed` clean every
  > time — and once made a full local gate red. A share of the stream measured the scheduler: a reader on an efficiency
  > core keeps less than half, and one the OS does not run until the writer is done keeps one lap (accepted 1024). The
  > tiny ring's `dropped > 0` was weaker the other way: a reader made to wait for the writer to finish drops nearly
  > everything (1.6 million over eight attempts) and passed it. Both cases now start the writer only once the reader
  > is about to loop, count the drains that copied records while the writer was still writing, and repeat — every
  > attempt checked for its properties — until one attempt made 100 such drains, at most eight times; the tiny ring
  > also holds that attempt's own `dropped > 0`. A start barrier alone left 1 of 200 (one overlapping drain). Measured:
  > 1,000 runs, no failure, at most 3 attempts; with the waiting reader, both guards fail in every run.

- **A self-test's cases can all pass while testing nothing, and their NAMES will not tell
  you.** Found 2026-08-27 in a PowerShell gate's `-SelfTest`: removing the fail-closed guard
  from a hash comparison left **all ten cases green**. `[string]::Equals` already refuses
  empty-vs-present, so three cases named *"an empty hash fails closed"*, *"a null hash fails
  closed"* and *"an empty EXPECTATION fails closed too"* were testing `Equals`, not us.

  **The case the guard actually carried was the one nobody wrote:** `Equals('','')` is
  **`true`**. That is the §S23-1 shape — two empty build ids compared equal and
  `ShmHandshakeValidator` reported `Ok` for every process on the machine — arriving in a
  different language.

  *"A gate never observed failing is a comment"* applies to a gate's **own tests**. Plant the
  canary in the thing under test, not in the caller, and check that it fails on exactly one
  case: if it turns several red, the cases are coupled and none of them is pinning what its
  name claims.
- **A failing `REQUIRE` used to END THE WHOLE BINARY, so one red test hid every test
  after it.** Fixed 2026-08-09, and recorded because the symptom was invisible: `ctest`
  reported *"1 test failed"* when the truth was *"and 2 never ran"*.

  Catch2 compiled itself with `CATCH_CONFIG_DISABLE_EXCEPTIONS` — the top-level
  `src/native/CMakeLists.txt` strips `/EHsc` from `CMAKE_CXX_FLAGS` deliberately, and
  `tests/CMakeLists.txt` added it back **only to the test binaries**, never to the
  Catch2 library that decides the mode. Measured off the generated command line:
  `... -std:c++20 -MT -Zi` with no `/EH` flag at all. In that mode a failed `REQUIRE`
  calls `std::terminate`.

  On this box, where WARP's D3D12 path is broken, that meant `fl_guard`'s D3D12 case
  killed the run and took the end-to-end injection cases with it — **including the
  honesty assertion §S29(a) names as the merge gate's coverage**, which passes fine
  when run on its own. `target_compile_options(Catch2 PRIVATE /EHsc)` recovered
  **70 test cases and 1051 assertions**. If you see a suspiciously small assertion
  count next to a failure, this is the shape to look for.
- **No budget in the integration tests may be sized on the harness's measured rate.** Five were, and
  every one went red under load while nothing was wrong: the suite runs four test assemblies in
  parallel, each spawning a harness and injecting an Overlay that creates a WARP device, so the
  presenting thread is descheduled far longer than a rate-derived constant allows. Wait for the
  **state**, bounded by a wall clock generous enough to be about the state rather than about the
  machine.
- **When one of those fails, capture the MESSAGE before changing anything.** #62 spent two rounds
  applying the remedy for a race to what turned out to be a loop bound and its assertion disagreeing
  by one — the loop exited at exactly 10 and the assertion demanded more than 10, so timing decided
  whether the off-by-one was visible. **A defect class is a hypothesis about the next failure, never
  a diagnosis of it.** One run with the message printed settles what four runs of pattern-matching
  cannot.
- **A test that reads a writer state ONCE is racing `InitThread`.** `layoutVersion` is published at
  step 2 and `status` becomes `READY` at step 6, with a WARP device creation in between; `apiMask` is
  set later still, on the first present the hook sees. So `TryAttach` succeeding, `guardTicks`
  advancing, `status == INIT` and `apiMask == 0` are all legitimate simultaneous states. Poll for the
  state you mean, and keep the timeout failing — `INIT` past the budget is
  `WriterNeverInstalledHooks`, not slowness.
- **Files written with LF fail `dotnet format --verify-no-changes`** even though the diff
  looks identical: `.editorconfig` mandates CRLF and `.gitattributes` normalises on
  checkout, so the working tree disagrees with both. Run `dotnet format` (no switch)
  before `check`, and normalise `.csproj`/`.ps1` by hand — the formatter does not touch
  those.
- **GPG can stall.** `git commit` fails with `signing failed: Timeout`, and `git push`
  then reports success while pushing only the old HEAD. Never pass `--no-gpg-sign`; just
  retry the commit and read *its* exit code.
- **The squash trap.** After a squash-merge the branch you were on is not an ancestor of
  `main`; a PR from it reports CONFLICTING and runs **no CI at all**. Read "no checks
  reported" as a symptom of that. Always `git fetch origin` then branch from
  `origin/main`; capture old base SHAs *before* merging a stacked pair.
- **Do not pass `--delete-branch`**: `main` is checked out in the primary worktree,
  which blocks it. GitHub deletes the remote branch anyway.
- **A control that compiles, binds and passes its view-model tests can still draw nothing.** WPF UI 4.3.0's
  `InfoBar` has no `ContentPresenter`, so every action button this repo placed as an InfoBar's child — the one
  dismissal of a safety notice — was silently dropped, and the owner met a red bar with no exit (2026-09-14).
  `SafetyNoticesTests` was green throughout: it asserted the model, and nothing instantiated the template.
  When a control's affordance matters, render it (`PagesLoadTests` has the STA harness) and walk the visual
  tree for the element — `16_WPFUI_SYNTAX` §Gotchas carries the fix and the test that retires it.
- **A test that finds a file by pid finds the wrong one on a machine that has run the tests before.** The native
  log test looked up `overlay-<pid>-*.log` in the real `%LOCALAPPDATA%\FrameLedger\logs`, which keeps every run's
  log (2130 on the dev box by 2026-09-14). A reused pid read a stale log and failed on the STOP line; the re-run
  drew a new pid and passed, so it was called a flake four times while the failure output printed the September 6
  path. **Read the path in a failure before calling it a flake**, and select by creation time, not by name.

---

