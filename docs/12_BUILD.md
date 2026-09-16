# 12 — Build & development environment

Mixed toolchain: .NET 10 for managed projects, MSVC for the native layer.

## Prerequisites

- Windows 11 (dev), **.NET 10 SDK**, **Visual Studio 2026** with: .NET desktop development, **Desktop development with C++** (MSVC v143+, Windows 11 SDK ≥ 10.0.22621), C++ ATL not required.
- **Vulkan SDK** (for the layer + validation layers during development).
- CMake ≥ 3.28 (native projects use CMake; consumed by the solution via a build target — see below).
- Optional: PawnIO for CPU-temperature testing; an NVIDIA GPU for the NVAPI/Reflex paths (dev machine: RTX 5080). AMD/Intel telemetry paths need no SDK — if you have such hardware, testing L1/L2 coverage there is valuable.
- **No admin needed for normal development.** `FL_MOCK=1` is **specified but not implemented** — `grep -rn FL_MOCK src tests tools build.ps1` returns nothing (CLAUDE.md §Dev mode records the same, and this line contradicted it in the present tense until 2026-08-05). When it exists it runs the whole app with a synthetic source and zero injection. Until then the no-injection development path is `hook-harness`, which needs no admin either.

## Native build

`src/native/` is CMake-based (`CMakePresets.json`, presets `x64-debug`, `x64-release`):

```
cmake --preset x64-release
cmake --build --preset x64-release
```

