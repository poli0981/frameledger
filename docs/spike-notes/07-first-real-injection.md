*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 7 · First real injection *(requires the guard, §1)*

### ✅ Done — 2026-08-03

- **Title:** Lies of P (Steam, Unreal, x64, single-player, no anti-cheat).
  Injected into `LOP-Win64-Shipping.exe`, the presenting process — `LOP.exe` at
  the install root is a shim and was left alone.
- **`/MT` DLL loaded without incident: yes.** `FlGuardEvaluate` → `Allow`;
  `FlGuardedInject` → `Allow`; `FrameLedger.Overlay.dll` present in the target on
  re-enumeration (143 → 144 modules), mapped at `0x7FFA60340000`, 820 KB.
  Afterwards: alive, responding, **103.6 s of CPU over 6 s wall-clock** across
  180 threads — still rendering, not stalled.
- **It also shut down cleanly with the Overlay still mapped.** `WM_CLOSE`, and
  both `LOP-Win64-Shipping.exe` and the `LOP.exe` shim exited on their own; no
  hang, no crash dialog, no orphan. Worth stating because process exit runs
  `DllMain(DLL_PROCESS_DETACH)` on a loader-locked thread, which is the one
  lifecycle path a `/MT` static-CRT DLL is most likely to fail on, and it is not
  exercised by `hook-harness` — the harness is killed, not closed. **Nothing is
  hooked yet**, so this clears the loader, not the unhook path; P1's fault
  policy and unhook still need the same check once `Present` is hooked.
- Reached through the shipped C ABI (`FrameLedger.Guard.dll`), so there was no
  path that skipped the gate. There is no injector CLI and there will not be —
  §S9 closed that as a design decision, not as a gap.

**The three refusals that came first are the valuable part.** Every one was a
real defect, and none would have surfaced without a real machine and a real
title:

| Attempt | Verdict | What it actually was |
|---|---|---|
| Deadly Heart Gambit, no rules installed | `RulesUnreadable` | Correct. A machine that has never run the product refuses by default. |
| Same, rules installed | `BlockedService` `EasyAntiCheat_EOS` | **Defect.** A Stopped/Manual service installed by an unrelated EOS game made the guard refuse *every process on the machine*, `explorer.exe` included. Fixed: present now means running. |
| Same, from the launching shell | `SuspiciousUnsigned` `FrameLedger.Guard.dll` | **Defect, open.** Our own DLL trips our own `guard` name fragment. See below. |
| Same, from a non-ancestor process | `Allow` → injection refused | Correct, and now legible: **Deadly Heart Gambit is x86**, and the Overlay is x64-only. This fills `14_TESTING`'s manual-matrix row for a 32-bit title. |

### ✅ Closed — the guard refused itself in launch mode *(fixed 2026-08-04)*

Isolated cleanly on the same title, same machine, same rules:

| Evaluating process | Verdict |
|---|---|
| An **ancestor** of the game, with `FrameLedger.Guard.dll` loaded | `SuspiciousUnsigned` |
| Not an ancestor | `Allow` |

§S16 walks the game's ancestors up to the first platform launcher. The name
`FrameLedger.Guard.dll` contains `guard`, which is one of the heuristic's
`nameFragments`, and the signer half is not wired — so an unchecked signature is
untrusted by definition and the pair refuses.

**In launch mode the Agent *is* the parent** (`04_CAPTURE` §Launch mode) **and
hosts that DLL**, so every launch-mode injection would refuse. Attach mode is
unaffected, which is why the run above succeeded. Not fixed here; recorded as its
own item.

Note the project also ships **unsigned** (CLAUDE.md rule 9 and the pinned-stack
Packaging row — *not* `12_BUILD`, which this line used to cite and which contains
no signing text at all), so wiring the signer check would not rescue our own
binaries. Whatever the fix is, it cannot be "sign it".

#### ✅ Fixed and re-measured on three real titles — 2026-08-04

The arrangement was reproduced end to end rather than argued: a stand-in for the
Agent, built into `FrameLedger.Agent`'s own output directory **beside a real
`FrameLedger.Guard.dll`**, loads that DLL and then **launches the game**. So our
binary is the game's ancestor and carries the module whose name trips the guard's
own `guard` fragment — the 2026-08-03 shape, with a real title on the other end.

Evaluate only. `FlGuardedInject` was never called; nothing was injected into any
game.

| Title | Store / engine | Pre-fix (`bb0da0a`) | Post-fix |
|---|---|---|---|
| Deadly Heart Gambit | Steam / Unity, x86 | `SuspiciousUnsigned` — `FrameLedger.Guard.dll` | **`Allow`** |
| Lies of P | Steam / Unreal, x64 | `SuspiciousUnsigned` — `FrameLedger.Guard.dll` | **`Allow`** |
| Alan Wake 2 | Epic / Northlight | `SuspiciousUnsigned` — `FrameLedger.Guard.dll` | **`Allow`** |

Two things about the harness that are worth keeping, because both produced a
confident wrong answer first:

