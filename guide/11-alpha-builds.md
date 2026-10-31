# Alpha builds

## What an alpha is

Besides the betas, FrameLedger publishes **alphas**: the same program as a beta, built on a version of .NET that
Microsoft has not finished yet — .NET 11's release candidate first. They exist so that a new .NET can be tried before the
betas move to it. An alpha's version ends in `-alpha.N` (`0.1.0-alpha.1`), its page on GitHub says *experimental alpha*,
and its installer is `FrameLedger.App-alpha-Setup.exe`.

**If you want results you can rely on, use the beta.** An alpha can crash, record wrongly or fail to update in ways no
beta has. The part of FrameLedger that goes into a game is the beta's own and does not use .NET; everything that starts
it, reads it and stores what it measured runs on the new .NET.

## Before it runs

The first time an alpha starts, the terms window shows the **Alpha notice** beside the other documents, with a box to
tick: *I understand that this is an experimental build and I use it at my own risk.* Accept stays off until it is ticked.
Until you accept, the alpha's capture agent does not run at all. The notice is shown again whenever its text changes.

While an alpha runs, the title bar carries an **ALPHA** badge (hover it to see the .NET version), and a yellow banner
above every page says what it is each time it starts; you can close the banner for that run. **Help ▸ About** names the
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

Microsoft supports .NET 11 on Windows 11 24H2 and later; on Windows 10 only in its LTSC and IoT editions. An alpha may
work on Windows 10 22H2 Home or Pro, but nobody supports it there. .NET 11 also needs a processor with SSE4.2 and POPCNT,
which every processor Windows 11 accepts has; on an older one the alpha does not start at all.

A release candidate stops being supported by Microsoft when the final version ships — for .NET 11's, 10 November 2026 —
and the next alpha moves to the final version.
