*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 17 · Unity, Godot and RPG Maker versions in installed games *(2026-10-12, beta.19)*

Measured read-only on the owner's machine (i7-14700KF, RTX 5080, Windows 11 29648) before the readers were written:
every `UnityPlayer.dll`, `*.pck`, `rmmz_core.js`, `rpg_core.js` and `RGSS*.dll` up to five levels under `D:\another`
(GOG, itch and loose installs), `D:\SteamLibrary\steamapps\common`, Steam's own `common` and `G:\` — 23 installs, read in
9.6 s. Then again through the shipped pipeline (`StaticGameDetector` over `GameFileProbe`, rules `2026.10.2`), which read
every install as tabled. Nothing was copied or written; the decision rules are in `05_DETECTION` §Engine version — Unity,
Godot, RPG Maker.

| Engine | Installs | What the files say | Read as (shipped) | Detect time |
|---|---|---|---|---|
| Unity 2020+ | DONT GO, Dialoop, Broken Shell Demo, Deadly Heart Gambit, DreadFields, Kemono Teatime, March Out!, Umamusume, Walto, Yasha, Yu-Gi-Oh! Master Duel, 帰路, Legends of Runeterra | `UnityPlayer.dll` ProductVersion `2022.3.39f1 (4e1b0f82c39a)` … `6000.3.17f1 (cf0352b38e81)`; FileVersion `2022.3.39.5118735` (the build number where the release letter belongs) | the ProductVersion's editor version (`unity_player`) | 26 ms – 4.1 s |
| Unity 2019.4 | A Space for the Unbound - Prologue (GOG) | ProductVersion = FileVersion `2019.4.1.15122501`; `*_Data/globalgamemanagers` byte 20: `2019.4.1f1` | `2019.4.1f1` (`unity_data`) | 102 ms |
| Unity 2019.4 | GIRLS' FRONTLINE 2: EXILIUM | ProductVersion `2019.4.40.16762411`; the data file's header is not readable (encrypted) | `2019.4.40` (`unity_player_file`) | 2.2 s |
| Godot 4 | Plethora of Bullets 2 (itch), 衔蝶 (Steam) | `<exe>.pck` header `GDPC`, format 2, `4.2.2` / `4.4.1`; the exe's `Godot Engine v4.4.1.stable.official` sits 57–78 MB in; 衔蝶's version resource is the studio's (`0.1.0.3`) | `4.2.2`, `4.4.1` (`godot_pck`) | 24–41 ms |
| RPG Maker MZ | A Doll's Locket - Remake (itch) | `js/rmmz_core.js` line 2 `// rmmz_core.js v1.10.0` (also under the runtime's `css\js\`) | MZ `1.10.0` | 41–53 ms |
| RPG Maker MV | Flower in Us (Steam), 996IR, HELLO, HELLO WORLD! (itch) | `www/js/rpg_core.js` `v1.6.2`, `v1.6.2`, `v1.6.1` (HHW's one folder above its runtime) | MV `1.6.x` | 70–275 ms |
| RPG Maker VX Ace | City of Bones, Träumerei Chapter 2 (itch) | `Game.ini`: `Library=System\RGSS301.dll`; the DLL's version `3, 0, 1, 1` | VX Ace, RGSS `3.0.1` | 427–442 ms |

- **Detect time** is the whole detection of one entry — the install's walk, the rules and the second read — on the USB
  drive; the second read itself is a version resource and at most a few kilobytes. It runs once per executable and rules
  version, like Unreal's.
- ~~**Four of the itch installs front their runtime with a 213 MB .NET launcher**~~ **Four of the itch installs put a
  213 MB .NET program in front of their runtime, and it is malware** *(corrected 2026-10-13, owner decision D57 — §20;
  do not run it)* (HELLO, HELLO WORLD!, A Doll's Locket - Remake — whose launcher asks for administrator —, City of
  Bones and Träumerei) — read as a launcher, the reason the import's choice of executable changed in the same train
  (`05_DETECTION` §Library import, D56).
- **Not measured:** RPG Maker 2000/2003 (`RPG_RT.exe`; none installed, and no rule detects it), XP and VX (no install —
  their RGSS1/RGSS2 libraries are read by the same rule as VX Ace's, unmeasured), a Unity build older than 2019 (its
  player is the executable, with no `UnityPlayer.dll`), a Godot 3 export, and a Godot pack appended to the executable
  (both installs ship the pack beside it; the trailer the reader follows is Godot's documented layout, tested on
  synthetic files only).

