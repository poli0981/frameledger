# Questions and answers

## Why does my session say N/A everywhere?

The game was recorded, not measured (**T2**): hooking is off for it, FrameLedger found anti-cheat, or the game is one
FrameLedger cannot measure. The session's summary says which. See [Record a game](02-record-a-game.md).

## I switched hooking on, and the game is still not measured.

FrameLedger refuses a game where it finds anti-cheat, and cannot measure 32-bit games, DirectX 9 games, Vulkan games you
start yourself, or a game that runs as administrator (unless the agent does too). A game that was already running when
FrameLedger updated needs one restart. The game's page and the session's summary say what happened.

## Why "Presented FPS" and not the game's own frame rate?

Because frame generation could not be measured in that session. Presented FPS counts every frame the game handed to
Windows, and the sentence beside it says whether generated frames could be among them. FrameLedger never labels a
number that may include generated frames as the game's own.

## Why is the video memory different from my in-game overlay?

FrameLedger shows what Task Manager shows for the game's process: its *Dedicated GPU memory*. Overlays often show the
whole graphics card's use, which includes every other program, or the amount the game is allowed to use. FrameLedger
shows the whole card's figure too, separately.

## Why is the CPU temperature N/A?

Reading it needs *Run the agent as administrator* (Settings ▸ Capture agent) and the separately installed PawnIO
driver — and that reading has not been checked on real hardware yet. CPU load needs neither.

## Windows warned me when I installed FrameLedger.

FrameLedger is not code-signed, so SmartScreen does not know its publisher. Check the download against the published
SHA-256 checksum before you run it ([Install and first start](01-install.md)).

## Can I use FrameLedger with online games?

Recording works with any game. **Hooking** an online or competitive game risks your account — see
[Anti-cheat and safety](04-anti-cheat-and-safety.md).

## Something is wrong. How do I report it?

**Help ▸ Report a bug…** builds a report on your PC and opens the issue form on GitHub; you decide what to attach. If
FrameLedger missed a game's anti-cheat, use the **Safety gap** form instead.

## Where are the full documents?

**Help ▸ About ▸ Legal documents**, or Settings ▸ Legal ▸ Reopen the legal documents. What FrameLedger cannot do is in
[Limitations](../LIMITATIONS.md).
