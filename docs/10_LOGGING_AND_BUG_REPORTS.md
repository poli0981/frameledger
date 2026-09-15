# 10 — Logging, diagnostics, bug reports

## Serilog configuration

- Sinks: rolling file per process — `logs/ui-.log`, `logs/agent-.log` (`rollingInterval: Day`, `retainedFileCountLimit: 7`, `fileSizeLimitBytes: 10 MB`, `rollOnFileSizeLimit: true`). Console sink in DEBUG builds. *(`ui-.log` is written since 2026-09-13, P3 PR-2, with `Process=ui` enriched and every unhandled path — Dispatcher, AppDomain, unobserved task — logged as Fatal/Error; ~~the crash dialog and minidump of §Crash handling are still P4's~~ — built 2026-09-15, P4 PR-9, §Crash handling's note.)*
- Minimum level `Information` (`Debug` toggle in Settings → applies live via `LoggingLevelSwitch`).
- Enrichers: process name, version, `SessionGuid` and `GamePid` scoped properties during capture (`LogContext.PushProperty`).
- Template: `[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}`.
- **Never log:** full user paths outside `%LOCALAPPDATA%\FrameLedger` (redact to `<user>`), machine name, any exe arguments of games. ~~A `RedactingEnricher` enforces the path rule.~~ *(No such enricher was ever written: found 2026-09-15, checking `legal/PRIVACY_POLICY.md` §3's "logs are redacted (user directory paths removed) before bundling" against the bundle, which copied the logs verbatim. An enricher could not have kept the rule anyway: exception text and the Overlay's native log never pass through one, and the data directory itself sits under the user's profile, so every run logs a user name. The rule is enforced where a log leaves the machine instead: `App/Services/LogRedactor` over every log copy in the bug bundle, §Bug report flow step 2. The files in `logs\` keep their full paths; they leave the PC only if the user copies them.)*
- Capture hot path logs nothing per-frame; counters summarized at finalize.

## Crash handling (the app's own crashes)

- Hook `AppDomain.CurrentDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` (both processes).
- On fatal: Serilog `Fatal` with full exception → write minidump via `MiniDumpWriteDump` (CsWin32, `MiniDumpWithIndirectlyReferencedMemory | WithThreadInfo`) to `crashdumps/` (keep last 5) → UI shows crash dialog offering the bug-report flow → exit code 1.
- Agent crash mid-session → `.partial` recovery path (04_CAPTURE) on next start finalizes an `interrupted` session.

> **Built 2026-09-15 (P4 PR-9).** `Infrastructure/Diagnostics/CrashDumpWriter` writes
> `crashdumps\<ui|agent>-<yyyyMMdd-HHmmss, UTC>-<pid>.dmp` through CsWin32's `MiniDumpWriteDump` with exactly the two
> flags above, keeps the five newest `*.dmp` of either process after each write, and never throws: a failure is a null
> path and a Warning line, and the partial file is removed.
>
> **The App.** A `DispatcherUnhandledException` is marked handled, so the process lives long enough to ask; then
> `Services/CrashReporter` runs Fatal → dump → `Services/CrashDialog` → on Yes, the shell window revealed (a window
> hidden in the tray would hold the report's dialogs where nobody sees them) and `BugReportFlow` → the host's
> `StopApplication`, and `RunAsync` exits with code 1 after the normal teardown (with no host yet, `Shutdown(1)`
> directly). The dialog is a **Win32 `MessageBox`**, not a WPF UI one: the shell's dialog host, theme and dispatcher are
> what a crash cannot vouch for. It says where the dump is (or that none could be written), that the capture agent keeps
> running, and that the report shows every file and includes the dump only when ticked. **One report per process**: a
> further exception while the dialog is up shows nothing; the first of those is logged whole and the rest are counted,
> because a fault in a layout pass repeats on every pass the dialog's own message loop runs and would flood
> `ui-*.log`. A failed dump still asks; a failed report is an Error line, not a second crash. An
> `AppDomain.UnhandledException` (any other thread) is Fatal + dump only: the runtime is already ending the process and
> no dialog can be relied on to appear, so the next bug report offers that dump instead. An unobserved task stays an
> Error line. A startup failure `RunAsync` catches itself (the ledger will not open, the host will not start) is not an
> unhandled exception: it is still a Fatal line and exit code 1 with no dialog.
>
> **The Agent** (no window, so no dialog): the same Fatal + dump from `AppDomain.UnhandledException` and from `Main`'s
> own catch, once per process whichever sees it first, then the exception stays unhandled, so the exit is the runtime's
> and never the Agent's usage code 1. The catch exists because an exception escaping `Main` runs `Main`'s `finally`
> first, which closes the logger; the AppDomain handler alone would dump with no Fatal line.
>
> Tests: `CrashDumpWriterTests` (a real dump of the test process starts `MDMP` and carries the UTC time in its name; the
> prune keeps five and touches no other file; an unusable directory is null and one line), `CrashReporterTests` (the
> order, no report on No, three further exceptions show nothing and are counted, a failed dump still asks, a failed
> report does not throw, the dialog's text with and without a dump).

## In-app log viewer (Logs screen)

Tails the active files (shared read), level filter, text search, pause autoscroll, "Open logs folder", "Export bug bundle". Reads at most last 2 MB per file into the view.

> **Built 2026-09-14 (P3 PR-8a)** — `App/Services/LogTail` + `LogsViewModel`; `08_UI` §Logs carries the built
> note. One file at a time (the newest of the chosen source), not both; the 2 MB cap is `LogTail.MaxBytes` and
> the read is `FileShare.ReadWrite | Delete` so Serilog's open handle and its daily roll never block it.

## Bug report flow (FR-13)

1. Entry points: Help → Report a bug, crash dialog, Logs screen button.
2. **Bundle builder** creates `FrameLedger-bugreport-YYYYMMDD-HHmm.zip` in a user-chosen location:
   - `logs/` (last 7 days, redacted copies)
   - `sysinfo.json` (app + agent + **overlay build id**, OS build, CPU/GPU name, driver version, telemetry source, Vulkan layer state, PawnIO present?, elevation state, locale)
   - `overlay-<pid>-*.log` for the last hooked sessions, plus hook fault details and the ring's dropped/fault counters — these are what make injection bugs diagnosable at all. *(The file exists since 2026-09-06 — `17_HOOK_ENGINE` §Native logging: written at init, on the Agent's request at session end, and on the stop; `FAULT` lines carry the exception code and the hook's name, `UNHOOK_*` lines say per patch whether it was restored or left to a later hooker.)*
   - `settings.json` (sanitized — no paths)
   - optional checkbox: last session metadata + aggregates JSON (never raw blobs by default)
   - crash dumps included only when the user ticks the checkbox (size warning shown)

   > **Step 2 built 2026-09-14 (P3 PR-8a), narrower than this list; steps 1 (Help menu, crash dialog), 3, 4 and 5
   > are P4's.** `App/Services/BugBundleBuilder` writes the zip where the Logs page's save dialog says
   > (`FrameLedger-bugreport-YYYYMMDD-HHmm.zip`): `logs/` = every `ui-*.log` and `agent-*.log` modified in the last
   > seven days, ~~**copied verbatim, not redacted** — the files carry no paths beyond the data directory and no
   > game data, and a redaction pass that nobody has specified would be a claim~~ **redacted since 2026-09-15, note
   > below** (the data directory is under the user's profile, so the claim was false on every machine); `overlay/` = the five newest
   > `overlay-<pid>-*.log` (the ring's counters and the `FAULT`/`UNHOOK_*` lines are in those files, not
   > separately); `sysinfo.json` = app version, and from the Agent's `HelloAck` when connected its version, the
   > overlay build id, telemetry source, Vulkan layer state, elevation, CPU-temperature availability (the
   > PawnIO question) and the disclosure version, plus OS version, bitness, locale and the write time — **no
   > CPU/GPU name or driver version**, which live in the ledger's hardware snapshots and are the session
   > metadata option this list already has; `settings.json` = every registry key's effective value (there is no
   > path-valued key). No session JSON ~~and no dumps~~ (PR-9 below). ~~The bundle is written and nothing else happens: no preview
   > dialog yet, no browser, no clipboard.~~ Tests: `BugBundleBuilderTests` (the seven-day cut, the overlay cap, a
   > `game-crash.log` beside ours is not shipped, both JSON files), `LogsViewModelTests` (the cancelled save writes
   > nothing).
   >
   > **Steps 1 (the Help menu), 3 and 4 built 2026-09-14 (P4 PR-3)** as `App/Services/BugReportFlow`, the one flow
   > behind Help ▸ Report a bug… and the Logs page's button: step 2's zip → step 3's `ContentDialog`
   > (`BugReportPreviewPrompt` / `Dialogs/BugReportPreviewContent`) listing every entry `WriteAsync` returned, with
   > "Show the zip in Explorer" and the drag instruction → step 4 as the dialog's two buttons: **Open GitHub issue**
   > opens `issues/new?template=bug_report.yml&title=[Bug]%20&labels=bug&app-version=…&os=…` — the form's field
   > ids are **hyphenated** (`app-version`, `os`), which is what GitHub prefills by, not the underscores step 4
   > above spelled; the OS is the form's own spelling (`Windows 11 26100.2314`, `IssueLink.OsText`) — and **Copy
   > summary as Markdown** puts `sysinfo.json`'s twelve keys on the clipboard as a table. Both behind ports
   > (`IBugReportPreview`, `IUrlOpener`, `IClipboard`) so `BugReportFlowTests` pins the exact URL without a browser.
   > ~~Still open from this list: the **crash dialog and the minidump** (§Crash handling, step 1's third entry point)
   > and the optional session-metadata / dump checkboxes of step 2.~~ PR-9 below, and the session-metadata checkbox after it (2026-09-15).
   >
   > **The log copies are redacted since 2026-09-15.** `App/Services/LogRedactor` is what the list's "redacted copies"
   > and `legal/PRIVACY_POLICY.md` §3 promised: in every `logs/` and `overlay/` entry the name in each
   > `X:\Users\<name>` path becomes `<user>`, and so does the last segment of this user's profile directory when it
   > lives elsewhere (`D:\Profiles\<name>`). Every spelling a log carries is covered: backslashes, the doubled
   > backslashes of Serilog's JSON properties, forward slashes, any case, a name with spaces, and the `?` the Overlay's
   > ANSI image path writes for a letter its code page lacks. The file is read as bytes through Latin-1, so a UTF-8
   > log and an ANSI one both come out byte for byte except the names. Over-redaction is the chosen failure:
   > `C:\Users\Public` reads `<user>` too. The measured need on the dev box: the profile path appeared 8 times in
   > `ui-*.log`, 7 in `agent-*.log` and in 175 overlay headers; no machine name or bare user name did. A crash dump is
   > memory and cannot be redacted, which its checkbox says. Tests: `LogRedactorTests` (fifteen cases, both
   > directions, UTF-8 and ANSI bytes), `BugBundleBuilderTests` (the copies carry no user name, the file on disk is
   > untouched).
   >
   > **The crash dump's checkbox and step 1's third entry point built 2026-09-15 (P4 PR-9).** When
   > `BugBundleBuilder.LatestCrashDump` finds a `*.dmp` written in the last seven days under `crashdumps\` (either
   > process's), the flow shows **Optional items** before the save dialog (`BugReportPreviewPrompt.AskCrashDumpAsync` →
   > `Dialogs/BugBundleOptionsContent`): one checkbox, **clear**, labelled with the dump's local time and its size in MB
   > (the size warning), and a line saying a dump is FrameLedger's own memory and can hold file paths, game names and
   > settings. Continue with the box clear writes the bundle without it; ticked, `crashdumps/<name>.dmp` goes in;
   > Cancel writes nothing. `WriteAsync` refuses a dump from anywhere but that directory, before the zip is created. It
   > is a dialog before the save rather than a box on step 3's preview because the preview lists a zip that is already
   > written. The crash dialog (§Crash handling) is step 1's third entry point. ~~Still open: the session-metadata
   > checkbox.~~ Built the same day, below. Tests: `BugBundleBuilderTests` (only when passed, only from the directory,
   > the newest of the week), `BugReportFlowTests` (no dump no question; left clear, ticked, cancelled),
   > `BugBundleOptionsViewModelTests`, `PagesLoadTests`.
   >
   > **The last session's checkbox built 2026-09-15**, which completes this list's optional items. The same **Optional
   > items** dialog carries a second box when the ledger holds a session: the newest one (`App/Services/LastSessionSummary`),
   > labelled with its game and local start time, with a line saying what the file holds. Ticked, the bundle gains
   > `session.json`: File ▸ Export's own JSON (`SessionExporter.Document` — metadata, aggregates, segments, hardware)
   > **without the user's notes and tags**, and passed through `LogRedactor` like a log because it names the executable.
   > No frame or sensor series, which is the list's "never raw blobs". The dialog now appears when either item exists and
   > not at all when neither does; the port's answer is `BugBundleOptions` (closed, or which boxes were ticked) where
   > PR-9's was `CrashDumpChoice`. Tests: `LastSessionSummaryTests` (the newest session, no notes or tags, nothing for an
   > empty ledger), `BugReportFlowTests` (left clear, and ticked with a profile path redacted in the zip),
   > `BugBundleOptionsViewModelTests` (every box clear, an absent item cannot be included), `PagesLoadTests`.
3. **Preview step:** the dialog lists every file included and lets the user open the zip before continuing. Nothing is ever sent automatically.
4. "Open GitHub issue" → launches browser to
   `https://github.com/poli0981/frameledger/issues/new?template=bug_report.yml&title=[Bug]%20&labels=bug&app_version=…&os=…`
   (short fields only — GitHub URLs cannot carry logs) with on-screen instruction: *"Drag the zip file into the issue description."* Clipboard fallback copies the environment summary as Markdown.
5. `bug_report.yml` issue form (in `.github/ISSUE_TEMPLATE/`) fields: description, steps, expected/actual, app version (prefilled), OS (prefilled), attachments note.

## Diagnostics extras

- `Tools → Agent status…` shows the `HelloAck` capability set (telemetry source, overlay build id, Vulkan layer — ~~tier availability~~, which meant "is the ETW fallback reachable" and has no meaning under a two-rung ladder) + last 20 Agent log lines.
- **Never include a game's own logs, saves, or config files** in a bug bundle, even when a crash looks game-related. We ship our logs only.
- `--diag` CLI flag on the App prints environment + capability report to stdout (support requests).

  > **Built 2026-09-14 (P3 PR-8a).** `App.OnStartup` sees `--diag` and runs `DiagAsync` instead of the host: no
  > window, no Agent, the ledger opened read-only for its schema version and the settings, then
  > `Services/DiagReport.Build` (app version, OS and bitness, runtime, locale, elevation, data and logs
  > directories, ledger schema or "could not open", whether `FrameLedger.Agent.exe` is beside the App, every
  > registry key's effective value) written to `logs/diag-<yyyyMMdd-HHmmss>.txt` **and** to stdout — the App is
  > a WinExe with no console of its own, so stdout carries the report only when the caller attached one or piped
  > it (`FrameLedger.exe --diag | more` works; a double-click leaves the file). Exit 0, or 1 with a Fatal line in
  > `ui-*.log`. The Agent's `--diag` stays on its `NotImplementedFlags` list — 10_LOGGING names the flag as the
  > App's. Tests: `DiagReportTests`.