- **The evaluator must not be in the target's ancestor chain.** The first attempt
  used `Start-Process`, so the evaluating process — which had P/Invoked into the
  guard and therefore carried `FrameLedger.Guard.dll` itself — was the game's
  grandparent. Post-fix still refused, and it looked like the fix did not work.
  It did; the refusal was about the evaluator, whose image is in
  `C:\Program Files\PowerShell`, correctly *not* ours. The script now walks the
  chain and asserts the evaluator is absent from it before reporting.
- **Launching the stand-in through WMI put `WmiPrvSE.exe` in the scan set**,
  which an unelevated guard cannot open — a correct `ProcessUnreadable` about
  something irrelevant. Orphaning it via `cmd /c start` terminates the ancestor
  walk at the stand-in instead.

An earlier iteration also stopped at `PreScanFailed` because the target was
`cmd.exe` and check 4 then tried to list `System32`. That is check 4 answering
correctly about the wrong directory — noted because it is the shape of a green
that means nothing.

> **Decided 2026-08-03 — identity by install root.** A
> four-lens panel, three refuters and a completeness critic. All three refuters
> broke the panel's first answer. What survived: suppress the fuzzy fragment tier
> for any scan-set process whose image resolves inside our own install directory,
> never for the target, ancestor walk left intact. `GetCurrentProcessId()` was
> rejected because the defect is a property of the **binary** and
> `FrameLedger.App` carries it too; the platform-launcher-style boundary was
> rejected because everything above the Agent is an undefined category, so
> truncating there is "could not look" recorded as clean. Full reasoning and the
> rejected options in §S18.
>
> **The panel also measured that this is worth more than it looked.** §S18 is the
> sole blocker of the entire **Vulkan Tier-1 path**: the layer is gated by
> `FRAMELEDGER_ENABLE_VK_LAYER=1`, which only the launching process can set, and
> §S1 does not apply there because the Vulkan path performs no injection at all.

#### ◐ The same tier matches benign system DLLs — measured twice, and the second time changed the conclusion

**First measurement (2026-08-03), unelevated, Windows 11 26300:** 331 processes,
0 access-denied, 4 modules matching the `protect` fragment, none anti-cheat.
Recorded as "a game that loads `mskeyprotect.dll` is refused today, in the mode
that ships", and used to justify wiring the signer half next.

**Re-measured 2026-08-04 on the same machine, and neither half of that survived:**
290 processes, 0 inaccessible, **3 hits**.

| Process | Module | Signature type | `O=` | Suppressed by a file-choice signer check? |
|---|---|---|---|---|
| `WidgetService` | `mskeyprotect.dll` | **Catalog** | Microsoft Corporation | **No — no embedded signature to read** |
| `ProtonVPN.Client` | `System.Security.Cryptography.ProtectedData.dll` | Authenticode | Microsoft Corporation | Yes |
| `Malwarebytes` | `Malwarebytes.Protection.Interop.dll` | Authenticode | **Malwarebytes Inc** | **No — publisher absent from `trustedSigners`** |

Two findings, both of which change what gets built:

- **The proposed fix addresses one case of three.** The signer table in §1 above
  was taken with `Get-AuthenticodeSignature`, which consults the CatRoot
  catalogs — so it reports `O=` for files that carry no embedded signature at
  all. `mskeyprotect.dll`, `kernel32.dll` and `nvapi64.dll` are all
  `SignatureType=Catalog`. A native `WinVerifyTrust` with `WTD_CHOICE_FILE`,
  which is what "wire `IsTrustedSigner`" means in every prior note, recovers
  nothing for them; `IsTrustedSigner(nullptr)` is false by contract and the
  refusal survives. Catalog verification needs `CryptCATAdminAcquireContext2` /
  `CalcHashFromFileHandle` / `EnumCatalogFromHash` + `WTD_CHOICE_CATALOG`.
  **A measurement taken with a convenient tool does not license a design built on
  a different one.**
- **"Refused today, in attach mode" is not supported.** All three hits are
  desktop processes — a widget host, a VPN client, an AV service. None can enter
  a game's scan set: `EnumerateScanSetImpl` stops the ancestor walk at the first
  `IsPlatformLauncher` match, and that list includes `explorer.exe`,
  `services.exe` and `svchost.exe`. Three real titles (§8) were scanned with no
  fragment hit at all.

~~The defensible claim: **the `protect` fragment matches a benign, widely-loaded
Microsoft system DLL, and has not been shown to match inside any game's scan
set.** A game process loading DPAPI/CNG for save encryption or a launcher token
would trip it — plausible, unmeasured, and worth fixing; not a live incident.~~

