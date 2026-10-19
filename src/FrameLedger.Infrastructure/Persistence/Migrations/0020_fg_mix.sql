-- 0020 (2026-10-19, beta.24, owner decision D62): every frame-generation state a session held, with its share of the
-- presenting time (03_METRICS §The mix).
--
-- One JSON column, written by the recorder's aggregation from the same five-second windows the steady state is read over
-- (Domain.Metrics.FgMix, stored through Application.Recording.FgMixRecord): each state's pooled factor, its own Native and
-- Displayed rates, its seconds and share; then the time with no frame generation and the time no state claims. Never an
-- average across states (CLAUDE.md rule 6). Written by the Agent only (06_DATA_MODEL §Writer ownership); ADD COLUMN only.
-- A session written before this script has none and is never rebuilt from its frame_blobs: they keep one Generated bit
-- per frame and lose the presents only DXGI counted, so a mix rebuilt from them would be wrong on exactly the titles that
-- need it.

ALTER TABLE sessions ADD COLUMN fg_mix TEXT;
