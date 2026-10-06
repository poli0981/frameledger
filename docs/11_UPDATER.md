# 11 — Updater

Velopack, feeding from GitHub Releases of `https://github.com/poli0981/frameledger`. ~~Stable channel only in v1.~~ *(Corrected 2026-10-04: two channels since P4 PR-5 — `update.channel`, ~~stable by default~~ or beta, which adds pre-releases. Every release so far is a pre-release; see §Built, Channels.)* *(Corrected 2026-10-05: the default is `auto` since beta.14, which follows the running copy — §The update audit, beta.14.)*

## Flow

- **Startup silent check** (if enabled): `UpdateManager.CheckForUpdatesAsync()` 5 s after UI idle. Offline/any failure → log `Information`, no dialog (NFR-10). Update found → non-blocking toast "Update vX.Y.Z available" → Downloads in background → "Restart to update" button (never auto-restart; the user may be mid-capture — if a capture is active, defer the prompt until session end).
- **Manual check** (Help → Check for updates): progress dialog; all failures produce the mapped dialog below.
- **A viewer never updates** (beta.15, D52): an App started with `--data-dir <copy>` registers neither Velopack nor the
  startup check (`Services.MachineFacingServices` gives it `ViewerUpdateClient`, which answers "not installed"), and
  Help → Check for updates and the automatic-check switch are disabled — an update replaces this PC's installed copy.
- **Never apply an update while a game is hooked** (FR-12): the Overlay DLL on disk must not change under a running game, and a mid-session swap would invalidate the ring layout handshake. If a capture is active, defer the prompt and the apply until the session ends.
- Apply: `WaitExitThenApplyUpdates` on restart. Agent is stopped via `Shutdown` before applying and the scheduled task action path is re-validated after update (Velopack keeps a stable `current` path, but verify in P4 and re-register task if the action path changed).
- Release notes (GitHub release body, Markdown) rendered in the update dialog. ~~(shown as plain text until beta.12 — §Flow and §Limits below said so)~~ **Rendered since beta.12** by `Controls/MarkdownView` (`08_UI` §Menu, *Documents are rendered*), relative links resolved against `CHANGELOG.md` at this build's source.

## Error mapping (FR-12)

| Condition | Dialog (resx key) | Extra behavior |
|---|---|---|
| HTTP 404 | `Update_Err404` — "No release feed found. The repository may have moved." | Link to releases page |
| HTTP 403 / 429 | `Update_Err_RateLimited` — "GitHub rate limit reached. Try again in {n} minutes." | Parse `Retry-After` / `X-RateLimit-Reset` when present; send `If-None-Match` ETags on checks to conserve the anonymous quota |
| HTTP 5xx | `Update_Err_Server` — "GitHub is having trouble. Try again later." | |
| Timeout / DNS / no network | `Update_Err_Offline` | Silent on auto-check |
| Package hash mismatch | `Update_Err_Corrupt` | Auto-retry once, then dialog |
| Unknown | `Update_Err_Unknown` + exception logged | "Report a bug" shortcut |

~~Detection-rules updates share this client, but the **`anticheat` block is fetched and applied on its own schedule regardless of the user's rules-update preference** (`05_DETECTION` FR-7.3) — a user who turned off rules updates must still receive new anti-cheat entries.~~ *(Corrected 2026-10-04: there is no rules fetch and no rules-update preference. Tools ▸ Update detection rules re-reads the copy seeded from the install (`07_IPC`); FR-7.3 is unmet (`20_OPEN_QUESTIONS` §S20). The sentence is what a fetch must do once built.)*

All update HTTP goes through one `GitHubHttpClient` (also used by rules updates) with: UA `FrameLedger/{version}`, 10 s timeout, ETag cache in `settings`, single retry with jitter for transient failures.

## Unsigned releases

