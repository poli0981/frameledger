# Record a game

## 1. Add the game

- **File ▸ Add game…** — pick the game's `.exe` file.
- **File ▸ Import library…** — FrameLedger lists the games installed through Steam, GOG, Epic and itch.io, and you
  tick the ones to add.

Every game is added with **hooking off**.

## 2. Play

When a game in your library starts, the capture agent records a session by itself. While it runs, the **Dashboard**
shows it live. When the game closes, the session is saved and you get a notice. A session shorter than 30 seconds is
not kept.

## Recorded, or measured

A game in your library is always **recorded**: how long you played, your PC's sensors (GPU temperature, load and power;
CPU load; memory in use) and how much memory the game itself used — its video memory and its RAM, read from Windows
from outside the game.

To **measure** a game — frame rate and frame times, the real render resolution, the upscaler, frame generation, ray
tracing — FrameLedger has to load a small component into it. That is called **hooking**, and it is a choice you make
**one game at a time**: open the game's page (Games ▸ the game) and switch **Hooking** on. A warning explains the risk
first. **Read [Anti-cheat and safety](04-anti-cheat-and-safety.md) before you do.**

In the session list, **T1** marks a measured session and **T2** one that was only recorded; a recorded session says
why it was not measured.

## When FrameLedger will not measure a game

- **It found anti-cheat.** FrameLedger then turns hooking off for that game and says why on the game's page.
- **The game is 32-bit or uses DirectX 9.**
- **It is a Vulkan game** you started yourself.
- **The game runs as administrator.** Settings ▸ Capture agent ▸ *Run the agent as administrator* can help (off by
  default; Windows asks every time the agent starts). Read its risk in [Limitations](../LIMITATIONS.md) first.
- **The game was already running when FrameLedger was updated** — restart the game once.

## Programs that are not games

Open the entry's page and switch **Recording** off: it is then ignored and nothing is recorded.

## Turning all hooking off

**Settings ▸ Capture ▸ Disable all hooking** stops every game from being hooked, whatever each game's own switch says. A
session that is running stops measuring within 30 seconds.
