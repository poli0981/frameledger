# Build and test

The full documents are [`docs/12_BUILD.md`](../docs/12_BUILD.md) and [`docs/14_TESTING.md`](../docs/14_TESTING.md).

## What you need

- Windows 10 (22H2) or 11, 64-bit.
- Visual Studio with the C++ workload (MSVC v143 or later, a Windows SDK of 22621 or later) and CMake 3.28 or later.
- The .NET SDK that [`global.json`](../global.json) pins.
- PowerShell 7.

## The one gate

```powershell
./build.ps1 check
```

It is the whole gate, and CI runs the same script. In order:

| Step | What fails it |
|---|---|
| Native build and ctest | A C++ warning (`/W4 /WX`) or a native test. |
| clang-format, `dotnet format` | Code not formatted as committed. |
| Managed build and tests | A C# warning (warnings are errors), or a test — the integration tests included. |
| coverage-gate | Too little coverage of Domain and Application. |
| rules-validate | A detection-rules file that breaks its schema or weakens the anti-cheat blocklist. |
| versioninfo, chokepoint, hookinventory, package-closure | A binary without its version resources; an injection anywhere but the guard's own code, or an evasion primitive anywhere; a hook not in the inventory; a developer tool in the package. |
| license-check, accuracy-check, notice-check | A package without its licence, a copy of the accuracy statement that drifted, a source file without its licence header. |
| changelog-check, release-notes, resx-audit | The changelog or the release-notes extractor broken, or a string missing in English, Vietnamese or Japanese. |
| struct-mirror, test-artifacts, wiki-check, placeholder guard | The shared-memory mirror test not run, test leftovers, a wiki link that leads nowhere, a stray `{{…}}` in a shipped document. |

Other tasks: `./build.ps1 native`, `./build.ps1 managed`, `./build.ps1 format` (writes the formatting). A skipped gate is
reported, never hidden.

## Tests

- **Managed:** one project per layer under `tests/` (xUnit v3, FluentAssertions). The App's tests render every page,
  window and dialog under the real theme dictionaries.
- **Native:** ctest suites for the guard, the ring, the layout, the Vulkan layer, the telemetry DLLs.
- **Integration:** the hook-harness is hooked for real, on WARP, so it runs on CI without a GPU.
- **A new test is shown red first** on the unfixed code: a check that has never failed has not been seen to work.

## Looking at the App with real data

`FrameLedger.exe --data-dir <a copy of a data folder>` opens the App as a viewer: no agent is started or contacted, and
nothing on the PC is changed. **Never run a development build against your own `%LOCALAPPDATA%\FrameLedger`**: a newer
build upgrades the database, and an older one cannot open it again.
