# Third-party licence texts

Full licence texts for everything FrameLedger bundles or links. The App's publish carries this directory as
`licenses/` beside the executables; `release.yml` adds `licenses/dotnet/` (the .NET runtime's licence and
third-party notices, from the runtime packs that publish used).

| Path | What it is | Who writes it |
|---|---|---|
| `nuget/` | One text per NuGet package the App and the Agent ship (77 on 2026-09-15), the distinct third-party notice files those packages carry (`notices-*.txt`), and `INDEX.md` | **Generated** by `tools/license-gather.ps1` from the build's restore output — never edited by hand; `tools/license-check.ps1` §3 fails when it differs |
| `apache-2.0.txt` | The Apache-2.0 text: the Vulkan headers, and every Apache-2.0 package in `nuget/` that ships no licence file of its own | Hand-committed |
| `mpl-2.0.txt` | The MPL-2.0 text: LibreHardwareMonitorLib and its three MPL-2.0 dependencies (their packages ship no licence file, so distributing it is our obligation, §3.1) | Hand-committed; `license-check` §2b requires it |
| `minhook-BSD-2-Clause.txt` | MinHook — vendored and built from source | Hand-committed; §2 |
| `nvapi-MIT.txt` | NVIDIA NVAPI SDK — headers + import library vendored | Hand-committed; §2 |
| `streamline-MIT.txt`, `fidelityfx-MIT.txt` | NVIDIA Streamline headers; AMD FidelityFX headers (2.x and 3.0 host) | Hand-committed; §2b, §2d, §2e |
| `jsmn-MIT.txt`, `catch2-BSL-1.0.txt` | jsmn (built into the Overlay); Catch2 (tests only) | Hand-committed; §2 |

`wpfui-MIT.txt` (2026-09-14) and `velopack-MIT.txt` (2026-09-14) were hand copies of texts the generator now
writes as `nuget/WPF-UI.txt` and `nuget/Velopack.txt`; they were removed with P4 PR-6 so each package has one
source. ~~`librehardwaremonitor-MPL-2.0.txt`~~ and ~~`mit.txt`~~ were planned names in this file that never
existed: the MPL text is `mpl-2.0.txt`, and MIT is written per package because its notice names the holder.

~~Two of these are release-blocking questions, not paperwork~~ — **both came back clean on 2026-08-02**
(`docs/spike-notes.md` §M3, §M4; `docs/20_OPEN_QUESTIONS.md` records them resolved):

- **NVAPI** — the vendored artifact, `nvapi64.lib` included, is MIT.
- **LibreHardwareMonitor** — no depended-upon file applies MPL-2.0 **Exhibit B**, so the GPL-compatibility route
  holds. The three MPL-2.0 packages LHM 0.9.6 pulls in (BlackSharp.Core, DiskInfoToolkit, RAMSPDToolkit-NDD) were
  not part of that search; they were checked the same way on 2026-09-15 and are recorded in
  `legal/THIRD_PARTY_NOTICES.md`. A version bump of any of them is a new search, not an inherited answer.
