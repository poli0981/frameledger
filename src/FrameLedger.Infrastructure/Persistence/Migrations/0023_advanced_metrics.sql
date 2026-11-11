-- 0023 (2026-11-11, owner decisions D101 and D102): what "Advanced metrics" stores.
--
-- Two tables of their own and no column on `sessions`: every read of a sessions row, the JSON export and the bug bundle
-- carry the whole row, and a driver profile does not belong in a bug bundle. The first CREATE TABLE since 0001.
--
-- machine_profiles  - the facts of the PC (the card's firmware and architecture, the driver's package, what a display
--                     can do, the driver's global profile), ONE ROW PER DISTINCT SET: `hash` is the SHA-256 of
--                     `facts_json`. Beside hardware_snapshots, not in it: that table's hash feeds the trend's
--                     hardware-change markers, and a set of facts that exists only while an option is on would raise a
--                     marker each time the option is switched.
-- session_advanced  - one row per session recorded with the option ON: its own facts and the state of each source
--                     (facts_json), the statistics of its advanced series (stats_json), what each series that is one of
--                     a family is (series_json), and the machine profile that was true when it ran. A session recorded
--                     with the option off has NO row - that absence is how the App says "the option was off when this
--                     was recorded" rather than "nothing answered". The series themselves are rows of sensor_blobs
--                     under names that start with `adv.`, with NaN for a tick that has no reading.
--
-- No identifier is in either (D104): no serial number, no unique id, no EDID, no other program's process id.
-- Written by the Agent only (06_DATA_MODEL section Writer ownership). A session deleted takes its row with it; a
-- machine profile no session points to any more is left, as a hardware snapshot is.

CREATE TABLE machine_profiles (
  id INTEGER PRIMARY KEY,
  hash TEXT NOT NULL UNIQUE,                   -- sha256 of facts_json, lower-case hex
  facts_json TEXT NOT NULL,                    -- Application.Advanced.AdvancedJson: a list of facts
  captured_at INTEGER NOT NULL                 -- unix ms: the start of the first session that had this set
);

CREATE TABLE session_advanced (
  session_id INTEGER PRIMARY KEY REFERENCES sessions(id) ON DELETE CASCADE,
  machine_profile_id INTEGER REFERENCES machine_profiles(id),   -- NULL: no source had a fact of the PC
  facts_json TEXT NOT NULL,                    -- the session's facts and each source's state
  stats_json TEXT,                             -- per advanced series: n, mean, median, min, max; NULL: it stored none
  series_json TEXT NOT NULL                    -- the directory of its family members, and how many series were left out
);

CREATE INDEX ix_session_advanced_profile ON session_advanced(machine_profile_id);
