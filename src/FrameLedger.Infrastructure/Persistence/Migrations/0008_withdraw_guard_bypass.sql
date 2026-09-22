-- 0008 (2026-09-22): the guard bypass is withdrawn (owner decision; 19_SAFETY §What a finding does to the game).
--
-- 0007 added five columns for the one day the guard had an override. The owner withdrew it before any release carried
-- it past 0.1.0-beta.4: a bypass that cannot open a process an anti-cheat driver protects, and that merely lets the
-- Overlay be loaded beside an anti-cheat that WILL see it, was a switch whose only reliable effect was a ban. Nothing
-- reads or writes these columns any more, and this script removes them so the schema says what the code does.
--
-- THE ONE DEPARTURE from "ALTER TABLE ADD COLUMN only" (06_DATA_MODEL §Migrations), and the reasons it is safe here:
--   * SQLite has supported ALTER TABLE ... DROP COLUMN since 3.35.0 (2021-03); Microsoft.Data.Sqlite bundles a newer
--     engine, and LedgerDatabaseTests asserts the version floor beside this script.
--   * none of the five is indexed, referenced, generated, or part of a constraint; guard_bypass_disclosure_version is
--     NOT NULL DEFAULT '' and drops like any other column.
--   * the data being dropped is the bypass acknowledgements themselves. A row that carried one loses it, which is the
--     decision: no game keeps a standing permission to inject past a finding. The mark on a session
--     (guard_bypassed = 1) is dropped with the columns; capture_notes still carries "guard-bypass=" for the sessions
--     that ran under it, so the ledger does not forget that they did.
--
-- Earlier scripts are still never edited; 0007 stays as applied.

ALTER TABLE sessions DROP COLUMN guard_bypass_signal;
ALTER TABLE sessions DROP COLUMN guard_bypass_family;
ALTER TABLE sessions DROP COLUMN guard_bypassed;
ALTER TABLE games DROP COLUMN guard_bypass_disclosure_version;
ALTER TABLE games DROP COLUMN guard_bypass_at;