Targets:
- `FrameLedger.Overlay` → `FrameLedger.Overlay.dll`
- `FrameLedger.VkLayer` → `FrameLedger.VkLayer.dll` + `VkLayer_FRAMELEDGER_overlay.json`
- `FrameLedger.Injector` → **static lib only.** There is no `FrameLedger.Injector.exe` and none ships (`20_OPEN_QUESTIONS` §S9, decided 2026-08-02). A standalone `LoadLibraryW` injector that users can run is a path into a game process that the guard does not stand in front of — a bypass, and a bad look for a project whose position is "we refuse where we are not welcome". The Agent links the static lib; the guard owns the chokepoint inside it (§S13(b)), so there is no callable entry point that skips the gate. Manual testing uses `hook-harness`, never a real game (§Debugging).
- `FrameLedger.NvapiBridge` → `FrameLedger.NvapiBridge.dll` (**2026-09-10, P2 PR-E2**): the C ABI the Agent reaches NVAPI through (`18_GPU_VENDOR_APIS` §L3) and the **only shipped binary that links `nvapi64.lib`**. Read-only by construction, loaded by the Agent into **its own process** by absolute path through `/FrameLedger.NvapiBridge.targets`, and **never into a game** — its name carries none of the words the guard's §S18 heuristic scans for. `fl_native_flags` only, not the hostile-environment set. `versioninfo-check.ps1` requires its version block like the other three shipped natives. ctest `fl_nvapi_bridge` (`src/native/tests/nvapi_bridge_test.cpp`) exercises every export and requires `BRANCH: AVAILABLE` or `BRANCH: DEGRADED` in the output, so a bridge gutted to `return 0` is red on a runner without the driver too.
- `FrameLedger.Overlay.Tests` → Catch2 unit tests (ring buffer, record layout, fault filter, seqlock) **and the guard's fail-closed matrix** (`14_TESTING` §Safety-guard tests) — the guard is native, so its tests are too
- `hook-harness` → the dummy **D3D11 + D3D12** app, WARP + composition swapchain so it runs headless on CI (`17_HOOK_ENGINE` §Test harness). D3D12 is device → command queue → swapchain, which is the acquisition asymmetry a D3D11-only fixture cannot exercise. ~~Vulkan and OpenGL modes to follow as those hooks land.~~ **`--vulkan --hold-presenting N` since 2026-09-06 (P1 item 3):** a real swapchain on a hidden window through the real loader, reached by `LoadLibraryW("vulkan-1.dll")` so only the vendored headers are needed and CI (no loader) still builds it; exit 77 = cannot run here, which the ctest reads as SKIP. **`--opengl --hold-presenting N` since the same day (P1 item 4):** a real context on a hidden window, `SwapBuffers`'d through gdi32 as a title does, on Microsoft's generic renderer — so it runs on CI.
- `fl-probe-hookprofile` → build-profile probes for `/guard:cf` and `-D_HAS_EXCEPTIONS=0` (`20_OPEN_QUESTIONS` §H1/§H3)
- `fl-probe-guard` → measures the Windows APIs the guard is built on, unelevated (`spike-notes.md` §1). Not the guard, and takes no injection rights.
- `fl_stub_sl_interposer` → `sl.interposer.dll`, and `fl_stub_sl_common` → `sl.common.dll` (`src/native/tools/vendor-stubs/`). **Fixtures, never shipped**, built under `FL_BUILD_TOOLS`; nothing `12_BUILD` publishes references them. They export the **measured** vendor names from `docs/vendor-exports.json` so the Overlay's symbol resolution is observable, and the second is a **decoy** exporting the same name from a different module — without it, "we resolve module-scoped" is a property no test can falsify. Their **output filenames are the vendor's and their version blocks are ours**: `fl_add_version_resource` takes the real filename so a file called `sl.interposer.dll` in our build tree still says plainly that it is FrameLedger's, which `19_SAFETY` requires of everything we build.
- `fl_stub_ffx_dx12` → `amd_fidelityfx_dx12.dll`, `fl_stub_ffx_upscaler` → `amd_fidelityfx_upscaler_dx12.dll`, `fl_stub_ffx_fg` → `amd_fidelityfx_framegeneration_dx12.dll` — the three AMD **leaves** — and `fl_stub_ffx_loader` → `amd_fidelityfx_loader_dx12.dll`, a **forwarding stand-in** bound to the two 2.x leaves through their FrameLedger-named *direct* entry — the way the real loader was measured to bypass the leaves' exports — so `--hold-presenting-ffx` can push every dispatch through a loader that is hooked along with the leaves and prove the count reads 1× (2026-09-04). Same rules as the Streamline stubs: fixtures, never shipped, vendor filenames with our version blocks. `fl_stub_ffx_fsr3_host` → `ffx_fsr3_x64.dll` (2026-09-05): the FSR 3.0 **host** facade, exporting the four names `SpeaksFsr3Host` probes for through the vendored `fsr3-v3.0.4` header's own `FFX_API`; `--ffx-topology fsr3host|fsr3host+mono` drives it, and `hookinventory-check` Pass C reads it as the export parser's second positive control.
- **`fl_fidelityfx_headers`** → an INTERFACE target over the five vendored FidelityFX ffx-api headers (`src/native/third_party/fidelityfx/`, tag v2.3.0, MIT by exception). Three include directories because upstream's headers include each other by relative path; include directories only and nothing to link — no `.lib` is vendored — and `hookinventory-check` Pass C reads the Overlay's export table because `ffx_api.h` declares its entry points `__declspec(dllexport)` unconditionally.
- **`fl_streamline_headers`** → an INTERFACE target over the vendored MIT Streamline headers (`src/native/third_party/streamline/`). Include directories only, **never linked**: `SL_API` entry points are `extern "C"` imports, and linking one would make `sl.interposer.dll` a load-time dependency of `FrameLedger.Overlay.dll`, which would stop the Overlay loading in every game that ships no Streamline — in the loader, before any of our code runs, with no message anywhere.

Compiler/linker flags (enforced in CMake, not per-target ad hoc): `/std:c++20 /MT /O2 /GS /guard:cf /Qspectre /GR- /W4 /WX`, `-D_HAS_EXCEPTIONS=0` for the Overlay target. Link `/DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA`.

**MinHook** is fetched by CMake `FetchContent` and built from source, pinned to the **commit** behind tag `v1.3.4` (`c3fcafdc`) rather than to the tag name — a tag can be moved, a commit cannot. FetchContent over a submodule so a fresh clone needs no extra step and CI has nothing to remember. Upstream warnings are not subjected to our `/W4 /WX`: warnings in third-party code are upstream's to fix, and failing our build on them would only tempt someone to patch the fetched copy, which drags it out of "consumed unmodified". The BSD-2-Clause notice ships in `legal/licenses/`, and `tools/license-check.ps1` fails the build if it goes missing while the FetchContent declaration is still present.

**NVAPI SDK** (MIT) **is vendored** at `src/native/third_party/nvapi/` — headers, `amd64/nvapi64.lib`, `License.txt`, consumed through `fl_nvapi`, proven by ctest `fl_nvapi_probe`, and **linked by a shipped binary since 2026-09-10** (`FrameLedger.NvapiBridge`, above). `legal/licenses/nvapi-MIT.txt` carries the notice and `license-check.ps1` binds the two bidirectionally.

