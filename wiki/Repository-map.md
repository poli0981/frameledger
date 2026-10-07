# Repository map

The layout with its history is in [`CLAUDE.md`](../CLAUDE.md) §Solution layout.

## Managed code (`src/`, .NET 10, C#)

| Project | What it holds |
|---|---|
| `src/FrameLedger.Domain` | Entities and the metric calculators. References nothing. |
| `src/FrameLedger.Application` | Use cases and ports: the capture loop, the session recorder, settings, the safety decisions. |
| `src/FrameLedger.Infrastructure` | SQLite, the shared-memory reader, telemetry, the native interop, parsers. The **only** project that calls native code. |
| `src/FrameLedger.Shared` | The pipe's message contracts and the C# mirror of the shared-memory structs. |
| `src/FrameLedger.Agent` | The capture agent's host: the watcher, the pipe server, the recorder. |
| `src/FrameLedger.App` | The WPF app (WPF UI 4.3.0): pages, dialogs, charts, strings in English, Vietnamese and Japanese. |
| `src/FrameLedger.CaptureHost` | A developer tool, never shipped. |

Dependencies point inwards: App and Agent → Application → Domain; Infrastructure implements Application's ports.

## Native code (`src/native/`, C++20, MSVC)

| Folder | What it holds |
|---|---|
| `src/native/FrameLedger.Overlay` | The component loaded into a game: hooks, the ring writer. |
| `src/native/FrameLedger.Injector` | The anti-cheat guard and injection, built into `FrameLedger.Guard.dll` for the agent. |
| `src/native/FrameLedger.VkLayer` | The Vulkan implicit layer and its manifest. |
| `src/native/FrameLedger.Shm` | The shared-memory layout, header-only; `fl_shm.h` is normative. |
| `src/native/FrameLedger.NvapiBridge` | NVIDIA telemetry, loaded by the agent. |
| `src/native/FrameLedger.ProcessStats` | The game's own memory, read from the agent. |
| `src/native/tests` | The native test suites (ctest). |
| `src/native/tools` | `hook-harness` (a dummy Direct3D 11/12, Vulkan and OpenGL app to hook), the probes, `fl-layout-dump`. |
| `src/native/third_party` | Vendored vendor headers: NVAPI, Streamline, FidelityFX, Vulkan. |

## Everything else

| Path | What it holds |
|---|---|
| `tests/` | One test project per managed project, plus shared helpers and fixtures. |
| `tools/` | The PowerShell gates and generators `./build.ps1` runs — see [Build and test](Build-and-test.md). |
| `rules/detection-rules.json` | Engine, platform and capability signatures, and the anti-cheat blocklist. |
| `guide/` | The user guide — embedded in the App, and the End-user half of this wiki. |
| `wiki/` | This wiki's own pages, published to the Wiki tab when a release is made. |
| `docs/` | The developer documents; [`docs/README.md`](../docs/README.md) is their index. |
| `legal/` | The documents a user accepts, the accuracy statement, licences and notices. |
| `.github/workflows/` | CI, CodeQL, the rules gate, the release and the wiki. |
| `LIMITATIONS.md` | What the whole software cannot do, in a player's words; also in the App. |
