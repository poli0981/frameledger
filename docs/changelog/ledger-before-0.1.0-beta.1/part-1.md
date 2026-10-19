<!-- Moved verbatim from CHANGELOG.md on 2026-10-19 (beta.24, owner decision D66): part 1 of 5 of the ledger kept before the first tag, newest first. CHANGELOG.md lists every part. -->

## Ledger before 0.1.0-beta.1 (2026-08-04 → 2026-09-16)

_The `[Unreleased]` entries as they stood when the first tag was cut; `release-notes.ps1` stops before this
heading. Newest first._

### Added

- **P5 prep: a release rehearsal, `SECURITY.md`, and an honest hygiene checklist (2026-09-16).** `release.yml` gains
  `workflow_dispatch` with a `version` input: the identical job — the gate, publish, runtime licences, the
  published-tree assertion, `vpk pack`, checksums, the artifact — with no tag and no Release, so the steps that had
  only ever run by hand on one machine run on the hosted runner before the first tag; a missing `## [x.y.z]` section
  is a warning with placeholder notes in a rehearsal and stays a stop on a tag. `SECURITY.md`: the private route for
  a vulnerability (GitHub's private reporting when enabled, else e-mail), the public *Safety gap* form for an
  undetected anti-cheat, what not to include, and the response *intent* stated as an intent; `ISSUE_TEMPLATE/config.yml`
  points a vulnerability at the private route; README's safety-gap paragraph links both. `13_CI_CD` §Repo hygiene
  checklist rewritten: the in-tree items were done and unticked, the first box asked for caller stubs the file's own
  opening paragraph rejects, and the rest are GitHub settings only the owner can set — listed as such. `14_TESTING`'s
  Velopack-delta row says it is reachable from the second release.
- **The "factor not counted" tooltip says which bucket, and by how much — `sessions.fg_refusal_detail`, schema
  0005 (2026-09-16).** The owner's Dashboard showed "DLSS-G active — factor not counted" on Hell Is Us, Black Myth:
  Wukong, Cronos and Onimusha; three refused `non_uniform` (one of eight equal-sample buckets departed more than
  25 % from the whole, or held no tokens — a menu, a loading screen, an alt-tab inside a whole play session) and
  one `multiple_streams` (a second swapchain), and the tooltip could only repeat the kind. `FgWindow` had computed
  the bucket, its ratio and the window's all along; `SessionAggregator` kept only the kind. Now
  `Application.Recording.FgRefusalDetail` — kind, subject, count, bucket index/count, the bucket's ratio (null when it
  held no tokens) and the window's — is stored as JSON beside `fg_refusal`, and `FpsPresentation.IdentifiedTooltip`
  appends its sentence: "Bucket 3 of 8 measured 2.25 against 3.65 for the whole session, over 53 samples." /
  "2 swapchains presented during the session." / "40 samples; at least 64 are needed to check." A kind with no
  numbers worth a sentence adds nothing; an unparsable column is the old tooltip. The producer is unchanged and
  no number is decided by it (owner's decision 2026-09-16: store and show the reason; no steady-state factor, no
  change to `multiple_streams`). Six strings (en/vi/ja). Tests: `SessionAggregatorTests`, `LedgerDatabaseTests`
  (script five), `FpsPresentationTests`. Docs: `06_DATA_MODEL` §Migrations, `03_METRICS` §FG factor, `08_UI` §FPS
  display rule.
- **RPG Maker is detected — MV/MZ and XP/VX/VX Ace — and `rulesVersion` is `2026.09.1` (2026-09-16).** Two engine
  rows `05_DETECTION` marked ⛔ "cannot be expressed in schemaVersion 2": the MV/MZ reasoning wanted a nested group
  for `nw.dll` **and** (`www/` **or** `package.json`), but MV and MZ both ship `package.json` beside `nw.dll`
  unconditionally, so `all: [nw.dll, package.json]` is the signal — measured on the owner's *Flower in Us* and a
  hand-extracted MV title, both `engine=none` until now. The RGSS family is one rule over the three DLL prefixes and
  the three archive extensions, no version (which DLL matched is not a value any extractor produces). Both sit
  after `unity`/`unreal` and before `godot`. Fixtures `engines/rpgmaker_mv`, `engines/rpgmaker_rgss`; the
  `every_engine_marker` canary carries both engines' markers and still expects `unity`. The version bump changes the
  sweep's cache key, so every game is re-detected on the Agent's next pass. Docs: `05_DETECTION` §Engine signatures.
- **The bug report's last optional item: the last session's summary, when ticked (2026-09-15).** `10_LOGGING` §Bug
  report flow step 2 listed "last session metadata + aggregates JSON (never raw blobs by default)" beside the crash
  dump, and `legal/PRIVACY_POLICY.md` §3 promises both only when ticked. The Optional items dialog PR-9 added now shows
  a box for each item that exists: the crash dump, and the newest session in the ledger (`LastSessionSummary`) labelled
  with its game and start time. Ticked, the zip gains `session.json`, File ▸ Export's own JSON (metadata, aggregates,
  segments, hardware) without the user's notes and tags, passed through `LogRedactor` because it names the executable;
  no frame or sensor series. With neither item there is no question. `CrashDumpChoice` became `BugBundleOptions`, and
  `IBugReportPreview.AskCrashDumpAsync` became `AskOptionsAsync(BugBundleOffer)`. Two strings (en/vi/ja). Tests:
  `LastSessionSummaryTests`, `BugReportFlowTests`, `BugBundleOptionsViewModelTests`, `PagesLoadTests`. Docs:
  `10_LOGGING` §Bug report flow step 2.
