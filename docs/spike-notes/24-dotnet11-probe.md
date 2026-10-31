## 24 · The whole gate on .NET 11 RC3 — what the alpha line's SDK asks of the source *(2026-10-31, beta.32, owner decisions D73–D76)*

**Why this was measured.** The alpha line (`12_BUILD` §The alpha line) builds this source on .NET 11. Before its switch
was written, the switch was applied in a throwaway worktree (branch `probe/net11`, never pushed) and `./build.ps1 check`
run there, to learn what the 11 SDK — its compiler, its analyzers, its runtime — asks of the source, and which of those
asks .NET 10 can also take (those land on main; the rest would be the alpha branch's).

**Where.** The dev box of §Environment (Windows 11 Pro build 29648, i7-14700KF, RTX 5080). SDK
`11.0.100-rc.3.26522.148`, runtime and Windows Desktop `11.0.0-rc.3.26522.148` (the out-of-band security release of
2026-10-25), already installed beside the 10.0 SDKs. The switch: `global.json` → that SDK with `allowPrerelease`;
`Directory.Build.props` → `net11.0-windows10.0.22621.0`; `Microsoft.Extensions.Hosting`, `Microsoft.Data.Sqlite`,
`System.Diagnostics.EventLog` → `11.0.0-rc.3.26522.148`. Nothing else.

**The decision table, written before the run:**

| Finding | Where it is fixed |
|---|---|
| A new analyzer diagnostic whose fix compiles as C# 14 and stays green on .NET 10 | main |
| A new diagnostic only C# 15 syntax or a .NET 11 API can satisfy | the alpha branch: that one rule off in its `.editorconfig`, with the reason |
| NU1510, the package graph, a licence text | the alpha branch |
| A compiler error from C# 15's breaking changes (`with(` in a collection expression, a type named `closed` or `union`, a span collection expression's scope) | main, rewritten so both compile |
| A test whose answer changes only on .NET 11 (`Math.Round`, saturating casts, `Environment.TickCount`, DI's disposal, ZIP CRC) | product code that relied on the old behaviour → main, so both runtimes give the same number; a test's own expectation → main, made robust; each case recorded here |
| `IHost.RunAsync` throwing for a failed `BackgroundService` | accepted on the alpha line; changed only if a test pins the old exit |
| A timing-sensitive test red | re-run once and named; red again → investigated as a defect |
| The coverage gate below its floor only on .NET 11 | investigated; the floor is not lowered |
| Native code | unchanged — MSVC does not see .NET; any difference is a defect |

**What the run found:**

| Step | Result |
|---|---|
| Native build and the 32 `ctest` suites | green, 32 of 32 |
| clang-format | clean |
| Managed build, warnings as errors | **red in four rounds, 21 sites in 14 files — every one fixable in a form .NET 10 also compiles** (below) |
| `dotnet format` | clean |
| Tests | **every suite green**: Domain 422, Application 553, Infrastructure 518 of 520, App 719, CaptureHost 137, Agent 29. The two Infrastructure cases not run are the two that need an administrator's token — the same two, skipped the same way, in the last .NET 10 run on this box |
| coverage-gate | OK — Domain 93.8 %, Application 97.4 %, 32 metric classes at or above 95 % |
| rules-validate, versioninfo-check, chokepoint-check, hookinventory-check, package-closure | OK |
| license-check | red, as it must be: `legal/licenses/nuget/` is the beta's package set (the nine Extensions abstractions now in the runtime, SQLitePCLRaw 3.0.5 and its engine package instead of 2.1.13). Regenerated on the alpha branch, never on main |

**The 21 sites** (`git diff` of this PR is the whole list):

- **CA1307, 13 sites.** .NET 11 adds `string.LastIndexOf(char, StringComparison)` and
  `StartsWith`/`EndsWith(char, StringComparison)`, so the analyzer now asks for a comparison on every
  `LastIndexOf(char)`, `StartsWith(char)` and `EndsWith(char)` (four, six and three of them, three in tests). Those
  overloads do not exist on .NET 10. The span forms — `AsSpan().LastIndexOf(c)`, `AsSpan().StartsWith(c)`,
  `AsSpan().EndsWith(c)` (`MemoryExtensions`, present in the 10.0 reference pack) — compare ordinally, as the char forms
  did, and satisfy both. One of the three in tests sits in a FluentAssertions expression tree, where a span cannot appear
  (CS8640): it reads `m.Length > 0 && m[m.Length - 1] == ' '`.
- **MA0002, 7 sites.** C# 15 lets a collection expression pass a comparer (`[with(StringComparer.Ordinal), …]`), and
  Meziantou.Analyzer 3.0.317 now flags a `HashSet<string>` or `Dictionary<string, …>` that `[]` builds without one. C# 14
  has no `with(...)`; the constructor given `StringComparer.Ordinal` — what `[]` used, the default comparer of `string` —
  compiles on both and changes no answer.
- **CA2026, 1 site** (a rule the 11 SDK's analyzers add): `JsonDocument.Parse(…).RootElement` leaves the document
  undisposed; `JsonElement.Parse`, in .NET 10 too, does not (a test's payload).

**What did not happen**, against the table: no compiler error from C# 15's breaking changes; no NU1510 (the repository
references none of the nine `Microsoft.Extensions.*` libraries .NET 11 moved into the shared framework — `Hosting`, the
one it references, stays a package); no test changed its answer, so `Math.Round`'s and the decimal conversions' corrected
rounding, the saturating float-to-small-integer casts, `Environment.TickCount`, DI's aggregated disposal and the ZIP CRC
check moved no expectation of this suite; no test pins the Agent's exit after a failed `BackgroundService`, so .NET 11's
rethrow from `IHost.RunAsync` — the Agent's crash path, with its minidump, where .NET 10 exited 0 — is the alpha line's
behaviour; no timing flake in the one run.

**Not measured here:** an installed alpha on real hardware with a real title, and an alpha updating to the next alpha —
the owner's run of the first alpha.
