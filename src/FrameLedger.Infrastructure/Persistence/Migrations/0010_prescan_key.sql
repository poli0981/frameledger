-- 0010 (2026-09-25): the Agent pre-scans the library (beta.8; 19_SAFETY §What a finding does to the game).
--
-- Until now the static pre-scan ran only when the user asked to turn hooking on, so a game's anti-cheat was known only
-- after that request or after a hooked session found it: most rows sat at hook_prescan_state = 'not_run', and the App
-- could not tell an anti-cheat game from any other. The Agent now scans every entry itself, and again whenever the
-- rules or the executable change — which needs to know what each row was last scanned against. These three columns are
-- that key, the pre-scan's own (as 0004's detection_* columns are the detection sweep's): the rules version the scan
-- ran under and the executable's size and mtime at the time. NULL is "never scanned by the sweep".
--
-- Written by the Agent only (06_DATA_MODEL §Writer ownership: hook-state columns are the Agent's).

ALTER TABLE games ADD COLUMN hook_prescan_rules_version TEXT;
ALTER TABLE games ADD COLUMN hook_prescan_exe_size_bytes INTEGER;
ALTER TABLE games ADD COLUMN hook_prescan_exe_mtime_ms INTEGER;