- **P4 PR-9 — the crash dialog, the minidump in both processes, and the crash dump as the bug report's opt-in
  (2026-09-15).** `10_LOGGING` §Crash handling specified Fatal → `MiniDumpWriteDump` → a dialog offering the bug report
  → exit code 1; only the Fatal line existed, and `Crash_Title` / `Crash_Body` were translated for a dialog nothing
  showed. `Infrastructure/Diagnostics/CrashDumpWriter` writes `crashdumps\<ui|agent>-<UTC time>-<pid>.dmp` through
  CsWin32 with `MiniDumpWithIndirectlyReferencedMemory | MiniDumpWithThreadInfo`, keeps the five newest and never
  throws. **The App** marks a dispatcher exception handled; `CrashReporter` logs it, writes the dump and shows
  `CrashDialog` — a Win32 message box, because a crash cannot vouch for the shell's dialog host — whose Yes reveals the
  shell window (one hidden in the tray would hold the report's dialogs unseen) and runs `BugReportFlow`; the host then stops and `RunAsync` exits with code 1 after the normal teardown. One dialog per
  process: a further exception while it is up is logged once and counted, since a layout fault repeats on every pass
  the dialog's message loop runs. An exception on any other thread is Fatal + dump only. **The Agent** logs and dumps
  from `AppDomain.UnhandledException` and from `Main`'s own catch (whose `finally` would otherwise close the logger
  first), once, and leaves the exception unhandled. **The bug report** offers the newest dump of the last seven days
  before the save dialog, as a checkbox that starts clear, labelled with its time and size, with a line saying a dump
  can hold file paths, game names and settings (`Dialogs/BugBundleOptionsContent`) — what `legal/PRIVACY_POLICY.md` §3
  already promised; Cancel writes nothing, and `BugBundleBuilder.WriteAsync` refuses a dump from anywhere but
  `crashdumps\`. `Crash_Body` becomes `Crash_Body_Format` and keeps its one fact (the capture agent keeps running);
  seven more strings (en/vi/ja). Tests: `CrashDumpWriterTests` (a real dump of the test process starts `MDMP`; the
  prune keeps five and touches nothing else; an unusable directory is null and a line), `CrashReporterTests` (the order,
  no report on No, three further exceptions show nothing, a failed dump still asks, a failed report is not a second
  crash), `BugBundleBuilderTests`, `BugReportFlowTests` (no dump no question; left clear, ticked, cancelled),
  `BugBundleOptionsViewModelTests`, `PagesLoadTests`. Docs: `10_LOGGING` §Crash handling and §Bug report flow,
  `15_ROADMAP`, HANDOFF §P4.
- **P4 PR-8 — the accessibility pass, with the section it passes against written first (NFR-9, 2026-09-15).**
  NFR-9 was one sentence and `08_UI` one bullet ("Ctrl+1…5; grids fully navigable; Esc closes flyouts/dialogs"), none
  of it built. `08_UI` §Accessibility now says what each clause means here and names the check: **Ctrl+1 … Ctrl+5**
  and the keypad open Dashboard / Games / Compare / Logs / Settings (`MainWindow` `InputBindings` →
  `MainWindowViewModel.NavigateToCommand` → `ShellShortcuts`), and `AccessibilityTests` holds the bindings, the map and
  the NavigationView's order together; **Esc closes the open `ContentDialog` or `MessageBox` with no result**, through
  its own Close path, and never a dialog that offers no Close button (`Services/DialogKeyboard`, one class handler on
  `Window` registered in `App.OnStartup`, bubbling so a dropdown's Esc wins) — WPF UI 4.3.0 handles no key in either
  dialog, measured in its source; **every input that shows no text of its own has an `AutomationProperties.Name`** —
  thirty controls had none (every Settings toggle, number box and choice box, the hooking switch, the trend metric,
  the Games and Logs filters and search boxes, the log tail, the edit-game and consent text boxes, the Legal Gate's
  document, the summary window's tags and notes, and the icon-only close button in the `InfoBar` template), a scan of
  the App's XAML goes red on the next one (proven by removing one name), and four new strings name the controls that
  had no label to borrow (en/vi/ja); **no XAML hard-codes a colour** (a second scan), so contrast is the Fluent
  themes'; the manifest is Per-Monitor V2 (a third). Known gaps are stated there: ScottPlot plots are not
  keyboard-operable, and no Narrator pass has been done. Docs: `08_UI` §Accessibility and §UX rules, `02_SPEC` NFR-9,
  `16_WPFUI_SYNTAX` §Gotchas (no Esc in 4.3.0, `CloseButtonText` defaults to "Close", the obsolete `MessageBox.Close`),
  `15_ROADMAP`, HANDOFF §P4. The Database maintenance dialog's status line is `LiveSetting="Polite"`, so a result is read out.
- **P4 PR-7 — Tools ▸ Database maintenance: integrity check, backup, the retention sweep on demand, compaction
  (2026-09-15).** `06_DATA_MODEL` §Retention promised the four since P0; the menu item said "not built yet". **The
  sweep is the Agent's**, because `frame_blobs` and `sensor_blobs` are (§Writer ownership): the App sends the new
  `SweepRetention` command (no payload; `07_IPC` row), and the Agent's `RetentionSweep` reads its own
  `retention.raw_sessions_per_game` and runs `ISessionRepository.SweepRetentionAllAsync` — the finalize's per-game
  rule for every game in one transaction (`ROW_NUMBER() OVER (PARTITION BY game_id …)`, a removed game's kept sessions
  included), answering `SweepRetentionAck { keep, games, sessions }`; unlimited (0) never reaches the repository, which
  refuses a keep below 1. The other three run in the App through `LedgerMaintenance` and change no row:
  `PRAGMA integrity_check`; a backup through `VACUUM INTO`, which is consistent while the Agent writes and **refuses
  the ledger, its WAL and its shared-memory file as a destination** — the save dialog's "replace?" would otherwise have
  let "delete the existing file first" delete the live database; and compaction (`VACUUM` + `wal_checkpoint(TRUNCATE)`)
  refused while a fresh `GetStatus` reports a session, with SQLite's busy error a status line rather than a crash.
  `LedgerDatabase.MaintainAsync` runs a statement outside a transaction, for these only. The dialog
  (`DatabaseMaintenanceContent` / `DatabaseMaintenanceViewModel`, `DatabaseMaintenancePrompts`) confirms the sweep with a
  Danger button and shows one status line; the menu's last `NotYetCommand` and the `NotYet_*` strings are gone.
  Strings en/vi/ja. Tests: `LedgerMaintenanceTests` (healthy, a backup that opens at the same schema with the same rows,
  never over a file or onto the ledger in any spelling, compaction that returns the space of ten swept series),
  `SqliteSessionRepositoryTests` (every game keeps its own newest N, a sensors-only session counted once, a second
  sweep finds nothing, a keep of 0 refused), `RetentionSweepTests`, `AgentCommandHandlerTests` (the composed sweep
  answers; none composed is not a command), `DatabaseMaintenanceViewModelTests` (twelve cases, the ledger-as-backup
  refusal among them), `PagesLoadTests` (the dialog loads under the real dictionaries). Docs: `07_IPC`,
  `06_DATA_MODEL` §Retention and §Writer ownership, `08_UI`, `15_ROADMAP`, HANDOFF §P4.
- **P4 PR-6 — every NuGet package the App and the Agent ship has its licence text, and the gate keeps it true
  (2026-09-15).** `tools/license-gather.ps1` reads the two publish roots' restore output (`obj/project.assets.json`,
  win-x64, every package with a runtime or native asset that is not a suppressed build-time dependency) and writes
  one text per package into `legal/licenses/nuget/`: the package's own licence file verbatim when it has one; for a
  URL-only package a reviewed override (`tools/license-overrides/`: Mono.Posix.NETStandard → the Mono LICENSE file
  its link resolves to, OpenTK.GLWpfControl → its LICENSE.md, both fetched 2026-09-15 with provenance); for an SPDX
  expression with no file, MIT's text under the package's own `<copyright>` field (never an invented holder —
  "not stated" when the package states none) or a pointer to the shared Apache-2.0 / MPL-2.0 text with the MPL
  Source Code Form link. Distinct third-party notice files are copied once (twenty-seven Microsoft.Extensions
  packages carry the same 78 KB file). An MPL-2.0 package that *applies* Exhibit B is red — the MPL text itself,
  which carries the sentence as a template, is excluded, as `spike-notes` §M4 learned. **77 packages ship; the
  notices table named about fifteen** — LibreHardwareMonitorLib's three MPL-2.0 dependencies (BlackSharp.Core,
  DiskInfoToolkit, RAMSPDToolkit-NDD), HidSharp, Mono.Posix, the OpenTK/GLFW and SkiaSharp/HarfBuzz families under
  ScottPlot and the Microsoft.Extensions stack were in every build without a row. The three MPL packages were
  searched upstream the §M4 way (the sentence once per repository, in LICENSE; no source file carries an MPL
  notice) and are recorded clear in `legal/THIRD_PARTY_NOTICES.md`. `license-check` §3 runs `-Check` (regenerate,
  compare both directions — a bump, a removal or a hand edit is red) and asserts the shared texts the generated ones
  point at exist; `build.ps1` runs the gatherer's fourteen-case self-test first. **The output is culture-invariant, and that
  was measured rather than assumed:** under tr-TR `-Check` went red on an unchanged tree, because PowerShell's `-match`
  ignores case in the current culture and "LICENSE" lower-cases to "lıcense" there; file names now match with
  `CultureInvariant`, ordering is ordinal, and a self-test case runs the gatherer under tr-TR in a child process (in-process
  it proved nothing — the Regex cache kept the first culture — and a culture-sensitive mutant passed until the case
  moved out). The App's publish now carries
  `legal/licenses/` as `licenses/` (plus `THIRD_PARTY_NOTICES.md` and the GPL text), and `release.yml` adds the .NET
  runtime's licence and notices from the runtime packs the publish used (their version follows the SDK, so they
  are not committed) and asserts all of it in the published tree — verified on a local publish (runtime 10.0.12,
  84 generated files). The hand copies `wpfui-MIT.txt` and `velopack-MIT.txt` are folded into the generated
  `nuget/WPF-UI.txt` and `nuget/Velopack.txt`. The generated directory is `nuget/`, not `packages/`: the
  repository's `.gitignore` ignores every `packages/` (a NuGet-era rule), which would have left CI comparing
  against nothing. Docs: `THIRD_PARTY_NOTICES` (intro, four transitive rows, two checklist items ticked),
  `legal/licenses/README.md` (rewritten: what is generated, what is hand-committed, the stale M3/M4 paragraph),
  `12_BUILD` gate 13, `13_CI_CD` licence guard, `16_WPFUI_SYNTAX` checklist, HANDOFF §P4.
- **P4 PR-5 — Velopack, the updater, `release.yml`, and one version source (`11_UPDATER`, 2026-09-14).**
  `FrameLedger.App/Update/`: `VelopackUpdateClient` behind the `IUpdateClient` port (Velopack's `UpdateManager` over
  its GitHub source for this repository; the `stable` channel is the releases GitHub does not mark pre-release, `beta`
  adds the ones it does), `UpdateFailureMapper` (the §Error mapping rows from the exception: 404 / 403+429 / 5xx /
  transport / checksum / unknown; an HttpClient timeout is Offline, not a cancel), `FrameLedgerFileDownloader` (Velopack's
  downloader with `User-Agent: FrameLedger/<version>`), `UpdateService` — the startup silent check 5 s after start when the copy is
  installed and the new `update.auto_check` setting (default on) says so, every failure a log line; Help ▸ Check for
  updates with the dialogs (`UpdatePrompts` over WPF-UI's `MessageBox`: the offer with the release notes and the
  unsigned-release footer, up to date, not an installed copy, the six error rows — a 404 offers the releases page, an
  unknown failure offers the bug report); the background download, retried once on a checksum failure; and **FR-12 as the shape of the whole thing**: a
  downloaded package is `Ready` only while the Agent reports no session, otherwise `Deferred` until the last
  `SessionCompleted`, and the apply itself is refused with a session running. The apply (`Restart to update`, the
  shell's new persistent InfoBar) sends `Shutdown` to the Agent, holds the connection's relaunch (`IAgentLink.SetLaunchHold`),
  waits for the pipe to drop (an Agent that does not stop in 10 s leaves everything as it was), hands Velopack the
  package (`WaitExitThenApplyUpdates`) and ends the host; the restarted App starts the Agent beside itself as always,
  and — when Velopack reports the restart — re-validates the logon task's action path (`--install-task` on `Stale`).
  `Program.Main` is now hand-written so `VelopackApp.Run()` precedes any window: the uninstall hook (`UninstallHook`,
  delegates for the test) asks a running Agent to stop, unregisters the Vulkan layer, removes the logon task and
  **asks** about the data folder (deleted on Yes only; unanswered inside Velopack's 30 s budget keeps it); install
  registers nothing. The tray raises "update available" as a balloon only while the window is off screen. Settings ▸
  Updates gained the startup-check toggle and the channel row's real text. **One version source:** `VERSION` at the
  repository root, read by `Directory.Build.props` (`<Version>`, SourceLink's `+sha` suffix off) and by
  `src/native/CMakeLists.txt`, with `versioninfo-check` comparing every built binary's `FileVersion` against it.
  **`release.yml`** on a `v*` tag: the tag's numeric core must equal `VERSION` and `CHANGELOG.md` must carry a
  non-empty `## [x.y.z]` section (`tools/release-notes.ps1`, self-tested in `build.ps1`), `{{RELEASE_DATE}}`
  substituted before the build, the identical gate, publish App + Agent self-contained + ReadyToRun with
  `-p:Version=<tag>`, the published tree asserted by name (both entry points, the four shipped natives, the rules
  seed, no capture host), `vpk pack`, `SHA256SUMS.txt` printed into the release body, `gh release create`
  (`--prerelease` for a `-beta.N` tag). **The package id is `FrameLedger.App`**: Velopack installs into and, on
  uninstall, deletes `%LOCALAPPDATA%\<packId>`, and `%LOCALAPPDATA%\FrameLedger` is the data folder — the id the docs
  named would have removed every user's ledger without asking (`UninstallHook.AreSeparate` now refuses the data delete
  if the two ever overlap). Published locally before the PR: 250 MB in 362 files, every assertion of the workflow's
  tree step green; `vpk pack` 1.2.0 over it produced `FrameLedger.App-win-Setup.exe` (104 MB) and verified `VelopackApp.Run()` in `Program.Main`. The GitHub release itself is unexercised until a tag. `legal/PRIVACY_POLICY.md` is now **2.1-draft**: the update check and download
  rows describe requests the software makes, which is a material change the policy's own rule says re-shows the Legal
  Gate. `legal/licenses/velopack-MIT.txt`. Tests: `UpdateServiceTests` (the skips, the channels, Deferred ↔ Ready,
  the dialogs and rows, the apply's three endings), `UpdateFailureMapperTests`, `UninstallHookTests`, the
  connection's hold, the tray's balloon, the settings toggle. Docs: `11_UPDATER` built note, `12_BUILD` §Version and
  §Publish & package, `13_CI_CD` §release.yml, `08_UI`, `06_DATA_MODEL`, HANDOFF §P4, `15_ROADMAP`, README §Privacy.
- **P4 PR-4 — the library import: Steam, GOG Galaxy, Epic and itch.io through a review checklist, hooking off for
  every row (FR-1.2, 2026-09-14).** `Application.Import.LibraryImporter` over four `IStoreLibrarySource` adapters in
  `Infrastructure.Import`: `SteamLibrarySource` (`libraryfolders.vdf` + `appmanifest_*.acf` through a new
  `ValveKeyValues` reader, the root from `HKCU\Software\Valve\Steam\SteamPath`), `GogLibrarySource` (the
  `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games` keys, read-only, whichever hive and key a caller names so a test uses
  its own), `EpicLibrarySource` (`Manifests\*.item`: `DisplayName`, `InstallLocation`, `LaunchExecutable`,
  `AppName`, `AppVersionString`), `ItchLibrarySource` (`apps\*\.itch\receipt.json.gz`). Steam and itch do not name
  the game's executable: `ExecutableLocator` walks the install three levels deep, drops helpers by name fragment
  (crash handlers, installers, redistributables, anti-cheat services, updaters) and takes the largest — a guess the
  review marks "guessed — check it". Discovery joins every title to the ledger: the store's executable or the
  guess, "already in the library", "no executable found" (a store record that outlived the install). File ▸ Import
  library… (`ImportLibraryFlow`) shows the checklist (`ImportReviewPrompt` / `Dialogs/ImportReviewContent`, a
  `DataGrid` with a tick column, select all / none, the count), adds what was ticked exactly as File ▸ Add game…
  would — **hooking off** — writes the store's platform, id and version through the new
  `IGameRepository.ApplyStoreMetadataAsync` under the same provenance rule as a detection write (the user's
  value is never overwritten), and shows the Games page. Nothing is launched, nothing is fetched. The first-run
  step 4 now points at the menu rather than saying "later build". Tests: `ValveKeyValuesTests`,
  `StoreLibrarySourcesTests` (fixtures the tests write: two Steam library folders, GOG under a test HKCU key,
  Epic `.item`, an itch receipt gzipped in the test, the locator), `LibraryImporterTests` (the join, the ticked
  subset, a store that throws, one executable under two stores), `ImportLibraryFlowTests` (end to end over real
  files and a scratch ledger; nothing found; cancel; the review's counting), `SqliteGameRepositoryTests` (the
  store write's provenance). 15 App strings en/vi/ja. Docs: `05_DETECTION` §Platform signatures built note (with
  the privacy line), `06_DATA_MODEL` §Writer ownership, `08_UI` §Shell menu note + §First-run step 4,
  `14_TESTING`, HANDOFF §P4, `15_ROADMAP` §P4.
- **P4 PR-3 — the bug report's steps 3–4, Help ▸ Documentation, and File ▸ Export as the submenu it was specified
  as (2026-09-14).** `App/Services/BugReportFlow` is the one flow behind Help ▸ Report a bug… and the Logs page's
  button: step 2's zip where the user says, then step 3's `ContentDialog` (`BugReportPreviewPrompt` /
  `Dialogs/BugReportPreviewContent`) listing every entry the builder wrote, with "Show the zip in Explorer" and the
  drag instruction, then step 4 as its two buttons — **Open GitHub issue** (`IssueLink.NewIssue`:
  `issues/new?template=bug_report.yml&title=[Bug]%20&labels=bug&app-version=…&os=…`, the form's real hyphenated
  field ids, the OS in the form's own spelling `Windows 11 26100.2314`) and **Copy summary as Markdown**
  (`sysinfo.json`'s twelve keys as a table). Nothing is ever sent by itself. Three ports keep it testable without a
  desktop: `IBugReportPreview`, `IUrlOpener` (`ShellUrlOpener`), `IClipboard` (`WpfClipboard`). Help ▸
  Documentation opens the README (`docs/` is developer-facing). File ▸ Export is now the submenu `08_UI` §Menu bar
  specifies: Session CSV / Session JSON for the session last selected on a Sessions tab or opened in a summary
  window (`SessionSelection`, written by the game page and the summary opener; `SessionExportService` over the
  summary window's own `SessionExporter` writers), Chart PNG opening that session's summary window where the
  plot lives; no selection says so in the strip. `BugBundleBuilder.SysInfoOf` is public for the link and the
  Markdown. Tests: `BugReportFlowTests` (the zip, the preview's entries against the archive, the exact URL, the
  Markdown, close, cancel), `SessionExportServiceTests` (CSV and JSON where the user says, unknown session,
  cancelled save, the selection), `LogsViewModelTests` over the flow. 14 App strings en/vi/ja. Docs: `10_LOGGING`
  §Bug report flow built note (the crash dialog and minidump stay open), `08_UI` §Shell menu note, HANDOFF §P4.
- **P4 PR-2 — the "is Vulkan" fact, and the layer's registration following the ledger (2026-09-14).** The rule
  `12_BUILD` §The Vulkan layer is not registered at install time states — registered only while at least one
  Vulkan game has hooking enabled, unregistered when the last is disabled — had no way to know which game was
  Vulkan; the check was "any enabled game". Now the fact is static: `GameFileProbe` reads the executable's import
  and delay-load tables (`Infrastructure.Detection.PeImports`, bounded, read-only, no exception for a malformed
  file) and looks for `vulkan-1.dll` as an ASCII or UTF-16 string in the bounded scan (a `LoadLibraryW` loader —
  `hook-harness --vulkan`'s shape); `StaticDetectionResult.UsesVulkan` is true / false / null, and the sweep
  stores a true as the id `vulkan` in `capability_flags` beside the rule ids ("Supports Vulkan" on the game page).
  `Application.Vulkan.VkLayerReconciler` over the new `IVkLayerRegistrar` port (`Infrastructure.Vulkan.VkLayerRegistrar`,
  the same manifest and HKCU value `--register-vklayer` writes) computes the desired registration from the consent
  store's enabled games joined to their flags and moves the registration to it — after the pipe handler's stamp,
  revoke and guard block, after the console `consent grant` / `revoke`, and after every detection sweep, so the
  crash policy's auto-disable and a fact that arrives after the consent converge within one interval; not staged
  = nothing to do. `--register-vklayer` runs the reconciler and refuses when no hook-enabled game references the
  loader; Settings' Register button follows the same rule (`HookedGameViewModel.UsesVulkan`). An Agent off the
  profile ledger (`--data-dir`) gets an inert registrar: a test or developer ledger never touches HKCU. Tests:
  `VkLayerReconcilerTests` (first Vulkan game registers, last disables, a D3D game does not, idempotent, not
  staged), `PeImportsTests` (a synthetic PE32+: both directories, the wide literal through the probe, a malformed
  file, a non-PE reads as unknown), `DetectionSweepTests` and `StaticGameDetectorTests` (the three-valued fact).
  `Settings_VkLayer_Note` reworded en/vi/ja. Docs: `17_HOOK_ENGINE` §Vulkan built note gap (1) closed,
  `12_BUILD` §The Vulkan layer…, `05_DETECTION` §Capability hints, `08_UI` §Settings, HANDOFF §P4.
- **P4 PR-1 — the detection rules engine wired into the Agent, and the Supports row (2026-09-14).** The engine,
  schema, validator and fixture corpus existed since P0 with no production caller and no writer. Now:
  `Application.Detection.DetectionSweep` runs `StaticGameDetector` over the real `GameFileProbe` for every game in
  the library whose `DetectionCacheKey` is stale — never scanned, the exe changed, the rules moved on — and
  persists through the new `IGameRepository.ApplyDetectionAsync`; `Agent.Hosting.DetectionHostedService` hosts it
  under `--serve` on its own task (the probe blocks on file I/O; the 1 Hz poll never waits on it), every 15 s and
  at once when `UpdateRules` re-seeds the rules file. **Schema 0004** `games.detection_exe_size_bytes` /
  `detection_exe_mtime_ms`: the key's exe half apart from `exe_size_bytes` / `exe_mtime_ms`, which are the consent
  fingerprint the gate reads and which a background scan must never refresh. The repository applies
  `05_DETECTION` §Caching's provenance rule per field — a `user` field is never touched, a `detected` one is
  refreshed, an empty field with no entry is filled and badged (without that clause a fresh game could never
  receive an engine), a null detected value erases nothing — and writes `capability_flags` whole as a JSON array of
  the rule ids (`dlss`, `dlss_g`, `dlss_rr`, `streamline`, `fsr`, `xess`, `xefg`; the App's `CapabilityNames` reads
  both spellings). App: Tools ▸ Update detection rules is a real command (`UpdateRules` over the pipe, the seeder's
  outcome in a snackbar); the game page's Supports row shows what the sweep wrote and says "not scanned yet" only
  until the Agent's next tick. Tests: `DetectionSweepTests` (5: scanned once then current, exe change and rules
  change re-scan, unreadable exe and unusable rules, the wake, the key field by field),
  `SqliteGameRepositoryTests` (the provenance rule end to end, the consent fingerprint untouched),
  `LedgerDatabaseTests` (0004), `GameDetailViewModelTests` (the Supports row from a detection write, the rule
  ids). 4 App strings en/vi/ja + the Supports empty-state reworded. Docs: `05_DETECTION` §Caching built note,
  `06_DATA_MODEL` §Writer ownership / §games / §Migrations, `04_CAPTURE` §Threading model, `08_UI` §Games,
  HANDOFF §P4 and `15_ROADMAP` §P4 struck.

### Fixed

- **The legal documents say what the software does, in release form (2026-09-16).** EULA 1.0, Disclaimer 2.2,
  Privacy Policy 2.2 — the `-draft` suffix (which `legal_acceptance.version` recorded) and the "Draft for review"
  banners are gone, so the Legal Gate shows all three again. **Privacy Policy §2** listed three requests the software
  has never made — a weekly safety-list update, a weekly detection-rules update, an opt-in Steam store lookup —
  under an audit note saying so since 2026-08-04; version 2.2 keeps the two real rows (Velopack's update check and
  download) and says there is no other request, with the removal recorded as history in the document itself. §1 no
  longer mentions cover art (no such feature) and states the log retention the code has (7 days App, 14 Agent).
  **Disclaimer:** the 25-line accuracy-audit block that sat inside the accepted text moved, unchanged, to
  `docs/legal-drift-history.md`; a five-line pointer explains how §4 is kept true (`legal/ACCURACY.md` +
  `accuracy-check`); the contact footer the other two documents carry is added. **Third-party notices:** the IGCL/ADLX
  box is ticked with its check named (`license-check` §1, in the gate since P0); the About-tab box says it is not
  built and not a gate for the first pre-release; the trademarks pointer reads §6. **README** §Privacy collapsed to
  one accurate paragraph. **Settings:** the `privacy.online_metadata` toggle is no longer shown — a switch for a request
  that has no code is the same over-disclosure — and the setting stays reserved (`06_DATA_MODEL` §Settings registry,
  `08_UI`). `20_OPEN_QUESTIONS` §S20: the feed lands as a new policy version, not as rows kept in advance. The
  accuracy block's "unreleased" sentence is deliberately untouched: it becomes false with the tag and is rewritten in
  the tag commit.
- **itch.io import reads butler's database, so an install location outside `%APPDATA%` is found (2026-09-16).**
  `ItchLibrarySource` scanned `%APPDATA%\itch\apps\*\.itch\receipt.json.gz` and nothing else; on the owner's machine
  the itch app's one install location is `D:\another\it`, `apps\` is empty, and the import logged `itch — 0 title(s)`
  with no reason — every itch title had to be added by hand. The app records its installs in
  `%APPDATA%\itch\db\butler.db`: `install_locations` (the folders the user chose), `caves` (one per install, with the
  folder name or a custom folder) and its own `verdict` JSON naming the executable butler launches. The adapter now
  copies that database to a temporary directory (the running app holds it in WAL mode), opens the copy read-only,
  joins the three tables, takes the first Windows candidate of the verdict as the executable (an HTML or Love2D game
  leaves the locator's guess), and still scans `apps\` receipts for anything the database does not list; an install
  found both ways is one row, the database's. It logs one line per run — what was consulted and what each source
  yielded — through the importer's sink, so `0 title(s)` has a reason beside it. `ItchLibrarySource`'s constructor
  now takes the itch directory (`%APPDATA%\itch`), not `apps\`. Tests: `StoreLibrarySourcesTests` (a butler.db built
  in the test with a location elsewhere, a custom folder, a verdict path with forward slashes, an HTML-only verdict,
  a verdict that will not parse, a receipt beside a cave and a receipt-only install; no database and a garbage
  database both fall back to receipts and say so). Docs: `05_DETECTION` §Platform signatures.
- **The Games card's hooking chip reads at 2.6:1 no more, and the Dashboard's FG qualifier no longer clips
  (2026-09-16).** "Hooking on/off" was `ui:Badge Appearance="Info"`: WPF UI paints that with `PaletteLightBlueBrush`,
  a fixed Material colour outside both theme dictionaries, under the theme's white text — `#03A9F4` in dark, below
  AA-large — and no `#` in our XAML for `NoXamlHardCodesAColour` to catch. It is a `Border` in the new `HookStatePill`
  style now (ON: the accent pair the ×FG chip uses; OFF: the Pill's muted fill with secondary text), the edit
  dialog's "Detected" badge is `Secondary`, and `ContrastTests` holds it: no App XAML uses the four palette
  appearances, and every pill's (text, fill) pair composited over the card and window backgrounds is ≥ 4.5:1 in both
  theme dictionaries, with the old pair asserted below it so the check is known to bite. The FG qualifier chip on the
  Dashboard's recent-sessions row was reported with it and had a different defect — a sentence-length qualifier in a
  star column with no trimming overflowed the row — so the qualifier text trims with an ellipsis at 260 px (the
  readout's tooltip carries the full sentence) and the row's readout column is sized to content with the game's name
  trimming first. Docs: `08_UI` §Contrast, `16_WPFUI_SYNTAX` §Theming.
- **A print verb no longer migrates the ledger it prints, and a close checkpoints the WAL it wrote (2026-09-16).**
  `FrameLedger.Agent --console sessions`, run against the owner's own ledger while its on-disk file was at schema 2,
  applied scripts 0003 and 0004 to it: every verb opened through the one migrating `LedgerDatabase.OpenAsync`. Found
  while investigating a ledger whose day of rows lived only in a 4 MB WAL whose 1006 frames carried a salt one
  generation older than the WAL header, over a main file no checkpoint had reached — a fresh connection (the console,
  a next launch) saw the 14 September state, while the running App and Agent saw the day through the shared WAL
  index. The mechanism is unrecorded (`06_DATA_MODEL` §Migrations carries what was observed and what is not known);
  the data was rebuilt from the old-salt chain by hand. What changed: `LedgerDatabase.OpenReadOnlyAsync` — an
  existing file, `query_only`, no pragma that writes, no `MigrationRunner`; a schema newer OR older than the build is
  refused with the reason, and `WriteAsync`/`MaintainAsync` throw. The Agent's `sessions`, `consent list`,
  `killswitch status` use it (`Program.PrintsOnly`), `db path` opens nothing at all, and a refusal is a console line
  with exit 7, never a crash dump; the App's `--diag` uses it too. `OpenAsync` now runs a passive checkpoint after
  migrating and reports it (`LedgerDatabase.OpenDiagnostics`, one `ledger: wal at open` log line), and
  `DisposeAsync` runs `wal_checkpoint(TRUNCATE)` and reports that, so a clean close leaves the main file complete
  whether or not SQLite's own close-time checkpoint runs. Tests: `LedgerDatabaseTests` (an older schema is refused
  byte-for-byte unchanged; a read-only open reads and refuses to write; a missing file creates nothing; a close leaves
  the row readable through `immutable=1`), `AgentReadOnlyVerbsTests` (the print-verb set, the refusal's exit code and
  the untouched file, `db path` without a ledger). Docs: `06_DATA_MODEL` §Migrations, `12_BUILD` §Debugging,
  `10_LOGGING` §Diagnostics, HANDOFF §Traps.
- **A start that fails says so on screen (2026-09-15).** When `App.RunAsync` caught its own exception — a ledger
  that would not open (a newer schema, a locked file), a host that would not start — FrameLedger wrote a Fatal line to
  `ui-*.log` and exited with code 1 with nothing on screen, so a user saw a click do nothing. It now shows
  `StartupFailureDialog`, a Win32 message box like the crash dialog (the shell is what failed): the exception's message,
  the log folder, and Yes to open that folder; the exit code is still 1, and a crash the dispatcher already reported
  shows no second dialog. Two strings (en/vi/ja). Tests: `StartupFailureDialogTests`. Docs: `10_LOGGING` §Crash
  handling.
- **Bug bundles no longer carry the user's name in log paths, which the privacy policy already promised
  (2026-09-15).** `legal/PRIVACY_POLICY.md` §3 says "logs are redacted (user directory paths removed) before
  bundling" and `10_LOGGING` named a `RedactingEnricher`; neither existed, and `BugBundleBuilder` copied `ui-*.log`,
  `agent-*.log` and `overlay-*.log` verbatim on the stated ground that they carry no paths beyond the data directory,
  which is itself under the user's profile. On the dev box the profile path appeared 8 times in the UI log, 7 in the
  Agent's and in 175 overlay headers. `App/Services/LogRedactor` now rewrites every log copy in the zip: the name in
  each `X:\Users\<name>` path becomes `<user>`, and so does a profile directory's last segment elsewhere, in
  backslash, doubled-backslash (Serilog's JSON), forward-slash, any-case and ANSI-`?` spellings, byte for byte
  otherwise (Latin-1 round trip, so UTF-8 and ANSI logs both survive). The files on disk keep their paths; the policy's
  sentence is now true without a word of it changing. Tests: `LogRedactorTests`, `BugBundleBuilderTests`. Docs:
  `10_LOGGING` §Serilog configuration (the enricher claim struck, with where the rule is enforced) and §Bug report
  flow step 2.
- **Two unmeasured facts were stored as values (2026-09-15).** The quality byte's `0xFF`, the Overlay's "a hook ran
  and could not tell" (`fl_shm.h` `upscalerQuality`), was aggregated as a number, so a session and the live card
  carried the text `255`, which the game page and the Dashboard append to the upscaler's name: every AMD dispatch
  carries `0xFF`, so an FSR title would read "FSR 255". `SessionAggregator` (row and segments) and
  `SessionProgressCalculator` now leave those frames out, and a quality nobody told is null (`FrameSample.QualityNotTold`).
  And `reflex_active` was written `false` on every hooked row with no latency sample, although nothing measures Reflex
  today; it is null there now, so an export no longer says "off" for a thing never measured. Tests:
  `SessionAggregatorTests` (not told is null, told frames still count; Reflex null), `SessionProgressCalculatorTests`.
  Found while checking `legal/ACCURACY.md` against the code. Not done: naming a preset the hook did read, which
  `03_METRICS` assigns to a `Domain.UpscalerNames` that does not exist; no measured title has produced one.
- **Test runs stop filling the real data folder with hook-harness logs (2026-09-15).** The injection tests inject the
  shipped Overlay, whose log goes to `%LOCALAPPDATA%\FrameLedger\logs` by design (§S21: not the environment, and no
  switch in the injected DLL), so the owner's folder held 2506 overlay logs, 2492 of them hook-harness's from test
  runs since 2026-09-06, beside 14 from real titles. Each test binary now removes, when its run ends, exactly the logs
  it caused: an `overlay-*.log` created after the run started whose first line names a hook-harness image it owns. A
  Catch2 listener in `guard_test.cpp` does it natively (`src/native/tests/overlay_log_sweep.h`, the image must be this build's
  `FL_HARNESS_EXE`), and `tests/Shared/HarnessOverlayLogSweep.cs`, an xUnit assembly fixture that
  `FrameLedger.DrainFixtures.targets` compiles into every test project staging the harness, does it for the managed
  suites (the copy beside the test binary, plus the renamed copy `PipeEndToEndTests` registers). Assemblies run in
  parallel with their own copies, so none removes a log another is about to read, and a game's log is never touched.
  The new `test-artifacts` gate (`tools/test-artifacts-check.ps1`, seven self-test cases) fails for a harness log
  created during the gate and still there, which is red on the unswept tree. Tests: `[sweep]` in `fl_guard_test`,
  `HarnessOverlayLogSweepTests`. The logs from before this change are left for the owner. Docs: `17_HOOK_ENGINE`
  §Native logging, `12_BUILD` gate list, `14_TESTING`, HANDOFF §Traps. No Overlay change, so no hook-path overhead.
- **The accuracy block says what the code measures on 2026-09-15 (`legal/ACCURACY.md`, embedded in `README.md` and
  `legal/DISCLAIMER.md`).** Dated 2026-09-06, it still said there was no Agent loop, storage, charts, library import
  or UI, that OpenGL intercepted nothing, that no shipped component drove the 30 s re-scan and that video memory was
  not measured — all false since. Every sentence was re-checked against the code: OpenGL is measured; Vulkan only
  when FrameLedger starts the game (a running Vulkan title gets the Direct3D component, unverified); FSR is named FSR 3
  or unversioned, never FSR 2 or 4; the preset is not reported anywhere; frame generation is Direct3D-only and falls
  back to a qualified Presented FPS; whole-card video memory is recorded while per-game VRAM, shader-stutter
  attribution, Reflex, HDR and CPU temperature are not; the shipped Agent runs the re-scan and the kill switch can
  only refuse. Nothing has been released. `README.md` outside the block: the safety row's "re-scan does not run at
  all", the XeSS and "always shown" promises, the Vulkan requirement, CPU temperature, NVAPI's Reflex, and a note that
  no release exists. `legal/DISCLAIMER.md` is 2.1-draft (the first-run gate shows it again): two history bullets the
  software caught up with are struck, and the path-tracing "confidence-scored suggestion" it never computes is
  corrected. Docs that contradicted the code: `04_CAPTURE` (attach mode on a running Vulkan title is not Tier 2 — an
  open decision for the owner), `05_DETECTION` (four runtime facts have no writer), `17_HOOK_ENGINE` (`wglSwapBuffers`
  is built), `15_ROADMAP` and HANDOFF (`StaticGameDetector` runs in `DetectionSweep`). Stale comments in `dllmain.cpp`
  (OpenGL "NOT HERE"), `layer.cpp` (`vkQueuePresentKHR` "NOT hooked") and `fl_guard.cpp` (the signer lookup "not wired
  yet") now say what the code does; comments only, no behaviour change.
- **The native log test read another process's log, and was filed as a flake four times (2026-09-14).**
  `guard_test.cpp`'s `FindOverlayLog` took the first `overlay-<pid>-*.log` by name in the real
  `%LOCALAPPDATA%\FrameLedger\logs`, which keeps every run's log — 2130 on the dev box. A harness that drew a
  reused pid (6840) read a finished log from 2026-09-06 and failed on its STOP line; a re-run drew a fresh pid and
  passed. The failure output had printed the stale path every time. It now takes the newest log created at or after
  the child process started (`GetProcessTimes`), and the test now plants a 1999-dated log under the child's pid
  before injecting, so the old lookup is red on every run instead of on pid reuse — measured: with the lookup
  reverted, 9 of 18 assertions failed reading the planted file; with the fix, 18 of 18 pass and the file is removed
  either way. Test-only: no shipped binary changes.
- **A DXGI present counter that went backwards read as four billion unseen presents (2026-09-14).** The Present
  hook reads `IDXGISwapChain::GetLastPresentCount` before forwarding and took `now − previous` on the chain's slot
  as unsigned arithmetic. A chain released and re-created at the same address inherits the slot and its old count
  while DXGI's count for the new chain starts near zero, so the difference wrapped to ~4e9: the owner's ledger
  held `dxgi_unseen_total = 4294967243` (−53 as `uint32`) on a title whose pacer never presented past the hook, the
  record byte saturated at 255, and that saturation is the `DxgiSaturated` refusal that took the frame-generation
  factor with it. `fl_dxgi_count.h` now owns the arithmetic: a regression is a reset — the present claims no delta
  (as a chain's first hooked present does), nothing is added, the slot re-arms on the new reading — and the
  session total saturates at `UINT32_MAX` rather than wrapping, the same direction the record byte already took
  (a numerator must read high and be refused, never low and be believed). Compile-time `static_assert`s pin the
  algebra; ctest `fl_dxgi_count` (`hook-harness --probe-dxgi-count`) drives the two behaviours. Hook-path cost: one
  compare on the virtual call the hook already made (`--probe-cost` in the PR). Docs: `fl_shm.h` @48/@52,
  `17_HOOK_ENGINE` §Hook inventory.
- **A safety notice was a red bar with no way to close it (2026-09-14).** WPF UI 4.3.0's `InfoBar` template has no
  `ContentPresenter`, so the action button every persistent banner carries as the InfoBar's Content — "Record this
  session without measuring it" on a refusal, Dismiss on an unhook or a degrade, the only dismissal once
  `IsClosable="False"` removed the X per `08_UI` §Notifications policy — compiled, bound and was never drawn. The
  Agent-offline banner's Retry and the game page's Re-enable were dropped the same way. `Styles/FrameLedger.xaml`
  now re-templates `ui:InfoBar` with the upstream template verbatim plus one presenter column, collapsed without
  content; no view model changed. `PagesLoadTests.TheInfoBarTemplateRendersItsContent` renders the app's template
  and the library's and asserts the button appears under one and not the other, so it turns red when upstream
  gains a presenter and the copy should go. `legal/licenses/wpfui-MIT.txt` added (the template is MIT text).
  Docs: `08_UI` §Notifications policy correction, `16_WPFUI_SYNTAX` §Gotchas, HANDOFF §Traps.
- **Frame generation read N/A beside a detection that had succeeded (2026-09-14).** Two of the owner's hooked rows
  carried `fg_mode = 'dlssg'`, `fg_source = 'api'` and `fg_factor` NULL — the Streamline tags named DLSS-G, the
  census had `sl.dlss_g.dll`, and the token count refused — and every FG surface in the App said N/A: the readout
  fell into the "detected" shape and printed `N/A → N/A FPS (×0.0 FG)` (a factor nobody counted, from `factor ?? 0`),
  the game page said `DLSS-G ` with a trailing space, the grid's Displayed and FG× said N/A with no reason, and the
  reason existed nowhere — `FgWindow.Refusal` was computed and dropped. Now: **schema 0003** adds `sessions.fg_refusal`
  (`Vocabulary.FgRefusal`: `not_counted`, `no_evaluations`, `non_uniform`, `dxgi_saturated`, …), written by
  `SessionAggregator.ApplyFg` whenever no factor is published; `SessionProgress` gains optional `fgRefusal` and
  `fgRuntimeCensus`; and the UI gains a fourth shape, `FpsReadoutKind.IdentifiedUncounted` — Presented FPS with a
  warning chip "DLSS-G active — factor not counted", the refusal in plain words as its tooltip ("read it as Displayed,
  not Native"), `DLSS-G · factor not counted` on the game page, N/A in Displayed and FG× (no factor exists; it is not
  a measured none). `GeneratedLine` no longer accepts a missing factor. The `fg_runtime_loaded` chip now names the
  module from the census (`08_UI` §FPS display rule asked for it from the start): `FG runtime loaded (sl.dlss_g.dll)
  — may include generated frames`. 15 App strings en/vi/ja (`Fg_Refusal_*`, `Fps_Fg_Identified_*`,
  `Fps_Census_RuntimeLoaded_Named_Format`, `Fg_Factor_NotCounted`). Tests: `FpsPresentationTests` (the shape, the
  live wire, the module name, the counted label), `GameDetailViewModelTests` (the page and the grid),
  `SessionAggregatorTests` (identity without a count records the refusal, the owner's zero-token shape, a counted
  factor carries none), `SessionProgressCalculatorTests`, `LedgerDatabaseTests` (the column). Docs: `03_METRICS`
  §Metrics FG factor row, `08_UI` §FPS display rule, `06_DATA_MODEL` §sessions + §Migrations, `07_IPC` §Messages.
  What this does NOT do: decide the count. Why the count refused on those two rows is what the new column will
  say on the next capture — the `dxgi_unseen_total` of 4294967243 on one of them (a `uint32` gone negative in the
  Overlay's DXGI delta) is a separate, native fix.
- **Two Settings descriptions were centred once they wrapped (2026-09-14).** A `TextBlock` with a `MaxWidth`
  inside a stretched `StackPanel` is centred by WPF as soon as its text wraps, so the descriptions under
  *Record games launched outside FrameLedger* and *Fetch store metadata online* showed their second line offset
  (owner screenshots, en and vi). Both carry `HorizontalAlignment="Left"` now, like the Vulkan layer and logon
  task rows already did. XAML only.

