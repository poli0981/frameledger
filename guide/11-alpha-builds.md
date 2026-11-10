# Alpha builds

## What an alpha is

Besides the betas, FrameLedger publishes **alphas**: the same program as a beta, built to try a newer version of .NET —
sometimes one Microsoft has not finished yet. They exist so that a new .NET can be tried before the betas rely on it. An
alpha's version ends in `-alpha.N` (`0.1.0-alpha.1`), its page on GitHub says *experimental alpha*, and its installer is
`FrameLedger.App-alpha-Setup.exe`.

**If you want results you can rely on, use the beta.** An alpha can crash, record wrongly or fail to update in ways no
beta has. The part of FrameLedger that goes into a game is the beta's own and does not use .NET; everything that starts
it, reads it and stores what it measured runs on the alpha's .NET.

## Right now: the alphas are paused

The alphas tried .NET 11. Since 0.1.0-beta.37 the betas run on .NET 11 too, so **0.1.0-alpha.4 is the last alpha for
now**: it brings an installed alpha onto the finished .NET 11, and after it no alpha is published until there is a newer
.NET to try.

A paused alpha gets **no updates** — no fixes, and not the list of anti-cheat software FrameLedger refuses to touch, which
only arrives inside an update. Its banner tells you so. **Install the latest beta over it** (below); your data stays.
One more thing: once ten betas have been published after 0.1.0-alpha.4, an older alpha can no longer find it as an
update, and installing the beta by hand is the only way forward.

## Before it runs

The first time an alpha starts, the terms window shows the **Alpha notice** beside the other documents, with a box to
tick: *I understand that this is an experimental build and I use it at my own risk.* Accept stays off until it is ticked.
Until you accept, the alpha's capture agent does not run at all. The notice is shown again whenever its text changes.

While an alpha runs, the title bar carries an **ALPHA** badge (hover it to see the .NET version), and a yellow banner
above every page says what it is each time it starts — while the alphas are paused, that they are, and to install the
beta; you can close the banner for that run. **Help ▸ About** names the
.NET it runs on.

## Your data

An alpha **replaces** the beta on your PC — only one of them is installed at a time — and uses the **same data folder**
and the same database ([Your data](05-your-data.md)). Before an alpha opens your database for the first time, it copies it
to the `backups` folder inside the data folder, as `ledger-before-<version>.db`; the newest three copies are kept. If the
copy cannot be made — the disk is full, say — the alpha does not open the database and tells you why.

Sessions an alpha records stay in the database when you go back to the beta, and the beta shows them.

## Installing one, and going back

There is no switch inside FrameLedger: an alpha never arrives as an update to a beta, and a beta never as an update to an
alpha.

- **To try an alpha:** download `FrameLedger.App-alpha-Setup.exe` from the alpha's page on GitHub Releases, check its hash
  against `SHA256SUMS.txt`, and run it. Windows' installer asks whether to replace the FrameLedger already installed;
  say yes.
- **Updates:** an alpha updates only to newer alphas. In **Settings ▸ Updates** the channel is locked on an alpha, and
  says so.
- **To go back:** install the newest beta's `FrameLedger.App-win-Setup.exe` the same way. Your database stays where it
  is.

## Windows and your PC

An alpha needs what a beta needs since 0.1.0-beta.37 ([Install](01-install.md)): Microsoft supports .NET 11 on
Windows 11; on Windows 10 (22H2) FrameLedger may run, but it is not tested there and nobody supports it; and on a
processor without SSE4.2 and POPCNT it does not start at all.

When an alpha runs on a .NET Microsoft has not finished, Microsoft stops supporting that version when the finished one
ships, and the next alpha moves to the finished one.
