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
    StoppedDuring = 6,    // the stop ran while this patch went in; the installer took it out again, or never put it in
    Pending = 7,          // in a Batch, created and waiting for its Commit; never logged
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

// ---------------------------------------------------------------------------------------------------------------------
// BATCHES (0.1.0-beta.46, owner decision D134): several patches in, or every patch out, for ONE stop of the process's
// threads instead of one a patch.
//
// WHY. MinHook stops every thread of the process to write a jump: a snapshot of the system's threads, then a suspend,
// a context read and a resume for each of this process's. Enabling or disabling ONE hook does all of that for one
// jump. In the owner's games of 2026-11-13 the thirteen Streamline patches took 0.7 s to 2.8 s of a game's start that
// way, 57 to 215 ms each, and a stop restored its patches at about 160 ms each. MinHook can queue: mark hooks to be
// enabled or disabled, then apply every mark under one stop of the threads.
//
// WHAT THE QUEUE IS, AND THE TWO THINGS IT MAKES DANGEROUS (hook.c, MH_ApplyQueued). The queue is ONE table for the
// process: applying it applies every hook whose mark differs from its state -- not the caller's marks, everybody's.
// So:
//   1. A mark may never be left standing. A hook marked "enable" that failed, left marked, would be enabled by the
//      next apply -- the stop's own, if that came next. And a hook marked "disable" for a target that a foreign overlay
//      has patched over would have OUR saved bytes written over THEIR jump by whoever applied next (§H7).
//   2. Two threads may not interleave their marks and applies: the installer's apply would execute the stop's marks
//      and the other way round.
// Hence the GATE (`BeginQueue` / `EndQueue` on the engine): mark, apply and commit happen inside it, and when it is
// free no hook has a mark that differs from its state. The gate is held for one apply and never across anything that
// can take the loader's lock (resolving a symbol is done before, by the caller).
//
// A draft of this marked EVERYTHING for disabling and then un-marked the targets it must not restore. It was refused
// on review against hook.c: un-marking a hook that is already disabled marks it for ENABLING, and the stop's own apply
// would have put a patch back in after the stop. Nothing here marks "all", and nothing un-marks by marking the
// opposite except where the hook's state is known.
//
// THE ENGINE, beside Create: QueueEnable(target) / QueueDisable(target) -> bool (mark only); Apply() -> bool (every
// mark, one stop of the threads; false when any failed -- it stops at the first failure and does not say which);
// EnsureEnabled(target) / EnsureDisabled(target) -> bool (one hook, true when it is in that state afterwards);
// BeginQueue() / EndQueue().
// ---------------------------------------------------------------------------------------------------------------------

// The stop's walk: every committed patch that is still OURS is restored, under one apply; a patch a foreign overlay
// has patched over is left exactly as it is (17_HOOK_ENGINE §Unhooking, §H7). The caller has already cleared the
// observing flag -- this is the read half of the pair described at the top of this file.
//
// `ours(target, detour)` -> bool: the bytes at the target are still the jump made for us (fl_patch_check.h).
// `report(name, target, wasOurs, restored)` is called once per committed patch, after the gate is released.
template <std::uint32_t Capacity, class Hooker, class Ours, class Report>
void RestoreAll(const Registry<Capacity>& registry, Hooker&& hooker, Ours&& ours, Report&& report) noexcept {
    struct Entry {
        void*       target;
        const char* name;
        bool        wasOurs;
        bool        marked;
        bool        restored;
    };
    Entry         entries[Capacity];
    std::uint32_t count = 0;
    bool          anyMarked = false;

    hooker.BeginQueue();
    registry.ForEachLive(
        [&entries, &count, &anyMarked, &hooker, &ours](void* target, void* detour, const char* name) noexcept {
            if (count >= Capacity) {
                return;
            }
            // Only a patch that is still ours is ever marked. One that is not is not touched -- not marked, and not
            // un-marked either: it has no mark to remove.
            const bool mine = ours(target, detour);
            const bool marked = mine && hooker.QueueDisable(target);
            anyMarked = anyMarked || marked;
            entries[count++] = Entry{target, name, mine, marked, false};
        });
    const bool applied = !anyMarked || hooker.Apply();
    for (std::uint32_t i = 0; i < count; ++i) {
        Entry& e = entries[i];
        if (!e.marked) {
            continue;
        }
        // The apply stops at the first hook it cannot restore: the ones after it are taken out one at a time, as
        // every patch was until 0.1.0-beta.46. One that will not come out at all has its mark removed -- it is
        // enabled, so marking it "enable" leaves nothing to apply.
        e.restored = applied || hooker.EnsureDisabled(e.target);
        if (!e.restored) {
            hooker.QueueEnable(e.target);
        }
    }
    hooker.EndQueue();

    for (std::uint32_t i = 0; i < count; ++i) {
        report(entries[i].name, entries[i].target, entries[i].wasOurs, entries[i].restored);
    }
}

