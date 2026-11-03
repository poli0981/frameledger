## 25 · Xbox app / Microsoft Store (GDK) titles — the layout, the launch chain, the image paths *(2026-11-03, beta.33, owner decisions D77–D82)*

**Why this was measured.** The owner asked for the games the Xbox app and the Microsoft Store install (PC Game Pass
included) to be handled, evaluated and analysed. FrameLedger had no source for them — the import read Steam, GOG, Epic
and itch, and the owner's log of 2026-11-01 lists exactly those four — and `02_SPEC` put "Microsoft Store/UWP/Game Pass
titles (packaged-app injection constraints)" out of scope for v1 without any document saying what the constraints were.
What follows is read from the owner's own installs: first read-only (the layout), then a launch probe the owner allowed
(D77): each game started through its package with nothing of FrameLedger in it, its processes recorded, and closed by
its window.

**Where.** The dev box of §Environment (Windows 11 Pro build 29648, i7-14700KF, RTX 5080), the Agent there running
elevated (D34). Gaming Services and GamingServicesNet running; its drivers `gameflt.sys` and `xvdd.sys` running.

| Title | Package family name | Version | Engine | Executables in `Content` |
|---|---|---|---|---|
| A Game About Digging A Hole (on C:) | `rokapublish.AGameAboutDiggingAHole_6q4vfhsywtz4j` | 1.2.1.0 | Unreal, `Binaries\WinGDK` | `DiggingGame.exe` (233,984 bytes), `DiggingGame\Binaries\WinGDK\DiggingGame-WinGDK-Shipping.exe` (126.1 MB) |
| Ascend to ZERO | `FlywayGames.AscendtoZERO_sey8peye7nc36` | 1.2.1.0 | Unreal, `Binaries\WinGDK` | `AscendtoZERO.exe` (339,968), `MementoMori\Binaries\WinGDK\AscendtoZERO-WinGDK-Shipping.exe` (160.7 MB) |
| Valor Mortis | `LyricalGamesLLC.ValorMortis_72fag5ekhk96y` | 1.3.5124.0 | Unreal, `Binaries\Win64` | `ValorMortisMSG.exe` (297,984), `ValorMortis\Binaries\Win64\ValorMortisMSG-Win64-Shipping.exe` (184.1 MB) |
| FINAL FANTASY I–VI (Pixel Remaster) | `39EA002F.FF1DF_n746a19ndrrjg` … `FF6DF` | 1.2.1.0–1.2.3.0 | Unity IL2CPP | `FINAL FANTASY*.exe` (664,336 each), `UnityCrashHandler64.exe` |
| inKONBINI | `BeepJapan.inKONBINI_h2va14as5wpxj` | 1.0.209.0 | Unity, D3D12 Agility SDK | `inKONBINI.exe` (672,256), `UnityCrashHandler64.exe` |
| Microsoft Jewel | `Microsoft.MicrosoftJewel_8wekyb3d8bbwe` | 1.1.8131.0 | Unity, D3D12 Agility SDK | `Microsoft Jewel.exe` (239,936) |

Every one also has `gamelaunchhelper.exe` (100,808–113,096 bytes) at the root of `Content`.

### Read-only, before any launch

