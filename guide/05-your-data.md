# Your data

## Where it is

Everything FrameLedger records stays on your PC, in `%LOCALAPPDATA%\FrameLedger` (Tools ▸ Open data folder): the
database with your library and sessions, the logs, and crash reports of FrameLedger itself. There is no account and
nothing is uploaded. The only network requests are the update check and the update download, both to GitHub
([Install and first start](01-install.md#updates)).

## What is in it

Your game library; your sessions — frame times when a game was measured, the statistics, your PC's sensors, the game's
own memory use, how the game's window was shown; a short description of your hardware for each session; your tags and
notes; your settings. The [Privacy Policy](../legal/PRIVACY_POLICY.md) lists all of it.

## How much is kept

The per-frame data of the newest **20 sessions of each game** is kept; older sessions keep their statistics. Change it
in **Settings ▸ Recording ▸ Raw series kept per game** (0 keeps everything).

## Back it up

**Tools ▸ Database maintenance… ▸ Back up…** writes a consistent copy of the database to a file you choose, even while
a game is being recorded. Backing up is up to you — FrameLedger does not do it by itself.

## Delete it

- **A game's sessions:** Games ▸ the game ▸ Sessions ▸ **Delete all sessions…**
- **A game:** its page ▸ **Remove…**
- **Every session of every game:** Settings ▸ Data ▸ **Delete all sessions…**
- **Everything:** uninstall FrameLedger and let it delete the data folder, or delete the folder yourself.

## Share it — carefully

An exported **CSV** or **JSON** file, and a bug report, can contain:

- your games' names and **the paths to their files**, which include your **Windows user name** when a game is installed
  in your user folder;
- your PC's processor, graphics card, driver and Windows version;
- your tags and notes.

Look at a file before you share it. **Help ▸ Report a bug…** builds the report on your PC, shows you every file in it,
and removes your user folder's path from the logs; you decide whether to attach it to an issue on GitHub. An export is
written as it is. Once you share a file, whoever receives it — and the site you post it to — can read it.

## Updating and going back

A new version upgrades the database in place. **An older version cannot open it afterwards** — there is no downgrade,
so keep a backup if you might want to go back.
