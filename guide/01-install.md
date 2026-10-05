# Install and first start

## What you need

- **Windows 10 (22H2) or Windows 11, 64-bit.**
- Games FrameLedger can measure: **64-bit DirectX 11, DirectX 12 and OpenGL games.** Other games — 32-bit games,
  DirectX 9 games, and Vulkan games you start yourself — are still recorded (how long you played, your PC's sensors,
  the game's memory use), but not measured. [Limitations](../LIMITATIONS.md) has the full list.

## Download and install

1. Download `FrameLedger.App-win-Setup.exe` from the project's
   [Releases page](https://github.com/poli0981/frameledger/releases) on GitHub.
2. **Check the file.** Each release publishes SHA-256 checksums in `SHA256SUMS.txt`. In PowerShell:
   `Get-FileHash .\FrameLedger.App-win-Setup.exe` — the result must match the line for that file.
3. Run it. FrameLedger is free software and is **not code-signed**, so Windows SmartScreen may warn about an unknown
   publisher. Once the checksum matches, choose **More info ▸ Run anyway**.

FrameLedger installs into `%LOCALAPPDATA%\FrameLedger.App` and needs no administrator rights. Your recordings are kept
separately, in `%LOCALAPPDATA%\FrameLedger`.

## The first start

The first window walks you through four short steps:

1. **Before you start** — the documents. You accept three: the End User License Agreement, the Disclaimer and the
   Privacy Policy. The GNU GPL v3 is shown too: it is your licence to FrameLedger and needs no acceptance. A short
   summary sits above them, and [The terms, in plain words](terms-in-plain-words.md) explains them, but the documents
   are the terms. Declining closes FrameLedger.
2. **The capture agent** — the background program that notices when one of your games starts and records it. It runs
   as you, without administrator rights.
3. **What hooking means** — the difference between a game that is recorded and a game that is measured. Read
   [Anti-cheat and safety](04-anti-cheat-and-safety.md) before you switch hooking on for any game.
4. **Your game library** — where to import it from. Nothing is measured until you choose to.

## Updates

A few seconds after it starts, FrameLedger asks GitHub whether a newer version exists. A new version downloads in the
background and is installed only when you restart FrameLedger — never while a game is being measured, and not while a
game it measured earlier is still running. You can switch the check off in **Settings ▸ Updates ▸ Check for updates at
startup**, and check by hand with Help ▸ Check for updates.

**Settings ▸ Updates ▸ Channel** is *Automatic* unless you choose: a test version (a pre-release, like every version so
far) looks for test versions, a release looks for releases. Choose *Stable* or *Beta* to decide yourself. Before
0.1.0-beta.14 the channel was *Stable* by default and found no version at all — on an older copy, choose *Beta* to update.

## Uninstall

Uninstall FrameLedger from Windows Settings ▸ Apps. It asks before it deletes your recordings folder.
