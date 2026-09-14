-- 0004 (P4 PR-1, 2026-09-14): the detection cache key's exe half, as its own columns.
--
-- 05_DETECTION §Caching keys a game's static detection on path + size + mtime + rulesVersion
-- (DetectionCacheKey). 0001 gave the rules half a column (detection_rules_version) and it looked
-- as if games.exe_size_bytes / exe_mtime_ms were the other half -- but those two are the
-- CONSENT fingerprint (SqliteGameConsentStore refuses a mismatching fingerprint while a block
-- stands, 19_SAFETY), written when the user consents and never by a background scan. A sweep
-- that refreshed them after a game patch would silently re-point a consent at a binary nobody
-- consented to. So the detection key gets its own two columns and touches nothing the gate reads.
--
-- NULL = never scanned by this build's sweep. The Agent writes both beside detection_rules_version
-- (06_DATA_MODEL §Writer ownership, the detected columns); the UI reads them only to say "scanned".
--
-- ALTER TABLE ADD COLUMN only: 0001..0003 are applied on real machines and are never edited.

ALTER TABLE games ADD COLUMN detection_exe_size_bytes INTEGER;
ALTER TABLE games ADD COLUMN detection_exe_mtime_ms INTEGER;