> **This paragraph said "is not vendored yet" until 2026-08-09**, in the present tense, and added that `src/native/third_party/` *"contains `CMakeLists.txt` and `vulkan-headers` only"*. Both were false from #55 (2026-08-05) onward. The sentence was itself a correction — of an earlier claim that NVAPI *was* vendored when it was not — so this file has now been wrong in both directions about the same fact, which is worth more than the fix: **CLAUDE.md's pinned stack and `18_GPU_VENDOR_APIS` §L3 were updated when the vendoring landed and this one was not.** `legal/` is gated bidirectionally and caught its own version; `docs/` is not gated, and this is what that costs. Link it normally; do **not** resolve NVAPI by ordinal (that was a workaround for a licensing constraint that no longer exists, and it breaks across driver versions). Still guard `NvAPI_Initialize` failure as a normal path — plenty of users have no NVIDIA GPU.

**No AMD or Intel GPU *telemetry* SDK is vendored.** Those vendors' sensors are covered by LibreHardwareMonitor and the DXGI/PDH baseline (`18_GPU_VENDOR_APIS`). A build that pulls in Intel IGCL or AMD ADLX material is a licensing regression, not a feature — CI greps for it. **The AMD FidelityFX ffx-api headers (MIT by exception, tag v2.3.0) ARE vendored since 2026-09-04**, for the Overlay's upscaler hook and for types only — `fl_fidelityfx_headers` above, `18_GPU_VENDOR_APIS` §AMD for the licence reading, and `license-check.ps1` §2d for the per-file gate. Intel's XeSS SDK failed the checklist and nothing of it is here.

**VERSIONINFO is mandatory** on `FrameLedger.Overlay.dll` and `FrameLedger.VkLayer.dll`: real `CompanyName`, `ProductName=FrameLedger`, `FileDescription`, version. Identifiability is a requirement (`19_SAFETY`), and a CI check fails the build if the resource block is missing.

## Managed build & native integration

- **`global.json` pins the SDK band** (`10.0.x`, `rollForward: latestFeature`). Without it `dotnet build` picks the newest installed SDK — on a machine with a .NET 11 preview installed that silently changes analyzer behaviour under `TreatWarningsAsErrors`, and makes local and CI disagree for reasons nobody can see in a diff.
- `Directory.Build.props`:
  - **`TargetFramework` = `net10.0-windows10.0.22621.0`** and **`SupportedOSPlatformVersion` = `10.0.19045.0`**. These are two different knobs and must not be conflated: the TFM platform version selects which Windows API projections are available to compile against, while the supported version is the floor CA1416 enforces. NFR-8 requires Windows 10 **22H2** = build **19045**; letting the supported version default to the TFM's would silently accept installs below the stated floor. Two constraints pin the target to 22621 rather than something closer to the floor: the SDK rejects `SupportedOSPlatformVersion` above `TargetPlatformVersion` (NETSDK1135), and 19045 is not a targeting-pack version in any case — the packs are cut at 19041, 20348, 22000, 22621, 26100. The Windows 11 SDK ≥ 10.0.22621 is already a prerequisite above, so this costs nothing. A bare `net10.0-windows` is wrong for a different reason: it resolves to `net10.0-windows7.0`, and CA1416 would then error on Mica, `MiniDumpWriteDump` and `QueryVideoMemoryInfo` — every Windows-10-era API this app is built on.
  - `Platforms`/`PlatformTarget` = `x64`. "x64 only" is asserted in CLAUDE.md and NFR-8 but was previously enforced nowhere except an ad-hoc `-r win-x64` on the publish command.

    > **`RuntimeIdentifier` is NOT set here, and this bullet claimed it was until 2026-08-06.** The props file's own comment explains why: forcing a RID onto class libraries pulls RID-specific assets into their restore for no benefit, so each executable sets it — `FrameLedger.App`, `FrameLedger.Agent` and now `FrameLedger.CaptureHost`, plus its test project, which needs the same RID to reference a RID'd exe. A new project copying this document rather than the props file would have inherited nothing.
  - Nullable enable, ImplicitUsings enable, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, deterministic, `ContinuousIntegrationBuild` on CI.
