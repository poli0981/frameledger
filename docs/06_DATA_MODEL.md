# 06 — Data model (SQLite)

`%LOCALAPPDATA%\FrameLedger\ledger.db`. Pragmas: `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `busy_timeout=5000`. `Microsoft.Data.Sqlite` + Dapper; all writes in explicit transactions.

**Writer ownership:** Agent writes `sessions`, `session_segments`, `frame_blobs`, `sensor_blobs`, `hardware_snapshots`, the hook-state columns on `games`, **and since P4 PR-1 (2026-09-14) the *detected* columns on `games`** — `engine`, `engine_version`, `platform` under the per-field provenance rule (`05_DETECTION` §Caching: a `user` field is never touched), `capability_flags` whole, and the cache key `detection_rules_version` / `detection_exe_size_bytes` / `detection_exe_mtime_ms`, and since schema 0011 (2026-09-25) the executable's own facts `exe_machine` / `exe_file_version` / `exe_product_version` / `library_versions`, written whole like the flags — through `IGameRepository.ApplyDetectionAsync` only. The UI and the Agent were both already writers of `games`; this adds columns to the Agent's half, not a third writer (HANDOFF §P4). **And since P4 PR-4 the UI's library import writes `platform`, `store_id` and `game_version` through `ApplyStoreMetadataAsync`** — the same per-field provenance rule, badged `detected` (a store's record is not the user's typing), on columns the UI already owned. **And since 2026-09-23 the Agent's relocator merges two entries for one executable under two drive letters** (`IGameMerge`, HANDOFF D26, `19_SAFETY` §A moved drive is the same executable): in one transaction it re-parents the dropped entry's `sessions` to the survivor (before the delete — `sessions.game_id` cascades), carries a block or a crash auto-disable still in force to the survivor (`hook_enabled` only ever to 0; `hook_blocked_reason` is copied, never originated or cleared), adds the crash counts, deletes the dropped `games` row, and then moves the survivor to the file's path (after the delete — `exe_path` is UNIQUE). It is the one Agent write that deletes a `games` row; it never touches a consent column. **And since 2026-09-23 the UI writes `record_sessions`** (schema 0009, FR-1.6): the entry's recording switch, which the Agent only reads — off, the watcher does not watch for the program. The script's one data change switches it off for entries an old import made for Steam's own tools (`SteamLibrarySource.KnownTools`, the ids as of the script); a twin merge keeps it off when either twin had it off. **And since 2026-09-25 the Agent's pre-scan of the library (`19_SAFETY` §What a finding does to the game) writes the hook-state columns of every entry, not only of those the user asked to hook** — `hook_prescan_state`, and on a finding `hook_enabled = 0` and `hook_blocked_reason` — and its own key, `hook_prescan_rules_version` / `hook_prescan_exe_size_bytes` / `hook_prescan_exe_mtime_ms` (schema 0010), through `IGameConsentStore.RecordPreScanAsync` only: columns that were already the Agent's, never over a block, never a new row. **Maintenance (P4 PR-7) adds no writer:** the retention sweep on demand is the Agent's, asked over the pipe; the UI's `VACUUM` rewrites pages with the same contents and changes no row (§Retention built note). UI writes `games` (user-editable fields — since P3 PR-3 that is the FR-1.3 metadata, the `*_default` tri-states, and `removed_at`; never a `hook_*` column), `session_annotations` (tags, notes, and since schema 0002 the FR-8.3 overrides — which is WHY they are on this table and not on `sessions`), `settings`, `legal_acceptance`. Both read everything. The ports say the same in their names: `IGameRepository.UpdateMetadataAsync` / `SetTriStateDefaultAsync` / `RemoveAsync`, `ISessionAnnotationRepository`, `RegisteredSettings`, `ILegalAcceptanceStore.RecordAsync` (all P3 PR-3).

## Schema (v2 — hook architecture)

> **`src/FrameLedger.Infrastructure/Persistence/Migrations/0001_init.sql` is normative since 2026-09-09
> (P2 PR-B).** The block below is the shape as drafted and is kept for its comments; the script adds
> what this document had no column for, each with the reason beside it in the script:
>
> - `sessions.session_guid` (UNIQUE) — the identity the `.partial` file, the pipe and recovery key on
>   (`20_OPEN_QUESTIONS` §G "Session identity"); `qpc_epoch` + `qpc_frequency` — the time base the
>   blobs and sensor series are aligned to (`03_METRICS` §Export, §Sensor aggregates);
>   `capture_mode` (`attach|launch`); the drain's own accounting (`drain_ticks`, `foreground_ticks`,
>   `records_before_attach`, `dxgi_presents_before_hook`, `gap_count`, `guard_ticks_published`,
>   `launch_wait_ms`); the 2026-09 metric additions (`presented_fps` + `presented_qualifier`,
>   `dxgi_unseen_total` / `dxgi_present_samples` / `displayed_counted_by`, `sl_tag_census`,
>   `sl_interposer_version`, `upscaler_driver_reported` / `fg_driver_reported` / `ngx_driver_words`,
>   `fg_none_withheld_reason`, `runtime_modules`, `executable_markers`, `writer_status_at_end`,
>   `early_stop_family`, `loader_signals`).
> - `games.exe_size_bytes` / `exe_mtime_ms` (the `ExecutableFingerprint`),
>   `hook_consent_provenance` (the `ConsentProvenance` NAME, default `NotRecorded`),
>   `hook_consent_disclosure_version`, **`hook_prescan_state`** (`not_run|clean|blocked|unverified` —
>   the third state one nullable `hook_blocked_reason` could not carry; nothing in P2 clears the
>   reason, per the owner question below), `hook_autodisabled_at`, `hook_last_injected_at`.
> - `frame_blobs.frame_index`, `swapchain_ids`; `sensor_blobs` gains the `t_ms` series.
> - **`fg_source`'s domain is `api|cadence|none|manual`, NULL = not measured** — this document said
>   `api|cadence|manual` while `03_METRICS` names rung 4 `none`; the CHECK carries the union.
> - **Every `*_at` / `*_ms` INTEGER is unix-ms UTC** (CLAUDE.md §Coding conventions), stated in the
>   script because it was stated nowhere in this file. QPC never leaves the pipeline except
>   `qpc_epoch` / `qpc_frequency`.
> - `exe_path` is `UNIQUE COLLATE NOCASE`, because consent matches `OrdinalIgnoreCase` on the
>   normalised full path and the index must agree with the lookup.

```sql
CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_at INTEGER NOT NULL);

