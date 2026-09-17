-- 0006 (2026-09-17): which part of the session fg_factor describes.
--
-- Until this date fg_factor was session-wide or NULL. On real play it was almost always NULL: every session opens
-- with a splash, a menu or a loading screen, FgWindow (rightly) refuses to average x1 menus with x4 gameplay, and
-- the refusal took the gameplay's own number with it -- one hooked session in nine published a factor on the
-- owner's machine (2026-09-16) while the live card, five seconds at a time, showed it throughout. The per-frame
-- counts in frame_blobs read 2.00 / 3.00 / 4.00 inside gameplay on every one of them.
--
-- Owner decision 2026-09-17 (03_METRICS §Frame Generation): when the session-level factor is refused as
-- non_uniform, publish the STEADY STATE -- the frame-generation state the session spent most of its generating
-- time in (Domain.Metrics.FgSteadyState: five-second windows, the live card's own; windows agreeing within 10 %;
-- at least 10 s and 10 % of the presenting time) -- and say so:
--
--   fg_factor_scope  'session' = one state for the whole claimed span (the only meaning fg_factor had before);
--                    'steady'  = fg_factor / native_fps / displayed_fps describe the steady state only;
--                    NULL      = no factor published, or the writer predates the column.
--   fg_steady_share  the fraction of the presenting time the steady state covers (0..1); NULL unless 'steady'.
--
-- fg_refusal and fg_refusal_detail stay set beside a 'steady' factor: they are why the number is not session-wide.
-- A reader that ignores these columns reads a steady factor as a session one, which is why every surface that
-- prints it prints the share beside it (08_UI §FPS display rule). Existing rows keep NULL: they are not
-- re-aggregated, though their frame_blobs would allow it.
--
-- ALTER TABLE ADD COLUMN only: earlier scripts are applied on real machines and are never edited (§Migrations).

ALTER TABLE sessions ADD COLUMN fg_factor_scope TEXT;
ALTER TABLE sessions ADD COLUMN fg_steady_share REAL;
