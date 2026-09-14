-- 0003 (2026-09-14): why a hooked session that IDENTIFIED frame generation carries no factor.
--
-- The owner's ledger held two rows with fg_mode = 'dlssg', fg_source = 'api' and fg_factor NULL: the
-- Streamline identity stood (the tags named DLSS-G, the census had sl.dlss_g.dll) and the count refused
-- (FgWindow.Refusal), and NOTHING recorded which refusal -- the Agent log does not narrate it and the row
-- had no column for it. Every FG surface in the UI then read N/A beside a detection that had succeeded,
-- with no way to say why (CHANGELOG, Fixed, 2026-09-14).
--
-- fg_refusal is FgRefusalKind as a token (Vocabulary.FgRefusal): not_counted, unattributed,
-- multiple_streams, count_saturated, dxgi_saturated, no_evaluations, too_short, non_uniform,
-- ambiguous_band, no_batches. NULL when a factor was published, or when the writer predates the column.
-- It is a REASON, never a measurement: nothing reads it to decide fg_mode or a number (03_METRICS §Frame
-- Generation). The Agent writes it (06_DATA_MODEL §Writer ownership: sessions is the Agent's table).
--
-- ALTER TABLE ADD COLUMN only: 0001 and 0002 are applied on real machines and are never edited (§Migrations).

ALTER TABLE sessions ADD COLUMN fg_refusal TEXT;
