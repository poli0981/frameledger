-- 0019 (2026-10-06, beta.14, owner decision D49): pacing, the present arguments, the graphics card's clocks and limits, and
-- efficiency (03_METRICS §Pacing, §Sync, §Sensor aggregates, §Efficiency).
--
-- Pacing (hooked sessions): the share of the measured time below 30 FPS, below 60 FPS and below the monitor's refresh rate,
-- and the mean change from one frame time to the next - over application frames where generated frames were counted, over
-- presents otherwise; the refresh share over presents always (it is the display's). A frame counts as below X when it is
-- 10 % longer than 1000 / X itself, or when the median of the nine around it is 2 % longer: without the margins a game
-- capped at 60 read half its time "below 60" from present-timing jitter alone (Domain.Metrics.FramePacing).
--
-- The present arguments (Direct3D only - OpenGL and Vulkan presents carry none): the share of presents that asked to wait
-- for the display (sync interval >= 1) and of those that allowed tearing. sessions.sync_interval_mode (0001) gets its
-- first writer with them; it needs no column here.
--
-- The card (both tiers, from the 1 Hz samples): average core and memory clock, average fan speed, the highest memory
-- temperature, and the share of ticks it was held back by its power limit or by heat - NVIDIA's layer only, null on every
-- other card. Efficiency (hooked): application frames per joule, FPS per watt of the card's board power - never displayed
-- frames (CLAUDE.md rule 6), and null wherever the application rate is not known.
--
-- Written by the Agent only (06_DATA_MODEL §Writer ownership); ADD COLUMN only, REAL throughout. A session written before
-- this script has none of them and reads N/A - never a zero.

ALTER TABLE sessions ADD COLUMN time_below_30_pct REAL;
ALTER TABLE sessions ADD COLUMN time_below_60_pct REAL;
ALTER TABLE sessions ADD COLUMN time_below_refresh_pct REAL;
ALTER TABLE sessions ADD COLUMN frametime_delta_mean_ms REAL;
ALTER TABLE sessions ADD COLUMN vsync_present_pct REAL;
ALTER TABLE sessions ADD COLUMN tearing_allowed_pct REAL;
ALTER TABLE sessions ADD COLUMN avg_gpu_core_clock_mhz REAL;
ALTER TABLE sessions ADD COLUMN avg_gpu_mem_clock_mhz REAL;
ALTER TABLE sessions ADD COLUMN avg_gpu_fan_rpm REAL;
ALTER TABLE sessions ADD COLUMN max_gpu_mem_temp REAL;
ALTER TABLE sessions ADD COLUMN power_limit_pct REAL;
ALTER TABLE sessions ADD COLUMN thermal_limit_pct REAL;
ALTER TABLE sessions ADD COLUMN app_frames_per_joule REAL;
