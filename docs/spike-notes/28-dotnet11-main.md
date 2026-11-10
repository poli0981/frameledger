## 28 · The main line on .NET 11's release — the whole gate, and what the analyses read *(2026-11-11, beta.37, owner decisions D94–D96)*

**Why this was measured.** The owner moved the main line to .NET 11 the day after it shipped (D94), setting aside the
condition `20_OPEN_QUESTIONS` §G had written for it — two alphas on the released runtime. §24 measured Release Candidate
3 on a throwaway branch; nothing had measured the release, and three things had never met .NET 11 at all: CodeQL's C#
analysis (a required check, main-only), SonarCloud's scanner, and a Velopack delta across two runtime majors. This note
is what stood in for the condition that was set aside.

**Where.** The dev box of §Environment (Windows 11 Pro build 29648, i7-14700KF, RTX 5080). SDK `11.0.100`, runtime and
Windows Desktop `11.0.0` (released 2026-11-10), installed beside the 10.0 SDKs. The hosted runner: image
`windows-2025-vs2026`, Visual Studio 18 Enterprise, MSVC 14.51.

**The switch, on main this time** (branch `beta37/pr1-net11`): `global.json` → `11.0.100`, `allowPrerelease` false;
`Directory.Build.props` → `net11.0-windows10.0.22621.0`; `Microsoft.Extensions.Hosting`, `Microsoft.Data.Sqlite`,
`System.Diagnostics.EventLog` → `11.0.0`; `legal/licenses/nuget/` regenerated (78 packages → 69, the alpha's set at
released versions). Nothing else in the build.

**What the release asked of the source that RC3 had not.** Two sites, both in tests written after the last alpha was
cut (beta.35's `ContrastThemeTests.Drawn.cs`): CA1307 on `string.StartsWith(char)` and `string.EndsWith(char)`, which
.NET 11 gives a `StringComparison` overload. They take that overload now — the main line compiles against .NET 11, so
§24's rule (a form .NET 10 also compiles) no longer binds. The 21 sites §24 fixed stayed quiet.

**`IHost.RunAsync`, observed for the first time.** §24 read Microsoft's note that a failed `BackgroundService` now fails
`RunAsync` and recorded that no test pinned it. `HostBackgroundFailureTests` does: a host with one service that throws
fails `RunAsync` with that exception on 11.0.0. The Agent's `--serve` therefore leaves through its crash report
(`10_LOGGING` §Crash handling) where it exited 0 on .NET 10; it was accepted, not wrapped.

**The gate, on the dev box (`./build.ps1 check`, 338 s, all gates passed).** Native: 32 of 32 suites. Managed: the build
with warnings as errors, 0 warnings; `dotnet format` clean; 2,561 tests passed and 2 skipped in the six projects (Domain
429, Infrastructure 578 and the 2 skipped, Application 568, CaptureHost 137 with its end-to-end cases, App 816, Agent
33), every one on `net11.0`; the coverage floors held (12 reports); `license-check` green on the regenerated texts. No
test changed its answer between .NET 10 and .NET 11.

**The pull request's own runs (#376, 2026-11-11), which were the first of each on .NET 11:**

| Run | What it showed |
|---|---|
| `check` (42916490997) | green in 12 min 22 s on the hosted runner, SDK 11.0.100 installed by `setup-dotnet` from `global.json` |
| `analyze (csharp, none)` (42916491104, CodeQL 2.27.4) | green in 4 min 5 s. Build mode `none` used the runner's 11.0.100 SDK for references (on .NET 10 it downloaded 10.0.100 itself): 1,221 source files found, 363 references resolved and none unresolved, 9 types unresolved (main's last run on .NET 10: 23 in three `Windows.Win32` namespaces), and "CodeQL scanned 1220 out of 1220 C# files" — the same line as main's last run. CodeQL's documentation lists C# up to 14 and .NET up to 10; `net11.0` with source that uses no C# 15 construct is read whole |
| `analyze (cpp, manual)` | green, 7 min 7 s — the native layer did not change |
| `sonarcloud` | green, the gate passed: the SonarScanner for .NET 12.0.0 (a `netcoreapp3.1` tool that rolls forward to the newest major) ran on the 11.0 runtime |
| Qodana (42916491047), the Community linter with the interim | the `bootstrap` line installed SDK 11.0.100 into the container, InspectCode 2026.2.3.1 loaded all thirteen `net11.0-windows` projects, and it reported **1 problem**: a redundant `!` in the new test, fixed in the same pull request |
| `release.yml` rehearsal on the branch (42916488083, `0.1.0-beta.37`) | green. `setup-dotnet` installed SDK 11.0.100 and, for `vpk` 1.2.161, the .NET 10 line (SDK 10.0.404, runtime 10.0.15). **The first delta across two runtime majors**: "Delta processed 0443 files. 0372 patched, 0071 unchanged, 0009 new, 0017 removed" — 72,604,898 bytes, against a full package of 110,314,263 (beta.36's: 107,902,117) and a `Setup.exe` of 117,828,375 |

**Not measured here.** An installed build on real hardware and a hooked title on .NET 11.0.0 (owner-only item 34);
Windows 10 22H2, which no version of FrameLedger has been run on; a processor without x86-64-v2.
