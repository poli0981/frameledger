-- 0007 (2026-09-21): the user's guard bypass, per game, and the mark it leaves on a session.
--
-- Owner decision 2026-09-21 (19_SAFETY §The user's bypass; CLAUDE.md rule 2 as amended that day). Until this date the
-- anti-cheat guard had no override of any kind. It has exactly one now, and this script is all the state it has:
--
--   games.guard_bypass_at                  when the user accepted the bypass disclosure for THIS game; NULL = off,
--                                          which is every existing row and every new one.
--   games.guard_bypass_disclosure_version  which text they accepted ('' = none). A timestamp without it is not an
--                                          acknowledgement, for the reason hook_consent_at without a provenance is
--                                          not consent; HookRequest.FromConsent reads both or neither.
--
-- There is NO global column, no settings key, no CLI switch and no field in rules/detection-rules.json, and there must
-- never be one: the bypass is a recorded human act about one executable, keyed like consent on the row's fingerprint,
-- and it lapses when that executable changes. It never touches hook_enabled, hook_consent_at or hook_blocked_reason --
-- the block stays on the row and stays true; the gate is what reads the bypass beside it.
--
--   sessions.guard_bypassed        1 when the session STARTED under the bypass (the guard refused on its own judgement
--                                  and the Overlay was injected anyway); 0 otherwise, including every earlier row.
--   sessions.guard_bypass_family   the anti-cheat family the guard named at that start, when it named one.
--   sessions.guard_bypass_signal   the module / driver / file that produced the finding.
--
-- A bypassed session is labelled on every surface that shows it and in every export. A session recovered from a
-- .partial after an Agent crash does not carry the mark (the verdict is known only to the live loop); its
-- capture_notes do not either. That is a recorded limit, not an oversight (06_DATA_MODEL).
--
-- ALTER TABLE ADD COLUMN only: earlier scripts are applied on real machines and are never edited (§Migrations).

ALTER TABLE games ADD COLUMN guard_bypass_at INTEGER;
ALTER TABLE games ADD COLUMN guard_bypass_disclosure_version TEXT NOT NULL DEFAULT '';
ALTER TABLE sessions ADD COLUMN guard_bypassed INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sessions ADD COLUMN guard_bypass_family TEXT;
ALTER TABLE sessions ADD COLUMN guard_bypass_signal TEXT;
