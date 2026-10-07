# Every setting

Settings is the last item on the left. Its sections follow, in the order it shows them. Settings marked *agent* are read
by the capture agent; unless the row says otherwise, a change applies from the next session.

## Capture

| Setting | What it does | Default |
|---|---|---|
| **Disable all hooking** | Nothing is loaded into any game, whatever each game's own switch says. A game being measured stops being measured at the next safety check, within 30 seconds. Sessions are still recorded. *Agent.* | Off |
| **Games with hooking on** | Shown while any game has hooking on: one **Turn off** per game. | — |
| **Hide hooking on anti-cheat games** | On, the page of a game where anti-cheat was found shows only what was found; off, it also shows the Hooking switch, greyed out. Such a game is never hooked either way. | On |
| **Allow hooking in games whose only anti-cheat runs in user mode** | Turns on the one narrow exception ([Anti-cheat and safety](04-anti-cheat-and-safety.md#the-one-exception-you-can-make)). The games that qualify are listed under it, each with **Make exception…** (after a warning) or **Withdraw**. *Agent.* | Off |
| **Vulkan layer** | The component that measures Vulkan games — but only a game FrameLedger starts itself, which the App cannot do yet, so a Vulkan game you start is recorded, not measured. The agent registers and removes it by itself; **Register** and **Unregister** repair that, for your Windows user only. | — |

The link under them, *Read what the hook does and does not do*, opens the safety document on GitHub.

## Recording

| Setting | What it does | Default |
|---|---|---|
| **Minimum session length (s)** | A shorter session is not kept. 5 to 600 seconds. *Agent.* | 30 |
| **Sensor interval (ms)** | How often the PC's sensors are read during a session. 500 to 2000 ms. *Agent.* | 1000 |
| **Raw series kept per game** | How many of each game's newest sessions keep their per-frame data; older ones keep their statistics. 0 keeps everything. *Agent.* | 20 |

## Data

| Setting | What it does | Default |
|---|---|---|
| **Delete all sessions** | Deletes every session of every game, with its data and notes, after asking. Your library, your settings and each game's hooking choice stay. It cannot be undone, and it is refused while a game is being recorded. | — |

## Capture agent

| Setting | What it does | Default |
|---|---|---|
| **Run the agent as administrator** | Lets FrameLedger measure games that run as administrator, and read CPU temperature with PawnIO installed. Windows asks every time the agent starts, at logon too. A warning explains the risk first; read [Limitations](../LIMITATIONS.md#running-the-agent-as-administrator). The agent restarts to apply it, or at its next start if a game is being recorded. | Off |
| **Start the agent at logon** | **Register**, **Repair** or **Remove** a task that starts the agent when you log on, for your user only and without administrator rights. Without it, FrameLedger starts the agent when you open it. | Not installed |

## Appearance

| Setting | What it does | Default |
|---|---|---|
| **Theme** | Follow system, Light or Dark. Applies at once. | Follow system |
| **Language** | English, Tiếng Việt or 日本語. The window is rebuilt in the new language at once. | English |

## Window

| Setting | What it does | Default |
|---|---|---|
| **Start FrameLedger with Windows** | Opens FrameLedger when you log on, for your user only. | Off |
| **Minimize to tray on close** | Closing the window keeps FrameLedger running by the clock; quit with **Exit** in the tray's menu or the File menu. | Off |
| **Show FPS with two decimals** | Every frame rate with two decimals (62.40) instead of a whole number (62). Pages show it when they next open. | Off |
| **Hide scroll bars** | No scroll bars are drawn anywhere; the wheel, the keyboard and touch still scroll. Applies at once. | Off |

## Updates

| Setting | What it does | Default |
|---|---|---|
| **Channel** | *Automatic* follows your copy: a test version (a pre-release) looks for test versions, a release for releases. Or choose *Stable*, or *Beta* (releases and pre-releases). | Automatic |
| **Check for updates at startup** | One question to GitHub a few seconds after FrameLedger starts. A new version downloads in the background and installs only when you restart. | On |

## Logging

| Setting | What it does | Default |
|---|---|---|
| **Debug logging** | FrameLedger writes far more detail into its log, for a bug report. The agent keeps its own level. | Off |

## System

This PC as FrameLedger records it with every session: processor, graphics card and driver, memory, Windows build and
the main display. Nothing is sent anywhere. **Copy** puts the lines on the clipboard, for a bug report.

## Legal

**Reopen the legal documents** shows the documents you accepted, and the GPL, again. Nothing is accepted again.
