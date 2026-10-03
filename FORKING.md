# Forking and redistributing FrameLedger

This page is for anyone who clones or forks FrameLedger — and above all for anyone who **gives builds of a modified
FrameLedger to other people**. It says what the licence lets you do, what it asks of you when you distribute, and the
practical things a fork has to change so that its users and FrameLedger's users do not get each other's updates, data
or blame. It is written to be useful, not as legal advice; the texts that decide are `LICENSE` (the GNU GPL, version 3)
and `NOTICE`.

## What the licence lets you do

FrameLedger is free software under the **GNU General Public License, version 3 only** (`GPL-3.0-only`). You may run it
for any purpose, study and change its source, and share copies — changed or not — free of charge or for a fee. You do
not need anyone's permission, and you do not have to accept the licence just to run the program (GPLv3 §9).

## What it asks when you distribute a build

"Distribute" here is the GPL's *convey*: giving a copy to someone else, including publishing an installer. Running
FrameLedger yourself, or changing it for yourself, asks nothing of you.

- **The source goes with the build.** Whoever receives your binaries must be able to get the complete source code
  that builds them, under the same licence (GPLv3 §6) — the C++ and C# sources, the build scripts (`build.ps1`,
  `src/native/CMakeLists.txt`, the workflows) and everything else needed to rebuild them. Publishing the exact source
  tree at the tag you built from, next to the release, is the simplest way. An installer such as the Velopack
  `Setup.exe` counts as a binary.
