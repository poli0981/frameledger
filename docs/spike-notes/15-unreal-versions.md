*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 15 · Unreal Engine versions in shipping executables *(2026-09-27, beta.10)*

Measured read-only on the owner's machine (i7-14700KF, RTX 5080, Windows 11 29648): every `*-Win64-Shipping.exe`
under `D:\SteamLibrary\steamapps\common` (a USB drive), before the extractor was written, then again through the
shipped `UnrealBuildReader` once it was. Nothing was copied or written; the numbers are in `05_DETECTION` §Engine version
(the table is not repeated here).

- **Numeric file version** (`VS_FIXEDFILEINFO`): the engine's MAJOR.MINOR.PATCH in seven of ten; a studio's own in three
  (Cronos 1.2.0.0, Rune Factory 1.0.3.0, SILENT HILL 2 1.0.0.5). The version *strings* are empty in six of ten.
- **Branch name** (UTF-16 `++UE4+Release-M.m` / `++UE5+Release-M.m`): present in five — in `.rdata` in four (15–30 MB
  sections), and 290 MB into `.code` in Black Myth: Wukong, whose protected executable has an 8 KB `.rdata`; in UMIGARI
  also in the version strings. Absent in five (Hell Is Us carries `UE5-CL-0`: a renamed branch).
- **The shipped reader, same ten files:** every version as tabled — 8 read, 2 honestly none — in 16 ms to 1.1 s each and
  7.5 s for Black Myth (the whole-file fallback, stopping at the first chunk that names a release line). A first pass
  that scanned only a PE's `.rdata` read Black Myth as `5.0.0` from the file version alone; the 1 MiB `.rdata` floor
  (`UnrealBuildReader.MinRdataBytes`) is what moved it to the stronger rung.
- No UE3 title is installed; the rule does not detect UE3 at all.