> #### 🔴 MEASURED IN A REAL SCAN SET BY CI, 2026-08-05 — and this file is where that belongs
>
> The sentence above is superseded. CI, running the drain integration test:
>
> ```
> the guard refused our own harness: SuspiciousUnsigned unknown
> System.Security.Cryptography.ProtectedData.dll
> ```
>
> **The mechanism is the one the paragraph above ruled out.** §S16 puts the target's
> *ancestors* in the scan set; the integration test spawns `hook-harness`, so the .NET
> test host is its parent — which is the launch-mode arrangement, where the Agent is
> the game's parent. A .NET host that loads that assembly poisons its own scan set and
> the injection it is attempting is refused. **A gate that cannot pass.**
>
> Attach mode is unaffected: a normally-launched game's ancestor chain terminates at a
> platform launcher one hop above it, so no .NET host of ours is in the set.
>
> **It passed on the dev box**, because the two hosts load different module sets — the
> second time in one PR that this machine was not the configuration that mattered, the
> first being the rules file. This entry is recorded here rather than only in
> `20_OPEN_QUESTIONS` because §Purpose of this file makes measurements this file's job,
> and #51 changed only that other document.
>
> **Live consequence:** the drain tests are `Category=Integration` and CI runs
> `./build.ps1 check -SkipIntegration`, so the only end-to-end proof of the capture
> path does not run on the machine that gates merges.

§S19(b) is therefore **deferred with a written rationale** rather than scheduled
next. Before any design is fixed, `fl-probe-signer` should answer three things on
this machine, in the shape `fl-probe-guard` established: whether
`WinVerifyTrust(WTD_CHOICE_FILE)` recovers `O=` from a catalog-signed system
binary; what one full `Evaluate()` costs with catalog verification across a real
scan set, against the 30 s re-scan; and whether `WTD_REVOKE_NONE` +
`WTD_CACHE_ONLY_URL_RETRIEVAL` emits network traffic — the default
`WTD_REVOKE_WHOLECHAIN` performs CRL/OCSP fetches, which breaks **NFR-10
offline-first** as well as CLAUDE.md rule 8, from inside the hard gate.

> **THE PROBE NOW EXISTS AND HAS ANSWERED ALL THREE, 2026-08-27 — see the
> §S19(b) subsection in §1 above.** This paragraph is kept because the QUESTIONS it
> fixes are what the probe was built to, and because a reader arriving here needs to
> know the shape was decided before the answers existed. Two of the three came back
> the way it predicted; the third — whether the offline flags avoid network I/O —
> came back with `cryptnet.dll` LOADED, which is neither a pass nor a failure and is
> why the discriminating run is the owner's.

Also measured: **`gameguard` could never fire.** The match is a case-insensitive
substring and `guard` is a substring of `gameguard`, so the shorter token always
won first. A shipped rule incapable of firing independently, inside the safety
gate.

> **Closed 2026-08-05 by removing it, and the reason it was safe to remove is not
> the reason that applies to the fragment below.** These two findings sat in one
> paragraph and the paragraph's conclusion — *"neither is fixed by deleting a
> fragment"* — was true of one and false of the other.
>
> - **`protect`**: deleting it IS a detection removal. This tier is the only
>   coverage for families the seed admits it has no data for (Ricochet, VAC), and
>   three refuters rejected deletion. Unchanged, still open as §S19(b).
> - **`gameguard`**: deleting it removes **zero** coverage, by construction. That
>   is what subsumption means — every module name `gameguard` could match,
>   `guard` matches too, and matches first. Measured separately: nProtect
>   GameGuard also has its own **named module family** in the same file
>   (`GameGuard`, `npgg`, `GameMon`, exact-prefix), so the fragment was redundant
>   twice over rather than once.
>
> `rules-validate` now fails when any fragment contains another, so the list
> cannot regrow a rule that cannot fire. Note that removing it does trip
> `rules-publish`'s removal check — correctly. That gate exists to make a
> blocklist removal reviewable, and this is one.

### The guard against a RUNNING title, 2026-08-15

The precondition every verification run below depends on, and it had never been
measured against a live process. Everything earlier in this file evaluated the
guard in **launch-mode arrangement** — our binary as the game's ancestor,
evaluate-only, nothing injected — or scanned files on disk. Neither answers the
question the capture path actually asks: *does the guard allow this title while it
is playing?*

**Alan Wake 2, pid 40424, running, `FlGuardEvaluate` → `Allow`.** Reason 0, empty
family, empty signal. Rules resolved from
`C:\Users\Anon\AppData\Local\FrameLedger\rules\detection-rules.json`, 24 reason
codes.

**Evaluate only. Nothing was injected**, and no consent record exists for this
title — `FlGuardEvaluate` is the read-only half of the ABI and takes no injection
rights.

Why it needed doing at all: the installed-corpus scan (§Environment) is a **file**
scan, and the guard scans **loaded modules of the live process** plus the target's
ancestors (§S16). A title clean on disk can still load an anti-cheat module at
runtime, and Alan Wake 2 is launched by the Epic client, which is in the scan set.
Had this refused, every per-title row in §8 would have been undischargeable and the
upscaler/FG/RT work would have had no oracle to verify against.

**One title, one moment.** This is not a statement about Cyberpunk 2077, about
Alan Wake 2 on another machine, or about the same machine after a launcher update.
It is the one measurement that was missing before the feature hooks had anywhere
to prove themselves.

