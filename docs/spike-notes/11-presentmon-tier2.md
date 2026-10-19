*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 11 · PresentMon / Tier 2 *(§M2, §M6)*

### ◐ Measured 2026-08-20 — the binary is here, and it will not run for us

- **Console binary obtained, version, pinned hash: ✅**
  `C:\Program Files\Intel\PresentMon\PresentMonConsoleApplication\PresentMon-2.5.1-x64.exe`,
  **956,768 bytes**, SHA256
  `9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191`.
  Version **2.5.1**, read from its own `--help` banner.

  > **It carries NO VERSIONINFO at all** — `FileVersion`, `ProductVersion` and
  > `CompanyName` are all empty. So "pin the version" has to mean **pin the hash
  > and the filename**; there is nothing in the file for a `versioninfo-check`-style
  > gate to read. Worth stating because this repository requires exactly that
  > metadata of everything it builds (`19_SAFETY`, `tools/versioninfo-check.ps1`),
  > and a bundled third-party binary that lacks it is a packaging fact, not a
  > detail.

- **Runs unelevated: 🔴 NO, on this machine.** Measured against
  `hook-harness --hold-presenting`, current user `DESKTOP-NUHVIDP\Anon`:

  ```
  error: failed to start trace session: access denied.
         PresentMon requires either administrative privileges or to be run by a
         user in the "Performance Log Users" user group.
  ```

  Exit code **6**, no CSV written. The account is in `BUILTIN\Users`,
  `docker-users` and `Authenticated Users` — **not** an administrator, and **not**
  in `Performance Log Users` (`S-1-5-32-559`), so the refusal is exactly what it
  says rather than something subtler.

- **Performance Log Users sufficient without admin: ❓ UNMEASURED**, and it stays
  that way here. Adding an account to a local group is a system settings change and
  it needs administrative rights of its own; it is the owner's to do, not a session's.

- **The shared service is running and it does NOT help the console.**
  `PresentMonSharedService` is `Running` / `Automatic`, `StartName = LocalSystem`,
  from `C:\Program Files\Intel\PresentMonSharedService\PresentMonService.exe`. The
  console binary still starts its **own** trace session and still fails. So §M6's
  "or the PresentMon Service" half is not answered by the service merely being
  installed — the console does not talk to it. Whether `PresentMon.exe` (the
  Application, a GUI) does is unmeasured and would not produce a CSV anyway.

- **2.x column set over stdout: ❓ UNMEASURED.** No session, no CSV, no header. This
  is the input `tools/frametype-oracle.ps1` parses, so **that parser has never seen
  a real PresentMon CSV** — it resolves columns by name and refuses loudly on
  anything it does not recognise, and its decision table is the only thing standing
  behind it. Said plainly rather than left for the next reader to assume.

### 🔴 THE RUN HAPPENED — 2026-08-27, three legs, three game launches, and row **P2**

Owner-run from an elevated shell via a three-leg runbook script. **That script is not in
this repository** — it drove PresentMon, so it was closed unmerged when PresentMon was dropped
the same day; do not go looking for it. Cyberpunk 2077, frame
generation off / ×2 / ×4 set in the title's own menu, 40 s per leg, one game launch
each. Analysis is in `20_OPEN_QUESTIONS` §S31; what belongs here is what was measured.

**The 2.x column set, which §M2 left unmeasured: 24 columns.**

```
Application, ProcessID, SwapChainAddress, PresentRuntime, SyncInterval, PresentFlags,
AllowsTearing, PresentMode, FrameType, CPUStartTime, FrameTime, CPUBusy, CPUWait,
GPULatency, GPUTime, GPUBusy, GPUWait, DisplayLatency, DisplayedTime, AnimationError,
AnimationTime, MsFlipDelay, AllInputToPhotonLatency, ClickToPhotonLatency
```

**`FrameType` is present, and every row of all three legs reads `Application`.**
Counted off the raw CSVs rather than off the tool's own summary:

| leg | rows | distinct `FrameType` values |
|---|---|---|
| off | 1,937 | `Application` × 1,937 |
| ×2 | 6,488 | `Application` × 6,488 |
| ×4 | 10,881 | `Application` × 10,881 |

**And the two instruments agree on the present rate to within 0.3%**, which is what
makes the finding a measurement rather than a shrug — PresentMon SAW the generated
presents:

