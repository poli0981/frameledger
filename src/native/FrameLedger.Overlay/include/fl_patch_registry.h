// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// The patch registry (17_HOOK_ENGINE §Unhooking): every inline patch the Overlay
// makes, so that the stop can take each one out again -- and the ONE sequence a
// patch goes in by, so that a patch the stop cannot see is never made.
//
// WHAT THIS REPLACES, AND WHY (0.1.0-beta.42, found by reviewing the plan for the
// next two hooks against the code). The registry was an array and a counter:
//
//   1. FULL MEANT SILENT. RegisterPatch advanced the counter and wrote the entry
//      only while it fitted; PatchTarget had already created and enabled the
//      hook. A 49th patch would have been live and unknown to the stop -- "the
//      part inside the game stops" (legal/DISCLAIMER.md §2) with one patch left
//      in, and no line anywhere saying so. Unreachable with the 20 patch sites
//      of today; the rule now does not depend on counting them right.
//   2. ENABLED BEFORE REGISTERED. PatchTarget enabled the hook, re-read
//      g_observing, and registered last. A stop that ran between the re-read and
//      the registration walked a table that did not hold the patch yet.
//
// THE SEQUENCE NOW (Install below): take a slot -- no slot, no patch -- create,
// enable, COMMIT the slot, and only then re-read the flag. The stop clears the
// flag and only then walks the committed slots. Both are a write followed by a
// read of what the other wrote, in the one total order sequentially consistent
// operations have, so at least one of the two sees the other: either the walk
// finds the committed slot and restores it, or the installer finds the flag
// clear and takes its own patch out. There is no interleaving in which neither
// does. (On x86-64 the only instruction this costs is the commit's XCHG, on an
// install path that runs a handful of times per process.)
//
// A SLOT IS Free -> Claimed -> Live, and from Live only to Retired. A claimed slot
// whose patch failed goes back to Free -- the walk never saw it. A slot that was
// ever Live is never handed out again: the walk reads a Live slot's three fields
// without a lock, and a slot refilled under it would be a target paired with
// another patch's detour. Retiring costs a slot only for a patch that raced the
// stop, and after the stop nothing is installed at all.
//
// Header-only, MinHook-free and allocation-free: the Overlay passes MinHook in as
// the `hooker`, and ctest fl_unhook_inline drives this same sequence against real
// MinHook patches -- the shipped sequence, not a copy of it.

#ifndef FRAMELEDGER_FL_PATCH_REGISTRY_H
#define FRAMELEDGER_FL_PATCH_REGISTRY_H

#include <atomic>
#include <cstdint>

namespace fl::patch {

// Why Install did or did not patch. The values are written to the native log (PATCH_FAILED a=<value>), so they
// are fixed: append, never renumber.
enum class InstallResult : std::uint32_t {
    Installed = 0,
    NoTarget = 1,    // nothing to patch
    Stopped = 2,     // observing was already off: nothing was created
    Full = 3,        // every slot is taken: nothing was created
    CreateFailed =
        4,    // the hooker could not create the hook (MinHook: already created, unsupported prologue, memory)
    EnableFailed = 5,     // created, not enabled: no patch at the target
    StoppedDuring = 6,    // the stop ran while this patch went in; the installer took it out again
};

template <std::uint32_t Capacity>
class Registry {
public:
    // A free slot for a patch that does not exist yet, or -1: the caller must not patch.
    int Claim(void* target, void* detour, const char* name) noexcept {
        for (std::uint32_t i = 0; i < Capacity; ++i) {
            std::uint32_t expected = kFree;
            if (slots_[i].state.compare_exchange_strong(expected, kClaimed, std::memory_order_acq_rel,
                                                        std::memory_order_relaxed)) {
                slots_[i].target = target;
                slots_[i].detour = detour;
                slots_[i].name = name;
                return static_cast<int>(i);
            }
        }
        return -1;
    }

