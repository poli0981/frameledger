# Games FrameLedger has been run with

*As of 2026-11-06 (0.1.0-beta.34). Built from a copy of the developer's own FrameLedger database and the repository's
measurement notes, and reviewed by the project's owner before it was published. It lists what happened on one machine;
it promises nothing about any other.*

*~~(Not re-read for 0.1.0-beta.11 or 0.1.0-beta.12 — noted 2026-10-04. Refreshing it needs a new copy of the database and
the owner's review; sessions since 2026-09-27, and the game-memory figures beta.12 adds, are not here.)~~ Refreshed
2026-11-06 from a copy of the database taken that day, and reviewed by the owner with the plan of 0.1.0-beta.34: the
sessions since 2026-09-27 are below, with the graphics memory a game used where it adds something.*

**The machine.** Every row below was run on one PC: an NVIDIA GeForce RTX 5080 (the only graphics card), an Intel Core
i7-14700KF, 32 GB of DDR5, Windows 11 (Insider builds 26300 → 29648) and a 2560×1440, 240 Hz monitor; the graphics
driver went from 32.0.16.1714 to 32.0.16.2114 over the sessions in the database. **No AMD or Intel
graphics card and no second machine has been tested**, and ~~**no real Vulkan or OpenGL game has been measured** — every
measurement below is Direct3D 11 or 12~~ **one real Vulkan game has been measured (Everhex Demo, 2026-10-13) and no
OpenGL game** — every other measurement below is Direct3D 11 or 12 *(corrected 2026-10-14)*. What the tiers mean and what FrameLedger cannot do: [LIMITATIONS.md](../LIMITATIONS.md).

**Two periods.** From 2026-08-03 to 2026-09-06 the games were measured with the developer's capture tool, one run at a
time against the game's own settings menu (reports in [spike-notes.md](spike-notes.md)). From 2026-09-16 the shipped
app and agent recorded them as a user would; ~~the database holds the sessions from 2026-09-22 on~~ the copy of
2026-11-06 holds the sessions from 2026-10-04 on — earlier ones went with the 2026-09-16 recovery and with library
entries and sessions removed since; a row citing the database before 2026-10-04 was read from the copy of 2026-09-27.

## Measured — hooked (Tier 1)

| Game | Engine | API | What was measured | When | Evidence |
|---|---|---|---|---|---|
| Cyberpunk 2077 | — | D3D12 | DLSS Balanced 1485×835 → 2560×1440, exact against its settings file; DLSS Frame Generation off ×1.00, ×3 (61.4 → 183.5 FPS), ×4 (58.7 → 234.1); FSR 3 + FSR frame generation ×2 (74.3 → 148.6); ray tracing *Yes* with path tracing on and *No* with it off, as set; on 2026-10-20, ×2, ×3 and ×4 chosen in the game's menu within one session read as separate states, ×4.0 at 63.8 → 255.3 FPS, with ray tracing and Ray Reconstruction *Yes* | 2026-08-15 → 10-20, about 26 runs | [spike-notes §8, §9, §11, §23](spike-notes.md), database |
| Dying Light: The Beast | — | D3D12 | DLSS Frame Generation ×4: Native 70.5 → Displayed 282.1 FPS, counted through DXGI; FSR + FSR frame generation ×2 (70.5 → 140.9); ray tracing *Yes* | 2026-09-04 → 09-06 | [spike-notes §9](spike-notes.md), [03_METRICS §Frame Generation](03_METRICS.md) |
| Hell Is Us | Unreal Engine 5.5.4 | D3D12 | DLSS Frame Generation ×4 (76.5 → 306.2); DLSS shown as *driver-reported* (it bypasses Streamline); FSR + FSR frame generation ×2 (85.7 → 171.4); XeSS frame generation shown as Presented FPS only; ray tracing *No* | 2026-09-03 → 09-16 | [spike-notes §8, §9](spike-notes.md) |
| Black Myth: Wukong | Unreal Engine 5.0.0 | D3D12 | DLSS Frame Generation ×4 (55.4 → 221.5); ray tracing *Yes* (16.2 % of frames in the app's session); a session that changed state showed its steady state (×4.1) instead of an average | 2026-08-20 → 09-16 | [spike-notes §9, §14](spike-notes.md) |
| Clair Obscur: Expedition 33 | Unreal Engine 5.4.4 | D3D12 | DLSS + Frame Generation ×4 (54.9 → 218.4) and ×2 (76.0 → 149.8); frame generation off read *none*, with DXGI agreeing; ray tracing *No*; the NVIDIA App's *Fixed ×6* read ×6.0 (81.8 → 490.9) on 2026-10-20 | 2026-09-04 → 10-20 | [spike-notes §9, §23](spike-notes.md), database |
| Rune Factory: Guardians of Azuma | Unreal Engine (version not in the executable) | D3D12 | DLSS Frame Generation ×4 (128.9 → 513.0); FSR frame generation compiled into the game counted ×2 but not named; ray tracing *No* (the game has none) | 2026-08-20 → 09-06 | [spike-notes §9](spike-notes.md) |
| Kingdom Come: Deliverance II | — | D3D12 | FSR (3.1 or 4) at 1505×847 → 2560×1440; frame generation *none* (the game ships none); ray tracing *No* | 2026-09-04 | [spike-notes §9](spike-notes.md) |
| Lies of P | Unreal Engine 4.27.2 | D3D12 | FSR 3 at 1506×848 → 2560×1440 with FSR frame generation ×2.04 (57.1 → 116.4) in the app; frame generation off read *none*; DLSS shown as *driver-reported*; ray tracing *No*; on 2026-10-19 and 10-20, FSR 3 at 1707×961 and at 1280×720 → 2560×1440 with FSR frame generation ×2.0 (54.6 → 109.6, 57.6 → 116.7) and no FSR quality mode — the game never asked for one while the hook was in | 2026-08-03 → 10-20 | [spike-notes §7, §9, §14, §22, §23](spike-notes.md), database |
| Onimusha: Way of the Sword (demo) | RE Engine | D3D12 | DLSS + DLSS Frame Generation: ×3.67 and ×3.93 with the capture tool, ×4.00 in the app's steady state (60 → 240 FPS), ×3.0 (60 → 180) in the app on 2026-09-22; ray tracing *Yes* | 2026-09-05 → 09-22 | [spike-notes §9, §14](spike-notes.md), database |
| Cronos: The New Dawn (demo) | Unreal Engine (version not in the executable) | D3D12 | DLSS with frame generation off (105 FPS, *none*); on ×2 (86.8 → 172.7), DLSS Frame Generation named; ray tracing *Yes* | 2026-09-06 → 09-16 | [spike-notes §9, §14](spike-notes.md) |
| Alan Wake 2 | — | D3D12 | Path tracing Ultra with DLSS and Ray Reconstruction: ray tracing *Yes* (96.8 % of frames), frame generation read ×1.00 (the game did not apply the setting that run). On 2026-09-04: DLSS with ray tracing *Yes* (99 % of frames), 92.5 FPS, frame generation *none* by count; then ray tracing off (*No*), DLSS, Presented 114.5 FPS. The quality preset was never read | 2026-08-04 → 09-04 | [spike-notes §8](spike-notes.md), run reports |
| SILENT HILL 2 | Unreal Engine 5.1 | D3D12 | DLSS Frame Generation identified, frames not counted (Presented 78 FPS) on 2026-09-25; then under the NVIDIA App's *Fixed ×6* ×6.0 (53.3 → 320.0) and under its *Dynamic, up to ×6* states ×2.0, ×3.0 and ×6.1; DLSS *driver-reported*; ray tracing *Yes* | 2026-09-25 → 10-20 | database, [spike-notes §22, §23](spike-notes.md) |
| DARK SOULS III | FromSoftware | D3D11 | Presented 59.8 FPS; no upscaler, frame generation or ray tracing loaded; then 34 sessions at the game's 60 FPS cap, the longest 6 h 12 min, four of them ended by the game crashing | 2026-09-03 → 10-25 | [spike-notes §9](spike-notes.md), database |
| Sekiro: Shadows Die Twice | FromSoftware | D3D11 | Presented 60 FPS (the game's cap): two sessions of 15 min and 2 h 18 min, then three more of up to 1 h 6 min | 2026-09-26 → 10-29 | database |
| Yu-Gi-Oh! Master Duel | Unity 6000.0 | D3D11 | Presented 239 FPS across six sessions (up to 5 h 25 min) | 2026-09-22 → 09-25 | database |
| GIRLS' FRONTLINE 2: EXILIUM | Unity 2019.4 | D3D11 | Presented 53–101 FPS; the last three sessions under the user-mode anti-cheat exception (NetEase Yidun), one of them 3 h 9 min, all ended normally; ten more under the exception, 28–72 FPS, the longest 2 h 9 min, all ended normally | 2026-09-21 → 10-20 | database, [HANDOFF](HANDOFF.md) |
| Aniimo | Unity 2022.3 | D3D12 | DLSS at 1707×960 → 2560×1440, frame generation *none* (×1.00, 205 FPS), ray tracing *No* — then blocked (below) | 2026-09-22 | database |
| AbyssMemory | Unreal Engine 4.26.1 | D3D11 | Presented 55 FPS, one short session | 2026-09-22 | database |
| Umamusume: Pretty Derby | Unity 2022.3 | D3D11 | Presented 37 FPS, one session | 2026-09-27 | database |
| 恋しかるべき (KOISHIKARUBEKI) | Unity 6000.3 | D3D11 | Presented 59.6 FPS | 2026-09-03 | [spike-notes §9](spike-notes.md) |
| Flower in Us | RPG Maker MV/MZ (NW.js) | D3D11 | Presented 59.8 FPS over three swap chains, after two refusals that found the game's browser process ambiguous (fixed) | 2026-09-03 → 09-04 | [spike-notes §9](spike-notes.md) |
| UMIGARI | Unreal Engine 5.5.4 | D3D12 | Presented 95–97 FPS, frame generation *none*, ray tracing *No* | 2026-09-05 → 09-06 | run reports only |
| Everhex Demo | — | Vulkan | Presented 239.7 FPS (1 % low 213.5), 14,955 frames in 69 s, through FrameLedger's Vulkan layer — the first real Vulkan game measured; started by Play set to *Start directly* | 2026-10-13 | database *(row added 2026-10-14 from a copy of the database)* |
| The Witcher 3: Wild Hunt — Remastered | — | D3D12 | DLSS 1707×960 → 2560×1440 with DLSS Frame Generation ×2.0 (118.8 → 237.4 FPS); ray tracing and Ray Reconstruction *Yes*; under the NVIDIA App's *Fixed ×6* it still presented ×2.0 — the setting is not the frames | 2026-10-04 → 10-20 | database, [spike-notes §23](spike-notes.md) |
| Phantom Blade Zero | Unreal Engine (version not in the executable) | D3D12 | DLSS *driver-reported*; DLSS Frame Generation under the NVIDIA App's *Dynamic* mode: up to ×6 held ×4.0 (59.5 → 235.6 FPS) and ×5.2 (44.3 → 231.2), up to ×3 held ×3.0 (58–76 → 175–227) for 75–94 % of each session longer than an hour; ray tracing *Yes*; thirteen sessions, the longest 6 h 29 min (5.26 million frames); up to 9.6 GB of graphics memory | 2026-11-01 → 11-05 | database, [spike-notes §26](spike-notes.md) |
| Hogwarts Legacy | Unreal Engine (version not in the executable) | D3D12 | With the game's *Neural Rendering* on, DLSS 5's runtime (`nvngx_dlssnr.dll` 310.9.16.0) loaded from the game's folder — the first game seen loading it; DLSS *driver-reported*; DLSS Frame Generation not counted (non-uniform, ×1.97 overall), Presented 140.2 FPS; ray tracing *Yes*, Ray Reconstruction *No* | 2026-11-06 | database, [spike-notes §26](spike-notes.md) |
| Valor Mortis (Xbox app) | Unreal Engine | D3D12 | DLSS Frame Generation ×6.0 (39.9 → 239.2 FPS), DLSS *driver-reported*, ray tracing *No*; recorded in attach mode | 2026-11-04 | database |
| inKONBINI (Xbox app) | Unity 6000.0 | D3D11 | Presented 205.9 FPS, one short session | 2026-11-03 | database |
| DARK SOULS II: Scholar of the First Sin | FromSoftware | D3D11 | The game's 60 FPS cap, nine sessions up to 5 h 29 min; one was unhooked by the safety check and recorded on | 2026-10-09 → 10-12 | database |
| Mithril | Unreal Engine (version not in the executable) | D3D12 | Presented 91.5–119.4 FPS, ray tracing *Yes*, four sessions up to 3 h 4 min | 2026-10-12 → 10-16 | database |
| KEEP IT ON HER | Unreal Engine 5.8 | D3D12 | Presented 59.5–108.8 FPS, ray tracing *Yes* | 2026-10-12 → 10-13 | database |
| WALTO | Unity 6000.3 | D3D12 | Presented 212.5 FPS over 2 h 41 min (2,057,022 frames) | 2026-10-08 | database |
| White Mask Mansion | Unity 6000.1 | D3D12 | Presented 286–309 FPS, five sessions | 2026-10-27 | database |
| LIMINAL: Within Demo | Unity 2022.3 | D3D11 | Presented 222.5–228.6 FPS | 2026-10-12 | database |
| Sleepytime Village | Unity 2021.3 | D3D11 | Presented 906–1,440 FPS (the game sets no cap) | 2026-10-25 → 10-26 | database |
| Scarlet Wolf | Unity 2021.3 | D3D11 | Presented 128.5 FPS | 2026-10-15 | database |
| March Out! Playtest | Unity 2022.3 | D3D11 | Presented 118.2 FPS | 2026-10-08 | database |
| A Space for the Unbound – Prologue (GOG) | Unity 2019.4 | D3D11 | Presented 61.0 FPS, started by FrameLedger | 2026-10-20 | database |

## Recorded without measurement (Tier 2)

| Game | Why nothing was measured | When | Evidence |
|---|---|---|---|
| HELLO, HELLO WORLD! | A 32-bit game (RPG Maker MV): refused by the guard once, and two sessions with hooking switched off — each recorded with its duration and sensors. *Its itch.io download puts malware in front of the game — do not run it (noted 2026-10-13, [spike-notes §20](spike-notes.md))* | 2026-09-23 | database |
| Umamusume: Pretty Derby | Three sessions played with hooking switched off for the game | 2026-09-27 | database |
| Sekiro: Shadows Die Twice | Two sessions played with hooking switched off | 2026-09-25, 10-07 | database |
| GIRLS' FRONTLINE 2: EXILIUM | One session whose process Windows would not let FrameLedger open, and two recovered after the agent itself stopped mid-session (*interrupted*) | 2026-09-22 → 09-23 | database |
| Bound Between Desks | Two sessions with hooking switched off (a Ren'Py 8.5 game) | 2026-10-15 → 10-20 | database |
| Phantom Blade Zero | Three sessions after a game update changed its executable on 2026-11-04, until hooking was consented to again on 11-05; one refused because a module list came back incomplete | 2026-11-04 → 11-05 | database |
| The Witcher 3: Wild Hunt — Remastered | Three sessions before hooking was consented to | 2026-10-13 → 10-20 | database |
| Mithril | Two sessions before hooking was consented to | 2026-10-16 | database |
| Hogwarts Legacy | One session with hooking switched off; Windows logged a crash of the game | 2026-11-06 | database |
| Valor Mortis, WALTO, Scarlet Wolf, Lies of P, A Space for the Unbound – Prologue | One session each with hooking switched off | 2026-10-08 → 11-04 | database |

## Refused by the anti-cheat guard

| Game | What was found | When | Evidence |
|---|---|---|---|
| ELDEN RING | Easy Anti-Cheat: its folder, and a process Windows would not let FrameLedger open. Nothing was ever injected; eight sessions recorded without measurement | 2026-09-21 → 09-27 | [CHANGELOG](../CHANGELOG.md), database |
| GIRLS' FRONTLINE 2: EXILIUM | NetEase Yidun (`NEP2.dll`, user mode) — turned hooking off after it had been measured; measured again since 2026-09-26 under the user-mode exception its owner made | 2026-09-25 | [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |
| Aniimo | NetEase Yidun with a kernel driver (`NEPKernel.sys`) in the game's folder — never eligible for an exception | 2026-09-25 | [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |
| Goose Goose Duck | Easy Anti-Cheat's service running, and its folder | 2026-08-04 | [spike-notes §13](spike-notes.md) |
| Neverness To Everness | Anti-Cheat Expert's kernel driver in the game's folder | 2026-08-04 | [spike-notes §13](spike-notes.md) |
| Wuthering Waves | Anti-Cheat Expert, kept beside the game's executable four folder levels in, where the check did not look before beta.21: a session with hooking on was injected and unhooked 37 seconds later, when the 30-second re-check met ACE's driver (`ACE-BASE.sys`); nothing was measured. Since beta.21 its folder and its Steam id refuse it before any injection *(row added 2026-10-13 from the agent's log)*; two sessions recorded without measurement on 2026-10-31 | 2026-10-12 → 10-31 | [spike-notes §19](spike-notes.md), database |
| Arknights: Endfield | Anti-Cheat Expert's folder (`AntiCheatExpert`) beside the game; one session recorded without measurement | 2026-10-31 | database |
| Stella Sora | Anti-Cheat Expert's folder (`AntiCheatExpert`); three sessions recorded without measurement | 2026-10-30 → 10-31 | database |
| Umamusume: Pretty Derby | CrackProof, on FrameLedger's title list since beta.11 (Steam 3224770) — measured before the list named it (above) | 2026-10-03 | [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |
| Counter-Strike 2 | Valve Anti-Cheat — refused by FrameLedger's title list since beta.8. On 2026-08-04 the checks alone allowed it (VAC is invisible to them); nothing was injected | 2026-08-04, beta.8 | [spike-notes §13](spike-notes.md), [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |

Also refused before any injection: **Deadly Heart Gambit** — a 32-bit game (2026-08-03).

## Engine version read from the executable only

The Unreal Engine version (beta.10) was checked against ten installed Unreal games without running them: Hell Is Us
5.5.4, Black Myth: Wukong 5.0.0, Clair Obscur: Expedition 33 5.4.4, Lies of P 4.27.2, SILENT HILL 2 5.1, UMIGARI 5.5.4,
AbyssMemory 4.26.1, **Martha Is Dead 4.27.0**; Cronos (demo) and Rune Factory name no version in their executables and
read *not found*. ([spike-notes §15](spike-notes.md), [05_DETECTION §Engine version](05_DETECTION.md)) Read since on the games
in the database: KEEP IT ON HER 5.8 and Wuthering Waves 4.26.2; Phantom Blade Zero, Hogwarts Legacy and Mithril name no
version in their executables, and an Xbox game's executable cannot be read outside its package (Valor Mortis).