// Several patches, registered before any can run, enabled under ONE stop of the threads, and none left in after a
// stop -- the sequence of Install, for a group. One thread fills and commits a batch (the Overlay's watchdog).
//
// Add: claim a slot and create the hook; nothing is at the target yet and nothing is marked. Commit: enable them all.
// After Commit every item carries its InstallResult, and the caller does what follows a patch (its log line, its
// family bit) for the ones that read Installed, in the order they were added.
template <std::uint32_t Capacity>
class Batch {
public:
    struct Item {
        void*         target = nullptr;
        void*         detour = nullptr;
        const char*   name = nullptr;
        void*         context = nullptr;    // the caller's: what to do once the patch is in
        std::uint32_t word = 0;             // the caller's
        int           slot = -1;
        InstallResult result = InstallResult::NoTarget;
    };

    // Pending: the hook is created and waits for Commit. Anything else is final and nothing was added.
    template <class Hooker>
    InstallResult Add(Registry<Capacity>& registry, const std::atomic<std::uint32_t>& observing, Hooker&& hooker,
                      void* target, void* detour, void** original, const char* name, void* context,
                      std::uint32_t word) noexcept {
        if (target == nullptr) {
            return InstallResult::NoTarget;
        }
        if (observing.load(std::memory_order_seq_cst) == 0u) {
            return InstallResult::Stopped;
        }
        if (count_ >= Capacity) {
            return InstallResult::Full;    // not reachable before the registry itself is full: one slot an item
        }
        const int slot = registry.Claim(target, detour, name);
        if (slot < 0) {
            return InstallResult::Full;
        }
        if (!hooker.Create(target, detour, original)) {
            registry.Release(slot);
            return InstallResult::CreateFailed;
        }
        items_[count_++] = Item{target, detour, name, context, word, slot, InstallResult::Pending};
        return InstallResult::Pending;
    }

    template <class Hooker, class Ours>
    void Commit(Registry<Capacity>& registry, const std::atomic<std::uint32_t>& observing, Hooker&& hooker,
                Ours&& ours) noexcept {
        if (count_ == 0u) {
            return;
        }
        hooker.BeginQueue();
        if (observing.load(std::memory_order_seq_cst) == 0u) {
            // The stop ran between Add and here. Nothing was marked and nothing is at any target: the slots go back,
            // and the hooks stay created and disabled in the engine, as a hook that failed to enable always has.
            for (std::uint32_t i = 0; i < count_; ++i) {
                registry.Release(items_[i].slot);
                items_[i].result = InstallResult::StoppedDuring;
            }
            hooker.EndQueue();
            return;
        }
        for (std::uint32_t i = 0; i < count_; ++i) {
            if (!hooker.QueueEnable(items_[i].target)) {
                registry.Release(items_[i].slot);
                items_[i].result = InstallResult::EnableFailed;
            }
        }
        const bool applied = hooker.Apply();
        for (std::uint32_t i = 0; i < count_; ++i) {
            Item& item = items_[i];
            if (item.result != InstallResult::Pending) {
                continue;
            }
            // The ENGINE says whether the hook is enabled, not the bytes at the target: a foreign overlay that
            // patched over us a moment after the apply would read as "not in", and taking that for a failure would
            // mark a restore nobody had compared (§H7). The apply stops at the first hook that fails, so the ones
            // after it are enabled one at a time -- one bad patch costs one patch, as it always has.
            if (applied || hooker.EnsureEnabled(item.target)) {
                registry.Commit(item.slot);
                item.result = InstallResult::Installed;
            } else {
                hooker.QueueDisable(item.target);    // it is disabled: this removes the mark, and marks nothing
                registry.Release(item.slot);
                item.result = InstallResult::EnableFailed;
            }
        }
        hooker.EndQueue();

        if (observing.load(std::memory_order_seq_cst) != 0u) {
            return;
        }
        // The stop ran while the batch went in -- the re-read of Install, for the group. Its walk either saw the
        // committed slots and restored the patches, and then they are no longer ours and nothing is marked, or it had
        // passed them, and this takes them out. Compared first, as the stop compares: a patch something else has
        // patched over in the meantime is left to it.
        bool takeOut[Capacity]{};
        bool anyMarked = false;
        hooker.BeginQueue();
        for (std::uint32_t i = 0; i < count_; ++i) {
            const Item& item = items_[i];
            if (item.result == InstallResult::Installed) {
                takeOut[i] = ours(item.target, item.detour) && hooker.QueueDisable(item.target);
                anyMarked = anyMarked || takeOut[i];
            }
        }
        const bool out = !anyMarked || hooker.Apply();
        for (std::uint32_t i = 0; i < count_; ++i) {
            Item& item = items_[i];
            if (item.result != InstallResult::Installed) {
                continue;
            }
            if (takeOut[i] && !out && !hooker.EnsureDisabled(item.target)) {
                hooker.QueueEnable(item.target);    // enabled and staying so: no mark may be left standing
            }
            registry.Retire(item.slot);
            item.result = InstallResult::StoppedDuring;
        }
        hooker.EndQueue();
    }

    std::uint32_t Count() const noexcept { return count_; }
    const Item&   At(std::uint32_t i) const noexcept { return items_[i]; }
    void          Clear() noexcept { count_ = 0u; }

private:
    Item          items_[Capacity]{};
    std::uint32_t count_ = 0u;
};

}    // namespace fl::patch

#endif    // FRAMELEDGER_FL_PATCH_REGISTRY_H