    // The patch is enabled; the stop's walk restores it from here on. Sequentially consistent ON PURPOSE: it is the
    // write of the write-then-read pair described at the top of this file.
    void Commit(int slot) noexcept {
        if (Valid(slot)) {
            slots_[static_cast<std::uint32_t>(slot)].state.store(kLive, std::memory_order_seq_cst);
        }
    }

    // The patch was never made. Only for a slot that was claimed and not committed.
    void Release(int slot) noexcept {
        if (Valid(slot)) {
            std::uint32_t expected = kClaimed;
            slots_[static_cast<std::uint32_t>(slot)].state.compare_exchange_strong(
                expected, kFree, std::memory_order_acq_rel, std::memory_order_relaxed);
        }
    }

    // The installer took a committed patch out itself. The slot is never handed out again.
    void Retire(int slot) noexcept {
        if (Valid(slot)) {
            slots_[static_cast<std::uint32_t>(slot)].state.store(kRetired, std::memory_order_seq_cst);
        }
    }

    // Every committed patch: visit(target, detour, name). The read of the write-then-read pair, on the stop's side.
    template <class Visit>
    void ForEachLive(Visit&& visit) const noexcept {
        for (std::uint32_t i = 0; i < Capacity; ++i) {
            if (slots_[i].state.load(std::memory_order_seq_cst) == kLive) {
                visit(slots_[i].target, slots_[i].detour, slots_[i].name);
            }
        }
    }

    std::uint32_t LiveCount() const noexcept {
        std::uint32_t n = 0;
        ForEachLive([&n](void*, void*, const char*) noexcept { ++n; });
        return n;
    }

    static constexpr std::uint32_t kCapacity = Capacity;

private:
    static constexpr std::uint32_t kFree = 0;
    static constexpr std::uint32_t kClaimed = 1;
    static constexpr std::uint32_t kLive = 2;
    static constexpr std::uint32_t kRetired = 3;

    static bool Valid(int slot) noexcept { return slot >= 0 && static_cast<std::uint32_t>(slot) < Capacity; }

    struct Slot {
        std::atomic<std::uint32_t> state{kFree};
        void*                      target = nullptr;
        void*                      detour = nullptr;
        const char*                name = nullptr;
    };
    Slot slots_[Capacity]{};
};

// Patch ONE address, registered before it can run and never left in after a stop.
//
// `hooker` is the inline-hook engine: Create(target, detour, original) -> bool, Enable(target) -> bool,
// Disable(target). The Overlay passes MinHook; so does the test.
//
// A hook that was created and could not be enabled stays created in the engine, as it always has: a later attempt
// then fails at Create, and nothing is at the target either way.
template <std::uint32_t Capacity, class Hooker>
InstallResult Install(Registry<Capacity>& registry, const std::atomic<std::uint32_t>& observing, Hooker&& hooker,
                      void* target, void* detour, void** original, const char* name) noexcept {
    if (target == nullptr) {
        return InstallResult::NoTarget;
    }
    if (observing.load(std::memory_order_seq_cst) == 0u) {
        return InstallResult::Stopped;
    }
    const int slot = registry.Claim(target, detour, name);
    if (slot < 0) {
        return InstallResult::Full;
    }
    if (!hooker.Create(target, detour, original)) {
        registry.Release(slot);
        return InstallResult::CreateFailed;
    }
    if (!hooker.Enable(target)) {
        registry.Release(slot);
        return InstallResult::EnableFailed;
    }
    registry.Commit(slot);
    if (observing.load(std::memory_order_seq_cst) == 0u) {
        // The stop ran while the patch went in. Its walk either saw the committed slot and restored the patch -- then
        // this disable finds it disabled already -- or it had passed the slot, and this is what takes the patch out.
        hooker.Disable(target);
        registry.Retire(slot);
        return InstallResult::StoppedDuring;
    }
    return InstallResult::Installed;
}

}    // namespace fl::patch

#endif    // FRAMELEDGER_FL_PATCH_REGISTRY_H
