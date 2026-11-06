-- 0022 (2026-11-06, beta.34, owner decision D84): DLSS 5's neural rendering, measured.
--
-- The share (0-100) of the frames a game rendered through Streamline on which it evaluated DLSS 5's neural rendering
-- (kFeatureDLSS_NR, Streamline 2.14), from the Overlay's per-frame fact (FL_FEAT_SL_NEURAL_RENDERING) over the presents that
-- drained a Streamline evaluation (Domain.Metrics.NeuralRendering). NULL where nothing could tell: an Overlay older than
-- this script decodes no neural rendering (its writer state lacks FL_HOOK_SL_NEURAL_RENDERING), and a title that never went
-- through Streamline drains no batch. Written by the Agent only (06_DATA_MODEL section Writer ownership); ADD COLUMN only.
-- A session written before has none and is never rebuilt: frame_blobs keep no feature byte.

ALTER TABLE sessions ADD COLUMN dlss_nr_pct REAL;
