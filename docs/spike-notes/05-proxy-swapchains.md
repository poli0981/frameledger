*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## 5 · Proxy swapchains *(§H5)*

### ✅ The shared-vtable premise, proven both directions — ctest `fl_vtable_identity_control`

`fl-probe-interposer` (2026-08-05). Two composition swapchains created by
independent routes through the real `dxgi.dll` report **one vtable**
(`0x…842BC688`, stable across runs), and a different interface reports a
different one. The second half is what makes the first readable: a comparison
that has never been shown to detect a *difference* carries no information when it
reports "same".

This is the property `17_HOOK_ENGINE` §`swapchainId` and the whole vtable-hook
design rest on, and it is now a ctest rather than a one-off measurement — if it
ever stops holding, the build fails instead of a game discovering it.

### ◐ The interposer question is NOT answered, and the first run of the probe answered it wrongly

Measured against **Cyberpunk 2077** (`sl.interposer.dll` 2.7.1) and **Black Myth:
Wukong** (2.7.4), loaded into our own process — no game running, no injection, no
guard.

| | |
|---|---|
| The interposer loads standalone and exports `CreateDXGIFactory2` | its address differs from `dxgi.dll`'s, so we are genuinely on its code path |
| Swapchain vtable through the interposer | **identical** to the real one |
| **Factory** vtable through the interposer | **also identical** |
| `sl.*` plugins mapped afterwards | **none** — only `sl.interposer.dll` itself |

**That last row is why the first three mean nothing, and the probe was changed to
say so.** As first written it printed *"VERDICT: THE SAME — a vtable-slot present
hook DOES catch presents made through the Streamline interposer"* for both
titles. It was a confident wrong answer. Streamline forwards straight to
`dxgi.dll` until `slInit()` has run and a feature is loaded, so what was measured
is that **passthrough is passthrough**. The tell was in the output the whole
time: a genuinely interposing Streamline cannot leave the *factory* vtable
unwrapped, because wrapping the factory is how it reaches the swapchain.

The probe now enumerates its own loaded modules and refuses to render a verdict
when no plugin is mapped, exiting **2 = inconclusive**. Same discipline as the
guard's tri-state collectors: "could not look" must not read as "looked and it
was clean".

**What answering it actually needs, stated rather than left to be rediscovered:**
`slInit()`, whose `sl::Preferences` argument is vendor ABI — i.e. the licence
question `legal/THIRD_PARTY_NOTICES.md` settles for Intel IGCL ("re-declaring the
API by hand is explicitly NOT an approved workaround") and which is unanswered
for NVIDIA. So §H5 case 3 is **blocked on a licence decision, not on hardware**,
which is a different and more tractable blocker than "needs a real DLSS-G
session". The alternative route is unchanged: observe a real title once the
Overlay has a present hook.

**What is bounded now:** the risk is confined to titles that go through the
Streamline interposer. NGX-direct titles call `nvngx*.dll` and never wrap the
swapchain, so the vtable premise is not in question for them.

