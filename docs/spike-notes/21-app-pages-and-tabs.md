*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 21 · Opening pages and switching tabs in the App *(2026-10-14, beta.23)*

The owner asked whether switching pages and tabs is slow. Measured with a harness over the App's own pages and view
models — not committed: it times, and a timing assertion in CI is a flake — on the dev PC (i7-14700KF, RTX 5080, Windows
11 29648), Release build, each page shown in WPF UI's `NavigationViewContentPresenter` in a real, off-screen window. The
ledger is a scratch one shaped like the owner's (its counts read from the owner's ledger without writing to it): 48 games, one with 100 sessions (NFR-4's
"opening a game with 100 sessions ≤ 500 ms"), one with a 2,000,000-frame session (the owner's longest: WALTO, 2,057,022
frames, with noisy frame times so a sort is a real sort). **Freeze** is the longest stretch the UI thread did not answer
(a probe re-posted at idle priority); median of 10 runs, 5 for the selection; *before* is two runs of `main` (2982679).

**The decision table, written before the first run:** fix what keeps the UI thread busy 50 ms or more, what can block
on I/O, and what grows with each visit; record the rest.

| What | Freeze before | Freeze after | Why it was slow |
|---|---|---|---|
| Open a game with 100 sessions | 1,175–1,507 ms | 369 ms | the Sessions grid inside the page's scroller had no height limit and built every row: 100 rows of 17 columns |
| Choose the 2,000,000-frame session | 843–1,016 ms | 17 ms | the blob read, its decode and the stutter detection ran on the UI thread (Microsoft.Data.Sqlite's async is synchronous), then three sorts of every frame for the Distribution tab (its histogram, its percentile curve, the curve's lows) and two walks for the Frametime chart — for tabs not even shown. The series is ready 0.85 s after the click either way; the window answers meanwhile now |
| A session ends after 20 visits to the Dashboard | 46–60 ms, **20 notices** | 18 ms, 1 notice | every Dashboard ever opened stayed alive (a transient `IDisposable` the root container keeps) and subscribed: each reloaded its lists and raised FR-3.8's notice |
| Open the library (48 games) | 265–404 ms | 275 ms | not fixed — below |
| Open a game with 9 sessions | 148–203 ms | 210 ms | building ≈ 1,750 visuals, the grid's 9 rows ≈ 130 each |
| Open Compare | 66–82 ms | 87 ms | building its visuals |
| Open Settings | 75–99 ms | 85 ms | building its visuals |
| Open the Dashboard | 48–61 ms | 46 ms | — |
| Open Logs | 16–23 ms | 23 ms | — |
| Switch a game page's tab (Frametime, Distribution, Trend, Sensors, Latency, back) | 1–25 ms | 1–24 ms | already cheap: a hidden tab is not in the visual tree, and its plot renders once when shown |

Also changed, and held by tests rather than by these numbers: the game page asks whether its executable is on disk off
the UI thread (a USB drive that sleeps answered on it — 39 of the owner's 41 Steam games are on one); the Logs page's
two-second tick reads nothing when the newest file has not changed (it re-read up to 2 MB and replaced the whole text);
Play reads the stores' records on the thread pool (every press listed Steam's manifests and copied itch's database on
the UI thread); the game page no longer summarises every game's sessions to show one.

**Above the bar, and not fixed.** A page that builds 600–2,500 visuals costs 85–370 ms whatever it reads — about 0.1 ms a
visual on this PC — and the remaining cost of the game page is its grid's rows (≈ 130 visuals each). The library is the
one that grows with the data: 275 ms at 48 games, about 5.7 ms a card, so about 0.6 s at 100. Its grid is a
non-virtualizing panel by decision (beta.11, `Controls/UniformWrapPanel`: cards an equal share of the width, each row as
tall as its tallest card); a virtualizing one builds only the cards on screen but makes every card one height and the
grid scroll under a fixed header — a change to how the page looks, offered to the owner instead of made here. *(The
owner's answer, 2026-10-15: the library stays as it is.)* Nor was a
schema migration added for an index on `sessions.started_at` (the Dashboard sorts the table): at the owner's 41 sessions
it costs nothing measurable, and a new schema version locks the ledger out of every earlier build.
