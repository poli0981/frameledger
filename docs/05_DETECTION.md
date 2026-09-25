# 05 — Detection

Detection now has **two clearly separated tiers**, and conflating them was the root cause of the accuracy problem this rewrite addresses.

| Tier | Source | Answers | Confidence |
|---|---|---|---|
| **Runtime facts** | API calls we hooked (`17_HOOK_ENGINE`) | What the game is *actually doing*: upscaler + quality, render/output resolution, FG, RT, present mode, HDR | **Measured** |
| **Static hints** | Files on disk, PE metadata, store manifests | What the game *is* and what it *could* support: engine, version, publisher, store, capability | Inference |

**Rule: a static hint may never set a runtime fact.** `nvngx_dlss.dll` existing on disk means the game ships DLSS. It does not mean DLSS is on. The old design blurred these and produced the field errors that motivated the rewrite. Static hints populate library metadata and *capability chips*; they never write `sessions.upscaler`, `fg_mode`, or the RT flags.

## Runtime facts (Tier 1 only)

Derived in the Agent from the record stream; the mapping is in `03_METRICS`. Summary of what each hook establishes:

| Fact | Established by |
|---|---|
| Graphics API + swapchain format, buffer count, swap effect, flags | `CreateSwapChain*` / present hooks |
| Present mode (flip / blt / independent flip) | swapchain desc + `presentFlags` + `syncInterval` |
| HDR output | `SetColorSpace1` |
| Upscaler identity + quality preset | NGX / Streamline / FFX / XeSS create+evaluate calls — **or, when no hook saw one, the NVIDIA driver's per-process NGX word, out of process and attributed (`03_METRICS` §Upscaling, the driver-reported rung, 2026-09-06): identity only, never the preset** |
| Render resolution vs output resolution (incl. mid-session changes) | upscaler parameter reads + `ResizeBuffers` |
| Frame Generation mode + factor | NGX/SL/FFX FG feature evaluation, then cadence — **Tier 1 only**. ~~then (Tier 2 only) PresentMon `FrameType`~~ retired (§S31 row P2), and Tier 2 no longer produces frames to run cadence over either |
| Ray Tracing active | AS builds + `DispatchRays` (both, to catch inline RayQuery) |
| Ray Reconstruction | NGX `RayReconstruction` feature evaluated |
| Reflex on + PC latency | NVAPI Reflex hooks |
| Per-process VRAM | `QueryVideoMemoryInfo` |
| PSO compilation events | pipeline-creation hooks |

> **Not built, 2026-09-15:** four rows above have no writer yet — HDR output, Reflex and PC latency, per-process
> VRAM, and PSO compilation events. The Overlay never sets their measured bits, so every calculator over them
> returns nothing and the columns stay null; `legal/ACCURACY.md` lists them as not measured. This table is the
> design; the block is the state.

> **"present-count delta" is removed from the FG row, and it was not merely unreliable —
> it was structurally zero.** `03_METRICS` §Frame Generation retired that rung on
> 2026-08-05: `IDXGISwapChain::GetFrameStatistics().PresentCount` counts presents *the
> application submitted through that swapchain*, i.e. the same events our present hook
> intercepts, so the difference is zero by construction and a metric that is always zero
> reads as "no frame generation" rather than as a failure. `17_HOOK_ENGINE` forbids
> re-adding it. This row went on naming it for ten days after the removal, which is the
> stale-by-not-being-touched failure this project keeps recording; `06_DATA_MODEL`'s
> `fg_source` enum carried the same value and is corrected in the same pass.

~~Tier-2 sessions have `upscaler = unknown`, resolutions `N/A`, RT `N/A`, and an FG mode only if cadence resolves it.~~ **Simpler since 2026-08-28: a Tier-2 session detects NOTHING.** It has no frames, so cadence has nothing to run over. `upscaler`, resolutions, RT and FG all read `N/A`, and the static-hint tier (below) is the only thing that says anything about such a title — which it labels *inference*, never measurement.

## Static hints — rules engine

