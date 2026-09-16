-- 0005 (2026-09-16): the numbers behind fg_refusal.
--
-- 0003 recorded WHICH refusal took the factor (non_uniform, multiple_streams, ...). On 2026-09-16 the owner's
-- Dashboard showed "DLSS-G active — factor not counted" on Hell Is Us, Black Myth: Wukong, Cronos and Onimusha, and
-- the tooltip could say only "the frame-generation state changed mid-session" — while FgWindow had computed which
-- of the eight buckets departed, its ratio and the whole window's, and SessionAggregator kept only the kind. This
-- column keeps the rest: FgRefusalDetail as JSON (Application.Recording.RecordingJsonContext) —
-- {"Kind":"non_uniform","Subject":"factor","Count":53,"BucketIndex":2,"BucketCount":8,"BucketValue":2.25,"Overall":3.65}
-- with BucketValue null when the bucket held no tokens at all (infinity is not JSON).
--
-- NULL when a factor was published, or when the writer predates the column. A REASON, never a measurement: nothing
-- reads it to decide fg_mode or a number (03_METRICS §Frame Generation). The Agent writes it (06_DATA_MODEL §Writer
-- ownership: sessions is the Agent's table); the UI's tooltip reads it. Existing rows keep NULL — the detail is
-- computed once at finalize and never re-aggregated.
--
-- ALTER TABLE ADD COLUMN only: earlier scripts are applied on real machines and are never edited (§Migrations).

ALTER TABLE sessions ADD COLUMN fg_refusal_detail TEXT;
