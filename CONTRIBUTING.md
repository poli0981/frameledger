# Contributing to FrameLedger

Thank you for looking. FrameLedger is a small project with strict rules, because it loads code into games and a mistake
can cost a user their account. This page says how to build it, what a change has to bring with it, what will not be
merged, and under what licence your contribution is accepted.

## Before you start

- **Read `CLAUDE.md`'s *Non-negotiable rules* and `docs/19_SAFETY_AND_ANTICHEAT.md` first.** They constrain everything
  that touches a game.
- **Open an issue before anything larger than a fix**, so the approach can be agreed before the work is done.
- **A game with anti-cheat that FrameLedger fails to detect** is a safety bug: use the *Safety gap* issue form.
- **A vulnerability in FrameLedger itself** goes the private route in `SECURITY.md`, never a public issue.
- Everyone who takes part follows the `CODE_OF_CONDUCT.md`.

## Building

You need Windows 10 22H2 or later (x64), Visual Studio 2022 with the C++ workload (MSVC v143), CMake 3.28 or later,
PowerShell 7, and the .NET SDK that `global.json` pins. Then:

```powershell
./build.ps1 check
```

That is the whole quality gate, and continuous integration runs the identical script: the native build and its tests,
`clang-format`, the managed build with warnings as errors, `dotnet format`, every test project, and the repository's own
checks (licences, the accuracy blocks, the licence headers, the changelog rules, …). `docs/12_BUILD.md` describes each
step and the build profiles. Run it in your main checkout: a fresh `git worktree` cannot configure the native build.

Never point a build of your own at the data FrameLedger records into (`%LOCALAPPDATA%\FrameLedger`): a newer build
migrates the database, and the released one cannot read it back. To look at real data, copy the folder and start
`FrameLedger.exe --data-dir <copy>` — a viewer that starts no Agent and changes nothing on the PC (`docs/12_BUILD.md`
§Debugging says how to copy it safely).

## What a pull request brings with it

The definition of done is the one in `CLAUDE.md`:

- it builds without warnings (C# and C++), the tests are green, and `dotnet format` and `clang-format` are clean;
- a change to the hook path states its measured overhead in the PR (`docs/14_TESTING.md` §Hook overhead);
- a new hook is listed in `docs/17_HOOK_ENGINE.md` §Hook inventory and justified against rule 4;
- every new user-visible string exists in `en`, `vi` and `ja` (`docs/09_I18N.md`);
- a change that makes a document wrong updates that document in the same PR;
- a change under `src/` adds an entry under `## [Unreleased]` in `CHANGELOG.md`;
- every new source file carries the licence header — `./tools/notice-check.ps1 -Fix` writes it. A file you wrote
  yourself may name you in its second line instead, in the same form:
  `Copyright (C) <year> <your name> - additional terms under GPLv3 section 7: see NOTICE`.

The pull-request template lists the safety checks for changes to the capture layer.

## What will not be merged

Whatever its other merits, a change that

- adds any way to override, weaken or skip the anti-cheat guard (rule 2);
- makes FrameLedger harder to detect — manual mapping, header erasure, thread hiding, unlinking modules, obfuscation,
  renamed binaries (rules 3 and 9);
- reads or writes game memory outside the hooked calls (rule 4), or makes a hook able to crash a game (rule 5);
- adds telemetry, analytics or any network request beyond the ones `legal/PRIVACY_POLICY.md` lists (rule 8);
- turns injection on for a game the user did not individually enable (rule 1).

## Licensing of contributions

FrameLedger is licensed under the GNU GPL, version 3 only, with the additional terms in `NOTICE`. **By opening a pull
request you confirm that you have the right to submit the change, and you license it under GPL-3.0-only, with the
additional terms in `NOTICE` (GPLv3 §7 (b), (c) and (e)) applying to it as they apply to the rest of FrameLedger's own
material.** There is no contributor licence agreement and no copyright assignment: you keep the copyright in your
contribution. Code from elsewhere must come with a licence compatible with GPL-3.0-only and be listed in
`legal/THIRD_PARTY_NOTICES.md`.

Changes written with AI assistance are welcome on the same terms — you are responsible for what you submit, as for any
other change.

## Questions

Open an issue, or write to contact@poli0981.dev.
