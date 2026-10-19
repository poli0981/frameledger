# Owner-only items that passed

*Moved verbatim from `docs/HANDOFF.md` §Owner-only on 2026-10-19 (beta.24, owner decision D66): the items the owner reported passed or took, numbers kept — a number is never reused. The open ones stay in `docs/HANDOFF.md`.*

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

