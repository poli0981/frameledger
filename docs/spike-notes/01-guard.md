*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 1 · Guard *(moved to first — `20_OPEN_QUESTIONS` §R1)*

Measured 2026-08-02, Windows 11 Pro Insider Preview 26300.9032, **unelevated**
(the default Agent under ADR-9). Probe: `src/native/tools/fl-probe-guard`,
running as ctest `fl_guard_apis`, so these are re-checked on every build rather
than being a one-off. It opens processes only with
`PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ` — the rights `19_SAFETY`
specifies — and never with `CREATE_THREAD | VM_OPERATION | VM_WRITE`.

### ✅ §S1 · Module enumeration on a suspended process — **it fails, it does not return empty**

```
suspended:          opened=1  enumerated=0  modules=0  enumErr=299 (ERROR_PARTIAL_COPY)
after ResumeThread: opened=1  enumerated=1  modules=9   (kernel32.dll present)
```

S1 predicted `EnumProcessModulesEx` "returns essentially nothing". **Measured, it
is sharper than that, and the difference is the whole point:** the call *fails*
with `ERROR_PARTIAL_COPY (299)`. It does not hand back a plausible empty success.

That is the good version of this news. An empty success is the dangerous shape —
it is exactly what `EnumDeviceDrivers` does, and it is what a guard reads as
"clean". An outright error cannot be mistaken for a clean scan by any caller that
checks the return value. So the guard's obligation here is narrow and explicit:

> **`ERROR_PARTIAL_COPY` means CANNOT DETERMINE, which means REFUSE.** Never
> "no modules found".

The blindness is the suspension and not our handle rights — the same target with
the same rights enumerates normally once resumed. §S1 is confirmed as a real
constraint on launch mode; what to *do* about it is still §S13(c).

### ✅ §S7 · WOW64 — the wrong filter under-reports, and silently

Live 32-bit target (`SysWOW64\cmd.exe`) enumerated from this x64 process:

| Filter | Modules |
|---|---|
| `LIST_MODULES_ALL` | **15** |
| `LIST_MODULES_DEFAULT` | 7 |
| `LIST_MODULES_32BIT` | 9 |
| `LIST_MODULES_64BIT` | 6 |

The default filter returns **less than half** the list, and returns it as a
*success*. A guard using the default would scan a 32-bit title, see 7 modules,
find no anti-cheat among them and report clean. `LIST_MODULES_ALL` is mandatory,
not a preference.

### ✅ Fail-closed inputs — "cannot inspect" is a distinguishable state

- **Protected process:** `OpenProcess` with the guard's own rights returns
  `ERROR_ACCESS_DENIED (5)` for at least one process on this machine. So
  "I could not look" and "I looked and it was clean" are genuinely different
  values, and `19_SAFETY` §Elevated / protected targets is implementable.
- **Absent service:** `OpenServiceW` on a non-existent name returns
  `ERROR_SERVICE_DOES_NOT_EXIST (1060)` **specifically**, not a generic failure —
  which is the discrimination `19_SAFETY` relies on.
- **NOT MEASURED — the DENIED service branch.** A standard user holds
  `SERVICE_QUERY_STATUS` on the stock service set, so `ACCESS_DENIED` is not
  producible against real services here. It has to be driven from a unit-test
  fake. Recorded rather than implied: A5 does **not** cover it.

### ✅ Driver enumeration — the replacement measured against the fail-open

```
EnumDeviceDrivers                 ok=1  count=266  non-null bases=0  recoverable names=0
NtQuerySystemInformation(11)      STATUS_SUCCESS  78,744 bytes
                                  parsed=266  well-formed=266  resolvable-on-disk=257  ntoskrnl=yes
```

`EnumDeviceDrivers` reproduces its documented fail-open exactly: 266 drivers
reported, **zero** usable base addresses, **zero** recoverable names. The `Nt*`
route returns 266 real native paths.

**The fail-open is purely a function of elevation** — established by CI, which
runs elevated and failed this probe's original assertion:

| Configuration | `EnumDeviceDrivers` |
|---|---|
| unelevated (this machine, the ADR-9 default) | 266 drivers, **0** bases, **0** names |
| elevated (GitHub `windows-latest`) | 260 drivers, **260** bases, **260** names |

The API is not broken; it is broken *for standard users*. That is the
configuration ADR-9 makes the default, and it means anyone who tests this while
elevated sees a perfectly working call and concludes the defect is imaginary.

