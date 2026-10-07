# Contributor rules

The rules are stated in full, with their amendments, in [`CLAUDE.md`](../CLAUDE.md) §Non-negotiable rules; the safety
policy behind them is [`docs/19_SAFETY_AND_ANTICHEAT.md`](../docs/19_SAFETY_AND_ANTICHEAT.md). In plain words:

## The nine rules

1. **Hooking is opt-in, per game.** The user adds the game *and* switches hooking on for it, after a warning. There is
   no "hook everything" mode and no automatic injection.
2. **The anti-cheat guard is a gate, not a warning.** It checks before every injection and every 30 seconds after. A
   finding refuses, says why, and switches the game's hooking off. There is no override — apart from one narrow,
   evidence-bound exception for anti-cheat that runs entirely in user mode, which the user grants per game and which
   ends for good the first time it goes wrong. Do not add another form of override.
3. **No evasion.** Injection uses the documented `LoadLibraryW`; the component keeps its real name, exports and version
   information. A performance tool must be *visible* to anti-cheat. A change that makes FrameLedger harder to detect is
   refused on principle.
4. **Never read or write game memory** beyond the arguments of the calls we hooked and the objects we own.
5. **The hook must never crash the game.** Every hook body is guarded, allocates nothing, takes no lock, logs nothing
   and lets no exception out; repeated faults switch the hook off.
6. **Never one inflated frame rate.** With frame generation measured, Native, Displayed and the factor are shown
   together; without it, *Presented FPS* with a sentence saying whether generated frames could be in it
   ([Metrics](Metrics.md)).
7. **Ray tracing, path tracing and ray reconstruction are Yes, No or N/A.** Where it cannot be measured, the answer is
   N/A — never a made-up Yes.
8. **No telemetry, no analytics, no silent network calls.** The rule permits three requests — the update check, a
   detection-rules fetch and store metadata the user opts into. Only the update check, and its download, exists today,
   and the user can switch it off.
9. **No obfuscation of our own binaries.** GPL v3; releases ship with SHA-256 checksums.

## What will never be built

Manual mapping or reflective loading; erasing PE headers; unlinking from the module list; hiding, renaming or
randomising the component, its exports or its object names; hiding threads or evading debuggers; packing; any bypass or
stealth mode, or instructions to defeat the guard; reading game state; a kernel driver of our own. A pull request that
proposes one is closed with a pointer to the safety document.

## Every pull request

The checklist is [`.github/pull_request_template.md`](../.github/pull_request_template.md):

- `./build.ps1 check` passes — no warning in C# or C++, every test green, formatting clean.
- A document a change makes wrong is corrected **in the same pull request**. A stale sentence is struck through and
  dated where it stands, not quietly rewritten.
- Every new user-visible string exists in English, Vietnamese and Japanese (`.resx`).
- Every first-party source file opens with the two-line licence header that [`NOTICE`](../NOTICE) explains
  (`tools/notice-check.ps1 -Fix` writes it).
- A change to `src/` adds a line to `CHANGELOG.md` `[Unreleased]`.
- A change to the hook layer states its measured overhead, lists any new hook in the inventory
  ([`docs/17_HOOK_ENGINE.md`](../docs/17_HOOK_ENGINE.md)) and answers the safety review questions in the template.
- A user-visible limit that moves updates [`LIMITATIONS.md`](../LIMITATIONS.md) in the same pull request.

How to contribute and how contributions are licensed: [`CONTRIBUTING.md`](../CONTRIBUTING.md).