Binaries are not code-signed (project policy). Consequences and mitigations, documented in README and the update dialog footer:
- SmartScreen warning on first run of a new version — expected; SHA-256 checksums (`SHA256SUMS.txt`) published with every release; CI prints them into release notes.
- ~~Velopack delta packages reduce download size; full package fallback automatic.~~ *(Corrected 2026-10-04: no delta has been published — `release.yml` never puts the previous release in `out/release` before `vpk pack`, so every update downloads the full package; a delta needs a `vpk download github` step first.)* **Built in beta.13:**
  `release.yml` downloads the previous release into `out/release` before `vpk pack`, so a release carries
  `FrameLedger.App-<version>-delta.nupkg` beside the full package; the previous full package is moved out (neither uploaded
  nor checksummed) and the feed lists this build alone, as `vpk upload github` would write it. Velopack applies the delta
  when the installed copy is the previous release and falls back to the full package otherwise — `0.1.0-beta.13` is the
  first release with one, so beta.12 → beta.13 is the first update that can use it (owner-only: HANDOFF item 10).
- Never bypass or suppress OS warnings programmatically.

## Versioning

SemVer `MAJOR.MINOR.PATCH`. Tag `vX.Y.Z` triggers the release workflow (13_CI_CD). `MAJOR` bumps for DB schema or IPC protocol breaks; migrations must cover every released `MAJOR-1` version.