CREATE TABLE games (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  exe_path TEXT NOT NULL UNIQUE,               -- where the executable lives; rewritten by ExecutableRelocator when the
                                               -- SAME bytes (exe_size_bytes, exe_mtime_ms) turn up under another drive
                                               -- letter (2026-09-22), and by Change executable for a DIFFERENT binary
  platform TEXT NOT NULL DEFAULT 'none',       -- steam|gog|epic|itch|none
  store_id TEXT,
  engine TEXT, engine_version TEXT,
  publisher TEXT, game_version TEXT,
  cover_path TEXT, notes TEXT,
  removed_at INTEGER,                          -- schema 0002 (P3 PR-3): FR-1.4 "remove, keep sessions" — the row stays
                                               -- (sessions.game_id is NOT NULL, ON DELETE CASCADE) but leaves the
                                               -- library; a later launch of the same exe restores it (removed_at = NULL)

  -- hooking state (19_SAFETY)
  hook_enabled INTEGER NOT NULL DEFAULT 0,     -- 0 = Tier 2, i.e. nothing measured; default for every new game
  hook_consent_at INTEGER,                     -- per-game informed consent timestamp
  hook_blocked_reason TEXT,                    -- set by the static AC pre-scan; non-null = toggle disabled in UI.
                                               -- 'Reason|Family|Signal' since 2026-09-25 (SqliteGameConsentStore.
                                               -- BlockText), so the App can say it in the user's language; older
                                               -- rows keep "Reason: Family Signal" and are shown as stored
  hook_prescan_rules_version TEXT,             -- schema 0010 (2026-09-25): the key the Agent's pre-scan of the
  hook_prescan_exe_size_bytes INTEGER,         -- library last scanned the row under — the rules version and the
  hook_prescan_exe_mtime_ms INTEGER,           -- executable's size and mtime; NULL = never scanned by the sweep.
                                               -- Change executable clears them with hook_prescan_state
  hook_autodisabled_reason TEXT,               -- set after repeated crashes
  hook_crash_count INTEGER NOT NULL DEFAULT 0,
  capability_flags TEXT,                       -- JSON: what the game SHIPS — never a measurement. Since P4 PR-1 (2026-09-14) a JSON
                                               -- array of the rule ids of rules/detection-rules.json (dlss, dlss_g, dlss_rr, streamline,
                                               -- fsr, xess, xefg), written whole by the Agent's detection sweep; this line used to spell
                                               -- the App's tokens (dlssg/dlssd), which the App still reads for older rows

  -- tri-state defaults inherited by new sessions
  rt_default TEXT NOT NULL DEFAULT 'na',
  pt_default TEXT NOT NULL DEFAULT 'na',
  rr_default TEXT NOT NULL DEFAULT 'na',

  detection_rules_version TEXT,
  exe_machine TEXT,                            -- schema 0011 (2026-09-25): what the executable runs as, from its PE
                                               -- headers (x64|x86|arm64|arm|anycpu|anycpu32|other|unknown); NULL =
                                               -- not read by this build yet, which the detection sweep reads as stale
  exe_file_version TEXT,                       -- schema 0011: the executable's PE FileVersion (the file's word; game_version
  exe_product_version TEXT,                    -- is the store's or the user's) and its ProductVersion
  library_versions TEXT,                       -- schema 0011: JSON array, the capability files the game SHIPS —
                                               -- {"capability","path","fileVersion","productVersion"}; what a session
                                               -- LOADED is sessions.runtime_modules
  field_provenance TEXT,                       -- JSON: {"engine":"detected","publisher":"user",...}
                                               -- absent or unrecognised reads as "user"
  added_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
);
```

> **`field_provenance` — decided now, implemented in P2.** FR-1.3 requires
> auto-detected fields to be badged and overridable, and there was nowhere to
> record which was which. One JSON column rather than a column per field, matching
> the `capability_flags` precedent in the same table: the overridable set is small
> and fixed (`engine`, `engine_version`, `platform`, `store_id`, `publisher`,
> `game_version`, `name`, `cover_path`), and a normalised override table would mean
> a join on every library-card render.
>
> **Absent reads as `user`, deliberately.** Of the two ways to be wrong, badging a
> human's typed value as auto-detected is a lie they cannot see through; failing to
> badge a detected value merely costs a badge on something they can still edit. The
> rule it exists to support — *detection never overwrites a `user` field* — is in
> `05_DETECTION` §Caching and is enforced by `StaticDetectionResult.ShouldWrite`,
> which is tested even though nothing persists yet.
>
> ~~No migration is written here. There is no SQLite layer at all, and §Migrations
> forbids editing an applied script — so guessing the shape into `0001_init.sql`
> before its consumers exist maximises the chance of baking in a wrong guess that
> can only be appended to.~~ **Written 2026-09-09 (P2 PR-B), with its first consumer:** the
> column exists in `0001_init.sql`; the writer (the detection cache, P4) and the reader (the
> library card, P3) are still unbuilt, so it is decided, created, and empty.

> **The three hook-state columns have a consumer before they have a table, and two
> fields it needs are not here.** Added 2026-08-06 with `IGameConsentStore`, whose
> only adapter today is a file inside the unshipped `FrameLedger.CaptureHost`.
>
> - **A disclosure provenance.** `hook_consent_at` is a bare timestamp and cannot
>   distinguish a stamp made after FR-2.1's reviewed dialog from one made after
>   anything else — so a record could carry a consent time that nothing had ever
>   disclosed anything for. `ConsentProvenance`'s zero value means *no disclosure was
>   shown*, and it deliberately has **no FR-2.1 member**: that dialog needs reviewed
>   `Safety_*` wording in en/vi/ja and no `.resx` exists anywhere in this tree.
> - **A disclosure/wording version.** `legal_acceptance` carries `(doc, version,
>   accepted_at)` while this column carries a timestamp alone, so nothing could mark
>   existing per-game consent stale when the reviewed wording changes. It is carried
>   from the *first* record because retrofitting one means treating unversioned
>   consent as either current or stale, and both are wrong about some record.
> - **A third pre-scan state.** `hook_blocked_reason` is two-state by definition here
>   — non-null disables the toggle — while `05_DETECTION` makes the pre-scan
>   tri-state, and says explicitly that *"could not verify"* must neither disable the
>   toggle (a false refusal with no appeal) nor be cleared (a fail-open). One nullable
>   TEXT column cannot carry three states.
>
> **The file-backed record was NOT a migration source, and was not migrated.** It lived in a
> build output, `git clean` removed it, and when the `games` table was written (2026-09-09, P2
> PR-B) it was written from this document — the file store and its DTOs were deleted the same day.
> The two fields the consumer needed and this table lacked are columns now:
> `hook_consent_provenance` and `hook_consent_disclosure_version`; the third pre-scan state is
> `hook_prescan_state`. **`ConsentProvenance` has a third name since 2026-09-10 (P2 PR-F)** — and a fourth, `ConsentDialog`, since 2026-09-13 (P3 PR-4), the only one a shipped user path produces:
> `AgentConsoleOperator`, the Agent's own `--console consent grant` (HANDOFF §P2 decision D4) — a
> shipped producer, still not FR-2.1, stamped by the Agent's clock; the store writes the NAME the
> acknowledgement carries and refuses `NotRecorded`.
>
> **Still unanswered, and it is an owner decision rather than a coding one:** who
> clears `hook_blocked_reason` when a re-scan comes back clean. `19_SAFETY` §A game
> already enabled can become blocked later makes a non-null value mean "toggle
> disabled" and makes re-enabling "a user action" — but if nothing clears the column
> the toggle is permanently disabled and that user action is impossible, while a
> managed component that clears it is deciding an anti-cheat fact. Nothing in this
> tree clears it today, which leaves the state unreachable rather than wrong.

```sql

