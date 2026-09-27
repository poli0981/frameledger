-- 0015 (2026-09-27, beta.10, owner request: "detect the exact Unreal Engine version"): where games.engine_version came from.
--
-- The detection sweep now reads an Unreal title's version from its shipping executable — the numeric file version and the
-- engine's branch name compiled into it (05_DETECTION §Engine version) — and the two witnesses are wrong in different ways,
-- so the App says which one a version rests on. The column holds a Domain.Detection.EngineVersionSource id; NULL for a
-- version the user typed and for every row until the sweep reads it again under rules 2026.09.6.
--
-- The UPDATE removes the values the old rule could only ever get wrong. Its extractor was a regex with two capture groups,
-- `\+\+UE(4|5)\+Release-(\d+\.\d+)`, and the evaluator returns the first — so every Unreal version it ever wrote was a bare
-- "4" or "5". Only detected values go: a value the user typed, or one with no provenance badge (which reads as the user's,
-- SqliteGameRepository.FieldProvenance), is left exactly as it is. A detection that later establishes nothing never
-- erases, so without this the digit would outlive the fix.

ALTER TABLE games ADD COLUMN engine_version_source TEXT;

-- CASE, not AND: SQLite promises no order for AND's operands, and json_extract on a malformed document is an error that
-- would fail the whole migration; a row whose provenance cannot be read is treated as the user's and left alone.
UPDATE games SET engine_version = NULL
WHERE engine = 'unreal'
  AND engine_version IN ('4', '5')
  AND CASE WHEN json_valid(field_provenance) THEN json_extract(field_provenance, '$.engine_version') END = 'detected';