- **The layout.** Each drive the Xbox app installs to holds `.GamingRoot` at its root: the bytes `RGBX`, a 32-bit count,
  then that many NUL-terminated UTF-16 folder names relative to the drive (C: `XboxGames`, 28 bytes; D: `xbox`, 18
  bytes). Under each folder, one folder per game: `Content\` (the game's files), a `Shaders\` folder and GUID-named
  package files beside it; `C:\XboxGames\GameSave` is not a game and has no `Content`.
- **The package's own description**, in `Content`: `MicrosoftGame.config` (the FINAL FANTASY titles spell it
  `MicrosoftGame.Config`) — `Identity` Name/Publisher/Version, `ShellVisuals` (DefaultDisplayName,
  Square480x480Logo 480×480, Square150x150Logo, StoreLogo, SplashScreenImage 1920×1080, BackgroundColor),
  `ExecutableList` (the launch executable: the Unreal root stub, the Unity player), TitleId, StoreId,
  `DesktopRegistration/ProcessorArchitecture` x64 — and `appxmanifest.xml`: one `Application` whose Executable is
  `GameLaunchHelper.exe` with `EntryPoint="Windows.FullTrustApplication"`, `runFullTrust`, `appLicensing`,
  `unvirtualizedResources`, and on all 11 `desktop6:FileSystemWriteVirtualization` and
  `desktop6:RegistryWriteVirtualization` set to `disabled`. Exactly one `MicrosoftGame.config` per game, at the root of
  `Content`; no reparse point anywhere inside `Content`; no `.sys` file and no anti-cheat folder in any tree; no
  `Content` tree holds more than 2,633 entries in all (check 4's budget is 4,096 per walk). Each FINAL FANTASY title
  ships `FINAL FANTASY*_Data\Plugins\x86_64\steam_api64.dll`.
- **Three spellings of one file.** Windows registers each package's InstallLocation as
  `C:\Program Files\WindowsApps\<PackageFullName>` — a junction to `D:\WindowsApps\<PackageFullName>` (the D: games),
  itself a junction to `D:\xbox\<Game>\Content\`; for the C: game, straight to `C:\XboxGames\<Game>\Content\`.
  `D:\WindowsApps` cannot be listed by the user; a path through it can still be opened. .NET's
  `Directory.ResolveLinkTarget` on the WindowsApps junction: access denied. A `FILE_READ_ATTRIBUTES` handle and
  `GetFinalPathNameByHandleW` resolve the whole chain, unelevated:
  `C:\Program Files\WindowsApps\LyricalGamesLLC.ValorMortis_1.3.5124.0_x64__72fag5ekhk96y\ValorMortis\Binaries\Win64\ValorMortisMSG-Win64-Shipping.exe`
  → `\\?\D:\xbox\Valor Mortis\Content\ValorMortis\Binaries\Win64\ValorMortisMSG-Win64-Shipping.exe`.
- **The executables refuse to be read or run by anything outside their package.** Every game `.exe` under `Content`
  (the stubs, the shipping executables, the Unity players, `UnityCrashHandler64.exe`) answers `CreateFileW` with error 5
  for `FILE_READ_DATA` and for `FILE_EXECUTE`, from a standard user's process, although its DACL grants Users read: its
  first ACE is `(XA;;0x1200a9;;;BU;(WIN://SYSAPPID Contains "<family name>"))`, then `(A;;FR;;;BU)`, and
  `READ_ATTRIBUTES` and `READ_CONTROL` work (size, times and security descriptor read). `gamelaunchhelper.exe` (which
  carries an extra explicit Users ACE), every `.dll`, the images and the XML files read. What refuses — most likely the
  Gaming Services file-system filter — is not established. Consequences: ~~no `CreateProcess` of a GDK game from outside
  its package~~ *(corrected the same day by the run below: `CreateProcess` of one does start a process — never the game
  in its package, so it is still not how FrameLedger starts one)*, and nothing of FrameLedger may need an executable's
  bytes (its PE header, version resource, imports or strings) — `PeImports` already answers `unknown` for a file it
  cannot open, without throwing.
- **Windows' own names for them.** `PackageFamilyNameFromId(Name, Publisher)` computes the family name;
  `GetPackagesByPackageFamily` lists the installed full name; `GetPackagePathByFullName` answers the WindowsApps path —
  all unelevated, documented `appmodel.h` calls. `<family name>!<the appxmanifest Application Id>` is exactly the
  AppUserModelID Windows' Start menu lists for each of the 11 (`Get-StartApps`), e.g.
  `LyricalGamesLLC.ValorMortis_72fag5ekhk96y!AppValorMortisShipping` and `39EA002F.FF1DF_n746a19ndrrjg!Game`.

### The probe (D77)

PowerShell with inline C#, from the scratchpad, as the standard user, with no FrameLedger hook enabled for any of these
titles (none is in the library). Six titles, one of each kind: A Game About Digging A Hole, Ascend to ZERO, Valor
Mortis, FINAL FANTASY, inKONBINI, Microsoft Jewel. For each:

1. `IApplicationActivationManager::ActivateApplication(<AUMID>, null, AO_NONE)` — the pid it returns, its HRESULT.
2. Every 100 ms for 30 s, each process that was not there before: pid, parent pid, image name, creation time,
   `QueryFullProcessImageNameW` (what the watcher and the guard's `ImageDirectory` read) **and**
   `GetModuleFileNameExW(handle, NULL)` (the loader's main-module path — what `TargetResolver` compares through
   `MainModule.FileName`), `GetPackageFullName`, `TokenIsAppContainer`, the integrity level, elevation,
   `GetProcessMitigationPolicy` (signature, dynamic code, user shadow stack, control flow guard, image load), whether
   `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ)` — the guard's module-scan rights — opens it, and
   the first poll at which `dxgi.dll`, `d3d11.dll`, `d3d12.dll`, `D3D12Core.dll` and `vulkan-1.dll` are in its module
   list.
3. The ancestors of the game's process up to the first name on the guard's launcher list, each with the same
   openability test, and every chain member's module names checked against the rules' fuzzy name fragments.
4. `CloseMainWindow` on the game's window, then up to 20 s for it to exit. A game that stays open is left open and
   named to the owner, never killed.

Plus, once each: `explorer.exe shell:AppsFolder\<AUMID>` for FINAL FANTASY (the command Play will use), and one
`CreateProcess` of `FINAL FANTASY.exe` by its path (expected: error 5 — if it starts, it is closed the same way).

### The decision table, written before the run

| Measured | Outcome | Then |
|---|---|---|
| The game process's image path (`QueryFullProcessImageNameW`) | `<root>\<Game>\Content\…`, the final path | the library entry is that path, and the watcher matches it as today |
| | `C:\Program Files\WindowsApps\<Full>\…` | a path under a `WindowsApps` segment is resolved with a read-attributes handle and `GetFinalPathNameByHandleW`, in the watcher's key and in the guard's `ImageDirectory` — only such paths, so no existing entry's key moves |
| The loader's main-module path against the image path | the same | `TargetResolver` moves to `QueryFullProcessImageNameW` all the same: one source of the image path for the watcher, the resolver and the guard |
| | different | that move is what makes a hooked session of a packaged game possible at all |
| The chain | GameLaunchHelper → the game (→ the shipping executable), parents alive | nothing to add: the entry is the process that presents, and the watcher sees it |
| | GameLaunchHelper exits first | the same — the watcher matches by path; no election is needed |
| GameLaunchHelper's parent | `svchost.exe` or `explorer.exe` | the guard's scan set stops there; the launcher list is unchanged |
| | `GamingServices.exe` or another SYSTEM process | it joins the hardcoded launcher list, with this row as the reason — else an unelevated Agent's scan cannot open it and refuses every session |
| A chain member the guard's rights cannot open | none | nothing to add |
| | one | named here; a platform process → the launcher list, anything else → the owner, mid-train |
| A chain member's module with a fuzzy fragment in its name | none | nothing to add |
| | one | recorded with its signature kind; the guard refuses (fail closed) and the owner decides — never relaxed here |
| The token | full trust, the user, medium integrity | the Tier-1 path; a token that is not an AppContainer uses the session's object namespace, so the ring and `Local\FrameLedger.Tolerate.<pid>` work unchanged (the ring itself is first seen in the owner's hooked session) |
| | an AppContainer | refused with a reason of its own (D78) |
| Mitigations | none of: signed-only images, prohibited dynamic code, strict shadow stack, strict CFG | inject |
| | any of them | refused before injecting, the policy named (the refusal is built either way: today each ends as a failed injection, no hooks, or a crash risk) |
| The graphics runtime mapped before a 1 Hz watcher can see the process | yes | the session is `late_attach`, as for a game Steam starts; `dxgiPresentsBeforeHook` says how much |
| `CreateProcess` of a game executable by path | error 5 | D80 stands: Play activates the package in both modes |

### What the run found *(2026-11-03, 03:57–04:09 UTC)*

Every activation answered `S_OK` with the pid of `gamelaunchhelper.exe`. The chain, each time: `gamelaunchhelper.exe`
(in the package; its parent the activating process) → `gamingservicesui.exe` (Gaming Services' own UI, no package,
appearing 0.1–0.8 s after the press and gone again) → `XboxPcTray.exe` (the Xbox app's tray, when not already running)
→ the package's launch executable 6.2–14.4 s after the press → for Unreal, the shipping executable 0.25–0.37 s later as
its child.
`gamelaunchhelper.exe` had exited before the 30 s were up in every run, while the game ran on.

| Title | Launch executable at | Process that presents | Graphics runtime mapped |
|---|---|---|---|
| A Game About Digging A Hole | `DiggingGame.exe` +14.4 s | `DiggingGame-WinGDK-Shipping.exe` +14.7 s | `dxgi` + `opengl32` at its first poll, `d3d11` 2.4 s later |
| Ascend to ZERO | `AscendtoZERO.exe` +8.7 s | `AscendtoZERO-WinGDK-Shipping.exe` +8.9 s | `dxgi` + `opengl32` at once, `d3d12` + `D3D12Core` 2.7 s later |
| Valor Mortis | `ValorMortisMSG.exe` +6.8 s | `ValorMortisMSG-Win64-Shipping.exe` +7.2 s | `dxgi` + `opengl32` at once, `d3d12` + `D3D12Core` 3.1 s later, `d3d11` 3.7 s |
| FINAL FANTASY | `FINAL FANTASY.exe` +8.8 s | the same (+ `UnityCrashHandler64.exe`) | `opengl32` 0.4 s, `d3d11` + `dxgi` 1.2 s |
| inKONBINI | `inKONBINI.exe` +6.8 s | the same (+ `UnityCrashHandler64.exe`) | `dxgi` + `d3d11` + `opengl32` at its first poll |
| Microsoft Jewel | `Microsoft Jewel.exe` +6.6 s | the same (+ six `msedgewebview2.exe` children in its package) | `dxgi` + `d3d11` at once, `d3d12` + `D3D12Core` 0.6 s later |

Against the table above:

| Measured | Outcome | So |
|---|---|---|
| The game process's image path | **`<root>\<Game>\Content\…`** for all six, the C: game included (`C:\XboxGames\…\Content\…`) | the entry is that path; the watcher matches as today |
| The loader's main-module path | **the same string** as the image path, for every process of every chain | no mismatch on these titles; `TargetResolver` still moves to `QueryFullProcessImageNameW` (one source) |
| The chain | GameLaunchHelper → the game (→ the shipping executable); GameLaunchHelper gone before the game is | nothing to add: the watcher matches the entry by path |
| GameLaunchHelper's parent | the activating process: `pwsh.exe` (the probe's `ActivateApplication`), `explorer.exe` (`explorer.exe shell:AppsFolder\<AUMID>`), `dllhost.exe` (below). From the Xbox app itself: not measured | started as Play starts it, the scan set stops at `explorer.exe`; the launcher list is unchanged |
| Chain members the guard's rights cannot open | **none**: every process of every chain — GameLaunchHelper while it lived, the stubs, the shipping executables, the Unity players and crash handlers, Jewel's WebView2 children (two of them at low and untrusted integrity) — opened with `QUERY_LIMITED_INFORMATION \| VM_READ` as the standard user | nothing to add |
| Fuzzy-fragment module names in a chain member | **none** (the Unreal games load `xgameruntime.dll`, `XCurl.dll`, `libHttpClient.GDK.dll`, `GameChat2.dll`, `gameplatformservices.dll`, `gamingservicesproxy_17.dll`, `XblAuthManagerProxy.dll`) | nothing to add |
| The token | every game process: in its package, **not an AppContainer**, medium integrity | the Tier-1 path; the session's object namespace |
| Mitigations | none of the four on any game process: signature `0x0`, dynamic code `0x0`, user shadow stack `0x100` (only `CetDynamicApisOutOfProcOnly`), CFG `0x0`/`0x1` (no strict mode), image load `0x0`. Jewel's WebView2 sandboxes carry signed-only `0x5` and dynamic code `0x1` — children, never a target | inject; the `TargetMitigation` refusal is built for the titles that do |
| The graphics runtime before a 1 Hz watcher sees the process | the Unreal shipping executables import `dxgi.dll` statically and create their D3D device 2.4–3.7 s later; the Unity players have it within 0.1–1.2 s | an attach within the watcher's second is before the Unreal device and around the Unity one: `late_attach`, as for a game Steam starts |
| `CreateProcess` by path | **not error 5.** `FINAL FANTASY.exe` (the package's launch executable): Windows started **`gamelaunchhelper.exe`, outside the package, in its place** (that process's image path and main module both `gamelaunchhelper.exe`), which activated the package — a second, packaged `gamelaunchhelper.exe` (its parent `dllhost.exe`) started `FINAL FANTASY.exe` 6.2 s after the press; the process the caller holds is never the game, and the game is not its descendant. `DiggingGame-WinGDK-Shipping.exe` (not a launch executable): ran **as itself, outside its package** (`GetPackageFullName`: no package), its window up, until closed | D80 stands on two measured grounds instead of the one assumed: a start by path either hands itself to the package within seconds (an early exit the crash safety counts, and a launch whose game it never sees) or runs the game outside its package — which FrameLedger must never cause. The Agent refuses to start an executable under a GDK package root |

**Closing.** Five of the six closed on `CloseMainWindow`; Valor Mortis' window went at once and its two processes
about 40 s later, by themselves. Microsoft Jewel ignored it and was left open for the owner, as agreed.

**Not measured:** a start from inside the Xbox app (GameLaunchHelper's parent there); a package that keeps write
virtualization on (all 11 turn it off), so where the Overlay's log would land in one; any GDK title with anti-cheat;
any Vulkan GDK title; the ring from a packaged process (first seen in the owner's hooked session).