CREATE TABLE hardware_snapshots (
  id INTEGER PRIMARY KEY,
  hash TEXT NOT NULL UNIQUE,                   -- sha256 of normalized fields
  cpu_name TEXT, gpu_name TEXT, gpu_driver TEXT,
  ram_gb REAL, os_build TEXT,
  display_res TEXT, display_hz REAL,
  captured_at INTEGER NOT NULL
);

CREATE TABLE sessions (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  snapshot_id INTEGER NOT NULL REFERENCES hardware_snapshots(id),
  started_at INTEGER NOT NULL, ended_at INTEGER NOT NULL, duration_s REAL NOT NULL,

  capture_tier INTEGER NOT NULL,               -- 1 = hooked, 2 = not hooked; CHECK (capture_tier IN (1,2))
  capture_notes TEXT,                          -- why tier degraded, late_attach, etc.
  late_attach INTEGER NOT NULL DEFAULT 0,
  telemetry_source TEXT,                       -- composite descriptor, e.g. 'l1+lhm+nvapi' (18_GPU_VENDOR_APIS)
  overlay_build_id TEXT,                       -- native DLL build that produced this data
  exit_status TEXT NOT NULL,                   -- normal|crashed|unhooked_safety|degraded|interrupted

  -- Layout v3 (2026-08-05) changed four things here, and none had shipped:
  --   * `hdr INTEGER` -> `hdr_flag`/`hdr_source`, matching the three tri-states
  --     beside it. The record's `hdr` bool became `colorSpace` and a bool has no
  --     third state; an INTEGER column would have reintroduced zero-means-two-
  --     things one layer down.
  --   * `dlss_rr` leaves the `upscaler` domain. RR is an independent axis and is
  --     already stored as `rr_flag`; it was a mutually exclusive upscaler VALUE in
  --     the record, which is the conflation v3 removed. A session where DLSS-SR
  --     and RR ran together stores `upscaler='dlss'` with `rr_flag='yes'`.
  --   * `rt_tier` + `hooks_installed_mask`: 03_METRICS' definite RT `No` needs
  --     both, and neither had a column. Without them the verdict is decided at
  --     finalize from evidence that cannot distinguish "no rays" from "no hook".
  --   * `upscaler_sharpness`, so the accessor hook's yield has somewhere to land.
  -- Free exactly once: 0001_init.sql does not exist yet (see below).

  -- presentation
  api TEXT, present_mode TEXT, swap_effect TEXT,
  hdr_flag TEXT NOT NULL DEFAULT 'na', hdr_source TEXT,  -- tri-state, like rt/rr beside it
  sync_interval_mode TEXT,                     -- observed vsync behavior

  -- upscaling / FG (measured at tier 1)
  upscaler TEXT,                               -- none|dlss|fsr2|fsr3|fsr4|xess|nis|unknown
  upscaler_quality TEXT,
  upscaler_sharpness INTEGER,                  -- percent; NULL when the API reports none
  render_w INTEGER, render_h INTEGER,          -- dominant segment
  output_w INTEGER, output_h INTEGER,
  upscale_ratio REAL,
  settings_changed_midsession INTEGER NOT NULL DEFAULT 0,
  -- 'na', NOT 'none', and the default is the whole point. `none` means "a hook ran and
  -- there was genuinely no frame generation" -- the one state 03_METRICS allows to be
  -- aggregated as a negative -- so a DEFAULT of 'none' reinstates at the storage layer
  -- exactly the affirmative negative the writer and the consumer both refuse
  -- (fl_shm.h: FL_FG_NOT_REPORTED is 0 in v3; MeasuredFacts.FgMode is null, never "none").
  -- Matches hdr_flag/rt_flag/pt_flag/rr_flag beside it, which were already 'na'.
  fg_mode TEXT NOT NULL DEFAULT 'na',
  fg_source TEXT,                              -- api|cadence|manual (`etw` removed 2026-08-28: no producer, no tier)
  fg_factor REAL,
  -- schema 0003 (2026-09-14): why fg_factor is NULL while fg_mode names a technology — FgRefusalKind as
  -- Vocabulary.FgRefusal spells it (not_counted, no_evaluations, non_uniform, dxgi_saturated, …). NULL when a
  -- factor stands or the writer predates the column. A REASON for the UI, never an input to fg_mode or a number.
  fg_refusal TEXT,
  -- FlWriterState.runtimeCensus, raw (FlRuntimeCensus bits). Which vendor runtime modules
  -- the loader reported in the process. NOT a measurement and never a source for fg_mode
  -- or upscaler: it qualifies the presented figure when fg_mode is 'na' (03_METRICS §Rung
  -- 0's qualifier). NULL = the writer predates the field; 0 = the census never ran.
  fg_runtime_census INTEGER,

  -- ray tracing (measured at tier 1)
  rt_flag TEXT NOT NULL DEFAULT 'na', rt_source TEXT,   -- measured|manual|inherited
  pt_flag TEXT NOT NULL DEFAULT 'na', pt_source TEXT,
  pt_confidence REAL,                          -- heuristic score, never auto-promoted to 'yes'
  rr_flag TEXT NOT NULL DEFAULT 'na', rr_source TEXT,
  rt_frame_pct REAL, rays_per_pixel REAL, rt_pso_count INTEGER,
  rt_tier INTEGER,                             -- D3D12_RAYTRACING_TIER x10; NULL = not queried
  hooks_installed_mask INTEGER,                -- FlHookFamily union over the session
  raster_pso_count INTEGER,

  -- frame statistics
  frame_count INTEGER NOT NULL, app_frame_count INTEGER NOT NULL,
  displayed_frame_count INTEGER NOT NULL, dropped_frames INTEGER NOT NULL,
  native_fps REAL, displayed_fps REAL,
  median_fps REAL, p1_low_fps REAL, p01_low_fps REAL,
  displayed_p1_low_fps REAL,                   -- 03_METRICS §Lows: stored for the
                                               -- Displayed chart series, never the headline
  min_fps REAL, max_fps REAL, frametime_stddev_ms REAL,
  stutter_count INTEGER, stutter_time_pct REAL,
  pso_stutter_pct REAL,                        -- tier 1 only
  reflex_active INTEGER, latency_avg_us INTEGER, latency_p95_us INTEGER,

  -- data quality
  dropped_records INTEGER NOT NULL DEFAULT 0,  -- ring overflow (agent stalled)
  fault_count INTEGER NOT NULL DEFAULT 0,      -- overlay hook faults
  data_quality_warnings INTEGER NOT NULL DEFAULT 0,

  -- memory & sensors
  vram_proc_avg_mb REAL, vram_proc_max_mb REAL, vram_budget_exceeded_pct REAL,
  vram_adapter_max_mb REAL,
  avg_cpu_temp REAL, max_cpu_temp REAL,
  avg_gpu_temp REAL, max_gpu_temp REAL, max_gpu_hotspot REAL,
  avg_gpu_load REAL, avg_cpu_load REAL, avg_ram_mb REAL,
  avg_gpu_power_w REAL, throttle_pct REAL
);
CREATE INDEX ix_sessions_game_started ON sessions(game_id, started_at DESC);

-- resolution/upscaler changes within one session (03_METRICS §Upscaling)
CREATE TABLE session_segments (
  id INTEGER PRIMARY KEY,
  session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  start_frame INTEGER NOT NULL, end_frame INTEGER NOT NULL,
  render_w INTEGER, render_h INTEGER, output_w INTEGER, output_h INTEGER,
  upscaler TEXT, upscaler_quality TEXT, fg_mode TEXT,
  native_fps REAL, displayed_fps REAL, p1_low_fps REAL
);
CREATE INDEX ix_segments_session ON session_segments(session_id, start_frame);

-- Per-frame series. Every column of the CSV export (03_METRICS §Export schema)
-- must be reconstructible from this table plus session_segments — the exporter
-- reads from here, not from the live ring.
CREATE TABLE frame_blobs (
  session_id INTEGER PRIMARY KEY REFERENCES sessions(id) ON DELETE CASCADE,
  codec TEXT NOT NULL, sample_count INTEGER NOT NULL,
  frametimes BLOB NOT NULL,        -- float32[] ms
  frame_flags BLOB NOT NULL,       -- byte[] : generated/dropped/gap bits
  rt_flags BLOB,                   -- byte[] : one per frame, all 3 bits preserved
                                   -- (asBuildObserved | dispatchObserved | psoCreatedEver).
                                   -- Collapsing these loses the inline-RayQuery distinction.
                                   --
                                   -- `rtPsoAlive` was the third bit's name here and is WRONG:
                                   -- fl_shm.h renamed it FL_RT_PSO_CREATED_EVER because the
                                   -- bit LATCHES -- creation is observed at CreateStateObject,
                                   -- destruction is COM Release, which is not in the hook
                                   -- inventory and must not be added -- so "alive" claimed a
                                   -- present-tense fact the hook set cannot retract.
  render_res BLOB,                 -- uint16[] : TWO pairs per frame (render W/H, output W/H),
                                   -- stored only when either varies
  dispatch_rays BLOB,              -- uint32[] : ray volume per frame, tier 1 only
  pso_created BLOB,                -- uint16[] : compile COUNT per frame, not a flag
  vram_proc BLOB,                  -- uint32[] MB per frame; held 1 Hz sample, see 03_METRICS
  latency_us BLOB                  -- uint32[], tier 1 + Reflex only
);

CREATE TABLE sensor_blobs (
  session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  -- schema 0007 (2026-09-21) added five guard-override columns (games.guard_bypass_at,
  -- games.guard_bypass_disclosure_version; sessions.guard_bypassed, guard_bypass_family, guard_bypass_signal) and
  -- schema 0008 (2026-09-22) DROPPED them with the feature (19_SAFETY §What a finding does to the game). Sessions that
  -- ran under it keep "guard-bypass=" in capture_notes.
  series TEXT NOT NULL,            -- cpu_temp|gpu_temp|gpu_hotspot|gpu_load|gpu_power|vram_proc|vram_adapter|cpu_load|ram_mb
                                   -- cpu_load / ram_mb / cpu_temp and sessions.avg_cpu_load, avg_cpu_temp, max_cpu_temp,
                                   -- avg_ram_mb were in schema 0001 with NO producer until 2026-09-21 (Telemetry.SystemTelemetrySource
                                   -- on the poller's tick). Rows before that date hold NULL, which is what they measured; no migration.
  hz REAL NOT NULL, codec TEXT NOT NULL, data BLOB NOT NULL,
  PRIMARY KEY (session_id, series)
);

CREATE TABLE session_annotations (
  session_id INTEGER PRIMARY KEY REFERENCES sessions(id) ON DELETE CASCADE,
  tags TEXT, notes TEXT,                       -- tags: a JSON string array (P3 PR-3); a row that would be all NULL is deleted
  -- schema 0002 (P3 PR-3): FR-8.3's per-session overrides live HERE, on the UI's table, beside the
  -- measurement on sessions and never over it — clearing an override restores what was measured.
  -- NULL = no override. sessions.rt_source stays the measurement's word.
  rt_override TEXT CHECK (rt_override IS NULL OR rt_override IN ('yes', 'no', 'na')),
  pt_override TEXT CHECK (pt_override IS NULL OR pt_override IN ('yes', 'no', 'na')),
  rr_override TEXT CHECK (rr_override IS NULL OR rr_override IN ('yes', 'no', 'na'))
);

CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
-- keys in use: hooking.kill_switch = '1' (FR-2.4, P2 PR-F; anything else, or no row, is off)
-- the full key list is §Settings registry below (P3 PR-3)
CREATE TABLE legal_acceptance (doc TEXT PRIMARY KEY, version TEXT NOT NULL, accepted_at INTEGER NOT NULL);
```

> **Written since 2026-09-14 (P3 PR-9).** `doc` is one of `eula` / `gpl` / `disclaimer` / `privacy`
> (`App.Services.LegalDocuments.Keys`); `version` is the document's own `**Version:**` line as embedded in the
> App at build (`GPL-3.0-only` for the licence text) — the App is the only writer, one row per document per
> Accept, re-recorded when the embedded version differs from the row's. `20_OPEN_QUESTIONS` §G "Legal doc
> versioning" closes on that.

## Settings registry

HANDOFF §P3 decision D16, built as `Application.Settings.SettingsRegistry` (P3 PR-3, 2026-09-13) and
closing `20_OPEN_QUESTIONS` §G's row. `settings` is key/value TEXT; the registry is the only key list, and
`RegisteredSettings` over `ISettingsStore` **validates writes and tolerates reads** — a hand-edited or
out-of-range row reads as the default, never as an exception. Booleans are exactly `1`/`0` (the kill
switch's rule); integers are invariant-culture within an inclusive range; choices are compared ordinally.
**No "setting changed" message exists:** the Agent re-reads its keys at each session start
(`SettingsRecorderPolicy` → `RecorderOptions`), and the UI owns the rest.

| Key | Kind | Default | Range / choices | Read by |
|---|---|---|---|---|
| `ui.language` | choice | `en` | `en` · `vi` · `ja` | UI |
| `ui.theme` | choice | `system` | `system` · `light` · `dark` | UI |
| `ui.start_with_windows` | bool | `0` | | UI (P4) |
| `ui.minimize_to_tray` | bool | `0` | | UI (PR-8) |
| `ui.hide_anticheat_hooking` | bool | `1` | | UI (beta.8, 2026-09-25): the game page of an entry the guard found anti-cheat in shows the finding in place of its Hooking card (`08_UI` §Game detail) |
| `capture.background` | bool | `1` | | Agent |
| `hooking.kill_switch` | bool | `0` | | Agent (FR-2.4, since P2 PR-F) |
| `capture.min_session_s` | int | `30` | 5–600 | Agent, per session (FR-3.6) |
| `telemetry.interval_ms` | int | `1000` | 500–2000 | Agent, per session (FR-3.5) |
| `retention.raw_sessions_per_game` | int | `20` | 0–10000; **0 = unlimited** | Agent, per session (§Retention) |
| `update.channel` | choice | `stable` | `stable` · `beta` | UI (P4 PR-5 reads it: `beta` = GitHub pre-releases too) |
| `update.auto_check` | bool | `1` | | UI (P4 PR-5: the startup silent check, `11_UPDATER` §Flow "if enabled") |
| `privacy.online_metadata` | bool | `0` | | *reserved* — no consumer and, since 2026-09-16, no toggle on Settings either: the store request it would enable has no code (CLAUDE.md rule 8), and PRIVACY_POLICY 2.2 removed the row that described it |
| `log.debug` | bool | `0` | | UI |

An operator's bounded capture (`--console … --seconds N`, which lowers the discard threshold to N) keeps
its bound under any `capture.min_session_s`: the setting moves the product's rule, not the operator's.

## Comparison safety

Because Tier-1 and Tier-2 sessions carry different fields, every query that compares sessions **must** filter or group by `capture_tier`. **Since 2026-08-28 the difference is total rather than partial** — a Tier-2 session has no frame data at all — so a mixed-tier overlay is not a degraded comparison, it is a comparison with nothing on one side. The Compare view refuses to overlay mixed tiers without an explicit "compare across tiers anyway" acknowledgement, and marks the chart accordingly. Likewise `settings_changed_midsession = 1` sessions are excluded from trend lines by default (they average across a settings change and are misleading), with a toggle to include them.

## Blob encoding

`float32`/`uint16`/`uint32` little-endian arrays through `DeflateStream` (Optimal). One hour at 100 fps ≈ 360k frames ≈ 1.4 MB raw frametimes ≈ **≤ 0.7 MB stored**; flags compress far better; render-res only stored when it varies. Decode helpers in `Infrastructure.Blobs` with round-trip test

> **Two rules the finalizer applies (2026-09-10, P2 PR-D).** `frametimes[i]` is the interval from the previous
> record ON THE SAME SWAPCHAIN; the first record of a stream, and any record after a torn slot or an
> overwrite skip, carries 0 with the `frame_flags` gap bit set, so the statistics exclude it and the export
> still reconstructs `qpc_ms`. `swapchain_ids` is present only when the session held more than one stream.
> Sensor series are aligned to `t_ms` (milliseconds from `qpc_epoch`) and a tick with no reading is stored as
> **−1** — never 0, which would be a reading — because temperatures, loads, power and memory are never negative.s (NaN forbidden — assert).

## The `.partial` file

> **Built 2026-09-10 (P2 PR-D), as HANDOFF §P2 pre-committed it.** `%LOCALAPPDATA%\FrameLedger\tmp\<sessionGuid>.partial`
> (the unshipped host: `tmp\` beside its binary, decision D5), one per session, append-only, written at every
> state transition and every flush interval (60 s), deleted by the finalize's success, read by recovery at the
> next start. Format: `chunk := u32 type | u32 length | payload | u32 crc32(type ‖ length ‖ payload)`,
> little-endian, and **the valid prefix wins** — the reader stops at the first chunk that is short or fails
> its check, so a kill at any byte loses at most the chunk being written (`PartialSessionFileTests` kills
> at every offset of a fixture and asserts exactly the complete chunks survive). The file is flushed to the
> OS after every chunk; the threat is the Agent dying, not the machine losing power.
>
> | type | payload | when |
> |---|---|---|
> | 1 `Header` | UTF-8 JSON: format version, `session_guid`, `started_at`, `qpc_epoch`/`qpc_frequency`, `game_id`, `snapshot_id`, exe path, pid, tier, mode, Overlay build id, telemetry descriptor, launch wait | on create — the breadcrumb `19_SAFETY` §Crash safety asks for before injection |
> | 2 `Records` | `i64` first ordinal, then N raw `FlFrameRecord` (64 B each); a chunk that does not continue the sequence is refused | each flush |
> | 3 `Gaps` | N × `i32` gap-before record indices | each flush |
> | 4 `Sensors` | N × one telemetry sample: QPC, wall clock, layer, presence mask, then only the fields that carry a value — null stays null | each flush |
> | 5 `Tick` | drain/foreground ticks, dropped, gaps, guard ticks, wall clock, the raw `FlWriterState`; the last one wins | each flush |
> | 6 `Note` | wall clock + UTF-8 text: a state transition | each transition |
> | 7 `Touches` | N × `i64` QPC at which the host touched the target | each flush |
>
> Recovery (`PartialRecovery`): a file whose guid is already a row is deleted (the finalize had landed);
> one under the minimum session length is discarded; one with no readable header is dropped; the rest
> become `interrupted` rows ended at the last flush (or the last record's clock), with every measured
> column computed from the prefix exactly as a live finalize would. **Rejected:** a `sessions_in_progress`
> table — a blob churn every minute, a lock fight with the UI in P3, and `04_CAPTURE` had already named a
> file under `tmp\`.
>
> **The header's `game_id` is where the session started, not necessarily where it lands (2026-09-23).** A live
> finalize and a recovery both ask `ISessionRepository.ResolveOwnerAsync`: that id when the row predates the session
> or holds the header's exe path; else the row that holds the exe path now (an entry removed and re-imported while
> the game ran); else nothing — `FinalizeStatus.GameRemoved` / `RecoveryStatus.GameRemoved`, file deleted, nothing
> written. `games.id` is `INTEGER PRIMARY KEY` without AUTOINCREMENT, so a deleted id can be handed to the next row
> added; `added_at` after the session's start and another path is how that row is told apart. A file recovery cannot
> finalize for any other reason is renamed `<guid>.partial.failed` (`IPartialSessionStore.Quarantine`) — not listed
> again, not retried, kept for a bug report — and recovery goes on to the next file: it runs before the watcher on
> every start, and one file used to be able to stop them all.

## Retention

Default: raw blobs for the **last 20 sessions per game** (configurable N or unlimited — `retention.raw_sessions_per_game`, 0 = unlimited, §Settings registry). Aggregates and segments kept forever. Sweep at finalize + on demand (Tools → DB maintenance, which also offers `PRAGMA integrity_check`, `VACUUM`, backup).

> **Built 2026-09-15 (P4 PR-7).** The on-demand sweep is the **Agent's**: the App sends `SweepRetention` (`07_IPC`),
> and the Agent runs `ISessionRepository.SweepRetentionAllAsync` — the finalize's per-game rule for every game in one
> transaction, `ROW_NUMBER() OVER (PARTITION BY game_id ORDER BY started_at DESC, id DESC)`, so the two can never
> disagree about which session is the Nth, and a removed game's kept sessions are swept like any other. A keep of 0
> never reaches it: unlimited is honoured by not sweeping. The other three run in the App through
> `Infrastructure.Persistence.LedgerMaintenance` and change no row: `PRAGMA integrity_check`; a backup through
> `VACUUM INTO` (a complete, consistent copy while the Agent keeps writing; never over an existing file and never onto
> the ledger, its WAL or its shared-memory file — the save dialog's "replace?" would otherwise have deleted the live
> database); and compaction, `VACUUM` + `wal_checkpoint(TRUNCATE)`, refused while a fresh `GetStatus` reports a
> session. `LedgerDatabase.MaintainAsync` is the one way to run a statement outside a transaction, and it is for
> these statements only.

## Migrations

Sequential embedded SQL (`Migrations/0001_init.sql`, `0002_*.sql`, …), applied at startup by whichever process opens the DB first, guarded by `schema_migrations` + a named mutex. Never edit an applied script; only append.

> **`0002_annotation_overrides_and_removal.sql` (P3 PR-3, 2026-09-13) — the first append**, and the first
> time the runner ran two scripts on a real file: `ALTER TABLE ADD COLUMN` only — the three override
> columns on `session_annotations` and `games.removed_at`. `LedgerDatabaseTests` asserts one
> `schema_migrations` row per script, so `LatestVersion` is 2.

> **`0002_annotation_overrides_and_removal.sql` (P3 PR-3, 2026-09-13) — the first append**, and the first
> time the runner ran two scripts on a real file: `ALTER TABLE ADD COLUMN` only — the three override
> columns on `session_annotations` and `games.removed_at`. `LedgerDatabaseTests` asserts one
> `schema_migrations` row per script, so `LatestVersion` is 2.

> **`0003_fg_refusal.sql` (2026-09-14) — `sessions.fg_refusal`.** The owner's ledger held two hooked rows with
> `fg_mode = 'dlssg'`, `fg_source = 'api'` and `fg_factor` NULL — the identity stood, the count refused — and
> nothing recorded which refusal; every FG surface said N/A beside a detection that had succeeded. One
> `ALTER TABLE ADD COLUMN`, written by the Agent (`SessionAggregator.ApplyFg`), read by the UI for the "factor not
> counted" tooltip (`08_UI` §FPS display rule). `LatestVersion` is 3; `LedgerDatabaseTests` asserts the column.

> **`0004_detection_cache.sql` (P4 PR-1, 2026-09-14) — `games.detection_exe_size_bytes` / `detection_exe_mtime_ms`.**
> The detection cache key's exe half (`05_DETECTION` §Caching), apart from `exe_size_bytes` / `exe_mtime_ms`,
> which are the consent fingerprint the gate reads and which a background scan must never refresh.
> `LatestVersion` is 4; `LedgerDatabaseTests` asserts the columns.

> **`0005_fg_refusal_detail.sql` (2026-09-16) — `sessions.fg_refusal_detail`.** 0003 recorded WHICH refusal took the
> factor; the owner's Dashboard then showed "DLSS-G active — factor not counted" on four titles with a tooltip that
> could only say "the frame-generation state changed mid-session", while `FgWindow` had computed which of the eight
> buckets departed, its ratio and the window's, and `SessionAggregator` kept only the kind. The column is
> `Application.Recording.FgRefusalDetail` as JSON (`RecordingJsonContext`): kind, subject, count, bucket index/count,
> the bucket's ratio (null when it held no tokens — infinity is not JSON) and the window's. Written by the Agent
> beside `fg_refusal`, read by the UI's tooltip (`FpsPresentation.RefusalDetailText`), never an input to a number.
> Existing rows keep NULL. `LatestVersion` is 5; `LedgerDatabaseTests` asserts the column.

> **`0006_fg_steady_state.sql` (2026-09-17) — `sessions.fg_factor_scope` / `fg_steady_share`.** `fg_factor` was
> session-wide or NULL, and on real play it was almost always NULL (`03_METRICS` §The steady state). `session` = one
> state for the whole claimed span; `steady` = `fg_factor` / `native_fps` / `displayed_fps` describe the state the
> session spent most of its generating time in, `fg_steady_share` of the presenting time, with `fg_refusal` and its
> detail still set beside it. A reader that ignores the scope reads a steady factor as a session one, which is why
> every surface prints the share. Existing rows keep NULL. `LatestVersion` is 6.

> **`0007_guard_bypass.sql` (2026-09-21) and `0008_withdraw_guard_bypass.sql` (2026-09-22) — the first DROP.** 0007
> added five columns for the per-game guard override; the owner withdrew the feature a day later and 0008 drops them
> (`ALTER TABLE … DROP COLUMN`, SQLite ≥ 3.35 — `LedgerDatabaseTests` asserts the engine's version beside the columns'
> absence). "Never edit an applied script; only append" holds: 0007 stays as applied and 0008 is the append. The
> data dropped is the override acknowledgements themselves, which is the decision. `LatestVersion` is 8.

> **`0010_prescan_key.sql` (2026-09-25) — `games.hook_prescan_rules_version` / `hook_prescan_exe_size_bytes` /
> `hook_prescan_exe_mtime_ms`.** The Agent pre-scans every library entry for anti-cheat since beta.8, and again
> whenever the rules or the executable change, so it needs what each row was last scanned against — its own key, as
> 0004's columns are the detection sweep's, and apart from `exe_size_bytes` / `exe_mtime_ms` for the same reason
> (the consent fingerprint must never be refreshed by a background scan). `ALTER TABLE ADD COLUMN` only; existing
> rows read NULL, which the sweep reads as "never scanned" and scans on its first pass. `LatestVersion` is 10;
> `LedgerDatabaseTests.ScriptTenAddsThePreScanKeyEmptyForExistingRows` applies it from 9 to a ledger holding a row.

> **`0011_executable_facts.sql` (2026-09-25) — `games.exe_machine` / `exe_file_version` / `exe_product_version` /
> `library_versions`.** The owner asked for the game's version, its DLSS / FSR versions and 32- or 64-bit; the detection
> sweep read the PE version resource and walked the tree for the capability files, and kept neither. Four columns,
> written whole by that sweep (`05_DETECTION` §What the files say about themselves) — facts about files, so no
> provenance and no user override. Existing rows read NULL, and a NULL `exe_machine` makes the sweep read the row once
> more on its first pass. `LatestVersion` is 11; `LedgerDatabaseTests.ScriptElevenAddsTheExecutableFactsEmptyForExistingRows`.

> **Every open used to migrate, and one that should not have did — 2026-09-16.** The Agent's `--console sessions`,
> a verb that prints, was run against the owner's ledger while the file on disk was at schema 2 and applied 0003 and
> 0004 to it. `LedgerDatabase.OpenReadOnlyAsync` exists since that day: an existing file, `PRAGMA query_only`, no
> `MigrationRunner`, and a refusal (`LedgerSchemaException`) for a schema **older** than the build as well as newer —
> the older file needs the App or `--serve` to bring it forward, not a verb whose name says it reads. The Agent's
> `sessions`, `consent list` and `killswitch status` open that way (`Program.PrintsOnly` is the list, pinned by
> `AgentReadOnlyVerbsTests`), `db path` opens nothing, and the App's `--diag` does the same.
>
> **What that run found, recorded because the mechanism is not known.** The on-disk `ledger.db` was 94 KB, 23 pages,
> dated 14 September, at schema 2 with three test games and no sessions; the day's 41 imported games and 18 sessions
> were in `ledger.db-wal` — 4 MB, 1006 frames — and every one of those frames carried salt-1 one **lower** than the
> WAL header's, with a checkpoint sequence of 1 and a single frame under the new salt. That is the shape SQLite leaves
> after a checkpoint has moved every frame into the main file and the next writer restarts the log; but no checkpoint
> had reached this main file (645 pages of data, 23 on disk, and the file's own time stamp two days old). A fresh
> connection therefore recovered one frame and saw the 14 September state, while the App and Agent that were running
> saw the whole day through the wal-index they shared. Neither process closed cleanly enough to fix it: the Agent's
> stop at 17:57 and the console's exit at 17:58 both left the WAL in place, and `-wal`/`-shm` then vanished between
> 18:00 and 18:04 with no FrameLedger process alive. The data was rebuilt by applying the old-salt chain to the main
> file (176 commits, `integrity_check` ok) and the broken set kept beside it. **Nothing in this repository writes the
> main file behind SQLite's back, replaces it, or restarts the WAL by hand**; what the code can do about an unknown
> mechanism is make the next occurrence visible, and it does: `OpenAsync` runs `wal_checkpoint(PASSIVE)` after
> migrating and logs the frame count it found and moved (`LedgerDatabase.OpenDiagnostics`), and `DisposeAsync` runs
> `wal_checkpoint(TRUNCATE)` and logs that, so a clean close leaves the main file complete and a WAL that will not
> checkpoint is a logged number rather than a silent one. A WAL at open holding frames a passive checkpoint cannot
> move, with no other FrameLedger process running, is the signature to look for.

> **Built 2026-09-09 (P2 PR-B):** `Infrastructure.Persistence.MigrationRunner` over scripts embedded in
> the assembly (so the schema a build applies is the one it was tested against), one transaction per
> script, under `Local\FrameLedger.Ledger.Migrate` — session-local rather than `Global\`, because the
> Agent and the UI share a session and the global namespace asks for a privilege a standard user need
> not hold. *(One name per ledger FILE since 2026-09-17 — `Migrate.<hash of the path>`,
> `MigrationRunner.LockNameFor`: with one name for every ledger, a test process frozen mid-migration held
> every other process's scratch ledger, and seven App and Agent tests failed after 30 s on #199's run.)* **A ledger at a version newer than the build's scripts is refused, not read**
> (`LedgerSchemaException`): guessing at a schema we do not understand is the one option that could
> turn a newer file into wrong answers. `LedgerDatabase` opens with the four pragmas above, one
> connection per process behind a gate, and every write in an explicit transaction — a failing blob
> insert leaves no session row behind (`SqliteSessionRepositoryTests`).

**`0001_init.sql` creates the schema above directly.** There is no v1 → v2
upgrade path, because there is no released v1: nothing has shipped, so no
FrameLedger database exists anywhere in the world. Writing and testing a
migration from a schema that never existed is work with no user on the other end
of it. The `schema_migrations` machinery still ships from day one — it is what
makes the *next* migration safe, and `11_UPDATER` §Versioning already requires
migrations to cover every released `MAJOR-1`. That obligation begins at the first
release, not before it.

## Hardware change markers (FR-6.3)

Snapshot captured per session, hashed, deduped. Trend queries join consecutive sessions' snapshots; differing fields emit a marker `{after_session_id, field, old, new}`. GPU driver version now comes from the vendor API (`18_GPU_VENDOR_APIS`) rather than WMI, which makes it accurate enough to be worth charting.
