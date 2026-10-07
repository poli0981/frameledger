# Data and IPC

The full documents are [`docs/06_DATA_MODEL.md`](../docs/06_DATA_MODEL.md) and [`docs/07_IPC.md`](../docs/07_IPC.md).

## The database

One SQLite file, `%LOCALAPPDATA%\FrameLedger\ledger.db`, in WAL mode, used through Microsoft.Data.Sqlite and Dapper.

| Table | Holds |
|---|---|
| `games` | The library, with each game's hooking, consent and anti-cheat state. |
| `sessions` | One row per session: its tier, how it ended, its figures. |
| `session_segments` | Spans of a session at one resolution and upscaler. |
| `frame_blobs`, `sensor_blobs` | The per-frame and sensor series, compressed. |
| `hardware_snapshots` | The PC as it was, shared by the sessions recorded on it. |
| `session_annotations` | Tags, notes and the user's overrides. |
| `settings` | Key and value, as `SettingsRegistry` (`src/FrameLedger.Application/Settings`) defines them. |
| `legal_acceptance` | Which version of each document the user accepted, and when. |
| `schema_migrations` | The migrations applied. |

- **Who writes what:** the agent writes sessions, their series and snapshots, and a game's hooking state; the App
  writes what the user edits — a game's details, annotations, settings, legal acceptance. Consent never goes through
  the database: the App asks the agent over the pipe.
- **Migrations** are numbered SQL files embedded in `src/FrameLedger.Infrastructure/Persistence/Migrations`, applied in
  order by whichever process opens the file first, under a lock. A database newer than the build is refused, which is
  why an older version cannot open a database a newer one has touched.

## Shared memory — the game and the agent

- One mapping per game process, created by the component in the game and opened by the agent: a handshake, the
  writer's state, a control block, the display state, then a ring of fixed-size frame records — one writer, one reader,
  no locks.
- The layout is defined twice: `src/native/FrameLedger.Shm/include/fl_shm.h` (normative) and
  `src/FrameLedger.Shared/ShmLayout.cs`. `ShmLayoutMirrorTests` compares every field's offset against JSON the native
  `fl-layout-dump` writes, in both directions, and the gate fails if that test did not run.
- Before reading anything, the agent compares the layout version, the record size and the build id, and refuses to
  attach on a mismatch.

## The pipe — the App and the agent

- A named pipe, `\\.\pipe\FrameLedger.v2`, carrying length-prefixed JSON messages (source-generated System.Text.Json,
  unknown fields ignored): commands from the App (switch hooking on, pause, delete sessions…) and events from the agent
  (a session started, made progress, ended; a safety notice).
- **The pipe is not a trust boundary for safety:** no message can tell the agent that something is safe. The guard
  decides on its own.
