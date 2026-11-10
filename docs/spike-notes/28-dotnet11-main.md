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
