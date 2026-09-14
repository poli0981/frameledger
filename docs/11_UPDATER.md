# 11 — Updater

Velopack, feeding from GitHub Releases of `https://github.com/poli0981/frameledger`. Stable channel only in v1.

## Flow

- **Startup silent check** (if enabled): `UpdateManager.CheckForUpdatesAsync()` 5 s after UI idle. Offline/any failure → log `Information`, no dialog (NFR-10). Update found → non-blocking toast "Update vX.Y.Z available" → Downloads in background → "Restart to update" button (never auto-restart; the user may be mid-capture — if a capture is active, defer the prompt until session end).
- **Manual check** (Help → Check for updates): progress dialog; all failures produce the mapped dialog below.
- **Never apply an update while a game is hooked** (FR-12): the Overlay DLL on disk must not change under a running game, and a mid-session swap would invalidate the ring layout handshake. If a capture is active, defer the prompt and the apply until the session ends.
- Apply: `WaitExitThenApplyUpdates` on restart. Agent is stopped via `Shutdown` before applying and the scheduled task action path is re-validated after update (Velopack keeps a stable `current` path, but verify in P4 and re-register task if the action path changed).
- Release notes (GitHub release body, Markdown) rendered in the update dialog.

## Error mapping (FR-12)

| Condition | Dialog (resx key) | Extra behavior |
|---|---|---|
| HTTP 404 | `Update_Err404` — "No release feed found. The repository may have moved." | Link to releases page |
| HTTP 403 / 429 | `Update_Err_RateLimited` — "GitHub rate limit reached. Try again in {n} minutes." | Parse `Retry-After` / `X-RateLimit-Reset` when present; send `If-None-Match` ETags on checks to conserve the anonymous quota |
| HTTP 5xx | `Update_Err_Server` — "GitHub is having trouble. Try again later." | |
| Timeout / DNS / no network | `Update_Err_Offline` | Silent on auto-check |
| Package hash mismatch | `Update_Err_Corrupt` | Auto-retry once, then dialog |
| Unknown | `Update_Err_Unknown` + exception logged | "Report a bug" shortcut |

Detection-rules updates share this client, but the **`anticheat` block is fetched and applied on its own schedule regardless of the user's rules-update preference** (`05_DETECTION` FR-7.3) — a user who turned off rules updates must still receive new anti-cheat entries.

All update HTTP goes through one `GitHubHttpClient` (also used by rules updates) with: UA `FrameLedger/{version}`, 10 s timeout, ETag cache in `settings`, single retry with jitter for transient failures.

## Unsigned releases

Binaries are not code-signed (project policy). Consequences and mitigations, documented in README and the update dialog footer:
- SmartScreen warning on first run of a new version — expected; SHA-256 checksums (`SHA256SUMS.txt`) published with every release; CI prints them into release notes.
- Velopack delta packages reduce download size; full package fallback automatic.
- Never bypass or suppress OS warnings programmatically.

## Versioning

SemVer `MAJOR.MINOR.PATCH`. Tag `vX.Y.Z` triggers the release workflow (13_CI_CD). `MAJOR` bumps for DB schema or IPC protocol breaks; migrations must cover every released `MAJOR-1` version.

## Built 2026-09-14 (P4 PR-5) — what exists, and where it deviates from the above

`src/FrameLedger.App/Update/`. Velopack 1.2.0 (`Directory.Packages.props`, pinned since P0, referenced since
this PR) behind the `IUpdateClient` port — `VelopackUpdateClient` is the one implementation, `UpdateService` the
flow, and the tests run the flow over a fake client because nothing here may touch the network in a test.

- **Startup silent check:** `UpdateHostedService`, 5 s after the host starts (the "5 s after UI idle" above —
  measured from start, because the shell shows synchronously inside it), only for a copy the installer laid down
  (`UpdateManager.IsInstalled`; a `bin/` build logs one line and does nothing) and only while `update.auto_check`
  is on (Settings ▸ Updates, default on — the "if enabled" above). Every failure is `Information` in the log,
  never a dialog. A release found is downloaded in the background; the tray raises a balloon only while the
  window is off screen (`08_UI` §Notifications policy), and the shell's persistent InfoBar carries the download
  and the **Restart to update** button.
