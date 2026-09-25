-- 0012 (2026-09-25): the NVIDIA driver profile a session's executable ran under (beta.8, owner request: "detect whether
-- the NVIDIA App overrides DLSS settings — FG, RR, the model preset").
--
-- The NVIDIA App writes its per-game DLSS / frame-generation overrides into the driver's own settings store (DRS). The
-- Agent reads, through the NVAPI bridge's read-only FlNvDriverProfile, which profile the driver applies to the
-- executable and the values it gives those settings (Application.Capture.NvidiaDriverSettings), beside each session
-- and in both tiers — it needs the executable's path and nothing of the game's process. JSON
-- (Application.Recording.DriverProfileRecord): the outcome (Application | Global | Degraded), the profile's name, and one
-- entry per setting with its value (null = set in no profile) and where the value comes from.
--
-- A fact about the driver's configuration, not a measurement: what a session MEASURED stays the hooks' (03_METRICS
-- §Upscaling). Written by the Agent (06_DATA_MODEL §Writer ownership); NULL for every row written before, and for a row
-- recorded where no NVIDIA bridge was there to ask.

ALTER TABLE sessions ADD COLUMN driver_profile TEXT;
