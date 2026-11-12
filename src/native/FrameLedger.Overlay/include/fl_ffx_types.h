// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// Which descriptor types an ffx-api module was handed, for the native log
// (17_HOOK_ENGINE §Native logging; 0.1.0-beta.42, owner decision D121).
//
// WHY. Cyberpunk 2077, set to FSR upscaling with FSR frame generation, was
// recorded with frame generation measured and the upscaler "unknown": the
// PREPARE and FRAMEGENERATION dispatches arrived and no UPSCALE dispatch ever
// did. Nothing in the field could say where the upscale went -- the record
// carries what was decoded, never what was not. This keeps, per hooked module
// and per entry point, the distinct values of the one member every ffx-api
// descriptor starts with -- its `type`, which the detours read already to choose
// an arm -- and the watchdog writes each to the log the first time it is seen.
// One session's log then says which module was called with what.
//
// NOT A MEASUREMENT AND NOT GATED BY ADVANCED METRICS: it is a diagnostic, like
// HOOK_INSTALLED. It reaches the log file and nothing else -- no record, no
// region of the shared memory, no session.
//
// THE HOOK'S SHARE IS A FEW LOADS. A type already kept costs one relaxed load
// per slot up to its own and no write; a title sends two to four distinct types
// per module, so that is two to four loads a call. A NEW type costs one
// compare-exchange, once. Slots fill in order and are never cleared, so the
// watchdog's "how many have I logged" is a prefix length. No allocation, no
// lock, no logging here (rule 5); nothing is read but the value passed in
// (rule 4).
//
// Header-only so the cost is measured on this code itself and ctest fl_ffx_types
// drives it without the Overlay.

#ifndef FRAMELEDGER_FL_FFX_TYPES_H
#define FRAMELEDGER_FL_FFX_TYPES_H

#include <atomic>
#include <cstdint>

namespace fl::ffxtypes {

inline constexpr std::uint32_t kSlots = 8;

// What did not fit, OR'd into Seen::more.
enum More : std::uint32_t {
    kMoreTypes = 1u << 0,    // a ninth distinct type arrived: the eight kept are not all there were
    kMoreZero = 1u << 1,     // a descriptor whose type was 0 arrived (0 is the empty slot, so it has no slot)
};

struct alignas(64) Seen {
    std::atomic<std::uint64_t> types[kSlots]{};
    std::atomic<std::uint32_t> more{0};
    // Filled out to two cache lines by hand, so that the words another module's detour reads are never on a line
    // this one writes -- and so that the padding is stated rather than left to the compiler (C4324).
    std::uint8_t reserved[60]{};
};
static_assert(sizeof(Seen) == 128, "two cache lines: eight types, the More word, and the stated padding");

namespace detail {

inline void NoteMore(Seen& seen, std::uint32_t bit) noexcept {
    // Read first: once the bit is set, a title that sends such a descriptor every frame writes nothing.
    if ((seen.more.load(std::memory_order_relaxed) & bit) == 0u) {
        seen.more.fetch_or(bit, std::memory_order_relaxed);
    }
}

}    // namespace detail

// Hook path. Keep `type` if it is new.
inline void Note(Seen& seen, std::uint64_t type) noexcept {
    if (type == 0u) {
        detail::NoteMore(seen, kMoreZero);
        return;
    }
    for (std::atomic<std::uint64_t>& slot : seen.types) {
        std::uint64_t kept = slot.load(std::memory_order_relaxed);
        if (kept == type) {
            return;
        }
        if (kept == 0u) {
            if (slot.compare_exchange_strong(kept, type, std::memory_order_relaxed)) {
                return;
            }
            if (kept == type) {
                return;    // another thread kept the same type in this slot first
            }
            // ...or a different one: this slot is taken, the next is tried.
        }
    }
    detail::NoteMore(seen, kMoreTypes);
}

// Watchdog side: how far one Seen has been written to the log.
struct Reported {
    std::uint32_t types = 0;    // slots [0, types) are in the log
    std::uint32_t more = 0;     // More bits already in the log
};

// Calls onType(type) for every kept type not reported yet, in the order they were kept, and onMore(bits) for More
// bits not reported yet. One thread per Reported.
template <class OnType, class OnMore>
void ReportNew(const Seen& seen, Reported& reported, OnType&& onType, OnMore&& onMore) noexcept {
    while (reported.types < kSlots) {
        const std::uint64_t type = seen.types[reported.types].load(std::memory_order_relaxed);
        if (type == 0u) {
            break;
        }
        onType(type);
        ++reported.types;
    }
    const std::uint32_t more = seen.more.load(std::memory_order_relaxed);
    if (const std::uint32_t fresh = more & ~reported.more; fresh != 0u) {
        onMore(fresh);
        reported.more |= fresh;
    }
}

}    // namespace fl::ffxtypes

#endif    // FRAMELEDGER_FL_FFX_TYPES_H
