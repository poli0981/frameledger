*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 18 · The Ren'Py version in an installed game *(2026-10-12, beta.20)*

The owner's one Ren'Py install, read-only: Bound Between Desks (Steam) — `BoundBetweenDesks.exe` (386,048 bytes, no version
resource), `BoundBetweenDesks.py`, `game\`, `lib\py3-windows-x86_64\` (Python 3.12: `libpython3.12.dll`,
`librenpython.dll`, `pythonw.exe`) and `renpy\` with the engine's `.py` sources beside `__pycache__\*.cpython-312.pyc`.

- `renpy\vc_version.py` (109 bytes, LF line ends): `branch = 'master'`, `nightly = False`, `official = True`,
  `version = '8.5.0.25111603'`, `version_name = 'In Good Health'`.
- `renpy\__init__.py` imports those names from `vc_version` (else from `renpy.versions.get_version()`) and builds
  `version_tuple` from `version.split(".")`; `renpy.version` is `"Ren'Py " + version_only`.
- The game's own `log.txt` (written at its last start, 2026-09-16) begins `Ren'Py 8.5.0.25111603`: the same number.

Then through the shipped pipeline (`StaticGameDetector` over `GameFileProbe`, rules `2026.10.3`): `renpy 8.5.0
renpy_vc_version` in 79 ms; Dialoop (`unity 6000.3.3f1`), Plethora of Bullets 2 (`godot 4.2.2`), A Doll's Locket - Remake
(`rpgmaker_mv 1.10.0`) and City of Bones (`rpgmaker_rgss 3.0.1`) read as in §17.

- **Not measured:** an older Ren'Py (no other install), whose `vc_version.py` may name no `version` — it then reads as
  not found; a build that ships the engine compiled only (none seen).