- **Keep the notices.** Ship `LICENSE`, `NOTICE` and the third-party licences with every copy. The release workflow
  already does this: the App's publish step copies them into `licenses\` beside the executables (`licenses\NOTICE.txt`,
  `licenses\FrameLedger-GPL-3.0.txt`, `licenses\THIRD_PARTY_NOTICES.md`, `licenses\nuget\`), and
  `.github/workflows/release.yml` refuses a published tree without them. Do not remove the copyright and licence
  headers from the source files.
- **Say that it is changed.** A modified version must carry prominent notices that you modified it, and when
  (GPLv3 §5(a)).
- **Add no restrictions.** You may not impose terms that take away the rights the GPL gives your users (GPLv3 §10) —
  an end-user agreement that forbids what the GPL permits is not allowed. FrameLedger's own EULA says that nothing in
  it limits those rights.

### The additional terms in `NOTICE`

FrameLedger's own material is licensed with three additional terms of the kinds GPLv3 §7 permits. In short:

- **(b)** keep the copyright line and the attribution *"Based on FrameLedger by poli0981 —
  https://github.com/poli0981/frameledger"* in your build's Appropriate Legal Notices (FrameLedger shows them under
  Help ▸ About);
- **(c)** do not name a modified version "FrameLedger" alone, say in its notices and documentation that it is modified
  and by whom, and do not present its builds, installers or update feed as the original project's;
- **(e)** no rights are granted in the name "FrameLedger" or the project's logo and icon, beyond saying truthfully
  where your work comes from ("based on FrameLedger", "a fork of FrameLedger").

`NOTICE` has the exact wording. These terms cover material written by FrameLedger's developer; how far they reach
depends on the copyright in that material — parts of FrameLedger were written with AI assistance under the developer's
direction. If the difference matters to you, ask a lawyer, not this page.

## What a fork that ships builds has to change

FrameLedger installs into the user's profile, keeps one database there, runs a background agent and registers a few
system-wide names. A fork that keeps them **collides with an installed FrameLedger**: the two would update each other,
share — and migrate — one database, and fight over the same pipe and shared memory. Change all of these together:

| What | Where | Why |
|---|---|---|
| Product name, icon, title | the App's resources, `--packTitle` in `release.yml`, `Strings.resx` | NOTICE (c) and (e) |
| Update feed | `VelopackUpdateClient.RepositoryUrl` (`src/FrameLedger.App/Update/VelopackUpdateClient.cs`) | otherwise your users are offered FrameLedger's releases, and FrameLedger's users yours |
| Install folder | `--packId` in `release.yml` (today `FrameLedger.App`) | Velopack installs into `%LOCALAPPDATA%\<packId>` and **deletes that folder on uninstall** — never choose a packId that equals any data folder |
| Data folder | `LedgerPaths` (`src/FrameLedger.Infrastructure/Persistence/LedgerPaths.cs`) and the other `%LOCALAPPDATA%\FrameLedger` paths (`DetectionRulesFile`, `VkLayerLaunchEnvironment`, the Overlay's log folder, and the guard's own `fl::guard::RulesFilePath` in `src/native/FrameLedger.Injector/src/fl_ac_rules.cpp`) | a newer schema migrates the database in place and an older build cannot open it again |
| Agent pipe | `IpcProtocol.PipeName` (`src/FrameLedger.Shared/Ipc/IpcProtocol.cs`) | the App talks to whichever agent owns the pipe |
| Shared memory and mutex names | `Local\FrameLedger.*` (the ring, the tolerance channel, `SingleInstance`, the agent's instance lock) — in C# and in `src/native` | two programs must not open each other's ring |
| Vulkan layer | `VK_LAYER_FRAMELEDGER_overlay`, its `HKCU\SOFTWARE\Khronos\Vulkan\ImplicitLayers` registration, and its `FRAMELEDGER_ENABLE_VK_LAYER` / `DISABLE_FRAMELEDGER_VK_LAYER` variables (the manifest template in `src/native/FrameLedger.VkLayer/` and `VkLayerLaunchEnvironment`) | one implicit layer per name — and a fork that keeps the variables is switched on by every FrameLedger launch |
| Start with Windows | `RunAtLogon.DefaultValueName` (`src/FrameLedger.Infrastructure/Startup/RunAtLogon.cs`) | one `HKCU\…\Run` value per name |
| Logon task | `LogonTask` (the Agent's maintenance verbs) | one task per name |
| Issue and help links | `IssueLink.Repository` (`src/FrameLedger.App/Services/IssueLink.cs`) — `RepositoryLinks` builds every document link in the App from it and the source ref `release.yml` passes — plus the Releases link in `guide/01-install.md` and `.github/ISSUE_TEMPLATE/` | your users' reports belong in your tracker |
| Who the documents name | `legal/EULA.md`, `legal/DISCLAIMER.md`, `legal/PRIVACY_POLICY.md` name poli0981 as the Developer, with a contact | the person who distributes a build is the one its users deal with; review every claim they make, too — they describe FrameLedger's builds, not yours |
| Version information | `FL_COMPANY_NAME` / `FL_PRODUCT_NAME` in `src/native/CMakeLists.txt`, and the `ProductName` that `tools/versioninfo-check.ps1` requires | anti-cheat vendors and users identify the DLLs loaded into games by it (`docs/19_SAFETY_AND_ANTICHEAT.md`); it must name your project |
| Checksums | your own `SHA256SUMS.txt` | users verify what *you* built |

The detection rules and the anti-cheat blocklist (`rules/detection-rules.json`) ship with each build; keeping them
current for your users becomes your job.

## What we ask, and cannot require

The GPL does not allow adding restrictions, so the following is a request, not a condition. FrameLedger is built
around a few rules (`CLAUDE.md`, *Non-negotiable rules*), and the reason is the people who use it:

- the anti-cheat guard is a hard gate with no override switch;
- injection is opt-in, per game, and never automatic;
- nothing is hidden from security software — no manual mapping, no header erasure, no renamed or obfuscated binaries;
- the game's memory is never read or written outside the hooked calls;
- no telemetry and no silent network requests.

A build that drops any of these can get its users' accounts banned. It is not FrameLedger, must not say it is
(NOTICE (c)), and the original developer is not responsible for it.

## See also

`NOTICE` · `LICENSE` · `CONTRIBUTING.md` (if you would rather send your change upstream) ·
`legal/TRADEMARKS.md` · `legal/THIRD_PARTY_NOTICES.md` · `docs/12_BUILD.md` (building from source)