The probe's assertion was originally unconditional — "the `Nt*` route recovers
more identities" — which encoded one machine's configuration as a universal
fact and went red on CI for a correct reason. It is now elevation-aware: it
asserts the fail-open when unelevated, and when elevated says plainly that this
run **cannot** demonstrate it.

**The assertions here check content, not count — deliberately.** "266 distinct
non-empty strings" is *not* discriminating: the earlier two-byte offset bug
produced exactly that, and every string was garbage. So the probe asserts that
`ntoskrnl.exe` is present, that **every** path is a native path (`\SystemRoot\`
or `\??\`), and that 257 of them name files that actually exist on disk.

**Canary, run every build:** the same buffer is re-parsed with the historical
two-byte skew and the validator must reject it —
`well-formed=0, ntoskrnl=NO`, first entry `ystemRoot\system32\ntoskrnl.exe`.
A count check passes that. This one does not.

### ◐ §S19(b) · the signer half, measured before it is designed — `fl-probe-signer` (2026-08-27)

`ctest fl_signer_probe`, unelevated, dev box. §S19(b) asked for three answers before
any design is fixed; these are they. The analysis and the decision rows live in
`20_OPEN_QUESTIONS` §S19(b) — what belongs here is what was **run**.

**Q1 · which route recovers an organisation.** Offline flags throughout
(`WTD_REVOKE_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL`):

| Subject | embedded | catalog | `O=` |
|---|---|---|---|
| `System.Security.Cryptography.ProtectedData.dll` (NuGet **6.0.0**) | **`ERROR_SUCCESS`** 3.61 ms | no catalog | `Microsoft Corporation` |
| `mskeyprotect.dll` | **`TRUST_E_NOSIGNATURE`** 0.23 ms | **`ERROR_SUCCESS`** 3.62 ms | `Microsoft Corporation` |
| `kernel32.dll` | `ERROR_SUCCESS` 4.69 ms | `ERROR_SUCCESS` 3.48 ms | `Microsoft Corporation` |

Full subject on every one: `C=US, S=Washington, L=Redmond, O=Microsoft Corporation,
CN=Microsoft Windows` — so `signerField: "O"` holds here, on a subject class it had
never been measured against.

**Two results that cut opposite ways.** The CI blocker is embedded-signed and verifies
offline, so the embedded half alone would clear that refusal. `mskeyprotect.dll` — the
module §S19(b) was *written about* — does not, and needs `CryptCATAdmin*`. The entry
predicted exactly that and it is now measured rather than argued.

**`kernel32.dll` carries BOTH**, which the probe was written assuming it would not.
"Catalog-signed" is not inferable from a file being a system binary; it is a per-file
measurement, which is why the probe reports both routes for every subject.

**And the module is not what §S19(b) says it is.** It calls the blocker *"a .NET
shared-framework assembly"*. `Microsoft.NETCore.App` 10.0.11 does not contain it. It
is a NuGet package assembly reached transitively as `Microsoft.NET.Test.Sdk` →
`System.Configuration.ConfigurationManager` → `System.Security.Cryptography.ProtectedData`,
staged next to every test binary. **Dropping the reference means dropping the test
SDK**, so the cheapest-looking route out of the CI refusal is closed.

**Q2 · cost.** Cold 3.45 ms; warm mean of 20 = **3.54 ms**. There is no amortisation:
the second verification of the same file costs what the first did. Comfortable against
the 30 s re-scan **because the scan set is small** — three real titles produced no
fragment hit at all — and it stops being comfortable on a title that trips the fuzzy
tier repeatedly.

**Q3 · 🔴 `cryptnet.dll` is newly loaded UNDER THE OFFLINE FLAGS.** Census bracketing
the offline arm alone: `cryptnet.dll` appears. Nothing further appears once the
default `WTD_REVOKE_WHOLECHAIN` calls run, so this is not the default arm leaking into
the measurement.

> **The first version of this probe could not have told those apart.** It censused
> once at the top and once at the bottom with both arms in between, so the module
> appeared and the delta could not attribute it — a census spanning both arms of the
> comparison it exists to discriminate. Fixed before the run recorded above. Same
> shape as §S30's *"two numbers agreeing can be one number read twice"*, one layer
> down.

**Mapped is not transmitted, and this probe cannot close the gap.** `cryptnet.dll`
being loaded is the module that *would* make a request, not evidence that one was
made; there is no packet counter here. **The discriminating run is the owner's:
adapters disabled, same three subjects, compare verdicts.** A verdict that changes
offline retires the route, and §S19(b)'s row G2 says so before the run.

**Canary.** The probe's own unsigned executable returns `TRUST_E_NOSIGNATURE` and
yields no organisation — so a green result discriminates rather than merely running.
`fl-baseline-probe` was retired by exactly this class of test.

~~**Not measured here:** the CI leg (a different machine stages a different copy), and
the adapters-disabled leg. Both have pre-committed rows. An unrun leg is unrun.~~
**The CI leg, 2026-09-06** (the ctest printed nothing for eleven days; `ci.yml` now runs the
probe as a step): `windows-latest`, elevated. Blocker copy from the runner's NuGet cache,
6.0.0, embedded/offline `ERROR_SUCCESS` 3.47 ms, `O= Microsoft Corporation`, no catalog;
`mskeyprotect.dll` embedded `TRUST_E_NOSIGNATURE`, catalog `ERROR_SUCCESS`; `kernel32.dll`
both; cold 3.45 / warm 3.53 ms; `cryptnet.dll` newly loaded under the offline flags; canary
`TRUST_E_NOSIGNATURE`; *"G1 is a CANDIDATE"*. Rows G3, G4, G5 do not fire.
**The adapters-disabled leg, the owner's run, 2026-09-06** (dev box, elevated): every verdict
unchanged — `kernel32.dll` both `ERROR_SUCCESS`; `mskeyprotect.dll` embedded `TRUST_E_NOSIGNATURE`,
catalog `ERROR_SUCCESS`; the blocker embedded `ERROR_SUCCESS`, `O= Microsoft Corporation`, subject
now `CN=.NET` (the August copy read `CN=Microsoft Windows`; same `O=`); cold 2.14 / warm 2.15 ms;
`cryptnet.dll` mapped; canary `TRUST_E_NOSIGNATURE`. **Row G1 reads**; the bound decision is the
owner's before anything is built.

### Still open

- **§S13(c) — decision on launch-mode injection timing.** The measurement above
  makes the constraint precise but does not make the decision. Deliberately not
  taken here: quantifying the early-init data actually lost needs a title that
  loads a presentation runtime lazily, and the only local fixtures are
  `hook-harness` (creates D3D at startup) and a 2D GOG prologue. A number from
  those would not generalise, which is worse than recording it as unmeasured.
  *(2026-09-06: built as "inject late" — P1 item 2 — and the number now comes
  from every launched session, as `dxgiPresentsBeforeHook` plus the reported
  wait. `20_OPEN_QUESTIONS` §S1.)*

### ✅ Signer field for the unknown-but-suspicious heuristic — **`O=`, not `CN=`**

Measured 2026-08-02, Windows 11 26300, unelevated, via
`Get-AuthenticodeSignature` on real binaries present on this machine.

| Binary | `CN=` | `O=` |
|---|---|---|
| `kernel32.dll` | Microsoft Windows | Microsoft Corporation |
| `win32u.dll` | Microsoft Windows | Microsoft Corporation |
| `nvapi64.dll` | **Microsoft Windows Hardware Compatibility Publisher** | Microsoft Corporation |
| `nvlddmkm.sys` (the display driver) | **Microsoft Windows Hardware Compatibility Publisher** | Microsoft Corporation |
| `nvrla.exe` | NVIDIA Corporation | NVIDIA Corporation |
| `steam.exe` | Valve Corp. | Valve Corp. |

The `trustedSigners` list was a guess marked UNVERIFIED, and the schema's own
comment said the comparison was against **CN**. Measurement says that is the
wrong field: **WHQL re-signing replaces the CN with a Microsoft publisher
string**, so NVIDIA's own kernel driver does not present as NVIDIA. Under a
CN comparison the whole driver stack reads as untrusted, and paired with the
`guard`/`protect` name fragments that is a false-refusal path — the failure
direction that is safe for the user's account but destroys trust in the gate,
which is how someone ends up asking for the override CLAUDE.md rule 2 says does
not exist.

`signerField` is now a schema **`const": "O"`**, so switching it back requires
editing the schema. `"Microsoft Windows"` was dropped from `trustedSigners` — it
is a CN value and matches nothing under `O=`.

**Untested by decision (2026-08-02):** `Intel Corporation` and
`Advanced Micro Devices, Inc.`. No AMD or Intel GPU and no iGPU on this machine
(§Environment), and the owner elected to keep NVIDIA as the v1 focus rather than
chase them.

The WHQL finding above is what makes that cheap: both vendors' driver binaries
are WHQL-signed, so they present `O='Microsoft Corporation'` and are already
suppressed by the first entry. Only their **non-WHQL first-party** binaries
depend on these two strings, and a wrong string there fails **closed** — a
refusal, never a silent allow. Kept in the data, labelled untested, not
presented as measured.

