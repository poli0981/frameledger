-- 0014 (2026-09-26): the user-mode anti-cheat exception (beta.9, owner decision D33; 19_SAFETY §The user-mode exception).
--
-- A game whose only anti-cheat findings are one user-mode family, that ships no kernel driver, is on no title list and
-- has been measured hooked at least twice may be excepted by the user, per game, while Settings' option is on. The
-- block itself is never cleared: these columns are a separate layer over hook_blocked_reason, in three groups.
--
-- ELIGIBILITY, the Agent's sweep's answer about a blocked row: whether an exception may be granted, the guard's
-- tolerant pre-scan it rests on (Reason|Family|Signal; NULL = nothing was asked, the block is not a module, file or
-- folder finding), the successful Tier-1 sessions counted, and the key it was reached under — the rules version, the
-- executable's size and mtime, and the block text — so a pass re-scans only when one of them changes.
--
-- THE GRANT, the user's, after the exception's disclosure: when (NULL = no exception in force), for which family, under
-- which disclosure, and for which bytes of the executable (a game update ends it).
--
-- THE END of the last grant: when and why (UserModeExceptionLapse's words). The grant's family, disclosure and bytes
-- stay beside it as history.
--
-- sessions.ac_exception_family names the family a HOOKED session ran under the exception with; NULL for every other
-- session and for every row written before.
--
-- Written by the Agent only (06_DATA_MODEL §Writer ownership: hook-state columns are the Agent's); ADD COLUMN only.

ALTER TABLE games ADD COLUMN ac_exception_eligible INTEGER NOT NULL DEFAULT 0;
ALTER TABLE games ADD COLUMN ac_exception_verdict TEXT;
ALTER TABLE games ADD COLUMN ac_exception_sessions INTEGER NOT NULL DEFAULT 0;
ALTER TABLE games ADD COLUMN ac_exception_checked_rules_version TEXT;
ALTER TABLE games ADD COLUMN ac_exception_checked_exe_size_bytes INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_checked_exe_mtime_ms INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_checked_block TEXT;
ALTER TABLE games ADD COLUMN ac_exception_at INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_family TEXT;
ALTER TABLE games ADD COLUMN ac_exception_disclosure_version TEXT;
ALTER TABLE games ADD COLUMN ac_exception_exe_size_bytes INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_exe_mtime_ms INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_lapsed_at INTEGER;
ALTER TABLE games ADD COLUMN ac_exception_lapsed_reason TEXT;
ALTER TABLE sessions ADD COLUMN ac_exception_family TEXT;
