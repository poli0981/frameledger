# Owner-only items that passed

*Moved verbatim from `docs/HANDOFF.md` §Owner-only on 2026-10-19 (beta.24, owner decision D66): the items the owner reported passed or took, numbers kept — a number is never reused. The open ones stay in `docs/HANDOFF.md`. Items that passed later are added below as they pass (38 and 39 on 2026-11-12).*

3. ~~**Which titles** go in `blockedExecutables` — a product decision with false-refusal
   consequences. The list ships empty until it is taken.~~ **Taken 2026-09-25 (D30):** per-title
   lists by Steam id and executable name, seeded and floored (§2026-09-25 — the beta.8 train).
12. ~~**beta.15 on real hardware.**~~ **Passed 2026-10-06** — the owner ran it on real hardware and reported it OK ("đã
   chạy và ok"). *(The item as written:)* (a) **The update from beta.14:** Help ▸ Check for updates, then *Restart to update*
   with no game running — the first update in which beta.14's own step stops the Agent before Velopack applies; the
   `velopack:` lines in `ui-*.log` say what was downloaded. (b) **A quick restart:** close a hooked game and start it again
   within a few seconds; both sessions are recorded. (c) **A change of language:** English ↔ Tiếng Việt in Settings; the
   theme and channel lists, the Logs page's lists, the Games sort, the tray's menu and the Agent pill follow without a
   restart. (d) **A launcher:** `FrameLedger.Agent.exe --console launch --exe <a launcher that starts the game>`; the
   log has `election:` lines and the session is the game's. (e) **The viewer:** copy the data folder with SQLite's backup
   (`12_BUILD` §Debugging) and start `FrameLedger.exe --data-dir <copy>`; the banner names the folder, the pill reads
   *Viewer — no Agent*, the switches are off, and the installed App runs beside it untouched. (f) The `ja` strings
   marked `review`.
14. ~~**beta.18 on real hardware.**~~ **Passed 2026-10-08** — the owner ran it on real hardware and reported it OK ("Đã
   test và ok"). *(The item as written:)* (a) **The dark theme:** every page's title and section headings — Dashboard, Games, a
   game's page, Compare, Settings, Logs — read light on the dark window, after starting in Dark, after Light → Dark, and
   after moving between pages. (b) **The overlay build** on the Dashboard's *Capture agent* card reads `v0.1.0-beta.18`,
   without `-dirty`. (c) **Disable all hooking:** turning it on says a game being measured stops within 30 seconds; with
   it on, a hooked game's notice reads "All hooking is switched off in Settings, so nothing was measured." (d) The `ja`
   strings marked `review`, `Safety_Unhooked_Unnamed_Format` among them.
15. ~~**beta.19 on real hardware.**~~ **Passed 2026-10-12** — the owner ran it and reported (a), (c), (d)'s unplugged
   drive, (e) and (f) OK ("Các mục đã test: 1, 2. 5, 6, 4, ok"); (b) and (d)'s removal were read from the logs: Refresh
   named the existing entry instead of offering a twin, and one check at 18:04:00 removed Deadly Heart Gambit, Vibe Train
   Demo and 衔蝶 (the App's restart at 17:47 had started the ten minutes again), the library then reading *Games · 46*.
   *(The item as written:)* (a) **A Doll's Locket - Remake:** its page ▸ *Change executable…* opens in `Dolls
   Locket\`; pick `css\Game.exe`. Its launcher asks for administrator, so the runtime is measured only with *Run the agent
   as administrator* on — otherwise the session says why nothing was measured. *(Withdrawn 2026-10-13, D57: that
   launcher is malware — do not run it; `spike-notes.md` §20.)* (b) **Refresh** offers no second A Doll's
   Locket. (c) **Engine versions:** a Unity game's page (*Unity 2022.3.62f2* on Umamusume, *Unity 2019.4.40* on GIRLS'
   FRONTLINE 2), Godot's (*Godot 4.4.1* on 衔蝶), RPG Maker's (*RPG Maker MZ 1.10.0*, *RPG Maker MV 1.6.2* on Flower in
   Us, *RPG Maker VX Ace (RGSS 3.0.1)*), each with its *Engine version* line. (d) **Uninstall a game that has no session**
   (or delete a copy you added): about ten minutes later its entry is gone and the Games page says so — two removed
   together are said in one notice that names both; with the USB drive unplugged nothing is removed; *Remove uninstalled
   games* off keeps it. (e) The Legal Gate shows the Privacy Policy 2.8
   once. (f) The `ja` strings marked `review`.
16. ~~**beta.20 on real hardware.**~~ **Passed 2026-10-13** — the owner: "Tất cả các item đã đạt yêu cầu" (every item
   met its requirement). *(The item as written:)* (a) **Bound Between Desks:** its page reads *Ren'Py 8.5.0*, and its
   *Engine version* line says it was read from the engine's version file (`renpy/vc_version.py`); the first sweep after
   the update reads every game once more (rules `2026.10.3`). (b) The Legal Gate shows the Privacy Policy 2.9 once. (c)
   The `ja` string marked `review` (`GameDetail_EngineVersion_RenPyVcVersion`).
17. ~~**beta.21 on real hardware.**~~ **Passed 2026-10-13** — the same answer. *(The item as written:)* (a) **Wuthering
   Waves:** after the update, before any session, its page says hooking is off — Anti-Cheat Expert beside its
   executable, and its Steam id on the title list; Neverness To Everness stays blocked, and GIRLS' FRONTLINE 2's
   exception stays in force. (b) **Play**, in both modes of Settings ▸ Games ▸ *Play button*, with a Steam game, Alan
   Wake 2 from the Epic Games Launcher (its link has not been tried on a real game), a GOG game and an itch game — and
   in Task Manager's Details tab the game's *Elevated* column reads *No*. (c) **The wheel and a touchpad** in Help ▸
   User guide, the licence in Help ▸ About and the Legal Gate: about three lines a notch, gliding; at the end of the
   update notes the window scrolls instead. (d) The Legal Gate shows the Privacy Policy 2.10 once. (e) The `ja` strings
   marked `review` (Play, Settings ▸ Games, `GameDetail_Supports_None`).
18. ~~**beta.22 on real hardware.**~~ **Passed 2026-10-13** — the same answer. *(The item as written:)* (a) The `ja`
   string marked `review` (`Agent_Unanswered`). (b) Nothing else to run: the change answers a request the agent drops
   mid-way, which the tests make happen and a person can only meet by chance — if a notice ever says the capture agent
   did not answer, the page beside it shows what was stored.
19. ~~**beta.23 on real hardware.**~~ **Passed 2026-10-15** — the owner: "Item 19 hiện đã đạt 100% yêu cầu" (the item
   met every requirement). *(The item as written:)* (a) **Artwork:** open the page of a Steam game (one on the D: drive)
   and of a GOG game — the banner, the cover beside the name, the band's colour; an itch game shows none. With a page
   open, switch Dark → Light → Dark: the band changes with the theme and every text on it stays readable. (b) **Your own
   image:** *Edit… ▸ Choose image…* then Save — the page shows it; *Edit… ▸ Use the store's image* — the store's again;
   choosing an image and pressing Cancel leaves nothing new in `%LOCALAPPDATA%\FrameLedger\covers`. (c) **Settings ▸
   Games:** *Show game artwork* and *Colour the game page from its artwork* off, then on. (d) **Speed:** open WALTO's
   page and choose its longest session, switch the page's tabs; visit the Dashboard a few times, then end a session —
   one "Session saved". (e) The Privacy Policy 2.11 is shown once after the update. (f) The `ja` strings marked
   `review`.
38. ~~**beta.40, Advanced metrics inside a real game**~~ **Passed 2026-11-12** — the owner ran it on ten games over one night (beta.40, then beta.41), was shown what a copy of the ledger and the Agent's log said of those sessions, and answered: "Bạn có thể đánh dấu là pass và tiếp tục." What the sessions covered: (b) the option off storing nothing, on a DLSS title and on an FSR 3.1 one; (c) three titles upscaling through FSR 3.1 — Kingdom Come: Deliverance II (chosen in the game), Dying Light: The Beast and Clair Obscur: Expedition 33, the last two with **FSR upscaling beside DLSS frame generation, which is how the owner had set them in the game** — and Lies of P through FSR 3; (d) every DLSS title saying *the game made none of the calls*, none chaining its options into the evaluation; (e) the sentence under the session in each of its three forms; (g) Girls' Frontline 2 under the user-mode exception. **Not exercised, and passed on the owner's word**: (f) two sessions of one game that kept running; (h) the cost; (i) the words. **(j) is not done**: the SonarCloud acceptance is still the owner's to click, and stands in HANDOFF §Left open. Valor Mortis's one crash was the game's own (a shader-compilation error, its own crash dialog — the owner). *(The item as written:)* (D111–D116; train B's first release — beta.41 is not begun
   until this passes). (a) **The update from beta.39**: the terms window shows the **Privacy Policy 2.16** once, and
   not the Disclaimer; a game that was **running across the update** is refused until it is restarted, with the
   reason, and measured normally after. (b) **The option off, a hooked game that uses DLSS or FSR**: the game runs
   and is measured as in beta.39; the session is not on the Advanced page's list; nothing in the Agent's or the
   Overlay's log mentions the upscaler's options. (c) **The option on, a title that upscales through FSR 3.1 or
   later** (AMD's `amd_fidelityfx_*.dll` in its folder; a title on the older FSR 3.0 interface, such as one that
   ships `ffx_fsr3_x64.dll` alone, is expected to show none): on *Session*, under Upscaling, sharpening on or off
   and its strength against the game's own setting; the field of view as a range, against the game's slider (it is
   the **vertical** angle, in radians with degrees beside it) — and wider after zooming or aiming; the near and far
   planes; the flags; the chart of the frame time the game reported beside the session's own frame time; FSR's
   memory only if the game asks for it. (d) **The option on, a DLSS title**: most are expected to say *the game made
   none of the calls these are read from*. If one shows rows, this is the first sight of them anywhere: the output
   size against the display resolution, the preset letters against the NVIDIA app's override or the DLSS
   indicator, HDR input against the game's HDR setting. Write down which title did. (e) **The sentence under the
   session** says which of the three it was, and the session's summary still names the upscaler and its quality as
   before. (f) **Two sessions of one game that kept running**, both with the option on: the second one's field-of-
   view range and counts are its own, not the first one's carried over. (g) **A game under the user-mode anti-cheat
   exception**, the option on: it runs, the session is hooked, and its trial is not ended by anything new. (h) **What
   it costs**: the game's own frame rate with the option on and off, and the Agent's processor time in a hooked
   session (NFR-2: 2.5 % of one core). (i) **The words**: the fourteen new `vi` strings and the Settings text; the
   `ja` ones are marked `review`. (j) **SonarCloud**: accept `csharpsquid:S3869` on
   `src/FrameLedger.Infrastructure/Diagnostics/ParentDump.cs` again — the same call the acceptance of beta.24 covers,
   raised anew because #398 moved it (`13_CI_CD` §SonarCloud says why it is safe); until then `main`'s reliability
   rating on new code shows E for that one line.
39. ~~**beta.41, the sensor library on the PC that showed the fault**~~ **Passed 2026-11-12** — the same answer. Seven sessions on beta.41 as administrator with PawnIO, no `advanced:` line in the Agent's log, 212 or 213 series in each session with the option on; on beta.40 the same morning the processor device faulted out after each of six sessions. **Not exercised**: (c) the page after a session was not looked at by anyone but the owner; (d) the unelevated run; (e) no value beside HWiNFO — that stays item 37 (e). The rarer fault, the reader failing to open, was one session in sixteen; `20_OPEN_QUESTIONS` §M13 keeps the count. *(The item as written:)* (D118; `20_OPEN_QUESTIONS` §M13). (a) **The
   update from beta.40**: no terms window, the ledger opens as before; a game running across the update is refused
   until restarted, as after every update. (b) **As administrator with PawnIO, Advanced metrics on**: five or more
   sessions of different games, some with the Advanced page left open on *This PC* throughout, some with it closed,
   one started within a few seconds of another ending. **Afterwards the Agent's log (Logs page, or
   `agent-<date>.log`) holds no `advanced: a device is left alone` and no `advanced: the sensor library did not
   open`**, and every one of those sessions has its processor, memory and motherboard readings on *Session* (about
   two hundred series; a session with seventeen is the fault). (c) **The page after a session ends**: the
   processor's rows on *This PC* are still there and still moving, without reopening the page. (d) **Not as
   administrator**: nothing differs from beta.40 — the card's sensors and the driver's figures, and *Sources* says
   why the rest is not read. (e) While there, the part of item 37 (e) nobody has done: a few of the processor's and
   the motherboard's values beside HWiNFO.
