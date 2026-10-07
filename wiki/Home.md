# FrameLedger

**FrameLedger** records how your games run on this PC — frame rate and frame times, the settings a game really renders
with, ray tracing, temperatures, and the game's own memory — and keeps every session so you can compare them later.
Everything stays on your PC: no account, no telemetry. It is free software under the GNU GPL v3, for 64-bit Windows 10
(22H2) and Windows 11, and it is in beta.

Download it from the [Releases page](https://github.com/poli0981/frameledger/releases).

## Use FrameLedger

The [user guide](../guide/README.md) is the one FrameLedger shows under Help ▸ User guide.

1. [Install and first start](../guide/01-install.md)
2. [Record a game](../guide/02-record-a-game.md)
3. [Read your results](../guide/03-read-your-results.md)
4. [Anti-cheat and safety](../guide/04-anti-cheat-and-safety.md) — read it before you switch hooking on for any game
5. [Your data](../guide/05-your-data.md)
6. [Questions and answers](../guide/06-faq.md)

To look something up: [The screens](../guide/07-screens.md), [Every setting](../guide/08-settings.md),
[When something goes wrong](../guide/09-troubleshooting.md), [Words FrameLedger uses](../guide/10-glossary.md),
[The terms, in plain words](../guide/terms-in-plain-words.md), and [Limitations](../LIMITATIONS.md) — what FrameLedger
cannot do.

## Build or change FrameLedger

Start at [Developers](Developers.md). The developer pages are short; each says what a part is and links to its full
document in [`docs/`](../docs/README.md).

- [Architecture](Architecture.md) — the processes, and how a frame travels from the game to the screen.
- [Repository map](Repository-map.md) — what is where.
- [Build and test](Build-and-test.md) — the toolchain and the one gate.
- [Contributor rules](Contributor-rules.md) — the rules every change keeps, safety first.
- [Capture pipeline](Capture-pipeline.md), [Metrics](Metrics.md), [Data and IPC](Data-and-IPC.md),
  [App UI development](App-UI-development.md), [Releasing](Releasing.md), [Developer glossary](Developer-glossary.md).

## More

- A bug or a question: the project's [issues](https://github.com/poli0981/frameledger/issues). A game whose anti-cheat
  FrameLedger missed: the **Safety gap** form there.
- A vulnerability: [`SECURITY.md`](../SECURITY.md).
- The licence: [`LICENSE`](../LICENSE) (GPL v3) and [`NOTICE`](../NOTICE); the name: [`legal/TRADEMARKS.md`](../legal/TRADEMARKS.md).
