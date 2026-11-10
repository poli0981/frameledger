## 29 · C# 15 against the checks — the canary, and where its constructs would go *(2026-11-11, beta.37, owner decision D96)*

**Why this was measured.** The owner asked for C# 15 on the main line in the same release that moved it to .NET 11 (D96).
The plan's rule, approved with it: a construct is used only where (1) the build, its analyzers and `dotnet format` take
it without an analyzer being switched off, (2) CodeQL extracts its file without an error, and (3) Qodana does not report
errors that are only its engine not knowing the syntax. So each construct was written once, in a file of its own, and
shown to each tool before any product code used it.

**The canary** (branch `probe/csharp15-canary`, draft pull request #377, closed unmerged): four files under
`tests/FrameLedger.Domain.Tests/Canary/`, one construct each, each with a test that runs —

```csharp
HashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), "a", "A"];      // collection expression arguments
private closed record State;  private sealed record Idle : State;              // a closed hierarchy, switched over
private union Pet(Cat, Dog);                                                    // a union, switched over
outer: foreach (int[] row in rows) { foreach (int cell in row) { if (cell < 0) { continue outer; } } }   // a labeled jump
```

**What each tool made of them.**

| Construct | SDK 11.0.100's compiler, its analyzers (`latest-all`), Roslynator 5.0.1, Meziantou 3.0.319, VS Threading 18.12.1 | `dotnet format` | ReSharper's engine: InspectCode 2026.2.3.1 (what Qodana 2026.2 runs) | InspectCode 2026.3.0 (the newest released) |
|---|---|---|---|---|
| Collection expression arguments, `[with(…), …]` | builds, 0 warnings | clean | **error** — "Feature 'collection expression arguments' is not available. Please use language version 'preview'." | the same error |
| Closed hierarchy, `closed record` | builds, 0 warnings; the switch needs no default arm | clean | **not parsed** — "Cannot resolve symbol 'closed'", "Unexpected token", then CS8509 on the switch it reads as open | the same |
| Union, `union Pet(Cat, Dog)` | builds **only with CA1815 silenced**: the SDK's own rule asks every union — a struct — to override `Equals` and the equality operators | clean | **not parsed** — "Cannot resolve symbol 'union'", "Identifier expected" | the same |
| Labeled `continue` / `break` | builds, 0 warnings | **moves the label one indent to the left** (`csharp_indent_labels`), the statement staying on its line | **not parsed** — "; expected", "Cannot resolve symbol 'outer'", and the loop's tail read as unreachable | **error** — "Feature 'labeled break and continue' is not available. Please use language version 'preview'." |

The four tests passed. No analyzer threw on a syntax node it did not know (an `AD0001` would have been an error here).
InspectCode was run on the dev box with `--toolset-path` at SDK 11.0.100's `MSBuild.dll`, a fresh caches folder each
time; 2026.2.3.1 reported 29 results in the four files, 2026.3.0 24.

**CodeQL (2.27.4): not established, and why a pull request cannot establish it.** `analyze (csharp, none)` was green on
the canary's pull request in 3 min 10 s, and its log says "Reading overlay changes": on a pull request the C# analysis is
an **overlay** — it extracts the files the pull request changed on top of the database cached from `main` — and its log
prints no per-file extraction result. "CodeQL scanned 1220 out of 1220 C# files" is `main`'s baseline figure, printed
unchanged on a pull request that had added eight files. The SARIF the code-scanning API returns carries no extraction
notification. CodeQL's documentation lists C# up to 14. So this run neither shows that CodeQL reads these constructs nor
that it does not; a full analysis needs a run that is not a pull request's (`codeql.yml` takes `workflow_dispatch`
since this pull request, for that).

**The conclusion, by the rule.** No C# 15 construct is used on the main line in 0.1.0-beta.37: every one of the four
fails condition (3) — Qodana's engine would report errors in each file that used one, on a gate of zero — and the union
fails (1) as well. What the main line takes from .NET 11 in this release is **API, not syntax**: the
`string.StartsWith` / `EndsWith` / `LastIndexOf(char, StringComparison)` overloads (sixteen call sites that §24's rule had
written as spans, or that §28 fixed), and `Process.TryGetProcessById` in place of a caught `ArgumentException`.

**What unblocks each, and where it would go** — the survey of 2026-11-11 over the thirteen projects, kept so the next
attempt starts from it:

| Construct | Waits for | Where it would go |
|---|---|---|
| `[with(comparer), …]` | a ReSharper engine that reads C# 15 as released, in the Qodana linter | the seven sites §24 wrote with explicit `StringComparer.Ordinal` constructors for MA0002, and about fifteen more local `HashSet`/`SortedSet` constructions (`GameFileProbe`, `CaptureOrchestrator`, `LibraryImporter`, `GameTitleGuess`, …). Not the `IReadOnlySet`-typed statics (not a collection-expression target) nor `ConcurrentDictionary` |
| `closed` | the same; and CodeQL's reading, measured by a dispatched run | `Application/Watch/WatchEvent` (the one abstract record in `src/`); and, as closed record hierarchies rather than unions, the hand-written "one of" shapes: `Domain/Detection/EngineMatch` (with `RuleEvaluator.MatchPlatform`), `Application/Recording/FgVerdict` with the identity and the withheld reason it travels with, `Application/Telemetry/GameProcess`, the Agent's private `ElevationDecision` |
| `union` | the same, **and** CA1815 settled — a union that must hand-write its equality is not shorter than what it replaces | only a value that is one of several *existing* types: `IRingAttacher.TryAttach`'s `(ICaptureSink?, ShmAttachRefusal)` |
| Labeled `continue` | the same | one site: `Infrastructure/Detection/InstallRoot.cs`, the `hit` flag |
| Extension indexers | — | nowhere: the six extension methods in `src/` are not index-like |

**Left as they are, whatever the tools read:** the enums whose names are stored or cross a wire
(`SessionEndReason`, `AntiCheatRefusalReason`, `ShmAttachRefusal`, `Tri`), and `AntiCheatVerdict` (it mirrors a native
result, and "zeroed means not evaluated" is a rule of the guard). And the two preview features — runtime-async and the
updated memory-safety rules — which D76's "no preview feature" still excludes.
