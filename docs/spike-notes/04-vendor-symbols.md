*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 4 · Vendor symbol reality check

Resolve the **actual** exported names; `17_HOOK_ENGINE` records conventions, not
verified facts. A wrong name degrades silently to `unknown`, which reads as
"working, no upscaler detected" — the highest false-confidence risk in the spike.
Record the SDK version each name came from.

### ✅ Answered 2026-08-05 — `docs/vendor-exports.json`

Measured across **34 distinct modules in 162 files** from installed titles by
`tools/vendor-exports.ps1` (dumpbin over files on disk; nothing is loaded). The
map is committed rather than transcribed here, because a table copied by hand is
a second source that drifts from the first.

**The finding this item existed to produce** — the NGX parameter surface splits
into two hook classes, and only one half is an exported function:

| Module | Parameter **accessors** | Parameter-object **factories** |
|---|---|---|
| `sl.common.dll` (Streamline) | **yes**, all 16 | yes |
| `nvngx.dll` / driver-store `_nvngx.dll` | **no** | yes (D3D11/12/VK/CUDA) |
| `nvngx_dlss.dll`, `_dlssg`, `_dlssd` | no | no |

Streamline-shimmed titles (9 of those installed here) expose
`NVSDK_NGX_Parameter_SetI/SetUI` as ordinary exports. NGX-direct titles do not:
the accessors are function pointers inside the object the factories return, so
there is no symbol to hook. `17_HOOK_ENGINE` §The NGX parameter surface carries
the decision and its CLAUDE.md rule-4 justification.

**Not gated.** Nothing cross-checks `17_HOOK_ENGINE`'s symbol table against the
JSON, so they can drift — that gate belongs with the PR that adds the feature
hooks, where a failing row would mean something. The map is also one machine on
one driver version, and the JSON says so in its own `$comment`.

