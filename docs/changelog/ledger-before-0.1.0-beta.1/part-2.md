<!-- Moved verbatim from CHANGELOG.md on 2026-10-19 (beta.24, owner decision D66): part 2 of 5 of the ledger kept before the first tag, newest first. CHANGELOG.md lists every part. -->

## Ledger before 0.1.0-beta.1 (continued, part 2 of 5)

### Added

- **P3 PR-9 — first run, the Legal Gate (FR-11), the closing sweep (2026-09-14).** `FirstRunWindow` /
  `FirstRunContent` / `FirstRunViewModel`, shown before the shell whenever `LegalGate` says a document has no
  acceptance at this build's version: the four documents embedded at build (`legal/EULA.md`, `LICENSE`,
  `legal/DISCLAIMER.md`, `legal/PRIVACY_POLICY.md` — the text this build was reviewed with, authoring comments
  stripped, `{{RELEASE_DATE}}` left for release), one Accept writing four `legal_acceptance` rows with each
  document's own `**Version:**` line, Decline ending the host, a closed window counting as a decline; the
  Agent-setup step (unelevated by default, elevation optional and required by no tier, the `HelloAck` facts as
  they arrive); the hooking explainer reusing the consent dialog's reviewed Tier 1 / Tier 2 paragraphs and
  saying hooking stays off until enabled per game; the import placeholder. Settings ▸ "Reopen the legal
  documents" shows them read-only. D8 unchanged: the gate is not a consent precondition. NFR-4: the first
  window's time from process start is logged every run (1.3–1.5 s on the dev box), the 100-session query is
  asserted under 500 ms, charts draw ≤ 2 × 2000 points per series. Tests (App, 120): `LegalDocumentsTests`,
  `FirstRunViewModelTests` (6), the Settings reopen, both first-run steps in `PagesLoadTests`. 29 App strings
  en/vi/ja. Docs: `08_UI` §First-run flow built note with the NFR-4 numbers, `06_DATA_MODEL` §legal_acceptance,
  `20_OPEN_QUESTIONS` §G legal-versioning row answered, `15_ROADMAP` §P3 code-complete, `09_I18N` scope line,
  CLAUDE.md's App line, HANDOFF row 9 struck and the one START HERE moved to a P4 pointer.
