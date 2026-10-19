-- 0021 (2026-10-19, beta.24, owner decision D64): where each loaded runtime module's file lived, by class.
--
-- One JSON column beside runtime_modules ({file: version}, P2): {file: "game" | "graphics_driver" | "windows"}, from the
-- path the module snapshot read through the loader's list (Application.Capture.RuntimeModuleSources) - the class is
-- stored, the path never is (a path names a user folder; the Privacy Policy says the folder is not kept). It is what
-- tells the NVIDIA App's DLSS override, which loads the driver's own copy from %ProgramData%\NVIDIA, from the library the
-- game ships (05_DETECTION section DLSS generation). Written by the Agent only (06_DATA_MODEL section Writer ownership);
-- ADD COLUMN only. A session written before has none; a session recovered from its crash file has neither column.

ALTER TABLE sessions ADD COLUMN runtime_module_sources TEXT;