- **Manual check:** Help ▸ Check for updates → `UpdateService.CheckInteractivelyAsync` and the dialogs in
  `UpdatePrompts` (WPF-UI `MessageBox`): the offer with the release notes (the GitHub release body, shown as text —
  no Markdown renderer is shipped), the unsigned-release footer, Download / Later; "up to date"; "not an
  installed copy"; and the error table below. There is no progress *dialog*: progress is the banner's percent. A
  package that fails its checksum is downloaded once more before the Corrupt row is shown (the table's "auto-retry
  once").
- **FR-12 is the shape of the flow, not a check at the end.** A downloaded package is `Ready` only while the Agent
  reports no session (its `StatusAck` at connect, then `SessionStarted` / `SessionCompleted` on the pipe);
  otherwise it is `Deferred`, the button is disabled and the body says why, and it becomes `Ready` when the last
  session ends. The apply is refused again inside if a session started meanwhile. A recording-only (Tier 2)
  session that began before the App connected is covered by the status; one that begins afterwards raises no
  `SessionStarted` (that event is the ring's) and is not — a known gap, recorded rather than hidden: the Overlay
  is not on disk under such a session, so the rule's reason does not apply to it.
- **Apply:** `Shutdown` to the Agent over the pipe, the connection's relaunch held (`IAgentLink.SetLaunchHold`),
  the pipe watched until it drops (10 s; an Agent that does not stop leaves everything as it was and says so),
  then `WaitExitThenApplyUpdates(asset, silent: false, restart: true)` and the host ends. The restarted App
  starts the Agent beside itself as always, and when Velopack reports the restart (`OnRestarted`) the host reads
  the logon task: `Stale` (the action path moved) runs `--install-task`, this user's own task, and says so in the
  log — the "re-validated after update" above. Velopack keeps `current` stable, so `Installed` is the expectation.
- **Channels:** `stable` = releases GitHub does not mark pre-release; `beta` = those too (`GithubSource(prerelease)`).
  Not Velopack's own `--channel` mechanism — one package set per tag, the GitHub flag decides. `13_CI_CD` §Branch &
  release policy.
- **Hooks** (`VelopackHooks`, in the hand-written `Program.Main` so `VelopackApp.Run()` precedes any window):
  install registers nothing; uninstall = `UninstallHook` (`12_BUILD` §Publish & package). `Run()` in a copy the
  installer did not lay down returns at once and runs no hook (probed on 1.2.0); it does append a line to Velopack's
  own `%LOCALAPPDATA%\velopack\velopack.log` on every start — local, the library's, and stated here because it is a
  file outside `%LOCALAPPDATA%\FrameLedger`.
- **Where it installs:** `%LOCALAPPDATA%\FrameLedger.App` (the package id), never `%LOCALAPPDATA%\FrameLedger`, which is
  the data folder Velopack would otherwise delete on uninstall (`12_BUILD` §Publish & package).

**Deviations from the table and the paragraph above, stated rather than left to be discovered:**

- `Update_Err_RateLimited` says "try again in about an hour", not "in {n} minutes": Velopack's downloader
  surfaces the status code (`HttpRequestException.StatusCode`) and not `Retry-After` / `X-RateLimit-Reset`.
  Likewise **no `If-None-Match` ETag** on the check — the request is Velopack's, not ours. The quota is 60/h
  anonymous; the App makes one request per start plus manual checks, so the header would change nothing in
  practice, but the sentence above promised it and this one withdraws it.
- **No `GitHubHttpClient`**, no 10 s timeout of ours, no jittered retry: the HTTP is Velopack's
  `HttpClientFileDownloader`, subclassed as `FrameLedgerFileDownloader` only to put `User-Agent: FrameLedger/{version}`
  in place of `Velopack/1.2.0` (a read-only static there). The GitHub source adds no `Authorization` header for an
  empty token — measured on 1.2.0 by reflection, so the request identifies the product and nothing else. When the rules fetch is built
  (`05_DETECTION` FR-7.3, `20_OPEN_QUESTIONS` §S20 feed half) it needs a client of its own; this paragraph no
  longer claims the updater provides one.
- Release notes are rendered as plain text, not Markdown.

`UpdateServiceTests` (App.Tests/Update) cover the two skips, the channels, Deferred ↔ Ready, each error row, and
the apply's three endings; `UpdateFailureMapperTests` the table; `UninstallHookTests` the hook; a real feed has
not been exercised — the first tag is the measurement (`13_CI_CD` §release.yml).
