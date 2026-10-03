# Anti-cheat and safety

## The risk

To measure a game, FrameLedger loads a small component into it ("hooking"). **Anti-cheat systems can detect that and
may warn you, block you, or permanently ban your account** — and a ban can arrive days after the session. The developer
cannot reverse a ban, recover progress, or speak to a game's publisher for you.

**Use hooking for offline and single-player games.** Switching it on for a game with online or competitive play is
your decision and your risk.

## What FrameLedger does to keep the risk small

- **Hooking is off for every game** until you switch it on for that game, after a warning.
- **It looks for anti-cheat** before it hooks a game and every 30 seconds while it is hooked. If it finds any, it
  refuses — or stops measuring at the next check — and turns hooking off for that game. **There is no switch that
  overrides this**, apart from the one narrow exception below.
- **It never hides.** It keeps its real name and does nothing to avoid being seen by security software.
- **It never reads or changes the game's memory**, its saves or your input. It only observes the graphics calls the
  game makes.

## What it cannot promise

- Its list of anti-cheat systems is its own and **can be incomplete**, and a game can add anti-cheat in an update.
- Anti-cheat that starts in the middle of a session can be there for **up to 30 seconds** — in the worst case 65 —
  before FrameLedger stops.
- Some anti-tamper software (Denuvo, for example) can make a game **crash** when it is hooked, even offline.

## The one exception you can make

**Settings ▸ Capture ▸ User-mode exceptions** (off by default). For a game whose only finding is one anti-cheat that runs
entirely inside the game — no driver, nothing else found — you may allow hooking for that game after a separate warning.
Its first two hooked sessions are a trial: one that does not go well ends the exception for good. FrameLedger still runs
every check under it, and **a ban is still possible.** Kernel anti-cheat is never allowed.

## Turning everything off

**Settings ▸ Capture ▸ Disable all hooking** stops all hooking at once, whatever each game's own switch says.

## If FrameLedger missed a game's anti-cheat

Please open an issue with the **Safety gap** form on the project's GitHub page. That is treated as a safety bug, with
the same priority as a security report. A fix reaches you with the next version you install.

The full statement of the risk is the [Disclaimer](../legal/DISCLAIMER.md), §2 and §2A.
