// fl_dxgi_count.h -- the arithmetic behind dxgiUnseen (fl_shm.h @52) and
// FlWriterState.dxgiPresentsUnseen (@48), kept out of the hook body so it can be
// asserted at compile time and driven by hook-harness --probe-dxgi-count.
//
// THE DEFECT THIS EXISTS FOR (2026-09-14). The Present hook reads
// IDXGISwapChain::GetLastPresentCount before forwarding and takes
// `now - previous` on the same slot as unsigned arithmetic. A counter that goes
// BACKWARDS -- a swapchain released and a new one created at the same address,
// so FindOrAdd hands back the old slot with the old count while DXGI's count for
// the new chain starts near zero -- made that difference wrap to ~4e9, which
// `fetch_add`ed into the session total and saturated the record byte at 255. The
// owner's ledger carried dxgi_unseen_total = 4294967243 (-53 as uint32) on a
// title whose pacer never presented past the hook at all, and the saturated
// record is the DxgiSaturated refusal that took the frame-generation factor with
// it. A regression is a RESET, not a count: nothing is added, the record claims
// no delta (FL_MEASURED_DXGI_PRESENTS stays clear for that present, the same as a
// chain's first hooked present), and the slot re-arms on the new count.
//
// Hook-path rules (17_HOOK_ENGINE): constexpr, allocation-free, no exceptions.

#ifndef FRAMELEDGER_FL_DXGI_COUNT_H
#define FRAMELEDGER_FL_DXGI_COUNT_H

#include <cstdint>

namespace fl::dxgicount {

struct Delta {
    // False when the counter went backwards: no delta exists, the record claims nothing,
    // the session total is untouched, and the caller re-arms on `now`.
    bool valid;
    // Presents DXGI counted between the two hooked presents beyond the hooked one itself.
    // 0 is DXGI agreeing with the hook -- a real reading, not silence.
    uint32_t unseen;
};

// `previous` is the count read at the last hooked present on this chain, `now` the
// count read at this one, BEFORE this present is forwarded. Both are DXGI's numbers.
constexpr Delta UnseenBetween(uint32_t previous, uint32_t now) noexcept {
    if (now < previous) {
        return Delta{false, 0u};
    }
    const uint32_t delta = now - previous;
    return Delta{true, delta > 1u ? delta - 1u : 0u};
}

// A session total that cannot wrap: the numerator of Displayed, which must read HIGH
// and be refused rather than low and be believed (the same direction fl_sl_seen.h
// argues for the record byte).
constexpr uint32_t SaturatingAdd(uint32_t total, uint32_t more) noexcept {
    return (UINT32_MAX - total) < more ? UINT32_MAX : total + more;
}

static_assert(UnseenBetween(10u, 11u).valid && UnseenBetween(10u, 11u).unseen == 0u,
              "a delta of exactly one is DXGI agreeing with the hook: valid, nothing unseen");
static_assert(UnseenBetween(10u, 10u).valid && UnseenBetween(10u, 10u).unseen == 0u,
              "an unchanged counter (a present DXGI did not count) is a valid zero, never a wrap");
static_assert(UnseenBetween(10u, 14u).valid && UnseenBetween(10u, 14u).unseen == 3u,
              "the 2.8.0 pacer's shape: four counted, one seen, three unseen");
static_assert(!UnseenBetween(10u, 9u).valid && UnseenBetween(10u, 9u).unseen == 0u,
              "a counter that went backwards is a reset, not 4294967295 unseen presents");
static_assert(!UnseenBetween(UINT32_MAX, 0u).valid, "a wrapped counter is a reset too");
static_assert(SaturatingAdd(UINT32_MAX - 1u, 5u) == UINT32_MAX, "the session total saturates rather than wrapping");
static_assert(SaturatingAdd(7u, 5u) == 12u, "an ordinary add is an add");

}    // namespace fl::dxgicount

#endif    // FRAMELEDGER_FL_DXGI_COUNT_H
