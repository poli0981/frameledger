-- 0018 (2026-10-03, beta.12, owner decision D43): the game process's own memory, both tiers (03_METRICS §Game process
-- memory), and every sensor series' statistics.
--
-- Read from OUTSIDE the game once a second: the GPU Process Memory performance counters for its dedicated and shared GPU
-- memory (Task Manager's two Details columns), GetProcessMemoryInfo for its private working set (Task Manager's "Memory"),
-- working set and commit. A hooked session reads the pinned process through the handle the session already holds; an
-- unpinned Tier-2 hold sums every process running the game's executable (game_memory_processes says how many).
--
-- NOT vram_proc_* (0001): those were defined as the in-process DXGI "local usage" compared with the budget, a different
-- quantity (dedicated reads a few MiB more per device, spike-notes §16 M3) that stays reserved for §H10. MiB throughout.
--
-- sensor_stats_json is every sensor series' n / mean / median / min / max, computed once at finalize so the summary's
-- statistics read STORED values (08_UI: stat cards are never recomputed) and outlive a retention sweep of the raw series.
--
-- Written by the Agent only (06_DATA_MODEL §Writer ownership: the sessions table is the recorder's); ADD COLUMN only.

ALTER TABLE sessions ADD COLUMN game_vram_dedicated_avg_mb REAL;
ALTER TABLE sessions ADD COLUMN game_vram_dedicated_median_mb REAL;
ALTER TABLE sessions ADD COLUMN game_vram_dedicated_max_mb REAL;
ALTER TABLE sessions ADD COLUMN game_vram_shared_max_mb REAL;
ALTER TABLE sessions ADD COLUMN game_ram_private_avg_mb REAL;
ALTER TABLE sessions ADD COLUMN game_ram_private_median_mb REAL;
ALTER TABLE sessions ADD COLUMN game_ram_private_max_mb REAL;
ALTER TABLE sessions ADD COLUMN game_ram_ws_max_mb REAL;
ALTER TABLE sessions ADD COLUMN game_commit_max_mb REAL;
ALTER TABLE sessions ADD COLUMN game_memory_processes INTEGER;
ALTER TABLE sessions ADD COLUMN game_memory_source TEXT;
ALTER TABLE sessions ADD COLUMN sensor_stats_json TEXT;
