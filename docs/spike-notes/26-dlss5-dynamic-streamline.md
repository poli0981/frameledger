## 26 · DLSS 5 on Hogwarts Legacy, Dynamic frame generation on Phantom Blade Zero, and Streamline 2.12 → 2.14.16 *(2026-11-06, beta.34, owner decisions D83, D84)*

**Why this was measured.** The owner asked, on 2026-11-06, how FrameLedger detects DLSS 5's Neural Rendering on
Hogwarts Legacy — turned on in the game — and why Phantom Blade Zero under the NVIDIA App's *Dynamic* multi frame
generation, up to ×3, read "DLSS 4" when Dynamic and ×6 exist only in DLSS 4.5; then, while the plan was written,
what had changed in Streamline since the copy the Overlay is built against.

**Where, and how.** The owner's ledger was read **read-only** — Python's `sqlite3` with `mode=ro`, no FrameLedger
binary, the Agent of `0.1.0-alpha.2` running beside it — on the dev box of §Environment (Windows 11 Pro build 29648,
i7-14700KF, RTX 5080, 2560×1440 at 240 Hz; NVIDIA driver 32.0.16.2101, then 32.0.16.2114 from 2026-11-05 18:29).
Streamline's headers were compared with the upstream repository's tags through GitHub's API, byte for byte.

### The NVIDIA App's frame-generation settings against the driver's report

`driver_profile` is the game's profile as the Agent read it at the session's start; the FG word and `frameGenerationMode`
are `NvAPI_NGX_GetNGXOverrideState`'s, from `ngx_driver_words`. Bits: `0x1F` initialised … selected, `0x20` preset,
`0x200`/`0x400` created/evaluated, `0x2000` `FG_MODE`, `0x8000` `FG_MULTI_FRAME`.

| Sessions | The App's setting (profile) | FG word | `frameGenerationMode` | Frames counted |
|---|---|---|---|---|
| Phantom Blade Zero 111, 112, 114 (11-01 → 11-02) | `NGX_DLSSG_MODE` 4 *dynamic*, ceiling 5, target `0x01000000` | `0x263F` | 3 | states ×3.0 … ×4.0, ×5.2, ×6.0 |
| Phantom Blade Zero 116 → 129 (11-03 → 11-05), the eight hooked sessions the driver answered (130 was recovered without its report) | `NGX_DLSSG_MODE` 4 *dynamic*, **ceiling 2**, target `0x01000000` | `0x263F` (`0x203F` once) | 3 | ×2.0 and ×3.0 only; ×3.0 held 75–94 % of each session longer than an hour |
| SILENT HILL 2 60, 71 (10-19, 10-20) | `NGX_DLSSG_MODE` 4 *dynamic*, ceiling 5 | `0xA03F` | 3 | ×2.0, ×3.0, ×6.1 |
| Clair Obscur: Expedition 33 64, The Witcher 3 68, SILENT HILL 2 36 | `NGX_DLSSG_MODE` 2 *on*, multi-frame count 5 (*Fixed ×6*) | `0xA03F` | 1 | ×6.0; Witcher 3 ×2.0 |
| Cyberpunk 2077 69 (multiplier chosen in the game) | none set | `0x5` | 0 | ×1.9, ×3.0, ×4.0 |

So the driver reports `FG_MODE` applied under both of the App's modes and its own mode number tells them apart — 3 under
Dynamic, 1 under Fixed, on all sixteen sessions — but NVIDIA's header names neither number. **What D83 reads is the
profile's value, which the header does name (4, `NGX_DLSSG_MODE_DYNAMIC`), with the report's `FG_MODE` bit as proof the
driver applied it**, and DLSS Frame Generation counted in the session. The ceiling's decode (frames generated per
rendered frame, `03_METRICS` §The mix) held: a ceiling of 5 reached ×6.0, a ceiling of 2 never passed ×3.0.

**Decided (D83):** a session the driver reports running the App's Dynamic mode is **DLSS 4.5**, below a measured ×5 and
above every DLSS 4 row; *Fixed* stays the frames' to name (`05_DETECTION` §DLSS generation).

### DLSS 5 on Hogwarts Legacy

