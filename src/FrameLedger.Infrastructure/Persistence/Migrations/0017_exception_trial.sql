-- 0017 (2026-10-03, beta.11, owner decision D38): the user-mode anti-cheat exception's trial (19_SAFETY §The user-mode
-- exception).
--
-- D33 asked for two successful Tier-1 sessions before an exception could be granted. Since beta.8 the library sweep
-- blocks a game the moment it is added, so a blocked game could never be hooked to earn them, and a reset ledger lost the
-- ones it had: the exception was unreachable for every game blocked from the start. D38 grants without them; the first two
-- hooked sessions under a grant are its trial, and an end during the trial — a crash, a safety unhook, the Overlay's own
-- stop, a new finding, or a session that recorded no frame or did not end normally — ends the exception for good.
--
-- ac_exception_trial_failed_at is that "for good": when an exception of this game ended during its trial, NULL when none
-- did. Nothing clears it — not a withdrawal, a game update, Change executable or the end of a later grant (there is none:
-- while it is set the Agent finds the game not eligible and the store refuses a grant in SQL). A twin merge keeps the
-- earlier of two. ac_exception_sessions (0014) is now the trial's progress under the grant in force.
--
-- Written by the Agent only (06_DATA_MODEL §Writer ownership: the exception's columns are the Agent's); ADD COLUMN only.

ALTER TABLE games ADD COLUMN ac_exception_trial_failed_at INTEGER;