Rules are **data, not code**: `rules/detection-rules.json`, bundled and updatable from the repo (raw GitHub URL, ETag-cached; manual "Update detection rules" in Tools + weekly auto-check). Schema:

```json
{
  "schemaVersion": 2,
  "rulesVersion": "2026.07.2",
  "engines":    [ { "id": "unity", "name": "Unity", "signals": [...], "version": {...} } ],
  "platforms":  [ ... ],
  "capabilities": [ ... ],
  "anticheat":  { "modules": [...], "drivers": [...], "blockedExecutables": [...], "blockedStoreIds": [...] }
}
```

The `anticheat` block is the same file that feeds the hard guard in `19_SAFETY` — shipping it as updatable data is what lets a newly-protected game be blocked without waiting for an app release. **Rules updates that touch the `anticheat` block are treated as security updates:** applied on next check regardless of the user's auto-update preference for other rules.

Signal types evaluated by `RuleEvaluator` (Domain): `file_exists`, `dir_exists`, `sibling_glob`, **`path_contains`**, `pe_company_contains`, `pe_product_contains`, `strings_contains` (bounded 8 MB scan), `manifest_field`. Combine with `all` / `any` — **not nested**, which schemaVersion 2 forbids. Version extractors: `pe_file_version` (of the sibling its `from` names, not of the executable), `pe_product_version_regex`, **`strings_regex`**, `manifest_field`.

> `path_contains` and `strings_regex` were in the shipped data and the schema
> and missing from this list; the schema's own `$comment` said so and nobody had
> made the edit.

**Every signal is three-valued.** `Match` / `NoMatch` / **`Unknown`**, and `Unknown` is what the evaluator returns whenever the probe could not establish that class of fact — a PE that would not read, a strings pass that did not finish, a bound that stopped the walk. A signal the probe never answered must never evaluate to `false`: that is the same collapse of "could not look" into "looked and it was clean" that `19_SAFETY` exists to prevent, applied to inference. A group is `Unknown` unless the signals it *did* read already decide it, and **an engine rule that evaluates `Unknown` stops the ordered walk** rather than falling through — otherwise a later rule gets reported as the first match when it was not.

**The evaluator does no I/O, but it is not a pure function of the directory.** It reads a `GameFileSnapshot` collected in one pass, and that snapshot is *rules-dependent*: the strings scan has to know which needles and regexes to look for before it reads. Worth stating because the tidier claim — "a pure evaluator over an arbitrary directory" — is what the design looks like from a distance and is not what it is.

**How the rules are checked, precisely:**

- `tools/rules-validate.ps1` checks the schema (with a canary, because `Test-Json` fails open on a malformed one), the imperative constraints a schema cannot express, the parser's capacity bounds, and **fixture coverage** — every engine and platform id has a fixture directory, and every fixture corresponds to a live id.
- **The evaluation is `RuleFixtureCorpusTests`** (`FrameLedger.Infrastructure.Tests`), which runs the real `RuleEvaluator` through the real `GameFileProbe` against `tests/fixtures/rules/**`. It runs under `build.ps1 check`, and therefore in CI.

> This section previously said the validator "runs rules against fixture trees in
> CI". It never evaluated a rule, and there were no fixture trees — a gate
> described in a normative document and implemented nowhere. `13_CI_CD` carried
> the same claim.

### Trust and staleness of the rules feed

`20_OPEN_QUESTIONS` §S4. The file is fetched over HTTPS from a raw GitHub URL.
Three rules, because a gate whose data can silently go stale is a gate with an
expiry date nobody sees:

- **The Agent reads rules from exactly one place:** its own Local AppData
  (`FrameLedger\rules\`). The source is not a parameter and cannot be redirected
  over the pipe (`07_IPC` §The pipe is not a trust boundary).

  > **This sentence was true of the pipe and false of the environment, and the
  > gap is §S21.** The native guard built that path from
  > `_dupenv_s("LOCALAPPDATA")` — an inherited variable, so whoever launched the
  > process chose the file, per launch, leaving nothing on disk. It now resolves
  > through `SHGetKnownFolderPath(FOLDERID_LocalAppData)`.
  >
  > Stated honestly rather than upgraded to a guarantee: shell resolution still
  > goes through the user's own shell-folder registration, so a user can still
  > move their Local AppData. What it removes is the **per-launch, per-process**
  > vector; redirection is now a persistent, machine-wide change affecting every
  > application. The thing that makes the residual harmless is the compiled-in
  > blocklist floor (`19_SAFETY` §The floor data cannot remove), not the path
  > resolution.
  >
  > **The residual is measured, not assumed** (2026-08-04):
  > `HKCU\…\Explorer\User Shell Folders\Local AppData` matches what the API
  > returns, and that key is `FullControl` for the current user with no
  > elevation. It really is relocatable — by a persistent change affecting every
  > application, which is a different proposition from an inherited variable, and
  > that difference is the whole claim being made here.
- **A fetched file replaces the local copy only if it validates.** Same
  structural checks `tools/rules-validate.ps1` runs, including the non-empty
  `anticheat` requirement. A malformed, truncated or empty-blocklist download is
  discarded and the **last valid copy is kept** — never cleared, never partially
  applied.
- **Staleness warns; it never disables.** Past N days without a successful
  check, the UI says so plainly. It must **never** be wired to relax or disable
  the blocklist: "the rules are old" is an argument for more caution, not less,
  and an expiry that weakens a gate is an override with a timer on it.

Signing the feed is **not** decided. HTTPS authenticates the host, not the
content, and a signature would authenticate the content. It is deferred rather
than dismissed: the app ships a seed blocklist that validates locally, so a
compromised feed can be *rejected* by the rules above but not *proven genuine*.
Recorded as a residual risk, not as a solved problem.

### Engine signatures

| Engine | Signals | Version |
|---|---|---|
| Unity | `UnityPlayer.dll` sibling **or** `<Exe>_Data/` dir | FileVersion of `UnityPlayer.dll` |
| Unreal 4/5 | exe matches `*-Win64-Shipping.exe` **or** `*/Content/Paks/*.pak` | ProductVersion regex `\+\+UE(4\|5)\+Release-(\d+\.\d+)` |
| Godot | `.pck` sibling **or** `strings_contains("Godot Engine v")` | strings regex `Godot Engine v(\d+\.\d+[\.\d]*)` |
| GameMaker | `data.win` | `N/A` |
| RPG Maker MV/MZ | `nw.dll` **and** `package.json` siblings (since 2026-09-16) | `N/A` — the `.js` header needs a `from` that `strings_regex` lacks |
| RPG Maker XP/VX/VX Ace | `RGSS1*`/`RGSS2*`/`RGSS3*.dll` sibling **or** a `*.rgssad`/`*.rgss2a`/`*.rgss3a` archive (since 2026-09-16) | `N/A` — one rule, the variant unnamed |
| RE Engine (Capcom) | `re_chunk_000.pak` sibling, the exact name (since 2026-09-23) | `N/A` |
| FromSoftware | a `*.bhd` **and** a `*.bdt` sibling (since 2026-09-23) | `N/A` |
| Ren'Py | `renpy/` dir **or** `*.rpa` | `renpy/__init__` strings / `log.txt` first line |
| CryEngine | `CrySystem.dll` | FileVersion |
| Source | `gameinfo.txt` + `bin/engine.dll` | `N/A` |
| Unknown | fallback | — |

Order matters (first match wins). Engine is user-overridable.

**The array order in `detection-rules.json` *is* that precedence**, and the only thing in the repository that notices a reorder is `tests/fixtures/rules/ordering/unity_markers_with_ue_structure` — a directory carrying Unity markers *and* Unreal structure, which is the case `14_TESTING` names.

> **Both RPG Maker rows were ⛔ "cannot be expressed in schemaVersion 2" until 2026-09-16, and the reasoning was
> half right.** It said MV/MZ needs `nw.dll` **and** (`www/` **or** `package.json`), a nested group v2 refuses. But
> MV and MZ both ship `package.json` beside `nw.dll` unconditionally — `www/` is only where MV keeps its data — so
> `all: [nw.dll, package.json]` is the whole signal and needs no nesting. Measured on the owner's *Flower in Us*
> (Steam) and a hand-extracted MV title, both `Game.exe` + `nw.dll` + `package.json` (+ `www/`), both `engine=none`
> until this rule. The RGSS family is one rule, `rpgmaker_rgss` "RPG Maker XP/VX/VX Ace", `any` over the three DLL
> prefixes and the three archive extensions, `version: null` — the variant is not named, which is the product
> decision the old note deferred, taken as "one engine, no version" rather than three rules. **What still cannot be
> expressed** is a version for either: MV/MZ's lives in a sibling `.js` header and `strings_regex` has no `from`;
> RGSS's is which DLL matched. Both rules sit after `unity` and `unreal` and before `godot` (an MV game has no
> `.pck`; a Godot game has no `nw.dll`), with fixtures `engines/rpgmaker_mv` and `engines/rpgmaker_rgss`, and the
> `every_engine_marker` canary carries both engines' markers and still expects `unity`. `rulesVersion` is
> `2026.09.1`, so the sweep's cache key changes and every game is re-detected on the next pass.

> **RE Engine and FromSoftware, 2026-09-23 (owner request, "mang tính tương đối" — a relative signal, not a proof).**
> Both are file-layout signals, checked on the owner's disk before they were written. **RE Engine:** every Capcom RE
> Engine title ships its content as `re_chunk_000.pak` beside the executable, with patches as
> `re_chunk_000.pak.patch_00N.pak` (RESIDENT EVIL 2 and the Onimusha demo). The rule names the base archive exactly — a
> glob without a star is the whole leaf name — so a patch archive alone is not it. **FromSoftware:** the studio's
> titles keep their data as `.bhd` header + `.bdt` data pairs beside the executable — DARK SOULS II
> (`GameDataEbl.bhd/.bdt`), DARK SOULS III (`Data0`–`Data5`), ELDEN RING (`Data0`–`Data3`, plus `DLC`) and Sekiro
> (`Data1`–`Data5`). The request described the second extension as `.bht`; on disk it is **`.bdt`**. An `all` group
> over the two globs — half a pair is not a pair. The request also described a `<Game>\Game\` layout; it is not a
> signal here, because Sekiro has no `Game\` folder and "the parent folder is named X" is not a signal type anyway.
> None of the owner's other 80 Steam titles carries either marker. Both rules sit after the RPG Maker rules and
> before `godot`, whose `*.pck` would also match a loose Wwise sound bank (an `Unknown` there would stop the walk
> before these rules). Fixtures `engines/re_engine` and `engines/fromsoftware`; the `every_engine_marker` canary
> carries both engines' markers and still expects `unity`. `rulesVersion` is `2026.09.2`, so every stored game is
> re-detected on the next sweep. Engine detection never feeds the guard: ELDEN RING stays refused by its anti-cheat
> whatever engine it is shown as. **Engines are shown by name since the same date** (`Formats.Engine` in the App —
> proper nouns, not resources; an unknown id, or one the user typed, passes through): the game card and page used
> to show the raw id (`rpgmaker_mv`).

⛔ *(kept for the record — the two rows above were the ones this note described)* **Two rows cannot be expressed in schemaVersion 2**, and are documented rather than half-implemented — a rule that exists but never fires is worse than one that is absent, because it reads as coverage:

- ~~**RPG Maker MV/MZ.** The signal is `nw.dll` **and** (`www/` **or** `package.json`) — a nested group, and `signalGroup` sets `maxProperties: 1` with its own `$comment` saying nesting is unsupported in v2.~~ Its version is a header inside a sibling `.js`, and `strings_regex` carries no `from`, so it cannot be aimed at a file other than the executable.
- ~~**RPG Maker XP/VX/VXAce.** The signals *are* expressible (`any` over the three `RGSS*` prefixes), but the version is "which of them matched" — an answer no extractor produces. Splitting it into three engine rules with `version: null` and the variant in the display name would work and is a product decision about how the engine reads in the UI, not a mechanical fill-in.~~

Everything else in the table is in the data and has a fixture; `rules-validate.ps1` fails if a rule id has no fixture directory, or a fixture no rule.

### Platform signatures & metadata

| Platform | Signals | Metadata |
|---|---|---|
| Steam | `steam_api64.dll`/`steam_api.dll` sibling, or path contains `steamapps\common` | nearest `steamapps/appmanifest_<id>.acf` (walk up): `appid`, `name`, `buildid` → version |
| GOG | `goggame-<id>.info` sibling, or `Galaxy64.dll` | `.info` JSON: `gameId`, `name`, `version` |
| Epic | `EOSSDK-Win64-Shipping.dll`, or path under an Epic install | `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` matched by `InstallLocation` |
| itch.io | `.itch\receipt.json.gz` | **`%APPDATA%\itch\db\butler.db`** (`install_locations` + `caves`, butler's `verdict` names the executable) since 2026-09-16, then the receipt JSON: title, id |
| None/Manual | fallback | PE VersionInfo |

**Auto-import (FR-1.2):** Steam via `libraryfolders.vdf` → all `appmanifest_*.acf` except `SteamLibrarySource.KnownTools` (2026-09-22: Steamworks Common Redistributables, Borderless Gaming, Wallpaper Engine, SteamVR, the Proton and Steam Linux Runtime entries, Blender — tools Steam's manifests do not distinguish from games, and which became library rows the watcher recorded); GOG via `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\*`; Epic via `Manifests\*.item`; itch by receipt scan. Import presents a review checklist; nothing is launched, nothing is hooked on import.

> **Built 2026-09-14 (P4 PR-4).** `Application.Import.LibraryImporter` over four `IStoreLibrarySource` adapters in
> `Infrastructure.Import` — `SteamLibrarySource` (`libraryfolders.vdf` + `appmanifest_*.acf` through
> `ValveKeyValues`, the Steam root from `HKCU\Software\Valve\Steam\SteamPath`), `GogLibrarySource` (the HKLM
> keys, read-only, the first HKLM read in the tree), `EpicLibrarySource` (`*.item` JSON: `DisplayName`,
> `InstallLocation`, `LaunchExecutable`, `AppName`, `AppVersionString`), `ItchLibrarySource`
> (~~`%APPDATA%\itch\apps\*\.itch\receipt.json.gz`~~ — **since 2026-09-16 butler's database first**: the itch app
> records the install locations the user chose in `%APPDATA%\itch\db\butler.db`, and they need not be under
> `%APPDATA%` at all — on the owner's machine the one location was `D:\another\it` and `apps\` was empty, so the
> receipt scan returned nothing, silently; `install_locations` + `caves` give every install, butler's own `verdict`
> names the executable it launches, and `apps\` receipts are still scanned for what the database does not list. The
> database is copied to a temporary directory and the copy opened read-only, because the running app holds it in
> WAL mode. One log line says what was consulted: `import: itch — butler.db read: 1 location(s), 2 cave(s), …`).
> **Two stores do not name the game's executable** (Steam,
> itch-by-receipt): `ExecutableLocator` walks the install ~~three~~ **four** levels deep, drops the helpers by name
> fragment, everything under a Unity `X_Data\Plugins` folder and every redistributables/prerequisites folder, and then
> **ranks** what is left — ~~takes the largest~~ an executable the engine's own layout names as the game (Unity: `X.exe`
> beside `X_Data`; Unreal: `*-Win64-Shipping.exe`) beats one named after the install folder, which beats a stranger, and
> size decides only between equals. "The largest wins" was measured wrong 2026-09-21 on GIRLS' FRONTLINE 2: EXILIUM:
> the import chose `GF2_Exilium_Data\Plugins\ZFGameBrowser.exe` (1,082,944 bytes) over the 675,392-byte Unity player, and
> because the watcher, consent and the gate are keyed on the executable path, hooking enabled on that row could never
> match the game. The same pass fixed Red Dead Redemption 2 (the 140 MB launcher installer under `Redistributables`),
> Counter-Strike 2 (`vconsole2.exe`), Rune Factory (root bootstrapper vs the shipping binary) and Dying Light: The
> Beast (the game sits four levels down). Still a *guess the review list marks as such*, and since the same date the
> game page shows the row's executable and **Change executable…** re-points it (hooking off and unconsented afterwards,
> exactly as a new game; a block is never cleared by it). GOG's `exe` and Epic's `LaunchExecutable` are the store's
> own. The review (`ImportReviewPrompt`) ticks only what can be imported: a row already in the library or with no
> executable on disk cannot be. Every added row lands exactly as File ▸ Add game… lands it — hooking off — and
> the store's platform, id and version go through `IGameRepository.ApplyStoreMetadataAsync` under the same
> provenance rule as a detection write (a value the user typed is never overwritten). The table above says
> "walk up to the nearest manifest" for the per-game metadata extractors; those are still unbuilt
> (`GameFileProbe` keeps `manifest_field` uncollected) — the import reads the launchers' own indexes instead.
> **Privacy:** local files and registry keys the launchers already wrote, read-only; no process is touched, nothing
> is fetched (CLAUDE.md rule 8's "opt-in store metadata" is the online lookup, which does not exist). First-run
> step 4 now points at File ▸ Import library… rather than saying "later build".

**Publisher/version order:** store manifest → PE `CompanyName`/`ProductVersion` → *(opt-in)* Steam `appdetails` lookup, cached 7 days.

## Capability hints (explicitly labelled as such)

Files shipped with a game tell us what it *supports*. These populate a **"Supports"** row in the game header — visually distinct from the measured per-session chips, and never mixed with them.

- DLSS SR `nvngx_dlss.dll` · DLSS-G `nvngx_dlssg.dll` · **Ray Reconstruction `nvngx_dlssd.dll`**
- Streamline `sl.interposer.dll`, `sl.dlss_g.dll`, `sl.reflex.dll`
- FSR `ffx_fsr2_*.dll`, `ffx_frameinterpolation_*.dll`, `amd_fidelityfx_*.dll`, `ffx_api*.dll` — a hint still, even though `amd_fidelityfx_dx12.dll` / `_upscaler_dx12.dll` / `_framegeneration_dx12.dll` are *hooked* since 2026-09-04: the measured chip comes from the dispatch, never from the file
- XeSS `libxess.dll`, XeFG `libxess_fg.dll`
- DXR-capable: `d3d12.dll` usage + RT-capable GPU (capability only — says nothing about the game)
- **Vulkan (since P4 PR-2, 2026-09-14) — a code-level fact, not a rule.** The rules can only ask about files
  beside the executable, and the Vulkan loader is not one of them (`vulkan-1.dll` lives in System32). So
  `GameFileProbe` reads the executable's import and delay-load tables (`Infrastructure.Detection.PeImports`,
  bounded and read-only) and looks for the loader's name as an ASCII or UTF-16 string in the bounded scan;
  `StaticDetectionResult.UsesVulkan` is true / false / null (could not read), and the sweep stores a true as the
  id `vulkan` in `capability_flags` beside the rule ids. It is what `17_HOOK_ENGINE` §Vulkan's register-on-consent
  automation keys on, and what the game page shows as "Supports Vulkan". Never a claim about what a session
  presented — that is `sessions.api`, measured.

The UI wording is deliberate: **"Supports DLSS-G"** (capability, from files) versus **"Frame Generation: DLSS-G ×1.9"** (measured, from this session). Users conflating these is exactly the confusion the old design created.

## Anti-cheat pre-scan (static)

Before a game is ever launched with hooking enabled, the static scan checks for anti-cheat SDKs shipped alongside it (`EasyAntiCheat/` directory, BattlEye binaries, EOS anti-cheat components, etc.). A hit **disables the hooking toggle for that game entirely** in the UI, with an explanation — the user cannot enable it, so the guard never even has to fire at launch. Prevention beats interception.

**Implemented in the native guard, not here** (`fl_prescan.cpp`, exposed as ~~`FlStaticPreScan`~~ `FlStaticPreScanGame` since 2026-09-25, which takes the executable, resolves the install root itself and runs check 3 as well). It uses the same `MatchName` and the same rules file as the injection guard, matching directory names against the `anticheat.directories` group and file names against `anticheat.files`. Nothing managed matches a blocklist (§S15 item 1).

Three things this section previously implied that are not true, and are worth stating:

- **The UI answer is advisory.** It decides whether the toggle is *offered*; it does not gate injection. The same scan runs a second time inside the guard's chokepoint against a directory derived from the target's own pid, so "prevention beats interception" is a convenience, not the enforcement.
- **A hit disables the toggle; "could not scan" must not.** The scan is tri-state. `Reason::kPreScanFailed` — directory absent, unlistable, past a bound, or behind a reparse point — is *neither* a hit nor a pass. It must surface as "could not verify", distinct from "anti-cheat found": disabling the toggle on it would be a false refusal with no appeal, and clearing it would be a fail-open.
- **The token list is thin.** `directories` and `files` carry three tokens today. Widening needs verified names; a guessed token fails closed by never firing, which is a silent hole (`19_SAFETY` §Blocklist seed).

## Caching & privacy constraints

- Detection results cached per game; refreshed when exe timestamp/size changes or `rulesVersion` changes. Implemented as `DetectionCacheKey` (path + size + mtime + `rulesVersion`).
- **A re-run never overwrites a field the user supplied.** `games.field_provenance` records where each value came from; anything not marked `detected` is left alone, and an absent or unrecognised provenance reads as *user*. Without this rule the re-run triggered by every rules update silently clobbers every correction the user has ever made — a data-loss bug that surfaces weeks later on somebody else's machine. Stated here because no document said it before, and the safe default is the one that costs a badge rather than a value.
  > **Built 2026-09-14 (P4 PR-1), and two things this section did not say.** *When it runs:* the Agent's
  > `DetectionHostedService` (`--serve` only) sweeps the library on its own task — every 15 s, and at once
  > when `UpdateRules` re-seeds the rules file — through `Application.Detection.DetectionSweep`, which runs
  > `StaticGameDetector` over the real `GameFileProbe` for every game whose key is stale and persists through
  > `IGameRepository.ApplyDetectionAsync`. *A missing executable is asked one more question before it is skipped
  > (2026-09-22): is the same file — same size, same mtime — under exactly one other drive letter? When it is, the row
  > follows it (`ExecutableRelocator`, `19_SAFETY` §A moved drive is the same executable) and this very pass scans
  > it there; `DetectionSweepReport.Relocated` counts them.* A game the App adds is scanned on the next tick (the App writes the
  > row, the Agent reads the table); no Agent running means no scan, and the game page says "not scanned yet".
  > *The key's exe half is NOT `exe_size_bytes` / `exe_mtime_ms`:* those are the consent fingerprint the gate
  > reads (`SqliteGameConsentStore` refuses a mismatch while a block stands), so schema 0004 gave the key its own
  > `detection_exe_size_bytes` / `detection_exe_mtime_ms` beside `detection_rules_version`. *And the provenance
  > rule, applied per field in the repository:* a `user` entry, an unrecognised entry, or a value with no entry is
  > the user's and stays; a `detected` entry is refreshed; **an empty field with no entry has nothing to protect
  > and is filled and badged `detected`** — without that clause a freshly added game could never receive an
  > engine at all, since `EnsureAsync` writes no provenance. A null detected value erases nothing.
  > `capability_flags` is written whole every run (a JSON array of the rule ids — `dlss`, `dlss_g`, `dlss_rr`,
  > `streamline`, `fsr`, `xess`, `xefg`) because the Supports row has no user-edit surface and a stale "Supports
  > DLSS-G" after the DLL left is the confusion §Capability hints exists to prevent.
- Strings scans bounded to 8 MB, local only.
- Module enumeration uses read-only handles (`PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ`); never a handle with write access outside the injection call itself.
- No game memory is read for detection purposes — all runtime facts come from arguments to APIs we hooked (CLAUDE.md rule 4).