- **P3 PR-8b — the tray, the Agent's maintenance flags, the Settings buttons over them (2026-09-14).** The tray
  (H.NotifyIcon.Wpf, `TrayHost` over a WPF-free `TrayViewModel`): four drawn icons — ● a Tier-1 session runs,
  ◐ sessions run and none is measured, ⏸ the Agent reports paused, a ring otherwise — the tooltip naming the
  game; menu Open / Pause–Resume capture (`PauseCapture`/`ResumeCapture`, the state shown is the `PauseAck`'s) /
  Agent status / Exit; a left click opens the window; **minimize to tray on close** honoured through
  `WindowClosePolicy` while a tray icon exists; **FR-3.8's balloon** "Session saved — game — click to view" only
  while the window is off screen (the Dashboard's snackbar otherwise — one event, one channel), its click opening
  the summary; a safety event is never a balloon. Tools ▸ Agent status… / Vulkan layer registration… navigate.
  The Agent's `--install-task` / `--uninstall-task` (`Infrastructure.Startup.LogonTask`: `\FrameLedger\FrameLedger.Agent`
  through the Task Scheduler COM API — `schtasks /Create /SC ONLOGON` is "Access is denied" unelevated, measured —
  this user's logon trigger, interactive token, lowest run level, `--serve`) and `--register-vklayer` /
  `--unregister-vklayer` (`Infrastructure.Vulkan.VkLayerRegistration`: the HKCU `ImplicitLayers` DWORD named by
  the manifest; register refuses while no game has hooking on; unregister also removes a stale value naming our
  manifest elsewhere); `HelloAck.vulkanLayerRegistered` now reads the key; `--diag` is the one flag the Agent
  still answers "not this binary's". Settings: Register / Unregister the layer and Register / Repair / Remove the
  task run the flag as a child process and re-read the machine. Tests: `TrayViewModelTests` (7), the Settings
  additions (3), `VkLayerRegistrationTests` (under a test's own key), `LogonTaskTests` (Integration: a real task
  under a test's own name), `AgentCommandLineSurfaceTests` (the flags are verbs), the e2e flags case now `--diag`.
  27 App strings en/vi/ja. Docs: `08_UI` §Shell tray note and §Settings 8b note, `17_HOOK_ENGINE` §Vulkan built
  note with its two stated gaps, `12_BUILD` §Agent flags, HANDOFF row 8 struck.
- **P3 PR-8a — Settings, the safety notices, the Logs page, `--diag` (2026-09-14).** The Settings page over the
  registry (`RegisteredSettings`, D16): Capture first — the global kill switch (`hooking.kill_switch`, the gate's
  fourth input, so a running measured session stops at its next 30 s re-scan) with its state line, the
  hook-enabled games with a per-row "Turn off" that is `SetHookEnabled { enabled: false }` over the pipe (the
  App still writes no hook-state column, D15), the Vulkan layer state from `HelloAck`, the safety-document link;
  then minimum session length / sensor interval / raw-series retention as bounded `NumberBox`es that refuse an
  out-of-range value in place, background capture, the Agent's elevation state with the "not required by any
  tier" paragraph, "Start FrameLedger with Windows" as this user's `HKCU\…\Run` value
  (`Infrastructure.Startup.RunAtLogon`), minimize-to-tray (stored; honoured by PR-8b), online metadata (off),
  the update channel (stored), debug logging (moves the App's Serilog `LoggingLevelSwitch` at once), and Legal
  reopen as a "not yet" line until PR-9. **Safety notices** (`Services/SafetyNotices`, `08_UI` §Notifications
  policy): `CaptureRefused`, `SafetyUnhook` and `CaptureDegraded` become persistent `InfoBar`s above every page
  with the family and signal named from the `Safety_*` strings, never a toast, never a timeout; the one button
  on a refusal is "Record this session without measuring it" — the acknowledgement, since the Agent already
  records Tier 2; `CaptureError` goes to the 4 s strip. **Logs** (`LogTail`, `LogsViewModel`): the newest
  `ui-*.log` / `agent-*.log` with shared read and a 2 MB cap, level filter over the template's prefix, search,
  pause, a 2 s refresh only while the page is shown, Open logs folder, and Export bug bundle = `10_LOGGING` step
  2 (`BugBundleBuilder`: our logs of the last seven days verbatim, the five newest overlay logs, `sysinfo.json`,
  `settings.json`; nothing is sent, steps 3–5 are P4's). **`FrameLedger.exe --diag`** writes the environment and
  capability report to `logs/diag-<ts>.txt` and stdout without starting the host. Tests (App, 100; Infrastructure
  +2): `SettingsViewModelTests`, `SafetyNoticesTests`, `LogTailTests`, `LogsViewModelTests`,
  `BugBundleBuilderTests`, `DiagReportTests`, `RunAtLogonTests`, and both pages rendered in `PagesLoadTests`. 69
  new App strings in en/vi/ja. Docs: `08_UI` §Notifications policy/§Logs/§Settings built notes, `10_LOGGING`
  built notes, HANDOFF row 8 amended (8b: tray, layer and task verbs), CLAUDE.md's App line.
- **P3 PR-7 — Trend, Sensors, Latency, and Compare with the mixed-tier guard (2026-09-14).** The game page's tabs
  2–6: Frametime and Distribution of the selected session (the summary window's own charts); **Trend** through
  `Charts/TrendSeriesBuilder` — one point per hooked session oldest first (Average = Native where frame generation
  was measured, Presented where not; Displayed only where measured; the lows; max GPU °C), FR-6.4's exclusion of
  mid-session-change sessions by default with the toggle and the excluded count, FR-6.3's markers wherever two
  consecutive sessions' hardware snapshots differ (GPU driver, GPU, CPU, display mode, OS) as labelled lines on
  a date axis; **Sensors** (core / hotspot °C, load %, power W on one plot; the game's own per-present VRAM and
  the adapter-wide sensor as two differently-labelled series on another); **Latency** (the `latency_us` blob with
  the stored avg / p95 lines, only when Reflex reported). **Compare** (`CompareViewModel`): the last 200 sessions
  across games in a checkbox picker (2–5), `IMixedTierPrompt` — a blocking `ContentDialog` that compares nothing on
  a decline (FR-6.2), the tier legend on the chart and an `InfoBar` when it proceeds — one percentile curve per
  hooked session (`Charts/PercentileCurve`, shared with the distribution), the stat table with Native / Displayed /
  Presented FPS as three separate rows, the best value of each row highlighted (higher wins for FPS, lower for
  temperature and stutter, N/A never), Tier-2 cells at N/A, PNG export. Tests (App, 74): `TrendSeriesBuilderTests`
  (order, the exclusion, Average/Displayed per shape, the markers' text), `CompareViewModelTests` (the picker's
  bounds, a declined guard, an acknowledged mixed comparison with its legend and N/A cells, the best-of-row
  rule), the selected-session series and the trend toggles on the game page, and all four new charts rendered
  through Skia in `PagesLoadTests`. 65 new App strings in en/vi/ja. Docs: `08_UI` §Games tabs and §Compare built
  notes, HANDOFF row 7 struck.
- **P3 PR-6 — the session summary window and the charts: `ChartTheme` over two palettes, min/max decimation,
  the frametime timeline with stutter markers, segment ribbon and sensor overlay, the distribution, FR-8.3's
  override, tags/notes, and CSV/JSON/PNG export (2026-09-14).** `Charts/SessionSeriesLoader` decodes a session's
  blobs once (cumulative time, the generated and gap bits, the application-frame view with
  `Domain.Metrics.StutterDetector` over it, segments from frame index to seconds, sensors aligned to `t_ms`);
  `Charts/Decimator` is FR-5.3 (every bucket's min and max in order, ≤ 4000 points per draw — a 500k-point
  series keeps its spike and its dip); `FrametimeChart` (native by default, the every-present series behind the
  FR-5.4 toggle only when generated frames exist, PSO-compile stutter coloured apart per FR-5.5, alternating
  segment spans, GPU temperature / load on the right axis) and `DistributionChart` (FPS histogram, the percentile
  curve through `Domain.Metrics.Percentile`) draw ScottPlot 5.1.59 plots under `ChartTheme` — one subscription to
  `ApplicationThemeManager.Changed` re-colours every live plot from `Styles/ChartPalette.{Dark,Light}.xaml`, the
  one place hex colours are allowed, with `ChartPaletteTests` pinning the two files' key parity.
  `Windows/SessionSummaryWindow` (a secondary `FluentWindow` with its own dialog host) shows the stored
  aggregates as stat cards — never recomputed, the lows labelled *(presented)* when frame generation was not
  measured — the chips resolved through `TriStateResolution` with FR-8.3's dialog writing the override to
  `session_annotations` beside the measurement (and the game default on request), the crash and safety-unhook
  bars, the tags/notes editor, and FR-9: `SessionExporter` writes the CSV with `03_METRICS` §Export schema's
  column line verbatim and its `#` header block, InvariantCulture throughout, no rows for Tier 2; the JSON
  (`frameledger-session/1`); PNG via `Plot.SavePng`. Opened from a Sessions-tab row and from the Dashboard's
  recent list; FR-3.8 is an in-app notice on `SessionCompleted` until the tray exists. Tests (App, 67):
  `DecimatorTests`, `ChartPaletteTests`, `SessionSeriesLoaderTests` (the bits, the views, the segment mapping,
  −1 sensor ticks dropped, a 2000-frame round trip with stutter flags), `SessionExporterTests` (the schema line,
  the header, invariance under vi-VN, a Tier-2 export with no rows, the JSON with N/A omitted),
  `SessionSummaryViewModelTests` (cards from the row, the override beside the measurement and its clearing,
  tags de-duplicated, exports through a scripted saver). 57 new App strings in en/vi/ja. Docs: `08_UI` §Session
  summary and `16_WPFUI` §ScottPlot theme sync built notes, HANDOFF row 6 struck with the two deliberate
  deviations (a dialog rather than a flyout; the toast waits for the tray).
- **P3 PR-5 — Games and Dashboard: the grid, the game page, the hooking control, the Sessions tab, the live
  card, `FpsReadout` and `TriStateChip` (2026-09-14).** `Services.FpsPresentation` decides CLAUDE.md rule 6 in ONE
  place for rows and for 1 Hz progress events — generated (`62 → 118 FPS (×1.9 FG)`, Native first, the factor a
  chip), measured none (`144 FPS` alone), not measured (Presented FPS with the census qualifier chip, muted or a
  warning) — and the table's two negatives, `—` (measured none) and `N/A` (not measured), that must not collapse;
  `FpsPresentationTests` pins all three shapes and both negatives. `Controls/FpsReadout` and `Controls/TriStateChip`
  render a model and decide nothing. Games: `GamesViewModel` (cards from `GameLibrary.ListCardsAsync`, search and
  sort in memory), FR-1.1 by pick, menu or a dropped `.exe` (`AddGameFlow`, one flow), FR-1.4 with
  **Keep sessions / Delete sessions too** revoking consent over the pipe first; `GameDetailViewModel` (header,
  **Supports** beside **Last session measured**, FR-1.3's edit dialog badging detected fields, the hooking toggle
  through `HookingConsent` with a refusal / mismatch / failure as a persistent `InfoBar` and FR-2.2's disabled
  switch with the reason inline, FR-2.5's InfoBar with re-enable, the Sessions `DataGrid` of FR-6.1 with the
  chips resolved through `TriStateResolution`). Dashboard: `LiveCaptureViewModel` over `SessionStarted` /
  `SessionProgress` / `SessionCompleted`, the recent list with game names, the totals strip
  (`ISessionRepository.CountSinceAsync` is new). Pages are navigated by type; `GameSelection` carries the game;
  `IPageNavigator`, `IGamePicker`, `IConfirmations`, `IEditGamePrompt`, `IMessageStrip`, `IAgentLink` are the
  seams the tests use. Tests (App, 54): the presentation rule, formats, `GameLibrary` over a scratch ledger, the
  game page end to end with a scripted Agent (toggle on → dialog → `SetHookEnabled` → reload; refusal → notice
  and a blocked row disables the switch; remove revokes first), the Dashboard's events, and **`PagesLoadTests`
  — every page and both templates load under the real theme dictionaries on an STA thread**, so a binding typo
  is red in CI. 130 new App strings in en/vi/ja. Docs: `08_UI` §Dashboard and §Games built notes, `16_WPFUI`
  §Custom controls as built, HANDOFF row 5 struck. **Deferred on purpose:** the card sparkline and the chip
  override flyout (PR-6), the Agent card's capability badges and Repair (PR-8), a dropped `.lnk`.
- **P3 PR-4 — FR-2.1's consent dialog: the reviewed `Safety_*` family in `Shared`, `ConsentProvenance.ConsentDialog`,
  and the Agent's stamp (2026-09-13).** `src/FrameLedger.Shared/Strings.resx` + `.vi` + `.ja` — 20 `Safety_*` keys:
  the dialog (intro that recommends neither choice, what is injected and why, **Tier 1 and Tier 2 as two
  paragraphs of the same shape**, the anti-cheat statement with the 30 s unhook, the terms statement, the typed
  phrase, the two buttons, the version-mismatch and agent-unavailable sentences) and the safety notices PR-5
  and PR-8 will render (`Safety_Refused_*`, `Safety_Blocked_Toggle_Format`, `Safety_AutoDisabled_Format`,
  `Safety_Unhooked_Format`, `Safety_RecordWithoutMeasuring`). `en` and `vi` written; **`ja` is the English
  verbatim under `safety: human review required`**, which `resx-audit` enforces. `Shared.Safety.SafetyDisclosure.Version`
  = `consent-dialog/1` (D14): `HelloAck.disclosureVersion` names what the Agent stamps against; the App's
  `HookingConsent` does not open the dialog on a mismatch (and says "restart both"), shows `ConsentPrompt`
  (WPF UI `ContentDialog`, primary button bound to the typed phrase, Enter keeps hooking off), and sends
  `SetHookEnabled { enabled: true, disclosureVersion }` — the version it showed, never a timestamp. The Agent's
  handler re-runs the pre-scan, compares the version to its own, and **stamps `ConsentProvenance.ConsentDialog`
  from its own clock** (`HookEnabledAck { enabled, outcome, prescan: "clean" }`) or answers the new
  `Error DisclosureVersionMismatch` with nothing written; `DisclosureUnavailable` remains for a composition
  that wired no version. `PipeClient.RequestEnvelopeAsync` lets the App tell a `Refused` from a
  `HookEnabledAck` (both are answers, neither is an error). `ConsentProvenance` has four names and
  `GameConsentRecordTests` pins them with the number. Tests: `HookingConsentTests` (App: the handshake gates the
  dialog, a declined dialog sends nothing, the request carries our version, stamped / refused / error each come
  back as what they are, the phrase is exact), `SafetyStringsTests` (every key in three languages through the
  satellites, vi translated, ja the English, no recommending word in any consent string), the handler's
  mismatch and stamp cases with a fake clock, and the real pipe end to end: wrong version → mismatch, our
  version → `ConsentDialog` on the row with the Agent's clock. Docs: `19_SAFETY` §User-facing consent (built;
  the console verb is the developer path), `09_I18N` (four members; the family arrived), `07_IPC` (`HelloAck`,
  `SetHookEnabled` as built), `06_DATA_MODEL` (the fourth name), HANDOFF item 2 and row 4 struck, D14 as built,
  CLAUDE.md. **Not in this PR:** the toggle that opens the dialog, FR-2.2's disabled state and FR-2.5's InfoBar
  are the game page's (PR-5); the `ja` review is a human's.
- **P3 PR-3 — the ports the UI reads and writes, the settings registry (D16), schema 0002, and the display
  producer (2026-09-13).** `ISessionRepository` gains `FindByIdAsync`, `ListByGameAsync` (newest first, bounded),
  `SummariseByGameAsync` (count, hooked count, playtime, last played per game — one query), `FindSegmentsAsync`,
  `FindSensorsAsync`; `ISessionAnnotationRepository` (new) writes the UI-owned row beside a session — tags as a
  JSON array, notes, and **FR-8.3's per-session overrides, which live on `session_annotations` and never on
  `sessions`** (`06_DATA_MODEL` §Writer ownership), beside the measurement rather than over it, so clearing one
  restores what was measured; `Application.TriState.TriStateResolution` is the precedence (manual → measured →
  inherited from `games.*_default` → N/A). `IGameRepository` gains `FindByIdAsync`, `UpdateMetadataAsync` (FR-1.3,
  wholesale; every field that changed is marked `user` in `field_provenance` so detection cannot overwrite it),
  `SetTriStateDefaultAsync`, and `RemoveAsync(keepSessions)` (FR-1.4: keep = `games.removed_at`, the row leaves
  the library and a later launch restores it; delete = the cascade). `ILegalAcceptanceStore.RecordAsync` /
  `ListAsync` (the writer FR-11's gate calls in PR-9). `GameRow` now carries the metadata, the tri-state defaults,
  `hook_consent_at`, `hook_prescan_state` and `removed_at`. **The settings registry**: `SettingsRegistry` (12 keys,
  kind, default, range or choices, who reads) and `RegisteredSettings` over `ISettingsStore` — validating writes,
  tolerant reads, nothing cached; `SettingsRecorderPolicy` resolves `capture.min_session_s`,
  `retention.raw_sessions_per_game` (0 = unlimited) and `telemetry.interval_ms` at each session start through
  the new `IRecorderPolicy` on `SessionRecorder`, whose poller factory now takes the session's options; the
  App's appearance keys are the registry's. `0002_annotation_overrides_and_removal.sql`, the first append
  (`LatestVersion` = 2). `Infrastructure.Recording.PrimaryDisplay` fills `hardware_snapshots.display_res` /
  `display_hz` from `EnumDisplaySettingsW` — they were "null until P3" since PR-B. Tests: the four PR-B adapters
  that had none (`SqliteGameRepositoryTests`, `SqliteHardwareSnapshotRepositoryTests`, `SqliteSettingsStoreTests`,
  `SqliteLegalAcceptanceStoreTests`), the annotation repository incl. the resolution over stored rows, the
  session queries incl. **NFR-4 measured: a 100-session game lists in a few ms against the 500 ms ceiling**,
  the registry both directions, the policy, the resolution, the display producer. `20_OPEN_QUESTIONS` §G's
  "Settings registry" row is closed; `06_DATA_MODEL` gains §Settings registry and the 0002 note; HANDOFF §P3
  row 3 is struck with the one deviation (no `IRtOverrideRepository` — see above).
- **P3 PR-2 — the first `.resx` family, its generator and its gate, and the App shell (2026-09-13).**
  `src/FrameLedger.App/Strings.resx` + `.vi` + `.ja` (65 keys, `ja` machine-drafted and marked `review`),
  `tools/resx-gen.ps1` writing the committed `Strings.Designer.cs` (the SDK-style WPF project runs no
  `ResXFileCodeGenerator`), and `tools/resx-audit.ps1` as `build.ps1` step 15 — self-test (11 fixture
  cases, both directions) then live: identifier keys, exact key sets across the three files, `Safety_*`
  in `ja` marked `safety: human review required` with the English verbatim or `safety: reviewed by <name>`,
  the accessor current; a tree with no family is red. `09_I18N` §Translation workflow and §Safety-critical
  strings, `12_BUILD` line 15, `13_CI_CD` and `CLAUDE.md` stop saying the tool does not exist. The App is a
  Generic Host under `16_WPFUI_SYNTAX` §App bootstrap: `FluentWindow`, TitleBar with the Agent pill, the
  Menu row (unbuilt items say so in a snackbar), NavigationView (Dashboard · Games · Compare · Logs, Settings
  in the footer), the offline `InfoBar` with Retry, `SnackbarPresenter`, `ContentDialogHost`; theme
  (Light/Dark/System with `SystemThemeWatcher`) and language (rebuilds the shell in the new culture) both
  persisted in `settings` under D16's `ui.theme`/`ui.language` through `SqliteSettingsStore` — the App opens
  `ledger.db` directly per `06_DATA_MODEL` §Writer ownership (D15). `Services.AgentConnection` is `07_IPC`
  §Client behavior as a state machine over `PipeClient`: 8 × 250 ms connect, start `FrameLedger.Agent.exe
  --serve` beside the App when absent, `Hello`/`GetStatus`, keepalive pings, the event stream, retry every
  5 s; the Dashboard's Agent card shows `HelloAck`/`StatusAck` with `N/A` until answered. Serilog
  `logs/ui-.log` per `10_LOGGING`. `tests/FrameLedger.App.Tests` (16): every key resolves in en/vi/ja and
  the satellites really load, the accessor's keys are the resx's, appearance settings round-trip, and the
  connection against an in-process `PipeServer` — Missing with nothing to start, Connected on `Hello`,
  Starting → Connected through a launcher that brings the server up, Offline then Connected again when it
  returns. Measured: first window at 1.3 s. Three things `16_WPFUI` now records: `MainWindow` is transient
  with `ShutdownMode=OnExplicitShutdown` and the host lifetime ending the process; the run is one task from
  a synchronous `OnStartup`; and `ApplicationThemeManager.Apply(…, updateAccent: true)` must precede the
  first window's XAML or the accent resources are missing at first Measure (`XamlParseException`).
- **P3 PR-1b — the command pipe, command half: `SetWatchlist`, `LaunchGame`, `SetHookEnabled` (pre-scan, no stamp
  yet), pause/resume, `StopSession`, `UpdateRules`, `Shutdown` (2026-09-13).** `Application.Ipc.AgentCommandHandler`
  answers every UI → Agent row of `07_IPC` §Messages under the one rule of §The pipe is not a trust boundary — the
  Agent establishes each fact itself. `SetHookEnabled false` revokes through the store; `SetHookEnabled true` runs
  the static pre-scan of the executable's directory, records a block or a "could not verify" through
  `RecordGuardBlockAsync` and answers `Refused { reason, family?, signal? }`, and answers a clean scan **`Error
  DisclosureUnavailable` with nothing stamped** — FR-2.1's reviewed text does not exist (PR-4), and a client's
  `disclosureVersion` is a claim, not a disclosure shown. `SetWatchlist` ensures identity rows (hooking off when
  new) and removes nothing; `UpdateRules` is the seeder's outcome and ignores any payload; `Shutdown` asks
  `IAgentLifetime` (the host's under `--serve`, a logged no-op under `--console`). `PauseCapture`/`ResumeCapture`
  flip `CapturePause`, one in-memory flag every session loop reads at its tick and tells the ring once per
  transition — supervision never pauses — and `StatusAck` gains `paused`. `StopSession` cancels the session's own
  stop token (`RecordRequest.StopToken`; the orchestrator now names every session it starts, `RecordRequest.SessionGuid`),
  which the loop reads at its next tick and ends as the new `SessionEndReason.StoppedByUser`: drained once more,
  finalized, `exit_status = normal`, the target left running. `LaunchGame` is `CaptureOrchestrator.LaunchAsync`
  over the new `ILaunchRecorderFactory` (the Agent's `AgentLaunches`: the Vulkan layer's environment unless the
  kill switch is on, the console's `launch` shape), one session per game, the launcher election after. The
  orchestrator's running-session table has one lock now that the pipe's tasks reach it (HANDOFF §P3 D12 as built:
  no `Channel`). `IExecutableIdentitySource` gains `Normalise`, so a client's spelling is never a key. In the gate:
  `AgentCommandHandlerTests` (over the real SQLite consent store: the block recorded, nothing stamped, a path in
  an `UpdateRules` payload ignored), `CaptureSessionTests` (a stop token ends as `StoppedByUser` with the last drain
  run; the pause told once per transition while the guard keeps scanning), `CaptureOrchestratorTests` (stop
  cancels the request's own token; launch once per game, refused for what it cannot name, the environment
  released with the session), and `PipeEndToEndTests` grows two scenarios over the real Agent composition and a
  real ring: the watched harness narrated → paused (status says so, state stays `capturing`) → resumed → stopped
  by guid (`StoppedByUser`, `normal`, harness still alive) → the session-less commands, including a real pre-scan
  answered `DisclosureUnavailable`; and `LaunchGame` starting the harness in launch mode and narrating it to
  `TargetExited`. **Measured on the way:** wiring the command handler into the request handler eagerly is a DI
  cycle (server → handler → commands → orchestrator → recorder → observer → publisher → server) that the
  container did not refuse and the first e2e run HUNG on (`--blame-hang` dump, 2026-09-13); the command handler is
  resolved lazily on the first command, and the e2e now writes the composition's Serilog lines to its scratch
  directory so the next hang leaves evidence. **Not in this PR:** the consent stamp (PR-4) and the App (PR-2).

- **P3 PR-1 — the command pipe, read half: `Shared.Ipc`, the Agent's pipe server, the session narration and the
  1 Hz live progress (2026-09-13).** `FrameLedger.Shared.Ipc` holds channel C's contract (`07_IPC` §C): the
  envelope, the read-half message set (`Hello`/`HelloAck`, `GetStatus`/`StatusAck`, `Ping`/`Pong`, `Error`, and
  the seven Agent → UI events), `IpcJsonContext` (source-generated, camelCase, nulls omitted, unknown members
  skipped both ways — pinned) and `IpcFraming` (4-byte LE length + UTF-8 JSON ≤ 1 MB, one write per frame, every
  refusal driven). `Infrastructure.Ipc.PipeServer` creates `\\.\pipe\FrameLedger.v2` through `CreateNamedPipe`
  with the DACL stated as SDDL (`D:P(A;;GA;;;<user>)(A;;GA;;;BA)`, `PipeAccessControl` — what §G asked for),
  `PIPE_REJECT_REMOTE_CLIENTS`, two instances with a slot each so a third client waits, and the client's token
  user compared by impersonation on its first frame; a stranger is disconnected without an answer and counted.
  `PipeClient` is the UI's end (`CurrentUserOnly`, acks correlated by id, events on a channel). `Application.Ipc`:
  `AgentRequestHandler` answers the three requests (`ProtocolMismatch` / `UnknownType` / `Malformed` /
  `HandlerFaulted` as `Error`, connection kept), `SessionEventPublisher` is the recorder's new second observer
  (`ISessionObserver`, decision D13) — `SessionStarted` at the attach, `SessionProgress` once a second and only
  while a client listens, the finished session's events through `RecordedSessionEvents` (the reason → event
  table, pinned per reason) — and `AgentStatus` is the snapshot `GetStatus` reads. `SessionProgressCalculator`
  computes the live window through the SAME Domain calculators as the row, and rule 6 holds at the wire:
  Native / Displayed / factor only when frame generation was measured, else the presented rate with the census
  qualifier — now `FgLadder.PresentedQualifier`, one function for the row and the pipe. The Agent hosts it under
  `--serve` only (`PipeServerHostedService`); `--console` composes it inert. `HelloAck` drops `etwAvailable`
  (the tier is gone) and gains `pid`; `vulkanLayerRegistered` and `cpuTempAvailable` are honest falses.
  `CaptureOutcome.Verdict` now carries the verdict that FIRED on a safety unhook, so `SafetyUnhook` names its
  family and signal. `Vocabulary.ExitStatusText` is the one spelling of `exit_status` for the column and the
  event. In the gate: `IpcFramingTests`, `IpcCodecTests`, `PipeServerClientTests` (a real pipe, in-process, incl.
  the refused stranger and the waiting third client), the calculator / events / publisher suites, and
  `PipeEndToEndTests` (Integration, `agent-harness`): the Agent's own composition in-process on a test-named pipe,
  the watcher finding `hook-harness`, and a client hearing `SessionStarted` → ≥ 3 `SessionProgress` a second apart
  → `SessionCompleted` for the same guid. **One thing that test measured on the way (2026-09-13):** the watcher's
  file-name fallback (`ProcessWatcher`, `StalePath`) adopted a sibling test assembly's `hook-harness.exe` running
  from another directory, started a session for it, and refused it for want of consent on THAT path — so the
  test's target is now a byte-identical copy of the harness under a run-unique name, and the story is keyed on
  the pid and the guid. Recorded because the same fallback would adopt a second install of a tracked game on a
  real machine; whether that is the wanted behaviour is `04_CAPTURE` §Process watcher's question, not this PR's.
  **Not in this PR:** the command half (PR-1b), the App that connects (PR-2), and any keepalive the Agent
  initiates — `Ping` is the client's.

- **P2 PR-G — kill-and-recover end to end, the FPS-impact runbook, and P2's closing sweep (2026-09-10).**
  `PartialRecoveryEndToEndTests` (Integration, in the merge gate) is `14_TESTING`'s long-standing
  `.partial` line, built: the shipped `FrameLedger.Agent.exe` captures `hook-harness` with the new
  `--console … --partial-flush-seconds <n>`, the test polls until a flush has actually landed, kills the
  Agent, and `--console recover` turns the valid prefix into an `interrupted` row whose `frame_count` is
  that prefix, deletes the file, and finds nothing on a second pass. The new option is under `--console`
  only and pinned by `AgentCommandLineSurfaceTests` beside the others: it bounds how often a running
  session writes its own crash-recovery file and names nothing about injection.
  `tools/fps-impact-runbook.ps1` (dev-only, never in `build.ps1 check`) sequences item 2's six launches —
  consent granted for the ON leg, revoked for the OFF leg, the same launch path both times so the only
  difference is the injection — reads the Agent's CPU and RSS, prompts for the game's own benchmark
  average, and prints the `spike-notes` §14 block. **It does not measure FPS and says so first:**
  FrameLedger's frame times exist only in the hooked leg. `spike-notes` §14 holds the three empty slots
  (the milestone row against a title's menus, §S1's launcher-fronted election, the FPS table) with the
  exact commands; `14_TESTING` item 2 now says *unmeasured* rather than nothing. **P2's code is complete;
  what remains are two measurements only the owner can take.**

- **P2 PR-F — the Agent host: `--serve` (watcher → orchestrator → recorder), `--console` verbs, the consent
  grant (decision D4) and the kill switch as the gate's fourth input (decision D7) (2026-09-10).**
  `FrameLedger.Agent` is a Generic Host now: `Composition.AgentServices` registers every `Application` port
  to its one shipped adapter, `WatcherHostedService` runs `PartialRecovery` first and then
  `Application.Watch.CaptureOrchestrator` — one process snapshot per second (`ToolhelpProcessSnapshotSource`),
  `ProcessWatcher` (exact normalised path, or the file name when exactly one `games` row carries it →
  `StalePath`, keyed on the real path), `ProcessTree` (a child that started before its parent is a reused
  pid), `DescendantElection` (the newest tracked descendant; a launcher's consent does not extend to what it
  spawns), ONE session per game at a time through the new `ISessionRecorder` port. `--console` carries
  `consent list|grant|revoke`, `capture`, `launch` (with the election after `LaunchTargetExited` /
  `LaunchNoPresentationRuntime`), `recover`, `sessions`, `db path`, `games add`, `killswitch on|off|status`,
  and `--data-dir` under `--console` only (decision D6); `--diag`, `--install-task`, `--uninstall-task`,
  `--register-vklayer`, `--unregister-vklayer` answer "not implemented in P2", exit 2. **D4:** `Agent
  --console consent grant` shows `OperatorDisclosure` (moved to `Application.Consent`, one text, an
  `OperatorSurface` for the first line and the stamp), refuses redirected stdin, and records the new
  `ConsentProvenance.AgentConsoleOperator` with the Agent's clock; `OperatorAcknowledgement` carries its
  provenance and `SqliteGameConsentStore` writes that name and refuses `NotRecorded`. **D7:** FR-2.4 is
  `HookRequest.FromConsent`'s fourth input — `AntiCheatRefusalReason.KillSwitchEngaged` (27, added to the
  native reason table so the mirror holds), checked before consent is read; `IKillSwitch`
  (`Infrastructure.Settings.SettingsKillSwitch` over `settings.hooking.kill_switch`) is read by
  `CaptureSession` before the request and at every guard-scan boundary, where an engaged switch publishes
  `unhookRequested` and ends the session as `KillSwitchEngaged` (`RefusedKillSwitch` when refused up front);
  `ProcessLauncher(enableVulkanLayer: false)` keeps the layer off by never setting
  `FRAMELEDGER_ENABLE_VK_LAYER`. A bounded `--console capture --seconds N` lowers the discard threshold to N.
  Tests: `AgentEndToEndTests` (Integration, in the gate) runs the shipped `FrameLedger.Agent.exe` against
  `hook-harness` with a scratch ledger and reads back ONE hooked `sessions` row — the milestone's technical
  half — plus the kill-switch refusal with no ring in the target, `consent grant` refusing redirected stdin,
  and the unbuilt flags' exit 2; `AgentCommandLineSurfaceTests` pins the surface (no pid, payload, force,
  yes, override); `ProcessWatcherTests`, `ProcessTreeAndElectionTests`, `CaptureOrchestratorTests`,
  `ToolhelpProcessSnapshotSourceTests` (this process and a child it starts), `SettingsKillSwitchTests`, the
  gate's `TheKillSwitchIsTheFourthInputAndOutranksAValidConsent`, the loop's
  `AnEngagedKillSwitchRefusesBeforeTheGuardAndStopsARunningSessionAtTheNextScan`, and the store's
  provenance round-trip. The capture host uses the moved disclosure unchanged.

- **P2 PR-E2 — the NVAPI bridge: `FrameLedger.NvapiBridge.dll`, L3 `NvapiTelemetrySource`, and the NGX probe
  in-process (2026-09-10).** A new native target, `src/native/FrameLedger.NvapiBridge/` — a C ABI
  (`fl_nvapi_bridge.h`: refcounted `FlNvInit`/`FlNvShutdown`, `FlNvReadSample` into a fixed struct with a
  `present` bit per field, `FlNvNgxState` for one pid, `FlNvDriverVersion`, and size / ABI-version / build-id
  queries so a drifted mirror is refused rather than read) — is the **only shipped binary that links
  `nvapi64.lib`**, read-only by construction, loaded by the Agent into its own process by absolute path
  (`/FrameLedger.NvapiBridge.targets`, imported by the Agent, the capture host and `Infrastructure.Tests`)
  and **never into a game**. `Infrastructure.Telemetry`: `NativeNvapiBridge` (the second P/Invoke facade after
  the guard's, same absolute-path rule), `NvapiTelemetrySource` (L3 — a clear bit is `null`, never 0; an `Init`
  that does not answer 0 disables the layer with zero faults, two throws disable it for the session; VRAM is
  **not** read, because the vendored `nvapi.h` declares neither a non-deprecated memory-info call — L1's PDH
  counter keeps it), and `NvapiNgxStateProbe`, the `INgxDriverProbe` in-process. The capture host composes
  L1 + L2 + L3 under the composite (`telemetry_source` can now read `l1+lhm+nvapi`) and **no longer spawns
  `fl-probe-nvapi.exe`**; the probe is no longer staged beside it. `Infrastructure.Native.BesideThisAssembly`
  is the assembly's one `DllImport` resolver — the runtime allows one per assembly, and the guard's static
  constructor already set it, which the first real-hardware test run found. ctest `fl_nvapi_bridge`
  (`src/native/tests/nvapi_bridge_test.cpp`) exercises every export and requires the `BRANCH:` line;
  `NvapiBridgeMirrorTests` checks both mirrors' sizes against the DLL's answers and **fails rather than skips**
  when the DLL is not staged; `NvapiTelemetrySourceTests` and `NvapiNgxStateProbeTests` script the bridge.
  `tools/versioninfo-check.ps1` requires the new DLL's version block. Measured on the dev box (RTX 5080,
  driver 616.64 r616_41) and written into `18_GPU_VENDOR_APIS` §L3: core 35 °C, GPU domain 0 %, 2670 / 15001
  MHz, throttle mask 0, PCIe ×16, memory-temperature and fan bits clear at an idle desktop.

- **P2 PR-D — the session recorder: `.partial`, five-step finalize, exit classification, crash auto-disable,
  recovery; the first hooked session stored in SQLite (2026-09-10).** `Application.Recording`:
  `SessionRecorder` owns a session end to end — identity and QPC time base, the `games` and
  `hardware_snapshots` rows, one telemetry poller per session, the `.partial` from before the first record
  to after the last, the loop, the classification, the finalize, the crash policy — on the loop's own task
  through the new `ICaptureObserver` seam on `CaptureSession` (after every drain: the poller's queue drained,
  the file flushed on its interval; the loop stays the ring's only reader). `SessionAggregator` fills every
  measured column of `sessions` and `session_segments` over Domain's calculators; `FgLadder` is the identity
  and withhold logic MOVED out of the capture host's report so the row and the report read ONE
  implementation (the report's strings and fixtures unchanged); `Vocabulary` is the tokens the schema stores.
  `SessionFinalizer`: the discard rule, then the FULL `frame_blobs` set through the new `ISeriesCodec` port —
  every column any record claimed, `latency_us` included, pinned by a reflection sweep of the DTO —
  per-stream frame times with the gap bit where an interval is not a frame time (torn slot, overwrite skip,
  first of a stream), `swapchain_ids` only past one stream, sensor series aligned to `t_ms` with −1 where a
  tick had no reading; then one transaction and the retention sweep. `ExitStatusMapper` is `04_CAPTURE`'s
  table as one function (safety stop → `unhooked_safety`; the capture side stopping on its own →
  `degraded`; non-zero exit code or an Application Error/WER event naming the exe in `[start, end+30 s]` →
  `crashed`); `CrashAutoDisablePolicy` counts only crashes within 60 s of the attach and disables on the
  second. The `.partial` (`06_DATA_MODEL` §The `.partial` file): CRC-framed append-only chunks, the valid
  prefix wins, `PartialSessionFileTests` kills a fixture at every byte offset. `PartialRecovery` turns every
  pending file into an `interrupted` row or drops it for a stated reason. `Infrastructure.Recording`:
  `PartialSessionFile`/`Store`, `EventLogCrashSource` (Application log 1000/1001, read-only), `HardwareSnapshotSource`
  (DXGI identity + registry CPU name + runtime memory + OS build; display fields null until P3);
  `DrainResult` gained `FirstSlot` so a torn slot is recorded at the RECORD it preceded; `ITargetLiveness`
  gained `ExitCode`; `TelemetryPoller` can own its source. **The unshipped host records through all of it**
  (`capture`/`launch` print `ledger: session … SAVED as sessions.id=N`, a 5 s discard threshold for bounded
  operator captures, a `recover` verb), and `CaptureHostEndToEndTests` now asserts the row, the blobs and the
  deleted `.partial` after a real launch — the first hooked session in a `ledger.db`. **One native change,
  measured on the way:** `fl_guard`'s launch-mode classification read the harness's `--vulkan` mode as
  OpenGL once the warmer host reached the first poll 20 ms sooner (opengl32 is a static import of the
  binary, vulkan-1 is mapped milliseconds later), injected the Overlay and left the layer's ring unowned;
  the guard now polls once more before committing to the injecting branch, pinned in both directions in
  `guard_test`. Hook-path overhead: none (the guard change is pre-injection; the Overlay is untouched).
  `04_CAPTURE` §Session recorder/§Crash & exit/§Launch mode, `06_DATA_MODEL` §The `.partial` file/§Blob
  encoding, `20_OPEN_QUESTIONS` §G "Session identity", `CLAUDE.md` §Solution layout.
- **P2 PR-C — the capture path promoted into the shipped assemblies; the CaptureHost is a thin shell
  (2026-09-09).** A pure move: `CaptureLoop` — every hooked session's driver since 2026-08-06 — is now
  `Application.Capture.CaptureSession`, over ports instead of delegates so the Agent's composition root
  can supply the adapters (`IRingAttacher`, `ITargetLivenessSource`, `ITargetResolver`, `IProcessLauncher`,
  `IRuntimeModuleSnapshot`, `INgxDriverProbe`, `IExecutableMarkerScan`), with `ICaptureSink`,
  `CaptureOptions`, `CaptureOutcome` (was `CaptureResult`), `SessionEndReason`, `SessionEndClassifier`,
  `FocusTally`, `RuntimeModuleSet`/`Info`, `CensusNames`, `SlTagCensusNames`, `ExecutableMarkers`/`Marker`,
  `NgxDriverState`/`NgxOverrideFlags`/`NgxProbeOutcome` beside it. `Infrastructure.Capture` holds the OS
  adapters: `ShmRingAttacher` + `ShmCaptureSink` over `ShmRingReader` (which gains no interface and no
  second consumer), `HeldProcessLivenessSource` + `ProcessTargetLiveness` (§S29(e)), `TargetResolver`
  (path only, never a pid; its one report line goes through a callback now, not the host's console) +
  `ChromiumGpuProcess`, `ProcessLauncher` (an instance over the Vulkan layer's environment, the
  duplicated enable-variable constant gone), `RuntimeModuleSnapshot`, `ExecutableMarkerScan`.
  `DrainResult` moved to `Shared` so Application can read it. The report's text for NGX state and
  markers stays with the report (`NgxDriverStateText`, `ExecutableMarkersText`); `NgxDriverProbe`
  (spawns `fl-probe-nvapi.exe`) stays in the host until PR-E2 puts it in-process. The loop's tests moved
  with it — `CaptureSessionTests` / `CaptureSessionLaunchTests` in `Application.Tests`, driven through the
  real SQLite consent adapter (a test project on Domain's `InternalsVisibleTo` would be a second mint) —
  and the report fixtures are byte-identical. Two new pins: `HookRequestSoleProducerTests` (no public
  constructor, no setter, exactly one factory) and `NoSecondRingReaderTests` (nothing but `ShmRingReader`
  opens the ring). `04_CAPTURE` §Frame source abstraction now describes what exists (`IFrameSource` never
  did) and gains §Threading model; §S27 restated a second time (the loop ships; the entry point is PR-F's);
  `CLAUDE.md` §Solution layout. Hook-path overhead: none (managed only).
- **P2 PR-E1 — telemetry L1, the composite and the 1 Hz thread (2026-09-09).** `IGpuTelemetrySource`
  had one adapter; it now has the ladder `18_GPU_VENDOR_APIS` §Abstraction describes.
  `BaselineTelemetrySource` (L1) reads the adapter's identity from DXGI once — name, LUID, ids, memory
  sizes, the user-mode driver version via `CheckInterfaceSupport(IDXGIDevice)` — as `GpuAdapterIdentity`,
  selects the first hardware adapter until the Overlay's handshake LUID names another, and carries one
  live field: adapter-wide dedicated memory in use from the PDH `GPU Adapter Memory … Dedicated Usage`
  counter bound by LUID (never a wildcard summed). **Measured on the way:** `IDXGIAdapter3::
  QueryVideoMemoryInfo`, which the doc listed as L1's adapter-wide usage, reports the *calling process's*
  usage — 0 bytes from the Agent beside a 16 GB adapter — which is exactly why the Overlay reads it
  in-process; the doc is corrected, not the number. The engine-utilisation counters are deliberately not
  read (`20_OPEN_QUESTIONS` §M10 decided: `LoadPct` is L2's vendor-reported load, labelled). `DxgiAdapters`
  and `PdhAdapterMemoryCounter` are the tree's first CsWin32 consumers (marshaling off: raw vtables, no
  apartment). `CompositeTelemetrySource` merges L3 > L2 > L1 per field, records which layer supplied
  each (`LayerOf`), applies the two-fault rule one level up to a layer whose `TryRead` throws, and
  produces the `l1+lhm+nvapi` descriptor from the layers still standing — the port gained `IsDisabled`
  so "nothing yet" and "never again" stop reading alike. `TelemetryPoller` is the `fl-telemetry` thread:
  one read per interval (≥ 500 ms), stamped with QPC (`TelemetrySample`), queued for the session loop,
  oldest dropped and counted when nobody drains. `QpcClock` names the counter the ring is in, and
  `QpcClockTests` pins `Stopwatch` / `TimeProvider.GetTimestamp` to it rather than trusting the
  documentation. §M7 and §M8 closed by the code taking the shape they asked for. Hook-path overhead:
  none (managed only, nothing in the game). `18_GPU_VENDOR_APIS` §Abstraction/§L1/matrix, `CLAUDE.md`
  pinned-stack row corrected in place.
- **P2 PR-B — the SQLite ledger: `0001_init.sql`, migrations, the consent adapter, the repositories, the
  blob codecs (2026-09-09).** `Microsoft.Data.Sqlite` and `Dapper` were referenced by zero lines; they now
  back `Infrastructure.Persistence`: `LedgerDatabase` (WAL, `synchronous=NORMAL`, `foreign_keys=ON`,
  `busy_timeout=5000`; one connection per process behind a gate; every write an explicit transaction),
  `MigrationRunner` (scripts embedded in the assembly, one transaction each, under a session-local mutex;
  a ledger NEWER than the build's scripts is refused — `LedgerSchemaException` — not read on a guess),
  and `0001_init.sql`, the `06_DATA_MODEL` v2 schema plus the columns it had no home for: `session_guid`,
  `qpc_epoch` / `qpc_frequency`, `capture_mode`, the drain's own accounting, the 2026-09 metric additions
  (Presented FPS + qualifier, DXGI-COUNTED, tag census, the driver-reported rungs, the withheld `none`),
  `fg_source` with `none` in its domain and NULL = not measured, `games.hook_prescan_state` (the third
  pre-scan state one nullable reason could not carry; nothing clears `hook_blocked_reason`), the consent
  provenance and disclosure version, and the fingerprint. Every `*_at` is unix-ms UTC, stated in the
  script. **`SqliteGameConsentStore` replaces the capture host's file store** — same semantics, every case
  of `FileGameConsentStoreTests` re-targeted (`SqliteGameConsentStoreTests`): a grant cannot clear a
  block, a block preserves the stamp, a revoke withdraws it, a default verdict is `unverified` and never
  a block, a re-grant against a different binary cannot inherit a block, an unreadable ledger answers
  `Failed` and consents to nothing. **It ships**, and `20_OPEN_QUESTIONS` §S27 is restated on that basis
  (`Domain.csproj`'s `InternalsVisibleTo` names Infrastructure now, not the CaptureHost). The unshipped
  host opens its OWN `ledger.db` beside its binary (HANDOFF §P2 decision D5) — the Agent stays the sole
  owner of `%LOCALAPPDATA%\FrameLedger` — and its e2e cases run through that ledger. Also new:
  `SqliteGameRepository` (the non-consent face of `games`: crash count, auto-disable, last injection),
  `SqliteSessionRepository` (row + segments + `frame_blobs` + `sensor_blobs` in ONE transaction — a failing
  blob insert leaves no row behind — `ExistsAsync` by guid for recovery, retention keeps the last N
  sessions' raw series and every aggregate), `SqliteHardwareSnapshotRepository` (hash-deduplicated),
  `SqliteSettingsStore`, `SqliteLegalAcceptanceStore` (read-only; FR-11 stays the UI's — decision D8),
  the Application ports and DTOs behind them (`SessionRow` column for column, generated with the SQL from
  one table so the INSERT, the parameters and the reader cannot drift), `Domain.Sessions` (`ExitStatus`,
  `CaptureTier`, `CaptureMode`), and `Infrastructure.Blobs.SeriesCodec` / `RenderResCodec`
  (`deflate-le-v1`, little-endian arrays through Deflate, NaN refused at encode time, two pairs per frame
  for `render_res`, all three RT bits preserved; 360k frame times round-trip under half their raw size).
  Hook-path overhead: none (managed only). `06_DATA_MODEL`, `04_CAPTURE` §The guard, `20_OPEN_QUESTIONS`
  §S27 and §G "Session identity", `CLAUDE.md` §Solution layout corrected in place. Also fixed in passing:
  `ShmDrainIntegrationTests.TheReaderReportsRecordsItLostWhileItWasNotDraining` read `INIT` instead of `READY`
  about one run in four (on unmodified `main` too) — the handshake is published before the present hooks and
  the present hook records before the READY store, so a case that laps the ring in ~50 ms could assert inside
  that window; it now polls, bounded, for the writer to leave `INIT` first.
- **P2 PR-A — `FrameLedger.Domain.Metrics`, and the capture host's consumer re-pointed at it (2026-09-09).**
  The calculators `03_METRICS` says to implement in Domain now exist there, under `tools/coverage-gate.ps1`'s
  95 % per-class floor, which had never evaluated anything: `FrameTimeSeries` (QPC → ms, the interval
  spanning a gap EXCLUDED rather than counted as a long frame), `Percentile` (linear interpolation, NumPy
  `linear`), `FrameStatistics` (median / 1 % / 0.1 % lows as `1000 / p`, min / max, population σ, the
  FR-4.8 guards at exactly 1,000 and 10,000 frames, the `(presented)` label carried not decided),
  `RollingMedian` (19 frames, truncated symmetric at both edges) + `StutterDetector` (`> 2 × median`, stutter
  time %, `pso_stutter_pct`), `VramAggregates` / `LatencyAggregates` / `SeriesAggregates` (N/A, never 0).
  Ported out of `FrameLedger.CaptureHost` with their tests: `FgWindow` (the factor and every refusal —
  now a `FgRefusalKind` + numbers, the English staying with the report as `FgRefusalText`, byte-identical),
  `UpscaleExtent`, `StreamSegmenter` → `SegmentBuilder`, `RecordWindow` (generic), `Tri`, and the three
  tri-states as `RtVerdict` (`RtSummary` carries `rt_frame_pct` over presents and `rays_per_pixel` over
  RT-active presents, the ×4 falsifier pinned) and `HdrVerdict`. Domain references nothing, so the
  calculators consume `FrameSample` / `WriterFacts` with Domain's own enums; `FrameLedger.Application`
  gains a reference to `FrameLedger.Shared` (HANDOFF §P2 decision D1) and
  `Application.Metrics.FrameSampleMapper` is the one place a record becomes a sample, with the two
  writer-state sentinels (`VramBudgetMb = 0`, `DxgiPresentsBeforeHook = 0xFFFFFFFF`) resolved to null
  there. `MetricEnumMirrorTests` pins every Domain enum to its Shared twin in both directions — a member
  added on one side alone fails. `MeasuredFacts` keeps the prose (which N/A, which qualifier, the withheld
  `none`) and calls Domain for the numbers; every report fixture in `CaptureHost.Tests` passes unchanged.
  Not in this PR: the FG ladder as a Domain verdict (`fg_mode` / the withheld `none` are still
  `MeasuredFacts` strings) — it lands with the recorder that stores it (PR-D). Hook-path overhead: none
  (managed only). `03_METRICS` §header, `14_TESTING` §Golden metric tests (the FG-ladder row named two
  retired rungs and an "ambiguous → `none`") and `CLAUDE.md` §Solution layout are corrected in place.
- **P1 item 4 — the unhook path, the native log, OpenGL (2026-09-06).** `StopObserving` no longer calls
  `MH_DisableHook(MH_ALL_HOOKS)`: every patch the Overlay made is in a registry, and each is restored
  **only while the bytes at its target are still our jump** (`fl_patch_check.h`; §H7 in its inline form —
  an overlay that patched after us chains through our jump and the blanket restore would have removed it
  silently). A fixed ring of structured events (`FAULT` with the exception code and the hook's name,
  `STOP`, `UNHOOK_RESTORED` / `UNHOOK_DECLINED` per patch, `HOOK_INSTALLED`, `SYMBOL_MISSING`,
  `SUPERVISION_LOST`…) is flushed to `%LOCALAPPDATA%\FrameLedger\logs\overlay-<pid>-*.log` at init, when
  the Agent bumps the new `FlControlBlock.logFlushRequested` counter at session end, and on the stop —
  never mid-frame; the CaptureHost prints the file and its notable lines. `opengl32!wglSwapBuffers` is
  hooked (`api` = OpenGL, output size from the DC's window, no `PRESENT_ARGS`), installed at init or lazily
  with the `LoadLibrary` detour waking the watchdog for `opengl32.dll`; `hook-harness --opengl
  --hold-presenting N` exercises it through gdi32's `SwapBuffers`. ctests `fl_unhook_inline`, `fl_guard`
  `[opengl]` / `[log]`. D3D9 is struck from the P1 line per `20_OPEN_QUESTIONS` §Scope decisions.
- **P1 item 3 — the Vulkan layer intercepts `vkQueuePresentKHR` (2026-09-06; `20_OPEN_QUESTIONS` §S2 ✅).**
  In a process both gates admitted, the layer creates the same ring the Overlay would (`fl_shm_host.h`,
  now the one home of the DACL / name / handshake for both capture sides; one ring per process, first
  creator owns it) at the first `vkCreateDevice`, writes one record per present (`api` = Vulkan, output
  size from `vkCreateSwapchainKHR`'s `imageExtent`, no `PRESENT_ARGS` because the call has none), and
  forwards everything — including when the ring is not its own, which the first fixture run proved
  necessary. §S2 part three lands with it: `unhookRequested` and the 07_IPC tick deadline both stop the
  layer on the present path (passthrough with the reason on the mapping), proven by a fake loader chain
  (ctest `fl_vklayer`) and a 1.5 s test-only flavour of the DLL (`fl_vklayer_supervision`). The harness
  gains `--vulkan --hold-presenting N` (real loader via `LoadLibraryW`, hidden window, exit 77 = cannot
  run here) and ctest `fl_vklayer_real` drives it through the real loader with `VK_ADD_IMPLICIT_LAYER_PATH`
  — measured honoured by loader 1.4.357 with no registry — skipping on CI. Launch mode classifies a
  Vulkan-only target as the layer's (`TargetIsVulkanLayered` = 26: full guard, nothing injected, attach to
  the layer's ring) and the CaptureHost hands the launched process the layer's environment
  (`VkLayerLaunchEnvironment`: manifest, enable variable, enable-list line for the session).
- **P1 item 2 — launch mode, built as "inject late" (2026-09-06; `20_OPEN_QUESTIONS` §S1 ◐, §S13(c) ✅).**
  The guard gains `FlGuardedInjectWhenReady(pid, dll, timeoutMs)`: it polls the launched target through
  the module seam — matching no blocklist — until a presentation runtime is mapped, then runs every
  check and injects exactly as `FlGuardedInject` does; a target that exits first or maps no runtime in
  the budget answers the new `LaunchTargetExited` / `LaunchNoPresentationRuntime` with nothing injected
  (ctest `fl_guard` `[launch]`, five cases incl. the real seam against `hook-harness`). The CaptureHost
  gains `launch --exe <path> [--args "…"] [--seconds n]` (`ProcessLauncher`: handle held from birth,
  `FRAMELEDGER_ENABLE_VK_LAYER=1`; the host never terminates what it launched), routed through the same
  gate by `HookRequest.WaitForPresentationRuntimeMs`, with `IAntiCheatGuard.GuardedInjectWhenReadyAsync`
  as the port's fourth method. The Overlay publishes `FlWriterState.dxgiPresentsBeforeHook` @60 — DXGI's
  count of presents before the first hooked one, the early-init cost §S1 deferred on — from the last
  reserved word (no layout bump; the writer state now has no slack), and the report prints it beside the
  measured wait. `04_CAPTURE` §Launch mode carries the shape and why it is not `CREATE_SUSPENDED`.
- **P1 item 1 — the `LoadLibrary` detour, both jobs (2026-09-06).** The Overlay patches
  `kernelbase.dll!LoadLibraryExW` after its present hooks (`17_HOOK_ENGINE` §DLL entry step 3): a module
  the hook inventory or runtime census names **wakes the watchdog** through an auto-reset event, so the
  lazy installers run within milliseconds of the module instead of on the next 1 Hz tick (94 ms measured
  against a 500 ms bound); a module matching the compiled anti-cheat floor's MODULE families **stops the
  Overlay from inside** with the new `FL_STATUS_STOPPED_BLOCKLISTED`, on the next present or watchdog
  iteration — `20_OPEN_QUESTIONS` §S6 ✅, `19_SAFETY` §During a session's in-process half (exact names
  only; fragment and signer stay the host's 30 s scan, so the Disclaimer's window is unchanged). The
  detour installs nothing inline (§H2), calls the original first, and skips data-file / resource loads.
  Two `FlWriterState` words from `reserved[]` (`loaderSignals` @56, `earlyStopFamily` @58; no layout
  bump), `hook-harness --load-after-ms N PATH`, two real-Overlay ctest cases (`[loader]`, `[S6]`), and
  the CaptureHost's `loader detour:` / `EARLY STOP` report lines, `SessionEndReason.WriterStoppedBlocklisted`
  (exit 5) with the family named from the staged rules file.
- **P0 closed — the closing sweep (owner: "theo bảng", 2026-09-06).** Exit criterion 1 is met with
  the quality preset amended to an honest `N/A` by measurement; criterion 2 is met with §S24 counting
  zero S-items against it. Three items closed by a gate each: **§S19(c)** — `rules-validate` gains a
  third canary proving the schema rejects `action: "allow"` and `signerField: "CN"`, so the policy is
  code and the data cannot say otherwise; **§S23-6** — the accuracy blocks are one text,
  `legal/ACCURACY.md`, embedded verbatim in `README.md` and `legal/DISCLAIMER.md` §4 and gated by
  `tools/accuracy-check.ps1` (self-tested both directions), and rewritten to 2026-09-06 truth (the old
  §4 still said frame generation and ray tracing had no hook); **§S29(d)** — `ci.yml` prints a Vulkan
  runtime census, and the blast-radius script runs as a CI step the day a runner has a loader, deferred
  on that measurement until then. Six items deferred with their rationale written and their phase named:
  S6 and S2 part three (P1), S23-3 (P3), S4 signing, S20 feed half and S14's store-id half (P4).
  `15_ROADMAP` and `HANDOFF` now say P1 starts.

- **The guard's signer half — the other conjunct of "name fragment AND not signed by a known vendor" —
  and the nine integration cases in the merge gate (§S19(b) row G1; owner's bound decision 2026-09-06).**
  Since 2026-08-05 the guard refused FrameLedger's own test host on CI because a .NET host loads
  `System.Security.Cryptography.ProtectedData.dll` and `protect` is a heuristic fragment, and CI skipped
  the drain, pause, drop and capture-host end-to-end cases. `fl-probe-signer`'s three legs (dev box, CI,
  the owner's adapters-disabled run) all read the same: the embedded signature verifies offline with
  `O=Microsoft Corporation`, ~2–4 ms, no verdict changing without a network. So `Sources` gained
  `ModuleSignerOrganisation` — `WinVerifyTrust` under `WTD_REVOKE_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL`,
  then the certificate's `O=` — and the module sink trusts a fragment-matching module only when that
  organisation is on the rules file's `trustedSigners` AND on the list compiled into the binary
  (`IsCompiledTrustedSigner`: the data may narrow, never widen). A trusted module keeps the scan
  looking; the target is judged like its ancestors; no signature, an invalid one, an unreadable `O=`, a
  null path or seam, a failing seam, one list without the other — all refuse. The catalog half stays
  deferred with its rationale. `hook-harness --load` proves both directions against the real seams
  (the NuGet blocker allowed; our own unsigned Overlay under a `protect` name refused, by name); nine
  fake-seam cases pin the decision. `ci.yml` runs `./build.ps1 check` with no switches; the switch stays
  in `build.ps1`, loud. `19_SAFETY`, §S19(b), §S29(a), `13_CI_CD`, `12_BUILD`, `HANDOFF` and the rules
  file's own comment carry it.

- **Rung 3 by elimination, and the executable file as the census's second witness (HANDOFF 7b / §H11,
  owner's choice 2026-09-06).** `frame generation: Active (technology not identified)` now prints every
  exclusion the session can make beside it — not DLSS-G when the NVIDIA driver's FG word reports no feature
  created and no HUD-less / UI tag was sent (or "not excluded" when the driver did not answer), no FSR
  frame-generation dispatch reached a hooked module, the frame-generation runtime modules the census names
  or that none is loaded, and the SDK strings the executable carries — never a vendor named as measured, and
  it says a frame generator compiled into the executable would read the same. The capture host scans the
  executable FILE on disk once after the session (`ExecutableMarkerScan`: `ffxFsr3`, `ffxFrameInterpolation`,
  `xefgSwapChain` and the non-FG strings for the reader; chunked with overlap, hits capped) and prints
  `executable markers:`; when a frame-generation-capable string is in the file and no module was loaded, the
  Presented qualifier reads *MAY include generated frames* instead of *cannot* — the sentence that was wrong
  in the dangerous direction on Rune Factory and Wukong. A marker is never identity. Intel's licence was
  re-checked the same day and is unchanged; XeSS-FG runs on non-Intel GPUs, so an XeFG session measures this.

- **The super-resolution identity from the NVIDIA driver, where no hook can see it — the driver-reported
  rung (owner decision 2026-09-06).** Three NGX-direct titles printed `upscaler: N/A` on every capture
  while the driver's per-process NGX word (`NvAPI_NGX_GetNGXOverrideState`, R570+, MIT NVAPI) reads
  `CREATED | EVALUATE` for them — measured 2026-09-06 without an NVIDIA-app override. The capture host now
  spawns `fl-probe-nvapi --ngx-state <pid>` beside every module snapshot (the probe gained one machine line,
  `NGXSTATE status=… sr=0x… …`, and is staged beside the host), merges the readings (`NgxDriverState`: the
  last answered word is the state, a change between readings is printed), prints `NVIDIA driver NGX state:`
  in the runtime block, and the `upscaler:` line reads `Dlss (driver-reported: … not counted by this hook)`
  when no hook named one. Identity only, each limit measured: no quality or preset (override fields), no
  ratio (`scalingRatio` is 0 even under an override), no frame-generation multiplier (`FG_MODE` /
  `FG_MULTI_FRAME` stay clear at ×4). **The FG word follows DLSS-G too, corrected on the first run's own
  readings:** `CREATED | EVALUATE` on every ×4 session, changing between readings as the feature came up, so
  it is printed beside `frame generation:` as agreement or a printed disagreement, and promoted to
  `DlssG (driver-reported …)` only where the count is active and no tag named the technology — the count is
  never the driver's. Owner's run (§H5 rows N1–N4): the negative held (`CREATED` clear with upscaling off),
  four NGX-direct titles now print `Dlss (driver-reported …)`, two hooked titles print agreement, and an
  FSR + DLSS-G session read `SR 0x205` (created, not evaluated) and correctly claimed nothing. Beside a hook's `Dlss` the driver's word prints as agreement or as a
  disagreement printed rather than resolved; beside an `N/A` with the bit clear it prints the driver's
  negative. `20_OPEN_QUESTIONS` §H5 pre-commits rows N1–N4, and N3 — DLSS switched off — withdraws the rung
  if the bit stays set. `03_METRICS` §Upscaling, `05_DETECTION`, `18_GPU_VENDOR_APIS` carry it.

### Changed

- **P3 PR-0 — the P2 sweep: what PR-G's closing sweep left stale, corrected in place (2026-09-13).**
  Docs and tools only, no `src/`. `tools/coverage-gate.ps1` said Domain and Application were "empty
  today" and the metric calculators "do not exist yet" four days after both had armed it; `13_CI_CD`
  counted nine `Category=Integration` cases (20 across six classes) and claimed the signer probe ran
  after the gate with `if: always()` "a one-line change nobody has made" — the step has carried it since
  #137, before the failure the bullet describes, so *why* the probe was not read on 2026-09-10 is now
  recorded as unmeasured rather than as a workflow defect; `09_I18N` said `ConsentProvenance` had two
  members (three since PR-F); `17_HOOK_ENGINE` cited `20_OPEN_QUESTIONS` §H9, answered and removed in the
  P0 spike; `ci.yml`'s census comment still promised "the next PR wires the script" after the census had
  read `vulkaninfo.exe` absent twice. HANDOFF: P2's START HERE struck, **§P3 is the head** with the
  slice table (0, 1, 1b, 2, 2b, 3–9) and decisions D11–D17; D1/D5/D6/D10 labelled where they were
  cited by number and defined unlabelled, and D2/D3 recorded as never assigned. `15_ROADMAP` §P3 takes
  the pipe, i18n, the Legal Gate, Settings, Logs and the tray from §P4 (owner, 2026-09-13). §G's
  threading-model row struck with its pointer; the settings-registry and pipe-security rows carry the
  decisions that close them. What P2 still owes is unchanged and is measurement: `spike-notes` §14's
  four slots, all empty, all the owner's.

- **The FG `N/A` and the upscaler `N/A` carry their witnesses.** Hell Is Us at XeSS + XeSS-FG (2026-09-06):
  no Streamline token on that plugin at XeSS, so nothing counted, and the report printed a bare *N/A (a hook
  ran …)* while the driver's FG word, the tags, the ffx census, the module census and the executable's own
  strings all had something to say. The N/A now prints the same exclusion clauses as rung 3 (*what this
  session can still say: not DLSS-G (…); …; the frame-generation runtime(s) loaded: …; the executable itself
  carries xefgSwapChain*), and the upscaler N/A says *libxess.dll is loaded, and XeSS cannot be hooked or
  declared under Intel's licence … by policy, not by ignorance* — HANDOFF 7c item 4's string change. Rune
  Factory at FSR + FSR FG printed the pre-committed 7b line verbatim on the same run.

- **A counted session whose factor was refused no longer reads "no evaluation was observed".** DL:TB at FSR
  upscaling + DLSS Frame Generation (2026-09-06): 4,415 tokens, 8,174 DXGI-counted presents, `DlssG` from
  the tags, factor refused because bucket 8 of 8 read 1.96 against 2.85 — and the Presented qualifier said
  a runtime was loaded and nothing was seen. It now says frame generation was counted and its factor
  refused, that DXGI counted presents this hook never saw so the Displayed rate is above the printed
  number, and that the number is neither Native nor Displayed.

- **The withheld `none` is narrowed to sessions where DXGI's counter was not read (§H5 Leg 0 landed).**
  The owner's morning run on the DXGI-counted build printed the rule-6 trio on Dying Light: The Beast
  (`Native 70.52 -> Displayed 282.08 (x4 FG)`, 12,387 presents DXGI-counted, `DlssG`) and then ran the
  title twice with frame generation OFF: census unchanged (`sl.dlss_g.dll` is a startup-time load),
  presents = tokens, `unseen=0` over thousands of samples, and the report withheld `none` exactly as the
  gate's cost paragraph pre-committed. The counter is the discriminator the gate lacked — on that shape
  the pacer's presents are DXGI presents on the hooked chain — so a counted `none` beside a READ counter
  with zero unseen now prints as `none`, with *"DXGI's own present counter agrees: 0 unseen over N hooked
  present(s)"* on the line; it is withheld only when the counter was not read, with the reason saying so.
  A writer state that counted unseen presents the records do not carry is refused as a contradiction.
  On every other title the counter is printed beside `none` as a second witness. CLAUDE.md rule 6 carries
  the amendment; three cases changed sides and four are new.

### Added

- **Displayed FPS counted by DXGI where the hook cannot count it — the 2.8.0 pacer's presents.** The
  owner's run on the DXGI counter landed `20_OPEN_QUESTIONS` §H5 row P1-DXGI twice: Dying Light: The
  Beast at DLSS Frame Generation ×4 on Streamline 2.8.0 reads 2.90 and 2.95 presents per hooked present
  that DXGI counted on the same chain and the inline patches never saw, while Streamline 2.7.3 (Hell Is
  Us, ×3.86) and 2.10.3 (the Onimusha: Way of the Sword demo, ×3.67) read 0.00 — their generated frames
  come through the patched bodies. So the record now carries `dxgiUnseen` (@52, from the reserved word,
  under `FL_MEASURED_DXGI_PRESENTS`, no layout bump; mirror and `fl-layout-dump` follow), and the
  consumer's window counts `Displayed = hooked + Σ dxgiUnseen` — in the rule-6 trio, in `presents/batch`
  and in the uniformity buckets, never in a frame-time distribution — printing *"Displayed is
  DXGI-COUNTED"* on its own line whenever the sum is above zero, because that number is a count DXGI
  made rather than this hook. `none` withdraws on that title by count; a saturated byte is refused like
  a saturated `fgEvaluations`; a byte under a clear bit is an honesty violation. A present the hook SAW and
  declined — a paused session — is not an unseen present: every declined present bumps an epoch that
  invalidates the chain's last reading, which the paused-session integration case caught (the first record
  after a resume claimed the whole pause, 0x55, before the guard). On every other title
  the window is byte-identical (five cases pin it; the injected `[fg]` case checks the byte is claimed
  and zero on the harness). `03_METRICS` §Displayed may be DXGI-COUNTED carries the rule.

- **DXGI's own present counter against the hook's, in the writer state and the report.** On the one shape
  where `none` is withheld (Streamline ≥ 2.8 with `sl.dlss_g.dll` loaded, Dying Light: The Beast), the
  open question was whether the closed pacer's generated presents reach DXGI through a body the inline
  patches miss or are displayed below DXGI. The Overlay now reads `IDXGISwapChain::GetLastPresentCount`
  on every hooked present of the chain it just saw (rule 4: the same object `GetDesc` is already read
  from) and publishes `dxgiPresentSamples` and `dxgiPresentsUnseen` (`FlWriterState @48/@52`, from
  `reserved[]`, no layout bump; mirror, `fl-layout-dump` and the offset asserts updated). The report prints
  `DXGI present counter: unseen=M over samples=N (r unseen per hooked present)` with the reading, and the
  withheld qualifier says which side of DXGI the generated presents fall on. `20_OPEN_QUESTIONS` §H5
  carries the rows written before the run: r ≈ 3 at ×4 means the generated presents ARE DXGI presents
  and the next PR labels a DXGI-counted Displayed rate; r ≈ 0 means below DXGI and only the kernel and
  vendor reads remain. The harness's presents are the negative control (`fl_guard` `[fg]`: zero unseen);
  the positive cannot be fixtured. `hook-harness --probe-frames` now prints the call's cost.

- **A stall diagnostic in the report, placed against FrameLedger's own touches on the target.** The
  owner saw Cyberpunk 2077 fall to 1 FPS for about a second under capture (2026-09-05) and the report
  could say nothing about whose it was. The capture host now records the QPC moment of every guard scan
  and module snapshot (`CaptureResult.TouchQpc`, the records' own clock), and `StallReport` prints the
  three longest present-to-present intervals with their session time and the nearest host touch: a
  touch inside or within 1 s names FrameLedger a SUSPECT and asks for a longer run on the 30 s cadence;
  a far one says *not ours*. It counts intervals at or over 100 ms and calls itself a diagnostic — it is
  not `03_METRICS`' stutter, which P2's calculators own. Four cases pin the placement.

- **DLSS Frame Generation is NAMED, from the tags the title already sends.** On every NVIDIA title measured
  the count was right and the technology unnamed — `Active (technology not identified)` on Cyberpunk at
  MFG ×3 and on the three UE titles at ×4 — because `kFeatureDLSS_G` is never evaluated through the export
  the identity hook owns. Streamline's DLSS-G programming guide §5.0 requires a title running frame
  generation to tag the HUD-less colour and UI buffers every frame through `slSetTag` /
  `slSetTagForFrame` / `slEvaluateFeature`'s inputs — the three tag routes this build already hooks and
  read one tag type from. The detours now record every tag's TYPE (rule 4: the same argument), a
  HUD-less or UI tag drained by a present marks it `FL_FG_DLSS_G`, and `FlWriterState.slTagCensus` (took
  `reserved[0]`, no layout bump) says which types arrived on which route for the session; the report
  prints it as `Streamline tag census: global=[…] frame=[…] local=[…]`. **Identity only:** the consumer's
  rule is the count decides `none`, identity decides the name — an active count beside the mark prints
  `frame generation: DlssG`; a counted 1.0 beside it prints `none` with the inputs noted; the withheld
  shape (§H5) keeps its N/A and says the title is feeding frame generation. Two injected cases send the
  DLSS-G list through each global export with no `kFeatureDLSS_G` evaluation and assert the mark on every
  record and the census on the right route; the existing tag cases assert its absence. `slDLSSGGetState`
  — the API the guide names for the presented count — is refused and the reason recorded in `03_METRICS`:
  the call resets the plugin's counter a title may read for its own FPS display, and the header marks it
  not thread safe.

### Changed

- **The tag route is measured on real titles** (docs): the owner's evening run on #121 read `frame generation:
  DlssG` on Hell Is Us, Expedition 33, Wukong, Rune Factory and Cyberpunk — every Streamline title tags the
  DLSS-G inputs through a route this build hooks — and Dying Light: The Beast's census shows the title tags its
  scaling input through `slSetTagForFrame` with no size, which closes HANDOFF 7a's last question without a
  layout bump. `spike-notes` §9 carries the table; §H5 records the pre-commitment as landed.

### Fixed

- **`render -> output` prints under frame generation.** `UpscaleExtent` keyed its window on the
  params bit, which the writer publishes only on the present that drained a dispatch or tag — one
  in N under frame generation — so `ClaimedSuffixStart`'s clean-suffix rule found no window and every
  FG-on capture since 2026-08-16 read `N/A` two lines under its own raw `render WxH` block
  (Cyberpunk at FSR 3 + FG, 2026-09-05, was the one that made it visible; the earlier "not computed
  yet" wording had hidden it). The window is now the identity suffix, which is per-record once the
  family is live, and every record inside it carrying both sizes counts. Two cases pin the FG shape
  and the refused intermittent-identity shape; the Cyberpunk FSR 3 consumer case now asserts the line.

### Added

- **`fl-probe-nvapi --ngx-state <pid>`: what the NVIDIA driver says about one process's DLSS
  features.** The owner's eleven-capture run on 2026-09-05 (`spike-notes` §9) left one NVIDIA gap in
  one sentence — the count is right on every title and the identity is what this build cannot say:
  DLSS on the four NGX-direct UE titles, DLSS-G everywhere, and the hidden presents on Streamline
  2.8. The NGX SDK is licence-blocked; `NvAPI_NGX_GetNGXOverrideState` (NVAPI, MIT, vendored, R570+)
  takes a process id and returns the driver's own feedback bits for SR / RR / FG plus the scaling
  ratio, quality mode and frame-generation count, out of process. The probe prints the raw words and
  every decoded bit so the owner's run against a running title measures whether the bits are
  populated without an NVIDIA-app override, before any report line is built on it. §H5 carries the
  decision that answer opens (a *driver-reported* rung in `03_METRICS`, the owner's call).

- **The FSR 3.0 host route: a fifth AMD target, `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale`.**
  Cyberpunk 2077 at FSR 3 upscales through the FSR 3.0 **host** DLL's named export while its frame
  generation goes through the 1.1.x monolith's `ffxDispatch`, so the leaf-only build printed
  `upscaler: N/A` beside `FsrFg` (§H11, 2026-09-04). The host is hooked at its own export with its own
  trampoline and latch, keyed by module in the installer, under the same family and publish point as
  the four ffx-api rows (`PublishFfxFamilyIfWhole` waits for a loaded host as it waits for a loaded
  leaf); its UPSCALE feeds the same drain word — identity `FSR3` as a fact, `renderSize` as the
  extent, the count when neither Streamline nor a PREPARE has spoken. Rule 4: `renderSize` of the
  descriptor the title passed and nothing else; the offset is pinned to a literal (1256).
  `ffxFsr3DispatchFrameGeneration` is deliberately not a row, with the reversal condition
  pre-committed in §H11. **The headers are vendored from tag `fsr3-v3.0.4`, not the `v1.1.4` three
  documents named**: the shipped module's twelve `ffxFsr3*` exports match 3.0.4 exactly, 1.1.4
  declares fifteen and appends fields after `renderSize`, and at 3.0.4 `ffx_types.h` reaches no
  `<mutex>` at all (ten files, no `ffx_message.h`; `src/native/third_party/fidelityfx-fsr3/`, root
  `LICENSE.txt` MIT verbatim, `license-check.ps1` §2e per file, blob shas checked against upstream).
  The two FidelityFX trees share one struct name, so the 3.0.4 header is included inside a
  namespace. Fixtures: the `ffx_fsr3_x64.dll` stub (Pass C's second positive control), harness
  topologies `fsr3host` / `fsr3host+mono`, an `[ffx]` injected case at K = 1 (the row's double-count
  control) and K ∈ {1, 4} beside the monolith, and a consumer case for Cyberpunk's shape. Owed: the
  owner's acceptance run, rows B1–B4 in §H11.
- **The report names the loaded vendor runtimes with their FILE versions, and a counted `none` is
  WITHHELD on the one shape measured to be wrong.** Dying Light: The Beast (Streamline 2.8.0) read
  `presents = tokens` five times with DLSS Frame Generation ON, and the owner's reading on 2026-09-05
  settled which way to read it: the title runs at DLSS ×4, the frames this hook records are the
  native ones, and the generated presents never reach the `Present` bodies the Overlay patches
  (`20_OPEN_QUESTIONS` §H5 case 3, realised). The census could not have said so — it records module
  NAMES, and Cyberpunk 2077 on 2.7.1 reads ×3.99 through the same hook — so the capture host now takes
  an out-of-process module snapshot beside every guard scan (`Process.Modules` + the file's version
  resource, the same documented module list the guard already enumerates; no game memory, no layout
  change, nothing on the present path) and prints one `module:` line per census-named module with
  version and path. `MeasuredFacts` withholds `none` when `sl.dlss_g.dll` — the Streamline plugin that
  would be doing the presenting, NOT `nvngx_dlssg.dll`, which every validated `none` also has loaded —
  is in the census beside an interposer at or above 2.8.0 (or one whose version could not be read), and
  the report falls through to `Presented FPS` with a qualifier that says the number counts APPLICATION
  frames and that the Displayed rate is unknown. Every validated `none` (Cyberpunk off / ×3 / ×4,
  Expedition 33 and Lies of P with frame generation off) is byte-identical, and eight cases pin that.
  The gate is interim: §H5 pre-commits the owner's session (DL:TB FG off / FG ×4, Cyberpunk MFG ×4,
  PresentMon elevated beside each) and the row on which it is withdrawn.

- **A whole-resource tag now yields the size the tagged `sl::Resource` declares.** Streamline
  documents a zero `extent` as "use the entire resource"; a title tagging that way stated the
  render size nowhere the Overlay read, and Dying Light: The Beast (SL 2.8.0) still published no
  extent on the `slSetTagForFrame` build with both tag rows live. `TagSize` in `fl_sl_inputs.h`
  reads the extent first and, when it is zero, the Resource's own `width/height` — GUID-checked,
  from the argument the hooked API received, on the local walk and both global-tag detours alike,
  so the three routes cannot disagree about what a tag says. A Resource declaring no size is
  still the honest unknown. Unit cases cover the shape, precedence of a non-zero extent over the
  Resource, a Resource that fails the GUID check and a half-declared size; the harness tags in
  that shape under `--sl-tag-whole-resource` and an injected case asserts the exact size arrives
  on every record through the 2.8 export.

