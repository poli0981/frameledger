# 20 — Open questions

Defects and gaps found by auditing docs `01`–`19` that **cannot be closed by
editing prose**. Each needs either an empirical answer from the P0 spike or a
design decision. `15_ROADMAP` names `docs/spike-notes.md` as where P0 writes the
answers; this file holds the questions.

Rules for this document:

- An item leaves this file only when it is **answered**, not when it is
  rephrased. The answer goes in the owning doc, and the entry here is deleted
  with a pointer in the commit message.
- Every S-series item is a **safety** item and blocks the first real injection.
- ~~Nothing here is a known bug in shipped code — there is no code yet.~~ **No
  longer true, and the change matters for how this file reads.** There is a built
  guard, a Vulkan layer, a rules seeder and 11 registered ctests, and §S21 records
  a fail-open found in *shipped* guard code. Entries here are now a mix of open
  questions and recorded defects.
- **A ✅ entry is closed.** They are kept rather than deleted, with their
  reasoning, so counting entries overstates the open work — read the markers.
  *(Since 2026-10-19, beta.24, D66: kept in `docs/open-questions/closed-s.md` and `closed-h.md`, verbatim, with a row each in the tables at the end of their series below.)*

---

## Scope decisions already taken (recorded, not open)

> **2026-09-21 — the guard gained one override, by owner decision, and it reverses a sentence this project wrote in
> several places.** CLAUDE.md rule 2 said "There is no override switch ... Do not add one"; `19_SAFETY` said "There is no
> override"; `HookingConsentOutcome` said so in a doc comment. The owner asked for a per-game "bypass the guard" mode at
> the user's own risk, behind a warning the user must accept first, and chose the per-game form over a global switch and
> over one that kept kernel anti-cheat refused. It is built as `19_SAFETY` §The user's bypass specifies: per game, off by
> default, its own two-act disclosure, stamped by the Agent, overruling the guard's JUDGEMENT only, never changing HOW
> FrameLedger injects (rule 3), and marking every session it starts. **What stays open, and is the owner's:** whether a
> shipped build should offer it for the kernel-level families at all (the rules file has no severity and may not grow
> one, so that would be a code list); and the ja text, which ships as English until a reviewer signs. **Pre-committed
> withdrawal:** an injection reached under it without the disclosure having been shown, or a session under it that is
> not marked, removes the switch from the App in the next release.
>
> **2026-09-22 — withdrawn by the owner, for a reason the pre-commitment did not list.** Its first real use (ELDEN
> RING under Easy Anti-Cheat) reached `TargetUnreadable`: the switch could not do anything where it was wanted, and
> where it could inject it only exposed the user. The owner asked for the opposite: a finding turns the game's hooking
> off, wherever the guard makes it. `19_SAFETY` §What a finding does to the game is the specification; CLAUDE.md rule 2 is
> unstruck. The two open items above (kernel-level families, the ja text) are moot.
>
> **2026-09-26 — one narrow exception, by owner decision D33, with the 2026-09-22 withdrawal quoted back.** Beta.8's
> library sweep blocked a game FrameLedger had measured hooked (GIRLS' FRONTLINE 2: EXILIUM, NetEase Yidun `NEP2.dll`),
> and the owner chose an evidence-bound, per-game exception for anti-cheat that is user-mode throughout (`19_SAFETY`
> §The user-mode exception; CLAUDE.md rule 2's amendment). The two reasons D22 gave are answered: it is offered only
> where FrameLedger has already measured the game hooked, and its risk is stated in its own disclosure. *(2026-10-03,
> D38: the first of the two no longer holds as written — a game blocked at add time could never be measured, so the
> measurement moved under the grant as a two-session trial whose failure is for good.)* **What stays
> open, and is the owner's:** the `ja` text of `Safety_Exception_*`, which ships as English until a reviewer signs;
> whether a family the rules file makes kernel-level later should end existing exceptions at once (today it does, at
> the sweep's next pass and at every session start); and a real-hardware check that NEP2.dll's load time relative to
> the injection is covered on the owner's machine (the ctest decoy covers the late load).

| # | Decision | Consequence |
|---|---|---|
| — | **D3D9 is not a Tier-1 API in v1.** The Overlay is x64-only; an x64 DLL cannot load into a 32-bit process, and D3D9 titles are almost entirely 32-bit | **The consequence hardened on 2026-08-28 without a word of this row changing**, which is why it is called out: that catalogue used to be "Tier 2", meaning frame times without injection. Tier 2 now measures nothing, so those titles are **unmeasurable in v1**. The VN / JRPG / older-indie catalogue is Tier 2. Reversing this means a second 32-bit Overlay **and** injector, doubling the native build matrix and adding a second struct-mirror surface. Revisit only with evidence that users care more about those titles than about the maintenance cost |
| — | **`ci.yml` is repo-local**, not a caller stub | The ops repo's `reusable-desktop-csharp.yml` runs `dotnet` directly with no native pre-step input, and `12_BUILD` requires CI and local to run the same script |
| — | **No `v1 → v2` migration** | Nothing shipped, so no such database exists. `0001_init.sql` creates the current schema |
| — | ~~**Multi-process titles are refused as `TargetAmbiguous` in v1**~~ — **the Chromium case is RESOLVED the same day (2026-09-03), by the vendor's own label** | Chromium-based runtimes — NW.js, Electron, RPG Maker MV/MZ, a large share of the VN catalogue — run several processes from ONE image path (browser, renderer, GPU), and *Flower in Us* refused as `TargetAmbiguous`. The presenting process is Chromium's GPU process, which owns no window, so "the one with the window" and "the parent" were both wrong picks. **What resolves it:** Chromium marks that process with `--type=gpu-process` on its own command line. `TargetResolver` now reads each candidate's command line through `NtQueryInformationProcess(ProcessCommandLineInformation)` — **served by the kernel, no `ReadProcessMemory`, no PEB walk, `PROCESS_QUERY_LIMITED_INFORMATION` only**, which is the rule-4 line `HeldProcessHandle` already draws — and resolves only when **exactly one** readable candidate carries the flag as a whole argument. Two GPU processes (two instances), none, or any unreadable candidate still refuse. §S27 is untouched: consent is keyed on the path, every candidate is that path, the guard scans the pid chosen, and `--pid` stays forbidden. **Measured the same evening, and the first rule did not fire:** *Flower in Us* runs **six** processes — browser, `crashpad-handler`, three `utility`, `renderer` — and **no `--type=gpu-process` at all**. NW.js runs the GPU **in-process, in the browser**, which is the one process Chromium leaves untyped. Second rule added: exactly one untyped candidate, every other candidate typed, no GPU process ⇒ the browser is the target; two untyped candidates are two instances and refuse. The refusal line now prints the tree (`browser=1, renderer=1, utility=3, …`) so the next unknown shape is data rather than a bare `TargetAmbiguous`. ~~**Still unmeasured:**~~ **Measured 2026-09-04** (`spike-notes` §9, run 3): the Overlay loads and sees presents inside that browser process — Flower in Us resolved, injected, captured, 3,587 records over three swap chains *(corrected 2026-10-04)* |
| — | **Intel XeSS / XeFG identity is closed at the census — no hook on any `libxess*.dll` export** (decided 2026-09-04, delegated by the owner) | The XeSS SDK failed `18_GPU_VENDOR_APIS` §Checklist (Intel Simplified Software License: binary-only grant, no reverse engineering, termination; headers forbid copying), so no declaration may be vendored or re-declared. The signature-free counter that seemed to survive that — an assembly thunk on a named export — still inline-patches Intel's code inside the game process, which the same licence forbids as *modification*; it would also be the only entry outside `FL_HOOK_GUARD` and a second build language, for identity alone. An Intel title therefore reports upscaler `NOT_REPORTED` and, with `libxess_fg.dll` in the census, Presented FPS under rule 6's *"MAY include generated frames"* qualifier — honest, and the same answer NGX-direct got. **Reversal condition:** Intel relicenses the headers so the checklist clears; then §H11's AMD route applies to Intel verbatim |
| — | **A title whose upscaler or frame generation is compiled into its executable is Presented FPS + the census's qualifier, in attach mode, and that is the ceiling for IDENTITY** (recorded 2026-09-04; **two such titles found the same night — Rune Factory, and Black Myth: Wukong for its upscaling, which ships and loads the 1.1.x monolith and never dispatches to it**) | No module for the census, no export for the inventory: nothing by name. What survives static linking is behaviour on interfaces we hook — the FSR3 frame-generation proxy swapchain at *creation* (launch mode only, §S1 deferred) and queue-side cadence with no vendor name on it. ~~**No installed title has this shape**~~ — **Rune Factory: Guardians of Azuma has it**: `amd_fidelityfx_dx12.dll` ships beside the executable and is never loaded (the census has no AMD module), FSR frame generation is on, and the report read `59.99 → 119.95 (×2 FG)`, *technology not identified* — because on a UE5 title **Streamline's frame token is requested whether or not NVIDIA's features are the ones running**, so the application-frame COUNT survives static linking even though the identity cannot. The ceiling is therefore lower than this row first said: the trio with an unidentified technology, not Presented FPS alone (§H11's run table). Identity for such a title stays out of reach by name |
| — | ~~**Tier 2 requires an elevated Agent**~~ | **CLOSED 2026-08-28 by the two-rung ladder.** The requirement came from Windows restricting ETW trace sessions; there is no ETW rung, so **no capture tier needs elevation**. Elevation is optional everywhere and buys exactly two things (CPU temperatures via PawnIO, attaching to elevated targets — ADR-9). An unelevated Agent whose Tier-1 attempt fails lands on Tier 2, which is what every unhooked session lands on regardless of privilege |

---

## S — Safety. Blocks the first real injection.

The project's central promise is that injection is opt-in per game, the guard is
a hard gate with **no override anywhere**, and no evasion is ever implemented.
Each item below is a place where the documents themselves leak a gap.

### S24 · The S-series ledger — read this before planning against the entries below

P0 exit criterion 2 (`spike-notes.md` §Exit criteria) is *"every S-series item
resolved, **or explicitly deferred with a written rationale**"*. That is not
auditable by reading 1,700 lines and counting ✅ marks — the markers are
load-bearing but they distinguish two states where the criterion needs four.
This table is the index; the entries remain the authority.

Added 2026-08-05. **Keep it current in the same PR that changes an item's state**,
or it becomes the next stale status claim this file exists to record.

> **The glyph legend, and it is the whole point of this table.** Unified 2026-08-27;
> before that, one disposition wore three marks and the criterion could not be audited
> by counting them, which is the only thing the table is for.
>
> | Mark | Means | Counts against exit criterion 2? |
> |---|---|---|
> | ✅ | resolved | no |
> | 🅓 | deferred, with a written rationale | no — the criterion allows this explicitly |
> | ◐ | partly closed | **yes** |
> | ⏳ | open, sequenced behind other work | **yes** |
> | ❓ | open, disposition undecided | **yes** |
> | 🔴 | open defect, found by an audit | **yes** |
> | 🚫 | owner-only — no PR can close it | counted separately, below |

| Item | State | Note |
|---|---|---|
| S3, S5, S7, S8, S9, S10, S11, S15, S16, S17, S18, S21, S22 | ✅ **resolved** | Thirteen. Reasoning kept in place rather than deleted |
| **S25, S26** | ✅ **resolved 2026-08-05** | The two runtime stops unreachable in a non-presenting process; occlusion probes recorded as frames |
| **S28** | ✅ **resolved 2026-08-05** | The guard's entry points share process-wide statics; a concurrent call cleared the blocklist mid-match and every check fell through to `Allow`. Reproduced by CI, not argued |
| **S27** | ✅ **resolved, and restated 2026-08-06** | The chokepoint is the anti-cheat gate, not the consent gate. It was closed by *not building* a capture host; item 1 built one, and ~~the ✅ survives on the clause that was always load-bearing — **no injecting entry point on any SHIPPED binary**. `FrameLedger.CaptureHost` is unpublished, and `tools/package-closure-check.ps1` makes that a gate rather than a fact about today's csproj.~~ *(Corrected 2026-10-04: ✅ restated 2026-09-10 in the entry below — the shipped Agent injects by design, and the four properties in the entry hold rule 1; `package-closure-check` keeps its meaning for the CaptureHost only.)* `HookRequest` is get-only behind one factory, so this entry's own rejected synthesis no longer compiles |
| S12 | 🅓 **deferred, rationale written** | Cautious mode → v1.1; it disabled nothing in v1. *(Marked ✅ until 2026-08-27 — same disposition, different glyph.)* |
| **S1** | ◐ **launch mode built as "inject late" 2026-09-06 (P1 item 2); the cost is now measured per title** | `FlGuardedInjectWhenReady` polls until a presentation runtime is mapped, then runs the full guard; `dxgiPresentsBeforeHook` and the reported wait are the input the 2026-08-05 deferral lacked. The ruling "inject late vs. no launch mode" is answered by construction — inject late exists — and whether it is *preferred* per title is what the numbers decide |
| **S13(c)** | ✅ **salvageable, and salvaged 2026-09-06** | The externally observable proxy the entry proposed is what was built; the missing input is produced by every launched session |
| **S19(b)** | 🅓 **deferred, rationale written — and MEASURED** | CI 2026-08-05: the fragment fires on `System.Security.Cryptography.ProtectedData.dll` loaded by a .NET **test host**, i.e. inside a real scan set in the launch-mode arrangement — refusing our own injection. The entry's "plausible and unmeasured" is superseded; the deferral rationale (a `CryptCATAdmin*` PR doing network I/O inside the hard gate, NFR-10) still stands. *(Marked 🔴 until 2026-08-27. The measurement belongs in this cell, not in the marker — a glyph that means "deferred" and a glyph that means "open defect" cannot be the same one in a table audited by counting.)* |
| **S14** | ✅ **both halves wired and seeded 2026-09-25** (exe half 2026-08-05) | ~~The store-id half's only in-policy route is P4's platform metadata extractors feeding a guard-side resolver~~ — built as a guard-side seam that reads Steam's `appmanifest_*.acf` and GOG's `goggame-<id>.info` itself; an install no store names is not refused, unreadable store metadata is. 30 Steam ids and 33 executables listed (rules 2026.09.4), floored; 33 Steam ids since 2026-10-03 (rules 2026.10.1: three CrackProof titles, `19_SAFETY` check 3). Epic has no reader |
| **S23-1** | ✅ **resolved 2026-08-05** | `FlGuardBuildId` gives the Agent a build id of its own, and `ShmHandshakeValidator` performs the comparison `07_IPC` and `04_CAPTURE` both specify. Every refusal path is driven, including **both ids empty** — the shape the feature shipped in, where `"" == ""` reported `Ok` for every process on the machine |
| **S23-4** | ✅ **resolved 2026-08-05** | `19_SAFETY` §During a session said "the module scan and the driver scan"; `EvaluateImpl` runs four. Reworded to "every pre-injection check" so it cannot go stale when a check is added, with the two omissions named — `services` is the only tier measured firing on real anti-cheat, and the pre-scan is the only one touching the filesystem |
| **S2 part three** | ✅ **built 2026-09-06 (P1 item 3)** | In-layer supervision on the present path, landed with `vkQueuePresentKHR`; `unhookRequested` and the tick deadline both stop the layer (passthrough with the reason on the mapping), proven by a fake loader chain and a 1.5 s test-only flavour of the DLL |
| **S4 signing** | 🅓 **deferred ~~to P4~~, unscheduled — P4 closed without it (corrected 2026-10-04); rationale written 2026-09-06** | Signing is a property of the feed and there is no feed (§S20); every rules file today shipped inside a release whose checksum attests it. The P4 decision (compiled-in key, detached signature, fail-closed) is written in the entry |
| **S6** | ✅ **built 2026-09-06 (P1 item 1)** | The detour on `kernelbase!LoadLibraryExW` stops the Overlay from inside on an exact-floor MODULE match, within one present or one watchdog iteration, and wakes the lazy installers within milliseconds for an inventoried module; measured against the real Overlay in ctest `fl_guard` `[loader]`/`[S6]`. Exact names only — fragment and signer stay the host's 30 s scan, so the disclosure does not change |
| **S19(a)** | ✅ **resolved 2026-08-05** | `gameguard` could never fire — `guard` subsumed it. Removed; `rules-validate` now fails when any fragment contains another, so the class cannot return. Zero coverage lost: nProtect has its own named module family |
| **S19(c)** | ✅ **resolved 2026-09-06** | The fields are constants the schema pins and the guard never reads, because the policy is code by CLAUDE.md rule 2 and `19_SAFETY`; `rules-validate` now proves the schema rejects `action: "allow"` and `signerField: "CN"` (canary 3), so the data cannot say otherwise |
| **S19(d) residual** | ✅ **resolved 2026-08-05** | A second canary, **derived from the shipped document** rather than hand-written, carries `nameFragments: []` and must be rejected. The entry's stated consequence was overstated and is corrected in place: the generated floor would not have disappeared, `gen-ac-floor.ps1` hard-errors and the native build fails — what was unguarded is the *schema* half |
| **S20 feed half** | 🅓 **deferred ~~to P4~~, unscheduled — P4 closed without it (corrected 2026-10-04); rationale written 2026-09-06** | **FR-7.3 stays unmet and written as unmet**: the fetch needs a shipped Agent, §S4's signing and the privacy policy's rows, all P4; until then a rules edit ships with the next release, checksum-attested |
| **S23-3** | ~~🅓 **deferred to P3, rationale written 2026-09-06**~~ ~~🔴 **open: P3 shipped the refusal UI without it**~~ ✅ **closed in beta.13** — the events carry the guard's own reason and a `BlockedService` / `BlockedDriver` notice says the family is running on this PC (`08_UI` §Safety events) *(it was 🔴 from 2026-10-04: a machine-wide notice still read "… was detected in this game")* | `08_UI`'s refusal notice is wrong in the commonest case; the requirement reaches `08_UI` and the `Safety_*` keys in the PR that creates the first refusal UI — a string with no view would read as done |
| **S23-5** | ✅ **resolved 2026-08-05** | Closed the way §S23-4 closed the same class — by **removing** the restatement, not correcting it — and `rules-validate` now fails when any `$comment` enumerates checks again. The rule's own first version was scoped to the wrong object and could not fire; it now walks every `$comment` and fails if it finds none |
| **S23-6** | ✅ **closed 2026-09-06** | `license-check` binds `THIRD_PARTY_NOTICES` bidirectionally, and the accuracy blocks are now ONE text — `legal/ACCURACY.md`, embedded verbatim in `README.md` and `DISCLAIMER.md` §4, gated by `tools/accuracy-check.ps1` (self-tested both directions). Truth stays a human's job; drift between copies no longer is |
| **S23-2** | 🚫 **owner only — no PR can close it** | `Rules / validate` is **not** a required status check on `main`. **Re-verified against the live branch protection 2026-08-05**, not repeated from the entry: `required_status_checks.contexts` is exactly `["check", "analyze (csharp, none)", "analyze (cpp, manual)"]` (`strict: true`, `enforce_admins: true`, `required_linear_history: true`). The `validate` job is path-filtered and `pull_request`-only, so a red removal check does not block the merge button. **The gate that exists to make the anti-cheat blocklist un-removable is advisory.** This is a branch-protection setting |

| **S29** | 🅓 **six ✅ and one 🅓 on 2026-09-06 — nothing 🔴 remains** | (a) ✅ **closed 2026-09-06**: the managed drain is in the merge gate — §S19(b)'s signer half removed the refusal and `ci.yml` dropped `-SkipIntegration` in the same PR, both mechanisms at once, as the sharpening below said they had to be. *(History: ◐ CORRECTED — the honesty assertion *was* in the merge gate natively (`fl_guard`, 20.58 s on CI); only the **managed** drain was ungated, and §S19(b) was **not** a prerequisite of the feature hooks. Sharpened 2026-08-06: fixing §S19(b) alone would still have gated nothing, because `build.ps1`'s `-SkipIntegration` applied `--filter 'Category!=Integration'` before the guard was ever asked.)* (b) ✅ `fl_vtable_indices` now pins the Overlay's indices through a shared header; (c) ✅ **closed 2026-08-06 by deleting `ShouldUnhookAsync`** — zero production callers, strictly weaker than `ScanOnceAsync`, and inverted in polarity; the gate's public instance surface is now pinned to `{StartAsync}`; (d) 🅓 **deferred 2026-09-06 on a measurement** — `ci.yml` now prints a Vulkan runtime census, and the script runs as a CI step the day a runner has a loader; until then it stays hand-run before any layer PR, with the P1 Vulkan entry as the reminder (the pre-commitment in the entry covers both rows); (e) ✅ **closed 2026-08-06 host-side**, with a held process handle and a classifier that takes no elapsed-time parameter, so a frozen `writeIndex` can never end a session; (f) ✅ **closed 2026-08-20**: the normative contradiction between CLAUDE.md rule 7 and `03_METRICS` about inline RayQuery is settled the way the entry proposed — AS-build activity proves ray tracing is happening, *naming the technique* is what needs a DXIL scan — and rule 7 is amended in the PR that wrote the hooks. Its second half closes with it: `FL_MEASURED_RT` now has a producer (both command-list detours, installed off the game's own device), so all three conjuncts of the `No` branch are live and **`Yes` and `No` are both reachable for the first time**, proved by injection in both directions; (g) ✅ the present-only writer claimed `FL_MEASURED_OUTPUT_RES` unconditionally, including on records with no size |
| **S30** | ✅ **closed 2026-08-15** | Answered on the title that raised it: with Ray Reconstruction on, Cyberpunk 2077 evaluates `kFeatureDLSS_RR` on every application frame and `kFeatureDLSS` **not once**, so RR was doing the upscaling and the decode had no arm for it. Evidence rather than inference: `renderW/H` are published only on a frame that drained an evaluation and read 1485×835 = 0.58 × 2560×1440, so the scaling-input tag arrives ON the RR evaluation. Two further defects fell out — the pre-committed decision table had two holes, and the fix contaminated the census that found it (it derived the id from the decoded byte) |
| **S31** | ✅ **resolved 2026-09-04 — row P1, and `tokens/batch = 1.00`** | Is a drained Streamline batch an application frame? **Yes.** The `slGetNewFrameToken` count (#103, #105) reads 1.00 / 2.99 / 3.99 against Cyberpunk's own off / ×3 / ×4 and 4.00 on Hell Is Us, and on every leg with batches the two counts agree to the frame. Two independent per-frame API counts are the oracle four instruments could not be. `fg_factor` is published; `none` is reachable by counting. *(The row as it stood while open:)* ◐ **row P2, 2026-08-27 — the ORACLE is retired, the QUESTION is not** | Is a drained Streamline batch an application frame? The PresentMon 2.x `FrameType` test, with its decision table pre-committed BEFORE the run and `tools/frametype-oracle.ps1` producing the input as two dimensionless ratios. **Two obstacles measured before it could be run**: the console binary will not start a trace session unelevated on the dev box (exit 6; the account is in neither Administrators nor Performance Log Users, and the running shared service does not help), and `--track_frame_type` is a **beta** option whose own help says it needs application or driver instrumentation of the Intel-PresentMon provider — so it may be as unavailable as `fl-baseline-probe` proved to be. Two of the table's six rows retire it outright. Blocks HANDOFF item 3's producer decision |
| **H11** | ◐ **AMD ffx-api BUILT and RUN 2026-09-04 (A1–A3 accepted; the loader row added the same evening after three loader titles read silent); Intel closed at the census by licence; FSR 3.0 host route deferred to its own PR** | XeFG and FSR3-FG identity. The AMD half is measured on nine titles: the three ffx-api leaves AND the SDK 2.x loader are hooked (the loader turned out to be the game's entry on loader-shipping titles, with the leaves silent behind it), the vendored MIT headers (tag v2.3.0) decode the dispatch descriptor, and rows A1–A3 landed as pre-committed and **R6 closed on Dying Light: The Beast and KCD2 the same night** (`FSR (3.1 or 4 …)` / `FsrFg` / ×2 / 1.01 and `none` / 1.00 through the loader row). Wukong's FSR upscaling is compiled in (Rune Factory's shape; the title has no FSR frame generation), so nothing more is owed on the AMD half; the FSR 3.0 host route is the one deferred piece Intel's SDK failed the licence checklist, so XeSS/XeFG stay at the census (§Scope decisions). `ffx_fsr3_x64.dll` (FSR 3.0, Cyberpunk only here) ~~needs ten host headers from tag v1.1.4 and is deferred with that costing written~~ **built 2026-09-05** from tag `fsr3-v3.0.4` (the docs' `v1.1.4` was the wrong tag: twelve exports match 3.0.4, not 1.1.4's fifteen, and 1.1.4's descriptor appends fields after `renderSize`); one row, the host's UPSCALE; **rows B1–B4 owed** (below) |

~~**Six items are ❓ and one is 🚫.**~~ **Recounted 2026-08-05: TWELVE items block
exit criterion 2, not seven, and the undercount came from reading only the ❓ rows.**
The criterion is *"resolved, **or** explicitly deferred with a written rationale"* —
so ⏳ (open, sequenced) and ◐ (partly closed) are just as open as ❓, and each needs
either work or a rationale written down:

- ~~**six ❓**~~ ~~three ❓~~ ~~four ❓~~ **three ❓** — S6, S19(c), S20 feed half. ~~**S30**~~ ~~**S31**~~ *(S19(a),
  S19(d) residual and S23-5 resolved 2026-08-05, each by a mechanism rather than by a
  correction. **S30 closed 2026-08-15 and S31 opened 2026-08-20**, and this bullet went on
  naming S30 until 2026-08-27 — so the total below stayed right by coincidence rather than
  by count.)*
- **three ⏳** — S2 part three, S4 signing, S23-3 *(S2 part three built 2026-09-06 and S23-3 closed in beta.13 — noted 2026-10-04; S4 signing stays deferred)*
- ~~two ◐~~ ~~**three ◐**~~ **two ◐ (2026-09-04)** — S14, S23-6. ~~**S31**~~ *(moved out of ❓ on 2026-08-27 when its test landed on row P2; **resolved 2026-09-04** when the token count landed on row P1 with `tokens/batch = 1.00` — the question answered by a second count rather than by an oracle)*
- **one 🔴** — S29, added by the audit that produced this recount; **five of its seven
  findings are now closed**, and the residue is (a) the ungated managed drain and (d) the
  hand-run blast-radius script. *(This bullet listed **three** residues beside a count of
  five — five closed plus three open is eight, out of seven — because (f) closed on
  2026-08-20 and only the count was updated. The count was right and the list was wrong,
  which is the same shape as the ❓ bullet above. Corrected 2026-08-27.)*
- **plus 🚫 S23-2**, which no amount of code will do — the owner has to change a
  branch-protection setting.

~~**Nine, down from twelve.**~~ **TEN on 2026-08-15**, and it went UP — S30 is a defect a real-title run found, not a doc drift. Keep this list in the same PR that changes a row, or
the recount becomes the next thing that needs recounting.

> **STILL TEN on 2026-08-20 — and this time nothing moved at all: one out, one in.** S30
> closed 2026-08-15, S31 opened 2026-08-20, so the total is unchanged while the membership
> is not. Recorded because the bullets above went on naming S30 for twelve days and the
> total stayed *right by coincidence*. **A count that is correct while its own list is wrong**
> is this table's characteristic failure, and it is exactly what the sentence above asks the
> next author to prevent by moving the list and the count in one PR. Corrected 2026-08-27,
> in a pass that touched no code — the §Traps entry *"a document can go stale by NOT being
> touched"* wearing the ledger's own name.

> **NINE on 2026-09-04.** S31 ✅ — the first S-item closed by a *measurement that answered
> the question* rather than by a decision to stop asking it, since S30. ❓ three, ⏳ three,
> ◐ two, 🔴 one, plus 🚫 S23-2. The list above moved in the same edit as this count.
>
> **ZERO on 2026-09-06 — exit criterion 2 reads as met, with one item outside any PR.** The
> owner's closing sweep ("theo bảng"): S19(c) and S23-6 resolved by a gate each, S29 down to
> one 🅓 on a pre-committed measurement, and six items deferred with their rationale written
> in the entry and their phase named in this table — S6 and S2 part three to P1, S23-3 to P3,
> S4 signing, S20 feed half and S14's store-id half to P4. ❓ zero, ⏳ zero, ◐ zero, 🔴 zero.
> **What remains is 🚫 S23-2**, a branch-protection setting only the owner can change; it is
> not resolved and not deferred, it is outside what a PR can do, and the criterion's own
> wording does not reach it. The bullet list above is history as of 2026-09-04 and is left as
> it stood; this paragraph is the current count.
>
> **Corrected 2026-09-09: ◐ is TWO, not zero.** S1 and H11 carry ◐ in their own headings and in
> the rows above — both *built, with a residue that is an owner measurement* (S1: the first
> real-title launch and the descendant election, now P2's; H11: rows B2–B4 unfired). The count
> *against exit criterion 2* is unchanged — neither is a safety gap and P0 stays closed — but the
> glyph column said ◐ while the sentence above said ◐ zero, which is exactly the failure this
> table records about itself (*"a count that is correct while its own list is wrong"*). Kept
> as ◐ two here rather than re-glyphing the rows: the rows were right and the paragraph was
> wrong, and the residue of each is a run, not a rationale. P2's sequencing is `HANDOFF` §P2.
>
> **2026-09-25: S14 ✅.** The store-id half, one of the six deferrals of 2026-09-06 (to P4), is built and seeded
> in the beta.8 train: a guard-side seam reads the store's own metadata, and the lists carry 30 Steam ids and 33
> executables. The deferred set is five; ◐ stays two (S1, H11); nothing else in this count moved.
>
> **STILL TEN on 2026-08-27, for the third consecutive movement.** S31 moved from ❓ to ◐,
> so ❓ goes four to three and ◐ goes two to three. **The number has now stayed at ten across
> three different changes**, and that is worth saying out loud precisely because a reader
> checking only the total would conclude nothing had happened since 2026-08-15. Three things
> have. The total is the least informative line in this section and it is the one people read.

> **A rising count here is the ledger working, not failing.** Every previous movement was downward and came from closing something. This one is upward because the first capture from a real game produced a wrong answer that no fixture had been able to produce — which is the whole reason exit criterion 1 asks for a real title rather than a harness.

> **Still nine on 2026-08-06.** Item 1 closed §S29(c) and §S29(e) and corrected
> §S29(f)'s premise, but §S29 is one row and stays 🔴 while (a), (d) and (f) are open,
> so the count does not move. Recording that explicitly, because "we closed two things
> and the number is the same" is exactly the kind of thing a later reader assumes is a
> mistake in the table.
>
> **Six owner decisions surfaced by item 1 and answered by nobody yet** — they are not
> S-series items and are not counted above, but they block nothing today and each is
> recorded where it belongs rather than only here: who may clear `hook_blocked_reason`
> (`06_DATA_MODEL` §games); whether per-game consent carries a wording version (decided
> **yes** and implemented, because it cannot be retrofitted — reversing it is still the
> owner's); what an operator must be shown before an `UnshippedHostOperator` record may
> be written, and whether that text is reviewed like a `Safety_*` string (`09_I18N`);
> whether FR-2.4's global kill switch is an input to `HookedCaptureGate` or is enforced
> upstream of it — the gate's own remark calls itself "the ONLY managed logic between
> the user's intent and the guard" and has three inputs and no kill-switch input;
> whether accepting the Legal Gate (FR-11) is a precondition of stamping consent; and
> whether §S18 blocker 3's "the Agent is the sole host of the guard" is re-ratified now
> that a third project imports `FrameLedger.Guard.targets`.
>
> **Three of these were decided 2026-09-09 and built 2026-09-10 (P2 PR-F):** the kill switch IS an
> input to `HookedCaptureGate` — the fourth, checked first (decision D7); what an operator is shown
> before a console record is written is `OperatorDisclosure`, one text for the unshipped host and the
> Agent's console, still not a reviewed `Safety_*` string and saying so first (decision D4); FR-11 is NOT
> a precondition of stamping consent in P2 (decision D8). The `hook_blocked_reason` clearing and the
> §S18 blocker-3 re-ratification stay the owner's.

> **2026-08-09, and the count does not move: still nine.** The upscaler identity hook
> landed (`HANDOFF` item 2) and closed nothing in this table, which is worth stating so
> nobody looks for a decrement. What it *did* do belongs here anyway:
>
> - **§S6's premise weakened again, in the direction of deferral.** The lazy feature-hook
>   install now runs on the watchdog and is under test, so "the first time their module
>   appears" is handled **without** a `LoadLibrary` hook. §S6 is about shrinking the 30 s
>   blocklist-detection window, which is a different job — but the mechanism it assumed it
>   would have to build is now built and unnecessary for P0. Its disposition is still an
>   open owner decision; the cost estimate attached to it is now too high.
> - **A licence question this file never asked has been answered, and it blocks a
>   documented plan.** `17_HOOK_ENGINE` §The NGX parameter surface recommends hooking
>   `NVSDK_NGX_Parameter_SetUI`; the NGX/DLSS SDK is the proprietary RTX SDKs Licence, so
>   `18_GPU_VENDOR_APIS` §Checklist step 3 forbids vendoring it **and** re-declaring it.
>   `FL_MEASURED_UPSCALER_PARAMS` therefore has no producer on the route the docs
>   specified, and P0 exit criterion 1 needs it. Corrected in `17_HOOK_ENGINE`; the
>   in-policy route is Streamline's own MIT surface. **Whether to revisit the checklist
>   for NGX is an owner decision no PR may take.**
> - **`spike-notes` §5's "blocked on a licence decision, not on hardware" is unblocked.**
>   Streamline is MIT, so §H5 case 3 — the risk that would make `fg_factor` structurally
>   1.0 on every Streamline title — is now reachable with `slInit`, and it belongs to
>   item 3.
> - **A gate that could not fail was removed from the native suite**, though it was not on
>   this list: a failed `REQUIRE` terminated the whole binary, so on this dev box one
>   environmental failure silently deleted the end-to-end honesty coverage §S29(a) rests
>   on. See `HANDOFF` §Traps.

~~**The markers themselves are part of the problem and should be unified.** One
disposition — *deferred, with a rationale written* — currently wears three glyphs:
S12 is `✅ deferred`, S1 and S13(c) are `🅓 deferred`, S19(b) is `🔴 deferred, but
now MEASURED`. This table exists so the criterion can be audited by *counting
markers*; three glyphs for one state defeats the only thing it is for.~~

**Unified 2026-08-27, and struck rather than deleted because the reasoning is the part that
generalises.** `🅓` is now the single mark for *deferred with a written rationale*: S12 moved
off `✅` and S19(b) off `🔴`, and the legend under §S24's heading states what every mark means
and whether it counts against exit criterion 2. **Neither move changes a disposition.**
S19(b) is as deferred, and as measured, as it was — its measurement lives in its note cell,
where it always did. What changed is that a marker no longer carries two meanings at once,
so the count and the glyphs can now disagree only if someone makes them.

### S29 · What the 2026-08-05 P0 completion audit found — five gates and one contradiction

A five-slice audit of the tree against the docs, with every claim re-checked by a
refuter told to default to *refuted*. The ledger drift it found is fixed in the PR
that records this. What follows is the residue: things that need code, recorded so
the next session finds them by reading.

**(a) ✅ The honesty contract is in the merge gate — natively, and since 2026-09-06 the MANAGED half too.**

> **Closed 2026-09-06 by §S19(b)'s signer half and the same PR's `ci.yml` change.** The .NET
> test host still loads `System.Security.Cryptography.ProtectedData.dll`; the guard now verifies
> its embedded signature offline, reads `O=Microsoft Corporation` on the compiled-in bound, and
> keeps looking. `ShmDrainIntegrationTests`, `CaptureHostEndToEndTests` and the LHM hardware case
> run in CI under `./build.ps1 check` with no switches. The entry below is the history.

> **This entry was WRONG when it was written on 2026-08-05, and the correction
> matters because the wrong version was used to re-order the work.** It claimed the
> assertion catching a violation *"lives only in `ShmDrainIntegrationTests`, which
> CI skips"*, and concluded that §S19(b) was a prerequisite of the item-4/6/7 hooks.
> Both halves are false. Found by a design panel refuting the claim rather than
> repeating it.

`measuredMask` and `rtFlags` are what stop a present-only writer asserting "no
upscaler, no frame generation, no ray tracing" as measured fact (CLAUDE.md rules 6
and 7). **The assertion exists in the NATIVE suite and CI runs it:**
`guard_test.cpp`'s *"the injected Overlay records real presents into the ring"*
injects into `hook-harness` and requires `measuredMask == FL_MEASURED_OUTPUT_RES`
and `rtFlags & FL_RT_NOT_MEASURED` on every drained record. It is ctest `fl_guard`,
and CI ran it in 20.58 s on 2026-08-05.

**Why the native path works where the managed one does not, which is the whole
shape of §S19(b):** `fl_guard_test.exe` is a *native* host. It never loads
`System.Security.Cryptography.ProtectedData.dll`, so the `protect` fragment never
fires and the guard does not refuse it. A .NET test host does, which is exactly why
`ShmDrainIntegrationTests` is skipped and this is not.

**What is genuinely not gated**, stated narrowly this time: the **managed** drain —
`ShmRingReader`, `ShmHandshakeValidator` against a live writer, and the
`PublishGuardResult` round trip. A regression there is caught by nothing automated.
Fixing §S19(b) buys that, and it is worth doing; it is **not** a prerequisite of the
feature hooks, and this entry should not be used to sequence them.

**(b) ✅ `ctest fl_vtable_indices` did not pin the Overlay's vtable indices — closed 2026-08-05.**
`hook-harness` declared `kPresentIndex = 8` / `kResizeBuffersIndex = 13` /
`kPresent1Index = 22` as its own literals, textually duplicated from the inline
values in `dllmain.cpp` with no shared header. Changing the Overlay's 8 to a 9 left
the ctest green: it proved a fact about `dxgi.dll`, not a fact about
`FrameLedger.Overlay`. The only test coupling the two is the integration class CI
skips — so in the merge gate the coupling was absent entirely.

**Closed by one header and an INTERFACE target.**
`FrameLedger.Overlay/include/fl_dxgi_vtable.h` holds the three slot numbers;
`fl_dxgi_vtable` lets `hook-harness` consume them without linking the DLL. Proven:
setting `kPresentIndex = 9` leaves the native build **green** and turns
`fl_vtable_indices` **red** (with three neighbouring harness tests, which depend on
hooking working). Restored: 16/16.

> **What this does not do:** the indices are still not *trusted* — the header is
> where the assumption is written once, and `--probe-vtable` calling each slot on a
> real swapchain is what makes it a measurement. It also does not license a
> hardcoded vtable *pointer*: the Overlay still reads the vtable off a throwaway
> WARP composition swapchain and releases it. What is ABI-constant is the slot
> index, not the address.
>
> P0 item 2's ✅ rests partly on "vtable indices proved by behaviour", and until now
> that proof did not reach the shipped values. It does now.

**(c) ✅ `HookedCaptureGate.ShouldUnhookAsync` — closed 2026-08-06 by DELETING it.**

Grep decided it: the only occurrences were the declaration and two tests. **Zero production
callers**, so removing it touched no shipping path. Its whole body was
`!(await _guard.EvaluateAsync(pid, ct)).IsAllowed` — strictly weaker than
`GuardSupervisor.ScanOnceAsync`, which additionally short-circuits on the latch, increments
`CompletedEvaluations` at one site on the far side of a returned verdict, latches
`UnhookRequested` and records `LastVerdict`. Routing it through the supervisor would have given
the gate per-session state on a class whose contract is that it "deliberately adds no judgement
of its own", for no new capability.

**The polarity trap was real and is worth recording**: `ShouldUnhookAsync` returning `true` meant
STOP, while `ScanOnceAsync` returning `true` means MAY CONTINUE. Two supervision APIs on adjacent
objects with inverted senses is a defect waiting for a hurried caller.

**Its two tests are replaced by one that is stronger.** They asserted the boolean and never the
tick or the latch — so they certified the API as sanctioned while saying nothing about what was
wrong with it, which is exactly how it came to read as sanctioned.
`TheGateExposesNoSecondInSessionRescanPath` pins the gate's public instance surface to exactly
`{StartAsync}`. Proven: re-adding the method leaves the build green and turns that Fact red.

> **The original entry follows, struck rather than deleted**, because "it is unit-tested, so it
> reads as sanctioned" is the observation that decided the disposition.

~~**(c) 🔴 `HookedCaptureGate.ShouldUnhookAsync` is a second in-session re-scan path**~~
that publishes no tick and does not latch — the two properties `GuardSupervisor`
and `fl_shm.h` §`guardTicks` spend paragraphs establishing as the point of the
design. It is unit-tested, so it reads as sanctioned, and it is the **more
discoverable** of the two APIs because a drain loop is already holding the gate. A
loop wired to it supervises the target while the Overlay's watchdog counts down to
a supervision-loss stop that never resets. Either route it through
`GuardSupervisor` or delete it; leaving both is how the counter quietly stops
meaning what it says.

**(d) ◐ `tools/vklayer-blastradius.ps1` case 3 could not fail — the assertion
half is closed, the regression-net half is not.** It printed in both branches and
never added to `$errors`. That is the only exercise of `enable_environment`, the
gate `15_ROADMAP` calls the highest blast radius in the spike — a passthrough bug
loads FrameLedger into every Vulkan process on the machine.

**It is now an assertion**: a non-matching value that enables the layer fails, and
says that `spike-notes` §2's measurement no longer holds. Being an observation was
*correct while the answer was unknown* — the step existed to discover the loader's
behaviour, and a discovery step that fails is just a step with an opinion. What
was wrong is that the discovery was made on 2026-08-02, recorded as settled, and
the step went on printing about it in green either way. **When a measurement
becomes a recorded fact, the step that produced it has to become the thing that
defends it**, or the fact stops being checked while a script that still mentions
it reads as coverage.

**Still open:** the script is excluded from `build.ps1` and CI by design (it
writes `HKCU`, and a CI runner is the wrong place to register machine-wide Vulkan
layers) and needs `vulkaninfo` on `PATH`. So the assertion only fires when someone
runs it by hand. Nothing reminds them, and a loader upgrade is exactly when it
would matter.

> #### Disposition 2026-09-06, pre-committed on a measurement the same PR takes
>
> The "wrong place" argument inverts on a hosted runner: it is disposable, so an implicit
> layer registered under its `HKCU` harms nothing, and it IS the place a loader upgrade
> would be noticed. What decides is whether the runner has a Vulkan loader and
> `vulkaninfo.exe` at all — they ship with a GPU driver's runtime, not with Windows.
> `ci.yml` now prints a **Vulkan runtime census** (`vulkan-1.dll`, `vulkaninfo.exe`, the
> ICD registry key) as a measurement step. **Rows:** present → the next PR runs
> `vklayer-blastradius.ps1` as a CI step and (d) closes ✅; absent → (d) is 🅓 **deferred on
> that measurement**: the script stays hand-run before any layer PR (`HANDOFF` §Traps), and
> the reminder is the P1 Vulkan entry that cannot land without running it. Either row ends
> S29 as an S-item; the count below is written for the absent row and corrected if the
> census says otherwise.
>
> **2026-09-06 (P1 item 3): the assertion now lives in a ctest, and it no longer needs the registry.**
> `fl_vklayer_real` starts the harness's `--vulkan` mode with `VK_ADD_IMPLICIT_LAYER_PATH` pointed at the
> build tree's manifest and the enable variable set, and asserts the loader mapped the layer and the layer
> recorded the presents — on every dev-box build, with no HKCU write and nothing left behind. It SKIPS
> where there is no loader or no presentable device, which is CI's row; the census still decides (d)'s
> CI leg. `vklayer-blastradius.ps1` remains the hand-run check of the *unset-variable* case (it was run
> before this PR, green), and the loader-upgrade reminder is now the ctest rather than a HANDOFF trap.
>
> **Read 2026-09-06 off the sweep PR's run (`windows-latest`): `vulkan-1.dll` PRESENT in
> System32, `vulkaninfo.exe` absent (not in System32, not on PATH), no ICD registry key.** The
> absent row: **🅓 deferred on that measurement.** The loader is there but nothing to drive it
> with and nothing for it to drive — `vulkaninfo.exe` ships with a GPU driver's runtime, and
> without an ICD `vkCreateInstance` fails before the layer question is fully asked. The script
> stays hand-run before any layer PR. **Reopen condition, written now:** a Vulkan mode of
> `hook-harness` (unwritten, `12_BUILD` §Targets) could replace `vulkaninfo.exe` as the driver,
> and whether the loader's layer discovery and `enable_environment` behaviour is observable
> with no ICD is one measurement away — the P1 Vulkan entry is where it is taken.

**(e) ✅ The reader cannot tell a dead target from a quiet one — closed 2026-08-06,
host-side, with no ABI change.**

The diagnosis stands and got worse on inspection. `ShmRingReader` holds the section
open, so a game that exits leaves `writeIndex` frozen and `status` `READY` —
byte-for-byte identical to a loading screen or an alt-tabbed window. `DllMain` handles
only `DLL_PROCESS_ATTACH` and deliberately has no `DLL_PROCESS_DETACH` teardown
(`17_HOOK_ENGINE` §Unhooking), so **nothing is written on the way out**. And §S26
removed the accidental heartbeat: an occluded title used to emit a steady stream of
`DXGI_PRESENT_TEST` probes, and the writer now drops them, so an occluded game writes
*nothing at all*.

**The signal comes from the OS, not from the mapping.** `CaptureLoop` opens a process
handle **before** `FlGuardedInject` and holds it for the session. Held rather than
re-resolved, because pids recycle and the ring is named after one; the kernel will not
reuse a pid while a handle to it is open. `Infrastructure.Io.HeldProcessHandle` opens it
with `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION` — the narrowest rights that
answer the question, named explicitly as `05_DETECTION` asks, with no `VM_READ` and no
`VM_WRITE`, so CLAUDE.md rule 4 is untouched. Putting liveness on `ShmRingReader` was
rejected: it would make the *reader* open the target, which is new surface for nothing.

> **THIS ENTRY WAS CLOSED ON A MECHANISM THAT DID NOT EXIST, and the correction is the
> part worth keeping.** The first version used `System.Diagnostics.Process.GetProcessById(pid)`
> and read `HasExited`, with a comment asserting that the handle kept the pid pinned.
> **Measured**, on .NET 10.0.10, by a probe over the live object: `_haveProcessHandle`
> is `false` and `_processHandle` is `null` both after construction and after reading
> `HasExited` — `GetProcessById` opens nothing, and `HasExited` opens a transient handle
> and releases it in its own `finally`. **No handle was held at any point and the pid was
> never reserved.** Three source comments, this entry and a CHANGELOG line all asserted
> it; nothing checked it.
>
> It cost two things. Between resolving the pid and injecting there is an awaited file
> read — long enough for an exit-and-recycle, after which `FlGuardedInject` would load
> the Overlay into a stranger on the strength of a consent record for a different binary.
> Not a rule-2 bypass, because the guard still scans the real pid; a rule-1 one, which is
> the class §S27 exists for. And mid-session, `HasExited` against a recycled pid answers
> about the *new* process, so the session would never end.
>
> Found by an adversarial review of the diff whose verifier wrote the probe rather than
> arguing from the docs — the ninth entry for `measure-windows-apis-dont-trust-them`, and
> the first where the API misled by doing *less* than its name implies. `TryOpen`
> returning null is now a refusal (`TargetCannotBePinned`), because a pid we could not
> pin must not be injected into, and `HeldProcessHandleTests` asserts the property rather
> than the outcome: it answers about a process that has fully exited, at a pid
> `Process.GetProcessById` refuses to resolve at all — which an implementation holding
> nothing cannot do.

**`SessionEndClassifier` takes no elapsed-time parameter, and that absence IS the fix.**
A frozen `writeIndex` never ends a session, however long it has been frozen; a
"how long since the last record" parameter is precisely how this defect would come back.
Its test drives five simulated minutes of silence with `status` `READY` and asserts
`Running`.

**It also separates two stops the mapping cannot.** `StopObserving` stores
`FL_STATUS_UNHOOKED` for the safety stop *and* for supervision loss, so only the side
that caused one knows which — and `legal/DISCLAIMER.md` §2 discloses them differently
("the guard fired" against "contact was lost"). The classifier takes
`weLatchedTheUnhook` for exactly that reason.

**(f) ❓ CLAUDE.md rule 7 and `03_METRICS` §RT disagree about inline RayQuery, and
one of them is normative.** Rule 7 names *"inline RayQuery without DXIL scan"* as a
case where measurement is genuinely impossible and `N/A` is the only honest answer.
`03_METRICS:128` says the opposite in as many words: *"Hooking
`BuildRaytracingAccelerationStructure` is what makes inline ray tracing (DXR 1.1
`RayQuery`) detectable at all"*, and `README:14` promises it to users.

They are reconcilable — AS-build activity proves *ray tracing is happening* in a
RayQuery-only title, while *classifying* the technique as RayQuery is what needs a
DXIL scan — but neither document says so, and the two readings produce opposite
answers for the same title. **Whoever builds P0 item 6 has to settle this before
writing the hook**, because it decides whether a RayQuery-only title reports `Yes`
or `N/A`. Related, and worse: `03_METRICS` defines the tri-state's **`No`** branch
as *"RT-capable device present, no AS builds and no dispatches for the whole
session"* — and nothing measures device RT tier, ~~no record field carries it, and
every byte of the 64-byte record is allocated. **As the record stands, item 6 can
reach `Yes` or `N/A` and never `No`.**~~

> **The PREMISE is stale and the CONCLUSION is understated — corrected in place
> 2026-08-06.** Layout v3 (#58) put `rtTier` at `FlWriterState` @24 and
> `hooksInstalledMask` at @28, and `03_METRICS` §RT/PT/RR now states the `No` branch
> against both as three conjuncts. The record's byte budget was never the constraint:
> these are session facts and went in region 2, not in the 64-byte record.
>
> **What survives is stronger.** A tree-wide grep finds no producer for either field —
> the only hits outside `fl_shm.h`, `ShmLayout.cs` and the consumer are
> `tools/fl-layout-dump/main.cpp`, which prints their offsets. So RT is `N/A` on every
> session today, and **both `Yes` and `No` are unreachable**, not just `No`. The
> throwaway consumer implements all three branches and a test drives each, so the day
> the RT hooks land the tri-state is already correct rather than being written under
> deadline beside them.
>
> The RayQuery half of this entry is **untouched and still open**: it is a normative
> contradiction between CLAUDE.md rule 7 and `03_METRICS`, and it is item 6's to settle
> before the hook is written.

> ### ✅ (f) IS CLOSED — 2026-08-20, ruled 2026-08-14 by the owner and now implemented
>
> **The reconciliation this entry proposed is the ruling.** AS-build activity proves ray
> tracing is happening ⇒ `RT = Yes`; *classifying the technique as RayQuery* is what needs a
> DXIL scan and stays `N/A`. CLAUDE.md rule 7 is amended to say so, in the PR that wrote the
> hooks, so the two documents no longer read in opposite directions. `03_METRICS:128` and
> `README:14` both stand unchanged, which is the outcome the entry expected.
>
> **The second half is closed by the same PR, and the closure is a measurement rather than a
> restatement.** `FL_MEASURED_RT` has a producer:
> `ID3D12GraphicsCommandList4::BuildRaytracingAccelerationStructure` and `::DispatchRays`,
> installed lazily off a command list created on the game's own device, with `rtFlags` and
> `dispatchRaysVolume` drained per present. `rtTier` and `hooksInstalledMask` already had
> producers, so **all three conjuncts of the `No` branch are live and `Yes` and `No` are both
> reachable for the first time**. Proved by injection, both directions: a fixture that builds
> acceleration structures and dispatches, and one that builds and never dispatches — the
> second is what makes the AS-build hook's whole justification falsifiable.
>
> **What is still `N/A` on purpose**: naming the technique, and path tracing, whose heuristic
> needs `maxTraceRecursionDepth` and `rtStateObjectsCreated` — both still unproduced, because
> `ID3D12Device5::CreateStateObject` is a separate PR. `MeasuredFacts.PathTracing` stays a
> fixed `Tri.NotApplicable`, which rule 7 requires of it in any case.

**(g) ✅ The present-only writer claimed the one thing it may claim — unconditionally — closed 2026-08-05.**

`RecordPresent` set `FL_MEASURED_OUTPUT_RES` on every record, and two paths reach it
with no output size to report:

- `FindOrAdd` is a fixed 16-slot linear scan and returns `nullptr` once they are
  taken, so `outputW/H` are never assigned and stay 0.
- `GetDesc` failing, in `FindOrAdd` or in `ForgetChainSize` after a resize, leaves
  them 0.

So the record said **"output resolution MEASURED: 0 × 0"** — and `03_METRICS`
computes the upscale ratio as `sqrt((outW*outH)/(renW*renH))` from exactly those two
fields. This is the same defect #36 spent two bytes to fix, surviving inside the fix:
the mask distinguishes *looked* from *did not look* for six other fields, and for the
seventh it was a constant.

**Found by a design panel refuting a proposed layout, in the shipped writer rather
than in the proposal.**

Closed: the bit is set only when `outputW != 0 && outputH != 0`, which is what every
other bit in the mask already means.

**Proven both directions, and the second direction needed a new fixture.** Nothing in
`hook-harness` could reach the overflow branch — `--plus-ui` creates *one* extra
swapchain, which is a second stream, not an overflow. `--hold-presenting-overflow`
round-robins 17 chains for the whole hold so the Overlay meets more than it can hold.
The existing end-to-end test asserts the mask is *exactly* `OUTPUT_RES` on a normal
target; the new one asserts it is *exactly* 0 on an overflowed one. Canary: restoring
the unconditional assignment turns the new test red with the build green.

> **The fixture was wrong first, and the test's own vacuity guard is what said so.**
> Its first version filled the table at startup and then held on the 17th chain — but
> the Overlay is injected ~800 ms later and only sees presents made after it hooks, so
> it observed an *empty* table and gave the "overflowed" chain slot 1. The assertion
> `overflowed > 30` reported **0**, which is why the loop full of `CHECK`s inside it
> did not silently pass by never executing. The test also pins the overflowed stream's
> *share* (~1/17), because an absolute floor alone would be satisfied by a harness
> presenting on one chain.

### S1 ◐ · The guard is structurally blind in launch mode — **launch mode built as "inject late", 2026-09-06 (P1 item 2)**

> **Built, and the deferral's missing input is now something every launched session produces.**
> The guard gained `GuardedInjectWhenReady(pid, dll, timeoutMs)` (ABI `FlGuardedInjectWhenReady`): it
> polls the target every 50 ms through the module seam — a `Collected::kFailed` (the suspended /
> still-in-loader `ERROR_PARTIAL_COPY`) is "not yet", never a pass — until `dxgi.dll` with
> `d3d11.dll`/`d3d12.dll`, or `opengl32.dll`, or `vulkan-1.dll` is mapped, then runs `GuardedInjectImpl`
> in full. The poll matches nothing against the blocklist; it chooses WHEN the guard runs and nothing
> about whether it passes. Two new reasons end the poll with nothing injected: `LaunchTargetExited`
> (24) and `LaunchNoPresentationRuntime` (25). The CaptureHost's `launch` verb starts the consented
> executable itself (`ProcessLauncher`, `FRAMELEDGER_ENABLE_VK_LAYER=1`, handle held from birth) and
> routes the same gate through `HookRequest.WaitForPresentationRuntimeMs`; it never terminates what it
> launched. Measured, ctest `fl_guard` `[launch]`: the fake-driven poll injects on the first
> enumeration that shows the runtime and not one before (7 polls, ~300 ms), a target that exits first
> and a budget that runs out each answer their own reason, anti-cheat mapped beside the runtime is the
> full scan's refusal, and the real-seam case against `hook-harness` injects within one poll (15 ms).
>
> **The number nobody had:** `FlWriterState.dxgiPresentsBeforeHook` @60 — DXGI's `GetLastPresentCount`
> at the first hooked present on the first hooked chain, i.e. presents that ran before the hook was in
> (0xFFFFFFFF = the hook never saw a present). With the reported wait (`launch: the guard injected N ms
> after the process was started`) that is the early-init cost §S13(c) said needed a lazily loading title:
> it now comes from every title, on every launched session. **What the numbers decide is whether launch
> mode is *preferred* for a given title, not whether it exists.** The 2026-08-05 rationale stands as
> written below; what it deferred is now measurable rather than argued. Owed: the first real-title launch
> (the owner's next captures), which will also be the first look at a launcher-first title landing on
> `LaunchTargetExited` and needing the Agent's descendant election (P2).

> **§S1 does not gate the Vulkan path.** Added 2026-08-03, because "launch mode
> is blocked by §S1 anyway" was being used as a reason to deprioritise §S18 and
> it is false for an entire Tier-1 API family. The Vulkan layer performs **no
> injection**: there is no `CREATE_SUSPENDED` target, no empty module list, and
> the session goes `Detected → Guarded → Capturing` without ever entering
> `Injecting`. Vulkan Tier 1 is nevertheless launch-mode-only
> (`17_HOOK_ENGINE:161` — only the launching process can set
> `FRAMELEDGER_ENABLE_VK_LAYER=1`), and §S18 was its sole blocker.
>
> **§S18 closed 2026-08-04, so that sentence is history.** What now blocks
> Vulkan Tier 1 is `vkQueuePresentKHR`, which is P1 and not started.

`04_CAPTURE` §Launch mode prefers `CreateProcess(CREATE_SUSPENDED)` → guard →
inject → `ResumeThread`. But a suspended process has **loaded no modules yet**,
so `EnumProcessModulesEx` returns essentially nothing and the primary
pre-injection check (`19_SAFETY` §Pre-injection checks item 1) is a no-op in the
*preferred* path. Anti-cheat that would have been caught in attach mode sails
through in launch mode.

> **Measured 2026-08-02** (`spike-notes.md` §1, ctest `fl_guard_apis`), and the
> result is sharper than this entry assumed. `EnumProcessModulesEx` against a
> `CREATE_SUSPENDED` target does **not** return an empty list — it *fails*, with
> `ERROR_PARTIAL_COPY (299)`. That is the safer of the two shapes: an error
> cannot be mistaken for a clean scan the way an empty success can. The rule
> ("any failure means REFUSE") is now in `19_SAFETY` item 1.
>
> So the *hazard* in this entry is downgraded — launch mode cannot silently
> pass a blind scan — but the *constraint* is confirmed: check 1 genuinely
> cannot run before the target's loader has. The decision below is still open.

**Proposed:** treat the static pre-scan and the driver scan as the gates that run
while suspended; resume, then re-run the module scan and only install hooks once
it passes and the first present is observed. This costs the early-init upscaler
data launch mode exists to capture — quantify that loss in P0 before accepting
it. Decide whether the answer is "inject late" or "no launch mode at all".

**Why the loss is still unquantified.** It needs a title that loads a
presentation runtime lazily. The local fixtures are `hook-harness` (creates D3D
at startup) and a 2D GOG prologue; a number from either would not generalise,
which is worse than recording it unmeasured. This is the one input §S13(c)
still lacks.

> #### 🅓 Deferred 2026-08-05, by owner decision, with this as the rationale
>
> The decision between "inject late" and "no launch mode at all" **is not taken**,
> and deferring it is the recorded answer rather than an omission — P0 exit
> criterion 2 admits a written deferral, and this is it.
>
> **Why deferring is defensible and not merely convenient.** The choice turns on
> one number nobody has: how much early-init upscaler data injecting late actually
> costs. Taking the decision without it would fix a design on an assumption, and
> §S13(c)'s own text has said since 2026-08-02 that the input is missing. Choosing
> "inject late" unmeasured risks shipping a mode that captures nothing useful;
> choosing "drop launch mode" unmeasured throws away **Vulkan Tier 1 entirely**,
> because `enable_environment` can only be set by the process that starts the game
> (`17_HOOK_ENGINE:161`). Neither is a decision to take blind.
>
> **What the deferral costs, stated rather than implied.** Launch-mode injection
> does not work today and will not until this is decided; attach mode is the only
> injection path that ships. Vulkan Tier 1 stays unreachable, though it is
> independently blocked on `vkQueuePresentKHR` (P1), so the deferral is not
> currently the binding constraint there.
>
> **What would end the deferral:** one title that loads `dxgi`/`d3d11`/`d3d12`,
> `opengl32` or `vulkan-1` lazily rather than at startup. The owner supplies real
> fixtures on request; this one has not been found on this machine.

### S4 ◐ · Trust model for the enable-list and the rules feed — **two of three closed**

**Enable-list: specified** (`17_HOOK_ENGINE` §The enable-list) — location,
format, bounds, exact case-insensitive image-name matching, sole writer, ACL,
and the rule that *every* failure is passthrough. Written down with it: the ACL
is not strong (anything running as the user can append a line), but a line there
only causes us to observe a Vulkan process, grants no injection, and cannot
disable the layer's own blocklist scan.

**Staleness: specified** (`05_DETECTION` §Trust and staleness of the rules feed)
— one read location, replace-only-if-valid with the last valid copy kept, and
staleness that *warns* and never disables. An expiry that weakens a gate is an
override with a timer on it.

**Signing: still open, and deliberately not dismissed.** HTTPS authenticates the
host, not the content. The rules above let a compromised feed be *rejected* but
not a genuine one *proven*. Residual risk, recorded rather than closed.

> #### 🅓 Deferred to P4, rationale written 2026-09-06
>
> Signing is a property of the feed, and there is no feed (§S20's feed half, deferred to
> P4 in the same sweep): every rules file on every machine today shipped inside a build
> whose SHA-256 is published, so the content IS attested, by the release. The decision that
> belongs to P4 — a compiled-in public key and a detached signature over the rules file,
> verified before validate-then-replace, with the same fail-closed polarity as the rest of
> the feed rules — is written here so the feed cannot ship without it. Residual until then:
> none beyond the release checksum, because nothing fetches.

### S19 · The unknown-but-suspicious heuristic has five defects of its own

Found 2026-08-03 by the §S18 panel and **re-measured by hand before recording**,
because three of them change what a shipped gate does. They are separate from
§S18 — no self-exclusion touches any of them.

> **Heading said "four" over a body running (a) to (e).** (e) was appended
> without updating the count. Corrected 2026-08-04, along with the claim that (b)
> "lands in attach mode, the only mode that ships today" — see the measurement
> under (b), which does not support it as written.

**(a) ✅ `gameguard` could never fire — closed 2026-08-05.**
`HasSuspiciousFragment` is case-insensitive `IContains`, and `"gameguard"`
**contains** `"guard"`. With both in the list, the `guard` fragment matched
everything `gameguard` would, first. A shipped rule incapable of firing
independently — the file's own recurring defect, sitting inside the safety gate.

**Closed by deleting the entry, and by a rule that stops the class returning.**
`rules-validate` now fails when any `nameFragment` contains another, naming which
one can never fire and why. Proven both directions: re-adding `gameguard` goes
red at a named line; the shipped list is green.

**Why deletion was the safe option here and is not, elsewhere.** The standing
objection to removing a fragment — it is a detection removal in a hard gate — is
correct for `protect` and does not apply to this one. Subsumption means *by
construction* that no module name loses coverage: anything `gameguard` matched,
`guard` matches. And it was redundant twice over, which nobody had checked:
nProtect GameGuard has its **own named module family** in the same file
(`GameGuard`, `npgg`, `GameMon`, exact-prefix), so the fuzzy tier was never its
only route.

**The hazard this closes was always in the future, and that is why "cosmetic" was
the wrong word for it.** With both entries present, someone removing `guard`
leaves a list that still appears to cover nProtect. Now the list is minimal, so
removing `guard` is visibly a removal.

> Removing it trips `rules-publish`'s `$removed = old − new` check. **That is the
> gate working**, not an obstacle: it exists to make a blocklist removal
> reviewable, and this is one.

**(b) `protect` produces measured false refusals, and they are not hypothetical.**
Unelevated, Windows 11 26300, **331 processes, 0 access-denied, 4 hits, none
anti-cheat**:

| Process | Module matching `protect` |
|---|---|
| `WhatsApp.Root` | `mskeyprotect.dll` — *Microsoft Key Protection Provider*, 10.0.26100.1746, `C:\WINDOWS\system32\` |
| `WidgetService` | `mskeyprotect.dll` |
| `ProtonVPN.Client` | `System.Security.Cryptography.ProtectedData.dll` |
| `Malwarebytes` | `Malwarebytes.Protection.Interop.dll` |

**The fix is not to delete the fragment** — that is a detection removal in a hard
gate, and `antitamper`/`protect` is the only tier covering families the seed
admits it has no data for (Ricochet, VAC). Three refuters agreed.

> #### Re-measured 2026-08-04, and the entry above was wrong in both directions
>
> Same machine, unelevated: **290 processes, 0 inaccessible, 3 hits** (not 4 —
> `WhatsApp.Root` was not running this time; the module set is the same).
>
> | Module | Signature | `O=` | Would the signer half suppress it? |
> |---|---|---|---|
> | `mskeyprotect.dll` | **Catalog** | Microsoft Corporation | **No** |
> | `System.Security.Cryptography.ProtectedData.dll` | Authenticode | Microsoft Corporation | Yes |
> | `Malwarebytes.Protection.Interop.dll` | Authenticode | **Malwarebytes Inc** | **No** |
>
> **The proposed fix would have addressed one of three.** `mskeyprotect.dll` —
> the module this entry was written about — carries no *embedded* signature at
> all; it is catalog-signed, as are `kernel32.dll` and `nvapi64.dll`. A
> `WinVerifyTrust(WTD_CHOICE_FILE)` implementation, which is what "wire the signer
> half" means to everyone who has read this entry, recovers nothing for it and
> `IsTrustedSigner(nullptr)` is false by contract, so it still refuses. Doing it
> properly needs `CryptCATAdminAcquireContext2` / `CalcHashFromFileHandle` /
> `EnumCatalogFromHash` and `WTD_CHOICE_CATALOG`, which nothing here budgeted.
> And `Malwarebytes.Protection.Interop.dll` is validly signed by a publisher
> absent from `trustedSigners`, so **no signer implementation fixes it** — only a
> data change to an allowlist that no CI gate reviews.
>
> **"A game that loads a Windows key-protection provider is refused today, in
> attach mode" is also not supported by the measurement.** All three hits are
> desktop processes — a browser helper, a VPN client, an AV service — and none can
> enter a game's scan set: `EnumerateScanSetImpl` walks ancestors only up to the
> first `IsPlatformLauncher` match, and that list includes `explorer.exe`,
> `services.exe` and `svchost.exe`, so a normally-launched game's chain terminates
> one hop above it. Three real titles have been scanned with no fragment hit.
>
> The defensible claim is narrower and still worth acting on: **the `protect`
> fragment matches a benign, widely-loaded Microsoft system DLL, and has not been
> shown to match inside any game's scan set.** A game process that loads DPAPI or
> CNG for save encryption or a launcher token would trip it, which is plausible
> and unmeasured — not "refused today".
>
> #### 🔴 MEASURED FIRING IN A REAL SCAN SET, 2026-08-05 — the paragraph above is superseded on its central point
>
> The claim that closed this entry was: *"the `protect` fragment matches a benign,
> widely-loaded Microsoft system DLL, and **has not been shown to match inside any
> game's scan set** … which is plausible and unmeasured — not 'refused today'."*
>
> **It has now been shown.** CI, running the drain integration test:
>
> ```
> the guard refused our own harness: SuspiciousUnsigned unknown
> System.Security.Cryptography.ProtectedData.dll
> ```
>
> The mechanism is the one this entry ruled out. §S16 puts the target's **ancestors**
> in the scan set, and the test host is the harness's parent — the launch-mode
> arrangement, where the Agent is the game's parent. A .NET host loading that
> assembly therefore poisons its own scan set, and the injection it is trying to
> perform is refused. **A gate that cannot pass**, which is the mirror-image defect
> this file records as hiding better than the fail-open, because refusing looks safe.
>
> Three things this does and does not say:
>
> - **It is the §S18 shape with a different module.** §S18 was our own
>   `FrameLedger.Guard.dll` matching `guard`; the exemption built for it is keyed on
>   the matched module being **ours by file id**, and a .NET shared-framework
>   assembly is not.
> - **Attach mode is unaffected.** The Agent is not an ancestor there — a
>   normally-launched game's chain terminates at a platform launcher one hop above
>   it — so this is a **launch-mode** hazard, and launch mode is deferred (§S1).
> - **It passed on the dev box and failed on CI**, because the two hosts load
>   different module sets. Which shipped configuration the dev machine is not, again.
>
> **Not fixed here.** The remedies all have costs this entry already priced: the
> signer half is a `CryptCATAdmin*` PR whose default revocation check does network
> I/O from inside the hard gate (NFR-10), deleting the fragment is a detection
> removal in a hard gate that three refuters rejected, and a location-based
> exemption for the shared framework widens the carve-out §S18 deliberately kept
> narrow. What changed is the **evidence**, not the decision: this is no longer a
> hypothesis, and whoever picks up §S19(b) now has a reproducible case.
>
> #### ⚠ AND IT FIRES ON THE DEV BOX TOO — INTERMITTENTLY — measured 2026-08-28
>
> This entry says the cases *"pass on a host that does not load the assembly"*, and the line
> below adds *"`./build.ps1 check` with no switches still runs them"*. Both read as though the
> dev box were a stable pass. **It is not.**
>
> One full `check` on a docs-only branch went red at
> `ShmDrainIntegrationTests.APausedSessionStopsRecordingAndResumesWhereItLeftOff` with
> `GuardedInjectAsync(...).IsAllowed` **False** — the guard refusing our own harness. The same
> test alone passed immediately afterwards, and the next full `check` passed too. Nothing in
> the branch touched code.
>
> **The mechanism is this entry's own**, one variable further out: the suite runs four test
> assemblies in parallel, so *which* modules a given test host has loaded when the guard walks
> its ancestors varies run to run. A .NET host that loaded
> `System.Security.Cryptography.ProtectedData.dll` refuses; one that has not, does not.
>
> **Two consequences.** A local red here is **not** evidence about the branch — check the
> refusal reason before reading the diff, exactly as `§Traps` now says for `fl_ring`. And the
> defect is worse than "CI-only" made it sound: the population is any host that happens to
> load a `protect`-matching module, which is a property of the run rather than of the machine.
>> The integration tests are traited `Category=Integration` and CI runs
> `./build.ps1 check -SkipIntegration`, which **skips loudly**. `./build.ps1 check`
> with no switches still runs them, and they pass on a host that does not load the
> assembly. A suite that quietly stops running a class is how a gate rots, so the
> skip is named in the CI log and in the gate summary.

> §S19(b) is therefore **deferred with this written rationale** rather than
> scheduled: it fixes one measured case of three, its true shape is a
> `CryptCATAdmin*` PR, and `WinVerifyTrust`'s default `WTD_REVOKE_WHOLECHAIN`
> performs CRL/OCSP network I/O from inside the hard gate — against **NFR-10
> offline-first** (`02_SPEC:105`) as well as CLAUDE.md rule 8. Build
> `fl-probe-signer` first, in the shape `fl-probe-guard` established, and answer
> those three questions with measurements before any design is fixed.

> Note what it costs beyond that: a per-module signature cost inside a gate that
> re-runs every 30 s (§S6), and a new fail-closed matrix row for "signature could
> not be checked". It is its own PR.
>
> **Two corrections to the cost as first recorded.** It said the check needs
> `WinVerifyTrust`/`CryptQueryObject` "in a TU compiled with no exception model" —
> `_HAS_EXCEPTIONS=0` comes from `fl_hostile_env_flags`, which
> `src/native/CMakeLists.txt` states explicitly is **not applied to the Injector**.
> That constraint becomes true only if the collector is placed in
> `fl_ac_rules.cpp`, which the Vulkan layer compiles in — which is its own reason
> to put it in `fl_guard_sources.cpp` instead, rather than drag wintrust and
> crypt32 into a DLL mapped into every Vulkan process on the machine.
>
> ~~And it is **not** a wiring change. `NameSink` is `bool(*)(void*, const char*)`
> fed by `GetModuleBaseNameA`, so the evidence the check needs — a module's full
> path — does not reach the decision point. The signer half requires widening the
> module seam, which is a new row in the fail-closed matrix, not a call added at
> `fl_guard.cpp:293`.~~
>
> ~~**And it must not be placed at `fl_guard.cpp:293`.** `NameSinkFn` latches the
> FIRST fragment-matching module per process (`!st->sawSuspicious`) and discards
> every later one. Harmless while any hit refuses; the moment a signer can CLEAR
> the latched name, a process that loads a trusted fragment-module before an
> untrusted one returns `Allow` with the second never recorded — a fail-open
> reachable by load order. The detection half at `fl_guard.cpp:203` has to be
> restructured, not extended.~~
>
> > **BOTH COSTS WERE PAID BY §S22(b), AND THIS ENTRY NEVER HEARD — struck 2026-08-27.**
> > They are the two clauses anyone costing (b) reads first, and both describe work that
> > has already landed:
> >
> > - **The seam is already widened.** `fl_guard.h` declares
> >   `using ModuleSink = bool (*)(void* ctx, const char* name, const wchar_t* path)`, and
> >   the module walk takes it. The full path reaches the decision point today; §S22's own
> >   entry records the change as *"not the size the plan assumed"*.
> > - **The restructure is already done**, and for this entry's own stated reason. The
> >   detection half no longer latches-and-skips: `ModuleSinkFn` returns *keep looking*
> >   after an exemption, and its comment says so in as many words — *"§S19(b) predicted
> >   exactly this and said the detection half has to be restructured rather than extended.
> >   This return is that restructure."*
> >
> > **What that leaves, and it is smaller than this entry prices:** one new `Sources`
> > member and one call beside `ModuleIsExempt`, in the same shape and with the same
> > fail-closed clauses. **The safety argument is unchanged and is the whole of it** — the
> > new call must return *keep looking* on a trusted module and never *stop*, or the
> > load-order fail-open the struck paragraph describes is reachable after all. It is still
> > a new fail-closed matrix row, and still its own PR.
> >
> > Struck rather than deleted: what went stale here is a **cost estimate**, and a cost
> > estimate that survives the work it was written about is how an item stays deferred
> > after its reason expires.

> #### `fl-probe-signer` EXISTS AND HAS BEEN RUN — 2026-08-27, dev box, unelevated
>
> This entry's deferral rationale asked for exactly one thing before any design is
> fixed: *"Build `fl-probe-signer` first, in the shape `fl-probe-guard` established,
> and answer those three questions with measurements."* It is `ctest fl_signer_probe`,
> and the numbers are in `spike-notes` §1. What belongs here is what they decide.
>
> **SAY THIS FIRST, BECAUSE IT WEAKENS EVERYTHING BELOW.** §S30 and §S31 each carry a
> decision table written *before* its run, so the mapping from measurement to action
> could not be assembled once the answers were known. **This table is not that.** The
> probe had to be run to be built — its acceptance criterion is that it *prints*
> answers, so an unrun probe is an unfinished one — and the dev-box numbers were
> therefore in hand before these rows were written. The rows below are pre-committed
> only for the legs that are still **unrun**: CI, and the adapters-disabled run. For
> the dev box they are a reading, not a prediction, and should be reviewed as one.
>
> **Q1 · the embedded route recovers an organisation for the CI blocker, and NOT for
> this entry's own subject.** Both halves matter and they point opposite ways:
>
> | Subject | embedded, offline | catalog, offline | `O=` |
> |---|---|---|---|
> | `System.Security.Cryptography.ProtectedData.dll` — the CI blocker | **`ERROR_SUCCESS`** | no catalog | `Microsoft Corporation` |
> | `mskeyprotect.dll` — this entry's own subject | **`TRUST_E_NOSIGNATURE`** | **`ERROR_SUCCESS`** | `Microsoft Corporation` |
> | `kernel32.dll` | `ERROR_SUCCESS` | `ERROR_SUCCESS` | `Microsoft Corporation` |
>
> So **the embedded half alone would clear the CI refusal** — the organisation it
> recovers is already in the shipped `trustedSigners` — while **`mskeyprotect.dll`
> still needs `CryptCATAdmin*`**, exactly as this entry predicted. The two are
> separable, and that separation is the affordable part: the merge-gate work does not
> have to buy the catalog half.
>
> `kernel32.dll` carries **both**, which contradicts the assumption this probe was
> written under — that a catalog-signed system binary is catalog-*only*. Recorded
> because it means "catalog-signed" is not a property you can infer from a file being
> a system binary; it has to be measured per file, which is what the probe now does.
>
> **§S19(b) ALSO MIS-DESCRIBES THE MODULE, and the correction closes a tempting
> route.** This entry calls it *"a .NET shared-framework assembly"*. It is not:
> `Microsoft.NETCore.App` 10.0.11 does not contain it. It is a **NuGet package
> assembly, version 6.0.0**, reached transitively as
> `Microsoft.NET.Test.Sdk` → `System.Configuration.ConfigurationManager` →
> `System.Security.Cryptography.ProtectedData`, and copied next to every test binary.
> The tempting inference — *a package reference can just be dropped* — **does not
> survive it**: dropping this one means dropping the test SDK.
>
> **Q2 · about 3.5 ms per module, and WARM IS NOT CHEAPER THAN COLD.** Cold 3.45 ms,
> warm mean of 20 = 3.54 ms. There is no amortisation to plan around: the second
> verification of the same file costs what the first did. Against a 30 s re-scan
> (§S6) that is comfortable *because the scan set is small* — three real titles
> produced no fragment hit at all — and it would stop being comfortable on a title
> that trips the fragment tier many times. A cache **within** one evaluation is
> admissible; a cache **across** evaluations is a re-scan that did not run.
>
> **Q3 · `cryptnet.dll` LOADS ANYWAY, and this is the finding that can stop the
> route.** Under `WTD_REVOKE_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL`, in a census
> bracketing the offline arm **alone**, `cryptnet.dll` is newly loaded into the
> process. Nothing further loads once the default `WTD_REVOKE_WHOLECHAIN` calls run,
> so this is not the default arm leaking into the measurement.
>
> > **The probe's first version could not have told those apart, and it read like an
> > answer.** It took one census at the top and one at the bottom with *both* arms in
> > between, so `cryptnet.dll` appeared and the delta could not attribute it. A census
> > that spans both arms of the comparison it exists to discriminate is not a
> > measurement. Fixed before this entry was written; recorded because it is the same
> > shape as §S30's *"two numbers agreeing can be one number read twice"*.
>
> **What loading is and is not.** `cryptnet.dll` being MAPPED is not a network
> request; it is the module that would make one. This probe has no packet counter, so
> it reports the module and refuses to conclude. **The discriminating run is the
> owner's: adapters disabled, same three subjects, compare the verdicts.**
>
> #### The rows, pre-committed for the legs that are still unrun
>
> | # | What the unrun leg reports | What it means | What to do |
> |---|---|---|---|
> | **G1** | Adapters disabled: all three verdicts UNCHANGED, and CI reproduces the blocker's `ERROR_SUCCESS` + `O=Microsoft Corporation` | The offline flags deliver offline behaviour, and the embedded half alone clears the CI refusal | **Build the embedded half**, and defer the `CryptCATAdmin*` catalog half **with its own written rationale** rather than silently. It is not on the path to the merge gate |
> | **G2** | Adapters disabled: any verdict CHANGES | The flags do not deliver offline behaviour — a verification whose answer depends on connectivity is doing network I/O inside the hard gate | **DO NOT BUILD IT.** §S19(b) stays deferred, now on a *measured* rationale rather than an assumed one. Re-open only with a mechanism that has no URL-retrieval path at all |
> | **G3** | CI reports an `O=` that is not `Microsoft Corporation`, or empty | The `O=` premise does not hold for the copy CI actually loads | **STOP.** A signer half reading the wrong field suppresses the wrong modules. Settle `signerField` in `19_SAFETY` §Blocklist seed first |
> | **G4** | CI's blocker copy returns `TRUST_E_NOSIGNATURE` | CI stages a different copy than the dev box does | Measure *which* copy before deciding. The dev-box answer does not transfer |
> | **G5** | Cost on CI exceeds ~10 ms/module | The 30 s loop absorbs it only for a small scan set | Not a veto alone: bound the scan set or cache **within** one evaluation, and re-measure. Never across |
> | **G6** | Anything not covered above | The probe found something nobody predicted | **Not a result.** Print it, add a row, re-run. Do not promote a surprise into a design |
>
> **And a constraint no row can lift, because it is not a measurement.** Wiring the
> signer half makes `trustedSigners` a **live allow-widening surface**: today the field
> suppresses nothing, and the moment it does, a rules push that adds a publisher widens
> the hard gate. The gate over that is `Rules / validate`, which **§S23-2 measured is
> not a required status check on `main`**. `19_SAFETY` already ruled on this exact shape
> for the launcher list — *"a data-driven cutoff would let a rules push widen the hard
> gate's blind spot ... the boundary of what the gate looks at is code."* **So even a
> clean G1 does not authorise the build**: either `Rules / validate` becomes required
> (§S23-2, owner-only, and the skip-shim lands first), or `trustedSigners` is bounded by
> a compiled-in allowlist the data file can only intersect. **That is an owner decision
> and no PR may take it.**
>
> **Nothing under `FrameLedger.Injector` changed with the probe**, deliberately. This
> is a measurement, and reading a row off the table is a separate PR — including the
> row that says build nothing.
>
> **THE CI LEG WAS NEVER READABLE, found 2026-09-06 when it was finally looked for.**
> `ctest fl_signer_probe` passes on `BLOCKER: (FOUND|NOT PRESENT)` and ctest prints nothing
> for a passing test, so eleven days of green CI carried none of the probe's answers — the
> leg rows G3/G4/G5 are written for was "run" in the sense that a binary exited 0. And the
> native tests run BEFORE `dotnet restore` in `build.ps1`, so on a cold runner the NuGet
> copy the probe searches first may not exist yet at ctest time. `ci.yml` now runs the probe
> as its own step AFTER the gate (`continue-on-error`, a measurement and not a gate), and the
> answers are read off that step's log. **Owner-only, still unrun:** the adapters-disabled
> leg (rows G1/G2 — a network setting, not a PR), and the allow-widening bound that no row
> can lift (below). The recommendation for the bound, so the owner decides between two
> written options rather than one: **bound `trustedSigners` by a compiled-in allowlist the
> data file can only intersect** — the boundary of what the gate trusts stays code, exactly
> as `19_SAFETY` ruled for the launcher list, and it needs no branch-protection change; the
> alternative, making `Rules / validate` a required check, is §S23-2 and is the owner's
> GitHub setting, not a file in this repository.
>
> **THE CI LEG, READ 2026-09-06 off the new step (run on `ci/signer-probe-leg`, `windows-latest`,
> the probe reporting `elevated: yes` — the runner is an administrator; the dev-box leg was
> unelevated).** The blocker at the runner's NuGet cache (`system.security.cryptography.protecteddata`
> 6.0.0, `lib/net6.0`): embedded/offline **`ERROR_SUCCESS`, 3.47 ms, `O= Microsoft Corporation`**,
> no catalog; `mskeyprotect.dll` `TRUST_E_NOSIGNATURE` / catalog `ERROR_SUCCESS`; `kernel32.dll` both;
> cold 3.45 ms, warm 3.53 ms; `cryptnet.dll` newly loaded under the offline flags, as on the dev box;
> the canary `TRUST_E_NOSIGNATURE`; the probe's own verdict *"G1 is a CANDIDATE"*. **So G3 does not
> fire (the `O=` premise holds on CI's copy), G4 does not fire (CI's copy is embedded-signed), G5 does
> not fire (3.5 ms).** What is left before G1 can be read is exactly the two owner-only legs above:
> adapters disabled (G1 against G2), and the bound.
>
> **THE ADAPTERS-DISABLED LEG, the owner's run, 2026-09-06 (dev box, elevated; reported in answer
> to the request to run it with every adapter off — if it was not, this paragraph is withdrawn
> and the leg is unrun).** Every verdict UNCHANGED against `spike-notes` §1: `kernel32.dll`
> embedded `ERROR_SUCCESS` / catalog `ERROR_SUCCESS`; `mskeyprotect.dll` embedded
> `TRUST_E_NOSIGNATURE` / catalog `ERROR_SUCCESS`; the blocker embedded `ERROR_SUCCESS`, no
> catalog, **`O= Microsoft Corporation`**; canary `TRUST_E_NOSIGNATURE`; `cryptnet.dll` still
> mapped under the offline flags and still not evidence of a request. Faster than the online run
> (2.14 ms cold, 2.15 ms warm; 2.97 / 2.13 ms on the system binaries), which is what an offline
> cache-only path should look like. **One detail worth its line:** the blocker's subject now reads
> `CN=.NET` where the August copy read `CN=Microsoft Windows` — the NuGet copy was re-staged with a
> different certificate — and `O=` is the same on both, which is the `signerField: "O"` decision
> confirmed on a subject class it was never measured against. **Row G1 reads.** The embedded half
> alone, offline, clears the CI refusal; the `CryptCATAdmin*` catalog half is deferred with its own
> rationale (it reaches `mskeyprotect.dll`, which is not on the path to the merge gate, and it is
> the `CatRoot` walk §S19(b) priced). **What still stops the build is the bound**, above — the
> owner's, and asked for beside this reading.
>
> #### ✅ BUILT 2026-09-06 — the embedded half, on row G1, under the compiled-in bound (owner: option 1)
>
> **The owner chose the bound the same afternoon: a compiled-in allowlist the rules file may only
> intersect.** `IsCompiledTrustedSigner` in `fl_ac_rules.cpp` names the five organisations the shipped
> rules name, and `IsTrustedSigner` now requires BOTH lists — a rules push that adds `"Evil Corp"`
> widens nothing, which is one assertion in `fl_guard`. `Sources` gained
> `ModuleSignerOrganisation`: `WinVerifyTrust(WTD_CHOICE_FILE)` under `WTD_REVOKE_NONE |
> WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_SAFER_FLAG`, and only after `ERROR_SUCCESS` the signing
> certificate's subject `O=` through `CryptQueryObject` — the probe's shape with fixed buffers, in
> `fl_guard_sources.cpp` (never `fl_ac_rules.cpp`, which the Vulkan layer compiles: wintrust and
> crypt32 stay out of every Vulkan process). `ModuleSinkFn` asks it after the ours exemption and
> BEFORE latching: a trusted module returns *keep looking*, exactly as the exemption does, so the
> load-order fail-open this entry predicted is closed by the same structure — and it applies to the
> TARGET too, because a game loading a Microsoft-signed key-protection provider is the false refusal
> the half exists to remove. **The fail-closed row, added:** no embedded signature (every catalog-only
> system binary — `mskeyprotect.dll` by design), an invalid one, an unreadable `O=`, a null path, a null
> seam, a seam that fails, an organisation on one list but not the other — all untrusted, all refuse.
> The catalog half stays deferred: it reaches only modules that are not on the path to the merge gate,
> and it is the `CatRoot` walk this entry priced.
>
> **Proved in both directions, against the real seams:** `hook-harness --load <dll>` puts a named module
> into the target before the guard looks. The NuGet copy of the CI blocker → **Allowed**, Overlay
> injected (skipped loudly on a machine with no NuGet cache; the managed integration cases are the gate
> that cannot skip it). Our own unsigned Overlay copied under a `protect`-bearing name outside the
> guard's directory → **SuspiciousUnsigned**, naming it, nothing injected. Nine fake-seam cases pin the
> decision (both lists, the target, kFailed, missing seam, pathless, load order, exact-hit precedence).
> **And `-SkipIntegration` is gone from `ci.yml` in the same PR**: the nine `Category=Integration`
> cases are in the merge gate for the first time since 2026-08-05, and CI runs the same
> `./build.ps1 check` a developer does. The switch stays in `build.ps1`, loud, for whoever must.

**(c) `signerField` and `action` are required by the schema and parsed by
nobody.** Both are `required` in `detection-rules.schema.json`, both carry
safety-relevant `$comment`s — `action` "is a const, not an enum, so `allow` and
`warn` are unrepresentable" — and neither appears anywhere in `fl_ac_rules.cpp`.
The heuristic's policy is hardcoded at `fl_guard.cpp:293`. So the
**warn-and-refuse** behaviour `19_SAFETY` §Blocklist seed describes is not
configurable and never was; the field that would express it has no consumer.

> Two corrections, 2026-08-04. This cited **`19_SAFETY:264`**, which is a
> blocklist table row (`| mihoyo protect | drivers | prefix | mhyprot |`); the
> sentence is at `19_SAFETY:281`. And "hardcoded at `fl_guard.cpp:293`" names only
> the *refusal* half — the *detection* half is at `fl_guard.cpp:203-206`, which is
> where the first-hit latch lives and where anyone implementing (b) has to work.
> Line numbers were dropped from this entry rather than corrected: they are what
> went stale.
>
> `action` is worth costing honestly before anyone wires it. It is a schema
> `const` with exactly one legal value, so a parser reading it can only ever see
> `warn_and_refuse` — a predicate whose answer changes nothing observable, which
> is the defect class this file exists to record. Wiring it means first deciding
> that `warn` or `allow` may exist, and CLAUDE.md rule 2 says they may not.
>
> #### ✅ Resolved 2026-09-06 — the consumer is the validator, and the policy is code by rule
>
> Decided the way the paragraph above costs it: `warn` and `allow` do not exist (CLAUDE.md
> rule 2), and the signer field is `O=` by measurement (`19_SAFETY`), so neither field is
> for the guard to read — they exist so that the DATA cannot claim otherwise. What was
> missing was the thing that makes that true: `rules-validate` now carries a third canary
> that mutates the shipped document to `action: "allow"` and to `signerField: "CN"` and
> requires the schema to reject both. Delete either `const` and the gate goes red. The
> guard's hardcoded refusal is the specification, and `IsCompiledTrustedSigner` (§S19(b))
> made the other half of the heuristic code-bounded the same day.

**(d) ◐ The fragment list has unreconciled copies — more than three. The runtime
hole is CLOSED.** `rules/detection-rules.json`, `guard_test.cpp` (the inline
`GoodRulesJson()` fixture) and `rules_budget_test.cpp` each carry their own, and
re-counting on 2026-08-04 found two the entry had missed: the prose in
`19_SAFETY` §Heuristic tier, and a `$comment` in `detection-rules.schema.json`
that restates the **four-fragment** version — the exact staleness §S19(e) was
raised about, sitting in the schema that gates the data. The generated floor is
not a copy: it is derived at build time and cannot drift.
`ParseRules` guards the entire heuristic read behind `heurTok >= 0` and
`IsCompleteEnoughToGate` never looks at fragments, so a rules file with no
`heuristic` block parses `kOk` and the tier silently stops existing.

> **Half of this entry was wrong, and the wrong half was the one that made it
> sound urgent.** It said "a rules push can empty `nameFragments` … and every gate
> stays green". Measured 2026-08-04 with `Test-Json` against the real schema:
> emptying the array fails (`minItems: 1`), and deleting the block fails
> (`heuristic` is in `anticheat.required`). `tools/rules-validate.ps1` runs that
> schema behind a discriminating canary, so **CI already refuses both**.
>
> `rules-validate.ps1` does contain zero *imperative* heuristic checks, which is
> what was actually observed and then over-read. Adding one would be a gate whose
> red input the schema already eats.
>
> What genuinely remains is narrower and sharper:
>
> - ~~**The runtime hole**, above, sequenced with §S20.~~ **Closed 2026-08-04 by
>   the generated floor**, and by a mechanism this entry did not consider: the
>   fragments are seeded into `Rules` before the file is read, so the tier cannot
>   stop existing because the data never supplied it. That needs no new
>   `ParseResult` cause, so `kRulesIncomplete`'s signal stays true and `layer.cpp`
>   is not driven inert. See §S21's floor note.
> - **✅ The schema canary does not discriminate on this constraint — closed
>   2026-08-05.** It was `{"schemaVersion":"not-a-number"}`, which any schema
>   still pinning `schemaVersion` rejects. Delete `minItems` from `nameFragments`
>   and that canary still passed, `Test-Json` still passed, and the schema half of
>   the floor was gone.
>
>   A second canary now carries `nameFragments: []` and must be rejected. It is
>   **derived from the shipped document** — parse, empty the array, re-serialise —
>   never hand-written, because a hand-written canary is a second statement of the
>   schema's shape and drifts from it, which is the defect this file exists to
>   catch. Mutating the real document makes the constraint under test the *only*
>   difference between the passing and failing cases. Proven red by deleting
>   `minItems` from the schema.
>
>   **The consequence in the bullet above was overstated and is corrected here.**
>   *"the CI floor silently ceases to exist"* is not what would happen.
>   `tools/gen-ac-floor.ps1` hard-errors on an empty fragment list and runs as a
>   CMake custom command, so the native **build** fails. What was unguarded is the
>   *schema* half — a rules file with an empty list reaching a machine, where the
>   compiled-in floor is what saves it. Narrower than written, and worth fixing on
>   its own terms rather than on an inflated one.
> - **✅ `trustedSigners` now has a gate in the direction that matters — closed
>   2026-08-05, and it took two attempts.** The original entry was right: the job
>   failed on family *removals* while an *addition* to the signer allowlist was
>   invisible, and that field suppresses refusals, which `19_SAFETY:74-81` already
>   forbids for the launcher list ("a data-driven cutoff would let a rules push
>   widen the hard gate's blind spot … the boundary of what the gate looks at is
>   code").
>
>   `cea744e` appeared to close it and did not. It added `trustedSigners` to
>   `Get-Tokens`, whose sole consumer is `$removed = old − new` — so an addition,
>   present only in *new*, still could not appear. Worse, removals then **did**
>   fire, and removing a trusted signer makes the guard stricter: the gate blocked
>   the safe direction and passed the dangerous one, beneath a comment asserting
>   "rules-publish cannot see such an addition. **It can now.**" A shipped comment
>   claiming a capability the code structurally cannot have, which is this file's
>   own recurring defect, in the commit that wrote this bullet's neighbours.
>
>   The field now has its own `$new − $old` comparison. Proven by extracting the
>   shipped step from the YAML and running it against the real seed — five cases,
>   before and after: adding a signer `PASS → FAIL`, removing one `FAIL → PASS`,
>   the three pre-existing cases unchanged. **Still a prerequisite of §S19(b)** in
>   the sense that matters: the gate makes an addition *reviewable*, not
>   impossible, and the field stays inert until the signer half is wired.

> Adding a runtime floor is right but not free: routing it through the existing
> `kRulesIncomplete` would make that reason's hardcoded signal — *"a required
> anti-cheat family is missing"* — a lie, which is the exact defect the
> `InjectionFailed` fix closed one commit earlier. And `layer.cpp` treats any
> non-`kOk` parse as **inert passthrough**, so a heuristic-only floor failure
> would silently disable the Vulkan layer machine-wide — over a tier the layer
> never uses, since `RunSelfScan` calls only `MatchName`. Both need a
> `ParseResult` that distinguishes causes.
>
> Note the shape of that prerequisite precisely: `ParseResult` **already** carries
> a cause (`kOk`/`kMalformed`/`kTooLarge`/`kIncomplete`, mapped to three distinct
> reasons). What is missing is a finer distinction *inside* `kIncomplete`, plus a
> layer-side policy for a non-safety floor failure. Stated as "needs a ParseResult
> that carries its cause", this entry sends someone to build an enum that exists.

**(e) ✅ Closed — the normative doc was already fixed, and this entry was the
stale one.** It claimed `19_SAFETY` lists four fragments. As of commit `bb0da0a`
— the same commit that recorded §S19 — `19_SAFETY:281` lists all five, and the
lines beneath it carry a correction block covering the fragment count, the
`gameguard` subsumption and the unread `action` field.

What survives is not a doc error but a missing gate: **nothing prevents the drift
recurring.** `rules-validate.ps1`'s doc/data cross-check parses only the
§Blocklist seed *table*, so the fragment sentence is invisible to it. That check
is worth extending; the claim that the doc is wrong is not.

> Recorded rather than deleted, because the entry contained the exact defect it
> complained about — a doc citation (`19_SAFETY:264`) pointing at text that is not
> there. Line 264 is a blocklist table row.

### S20 ◐ · Delivering the rules file — **seed half done, feed half open**

The guard reads its blocklist from one place under Local AppData, and until
2026-08-04 nothing in this repository ever wrote a file there. On any machine that
had not hand-installed one the guard answered `RulesUnreadable` for every title —
correct fail-closed behaviour, and also the whole story: the first real
injection's opening refusal was exactly this.

> **The entry's own opening sentence was not quite true**, and the exception
> matters for anyone reading it as a survey. `fl-probe-vklayer` DOES seed the
> file — install-only-if-absent, path resolved through `fl::guard::RulesFilePath`
> — and then deletes what it installed. So the repository had a writer; it had no
> *product* writer, and a ctest that removes its own work leaves the machine
> exactly as it found it.

**✅ The seed half is built.** `rules/detection-rules.json` now ships in the
Agent's output (`FrameLedger.Rules.targets`), and `RulesSeeder` installs it to the
product location. The Agent is the sole writer, per §S18's ratification, and it is
a real caller rather than a component nothing invokes: `Program.cs` runs it and
reports the outcome.

Measured end to end on this machine — remove the file, the guard answers
`RulesUnreadable`; run the Agent; the guard reads its rules and reaches check 1.

Four decisions worth keeping, each of which replaced something the design review
disproved:

- **Provenance, not `rulesVersion`.** The first design replaced the installed file
  when the packaged seed was strictly newer. Measured against this repository's
  own history, every commit that changed the `anticheat` block left `rulesVersion`
  untouched and the one commit that bumped it changed the block not at all — so
  the rule would have delivered **none** of the changes it existed for. The seeder
  records a hash of what it installed instead.
- **`ReplaceFileW`, not `MoveFileEx`.** See §S21: the prescribed primitive returns
  `ERROR_ACCESS_DENIED` against a live reader. `ReplaceFileW`'s backup parameter
  also makes `05_DETECTION`'s "the last valid copy is kept" a mechanism rather
  than a sentence.
- **Validated by the guard's own parser.** `FlGuardCheckRules` is a new
  observation-only export, because `DetectionRulesFile` never reads the
  `anticheat` block (§S15) — validating with it would have checked everything
  except the half the hard gate consumes, and could have installed a document the
  guard then refuses for every title while reporting success.
- **A usable file we did not write is left alone.** Safe only because §S21's floor
  is now generated from the shipped blocklist: a rules file can ADD and cannot
  remove. Under the narrow floor the same rule would have handed permanent control
  of the blocklist to whoever created the file first.

#### What is still open, and it is not small

- **The feed half.** `05_DETECTION` §Trust and staleness specifies an HTTPS fetch
  with validate-then-replace; none of it exists. So **FR-7.3 is unmet**: anti-cheat
  entries cannot reach a machine on their own schedule, only with a release. A
  rules edit still changes nothing on any installed machine until the next build.

  > **🅓 Deferred to P4, rationale written 2026-09-06.** The feed is the product's one
  > permitted outbound request after the release check (CLAUDE.md rule 8), and it needs
  > three things P0 does not have: a shipped Agent to run it in, the signing §S4 defers to
  > the same phase, and the privacy policy's rows that describe it (~~`legal/PRIVACY_POLICY.md`
  > already flags them as describing a request the software does not make~~ — **removed in version 2.2, 2026-09-16**:
  > the feed lands as a new policy version the Legal Gate shows again, not as rows kept in advance). Until then a
  > rules edit reaches a machine with the next release, whose checksum attests it — a slower
  > delivery, not a weaker gate. FR-7.3 stays unmet and stays written as unmet.
- **No binary/data handshake inside the guard.** `ParseRules` still walks only the
  `anticheat` subtree and never reads `schemaVersion` or `rulesVersion`. Teaching
  it to refuse an unknown version would be a second machine-wide refusal lever
  pulled by data, and `rules-publish.yml` gates neither field — see `13_CI_CD`,
  which used to claim a bump check that does not exist.
- **`trustedSigners` is the one allow-widening field a foreign file still
  controls.** Deliberately not floored, because flooring an allowlist has the
  wrong polarity. ~~Inert today — `IsTrustedSigner` has no production call site
  while §S19(b) is deferred — and~~ **live the moment the signer half is wired** *(corrected 2026-10-04: wired since
  §S19(b), 2026-09-06, and bounded by `IsCompiledTrustedSigner`)*,
  which makes gating it a prerequisite of §S19(b) rather than of this item.
- **This is what arms the Vulkan layer's self-scan for the first time.** `layer.cpp`
  §S2's second half could only ever answer "inert" while the file never existed.
  It is now reachable, on a machine where the file is present. ~~The layer still
  intercepts nothing (`vkQueuePresentKHR` is P1), so nothing observable changes
  yet — recorded here so it is not discovered as a surprise when the present hook
  lands.~~ *(Corrected 2026-10-04: `vkQueuePresentKHR` is intercepted since 2026-09-06, §S2 part three.)*
- **A seed replacement is a rules update in FR-2.3's sense**, and the obligation
  attached to that event — re-run the pre-scan, force-disable a game that has
  started matching — ~~is unimplemented. There is no `games` table yet, so there is
  nothing to write; named here so whoever builds persistence has an anchor.~~ *(corrected 2026-10-04: built —
  `UpdateRules` wakes `AntiCheatPreScanSweep`, beta.8, keyed per rules version by schema 0010.)*

### S23 · What the 2026-08-05 handoff audit found and did NOT fix

Eight PRs merged in one session; the audit ran afterwards, as
[adversarial review] prescribes, and found 53 items. The staleness was fixed in
the same PR that records this. What follows is the residue — real gaps, recorded
so the next session finds them by reading rather than by tripping over them.

**1. ✅ `FL_BUILD_ID` has a writer and no reader — closed 2026-08-05.**
`FlGuardBuildId` on `FrameLedger.Guard.dll` gives the Agent the value `04_CAPTURE`
calls "its own", and `FrameLedger.Shared.ShmHandshakeValidator` is the comparison.

**Why the GUARD carries it and not the Overlay.** The Overlay exports
`FlGetBuildId()`, and the Agent cannot call it: reaching that export means
`LoadLibraryW` on `FrameLedger.Overlay.dll`, which starts its init thread and
creates a ring under the **Agent's own pid**. The payload is not something its own
host may load. The guard is already loaded by the Agent, by absolute path from our
install directory (§S22).

**Guard and Overlay agree by construction, not by test, and that is stated rather
than implied.** `FL_BUILD_ID` is one INTERFACE compile definition on
`FrameLedger.Shm`, set once per CMake configure, and both targets link it. No test
asserts the equality because `fl_guard_abi.cpp` is not compiled into
`fl_guard_test` — `guard_test.cpp` says so at the assertion that covers the
Overlay's half.

**The failure this closes is a fail-open, and the test for it was nearly missed.**
The naive implementation compares two strings. With neither side carrying an id,
`string.Equals("", "")` is **true**, so the gate reports `Ok` and permits attaching
to anything — measured, by removing the emptiness guards and watching
`ShmAttachRefusal.Ok` come back. The first draft of the test file asserted "no id
of our own refuses" and "no id in the handshake refuses" as separate cases and
**never put both halves in one call**, which is the only arrangement that reaches
the defect. Recorded because a suite can cover each half of a condition and still
miss the condition.

<details><summary>The finding as recorded</summary>

**1. `FL_BUILD_ID` has a writer and no reader.** §S22-era work gave
`FlShmHandshake::buildId` a producer, which it had never had. But the contract is
a *comparison* — `07_IPC` "the Agent compares … against its own",
`04_CAPTURE` "validate layout version + build id against our own" — and the
managed side has no build id and no way to obtain one: `FL_BUILD_ID` is a CMake
INTERFACE compile definition visible only to native targets, and `grep -rni
buildid` over `src/**/*.cs`, `*.csproj` and `*.props` returns **zero**. So the
refuse-to-attach-on-mismatch gate still cannot run. Half a mechanism reads as a
whole one in the CHANGELOG, and that is corrected here rather than there.

</details>

**2. `rules-publish`'s removal check is not a required status check.** `main`'s
required contexts are exactly `check`, `analyze (csharp, none)` and
`analyze (cpp, manual)`. The `validate` job is path-filtered and runs on
`pull_request` only, so a red removal check **does not block the merge button** —
the gate that exists to make the anti-cheat blocklist un-removable is advisory.
Fixing it is a branch-protection change, i.e. the owner's, not a code change.

**3. ~~🅓 Deferred to P3, rationale written 2026-09-06~~ ~~🔴 Open~~ ✅ Closed in beta.13 (`08_UI` §Safety events: a driver or service is said as running on this PC) — P3 built the refusal UI without it (corrected 2026-10-04: `SafetyNotices.RefusalText` formats every named refusal as "… was detected in this game", so the measured case below still reads wrong) — `08_UI` describes a refusal notice that is wrong in the commonest case.** *(The requirement below must reach `08_UI` and the `Safety_*` keys in the PR that creates the first refusal UI, and not before: there is no UI to be wrong in yet, and a `.resx` string with no view is the kind of fix that reads as done. The spike log carries the measurement; this line is the reminder.)* It
promises "the specific signal named in plain language". Measured (`spike-notes`
§13): while any Easy Anti-Cheat title is running, **every** target — including a
freshly spawned, unrelated process — refuses with `BlockedService` naming
`EasyAntiCheat_EOS`, because checks 2 and 2b are machine-wide. The user is shown
the name of a game they are not playing. The requirement created by that
measurement was written only into the spike log; it needs to reach `08_UI` and
the `Safety_*` resx keys before any UI exists.

**4. ✅ The runtime re-scan loop is described as two checks and implements four — closed 2026-08-05.**
`19_SAFETY` §During a session now reads "every pre-injection check", deferring to the
one list in §Pre-injection checks — including its ⚠ that check 3's store-id half cannot
be called, which the re-scan inherits rather than closes. A list restated in two places
is what went stale; the fix is to stop restating it, not to correct the copy. The two
checks the old wording omitted are named there, with why each matters.

> **This closure was itself stale within hours, and the mechanism is worth naming.**
> The sentence it landed said *"including its ⚠ that check 3 is unwired … one of the
> four documented ones still does not [run]"*. #52 rewrote that ⚠ three hundred lines
> away and the pointer kept paraphrasing the old text — **the exact restate-in-two-
> places failure this item was closed for, committed by the closure.** Deferring to
> another section does not help if the pointer restates what it points at; a paraphrase
> is a copy. It was also wrong about the count: 2b is a documented check, so five run,
> not four. Corrected 2026-08-05.

<details><summary>The finding as recorded</summary>
`19_SAFETY` §During a session reasons explicitly about the loop's composition and
names the module and driver scans. `GuardSupervisor.ScanOnceAsync` →
`FlGuardEvaluate` → `EvaluateImpl` runs drivers, **services** and the **static
pre-scan** as well. That omits the only tier ever measured firing on real
anti-cheat (services) and the only one touching the filesystem (check 4) — whose
cost this session raised by deepening the walk. The paragraph that decides what
the loop costs is missing the expensive half.

</details>

**5. ✅ A fourth statement of the gate's composition lived in the shipped data — closed 2026-08-05.**
`rules/detection-rules.json`'s own `$comment` enumerated the checks that run,
omitting 2b and omitting `services` from the signals that catch the seed — in the
one copy that ships to users. Three doc-side variants were reconciled that
session; this one was not, and `rules-validate`'s doc/data cross-check reads the
blocklist table, not the comment.

> **Closed the way §S23-4 closed the same class: by removing the restatement, not
> by correcting it.** The comment now states only facts about *this data* — that
> both per-title arrays are empty, and where the composition is documented — and
> `rules-validate` fails when any `$comment` in the document enumerates checks
> again. Proven red by putting the old sentence back.
>
> **The first version of that rule could not fire, and that is the part worth
> keeping.** It was scoped to `anticheat.$comment`; the text it exists to catch
> lives in the **top-level** `$comment`. A check pointed at the wrong object —
> this file's signature defect, committed inside the fix for it. Its canary
> reported green twice before the cause was found: once because a backtick inside
> a double-quoted PowerShell needle silently mangled the search string so the
> mutation never applied at all (the harness, for the sixth time on this project),
> and once for real. The rule now walks **every** `$comment` in the document and
> **fails if it finds none**, because a walk that reports clean having looked
> nowhere is the same defect one layer up.

**6. `legal/` is audited by nothing.** Every other document is bound to the code
by something — `rules-validate` cross-checks the blocklist against `19_SAFETY`,
`static_assert`s bind `fl_shm.h` to `07_IPC`, `versioninfo-check` and
`chokepoint-check` bind claims to binaries. `legal/` has one hand-written
accuracy block, in one of four files, with a hardcoded count. It went stale
within hours: a fourth unimplemented promise was added directly beneath a header
saying "Three". The block now says so about itself, which is a note and not a
gate.

> **◐ Partly closed 2026-08-05, and what it cost to find is the point.**
> `license-check.ps1` now binds `THIRD_PARTY_NOTICES.md`'s bundling claims to the
> filesystem, bidirectionally, failing rather than skipping on a renamed row. That
> is the first real gate over anything in `legal/`.
>
> It was written because the un-audited half was **already false in two rows**:
> NVAPI ("**Yes** — headers and import library vendored. **Verified 2026-08-02**")
> and Intel PresentMon ("Bundled as a pinned native binary; SHA-256 verified at
> build"). Neither exists — `src/native/third_party/` holds `CMakeLists.txt` and
> `vulkan-headers`, and `assets/` is not a directory. The old check keyed on the
> path a component *would* occupy, so it could only ever fire on
> vendored-without-a-licence; the reverse was structurally invisible.
>
> **Still open, and it is the larger half:** the *accuracy blocks* — DISCLAIMER's
> four-going-on-five unimplemented promises, and now this file's bundling audit —
> remain hand-maintained prose that nothing verifies. A gate over one table is not
> a gate over `legal/`.
>
> **✅ Closed 2026-09-06, by making there be one block.** `legal/ACCURACY.md` is the single
> statement of what the software measures, dated; `README.md` and `legal/DISCLAIMER.md` §4
> embed it verbatim between markers, and `tools/accuracy-check.ps1` — a `build.ps1` gate with
> a four-case self-test, both directions — fails when any copy differs from the source, when
> a copy has no markers, or when the source is empty. What the gate does NOT do is decide
> whether the block is true; that stays with whoever changes the Overlay or the capture host,
> and the block's own comment says so. The block was rewritten to 2026-09-06 truth in the same
> PR (the previous §4 note still said frame generation and ray tracing had no hook).

### S12 ✅ · "Cautious mode" — **closed by deferring it, explicitly**

Deferred to v1.1 with the overlay, recorded in `19_SAFETY` §Crash & stability
safety. It was defined as "hooks installed, overlay drawing disabled" and **v1
draws no overlay** (FR-15 is v1.1), so it disabled nothing — a no-op dressed as a
safety measure, which is worse than an absent feature because it reads as
coverage. The breadcrumb is still written and still read in v1; the behaviour
that actually protects the user is the existing two-crashes-in-60s auto-disable.

---

### Closed S-series entries — in `open-questions/closed-s.md` (2026-10-19, D66)

Moved verbatim, in this order; §S24 above stays the index of every S item's state.

| Entry | In |
|---|---|
| S26 ✅ · The ring counted occlusion probes as frames — closed 2026-08-05 | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S28 ✅ · The guard is not re-entrant, and nothing enforced that — closed 2026-08-05 | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S30 ✅ · The record names the WRONG upscaler on a real title — closed 2026-08-15, and the fix broke the inst… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S30 ❓ · The record names the WRONG upscaler on a real title, and it is honest while being wrong | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S31 ✅ · Is a drained Streamline batch an application frame? — YES, measured 2026-09-04 by an oracle that di… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S31 ◐ · Is a drained Streamline batch an application frame? — the ORACLE is retired, the QUESTION is not *(… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S27 · The chokepoint is the ANTI-CHEAT gate, and it is not the consent gate | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S25 ✅ · Both runtime stops were unreachable in a non-presenting process, and pause was unreachable on a tic… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S2 ✅ · The Vulkan layer has no guard — both halves done; part three built 2026-09-06 (P1 item 3) | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| ~~◐~~ ✅ Part three: mid-session guard inside a layered process — decided, ~~half built~~ built 2026-09-06 (… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S3 ✅ · UpdateRules { path } — closed, and it was not alone | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S5 ✅ · detection-rules.json schema — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S13(c) · Is launch-mode injection salvageable? | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S15 ✅ · The four consequences of putting the guard in C++ (§S13(a)) — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S16 ✅ · *Which* process does the guard scan? — closed: the game's own subtree | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S17 ✅ · The schema accepted rules files the guard refuses to parse — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S18 ✅ · The guard refuses itself — closed 2026-08-04 | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S21 ✅ · The hard gate's data source was caller-nameable, and its completeness floor never read the values —… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S22 ✅ · The guard gated the TARGET and nothing gated the PAYLOAD — both halves closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S14 ✅ · Pre-injection check 3 ~~is unwired, and has no "cannot determine" state~~ — both halves wired and s… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S6 ✅ · The 30 s scan window is the weakest part of the most important behavior — the in-process half built… | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S7 ✅ · Guard handle rights and WOW64 — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S8 ✅ · The absence-of-override mechanism — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S9 ✅ · FrameLedger.Injector.exe — closed: it does not exist | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S10 ✅ · Machine-wide layer registration without consent — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |
| S11 ✅ · FR-2.2 / FR-2.3 interaction — closed | [`open-questions/closed-s.md`](open-questions/closed-s.md) |

## H — Native hook layer. Blocks P1.

> **H1 and H3 are resolved, and both flags are now applied** to the Overlay and
> the Vulkan layer via the `fl_hostile_env_flags` interface target. Evidence in
> `spike-notes.md` §3; the probe lives at `src/native/tools/fl-probe-hookprofile`
> and runs as ctest `fl_hook_profile`, so a regression fails the build rather
> than reaching a game.
>
> Two residual risks were recorded rather than waved away, and both feed §H8:
> CFG **strict mode** was off during the measurement, and `-D_HAS_EXCEPTIONS=0`
> turns a would-be throw into `__fastfail` — an uncatchable kill of the host
> process.

### H2 ◐ · `LoadLibrary` hook, the loader lock, and MinHook's thread suspension

**Partly answered — the safe path is verified, the hazard is still an argument.**

`fl-probe-hookprofile` ran 20 MinHook enable/disable cycles, each suspending
every other thread to patch, while a worker performed 4,510
`LoadLibrary`/`FreeLibrary` cycles. No deadlock. So the **deferred** pattern
`17_HOOK_ENGINE` §DLL entry mandates is sound under heavy loader contention.

What is *not* proven is that the naive inline install deadlocks. A probe that
deadlocks cannot report its own result, and hanging CI to demonstrate a hazard
we have already chosen to avoid buys nothing — so the case against inlining
still rests on the mechanism (suspending a thread that holds the loader lock),
not on a measurement.

**Remaining:** exercise the deferred path against a real game that loads D3D12
lazily, which is the case the hook exists for. Until then, keep the rule and do
not let anyone "simplify" it back to an inline install on the grounds that the
probe never showed a deadlock.

> **The deferred path is built and exercised in a fixture (2026-09-06, P1 item 1 — §S6).** The detour
> signals an auto-reset event the watchdog waits on with its 1 s timeout; it never calls MinHook.
> ctest `fl_guard` `[loader]` loads `sl.interposer.dll` into a presenting harness 2.3 s after start and
> sees the hook family within 94 ms, wake count ≥ 1, no fault, no stop. **Still owed, unchanged:** the
> real-game leg — a title that pulls Streamline or D3D12 in lazily, hooked through the wake — which the
> `loader detour:` report line will show as a non-zero wake count with the family present from the first
> bucket.

### H11 ◐ · XeFG and FSR3-FG identity — **AMD built 2026-09-04 through the ffx-api leaves, complete 2026-09-06; Intel closed by licence; FSR 3.0 host route built 2026-09-05**

> **2026-09-06: the AMD half is complete on its oracle** — Lies of P at FSR 3.1 + FSR FG on the #129 build
> prints `Fsr3` / `FsrFg` / `59.77 → 119.55 (×2 FG)` / `2560x1440 -> 2560x1440` with `presents/frame=2.00`,
> `frames/upscale-drained=1.00`, `unseen=0` and the NVIDIA driver's word saying nothing was created
> (`spike-notes` §9); the owner called it done. **Intel, re-checked the same day: the licence has not
> changed** (`18_GPU_VENDOR_APIS` §Intel), so nothing in `libxess*.dll` may be hooked or declared, and the
> signature-free thunk stays refused. **What is left in policy for an XeFG session, proposed and not yet
> built — identity BY ELIMINATION, labelled as such:** frame generation is COUNTED by the Streamline token
> (UE titles request it whatever the FG vendor — measured on Rune Factory's compiled-in FSR) or by DXGI's
> counter; DLSS-G is EXCLUDED by the NVIDIA driver's FG word (no feature created) and by the absence of the
> HUD-less / UI tags; FSR-FG through a shipped DLL is EXCLUDED by the ffx census (no FRAMEGENERATION dispatch
> at any hooked module); the frame-generation runtime the census names is `libxess_fg.dll`. The line would
> read `Active (technology not identified) — by elimination among the runtimes loaded: not DLSS-G (the
> NVIDIA driver reports no NGX frame-generation feature), no FSR dispatch reached a hooked module, and the
> frame-generation runtime present is libxess_fg.dll; a frame generator compiled into the executable would
> read the same`. Never `XeFg` as a measured identity; never a render extent. Intel's README says XeSS-FG
> runs on non-Intel GPUs with SM 6.4, so DL:TB / Hell Is Us / Cronos / Expedition 33 with XeSS FG on are
> the sessions that measure it, and the pre-commitment is the last clause: the wording must survive the
> one static title (Rune Factory at FSR FG) reading the same way.
>
> **BUILT 2026-09-06 (owner: "build cả hai"), with the executable-file scan beside it.** `FgModePrinted`
> carries the elimination sentence on every rung-3 line; `ExecutableMarkerScan` reads the exe on disk once
> after the session and the clear-census qualifier says MAY when a frame-generation-capable SDK string is in
> the file. Expected on an XeFG session (DL:TB / Hell Is Us / Cronos / Expedition 33 with XeSS FG on):
> the trio or a Presented line from the token or DXGI count, and `frame generation: Active (technology not
> identified) — by elimination …: not DLSS-G (…), no FSR frame-generation dispatch …; the frame-generation
> runtime(s) loaded: libxess_fg.dll …`. Expected on Rune Factory at FSR FG: the same shape with *"no
> frame-generation runtime module is loaded at all; the executable itself carries ffxFsr3, …"*. Neither
> prints a vendor as measured.
>
> **LANDED 2026-09-06, 13:45–14:02 (`spike-notes` §9).** **Rune Factory at FSR + FSR FG** reads exactly the
> pre-committed line: `30.05 → 60.05 (×2 FG)` from the Streamline token, `unseen=0` (the compiled-in FSR 3
> presents through the patched body), `frame generation: Active (technology not identified) — by elimination
> …: not DLSS-G (…); no FSR frame-generation dispatch …; the frame-generation runtime(s) loaded:
> nvngx_dlssg.dll; the executable itself carries ffxFsr3, ffxFrameInterpolation — …` — 7b at its ceiling, the
> Steam overlay showing FG where the report shows the count. **Hell Is Us at XeSS + XeSS-FG** (the owner:
> the Steam overlay could not show FG there): `Presented FPS: 186.47`, **`tokens=0`** — this plugin version
> requests no Streamline token at XeSS, where it did at DLSS — so no count and no factor; `unseen=0`, so
> XeFG presents its generated frames through the patched body and the 186 IS the Displayed rate; the driver's
> words clear; the file carries `xefgSwapChain ×13`. The FG line read the bare *"N/A (a hook ran …)"* and the
> upscaler N/A named "a vendor this build does not decode" — both now carry their witnesses: the N/A prints
> the same exclusion clauses (*the frame-generation runtime(s) loaded: nvngx_dlssg.dll, libxess_fg.dll, …;
> the executable itself carries xefgSwapChain*), and the upscaler line says *libxess.dll is loaded, and XeSS
> cannot be hooked or declared under Intel's licence … N/A here by policy, not by ignorance* (7c item 4's
> string change, done). **Cronos at XeSS** (no FG option): `FPS: 110.02`, `none` by count with DXGI agreeing
> — this plugin requests the token at XeSS. **The ceiling for XeFG on a title that requests no token:**
> Displayed measured, Native unknown, vendor by elimination — there is no in-policy application-frame
> producer for XeFG (the token is the plugin's choice; the XeFG API cannot be hooked; the ffx census is
> silent by construction), and the report says so rather than inventing one.
>

> **BUILT, 2026-09-04, the AMD half — and the deferral's own rule was kept: header first, decode
> second, never from observation.** `HANDOFF` item 7c. The FidelityFX headers cleared the
> checklist and were vendored at tag **`v2.3.0`** (the tree is MIT by exception, 845 of 845 files;
> `18_GPU_VENDOR_APIS` §AMD carries why 2.3.0 and not 1.1.4), and the Overlay now hooks `ffxDispatch`
> on the three **leaves** — `amd_fidelityfx_dx12.dll`, `amd_fidelityfx_upscaler_dx12.dll`,
> `amd_fidelityfx_framegeneration_dx12.dll` — and never on `amd_fidelityfx_loader_dx12.dll`, which
> forwards to the effect DLLs through their own exports (all four modules export only the five
> ffx-api names, so that is the only route there is) and would double every count. What it reads
> is the descriptor's head type and, once matched, `renderSize` and `frameID`; what it publishes is
> identity (`FSR3` from the 1.1.x monolith, `FSR_UNVERSIONED` from the 2.x upscaler DLL that hosts
> FSR 3.1 and FSR 4 behind one type), `renderW/H`, `fgMode = FSR_FG` on a present that drained a
> generated batch, and `fgEvaluations` from PREPARE's `frameID` counted on a new index only. The
> K = 1 / K = 4 fixtures run on both vendor shapes with a forwarding loader decoy in the chain.
>
> **The owner's run, pre-committed before it happens** (one launch per leg; read the
> `ffx dispatch census` line and the trio):
>
> | Row | Leg / reading | Verdict |
> |---|---|---|
> | **A1** | Lies of P, FSR + FG: `upscaler: Fsr3`, `frame generation: FsrFg`, `presents/frame ≈ 2.0`, `frames/upscale-drained ≈ 1.00`, `fg-dispatch/frame ≈ 1.0` | accept |
> | **A2** | Lies of P, FSR, FG off: `presents/frame ≤ 1.05` → `none`, no `FsrFg` record, frames ≈ upscale-drained | accept |
> | **A3** | Hell Is Us, FSR (+ FG if offered): `FSR (3.1 or 4 …)` printed; if Streamline still issues tokens, `fgEvaluations` is tokens and `frames/upscale-drained ≈ 1.00` corroborates across vendors; if tokens are 0 the latch hands the count to AMD — record which | accept, record |
> | **R1** | A title that never prepares (count from UPSCALE dispatches): FG on still reads K, off reads 1.00 | accept, note here |
> | **R2** | `presents/frame ≥ 1.5` with zero `FsrFg` records: the title's `frameGenerationCallback` bypasses the export → `Active (technology not identified)` | honest; follow-up, no code change |
> | **R3** | `presents/frame ≈ 0.5·K` or below 1: a second hook in the chain — check `hooks installed`, the census, whether a loader row crept in | defect, block |
> | **R4** | `frames/upscale-drained ≈ 2.00` with FG off: two upscale contexts per frame, or a doubled count on a title that never prepares | investigate before publishing |
> | **R5** | UE5 with both vendors loaded and tokens ≠ prepares per frame: Streamline wins by the latch; the census line prints the disagreement | record; revisit precedence |
>

> **RUN, 2026-09-04 evening — thirteen captures across nine titles, one launch each (`E:\frameledger-runs\2026-09-04\`).
> Rows A1–A3 accepted; one row the table did not have was found, and it reversed a design decision the same day.**
>
> | Title / setting | Shape | `upscaler` | `frame generation` | Trio / FPS | `frames/upscale-drained` |
> |---|---|---|---|---|---|
> | **Lies of P**, FSR + FG (A1) | monolith | `Fsr3`, render 854×480 | `FsrFg` | `118.98 → 238.02 (×2 FG)` | **1.00**, fg-dispatch/frame 1.00 |
> | **Lies of P**, FSR, FG off (A2) | monolith | `Fsr3` | **`none`** by count (3506 frames / 3504 presents) | `FPS: 59.56` | 1.00 |
> | **Hell Is Us**, FSR + FG (A3) | UE5, two leaves, no loader | `FSR (3.1 or 4 …)`, 1707×961 | `FsrFg` | `85.7 → 171.35 (×2)` | 1.00, fg-dispatch/frame 1.00 |
> | **Hell Is Us**, FSR, FG off | same | same | `none` | `FPS: 104.18` | 1.00 |
> | **Expedition 33**, FSR upscale + **DLSS frame generation ×3** | UE5, two leaves | `FSR (3.1 or 4 …)`, 1707×961 | `Active (technology not identified)` — no FRAMEGENERATION dispatch, no Streamline token; the count came from the UPSCALE dispatches | `67.51 → 200.66 (×2.97 FG)` | 1.00 (fg-dispatch/frame 0.00) |
> | **Expedition 33**, FSR, FG off | same | same | `none` | `FPS: 85.12` | 1.00 |
> | **Cyberpunk 2077**, FSR 3 + FG | FSR 3.0 host DLLs for upscaling, the **1.1.x monolith for frame generation** | `N/A` (upscale is through the FSR 3.0 host DLLs, unhooked on this build; **the host row landed 2026-09-05 — rows B1–B4 below**) — render 1506×847 from PREPARE | `FsrFg` (4261 generated batches) | `72.93 → 145.76 (×2)` | — (no UPSCALE dispatch; count = Streamline tokens 4266) |
> | **Dying Light: The Beast**, FSR + **FSR FG** | loader + two leaves | `N/A` | `N/A` — **zero dispatches at any leaf export** | `Presented 146.54`, WARNING | census: no FSR identity, no FSR_FG |
> | **KCD2**, FSR (×2 runs) | loader + upscaler leaf | `N/A` | `N/A` — zero leaf dispatches | `Presented 58.8`, cannot-include shape (no FG runtime) | — |
> | **Black Myth: Wukong**, FSR | monolith + loader 2.1.0 | `N/A` | `none` (Streamline tokens = presents) — zero leaf dispatches | `FPS: 41.11` | — |
> | **Rune Factory**, **FSR FG** (×2 runs) | **statically linked**: `amd_fidelityfx_dx12.dll` is on disk and NOT in the census | `N/A` | `Active (technology not identified)`, count from Streamline tokens | `59.99 → 119.95 (×2 FG)` | — |
>
> **What it settles.** A1–A3 land exactly as pre-committed, on both vendor shapes the leaf rows
> reach (the 1.1.x monolith; the UE5 pair with no loader), with the two per-frame counts agreeing to
> the frame every time and `none` reached by count on every FG-off leg. **The Expedition 33 FG leg is
> the mixed-vendor case the table did not name:** FSR upscaling with NVIDIA's ×3 frame generation on
> top — no FRAMEGENERATION dispatch, no Streamline token (UE5 drives DLSS-G through NGX), so the
> UPSCALE count carried the application frames and the report said *active, technology not
> identified* at ×2.97 against the title's own ×3. Honest and correct. **Cyberpunk at FSR 3 splits
> across two AMD generations:** the FSR 3.0 host DLLs upscale (the deferred route, so `upscaler: N/A`)
> while frame generation goes through the 1.1.x monolith, which the row sees — `FsrFg` at ×2.
>
> **What it reversed — row R6, which the table lacked: the SDK 2.x LOADER is the game's entry, and
> the leaves stay silent behind it.** Dying Light: The Beast (FSR + FSR FG, which neither Steam's
> counter nor DLSS Swapper could see either), KCD2 (FSR) and Wukong (FSR) produced **zero**
> dispatches at any leaf export while the leaves sat in the census. The morning's argument — five
> identical export tables, so the loader can only reach a leaf through the leaf's export — was
> wrong about the loader: the MIT source shows an *external provider object* and the signed loader
> evidently calls it, not `ffxDispatch`. **So the loader is a row** (the PR after this run), the
> stub forwards through the leaves' direct entry to model what was measured, and the K = 1 control
> with all four modules hooked is what would show a loader that re-entered an export (2.0). Owed:
> the three loader titles again, against **R6-accept** = identity + count as the leaf rows give,
> `frames/upscale-drained ≈ 1.00`; **R6-double** = `presents/frame ≈ 0.5·K` → the loader re-enters
> an export on that version and the leaf rows must yield to it.
>
> **And 7b's title exists after all: Rune Factory: Guardians of Azuma links FSR 3 frame generation
> statically.** The 1.1.x monolith ships beside the executable and is never loaded — the census has
> no AMD module at all — yet FSR FG is on and the Streamline token count still measured ×2.00
> against the title's own setting, so the report printed the trio with *technology not identified*.
> That is better than the ceiling §Scope decisions predicted (Presented FPS + qualifier): on a UE5
> title Streamline's token is there even when the upscaler and FG are AMD's, so the application-frame
> count survives static linking. The identity does not, and cannot.
>
> **ROW R6 RUN, 2026-09-04 night, on #111 (one launch each):**
>
> | Title / setting | `upscaler` | `frame generation` | Trio | `frames/upscale-drained` | Verdict |
> |---|---|---|---|---|---|
> | **Dying Light: The Beast**, FSR + FSR FG | `FSR (3.1 or 4 …)`, 1505×847 | `FsrFg` | `70.49 → 140.9 (×2 FG)` | **1.01** (fg-dispatch/frame 0.99) | **R6-accept** — the loader row carries the title the leaf rows could not, at 1×, no double count |
> | **KCD2**, FSR | `FSR (3.1 or 4 …)`, 1505×847 | `none` by count (3523 / 3523) | `FPS: 59.97` | **1.00** | **R6-accept** |
> | **Black Myth: Wukong**, FSR (×2 runs; **the title has no FSR frame-generation option**) | `N/A` | `none` (Streamline tokens = presents) | `FPS: 39.15 / 90.64` | — (no dispatch at the monolith OR the loader) | **not R6 at all** — Rune Factory's shape, see below |
>
> Two of the three loader titles are closed on the first rerun, and the K = 1 control the fixtures
> run is confirmed by a real title: with all four modules hooked, DL:TB's loader route counts
> once. **Wukong is a different shape**: `amd_fidelityfx_dx12.dll` is *loaded* (it is in the census)
> and *never dispatched to*, with the loader silent too, while FSR upscaling is on and Streamline's
> tokens equal the presents. That is Rune Factory's shape with one difference — the monolith is in
> the process — and the reading that fits every number is **FSR upscaling compiled into the UE
> plugin (static, as Rune Factory's) with the 1.1.x monolith loaded for FSR frame generation and
> idle while FG is off.** ~~The leg that tests it is Wukong with FSR frame generation ON~~ —
> **the owner reports the title offers no FSR frame generation at all**, so the monolith is loaded
> for nothing this hook can see (a plugin that ships it and never dispatches to it), and **Wukong
> joins Rune Factory in §Scope decisions' static row**: FSR upscaling compiled into the UE plugin,
> identity out of reach by name, the application-frame count carried by Streamline's token. Nothing
> is owed on it.
>
> ~~**Still deferred, with the costing written:** the FSR 3.0 host route — `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale`
> / `ffxFsr3DispatchFrameGeneration`, named exports, no struct decode for identity — needs ten
> host headers from tag `v1.1.4` (`main` dropped `ffx_fsr3.h`), whose closure reaches
> `<mutex>`/`<shared_mutex>` through `ffx_types.h`, for exactly one installed title (Cyberpunk 2077,
> whose default here is DLSS). Its own PR, with Cyberpunk switched to FSR 3 as the oracle.~~
>
> ### THE FSR 3.0 HOST ROUTE IS BUILT, 2026-09-05 — and the costing above named the wrong tag
>
> **`fsr3-v3.0.4`, not `v1.1.4`.** Read off the upstream tags: Cyberpunk's `ffx_fsr3_x64.dll`
> exports twelve `ffxFsr3*` names, which is exactly `fsr3-v3.0.4`'s declaration list; `v1.1.4`
> declares fifteen (adds `ContextGetGpuMemoryUsage`, `ContextDispatchFrameGenerationPrepare`,
> `GetEffectVersion`) and its `FfxFsr3DispatchUpscaleDescription` appends `upscaleSize`, `flags`
> and `frameID` **after `renderSize`** — a detour typed against it would read the wrong bytes off
> the module the title ships. The prefix through `renderSize` is identical across 3.0.3 / 3.0.4 /
> 1.1.4 and is the only field read; `dllmain.cpp` pins its offset to a literal. And **`<mutex>` is
> not in the closure at all** at this tag — `ffx_types.h` includes `<stdint.h>` only; the mutex block
> exists only from 1.1.x. Ten files, no `ffx_message.h`, vendored at
> `src/native/third_party/fidelityfx-fsr3/` (root `LICENSE.txt` = MIT verbatim, inline grants;
> `license-check.ps1` §2e).
>
> **One row, the same family.** `ffx_fsr3_x64.dll!ffxFsr3ContextDispatchUpscale` feeds the ffx-api
> drain word: identity `FSR3` as a fact (the 3.0 host hosts nothing else), `renderSize` as the
> extent, and the UPSCALE count when neither Streamline nor a PREPARE has ever spoken. Published by
> `PublishFfxFamilyIfWhole`, which now waits for a loaded host as it waits for a loaded leaf.
> `ffxFsr3DispatchFrameGeneration` is **not** a row: on the one title the monolith generates
> (4261 batches) and the count is Streamline's, and the host's FG export fires per *generated*
> batch. **Reversal, pre-committed:** it becomes a row only on a title reading
> `Active (technology not identified)` with `FL_CENSUS_FFX_FSR3` set and no monolith in the census.
> Fixtures: the `ffx_fsr3_x64.dll` stub (Pass C's second positive control), harness topologies
> `fsr3host` and `fsr3host+mono`, and a `[ffx]` injected case at K = 1 (the double-count control
> for the row) and K ∈ {1, 4} beside the monolith.
>
> **Acceptance, pre-committed — Cyberpunk 2077 (runbook title 3), one launch per leg:**
>
> | Row | Leg / reading | Verdict |
> |---|---|---|
> | **B1** | FSR 3 + FSR FG: `upscaler: Fsr3`; `render 1506x847 -> 2560x1440` with **one** distinct extent (the host's `renderSize` and the monolith's PREPARE agree); `frame generation: FsrFg`; `≈73 → ≈146 (×2 FG)` from tokens; `frames/upscale-drained = 1.00`, `fg-dispatch/frame = 1.00`; `module: ffx_fsr3_x64.dll (no version resource)` | accept |
> | **B1-off** | FSR 3, FG off: `Fsr3`, `none` by count, `frames/upscale-drained = 1.00` | accept |
>
> **B1 and B1-off LANDED, 2026-09-05 (owner's run on #119, `cyberpunk2077-105954` and `-135728`):**
> FSR 3 + FG → `upscaler: Fsr3`, `render 1506x847 upscaler=Fsr3 on 4332 record(s)` (one tuple),
> `frame generation: FsrFg`, `74.31 → 148.56 (×2 FG)`, `frames/upscale-drained = 1.01`,
> `fg-dispatch/frame = 1.00`, `module: ffx_fsr3_x64.dll (no version resource)`; FSR 3 with FG off →
> `Fsr3`, `none` by count (4919 = 4919), `frames/upscale-drained = 1.00`,
> `render -> output: 1506x847 -> 2560x1440 = 1.7x (59% render scale)`. B2–B4 did not fire. **One
> defect the run exposed, in the consumer:** the FG-on leg printed `render -> output: N/A` two lines
> under its own raw block — `UpscaleExtent` keyed its window on the params bit, which under frame
> generation rides on one present in N, so `ClaimedSuffixStart`'s clean-suffix rule found no
> window; it now keys on the identity bit (per-record once live) and counts every record inside
> that carries both sizes. Every FG-on capture since 2026-08-16 had been reading `N/A` there; the
> earlier "not computed yet" wording had hidden it.
> | **B2** | `upscale-drained = 0` with `ffx_fsr3_x64.dll` in the census and FSR on — the game calls `ffx_fsr3upscaler_x64.dll` directly and the facade is silent | add the upscaler-DLL row (`ffxFsr3UpscalerContextDispatch`, ten `FfxResource`s, its own offset pin) **instead** — never both |
> | **B3** | `frames/upscale-drained ≈ 2.00` | two upscale contexts per frame or a re-entered dispatch — investigate before publishing (mirrors R4) |
> | **B4** | `SETTINGS MOVED: 2 distinct extents` | the host and the PREPARE disagree on the input size; print both, do not average |
>
> *The banner as it stood on the morning of 2026-09-04:* **The deferral has a next step, and it is `HANDOFF` item 7c.** The Streamline
> work built every tool this needs — module-scoped inventory rows, types-only vendoring with a
> consumer in the same commit, `hookinventory-check` Pass A–D, the K = 1 / K = 4 harness pair,
> and a per-frame count validated against a title's own menu — and Lies of P (FSR 3.1 FG on,
> `amd_fidelityfx_dx12.dll` alone) is the title that shows what this deferral costs today:
> `upscaler: N/A`, `frame generation: N/A`, a WARNING, no factor. The first move is the one this
> entry names and nobody had made: **run the licence checklist on the FidelityFX and XeSS SDK
> headers.** ~~Both are published under MIT~~ — **run 2026-09-04, and the sentence struck was
> wrong about Intel.** FidelityFX at tag `v1.1.4` is MIT root-and-inline and is clear to vendor
> types-only (as is `fsr3-v3.0.4`, the tag the host route actually vendored on 2026-09-05); the XeSS SDK is the Intel Simplified Software License (binary-only grant,
> no-reverse-engineering, termination) and its headers say they may not be copied without
> Intel's permission, so step 3 binds and XeSS stays where NGX is. `18_GPU_VENDOR_APIS` §Vendor
> SDKs carries both decisions with the evidence. **So 7c is AMD only.** The one thing that
> looked left for Intel — a signature-free call counter on a named export — was refused the
> same day (§Scope decisions, the Intel row): it still patches Intel's code in the game process.

HANDOFF item 3 asks for this to be *deferred with a written rationale rather than
guessed*, and this is that rationale. The deferral is the decision; it is not a
placeholder.

**What is measured** (`docs/vendor-exports.json`, dev machine, 2026-08-05):

| Module | Exports that would identify frame generation |
|---|---|
| `libxess_fg.dll` | `xefgSwapChainD3D12InitFromSwapChain`, `…GetSwapChainPtr`, `…SetEnabled` — 28 of its 31 exports are `xefgSwapChain*` |
| `ffx_fsr3_x64.dll` | `ffxFsr3SkipPresent` |
| **`amd_fidelityfx_framegeneration_dx12.dll` (3.1.5, in three installed titles)** | **five generic `ffx*` entry points and nothing else** |

**Why the third row is the blocker.** On the newer AMD module the symbol name carries
no identity at all: which *feature* a call configures is decided by a **field inside a
struct passed as an argument**, and we have no headers for that struct. Reading it
would mean hand-declaring a vendor ABI from observation — which is exactly what
`#71` proved fatal in the Streamline case, where a module kept the symbol NAME and
changed the SIGNATURE and a wrong argument list yields *a wrong answer rather than a
crash*, the failure class `17_HOOK_ENGINE` calls the highest false-confidence risk in
the spike. A guessed struct layout is the same defect with a different vendor's name
on it.

**Whether the headers may be vendored is not the obstacle** — `18_GPU_VENDOR_APIS`
§Checklist has to rule on FidelityFX and XeSS-FG licences before either could be
consumed, and nobody has run that checklist for them. So there are two gates here,
and the licence one has not even been opened.

**What the deferral costs, stated so it is not rediscovered as a surprise.** An XeFG
or FSR3-FG title reports `fgMode = FL_FG_UNKNOWN` and `FL_MEASURED_FG` set — *"a hook
ran and could not identify what it saw"*, which is the honest state and is already
what `dllmain.cpp` publishes. It does **not** report `NONE`, so nothing is aggregated
as a negative. The cost is coverage, not correctness.

**What would lift it**, in order: run `18_GPU_VENDOR_APIS` §Checklist on the FidelityFX
and XeSS-FG SDK licences; if either clears, vendor the header **with its consumer in
the same commit** (the rule §2b's `sl_dlss.h` landed under); and only then decode the
struct — never before, and never from observation.

**Related and NOT the same question.** Both vendors also take over the present
(§H5 case 3, and item 3's own entry), which is a separate hazard: identity is what
this item defers, and whether generated presents reach our vtable is what §H5 tracks.

### H10 · Per-process VRAM thread inside the game process

🅓 **The usage half is answered from outside the game — 2026-10-03 (beta.12, owner decision D43); the budget half is
deferred with §M11.** The game's dedicated and shared GPU memory are read by the Agent from the `GPU Process Memory`
counters (`18_GPU_VENDOR_APIS` §The game process's memory), so no thread and no DXGI call is added inside the game for
them. What only an in-process call could give is the budget, and nothing reads it. *(The question as it stood:)*

`17_HOOK_ENGINE` §Memory calls `QueryVideoMemoryInfo` "once per second from our
own thread". Spawning a thread inside a host process we do not own is a heavier
footprint than the rest of the design's posture, and DXGI calls off the render
thread need checking. Consider sampling on the present hook every N frames
instead, which needs no thread at all.

---

### Closed H-series entries — in `open-questions/closed-h.md` (2026-10-19, D66)

Moved verbatim, in this order.

| Entry | In |
|---|---|
| H5 ✅ · Proxy swapchains defeat the dummy-vtable assumption — the DLSS / DLSS-G question closed 2026-09-06 (… | [`open-questions/closed-h.md`](open-questions/closed-h.md) |
| H6 ✅ · D3D12 command-list hooks count recorded, not executed, work — answered 2026-08-20 with the RT hooks… | [`open-questions/closed-h.md`](open-questions/closed-h.md) |
| H7 ✅ · Vtable restore on unhook clobbers later hookers — closed | [`open-questions/closed-h.md`](open-questions/closed-h.md) |
| H8 ✅ · "Never crash the game" — closed | [`open-questions/closed-h.md`](open-questions/closed-h.md) |

## M — Metrics and telemetry. Blocks P2.

> **M3 and M4 are resolved — both licence questions came back clean.** They were
> the two items that needed no hardware and could each have invalidated an
> entire telemetry layer, so they were answered first. Evidence and the exact
> reasoning are in `spike-notes.md` §0; the licence texts now ship in
> `legal/licenses/`. The remaining M-items still need measurement.


| # | Question |
|---|---|
| M1 ✅ | ~~Can PresentMon 2.x `FrameType` see **driver-level** frame generation (AMD AFMF)?~~ | **Closed by DECISION 2026-08-27, not by measurement, and the distinction matters.** PresentMon is dropped, so this question has no subject. What was learned before it went: `FrameType` reports events a vendor chose to emit, and NVIDIA's DLSS-G emits none (§S31 row P2) — so the "capability inversion" this row hoped for was never demonstrated for any vendor. If a Tier-2 mechanism is ever chosen (§G), ask this of **that** tool rather than reviving this row |
| M2 ✅ | ~~Does the pinned console binary still exist, run unelevated, and emit the 2.x column set?~~ | **Answered, then closed 2026-08-27.** It exists (2.5.1, 956,768 bytes, SHA256 `9BEC…A191`, **no VERSIONINFO at all**); it does **not** run unelevated (exit 6 — the account is in neither Administrators nor Performance Log Users, and the running `PresentMonSharedService` does not help because the console starts its own session); and the 2.x column set **is measured** — 24 columns, `spike-notes` §11. The parser ate three real CSVs and resolved `FrameType` correctly against a first column literally named `Application`. Closed because the binary is dropped, and kept in full because every one of those facts cost a measurement |
| M5 ✅ | **Do LHM GPU sensors work unelevated, without PawnIO?** **YES — measured 2026-09-03, row R1 of the pre-committed table below**, NVIDIA RTX 5080 / 616.56 / LHM 0.9.6: eight fields unelevated (core and memory temperature, load, VRAM used, core and memory clock, package power, fan), the identical eight in the elevated control, PawnIO never opened. The three user-facing sentences stand. Numbers in `spike-notes` §10. *(The row as it stood while open follows, unchanged.)* This decides whether the default unelevated Agent has temperatures at all, and therefore how ADR-9 reads to users. **SEVERITY PROMOTED 2026-08-28, and the promotion is the point.** It used to decide how a design note read. It now decides whether a **user-facing claim is true**: the two-rung ladder says a Tier-2 session records duration and hardware telemetry, and that sentence is heading for `README`, the consent dialog and `legal/EULA.md`. If LHM needs elevation for GPU sensors, then the DEFAULT Agent's Tier-2 session is **duration only** and every one of those documents is wrong. Until it is measured, all three are worded *"whatever hardware telemetry this machine can provide"* — honest under either outcome, at the cost of vagueness. `18_GPU_VENDOR_APIS` §P0(b) already specifies the test and the dev box can run it |
| M6 ✅ | ~~Elevation-free Tier 2~~ | **Closed by decision 2026-08-27.** Half was answered on 2026-08-20 in the direction nobody expected: `PresentMonSharedService` is installed and `Running` as LocalSystem and the console **still** fails, because it starts its own trace session. The other half — whether `Performance Log Users` suffices without admin — stays unmeasured and now has no subject. **The elevation question does not disappear with the tool**: ETW trace sessions are restricted by Windows, not by PresentMon, so whatever §G chooses inherits it. Re-ask it there |
| M7 ✅ | ~~`18_GPU_VENDOR_APIS` §Runtime policy says telemetry is never read from the game process, but `17_HOOK_ENGINE` reads per-process VRAM and Reflex latency there. Reconcile the wording — the rule means "no vendor SDK polling loops in the game", not "no measurement in the game"~~ **Closed 2026-09-09 (PR-E1) by the code taking exactly that shape.** The rule is *no telemetry polling loop in the game*: the one 1 Hz loop is `TelemetryPoller` on the Agent's `fl-telemetry` thread, and it reads L1/L2/L3 only. Per-process VRAM stays an in-process read on the present path (`vram_proc`, `17_HOOK_ENGINE`), and PR-E1 measured why that must be so: `IDXGIAdapter3::QueryVideoMemoryInfo`'s `CurrentUsage` is the *calling process's* figure — 0 bytes from the Agent — so it is a measurement only where the game is. **Corrected 2026-10-03 (beta.12, D43): "only where the game is" holds for `QueryVideoMemoryInfo` and not in general** — the `GPU Process Memory` performance counters give any process's dedicated and shared GPU memory from outside it, with no handle, and the Agent reads them since that day (`18_GPU_VENDOR_APIS` §The game process's memory; `spike-notes` §16). The in-process read was never built. Adapter-wide usage is L1's PDH counter. §Runtime policy's sentence stands as written; this row is the reconciliation |
| M8 ✅ | ~~The `GpuSample` type has no latency field, yet L3 is credited with Reflex/PC latency. Latency is per-frame and arrives via the ring, not the 1 Hz sample. Fix the layering description~~ **Closed 2026-09-09 (PR-E1): the description now matches the type.** `GpuSample` has no latency field by design (its own remarks said so since 2026-09-03), `TelemetrySample` carries a 1 Hz reading stamped with QPC, and per-frame latency is `FlFrameRecord.reflexLatencyUs` in the ring. The §L3 table row "Reflex / PC latency" is an *in-process hook fact* the Overlay records (`SetSleepMode` / `SetLatencyMarker`, `17_HOOK_ENGINE`), never a poller field; `NvapiTelemetrySource` (PR-E2) will not call `NvAPI_D3D_GetLatency` |
| M9 ✅ | **Closed 2026-08-02 by a decision.** The old file/module detection does not exist in this repository and the owner confirmed there is no copy elsewhere, so **"build a minimal static-hint detector as the measurement baseline" is now P0 item 3** (`15_ROADMAP`). It needs no guard and no injection. Until it exists, ADR-7's headline claim stays out of the README rather than being asserted unmeasured |
| M10 ❓ | **§CPU — is the CPU temperature this build reads the right sensor, and does it read at all on the owner's machine?** `LhmCpuTemperatureReader` (2026-09-21) opens a CPU-only LibreHardwareMonitor `Computer` when the Agent is elevated AND `PawnIo.IsInstalled`, takes the package sensor (`CPU Package` / `Core (Tctl/Tdie)`) and falls back to the hottest core. It was written against LHM's sensor names and a fake `ILhmComputer`; **no elevated run with PawnIO has been taken.** | **The measurement is one session:** Agent elevated, PawnIO installed, a five-minute capture with HWiNFO or Ryzen Master open beside it. Pre-committed: `max_cpu_temp` within 3 °C of the reference's package peak → keep; a core sensor picked where a package sensor exists, or a reading tens of degrees off (a distance-to-TjMax sensor mistaken for a temperature) → fix `Pick` and re-run; nothing read at all → `cpu_temp` stays N/A and `legal/ACCURACY.md` says processor temperature is not measured. CPU **load** and memory are not in question: `TheRealCountersAnswerOnThisMachine` reads them on every gate run. |
| M11 🅓 | **The game's VRAM budget and `budget_exceeded_pct` (FR-4.5's second half), 2026-10-03.** D43 reads the game's usage from outside (the `GPU Process Memory` counters); the budget the OS grants the process is what DXGI's `QueryVideoMemoryInfo` returns to the *calling* process (§M7), so only an in-process call — the Overlay's background thread, which today only checks for a stop — or `D3DKMTQueryStatistics`' `PROCESS_SEGMENT_GROUP` query (documented "Reserved for system use") could read it. **Deferred by decision:** neither is built; `vram_budget_exceeded_pct` stays NULL and the budget N/A, and the in-process route would change what the Disclaimer says the Overlay's thread does. Reopening it needs that decision, not a measurement. | — |
| M10 ✅ | ~~PDH `\GPU Engine(*)\Utilization Percentage` summed across engines does not reproduce the Task Manager figure the doc invokes. Decide what we actually report and label it accordingly~~ **Decided 2026-09-09 (PR-E1): L1 does not read the engine counters at all.** `LoadPct` is L2's vendor-reported `GPU Core` load (measured ✓ on NVIDIA, §M5 row R1) and is labelled *GPU load (vendor)* wherever it is shown; on a machine without L2 the field is N/A, never a sum that is not the number the user expects. What L1 does read from PDH is one counter, `\GPU Adapter Memory(luid_…_phys_0)\Dedicated Usage`, bound by LUID — the figure Task Manager labels "Dedicated GPU memory", measured 1301 MB at an idle desktop on the RTX 5080. Reopening this needs a measurement of `max(engine)` against Task Manager's own number, not a decision |

#### §M5 — the decision table, pre-committed 2026-09-03 BEFORE the first run

The §S30 / §S31 discipline: write down what each outcome means before the instrument
produces one, so the result cannot be read into the conclusion that is most convenient.
The instrument is `FrameLedger.CaptureHost probe-lhm`, run **unelevated first**, then once
elevated as a control — the control exists to separate *"needs elevation"* from *"this
driver / this LHM version has no such sensor on an RTX 5080"*, which read identically from
the unelevated run alone. The five fields the row is decided on are the ones `README`'s
Tier-2 sentence would name if it named any: **core temperature, load, power, adapter VRAM
used, core clock.**

| Row | Unelevated, no PawnIO, `Computer { IsGpuEnabled }` only | Consequence |
|---|---|---|
| **R1** | All five fields non-null | The three documents stand and may name the fields instead of saying *"whatever"*. `18_GPU_VENDOR_APIS` L2 row → ✓. §M5 ✅ |
| **R2** | A non-empty subset non-null | The three documents name exactly that subset. If the consent sentence changes, `OperatorDisclosure.Version` /2 → /3. §M5 ✅ with the subset recorded |
| **R3** | LHM enumerates a GPU and every sensor reads null | The default unelevated Agent's Tier-2 session is **duration + reason only**. `README:67`, `legal/EULA.md:15` and `OperatorDisclosure.cs` narrow accordingly, /3. §M5 🔴 until those edits land in the same PR |
| **R4** | `Open()` / `Accept()` throws, or a poll never returns | As R3, and the *"throws or hangs twice ⇒ disabled for the session"* rule (`18_GPU_VENDOR_APIS` §Runtime policy) is exercised by a real fault rather than a fixture. Record separately whether the failure names PawnIO, because that would mean the GPU path is not user-mode after all |

Two things this table does NOT decide, stated so the run is not read as deciding them:
which of the fields AMD or Intel expose (§R5/§R6, untestable here), and whether the CPU
and board sensors need PawnIO (they do, by LHM's own design — `--cpu` is deliberately not
an option of the probe, so the run cannot accidentally measure that instead).

**RESULT, 2026-09-03 — R1, both runs.** Unelevated: all five deciding fields plus memory
temperature, memory clock and fan; through the port, `Capabilities` reads the same eight
and `faults=0`. Elevated control, launched through UAC: the same eight fields, the same
verdict, values within idle drift. Hotspot temperature is the one field this vendor did
**not** expose — there is no `GPU Hot Spot` sensor in the tree at all, which is a property
of this driver / GPU / library combination and not of elevation. One defect the raw tree
caught before the verdict: the first draft of `SensorMap` mapped `D3D Shared Memory Used`
(system RAM) to adapter VRAM by a fragment rule; fixed and pinned by a test in the same
PR, which is what printing the raw tree ahead of the interpretation is for.
Consequences taken: the three sentences stand (`README`'s row may now name the fields and
does), `OperatorDisclosure.Version` stays /2, `18_GPU_VENDOR_APIS` L2 NVIDIA column filled,
AMD / Intel untouched.

---

## G — Missing specifications. Block P2–P4.

Each of these is referenced by an existing doc but specified nowhere.

| Area | What is missing | Referenced by |
|---|---|---|
| **Agent lifecycle** | Scheduled-task definition, start-at-logon, how elevation is requested and persisted, what "Repair" repairs. **`--serve` exists since 2026-09-10 (P2 PR-F)** — a Generic Host around the watcher, stopped by Ctrl+C with a 15 s finalize grace — and `--install-task` / `--uninstall-task` answer "not implemented in P2"; the task definition and start-at-logon are what stays open *(corrected 2026-10-04: built in P3 PR-8b — `LogonTask` behind both flags, and start-with-Windows as the HKCU Run value)*. **One Agent per data folder since 2026-09-17** (`01_ARCHITECTURE` §Lifecycle: `AgentInstanceLock`, exit 10; beta.1 ran four after an elevated App was refused by the pipe). **Still open from that morning:** (1) the ring's single consumer is enforced across data folders only by `NoSecondRingReaderTests`' source sweep — a developer's `--console --data-dir` capture beside the installed Agent can still attach a second reader to one game's `Local\FrameLedger.Ring.<pid>`, and a runtime claim beside the mapping would close it; (2) ~~elevation is inherited, not chosen — an App started as administrator starts an elevated Agent, which injects and writes the ledger as administrator~~ **closed 2026-09-27 (beta.10, owner decision D34): chosen** — `capture.run_elevated`, a UAC prompt at every Agent start, the logon task's Agent asking through its own prompt, and with the option off an administrator's App starts the Agent with the shell's token (`01_ARCHITECTURE` ADR-9). "How elevation is requested and persisted" is that: requested by the prompt, persisted as one settings row and never as a task's run level | `08_UI` Settings, `11_UPDATER`, `12_BUILD` flags |
| **Session identity** | ~~`sessions` has no GUID column, yet `SessionStarted`/`StopSession`/`.partial` files are all keyed by `sessionGuid`.~~ **`sessions.session_guid` (UNIQUE) exists since 2026-09-09 (P2 PR-B), with `qpc_epoch` / `qpc_frequency` beside it.** ~~Still open: the `.partial` file format is undefined, and it is the crash-recovery artifact — PR-D's, pre-committed in HANDOFF §P2 (CRC-framed append-only chunks, the valid prefix wins)~~ **Built 2026-09-10 (P2 PR-D): `06_DATA_MODEL` §The `.partial` file, `PartialSessionFile` + `PartialRecovery`, killed at every byte in the merge gate.** ~~What stays open here is the pipe's use of the guid (P3)~~ *(built in P3 PR-1/1b: the session events and `StopSession` carry `SessionGuid` — corrected 2026-10-04)* | `07_IPC`, `04_CAPTURE`, `06_DATA_MODEL` |
| ~~**Settings registry**~~ | ~~The `settings` table is key/value with no key list, defaults, types, or validation — and no message for the UI to push a changed setting to the Agent.~~ **Decided 2026-09-13 (HANDOFF §P3 decision D16)**: the key list with defaults and ranges, and *no* push message — the Agent re-reads its keys at each session start. **Built the same day (P3 PR-3):** `06_DATA_MODEL` §Settings registry (12 keys), `Application.Settings.SettingsRegistry` + `RegisteredSettings` (validating writes, tolerant reads), `SettingsRecorderPolicy` resolving `capture.min_session_s` / `telemetry.interval_ms` / `retention.raw_sessions_per_game` at each session start. **Row closed** | `06_DATA_MODEL`, FR-10 |
| **Error taxonomy** | `07_IPC` lists `CaptureError` codes; no canonical mapping to resx keys and user-facing text, though `09_I18N` requires safety strings to be reviewed as legal text | `07_IPC`, `09_I18N` |
| ~~**Threading model**~~ | ~~Which component owns which thread, UI-thread rules, and how the 1 Hz telemetry poller, 10 Hz drain and pipe reader interact~~ **Answered 2026-09-09 (P2 PR-C): `04_CAPTURE` §Threading model** — four threads, one owner per shared object, the pipe reader named as one more producer. This row was not struck when the section was written; struck 2026-09-13. What is still unspecified is the UI thread's own rules, which is `08_UI`'s in P3 | `04_CAPTURE`, `18_GPU_VENDOR_APIS` |
| **Pipe/shm security** | Stated as policy ("DACL granting the current user's SID") but not as implementable SDDL, nor who creates the objects with what rights. The shm half is built (`fl_shm_host.h`, the Overlay creates the section under the current user's SID); the pipe half is **built 2026-09-13 (P3 PR-1, HANDOFF §P3 decision D11)**: `D:P(A;;GA;;;<user>)(A;;GA;;;BA)` (with the owner `O:<user>` stated since 2026-09-17, `07_IPC` §C) handed to `CreateNamedPipe` with `PIPE_REJECT_REMOTE_CLIENTS`, and the client's token user compared by impersonation on its first frame (`Infrastructure.Ipc.PipeAccessControl`, pinned by `PipeServerClientTests` including the refused stranger). **Row closed** | `07_IPC` |
| ~~**Legal doc versioning**~~ | ~~FR-11 re-shows documents "when a document version increments". Nothing says where the current version lives, who writes `legal_acceptance.version`, or how the markdown is rendered in-app~~ **Answered 2026-09-14 (P3 PR-9):** the version is the document's own `**Version:**` line, read from the copy embedded in the App at build (`App.Services.LegalDocuments`); the App writes `legal_acceptance.version` on the one Accept and re-shows any document whose embedded version differs from its row; ~~the markdown is shown as text with authoring comments stripped, not rendered~~ the markdown is shown with authoring comments stripped and, since beta.12, rendered (`08_UI` §Menu, *Documents are rendered*; §First-run flow built note) | FR-11, `06_DATA_MODEL` |
| **Accessibility / DPI** | NFR-9 states requirements; no design anywhere | NFR-9, `08_UI` |
| **Uninstall / data deletion** | One Velopack clause. No in-app "delete all my data", despite the privacy position | `12_BUILD`, `legal/PRIVACY_POLICY.md` |
| ~~**Cover art**~~ | ~~Appears in the schema, the data directory and the UI; nothing says where it comes from or the licence position on downloaded store art~~ **Answered 2026-10-14 (beta.23, owner decisions D60/D61):** from the user's own image (a copy in `covers\`) or what the Steam and GOG GALAXY apps already keep on this PC — nothing is downloaded, so no store art is redistributed or fetched; the art stays its owners' (`legal/TRADEMARKS.md`), the Privacy Policy 2.11 names the files read. `08_UI` §Games › Game detail. **Row closed** | `06_DATA_MODEL`, `05_DETECTION` |
| **The main line's .NET** *(added 2026-10-31, D73–D76)* | **When does the main line — the betas and the first release — leave .NET 10 LTS, and for what?** The alpha line (`12_BUILD` §The alpha line) builds the same source on .NET 11 to gather the evidence; it decides nothing by existing. What is known: .NET 11 is STS, supported 24 months to about 2028-11, no later than .NET 10 LTS (2028-11-14), so moving buys no support time; Microsoft's supported-OS list for .NET 11 has Windows 10 only in its LTSC and IoT editions, against NFR-8's Windows 10 22H2 floor; C# 15's unions and closed hierarchies would suit the tri-states and refusal reasons, but only once the main line compiles C# 15. **Decision table, written before the evidence:** move to .NET 11 only if (a) at least two alphas on the GA runtime pass the gate and an owner's run of a hooked title with no runtime-specific defect, (b) the owner accepts dropping NFR-8's floor to Windows 11 or ships knowingly unsupported on Windows 10, and (c) something .NET 11 brings is wanted — a measured gain or a language feature; otherwise stay on .NET 10 and take .NET 12 LTS (expected 2027-11) the same way, through the alpha line first | `12_BUILD`, `13_CI_CD`, `02_SPEC` NFR-8, CLAUDE.md pinned stack |
| **Tier-2 mechanism** *(was: PresentMon distribution)* | **By what mechanism does a shipped build reach a no-injection MEASUREMENT?** Nothing names one. Since 2026-08-28 the ladder is two rungs and Tier 2 is explicitly not a measurement — duration, sensors and the reason — so this row is no longer about degraded fidelity but about whether that capability returns at all. **It now owns TWO casualties, not one:** the frame-generation oracle §S31 spent four attempts looking for, and `14_TESTING`'s Tier-1 accuracy cross-check, struck the same day because it required a second instrument measuring the same frames. **One mechanism would restore both**, which is the strongest argument for costing it. Constraints on any answer: `README`'s capture-tier table and its **safety** row *"always a way out"*, `legal/DISCLAIMER.md` §73 and `legal/EULA.md` §33; ETW needs elevation whatever the tool; and `15_ROADMAP` has no v2 fallback either | `README`, `legal/DISCLAIMER.md`, `legal/EULA.md`, `04_CAPTURE`, `14_TESTING`, `15_ROADMAP` |

---

## R — Roadmap resequencing

~~1–4 are folded into `15_ROADMAP` (2026-08-02) and are no longer proposals:~~
the guard is **item 0**, the Vulkan passthrough test is **item 1**, the two
licence checks were run first and came back clear (`spike-notes.md` §0), and
P0's exit criteria no longer import P2 work — the FPS-impact measurement moves
to the end of P1, while the harness-level per-present cost (no game, no Agent,
no drain) stays in P0. ~~Items 5–9 below are still open.~~ *(Corrected 2026-10-04: 5 and 6 are deferred, 🅓 below; 7
is moot — `ci.yml` has gated every PR since bootstrap; 8 is history, P1 having closed; 9 stays open — no `ja` string has
been reviewed, an owner-only item.)*
5. **P0 item 5 asks for an AFMF decision on an RTX 5080.** AFMF is AMD driver
   -side. Reword to "validate on NVIDIA; record AFMF as untested", per the
   precedent `14_TESTING` already sets for absent hardware.
6. **`18_GPU_VENDOR_APIS`'s capability matrix cannot be filled for AMD/Intel** on
   the stated dev machine, yet it drives what the UI advertises as available.
   Leave explicit "untested" markers rather than `?`.

   > #### 🅓 §R5 and §R6 deferred 2026-08-05, by owner decision, with this rationale
   >
   > **The AMD/Intel half of the capability matrix and the AFMF question are not
   > deferred for cost — they are unreachable on the only machine that exists.**
   > `spike-notes.md` §Environment records it: one adapter, an RTX 5080, and a `KF`
   > CPU with no iGPU. There is no measurement to take. Deferring is the only
   > honest option; the alternative is a matrix filled from documentation, which is
   > exactly what §M3/§M4 were run first to avoid.
   >
   > **The NVIDIA half is not deferred** and remains P0 item 8: L1 (DXGI + PDH),
   > L2 (LibreHardwareMonitor — including §M5, whether GPU sensors work unelevated
   > without PawnIO, which decides whether the default Agent has temperatures at
   > all) and L3 (NVAPI). None of it exists in code today.
   >
   > **What the deferral costs.** The matrix drives what the UI advertises as
   > available, so on AMD and Intel hardware v1 ships advertising capabilities
   > nobody has verified. The mitigation is the marker, not a guess: **`untested`,
   > never `?`, and never a checkmark inferred from a vendor's documentation.**
   >
   > **And the matrix as written cannot record this deferral**, which is its own
   > defect and is why the note lives here as well as there: its columns are
   > `L1 | L2 | L3` with no vendor axis, so "measured on NVIDIA, untested on
   > AMD/Intel" is not expressible in it. Whoever fills it for item 8 has to add
   > that axis first, or the distinction this deferral rests on is lost in the
   > artifact that consumes it.
7. **CI is scheduled for P5**, but CLAUDE.md makes green C# **and** C++ builds
   plus passing tests a per-PR gate from the first PR. CI belongs in bootstrap.
8. **P1 at 1.5 weeks is the largest under-estimate** — present hooks for three
   API families, feature hooks for four vendor SDKs, RT and PSO hooks, ring
   writer, fault policy, unhook path, native logging, injector launch *and*
   attach modes, plus the fully-tested guard that `19_SAFETY` calls the one
   component where a bug can cost someone an account.
9. **The `ja` safety-string reviewer is on the critical path but appears in P4.**
   `09_I18N` fails the build until a human signs off on safety translations.
   Identify the reviewer during bootstrap and draft the `Safety_*` keys as soon
   as the consent wording is stable.
10. **Four artifacts the documents name do not exist, and one of them is claimed
    as a gate that runs.** Found 2026-08-04 by an audit of the repository's own
    status records, and grouped here rather than fixed one by one because the
    decision is the same for all of them: build it, or stop describing it as
    present.

    | Named where | Artifact | State |
    |---|---|---|
    | `12_BUILD:139`, `13_CI_CD:11`, `09_I18N:28`, `CLAUDE.md:66` | `tools/resx-audit` | ~~Absent. **Honestly handled** — `build.ps1:309` skips it loudly with a reason, and no `.resx` files exist yet~~ **✅ Built 2026-09-13 (P3 PR-2)**, a `build.ps1` step (marked 2026-10-04) |
    | `12_BUILD:136`, `13_CI_CD:11` | the managed **struct-mirror** test | ~~Absent. Nothing under `tests/` references `FlFrameRecord`.~~ **✅ Built in #47** — `ShmLayout.cs` + `ShmLayoutMirrorTests`, driven by `fl-layout-dump`'s JSON rather than a transcribed table, asserting blittability as well as offsets, and walking the field list in both directions. `build.ps1`'s gate now reads the run's `.trx` and fails when the class did not execute, so **deleting the test is red too** — it used to `Test-Path` a source file |
    | `13_CI_CD:21`, `12_BUILD:121`, `CHANGELOG:9` | `.github/workflows/release.yml` | ~~Absent. `CHANGELOG`'s header instructs an author to write for a consumer that does not exist~~ **✅ Built 2026-09-14 (P4 PR-5)**; it published beta.1 through beta.11 (marked 2026-10-04) |
    | `12_BUILD` §Local quality gate | gates omitted from the list | ~~**Three documents give three different counts, which is the finding.** `build.ps1` has **15** `Write-Step` calls as of 2026-08-05 (14 before this PR's `changelog-check`); `12_BUILD` lists 10; this row said 13.~~ **Corrected 2026-08-06: `build.ps1` has 17 `Write-Step` calls** (`package-closure` added two), and `12_BUILD`'s list is rewritten to 16 numbered entries — the difference is that `Invoke-Native`'s three steps run under one heading there. `13_CI_CD` no longer restates the list at all; it points at `12_BUILD`, because restating it in two places is what let one of them rot. **Nothing still derives the list from the script**, so the drift is slowed rather than stopped, and this row remains open for that reason. **And it drifted again, exactly as predicted — re-counted 2026-08-28.** `hookinventory-check` had reached `build.ps1` and never reached the list; `frametype-oracle` had reached both and then been deleted with PresentMon, so the two errors happened to cancel and the *totals* matched while the *membership* did not — the same coincidence §S24's own recount was caught by. The list is rebuilt to **17 entries against 17 `Write-Step` calls, in script order** (re-deriving the order caught a second defect: two adjacent entries had been transposed relative to the script). **The row still does not close.** Aligning by hand is what was done in 2026-08-06 and again here; it lasts until the next gate lands. The fix is a gate that reads the `Write-Step` names out of `build.ps1` and fails when the list disagrees — until it exists, re-count rather than trusting the number |

    The struct-mirror row is the one that matters. `CLAUDE.md` §Struct mirroring
    makes that test the mechanism protecting the shared-memory ABI, and a doc that
    says a gate runs is worse than a doc that says it is missing — the second
    prompts someone to write it. **`build.ps1`'s skip-loudly discipline is the
    right answer here**: a gate that is not written should be named and skipped,
    not omitted.

    ~~Not urgent — the ring is P1 and there is nothing to mirror yet.~~ **That
    stopped being true on 2026-08-05.** `fl_shm.h` defines four structs with
    39 pinned offsets, `fl_ring.h` implements the protocol, and the Overlay
    writes real records into a real mapping — so there is now a live ABI with
    **nothing binding it to a managed reader**, which is the state the gate
    exists to prevent rather than the state that made it unnecessary. The count
    also grew: `build.ps1:324-329` enumerates **nine** files describing the
    mechanism in the present tense, and the two strongest sites are `fl_shm.h`
    (normative) and `fl-layout-dump/CMakeLists.txt` (a build file, which reads as
    a wiring fact rather than as prose). All nine were marked pending on
    2026-08-05; §S24 schedules the mirror itself.

    Recorded so it is found by reading, not by trusting.