Session 132 (2026-11-06 13:56, hooked, attached, 141 s, Neural Rendering on in the game): `runtime_modules` holds
**`nvngx_dlssnr.dll` 310.9.16.0, loaded from the game's folder** — the name `CensusNames` carried from community sources
(owner-only item 20(d)) seen on a real game for the first time — beside `nvngx_dlss.dll`, `nvngx_dlssd.dll`,
`nvngx_dlssg.dll` 310.9.16.0 and Streamline 2.14.16 (`sl.interposer.dll`, `sl.common.dll`, `sl.dlss_g.dll`,
`sl.reflex.dll`). The install ships the matching Streamline plugin, `Engine\Plugins\Runtime\Nvidia\StreamlineCore\
Binaries\ThirdParty\Win64\sl.dlss_nr.dll` (402,048 bytes; `nvngx_dlssnr.dll` is 266,424,944), which no list asked about.
The driver's super-resolution word read `0x80605` (created and evaluated, no override); the session counted DLSS Frame
Generation but refused a factor (non-uniform, ×1.97 overall); 140.2 FPS presented.

What the App made of it: *DLSS · Super Resolution 310.9.16, from the game · … · DLSS 5 runtime loaded* — loaded, never
whether it ran. Nothing measured that: an evaluation of a Streamline feature the Overlay does not decode lights
`FL_FEAT_SL_UNDECODED`, which the Agent does not store, and the session's `sl_tag_census` (`0x20E00`) shows only that
the game tags a buffer type the census does not name, per frame (bit 17) — which Streamline 2.14 names, below.

### Streamline: the vendored copy against the releases

The vendored closure (`src/native/third_party/streamline`, "main, 2026-08-09") is **byte-identical to tag `v2.12.0`**,
all eleven files. Releases since: `v2.14.1` (2026-09-08: V-Sync and frame-rate limiters in Dynamic multi frame
generation, `VK_NV_low_latency2`) and `v2.14.16` (2026-10-16: fixes) — the version Hogwarts Legacy ships and the NVIDIA
App's override loads (`1B0_E658700.dll` 2.14.16.0). From `v2.12.0` to `v2.14.16` three of the eleven change:

- `sl_core_types.h`: **`kFeatureDLSS_NR = 1004`**; buffer types 70–72, reserved until then, become
  `kBufferTypeUpliftInputColor`, `kBufferTypeUpliftOutputColor` and `kBufferTypeUpliftControlMask` — "neural-net
  uplift passes", DLSS 5's inputs; `Resource::reserved` (32 bits) becomes `internalFlags` + `reserved` (16 each, the
  struct's size unchanged).
- `sl_struct.h`: `kStructVersion7`.
- `sl_version.h`: 2.14.16.

`sl_core_api.h` (`PFun_slEvaluateFeature`), `sl_dlss.h`, `sl.h` and `license.txt` are unchanged, and nothing of
FrameLedger reads `Resource::reserved`. **Decided:** re-vendor at `v2.14.16` (the owner: "check what changed … and fix it
if really needed") so the Overlay decodes DLSS 5's evaluations by the vendor's own constant (D84).

### What beta.34 builds on it, and owner-only item 30(a)'s decision table — written before the run

Built (D84): the Overlay decodes `kFeatureDLSS_NR` into `FL_FEAT_SL_NEURAL_RENDERING` and publishes
`FL_HOOK_SL_NEURAL_RENDERING` with the detour; the Agent stores `dlss_nr_pct` — the share of the Streamline batches that
carried it — only from a writer that published the family. The harness drives the id through an injected Overlay
(`guard_test`, *DLSS 5's neural rendering is its own fact*). What no fixture can say is whether Hogwarts Legacy evaluates
neural rendering through `slEvaluateFeature` at all; one short hooked session each:

| Neural Rendering in the game | `dlss_nr_pct` | What it means, and what follows |
|---|---|---|
| On | above 0 | the game evaluates it through Streamline: *DLSS 5 (neural rendering on N % of rendered frames)* — D84 holds as built |
| On | 0 | Streamline batches drained and none carried 1004: neural rendering runs another way (NGX directly, or another id). The line reads *runtime loaded, not seen running*; record the session's `sl_tag_census`, and the decode is revisited before anything is named |
| On | NULL | no Streamline batch drained, or the family is missing from `hooks_installed_mask` (bit 12): the detour never installed — record which |
| Off | 0, the runtime loaded | the runtime loads with the feature off: "loaded" never meant "on", which is why D84 waits for the evaluation |
| Off | NULL, the runtime not loaded | the game loads it only when on — recorded, nothing changes |
