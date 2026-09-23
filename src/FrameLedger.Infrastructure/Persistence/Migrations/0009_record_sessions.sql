-- 0009 (2026-09-23): a library entry can be left unrecorded (owner request; HANDOFF D29).
--
-- Borderless Gaming starts with Windows on the owner's machine, and since Tier 2 exists (2026-09-22) every library
-- entry that runs gets a session: one per boot, for as long as the utility runs, counted in the playtime totals.
-- record_sessions = 0 means "FrameLedger does not watch for this program": no session, no measurement, no injection.
-- It is the user's switch (the game page), written by the App; the Agent only reads it (06_DATA_MODEL §Writer ownership).
--
-- The one data change: entries an import made for Steam's own tools before the import learnt to skip them
-- (SteamLibrarySource.KnownTools, 2026-09-22) start with the switch off. The ids are that list as this script was
-- written; a test holds every id here to the list in code. A tool added to the list later is skipped by the import,
-- and an entry that already exists for it keeps its switch until the user turns it off.

ALTER TABLE games ADD COLUMN record_sessions INTEGER NOT NULL DEFAULT 1;

UPDATE games SET record_sessions = 0
WHERE platform = 'steam'
  AND store_id IN ('228980', '250820', '365670', '388080', '431960', '1070560', '1391110', '1493710', '1628350', '2180100');