| leg | our hook | PresentMon | our `presents/batch` | our `evaluations` |
|---|---|---|---|---|
| off | 1,676 presents / 38.82 s = **43.16 /s** | 1,937 / 45 s = **43.04 /s** | N/A — no batch drained | 0 |
| ×2 | 5,746 / 38.64 s = **144.31 /s** | 6,488 / 45 s = **144.18 /s** | **2.00** | 0 |
| ×4 | 9,652 / 38.77 s = **241.84 /s** | 10,881 / 45 s = **241.80 /s** | **3.99** | 0 |

Every leg: `status=Ready`, `faults=0`, `layoutVersion=3`, `attach=Ok`, one stream, one
segment, **0 gaps and 0 dropped**, `unidentified=0`.

So displayed rate tracks the configured multiplier, both instruments count the same
present stream, and at ×4 roughly three presents in four cannot be application frames
— while PresentMon classifies **100%** of them `Application`. That is §S31 row **P2**,
and `tools/frametype-oracle.ps1` reported it as a **falsifier** on all three legs
rather than as a ratio of 1.0, which is the one behaviour it was written to guarantee.

**Unmeasured, and it decides the REASON rather than the action:** whether
`--track_frame_type` was in effect at all. If `FrameType` ships in `--v2_metrics`
output regardless of the flag, these legs recorded a default. The discriminator is one
five-second run **without** the flag, checking only whether the header carries the
column — no game, no capture, no launch spent. It still needs elevation; attempted
unelevated 2026-08-27 and refused with the same exit 6 this section opens with.

> **Both branches end at the same action**, which is why §S31 landed anyway: either the
> vendor emitted nothing, or the invocation produced no frame-type data at all. In
> neither case did PresentMon answer the question **as invoked**.

**The discriminator was run on 2026-08-27 and produced no file at all**, so it settled nothing:
the command targeted `explorer.exe`, which may present nothing PresentMon tracks, and "no CSV"
is indistinguishable from "no `FrameType` column". Badly chosen target, recorded as such. Moot
now — PresentMon was dropped the same day — and written down so nobody re-runs it thinking it
is untried.
**Two findings nobody was looking for.**

- **The `off` leg drained ZERO Streamline batches**, against `presents/batch = 1.000`
  at off in §8. The difference is Ray Reconstruction: §S30 established RR is evaluated
  once per application frame, so with RR on there are batches even with FG off. RR was
  not on here. **Our side therefore had two readable legs, not three** — turn RR on if
  the off leg needs to carry a ratio.
- **The CSV's first column is named `Application`** and holds the process name, while
  the value being counted, in column 9, is also the string `Application`. A parser
  resolving by position — or grepping for the word — counts process names and reports a
  ratio from them. `frametype-oracle.ps1`'s *"resolved BY NAME from the header, never by
  position"* was written before it had ever seen a real CSV, against exactly the
  collision that was waiting in it.

**`kFeatureDLSS_G` is zero on all three legs**, which takes the count to five capture
sets across five titles. `HANDOFF` item 3's premise has now failed everywhere it has
been looked for.

**Artifacts:** `%LOCALAPPDATA%\FrameLedger\s31\{off,x2,x4}\` — `presentmon.csv` and
`capturehost-report.txt` per leg. Not committed; they are measurement output and the
numbers above are the record.

### 🔴 `--track_frame_type` is a BETA option that needs the VENDOR to cooperate

This is the finding, and it lands on a pre-committed oracle. `PresentMon 2.5.1
--help`, verbatim, under **Beta Options**:

> `--track_frame_type`  Track the type of each displayed frame; **requires
> application and/or driver instrumentation using Intel-PresentMon provider.**

So `FrameType` is **not** a general ETW classification of any present. It is a
report of events the application or the graphics driver chose to emit through
Intel's provider. `03_METRICS` rung 2 says *"PresentMon 2.x `FrameType` column
reports generated frames directly"* and §S30 called it *"a mechanism that divides
by nothing"* — both are true of the mechanism and neither is a promise that the
mechanism is **available** on an NVIDIA DLSS-G title. Whether NVIDIA's driver
instruments that provider is the make-or-break question and is unmeasured.

**The falsifier is written before the run** (§S31): if a DLSS-G capture comes back
with no `FrameType` column, or with every row spelled `Application` while frame
generation was demonstrably on, PresentMon is retired as the application-frame
oracle for NVIDIA frame generation **in that same row** — exactly as
`fl-baseline-probe` was, rather than being re-run until it answers.

