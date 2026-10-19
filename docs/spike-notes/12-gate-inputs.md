*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 12 · The gate's own inputs — §S21 and §S20 *(2026-08-04)*

Analysis and the decisions live in `20_OPEN_QUESTIONS` §S21 and §S20. What
belongs here is what was **run**.

### ✅ §S21 · The override, demonstrated end to end on two real binaries

Same input to both — a crafted twelve-line rules file naming the three families
`IsCompleteEnoughToGate` required, with values that match nothing, plus a real
`EasyAntiCheat_x64.dll` loaded in the target:

| Binary | Verdict |
|---|---|
| `bb0da0a`, the tree before the fix | **`Allow`** |
| The fix branch | **`BlockedModule`**, family "Easy Anti-Cheat" |

No admin, no write to our install directory, nothing left on disk. This is the
one measurement to keep from the whole item: the hard gate could be turned off by
an inherited environment variable and a text file.

### ✅ §S21 · The ANSI path, on the real Win32 calls (system ACP 1252)

A profile component the system code page cannot spell exists on disk and
`CreateFileW` opens it, while `CreateFileA` fails `ERROR_INVALID_NAME (123)`.
Under an ASCII profile the same file opens both ways — which is exactly why a dev
box cannot see this, and why it would have shipped.

**A measurement of my own that was wrong first.** The initial round-trip used
`[Text.Encoding]::Default`, which in .NET Core is **always UTF-8** and not the
process ANSI code page, so it reported no loss. Re-run against the real ACP, it
lost the characters. *Measuring the wrong API is indistinguishable from measuring
a working one.*

### ✅ §S21 · What the first floor actually covered

Measured against the shipped seed, the hand-written three-family floor held
**4 of 22 values, 2 of 5 groups, 0 of 5 name fragments** — so it closed "a crafted
file allows everything" and left "a crafted file removes most of the blocklist"
open. Recorded because the write-up read as though it bounded more than it did.
The floor is now generated from `rules/detection-rules.json` at build time and
carries all of it.

Two residuals measured rather than asserted:

- The known-folder path **is** still user-relocatable — the `User Shell Folders`
  key under `HKCU` is FullControl for the user. That is a persistent change
  affecting every application, not a per-launch variable, so the fix is a
  narrowing and is written down as one.
- Moving the layer to `SHGetKnownFolderPath` added `ole32` and `SHELL32` to its
  imports. Resident in **62%** and **61%** of inspectable processes respectively,
  so the layer takes on no meaningful new load-time surface.

### ✅ §S20 · `MoveFileEx` vs `ReplaceFileW` against a live reader

`MoveFileExW(MOVEFILE_REPLACE_EXISTING)` returns **`ERROR_ACCESS_DENIED`** when
the destination is open by a reader holding the guard's exact share mode
(`READ|WRITE|DELETE`). `ReplaceFileW` succeeds. **This repository had prescribed
the failing one** — in a source comment and in the ledger — as guidance for
whoever built the seeder. A prescription nobody ran is a guess in the imperative
mood.

### ✅ §S20 · `rulesVersion` does not track the blocklist

The first seeder design replaced the installed file when the packaged seed was
strictly newer. Measured against this repository's own history of that file:
`31825cf` and `ba5355e` both changed the `anticheat` block **without** touching
`rulesVersion`, and `a4a2c63` — the only commit that bumped it — changed the block
**not at all**. The rule would have delivered none of the two changes it existed
for. The seeder records a hash of what it installed instead.

### ✅ §S20 · The seed half, on the machine's real rules location

Remove the file → the guard answers `RulesUnreadable`. Run the Agent → the guard
reads its rules and reaches check 1. Four Agent branches exercised against the
real location, with the machine's own file backed up and restored by hash:
already-current, installed-from-nothing, idempotent second run, corrupt file
replaced.

The feed half is untouched, so a rules edit still reaches no installed machine
until a release.

### 🔴 The canary harness lied three times before it caught anything

Kept because this file's rules ask for dead ends, and because this one produced
three confident greens in one session:

1. `ctest`/Catch2 with `-c "*name*"` **selects nothing and exits 0**, so every
   "the canary passed" was vacuous.
2. The rewritten check matched only Catch2's **failure** summary, so a genuinely
   green run parsed as "the filter selected nothing".
3. `build.ps1`'s throw escaped the harness and **left a canary edit on disk** —
   the next build then failed on an unrelated `/W4 /WX` unused-parameter warning,
   which was nearly diagnosed as the canary working.

Now: tag filters, an assertion that assertions actually ran, and `try`/`finally`
around the build. A canary that cannot go red is the defect it was written to
catch, one level up.

---