- `Directory.Packages.props`: central package management, all versions pinned (including `WPF-UI` = 4.3.0 exactly — `16_WPFUI_SYNTAX` §Version hygiene).
- **Native output reaches the managed side through `.targets` files imported by `FrameLedger.Agent`**, not through `FrameLedger.Infrastructure`: `/FrameLedger.Guard.targets` (the guard DLL), `/FrameLedger.Overlay.targets` (the payload, §S22), `/FrameLedger.Rules.targets` (the blocklist seed) and, since 2026-09-10, `/FrameLedger.NvapiBridge.targets` (the L3 bridge; it **warns rather than fails** when the DLL is absent, because a missing telemetry layer reports itself where a missing guard would be a safety hole). Ordering comes from `build.ps1`, which runs the native build first — **not** from the solution: `FrameLedger.slnx` contains no native project.

  > **This bullet was wrong in four ways and is corrected 2026-08-04.** It named a copy of `FrameLedger.Injector.exe`, which §S9 closed and which line 25 above says does not exist, 24 lines earlier in the same document. It attributed the copying to a build target in `FrameLedger.Infrastructure.csproj`, which contains no `<Target>` at all. It said the copies happen there, when the guard DLL was deliberately moved out to a `.targets` file so it would stop flowing to every referencing project (§S18 blocker 3). And it credited the solution with an ordering the solution cannot express.
- **Struct mirror check — ✅ built 2026-08-05.** `ShmLayoutMirrorTests` runs `tools/fl-layout-dump`, reads its JSON, and asserts size plus every field offset against the C# `[StructLayout]` mirrors in `FrameLedger.Shared` — in **both directions**, so a field on either side alone fails. It also asserts blittability, which offsets cannot see. The `struct-mirror` gate reads the run's `.trx` and fails when that test class did not execute, so deleting the test is red as well as breaking it. Struct drift between the two layers is the most dangerous silent bug in this architecture; the build now genuinely catches it.

  > Until 2026-08-05 this bullet ended "the build catches it" in the present tense, while `src/FrameLedger.Shared/` held a `.csproj` and no `.cs` files — **and this document contradicted itself**, because line 138 below said correctly all along that the gate did not exist. `20_OPEN_QUESTIONS` §R10 has the history.

## Debugging

Launch profiles:
- **"UI + Mock"** — `FL_MOCK=1`, no Agent, no injection. Default for UI work.
- **"UI + Agent"** — both managed processes, real capture against `hook-harness`.
- **"Harness + Overlay"** — starts `hook-harness` under the debugger with the Overlay injected at launch; attach a second native debugger to step hook code.

**Toolchain gotchas, both hit on a real machine and both handled by `build.ps1`:**

- **msys2 / MinGW on `PATH` breaks the native build.** `vcvars64` *prepends* to
  whatever `PATH` it inherits, and MSVC ships `link.exe` — there is no `ld.exe`
  to shadow MinGW's. CMake then picks MinGW's linker and the build dies with
  `cannot find /nologo: No such file or directory`, a failure a long way from
  its cause. `build.ps1` strips only the MinGW entries before importing the
  MSVC environment, so `dotnet`, `git` and `cmake` survive.
- **`vcvars64` exports `Platform=x64`,** which is meaningful for `.vcxproj`
  builds. We have none, and MSBuild reads it as a *solution* platform, so
  `dotnet build FrameLedger.slnx` then fails with `MSB4126 solution
  configuration "Release|x64" is invalid`. `build.ps1` clears it after import;
  project-level x64 comes from `Directory.Build.props`.
- `ninja` and `clang-format` both ship inside the VS C++ workload but are on
  neither the default `PATH` nor the `vcvars` one. `build.ps1` locates both, so
  the C++ workload alone is enough to run every gate.

Native debugging notes: use **`hook-harness`, never a real game**, for step-through debugging — a breakpoint inside a present hook of a real game freezes it in ways anti-cheat and drivers both dislike. Enable the Vulkan validation layers when touching `FrameLedger.VkLayer`. Application Verifier + PageHeap on the harness catches ring-buffer bugs early.

