-- 0016 (2026-09-27, beta.10, owner request: "detect the game's window size and whether it runs full-screen, borderless or
-- windowed, and how much of the time"): a session's display facts (03_METRICS §Display mode).
--
-- Milliseconds per mode, sampled on the capture loop's own ticks — every 100 ms hooked, every second held at Tier 2 — each
-- sample standing for the time until the next, a pause not counted. `covers` is a window covering its monitor that nothing
-- could say was exclusive or not (OpenGL, Vulkan, Tier 2); `nowindow` is time no window of the game could be read, and is
-- outside every share. The sizes are the ones seen in the mode that lasted longest. `display_source` is the strongest thing
-- any sample rested on: swapchain (DXGI asked), opengl, window (geometry only).
--
-- NULL on every row written before, on a session recovered from its .partial file (the tally is not in the crash file) and
-- where the loop never sampled. ADD COLUMN only.

ALTER TABLE sessions ADD COLUMN display_exclusive_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_borderless_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_windowed_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_covers_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_minimized_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_nowindow_ms INTEGER;
ALTER TABLE sessions ADD COLUMN display_changes INTEGER;
ALTER TABLE sessions ADD COLUMN display_source TEXT;
ALTER TABLE sessions ADD COLUMN display_window_w INTEGER;
ALTER TABLE sessions ADD COLUMN display_window_h INTEGER;
ALTER TABLE sessions ADD COLUMN display_buffer_w INTEGER;
ALTER TABLE sessions ADD COLUMN display_buffer_h INTEGER;
ALTER TABLE sessions ADD COLUMN display_monitor_w INTEGER;
ALTER TABLE sessions ADD COLUMN display_monitor_h INTEGER;
ALTER TABLE sessions ADD COLUMN display_monitor_hz INTEGER;
