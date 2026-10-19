*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 13 · Can the guard go RED against real anti-cheat? *(2026-08-04)*

Every real-machine verdict recorded before this section was `Allow` or an
*uncertainty* refusal. The only blocklist positive ever seen — `EasyAntiCheat_EOS`
as an installed-but-stopped service — was classified as a defect and deliberately
narrowed away. So nothing had ever shown that the module, driver, service or
directory tiers fire against a running commercial anti-cheat, and a gate that has
never been observed to refuse carries the same amount of information as one that
never refuses.

Measured against four real titles, unelevated. **`FlGuardEvaluate` and
`FlStaticPreScan` only — nothing was injected anywhere.**

### ✅ It goes red — through the SERVICE tier, and machine-wide

With Goose Goose Duck (Easy Anti-Cheat, Unity) actually running:

| Target | Verdict |
|---|---|
| `Goose Goose Duck` (pid) | `BlockedService`, family `Easy Anti-Cheat`, signal `EasyAntiCheat_EOS` |
| `GGDLauncher` | same |
| `EasyAntiCheat_EOS` | same |
| **a freshly spawned `hook-harness`, completely unrelated** | **same** |

State at the moment of measurement: service `EasyAntiCheat_EOS` **Running**,
driver `EasyAntiCheat_EOSSys` **Running**.

**The refusal is machine-wide, and that is worth stating plainly rather than
discovering.** Checks 2 and 2b do not depend on the target, so while any EAC
title is running FrameLedger refuses to inject into *anything* — a user with a
party game idling in the background cannot capture an unrelated single-player
title, and the signal names a game they are not playing. That is the correct
fail-closed posture (a live anti-cheat driver is a machine-wide hazard, which is
exactly what §S1's driver check exists for), but the user-facing text has to
explain it or it reads as a bug.

### 🔴 Check 1 structurally cannot see an EAC-protected game

Measured with the guard's own rights and flags
(`PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ`, `LIST_MODULES_ALL`):

| Process | Result |
|---|---|
| `Goose Goose Duck` | `OpenProcess` succeeds, **`EnumProcessModulesEx` fails with `ERROR_ACCESS_DENIED (5)`** |
| `EasyAntiCheat_EOS` | `OpenProcess` fails with `5` |
| `GGDLauncher` | 62 modules readable, **none** matching any blocklist prefix or fuzzy fragment |

So the module tier's `EasyAntiCheat` entries can never fire on the process that
matters: the guard gets `kFailed` → `kProcessUnreadable`, which is a refusal for
"could not look", not for "found EAC". The handle opening while the enumeration
is denied is worth remembering — a probe that only checked `OpenProcess` would
have concluded the process was readable.

**The consequence for §S16:** the readable member of this title's tree is the
launcher, and the launcher carries nothing. The service check is doing all the
work.

### 🔴 Two whole families were missing, and one is kernel-level

| | |
|---|---|
| `ACE-BASE.sys`, `ACE-ADVT.sys` | Installed under `System32\drivers` by Neverness To Everness (Anti-Cheat Expert, **kernel level**). Matched **nothing** — the family was absent from `19_SAFETY`'s table and from the data |
| service `AntiCheatExpert Protection` | Registered. Matched nothing |
| `EasyAntiCheat_EOSSys` | Running as a **kernel driver**. Matched nothing in `drivers`; the refusal came from `services` instead |
| `NTEGlobal/driver/PGameProtectDriver_X64.sys` | A kernel driver shipped **inside the game install**. Matched nothing. Its name contains `protect`, but the fuzzy tier is modules-only and never looks at check 4's file and directory groups |

All four are now in the data and in `19_SAFETY` §Blocklist seed, with the
unmeasured siblings marked as such.

**Valve VAC remains absent, and CS2 measures `Allow`.** VAC is neither a
machine-wide driver nor a service; it is modules loaded into the game process,
which — per the previous section — is exactly what cannot be read. The route
`19_SAFETY` reserves for it is `blockedStoreIds` — check 3's **store-id half**,
which cannot be called (§S14). #52 wired the *executable* half; that changes
nothing here, because a renamed executable defeats a name list and the store-id
route is exactly why `19_SAFETY` reserves it. Recorded rather than papered over:
**a real VAC title is allowed by this guard today.**

### 🔴 The data addition alone changed nothing, and that is the sharper finding

Adding `PGameProtectDriver_X64.sys` made check 4 refuse when the scan started at
`NTEGlobal` — and still return `Allow` from the **install root**, which is where
check 4 actually runs (`ImageDirectory` → `ResolveInstallRoot`).

`kMaxPreScanDepth` was 2. Measured with a control tree carrying a known
blocklisted file at increasing depth:

| File at | Old cap | New cap |
|---|---|---|
| depth 0 | refuse | refuse |
| depth 1 | refuse | refuse |
| depth 2 | **Allow** | refuse |
| depth 3 | Allow | **Allow** |

The driver sits at depth 2. So the blocklist row was coverage on paper — a row
that cannot be reached from where the scan begins.

**The cap was measured before it moved, because it is a REFUSAL and not a
truncation:** overrunning `kMaxPreScanEntries = 4096` does not scan less, it
refuses the title. Across 67 installed titles the worst case is **506** entries
at the old reach and **729** at the new one — 18% of budget, 5.6x headroom.

### ✅ Proven on the whole corpus, both directions

The deeper pre-scan run against all 67 installed titles:

| Verdict | Count |
|---|---|
| `Allow` | 65 |
| `AntiCheatDirectory` (Goose Goose Duck) | 1 |
| `AntiCheatFile` (Neverness To Everness) | 1 |
| `PreScanFailed` | **0** |

Exactly the two anti-cheat titles refuse, nothing else does, and the deeper walk
produced no false refusal on any real install.

### What is still unmeasured

- Whether the module tier fires on **any** real anti-cheat. It could not be
  reached here: the one title whose modules are readable carries none.
- Whether the driver tier fires. `ACE-*` and `EasyAntiCheat*` are now in
  `drivers`, but both were **Stopped** during the measurement window that would
  have exercised it, and the EAC session was caught by the service check first.
  The driver rows are therefore data-complete and **behaviourally untested**.
- VAC, as above.

---

