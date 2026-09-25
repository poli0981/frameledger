-- 0013 (2026-09-25): where the charted stream's first present sits on the session's clock (beta.8, owner request: "fix
-- the charts that are not reasonable").
--
-- The frame series are timed from their first present (frametimes are intervals, 06_DATA_MODEL §Blob encoding) and the
-- sensor series from qpc_epoch (sensor_blobs' t_ms), and nothing said how far apart the two zeros were: the game's launch
-- and its loading screen lie between them, so the frametime chart's sensor overlay was drawn tens of seconds early. This
-- column is that distance — the first present of the stream the charts draw (SegmentBuilder.DominantStream, the rule the
-- session's statistics use), in milliseconds from qpc_epoch — written by the finalizer with the frames.
--
-- NULL for every row written before, whose overlay stays off rather than drawn out of step. Gaps are still collapsed in the
-- frame time axis (a gap's interval is stored as 0), so a series drifts ahead of its sensors by every gap it held.

ALTER TABLE frame_blobs ADD COLUMN first_present_ms REAL;
