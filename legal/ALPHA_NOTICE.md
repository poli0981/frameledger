# FrameLedger — Alpha notice

**Version:** 1.0 · **Effective:** {{RELEASE_DATE}}

<!-- Owner decisions D73-D76 (2026-10-31): the text below was approved with the plan. Shared.AlphaNotice.Version must
     equal the version above (AlphaNoticeTests); an alpha build's Legal Gate shows this document with its own tick box,
     and an alpha's Agent does not serve until it is accepted at this version. -->

This notice applies only to **alpha** builds of FrameLedger (versions named `x.y.z-alpha.N`). An alpha build shows it, and
it must be accepted, before the build runs. It adds to the EULA, the Disclaimer and the Privacy Policy and takes nothing
away from them; nothing in it limits your rights under the GPL-3.0.

## 1. What an alpha build is

An alpha build is the source of a published beta, built for and run on a **prerelease version of .NET** — when this
notice was written, .NET 11 Release Candidate 3 — with the matching C# compiler. It exists so that a new .NET can be tried
before FrameLedger's regular builds move to it. The runtime, its libraries and its compiler are prerelease software from
Microsoft, and FrameLedger has not been tested on them the way a beta is.

## 2. What can go wrong

Everything the Disclaimer says about pre-release builds (§0), and more: an alpha may crash, hang, fail to record, record
wrongly or fail to update in ways no beta has shown. The component loaded into a game is the same native code as the
beta's and does not use .NET; everything that starts it, reads it, computes from it and stores it runs on the prerelease
runtime.

## 3. Your data

An alpha build replaces the beta on your PC and uses the **same data folder** (`%LOCALAPPDATA%\FrameLedger`) and the same
ledger. Before an alpha build first opens your ledger it saves a copy in the `backups` folder inside it, and it does not
open the ledger if that copy cannot be made. Sessions an alpha records stay in the ledger when you go back to the beta.

## 4. Windows and hardware

Microsoft supports .NET 11 on Windows 11 24H2 and later, and on Windows 10 only in its LTSC and IoT editions. An alpha may
run on Windows 10 22H2 Home or Pro, but nobody supports it there. .NET 11 needs a processor with SSE4.2 and POPCNT
(x86-64-v2) and does not start without them.

## 5. Your responsibility

You install and run an alpha build **entirely at your own risk**. It comes with no warranty (EULA §5, GPL-3.0 §15–17), and
the developer is not responsible for any incident, loss or damage that results from using it (EULA §6, Disclaimer §0). If
you need dependable results, use the beta.

## 6. Updates, and going back

An alpha updates only to newer alphas, and a beta only to newer betas; neither becomes the other by itself. To go back,
install the latest beta from the project's GitHub Releases over the alpha; your ledger stays where it is. A release
candidate stops being supported by Microsoft when the final release ships (for .NET 11 RC3, 10 November 2026); the next
alpha moves to it.
