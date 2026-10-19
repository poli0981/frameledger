*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 19 · Anti-cheat beside the executable — Wuthering Waves *(2026-10-13, beta.21, owner decision D58)*

The owner's report: FrameLedger found no anti-cheat in `D:\SteamLibrary\steamapps\common\Wuthering Waves` (Steam
`3513350`, Unreal Engine 4.26.2). Read-only, on the owner's disk and in the Agent's own log.

**The layout.** The install root holds the launcher `Wuthering Waves.exe` (483,768 bytes), `Client\`, `Engine\` and
`ClearThirdParty.exe`; the library entry is the shipping binary `Client\Binaries\Win64\Client-Win64-Shipping.exe`
(169,213,960 bytes). Beside it, `Client\Binaries\Win64\AntiCheatExpert\` holds Tencent's ACE: `ACE-BASE.sys`
(4,125,432), `ACE-CORE.sys` (3,923,120) with `ACE-CORE.sys2`, `.sysa` and `.sysa2`, `ACE-Base64.dll` (69,736,848),
`ACE-Service64.exe`, `ACE-Setup64.exe`, `ACE-Base.dat` and `pld.dat`. The folder is **four levels** under the root;
entries per level under the root: 6, 10, 31, 276, 109, 136 (levels 1–6). Neverness To Everness keeps a second
`AntiCheatExpert\` beside its `HTGame.exe`, at `Client\WindowsNoEditor\HT\Binaries\Win64\` — **six** levels down.

**Why it read clean.** Check 4 walked the install root only (since 2026-09-25), three levels deep, and a folder at the
last level is named and never opened, silently: the walk listed `Win64` and reported a complete, clean tree. The rules
already named the folder (`directories`: `AntiCheatExpert`); the walk never handed the name to the matcher.

**What it cost, from the Agent's log.** Added by import 2026-10-11 19:50:19 → `prescan: scanned 2 — anti-cheat 1, clean 1`
(the anti-cheat was NTE's `PGameProtectDriver_X64.sys`, three levels down); re-scanned after the game's update at
21:22:14, clean; re-added 2026-10-13 18:30:44, clean. With hooking turned on, the session of 2026-10-12 started at
21:10:20 found nothing — ACE's driver not yet loaded, its services stopped — and was injected; at 21:10:57 the 30 s
re-scan met `ACE-BASE.sys` (written to `System32\drivers` at 21:10 by the game's ACE setup) and unhooked:
`SafetyUnhook; Saved (tier 1, exit=UnhookedSafety, frames=0); guard=BlockedDriver/Anti-Cheat Expert`. A driver is a
machine-wide finding, which refuses the session and turns nothing off, so the game's hooking stayed on. Registered on
the machine afterwards: the kernel service `ACE-BASE` (demand start) and the user-mode `AntiCheatExpert Protection`.

**The options, measured across the owner's 83 Steam installs** (read-only listings):

| Option | Cost | Newly finds | Verdict |
|---|---|---|---|
| Walk the executable's own folder too | ≤ 103 entries (NTE), 95 for Wuthering Waves | exactly the two `AntiCheatExpert\` folders above | **taken** |
| Depth 3 → 4 | one constant; peak 1,627 entries (Half-Life 2) | Wuthering Waves' folder name only — NTE's is six levels down, and the folder's files stay out of reach | rejected |
| Depth 5 or more | over 4096 entries for Yu-Gi-Oh! Master Duel (41,251), Yasha (12,215), GIRLS' FRONTLINE 2 (9,969) | — | rejected: those games would read *could not verify* and be refused at session start, GF2 under its D33 exception included |
| Make the depth cap refuse | — | — | rejected: it would refuse nearly every game |

**Taken (rules `2026.10.4`):** check 4 walks the install root, then the executable's folder when it lives below the
root (`ScanGameFolders`, `fl_prescan.cpp`), each with its own 4096-entry budget; the five `ACE-*` names join the
`files` group, `ACE-CORE.sys` the `drivers` group, `ACE-BASE` the `services` group and `ACE-Base64.dll` a `modules`
row of its own (so an injected Overlay stops itself if it loads); and Wuthering Waves' Steam id is on the title list.
The version bump is what makes every game's pre-scan run again: the sweep re-scans only when the rules version or the
executable changes.

- **Not measured:** that the game loads `ACE-Base64.dll` (no session has run since), and whether ACE's other titles
  keep the same layout.

