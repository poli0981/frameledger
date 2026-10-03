# Games FrameLedger has been run with

*As of 2026-09-27 (0.1.0-beta.10). Built from a copy of the developer's own FrameLedger database and the repository's
measurement notes, and reviewed by the project's owner before it was published. It lists what happened on one machine;
it promises nothing about any other.*

*(Not re-read for 0.1.0-beta.11 or 0.1.0-beta.12 — noted 2026-10-04. Refreshing it needs a new copy of the database and
the owner's review; sessions since 2026-09-27, and the game-memory figures beta.12 adds, are not here.)*

**The machine.** Every row below was run on one PC: an NVIDIA GeForce RTX 5080 (the only graphics card), an Intel Core
i7-14700KF, 32 GB of DDR5, Windows 11 (Insider builds 26300 → 29648) and a 2560×1440, 240 Hz monitor. **No AMD or Intel
graphics card and no second machine has been tested**, and **no real Vulkan or OpenGL game has been measured** — every
measurement below is Direct3D 11 or 12. What the tiers mean and what FrameLedger cannot do: [LIMITATIONS.md](../LIMITATIONS.md).

**Two periods.** From 2026-08-03 to 2026-09-06 the games were measured with the developer's capture tool, one run at a
time against the game's own settings menu (reports in [spike-notes.md](spike-notes.md)). From 2026-09-16 the shipped
app and agent recorded them as a user would; the database holds the sessions from 2026-09-22 on — earlier ones went
with the 2026-09-16 recovery and with library entries removed since.

## Measured — hooked (Tier 1)

| Game | Engine | API | What was measured | When | Evidence |
|---|---|---|---|---|---|
| Cyberpunk 2077 | — | D3D12 | DLSS Balanced 1485×835 → 2560×1440, exact against its settings file; DLSS Frame Generation off ×1.00, ×3 (61.4 → 183.5 FPS), ×4 (58.7 → 234.1); FSR 3 + FSR frame generation ×2 (74.3 → 148.6); ray tracing *Yes* with path tracing on and *No* with it off, as set | 2026-08-15 → 09-05, about 25 runs | [spike-notes §8, §9, §11](spike-notes.md) |
| Dying Light: The Beast | — | D3D12 | DLSS Frame Generation ×4: Native 70.5 → Displayed 282.1 FPS, counted through DXGI; FSR + FSR frame generation ×2 (70.5 → 140.9); ray tracing *Yes* | 2026-09-04 → 09-06 | [spike-notes §9](spike-notes.md), [03_METRICS §Frame Generation](03_METRICS.md) |
| Hell Is Us | Unreal Engine 5.5.4 | D3D12 | DLSS Frame Generation ×4 (76.5 → 306.2); DLSS shown as *driver-reported* (it bypasses Streamline); FSR + FSR frame generation ×2 (85.7 → 171.4); XeSS frame generation shown as Presented FPS only; ray tracing *No* | 2026-09-03 → 09-16 | [spike-notes §8, §9](spike-notes.md) |
| Black Myth: Wukong | Unreal Engine 5.0.0 | D3D12 | DLSS Frame Generation ×4 (55.4 → 221.5); ray tracing *Yes* (16.2 % of frames in the app's session); a session that changed state showed its steady state (×4.1) instead of an average | 2026-08-20 → 09-16 | [spike-notes §9, §14](spike-notes.md) |
| Clair Obscur: Expedition 33 | Unreal Engine 5.4.4 | D3D12 | DLSS + Frame Generation ×4 (54.9 → 218.4) and ×2 (76.0 → 149.8); frame generation off read *none*, with DXGI agreeing; ray tracing *No* | 2026-09-04 → 09-06 | [spike-notes §9](spike-notes.md) |
| Rune Factory: Guardians of Azuma | Unreal Engine (version not in the executable) | D3D12 | DLSS Frame Generation ×4 (128.9 → 513.0); FSR frame generation compiled into the game counted ×2 but not named; ray tracing *No* (the game has none) | 2026-08-20 → 09-06 | [spike-notes §9](spike-notes.md) |
| Kingdom Come: Deliverance II | — | D3D12 | FSR (3.1 or 4) at 1505×847 → 2560×1440; frame generation *none* (the game ships none); ray tracing *No* | 2026-09-04 | [spike-notes §9](spike-notes.md) |
| Lies of P | Unreal Engine 4.27.2 | D3D12 | FSR 3 at 1506×848 → 2560×1440 with FSR frame generation ×2.04 (57.1 → 116.4) in the app; frame generation off read *none*; DLSS shown as *driver-reported*; ray tracing *No* | 2026-08-03 → 09-16 | [spike-notes §7, §9, §14](spike-notes.md) |
| Onimusha: Way of the Sword (demo) | RE Engine | D3D12 | DLSS + DLSS Frame Generation: ×3.67 and ×3.93 with the capture tool, ×4.00 in the app's steady state (60 → 240 FPS), ×3.0 (60 → 180) in the app on 2026-09-22; ray tracing *Yes* | 2026-09-05 → 09-22 | [spike-notes §9, §14](spike-notes.md), database |
| Cronos: The New Dawn (demo) | Unreal Engine (version not in the executable) | D3D12 | DLSS with frame generation off (105 FPS, *none*); on ×2 (86.8 → 172.7), DLSS Frame Generation named; ray tracing *Yes* | 2026-09-06 → 09-16 | [spike-notes §9, §14](spike-notes.md) |
| Alan Wake 2 | — | D3D12 | Path tracing Ultra with DLSS and Ray Reconstruction: ray tracing *Yes* (96.8 % of frames), frame generation read ×1.00 (the game did not apply the setting that run). On 2026-09-04: DLSS with ray tracing *Yes* (99 % of frames), 92.5 FPS, frame generation *none* by count; then ray tracing off (*No*), DLSS, Presented 114.5 FPS. The quality preset was never read | 2026-08-04 → 09-04 | [spike-notes §8](spike-notes.md), run reports |
| SILENT HILL 2 | Unreal Engine 5.1 | D3D12 | DLSS Frame Generation identified, frames not counted (Presented 78 FPS); DLSS *driver-reported*; ray tracing *Yes* | 2026-09-25 | database |
| DARK SOULS III | FromSoftware | D3D11 | Presented 59.8 FPS; no upscaler, frame generation or ray tracing loaded | 2026-09-03 | [spike-notes §9](spike-notes.md) |
| Sekiro: Shadows Die Twice | FromSoftware | D3D11 | Presented 60 FPS (the game's cap), two sessions of 15 min and 2 h 18 min | 2026-09-26 | database |
| Yu-Gi-Oh! Master Duel | Unity 6000.0 | D3D11 | Presented 239 FPS across six sessions (up to 5 h 25 min) | 2026-09-22 → 09-25 | database |
| GIRLS' FRONTLINE 2: EXILIUM | Unity 2019.4 | D3D11 | Presented 53–101 FPS; the last three sessions under the user-mode anti-cheat exception (NetEase Yidun), one of them 3 h 9 min, all ended normally | 2026-09-21 → 09-27 | database, [HANDOFF](HANDOFF.md) |
| Aniimo | Unity 2022.3 | D3D12 | DLSS at 1707×960 → 2560×1440, frame generation *none* (×1.00, 205 FPS), ray tracing *No* — then blocked (below) | 2026-09-22 | database |
| AbyssMemory | Unreal Engine 4.26.1 | D3D11 | Presented 55 FPS, one short session | 2026-09-22 | database |
| Umamusume: Pretty Derby | Unity 2022.3 | D3D11 | Presented 37 FPS, one session | 2026-09-27 | database |
| 恋しかるべき (KOISHIKARUBEKI) | Unity 6000.3 | D3D11 | Presented 59.6 FPS | 2026-09-03 | [spike-notes §9](spike-notes.md) |
| Flower in Us | RPG Maker MV/MZ (NW.js) | D3D11 | Presented 59.8 FPS over three swap chains, after two refusals that found the game's browser process ambiguous (fixed) | 2026-09-03 → 09-04 | [spike-notes §9](spike-notes.md) |
| UMIGARI | Unreal Engine 5.5.4 | D3D12 | Presented 95–97 FPS, frame generation *none*, ray tracing *No* | 2026-09-05 → 09-06 | run reports only |

## Recorded without measurement (Tier 2)

| Game | Why nothing was measured | When | Evidence |
|---|---|---|---|
| HELLO, HELLO WORLD! | A 32-bit game (RPG Maker MV): refused by the guard once, and two sessions with hooking switched off — each recorded with its duration and sensors | 2026-09-23 | database |
| Umamusume: Pretty Derby | Three sessions played with hooking switched off for the game | 2026-09-27 | database |
| Sekiro: Shadows Die Twice | One session played with hooking switched off | 2026-09-25 | database |
| GIRLS' FRONTLINE 2: EXILIUM | One session whose process Windows would not let FrameLedger open, and two recovered after the agent itself stopped mid-session (*interrupted*) | 2026-09-22 → 09-23 | database |

## Refused by the anti-cheat guard

| Game | What was found | When | Evidence |
|---|---|---|---|
| ELDEN RING | Easy Anti-Cheat: its folder, and a process Windows would not let FrameLedger open. Nothing was ever injected; eight sessions recorded without measurement | 2026-09-21 → 09-27 | [CHANGELOG](../CHANGELOG.md), database |
| GIRLS' FRONTLINE 2: EXILIUM | NetEase Yidun (`NEP2.dll`, user mode) — turned hooking off after it had been measured; measured again since 2026-09-26 under the user-mode exception its owner made | 2026-09-25 | [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |
| Aniimo | NetEase Yidun with a kernel driver (`NEPKernel.sys`) in the game's folder — never eligible for an exception | 2026-09-25 | [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |
| Goose Goose Duck | Easy Anti-Cheat's service running, and its folder | 2026-08-04 | [spike-notes §13](spike-notes.md) |
| Neverness To Everness | Anti-Cheat Expert's kernel driver in the game's folder | 2026-08-04 | [spike-notes §13](spike-notes.md) |
| Counter-Strike 2 | Valve Anti-Cheat — refused by FrameLedger's title list since beta.8. On 2026-08-04 the checks alone allowed it (VAC is invisible to them); nothing was injected | 2026-08-04, beta.8 | [spike-notes §13](spike-notes.md), [19_SAFETY](19_SAFETY_AND_ANTICHEAT.md) |

Also refused before any injection: **Deadly Heart Gambit** — a 32-bit game (2026-08-03).

## Engine version read from the executable only

The Unreal Engine version (beta.10) was checked against ten installed Unreal games without running them: Hell Is Us
5.5.4, Black Myth: Wukong 5.0.0, Clair Obscur: Expedition 33 5.4.4, Lies of P 4.27.2, SILENT HILL 2 5.1, UMIGARI 5.5.4,
AbyssMemory 4.26.1, **Martha Is Dead 4.27.0**; Cronos (demo) and Rune Factory name no version in their executables and
read *not found*. ([spike-notes §15](spike-notes.md), [05_DETECTION §Engine version](05_DETECTION.md))
