*Moved verbatim from `docs/spike-notes.md` on 2026-10-19 (beta.24, owner decision D66); `docs/spike-notes.md` is the index of every section, cited as `spike-notes §N`. Nothing below was edited, so its "below" and "above" point inside the original file.*

## Exit criteria

`15_ROADMAP` §P0. Nothing in P1 starts until all are met.

- [x] A throwaway build records a real session from a real offline game
      reporting **correct** upscaler, quality preset, render → output
      resolution, FG factor and RT state — verified against the game's own
      settings menu. **Met 2026-09-06 with one amendment, the owner's:** the
      quality preset reads `N/A` on every title, by measurement — Streamline's tags
      carry no size or preset on Direct3D 12, NVIDIA's override fields are the
      override's, XeSS is closed by licence — and an honest `N/A` where measurement
      is impossible is the *correct* value CLAUDE.md rule 7 asks for. Upscaler,
      render → output (where the vendor's call carries it), FG factor and RT state
      are verified against the menus of nine titles (§9). The owner closed 7a on
      this reading and closed P0 on it here.
- [x] **Moved to the end of P1** (§R4, decided 2026-08-02) — ~~measured game FPS
      impact under 0.5%~~. As written it imported P2's drain, aggregate and
      recorder paths; the harness-level per-present cost stayed in P0 and is
      measured in §3 (8.4 ns).
- [x] Every S-series item in `20_OPEN_QUESTIONS.md` resolved, or explicitly
      deferred with a written rationale. **Met 2026-09-06** (§S24: zero counted
      against it). 🚫 S23-2 is a branch-protection setting outside any PR and is
      recorded as such, not as met.
- [x] M3 and M4 answered **before** any NVAPI or LHM code is written — both
      CLEAR, §0 above.

> ~~Note (§R4): … Decide before starting, not halfway through.~~ **Decided
> 2026-08-02** — the FPS-impact criterion moved to the end of P1, and the
> harness-level per-present cost stayed in P0 as item 2. This file went on asking
> for a decision that two other documents had already recorded.

---

