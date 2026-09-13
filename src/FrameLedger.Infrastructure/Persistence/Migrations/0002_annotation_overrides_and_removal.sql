-- 0002 — P3 PR-3 (2026-09-13): the UI's first writes need two things 0001 did not have.
--
-- 1. FR-8.3's per-session overrides. They live on session_annotations — the UI-owned table — and NOT on
--    sessions, which is the Agent's (06_DATA_MODEL §Writer ownership); and beside the measurement rather
--    than over it, so clearing an override restores what was measured. NULL = no override; the flag
--    columns on sessions keep meaning what the writer measured (rt_source stays 'measured').
-- 2. FR-1.4's "remove the game, keep its sessions": sessions.game_id is NOT NULL with ON DELETE CASCADE,
--    so keeping the sessions means keeping the row; removed_at marks it out of the library, and a later
--    launch of the same executable restores it (SqliteGameRepository.EnsureAsync).
--
-- ALTER TABLE ADD COLUMN only: 0001 is applied on real machines and is never edited (§Migrations).

ALTER TABLE session_annotations ADD COLUMN rt_override TEXT CHECK (rt_override IS NULL OR rt_override IN ('yes', 'no', 'na'));
ALTER TABLE session_annotations ADD COLUMN pt_override TEXT CHECK (pt_override IS NULL OR pt_override IN ('yes', 'no', 'na'));
ALTER TABLE session_annotations ADD COLUMN rr_override TEXT CHECK (rr_override IS NULL OR rr_override IN ('yes', 'no', 'na'));

ALTER TABLE games ADD COLUMN removed_at INTEGER;