Agent flags: `--serve`, `--console`, ~~`--diag`~~ (the App's — `FrameLedger.exe --diag`, `10_LOGGING`; the Agent answers exit 2 for it), `--install-task`, `--uninstall-task`, `--register-vklayer`, `--unregister-vklayer` (all four real since 2026-09-14, P3 PR-8b: the task is `\FrameLedger\FrameLedger.Agent` through the Task Scheduler COM API — `schtasks /Create /SC ONLOGON` is "Access is denied" unelevated on the dev box, measured — this user's logon trigger, interactive token, lowest run level; the layer registration is `17_HOOK_ENGINE` §Vulkan's HKCU value).

> **Built 2026-09-10 (P2 PR-F): `--serve` and `--console`; the other five answer "not implemented in P2"
> with exit 2** rather than being silently accepted (`--diag` is the App's anyway, `10_LOGGING`). The console
> surface, pinned by `AgentCommandLineSurfaceTests`: `consent list | grant --exe | revoke --exe`, `capture --exe
> [--seconds n]`, `launch --exe [--args "…"] [--seconds n]`, `recover`, `sessions [--last n]`, `db path`,
> `games add --exe`, `killswitch on | off | status`, and `--data-dir <dir>` — **under `--console` only**
> (HANDOFF §P2 decision D6): `--serve` never takes one, so the product's `%LOCALAPPDATA%\FrameLedger` is not
> selectable and an integration test cannot point the product at a profile. `AgentEndToEndTests` runs the
> shipped `FrameLedger.Agent.exe` against `hook-harness` with a scratch `--data-dir` and reads the one
> `sessions` row back — the milestone's technical half, in the merge gate.

> **The print verbs open the ledger read-only (2026-09-16).** `sessions`, `consent list` and `killswitch status`
> migrate nothing and refuse — exit 7, the reason on stderr — a ledger at a schema older or newer than the build;
> `db path` opens no file at all. Until then every verb went through the migrating open, and `sessions` run against
> the owner's ledger applied two scripts to it (`06_DATA_MODEL` §Migrations). `AgentReadOnlyVerbsTests` pins the set.

## Bundled assets

- ~~`assets/native/PresentMon.exe` (pinned, SHA-256 verified at build) for the Tier-2 fallback~~ — **DROPPED 2026-08-27, and there are no bundled native assets at all.** §S31 measured PresentMon classifying every frame of a ×4 capture as an application frame (row P2); the owner then dropped it outright. It is not bundled, not fetched, not used, and `tools/frametype-oracle.ps1` — the parser that consumed its output — is deleted with it. `assets/` does not exist and now has no reason to. **And since 2026-08-28 Tier 2 is not a measurement at all**: the ladder is two rungs, `EtwFrameSource` is deleted from the design, and whether a shipped build ever regains a no-injection measurement is `20_OPEN_QUESTIONS` §G.

  The struck text is kept because the *reason* it was written is still live. What it said next, and what remains true:

  > **planned, not present.** `assets/` does not exist, nothing fetches or verifies the binary, and `EtwFrameSource` is unwritten. `20_OPEN_QUESTIONS` §M2 (does the pinned console binary still exist, run unelevated, and emit the 2.x column set?) is unanswered, so this is not merely unpinned — there is nothing to pin yet.
- ~~Vulkan layer manifest, **written** with the installed layer path at install time (Velopack hook) — but **not registered there**.~~ **The install hook writes nothing (P4 PR-5):** the manifest is written by the registrar the first time the ledger asks for the layer (`VkLayerRegistrar.Register`, P4 PR-2), so an install that never enables a Vulkan game has no manifest either. Registration is a separate, later act.

### The Vulkan layer is not registered at install time

`20_OPEN_QUESTIONS` §S10. An implicit layer is **machine-wide by nature**: once
registered, the loader maps it into *every* Vulkan process on the system,
including ones the user never added to FrameLedger. Registering it as a side
effect of installation would put our DLL into unrelated Vulkan applications
before the user has enabled a single game — and before any consent dialog has
been shown, which contradicts FR-2.1 and `19_SAFETY` §User-facing consent.

The correct rule is the one `17_HOOK_ENGINE` §Vulkan already states, and it is
now the only rule: **registered only while at least one Vulkan game has hooking
enabled**, unregistered when the last such game is disabled, and unregistered on
uninstall. Under `HKCU` — never `HKLM`, never requiring admin.

This governs all three places that touched registration: the install hook above,
the `--register-vklayer` Agent flag (§Agent flags — a manual repair tool, not the
normal path), and the Settings button (`08_UI` §Settings — it reflects and
repairs state, it does not grant machine-wide reach on its own).

> **Enforced 2026-09-14 (P4 PR-2).** "A Vulkan game" is a static fact of the executable — it imports or
> `LoadLibrary`s `vulkan-1.dll` — read by the Agent's detection sweep into `capability_flags` as `vulkan`
> (`17_HOOK_ENGINE` §Vulkan built note). `Application.Vulkan.VkLayerReconciler` registers when the first
> hook-enabled game carries it and unregisters when the last one is disabled, after every consent change and
> after every sweep; the flag and the button run the same rule. The uninstall hook is still the updater's
> (P4, Velopack).

## Version

**One source: the `VERSION` file at the repository root** (`MAJOR.MINOR.PATCH`, nothing else — P4 PR-5,
2026-09-14). Before it, the managed assemblies carried the SDK's default `1.0.0` while CMake said `0.1.0`,
two products claiming to be one.

- `Directory.Build.props` reads it into `<Version>` (assembly, file and informational versions; SourceLink's
  `+sha` suffix is off so About, `Hello` and the bug bundle print the version a user can find on the releases
  page). The read is conditional on `$(Version)` being empty, so `release.yml` passes `-p:Version=0.1.0-beta.1`
  for a pre-release and the assemblies carry the tag's full version.
- `src/native/CMakeLists.txt` reads the same file into `project(VERSION …)`, which the `.rc` template turns
  into every VERSIONINFO block. CMake's `project()` accepts numbers only, so the native `FileVersion` is the
  numeric core even for a pre-release.
- `tools/versioninfo-check.ps1` compares every built binary's `FileVersion` against the file (`x.y.z.0`), so a
  read that silently produced `0.0.0` on either side is red in the gate.
- `release.yml` refuses a tag whose numeric core is not the file's. Bumping the version is one edit to
  `VERSION`, in the same commit as the changelog section, before the tag.

The Velopack package version is the tag's (`vpk pack --packVersion`), and `UpdateManager.CurrentVersion` reads
the installed package's manifest — not the assembly — which is why the two are made to agree here rather than
assumed to.

## Publish & package

```
cmake --build --preset x64-release
dotnet publish src/FrameLedger.App   -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o out/app
dotnet publish src/FrameLedger.Agent -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o out/app
vpk pack --packId FrameLedger.App --packVersion {ver} --packDir out/app --mainExe FrameLedger.exe --packTitle FrameLedger --releaseNotes out/notes.md --outputDir out/release
```

> **Run by `release.yml` since 2026-09-14 (P4 PR-5)** — `13_CI_CD` §release.yml carries the steps around it
> (the version gate, `{{RELEASE_DATE}}`, the published-tree assertion, checksums, the release). Both publishes
> take `-p:Version=<tag>` (§Version). No `--icon`: `assets/icon.ico` does not exist, and this line named it
> for six weeks.
>
> **The package id is `FrameLedger.App`, and this line said `FrameLedger` until 2026-09-14 — which would have
> deleted every user's ledger on uninstall.** Velopack installs into `%LOCALAPPDATA%\<packId>` and removes that
> directory when the app is uninstalled; `%LOCALAPPDATA%\FrameLedger` is the data folder (`LedgerPaths`, `07_IPC`,
> `legal/PRIVACY_POLICY.md` §1). With the same id the install would live inside the ledger's folder and the
> uninstaller would take `ledger.db` with it, without the question below. The title users see stays "FrameLedger"
> (`--packTitle`); the installer is `FrameLedger.App-win-Setup.exe`. `UninstallHook.AreSeparate` refuses the data
> delete if the two ever overlap again, so a future id change fails safe rather than silently.
>
> **Exactly two roots, and a gate now says so.** `src/FrameLedger.CaptureHost` — the unshipped
> capture host — is outside the package because neither root references it, and
> `tools/package-closure-check.ps1` is what keeps that true rather than remembered. It matters
> more than an ordinary layering rule: §S27 was closed on the grounds that no shipped binary
> carries an injecting entry point, and the host **is** one. A `ProjectReference` from either root
> would reopen §S27 silently, so the gate names the edge. Proven both directions — a reference
> from the Agent leaves the build green and turns the gate red.
>
> Solution membership is NOT the mechanism and must not be confused with it: `FrameLedger.slnx`
> is never passed to `dotnet publish`, and leaving the host out of it would exclude it from
> `dotnet build`, `dotnet format`, the analyzers and `dotnet test` — a far larger hole than the one
> it would appear to close.
>
> **What the gate does not see, said here rather than left to be discovered.** It resolves
> `<ProjectReference>` and nothing else — not `<Import Project="…targets">`, not
> `Directory.Build.props`. This repository's idiom for putting a foreign binary beside a project's
> output is exactly an imported `.targets` with a `<None … CopyToOutputDirectory>` item, so a
> `.targets` that staged `FrameLedger.CaptureHost.exe` into a publish root would be invisible to it.
> Narrower than it sounds — staging a binary is not referencing a project, the shipped assemblies
> would still hold no code path to it, and the copy would be a visible new `<Import>` in a root
> csproj — but it is a hole, and a gate that overstates its reach is the thing this one exists to
> prevent.

- No trimming (WPF + reflection), no NativeAOT (WPF unsupported), **no obfuscation** (GPLv3 policy, and `19_SAFETY` forbids making our binaries harder to identify).
- `SatelliteResourceLanguages=en;vi;ja`. ~~Expected package ≈ 95–130 MB self-contained.~~ **Measured 2026-09-14 (P4 PR-5): the published tree is 250 MB in 362 files** (App + Agent, self-contained, ReadyToRun roughly doubles the IL). `vpk pack` 1.2.0 over that tree, run by hand the same day with the workflow's arguments: `FrameLedger.App-win-Setup.exe` **104 MB**, the full `.nupkg` and the portable zip 100 MB each, plus `releases.win.json` / `RELEASES` / `assets.win.json`; vpk's own pre-process step verified `VelopackApp.Run()` in `FrameLedger.App.Program::Main()`.
- Velopack hooks (**built 2026-09-14, P4 PR-5**: `App/Update/VelopackHooks`, wired in the hand-written `Program.Main` before any window). **Install registers nothing** — the first-run flow is the offer (Agent setup), and the layer follows the ledger (§The Vulkan layer is not registered at install time); this line used to say "offer Agent setup + Vulkan layer registration", and the second half was the thing the section below forbids. Uninstall runs `UninstallHook`: `Shutdown` to a running Agent over the pipe (best effort, it holds the install directory), unregister the layer (HKCU), remove the logon task, then **ask** about the data folder — deleted on Yes only, never when it and the install overlap (`AreSeparate`), and a question unanswered inside Velopack's 30 s callback budget keeps it.

## Release-time token substitution

`{{RELEASE_DATE}}` in `legal/*.md` is the **only** placeholder that survives into
the repository, and it is deliberate: the effective date of a legal document is
the date it ships, which is not knowable at authoring time. `release.yml` (**built 2026-09-14**, P4 PR-5 — the step runs before the
build, because the App embeds `legal/*.md`)
substitutes it with the tag date when packaging, and `ci.yml` fails the build if
**any other** `{{` token appears in `README.md` or `legal/*.md` (`13_CI_CD.md`
§ci.yml). Everything else — repository URL, slug, developer identity, contact
address — is resolved in the source files, because an unsubstituted token in a
document the app displays for acceptance (FR-11) is a defect, not a template.

## Local quality gate (pre-push)

`./build.ps1 check`:
1. `cmake --build --preset x64-release` (C++ `/W4 /WX`)
2. native tests (Catch2)
3. `clang-format --dry-run -Werror` over `src/native`
4. `dotnet restore` + build (warnings as errors)
5. `dotnet format --verify-no-changes`
6. `dotnet test --logger trx` — including `ShmLayoutMirrorTests`; with no switches it also runs the
   `Category=Integration` classes, which CI excludes
7. `tools/coverage-gate.ps1` — reads this run's cobertura reports; self-arming, and armed today for
   `FrameLedger.Domain` and `FrameLedger.Application`
8. `tools/rules-validate.ps1` (schema + `anticheat` block sanity — a malformed or empty blocklist is a safety bug)
9. `tools/versioninfo-check.ps1` — reads the built binary, because what ships is what an anti-cheat vendor sees; since P4 PR-5 it also compares every `FileVersion` against the `VERSION` file (§Version)
10. `tools/chokepoint-check.ps1` — injection and evasion primitives confined to one file, native **and** managed, plus the `FL_GUARD_TESTABLE` symbol check against the shipped artifacts
11. **`tools/hookinventory-check.ps1`** — three passes: A resolves every vendor symbol the Overlay names against `docs/vendor-exports.json`, B sweeps for stray literals, C reads the BUILT Overlay's import table and asserts it imports no vendor module. C skips loudly under `-SkipNative`. **Missing from this list until 2026-08-28**, which is §R10 happening again
12. **`tools/package-closure-check.ps1`** — both halves: the self-test (5 cases, 4 of which must go RED) **and** a live pass over this repository. It walks the transitive `ProjectReference` closure of the two publish roots below and fails on anything outside the allowlist, naming the reference edge. `FrameLedger.CaptureHost` is an injecting entry point kept out of the package by construction, and `20_OPEN_QUESTIONS` §S27 is closed on exactly that basis
13. `tools/license-check.ps1` — asserts every vendored third-party has a licence copy in `legal/licenses/`, and that no Intel IGCL / AMD ADLX material has appeared in the tree; since P4 PR-6 (§3) also that `legal/licenses/nuget/` is exactly what `tools/license-gather.ps1` writes for this build's restore output — every NuGet package the App and the Agent ship (77), both directions. `license-gather.ps1 -SelfTest` runs first: fourteen fixture cases, five that must be RED (a hand edit, a package that no longer ships, a URL-only licence with no reviewed override, an applied MPL Exhibit B, a missing restore output), and one run under tr-TR in a child process, because the output must not depend on the machine's culture
13b. `tools/accuracy-check.ps1 -SelfTest`, then live — the accuracy block is ONE text (`legal/ACCURACY.md`) embedded verbatim in `README.md` and `legal/DISCLAIMER.md` §4; a copy that drifted, a copy with no markers, or an empty source is red. Four self-test cases, both directions (§S23-6, 2026-09-06)
14. `tools/changelog-check.ps1 -SelfTest` — nine cases, five expected RED. The live half needs a pull request's changed-file list and is supplied by `ci.yml`
14b. `tools/release-notes.ps1 -SelfTest` — five cases over a fixture changelog (a dated and an undated section, a missing one, a prefix that must not match, an empty one). The live half needs a tag's version and runs in `release.yml`, where a missing or empty section is a red release (P4 PR-5)
15. `tools/resx-audit.ps1` — ~~**skipped loudly; it does not exist, and no `.resx` file does either**~~ **built 2026-09-13 (P3 PR-2)**: `-SelfTest` first (11 fixture cases, both directions), then the live pass over every `Strings.resx` family under `src/` — key sets equal across en/vi/ja, `Safety_*` in `ja` marked for review or signed, `Strings.Designer.cs` current with `tools/resx-gen.ps1`. A tree with no family is red (`09_I18N` §Translation workflow)
16. **struct-mirror** — reads this run's `.trx` and fails when `ShmLayoutMirrorTests` did not execute, so deleting the mirror test is red as well as breaking it
16b. **`tools/test-artifacts-check.ps1`** — `-SelfTest` first (seven cases, two RED), then live: an `overlay-*.log` created since this gate started, still in the real `%LOCALAPPDATA%\FrameLedger\logs` after the tests, whose first line names a `hook-harness*` image, fails the gate. It is what makes a test binary without its log sweep red (`17_HOOK_ENGINE` §Native logging, 2026-09-15); a game's log never counts
17. Placeholder guard — fails if any `{{` token other than `{{RELEASE_DATE}}` survives in `README.md` or `legal/*.md`

> **This list was wrong in two ways until 2026-08-06 and both were the same kind of wrong.** Step 6
> said the struct-mirror check *"does not exist; `build.ps1` declares and skips it loudly"* — while
> line 52 of this same document, and `build.ps1` itself, had it as a hard throwing gate since
> 2026-08-05. And the list omitted four gates the script actually runs: `coverage-gate`,
> `versioninfo-check`, `chokepoint-check` and `changelog-check`. A document whose job is to be the
> list of what `check` does was missing 40% of it, which is worse than having no list: a reader
> plans against it. `13_CI_CD.md` repeats the struct-mirror claim and is corrected with it.

CI runs the identical script (`13_CI_CD`) with no switches since 2026-09-06 — `-SkipIntegration` was
CI's from 2026-08-05 until §S19(b)'s signer half removed the refusal behind it — so a green CI is now
evidence for the managed drain too, and `./build.ps1 check` with no switches is what a developer runs
before pushing.

**Gates skip loudly.** A gate whose tool is not installed (no `cl.exe`) or not
yet written prints `SKIPPED`, is listed again in the summary, and the run ends
with `PASSED WITH N SKIPPED GATE(S)` rather than a clean `ALL GATES PASSED`.
A gate that silently passes because it did nothing is worse than no gate: it
reads as "checked" in CI output when nothing was checked.

**Gates are proven red-green, not just green.** `license-check` and
`rules-validate` were each verified to fail on a planted violation — an IGCL
header dropped into the tree, and an emptied `anticheat.modules` list — before
being wired in. A safety gate that has only ever been observed passing has not
been tested.
