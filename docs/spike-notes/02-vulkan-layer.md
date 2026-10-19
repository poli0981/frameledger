*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 2 · Vulkan layer passthrough *(moved earlier — §R2)*

Measured 2026-08-02 against **Vulkan loader 1.4.357** on Windows 11 26300,
unelevated, with `tools/vklayer-blastradius.ps1`. That script is the **only**
place the layer is registered; it registers under `HKCU`, runs `vulkaninfo`, and
unregisters in a `finally` block including on failure. Verified clean afterwards.

### ✅ `enable_environment` works, and it compares the VALUE

| Condition | Result |
|---|---|
| `FRAMELEDGER_ENABLE_VK_LAYER` unset | loader locates the manifest, **never maps the DLL** |
| set to `1` (the manifest's value) | `Insert instance layer` / `Inserted device layer` — mapped |
| set to `0` (non-matching) | **not** enabled — the loader compares the value, not mere existence |

The value comparison is the better of the two possible answers and was not
safe to assume: had the loader merely checked existence, a stray
`FRAMELEDGER_ENABLE_VK_LAYER=0` anywhere in a user's environment would have
enabled us machine-wide.

**Still a loading gate, not a security gate.** Anything running as the user can
set the variable. It shrinks the default blast radius from "every Vulkan process
on the machine" to "processes the Agent launched"; it does not authorise.

### 🔴 Declining `vkNegotiateLoaderLayerInterfaceVersion` CRASHES the host

The single most valuable thing this test found, and it was in code written the
same afternoon. The obvious-looking gate — "if this process is not in the
enable-list, return `VK_ERROR_INITIALIZATION_FAILED` from negotiation so the
loader skips us" — does **not** make loader 1.4.357 skip the layer. It
access-violates the application. Reproduced every time, with and without
`VK_LOADER_DEBUG`:

| Enable-list state (variable set) | `vulkaninfo` exit |
|---|---|
| file absent | `0xC0000005` |
| file present, this process not listed | `0xC0000005` |
| file present, this process listed | ok |

So that design would have crashed **every Vulkan application on the machine
outside our enable-list** — a far larger blast radius than the one §S2 exists to
reduce, and inflicted on applications that have nothing to do with FrameLedger.

**The rule that follows:** the layer always accepts negotiation and always
forwards. The enable-list decides what we *intercept*, never whether we *load*.
Being present and inert is cheap; being absent by erroring out is not something
this loader supports.

### Two false positives in the test itself, both corrected

Recorded because each would have reported a working gate as broken, and both are
the same shape as defects found elsewhere in this project:

1. **Discovery is not loading.** With the variable unset the loader still prints
   `Located json file "...\FrameLedger.VkLayer\VkLayer_..._overlay.json"`. A
   match on `FrameLedger.VkLayer.dll` hit that *path* — the manifest sits in a
   directory of that name.
2. **Availability is not loading.** `vulkaninfo`'s own report lists the layer by
   name, read from the manifest. That is the tool describing the system, not the
   loader mapping anything.

The unambiguous signal is the loader printing `Insert instance layer` /
`Inserted device layer` with our name and DLL path.

### ✅ `VK_ADD_IMPLICIT_LAYER_PATH` is honoured, and it needs no registry (2026-09-06, P1 item 3)

Measured against the same loader 1.4.357 with `vulkaninfo --summary` and `VK_LOADER_DEBUG=layer`: with
`VK_ADD_IMPLICIT_LAYER_PATH` pointed at the build tree's `FrameLedger.VkLayer` directory and
`FRAMELEDGER_ENABLE_VK_LAYER=1`, the loader prints `Insert instance layer "VK_LAYER_FRAMELEDGER_overlay"
(…\FrameLedger.VkLayer.dll)` and `Inserted device layer …` — the unambiguous signal above — with nothing
under `HKCU` or `HKLM`, and `vulkaninfo` exits 0 with the layer inert (no enable-list entry for it). The
first run's `grep … | head` cut the log before the `Insert` lines and read as "found, not inserted"; the
lines are at the instance-creation point, ~850 lines in. So the operator host launches Vulkan titles with
no registration at all, and the real-loader ctest (`fl_vklayer_real`) does the same: the harness's
`--vulkan` mode presented 241 frames in 4 s on a hidden 240-wide window and the layer recorded every one.

### ✅ On NVIDIA a Vulkan swapchain presents through DXGI — and the DXGI hook sees it (2026-09-06)

Found by the first end-to-end run of `launch` on the harness's `--vulkan` mode: the guard's poll saw
`dxgi.dll` + `d3d12.dll` (the fixture imports d3d11/dxgi; but the NVIDIA Vulkan ICD `nvoglv64` maps them
too) and injected the Overlay, whose `Present` hook then recorded **295 DXGI presents in 5 s with
`apiMask = D3D12` and DXGI's counter agreeing (248 samples, 0 unseen)** — the Vulkan swapchain's own
presents, made by the driver on an internal DXGI flip-model chain. So on this driver the injected Overlay
*can* measure a Vulkan title's presents; that is a driver property, not a portable one (AMD's and Intel's
WSI need not do it), which is why the layer stays the capture side. Two consequences landed: the
launch-mode guard now classifies **any** process that mapped `vulkan-1.dll` as the layer's, and the
harness's `--vulkan` mode maps the loader before anything else so the fixture looks like a real Vulkan
title to the first poll.

### ✅ The in-layer blocklist scan — §S2's second half

The layer scans its own process at init and goes fully passthrough on any hit,
using the same matcher and the same rules file as the injection guard. Verified
by `fl-probe-vklayer` (ctest `fl_vklayer_selfscan`), both directions:

```
[PASS] a clean process is NOT forced inert (so the blocked case below can mean something)
[PASS] the planted module is loaded into this process
[PASS] the self-scan now says STAY INERT - a blocklisted module was found
       after unload the scan says: may observe
```

The planted module is our own DLL copied under a blocklisted name
(`14_TESTING` §Integration tests: "a renamed harmless DLL, not real anti-cheat
software"). Proven red by making the matcher stop matching.

Two things worth keeping:

- **Both directions are asserted.** A self-scan that always answered "stay
  inert" would pass a one-sided test while silently disabling the layer
  everywhere — and would look exactly like a working gate.
- **The probe installs the seed rules when none exist**, then removes them.
  Without that it skipped on any machine that had not run the product, and a
  ctest that always skips is a gate that cannot fail. Installing them is the
  only option: there is deliberately no way to point the layer at a different
  rules file (§S3).

### Not yet done
- **Passthrough under a real Vulkan game**, alongside the six implicit layers
  already resident on this machine (§Environment). `vulkaninfo` proves
  load/no-load; it does not prove a real title still renders correctly with us
  in the chain.

