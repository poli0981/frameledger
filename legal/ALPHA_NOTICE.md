# FrameLedger — Alpha notice

**Version:** 1.1 · **Effective:** {{RELEASE_DATE}}

<!-- Owner decisions D73-D76 (2026-10-31): the text was approved with the plan. Version 1.1 (owner decisions D97 and D98,
     2026-11-11: what an alpha is once .NET 11 has shipped, and the line's pause) was approved with that day's plan.
     Shared.AlphaNotice.Version must equal the version above (LegalDocumentsTests); an alpha build's Legal Gate shows this
     document with its own tick box, and an alpha's Agent does not serve until it is accepted at this version. -->

This notice applies only to **alpha** builds of FrameLedger (versions named `x.y.z-alpha.N`). An alpha build shows it, and
it must be accepted, before the build runs. It adds to the EULA, the Disclaimer and the Privacy Policy and takes nothing
away from them; nothing in it limits your rights under the GPL-3.0.

## 1. What an alpha build is

An alpha build is the source of a published beta, built to try a newer version of .NET — at times a **prerelease
version** — with the matching C# compiler. When this notice was written the alpha ran on .NET 11.0.0, Microsoft's final
release of .NET 11, which the betas moved to in 0.1.0-beta.37; the alphas before it ran on its Release Candidate 3, and a
later alpha may run on a prerelease of the next .NET. It exists so that a new .NET can be tried before FrameLedger's
regular builds rely on it. When the runtime is a prerelease, it, its libraries and its compiler are prerelease software
from Microsoft, and the build says so. In either case FrameLedger has not been tested on an alpha the way a beta is.

## 2. What can go wrong

Everything the Disclaimer says about pre-release builds (§0), and more: an alpha may crash, hang, fail to record, record
wrongly or fail to update in ways no beta has shown. The component loaded into a game is the same native code as the
beta's and does not use .NET; everything that starts it, reads it, computes from it and stores it runs on the alpha's
runtime.

## 3. Your data

An alpha build replaces the beta on your PC and uses the **same data folder** (`%LOCALAPPDATA%\FrameLedger`) and the same
ledger. Before an alpha build first opens your ledger it saves a copy in the `backups` folder inside it, and it does not
open the ledger if that copy cannot be made. Sessions an alpha records stay in the ledger when you go back to the beta.

## 4. Windows and hardware

Microsoft supports .NET 11 on Windows 11, and on Windows 10 only in its LTSC and IoT editions. An alpha — as a beta does
from 0.1.0-beta.37 — may run on Windows 10 22H2 Home or Pro, but it is untested there and nobody supports it. .NET 11
needs a processor with SSE4.2 and POPCNT (x86-64-v2) and does not start without them.

## 5. Your responsibility

You install and run an alpha build **entirely at your own risk**. It comes with no warranty (EULA §5, GPL-3.0 §15–17), and
the developer is not responsible for any incident, loss or damage that results from using it (EULA §6, Disclaimer §0). If
you need dependable results, use the beta.

## 6. Updates, the pause, and going back

An alpha updates only to newer alphas, and a beta only to newer betas; neither becomes the other by itself. To go back,
install the latest beta from the project's GitHub Releases over the alpha; your ledger stays where it is.

A prerelease .NET stops being supported by Microsoft when its final release ships; the next alpha moves to it. **When the
betas run on the same .NET as the newest alpha, the alpha line pauses**: no further alpha is published until a newer .NET
is tried, and a paused alpha receives no updates — its list of anti-cheat software included, which reaches FrameLedger
only inside an update. A paused alpha says so when it starts. Install the latest beta over it.
