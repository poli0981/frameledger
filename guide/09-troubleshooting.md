# When something goes wrong

Find what you see, then what it means and what to do. Questions that are not problems are in
[Questions and answers](06-faq.md).

## The capture agent is not working

| You see | It means | Do this |
|---|---|---|
| **Connecting to agent…** or **Starting agent…** | FrameLedger is reaching its agent, or starting it. | Wait a few seconds. |
| **Agent offline**, and a banner: *The capture agent is not answering* | The agent is installed but does not answer. Your library, sessions and charts still work; only what is live does not. FrameLedger keeps trying. | **Retry** on the banner, or close FrameLedger and open it again. |
| **Agent not installed** | `FrameLedger.Agent.exe` is missing from FrameLedger's folder. | Install FrameLedger again from the [Releases page](https://github.com/poli0981/frameledger/releases). |
| **Waiting for administrator permission…** | *Run the agent as administrator* is on, and Windows is asking. The prompt may be waiting on the taskbar. | Answer it — or switch the option off in Settings ▸ Capture agent. |
| Settings says *the administrator prompt was declined* | The agent runs with your normal rights. | Nothing, unless you need it ([Every setting](08-settings.md#capture-agent)). |

## A game was recorded but not measured

The session is **T2**, its frame figures read N/A, and its summary says why on the **Why:** line — go by that line.
While the game runs, a notice may say *hooking refused*.

| Why | Do this |
|---|---|
| Hooking was off for the game. | Switch **Hooking** on in the game's page, after reading [Anti-cheat and safety](04-anti-cheat-and-safety.md). |
| The game's file changed since hooking was switched on — an update, usually. | Switch hooking on again for it. |
| The game you added is its launcher. | Add the game's own `.exe` too: hooking a launcher does not reach the game it starts. |
| Anti-cheat was found in the game. | Nothing: FrameLedger will not hook it, and hooking stays off. The only exception is narrow ([Anti-cheat and safety](04-anti-cheat-and-safety.md#the-one-exception-you-can-make)). |
| An anti-cheat driver or service was running on the PC. It may belong to another game. | Close the program it belongs to: the next session is measured again. |
| Anti-cheat appeared while the game was running. | Nothing: measuring stopped, and the summary says *Measurement stopped early*. |
| FrameLedger could not check the game's files. | It checks again by itself; Tools ▸ Update detection rules checks now. |
| The game runs as administrator. | Settings ▸ Capture agent ▸ *Run the agent as administrator* — read its risk in [Limitations](../LIMITATIONS.md#running-the-agent-as-administrator) first. A game protected by an anti-cheat driver cannot be measured either way. |
| The game is 32-bit, uses DirectX 9, or uses Vulkan. | Nothing: these are recorded, not measured ([Limitations](../LIMITATIONS.md#games-it-will-not-measure)). |
| **Disable all hooking** is on. | Switch it off in Settings ▸ Capture. |
| The game was already running when FrameLedger was updated. | Close the game and start it again. |
| Hooking was switched off after the game crashed twice soon after it started. | Switch it on again in the game's page if the cause may be gone — after a driver update, for example. |
| The game's file is gone. | If only its drive letter changed, FrameLedger follows it within about 15 seconds; otherwise **Change executable…** on the game's page. |
| The component could not be loaded into the game. | [Report a problem](#report-a-problem). |

## Some numbers read N/A

- **Everything** reads N/A: the session was not measured — see above.
- **1% low** needs 1,000 of the game's own frames, and **0.1% low** 10,000: a shorter session reads N/A for them.
- **VSync** reads N/A for OpenGL games.
- **VRAM (this game)** reads N/A for the game's first second.
- **GPU sensors** on AMD and Intel cards have not been tested, and **GPU held back** is read on NVIDIA cards only.
- **A session's charts are gone**: its per-frame data was removed under *Raw series kept per game*
  ([Every setting](08-settings.md#recording)); its statistics stay.
- **Presented FPS** instead of the game's own frame rate, **video memory** that differs from an in-game overlay, and
  **CPU temperature**: see [Questions and answers](06-faq.md).

## A session says the game crashed

*How it ended*, on the session's summary:

| It says | It means |
|---|---|
| ended normally | The game closed by itself. |
| ended with exit code 1 | What *End task* or `taskkill` leaves behind: not a crash. |
| crashed (0xC0000005 …) | The game ended with an error code. *0xC0000006* often points at a failing drive. |
| crashed (Windows logged an application error) | Windows recorded the game's crash. |
| crashed (the game started its crash reporter, …) | The game's own crash reporter started while it was ending. |
| interrupted | The agent itself stopped during the session; the session was rebuilt when the agent next started. |

More in [Limitations](../LIMITATIONS.md#when-a-game-crashed). If FrameLedger itself stops, it says *FrameLedger stopped
unexpectedly* and offers to report it.

## An update does not install

- *A session is running, so it is applied only after the session ends* — restart FrameLedger once the game has closed.
- *… is still loaded in a game FrameLedger measured* — close that game, then **Restart to update**.
- *Try again in about an hour* — GitHub limits how often it is asked. *Offline* — no connection.
- A download that fails its check twice: [report a problem](#report-a-problem).
- Windows warns about the installer: see [Install and first start](01-install.md#download-and-install).

## The database

Everything is in one file, the ledger. **Tools ▸ Database maintenance…**:

- **Check** reads the whole file. If it finds a problem, **Back up…** first, then [report a problem](#report-a-problem).
- **Back up…** writes a consistent copy wherever you choose, even while a game is recorded.
- **Sweep…** removes old per-frame data, as *Raw series kept per game* does; it needs the agent running.
- **Compact** gives freed space back to the disk; it waits until no game is being recorded.
- *The ledger was busy* — try again in a moment.

## Looking at a copy of your data

`FrameLedger.exe --data-dir <folder>` opens a **copy** of a data folder to look at — one sent with a bug report, for
example. The badge reads **Viewer — no Agent**: nothing is recorded and nothing on this PC is changed. It refuses your own
data folder.

## Report a problem

- **Help ▸ Report a bug…** (or Logs ▸ Export bug bundle) saves a zip on your PC with FrameLedger's logs, a description of
  your PC and your settings — and, if you tick them, FrameLedger's last crash dump and the last session without its
  frames, notes or tags. Your Windows user name is removed from the paths in the logs; the crash dump is not cleaned.
  You see every file, then the issue form opens on GitHub, and you add the zip yourself: FrameLedger sends nothing.
- The logs are in `%LOCALAPPDATA%\FrameLedger\logs` (Logs ▸ Open logs folder).
- If FrameLedger missed a game's anti-cheat, use the **Safety gap** form instead
  ([Anti-cheat and safety](04-anti-cheat-and-safety.md#if-frameledger-missed-a-games-anti-cheat)).
