# Developers

FrameLedger is a C++ component loaded into the games a user chose, a C# capture agent beside it, and a WPF app, with one
SQLite file between them. These pages are short on purpose: each says what a part is and where its full document is.
The full set is [`docs/`](../docs/README.md), and the rules every AI-assisted change follows are in
[`CLAUDE.md`](../CLAUDE.md).

## Start here

1. [Contributor rules](Contributor-rules.md) — the rules every change keeps. Safety comes first, and some changes are
   refused whatever they improve.
2. [Architecture](Architecture.md), then [Repository map](Repository-map.md).
3. [Build and test](Build-and-test.md) — `./build.ps1 check` is the whole gate, the same one CI runs.

Picking up work where it stopped: [`docs/HANDOFF.md`](../docs/HANDOFF.md) has the sequencing, the owner's decisions
and the traps that each cost a cycle.

## Where status lives

These pages never say what is done or what is next. Status is in four files, and nowhere else:

| Question | File |
|---|---|
| What is unresolved, and what does it block? | [`docs/20_OPEN_QUESTIONS.md`](../docs/20_OPEN_QUESTIONS.md) |
| What was measured, and on what machine? | [`docs/spike-notes.md`](../docs/spike-notes.md) |
| Which phase is where? | [`docs/15_ROADMAP.md`](../docs/15_ROADMAP.md) |
| What landed, in which release? | [`CHANGELOG.md`](../CHANGELOG.md) |

Check a status claim against the code before you plan on it: more than one document has said "built" of something that
was not, and the repository corrects such sentences where they stand — struck through and dated — rather than quietly.

## The other pages

- [Capture pipeline](Capture-pipeline.md) — what the agent does from the moment a game starts.
- [Metrics](Metrics.md) — what each number means, and the rules for showing it.
- [Data and IPC](Data-and-IPC.md) — the database, the shared memory and the pipe.
- [App UI development](App-UI-development.md) — the WPF app, and the traps of its UI library.
- [Releasing](Releasing.md) — from a merged change to an update on a user's PC.
- [Developer glossary](Developer-glossary.md) — the project's own words.

Contributing is described in [`CONTRIBUTING.md`](../CONTRIBUTING.md), redistributing in [`FORKING.md`](../FORKING.md),
and how to report a vulnerability in [`SECURITY.md`](../SECURITY.md).
