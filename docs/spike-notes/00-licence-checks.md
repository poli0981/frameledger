*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 0 · Licence checks *(no hardware needed — do these first)*

Answers `20_OPEN_QUESTIONS` §M3, §M4. Both can invalidate a whole telemetry
layer, and neither needs a GPU.

### ✅ M4 · LibreHardwareMonitor MPL-2.0 Exhibit B — **CLEAR** (2026-08-02)

- Pinned version: `LibreHardwareMonitorLib` **0.9.6**, nuspec
  `<license type="expression">MPL-2.0</license>`, repo commit `3d331e33`.
- **The LICENSE file is not the evidence.** MPL-2.0's own text *contains*
  Exhibit B as a template (at lines 369–372 of the licence), so grepping the
  licence file finds the string in every MPL-2.0 project ever published and
  proves nothing. What matters is whether a **source file applies** the notice.
- Authenticated code search across the whole repository for
  "Incompatible With Secondary Licenses": **total_count = 1, and the single hit
  is `LICENSE` itself** — the template, not an applied notice.
- Every file we depend on carries **Exhibit A**, the permissive form:
  `Computer.cs`, `Sensor.cs`, `Gpu/NvidiaGpu.cs`, `Gpu/AmdGpu.cs` all begin
  "This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0" with no Exhibit B sentence.
- The shipped 0.9.6 `.nupkg` was unpacked and scanned: **no Exhibit B notice in
  any of its 46 entries.**
- **Result: MPL-2.0 §3.3 Secondary Licenses applies → GPL-3.0 compatible.**
  L2 telemetry is safe to build on.

Two things this turned up that are *not* blockers but are now known:

- **The package ships no licence file at all** (`<license>` is an SPDX
  expression). MPL-2.0 §3.1 therefore makes shipping the text **our**
  obligation — `legal/licenses/mpl-2.0.txt` is now committed, and the
  `THIRD_PARTY_NOTICES` release gate is a real requirement, not boilerplate.
- `Gpu/IntelIntegratedGpu.cs` has **no licence header** — an upstream
  inconsistency, not an Exhibit B problem. The repo-root LICENSE still governs.
  Worth re-checking on any version bump.

### ✅ M3 · NVAPI licensing and the import library — **CLEAR** (2026-08-02)

- Artifact: <https://github.com/NVIDIA/nvapi>, `main`.
- **The import library is in the repository**: `amd64/nvapi64.lib` and
  `x86/nvapi.lib` are tracked files, not a separate SDK download. That was the
  crux of the question — a `.lib` obtained from the NVIDIA SDK installer would
  have come with the SDK's own agreement instead.
- `License.txt` answers it in its first line, unusually explicitly:
  > `nvapi.lib and nvapi64.lib are licensed under the following terms:`
  followed by `SPDX-License-Identifier: MIT` and the standard MIT text. **The
  grant names the import libraries as its subject.**
- Headers carry their own `SPDX-License-Identifier: MIT` blocks
  (`nvapi.h`, `nvapi_lite_common.h`, `nvapi_interface.h` all verified).
- **Result: headers and `nvapi64.lib` are both MIT.** MIT is one-way compatible
  with GPL-3.0; vendoring is fine provided the notice is retained, which
  `tools/license-check.ps1` enforces. `legal/licenses/nvapi-MIT.txt` committed.
- Reflex / PC latency (FR-4.6, the latency tab, `sessions.latency_*`) is
  therefore reachable. Ordinal resolution stays rejected — it was only ever a
  workaround for a constraint that does not exist.

---

