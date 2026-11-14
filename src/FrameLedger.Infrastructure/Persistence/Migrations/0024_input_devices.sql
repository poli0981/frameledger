-- 0024 (2026-11-14, 0.1.0-beta.50, owner decisions D155-D163): what a session was played with, and whether a game's
-- store lists controller support (03_METRICS §Input, 21_INPUT_DEVICES).
--
-- sessions: milliseconds, read by the Agent from outside the game - so both tiers have them. `input_observed_ms` is the
-- time the reader ran and the session was not paused; `input_foreground_ms` is the part of it a window of the game was
-- in front, and the four classes add up to it: a controller in use, the keyboard or mouse in use (one class - only the
-- time of the last input is read), both, neither. `input_method_switches` counts the changes between a controller and
-- the keyboard and mouse. `input_source` names the sources that answered (rawinput, xinput, lastinput, joined by +).
-- `input_devices` is InputDevicesRecord's JSON: each controller's maker, model and connection, its time connected and
-- in use, its connections and drops, its battery; each maker's share; what the reader's sources did. No serial number,
-- no address, no path. The series of the timeline are sensor_blobs rows named input.* (NaN where there is no value).
--
-- These are columns of sessions, not a table of their own, on purpose (D160): the trend and the comparison read a
-- session's row, and the CSV/JSON export and the bug bundle carry it - the Privacy Policy's section 3A says so.
--
-- games: `controller_support` is what the store's own file on the disk says - 'full', 'partial', or 'none' for a game
-- the store knows and lists no support for; `controller_support_source` names the file ('steam-appinfo');
-- `controller_support_at` is when it was read (unix ms). No network request is made for it.
--
-- NULL on every row written before; on a session recorded with the option off, or before the terms that describe the
-- reading were accepted (D156); on a game no store file describes. ADD COLUMN only.

ALTER TABLE sessions ADD COLUMN input_observed_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_foreground_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_controller_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_km_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_both_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_idle_ms INTEGER;
ALTER TABLE sessions ADD COLUMN input_method_switches INTEGER;
ALTER TABLE sessions ADD COLUMN input_source TEXT;
ALTER TABLE sessions ADD COLUMN input_devices TEXT;

ALTER TABLE games ADD COLUMN controller_support TEXT;
ALTER TABLE games ADD COLUMN controller_support_source TEXT;
ALTER TABLE games ADD COLUMN controller_support_at INTEGER;
