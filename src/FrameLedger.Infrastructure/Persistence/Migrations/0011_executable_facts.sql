-- 0011 (2026-09-25): what a game's files say about themselves (beta.8, owner request: game version, DLSS/FSR versions,
-- 32-bit or 64-bit).
--
-- The detection sweep already read the executable's PE version resource and walked the install tree for the files the
-- capability rules name, and kept neither: games.game_version is the STORE's word (a Steam build id, a GOG or Epic
-- version string) or the user's, under field_provenance, so the file's own versions get columns of their own.
--
--   exe_machine          what the executable runs as, from its PE headers: x64 | x86 | arm64 | arm | anycpu |
--                        anycpu32 | other | unknown (Domain.Detection.ExecutableArchitecture). NULL = not looked at by
--                        this build yet, which the sweep reads as stale; 'unknown' = looked at and unreadable.
--   exe_file_version     the executable's PE FileVersion string (for an Unreal title, often the ENGINE's version)
--   exe_product_version  its PE ProductVersion string
--   library_versions     JSON array: the capability files the game SHIPS, one object per file —
--                        {"capability","path","fileVersion","productVersion"}. What the driver or the NVIDIA App loads
--                        at run time can differ; that is sessions.runtime_modules.
--
-- Written by the Agent's detection sweep only, whole, like capability_flags: facts about files have no user override.

ALTER TABLE games ADD COLUMN exe_machine TEXT;
ALTER TABLE games ADD COLUMN exe_file_version TEXT;
ALTER TABLE games ADD COLUMN exe_product_version TEXT;
ALTER TABLE games ADD COLUMN library_versions TEXT;