> **Under `0.x` this rule is not applied, and every pre-release says so by shipping one anyway** (SemVer §4: a major version
> zero is initial development). Schemas ~~0008–0015~~ 0008–0017 *(corrected 2026-10-04: 0016 shipped in beta.10 beside 0015, 0017 in beta.11; 0018 rides beta.12)* and the shared-memory layout 4 (2026-09-27, beta.10) each shipped in a
> `0.1.0-beta.N`. What protects a user is not the number: the ledger migrates in place (`06_DATA_MODEL` §Migrations), and a
> layout mismatch is refused at attach with "restart the game" (`07_IPC` §Protocol rules). The rule applies from `1.0.0`.

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
  `UpdatePrompts` (WPF-UI `MessageBox`): the offer with the release notes (the GitHub release body, ~~shown as text —
  no Markdown renderer is shipped~~ rendered since beta.12, at most 260 px tall and scrolling), the unsigned-release footer, Download / Later; "up to date"; "not an
  installed copy"; and the error table below. There is no progress *dialog*: progress is the banner's percent. A
  package that fails its checksum is downloaded once more before the Corrupt row is shown (the table's "auto-retry
  once").
- **FR-12 is the shape of the flow, not a check at the end.** A downloaded package is `Ready` only while the Agent
  reports no session (its `StatusAck` at connect, then `SessionStarted` / `SessionCompleted` on the pipe);
  otherwise it is `Deferred`, the button is disabled and the body says why, and it becomes `Ready` when the last
  session ends. The apply is refused again inside if a session started meanwhile. ~~A recording-only (Tier 2)
  session that began before the App connected is covered by the status; one that begins afterwards raises no
  `SessionStarted` (that event is the ring's) and is not — a known gap, recorded rather than hidden: the Overlay
  is not on disk under such a session, so the rule's reason does not apply to it.~~ **Closed 2026-09-23:** a held
  (Tier 2) session raises its own `SessionStarted`, so it defers an update like any other; and the status read at
  connect SEEDS the running set rather than being OR-ed with it forever — before, a session running when the App
  connected held every download at `Deferred` until the next reconnect, hours after it ended.
- **Apply:** `Shutdown` to the Agent over the pipe, the connection's relaunch held (`IAgentLink.SetLaunchHold`),
  the pipe watched until it drops (10 s; an Agent that does not stop leaves everything as it was and says so) —
  **and since beta.10 the data folder's instance lock watched until it is let go, within the same 10 s**: an Agent run as
  administrator (the admin mode) holds the install directory's files exactly as long as its process lives, and one that
  holds the folder without answering this App (still starting, another version) is never updated under — the apply is
  refused with `Update_AgentStillRunning` instead,
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
- ~~Release notes are rendered as plain text, not Markdown.~~ Corrected 2026-10-03: rendered since beta.12 (Markdig; HTML off, no image fetched).

## The update audit, beta.14 (owner request 2026-10-05: "check the update logic")

No failure had been reported; the audit read the flow above against Velopack 1.2.0 and the owner's own updater log
(`%LOCALAPPDATA%\velopack\velopack_FrameLedger.App.log`, read-only) and found two defects that made the flow above
partly fiction, and six smaller ones. Each is fixed in place:

- **A copy left on its defaults never found an update.** `update.channel` defaulted to `stable` while every release is a
  GitHub pre-release; the owner's log reads "No releases found" from the day their ledger (and its settings row) was reset.
  **Owner decision D47:** the default is `auto`, which follows the running copy — a pre-release asks for pre-releases, a
  release for releases (`Update/UpdateChannelPolicy`, SemVer's `-` before any `+`) — and a stored `stable` or `beta` is the
  user's choice and wins. Settings shows *Automatic (Beta)* or *(Stable)* first. Only an explicit selection ever wrote the
  row (`SettingsViewModel.Persist` is guarded while loading), so existing copies with no row move to `auto`.
- **Velopack applied a downloaded update at the next start, behind FR-12.** `VelopackApp` applies a package waiting in
  the install folder on startup unless told not to (`SetAutoApplyOnStartup`, ON by default), and the startup check
  downloads in the background — so the next start of `FrameLedger.exe` (the Start menu, the logon Run value, a second
  click, `--diag`) applied it inside `VelopackApp.Run()`, before the single-instance claim, before FR-12 could ask
  whether a session runs, and before the Agent was asked to stop; its updater killed the Agent instead (the owner's log:
  "Auto apply is true, so restarting to apply update…", then "Killing process: …FrameLedger.Agent.exe"). Off now
  (`VelopackHooks`, pinned by `VelopackHooksTests` through the builder's private fields); a package an earlier run left
  is adopted at start — `UpdateService.AdoptPending`, from `UpdateManager.UpdatePendingRestart`, no feed request — and is
  Ready or Deferred like any other.
- **An update Velopack starts itself** (the installer run over an installed copy) asks the Agent to stop first, as the
  uninstaller does (`OnBeforeUpdateFastCallback`, 15 s budget, a line in `logs\update.log`). The hook runs in the
  version being replaced, so it first works for the update after beta.14.
- **The apply waits while a game still has the Overlay loaded** from the install folder (`Update/PayloadInUse`: a mapped
  image refuses an exclusive write — a file-system question, no process opened). The Overlay is never unloaded from a
  live game (`17_HOOK_ENGINE`), so after a mid-game stop the swap would fail half way after the App had quit. Asked before
  the Agent is stopped; the strip names the file.
- **An Agent waiting on the administrator prompt** (the logon task's, the admin mode) holds only its elevation marker, not
  the instance lock; the apply now waits for either (`UpdateService.AgentHolds`).
- **The stop budget is the Agent's own shutdown grace and five seconds** (`IpcProtocol.AgentShutdownGrace`, 15 s, which
  the Agent's host now reads too): the App waited 10 s for an Agent that took up to 15 to finalize its sessions.
- **The offer says the download Velopack will make:** the deltas from the installed version when the feed has them
  (`UpdateInfo.DeltasToTarget`; 1.6 MB for beta.12 → beta.13) and the full package as the fallback — it said ~102 MB.
  Velopack's own lines are in the App's log since beta.14 (`velopack: …`, `VelopackSerilogLogger`), so whether a delta
  or the full package came is in the bug report too.
- **Smaller:** an `IOException` is "offline" only from the network (`HttpIOException`, a socket error) — a full disk
  read as offline; the uninstaller removes the "Start FrameLedger with Windows" Run value, which named an executable it
  deletes.

**What none of this reaches:** a copy still on 0.1.0-beta.13 or older runs its own code for the update to beta.14 —
stable by default, apply-at-start on. Its user chooses *Beta* in Settings ▸ Updates and updates with no game running
(the beta.14 release notes say so); everything above holds from beta.14 on.

`UpdateServiceTests` (App.Tests/Update) cover the two skips, the channels, Deferred ↔ Ready, each error row, and
the apply's three endings; `UpdateFailureMapperTests` the table; `UninstallHookTests` the hook; ~~a real feed has
not been exercised — the first tag is the measurement~~ *(corrected 2026-10-04: the feed has existed since
v0.1.0-beta.1, 2026-09-16, and every release since carries `releases.win.json`; whether an installed copy has updated
through it is not recorded here)*
(`13_CI_CD` §release.yml).
