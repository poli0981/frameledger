// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// Compare-and-restore for INLINE patches (17_HOOK_ENGINE §Unhooking, §H7) --
// the shape the Overlay's hooks actually are, driven against a REAL MinHook patch
// and a SIMULATED foreign one, deterministically, with no overlay software
// installed and no game.
//
// The existing ctest fl_unhook_preserves_foreign proves the rule for VTABLE
// swaps in the harness. The Overlay swaps no vtable: every hook it installs is a
// MinHook inline patch, and MH_DisableHook writes the saved prologue back without
// looking. So the predicate the Overlay consults before disabling -- "is the jump
// at the target still the one that ends at OUR detour?" -- is proved here, both
// directions, exactly as H7 demanded of the vtable form: it must say YES for our
// own untouched patch (or a compare-and-restore that never restores is not a fix)
// and NO once somebody patched over us.

#include <windows.h>

#include <atomic>
#include <catch2/catch_test_macros.hpp>
#include <cstdint>
#include <cstring>
#include <fl_patch_check.h>
#include <fl_patch_registry.h>
#include <MinHook.h>

namespace {

// A real function with a real, relocatable prologue. noinline and non-trivial so
// the compiler keeps a body MinHook can patch, and volatile so nothing folds.
__declspec(noinline) int Target(int x) {
    volatile int acc = x;
    for (int i = 0; i < 3; ++i) {
        acc = acc * 3 + i;
    }
    return acc;
}

using TargetFn = int (*)(int);
TargetFn g_orig = nullptr;

int Detour(int x) {
    return g_orig(x) + 1000;
}

// The foreign overlay's detour and the relay it jumps through, in the exact
// JMP_ABS shape MinHook uses, so the bytes at the target look like a legitimate
// patch that simply ends somewhere else.
int ForeignDetour(int x) {
    return x - 1;
}

// Within +/-2 GB of `near`, as MinHook's own relays are: an E9's rel32 cannot
// reach an arbitrary 64-bit address, and a relay placed anywhere by
// VirtualAlloc(nullptr) is exactly what crashed the first run of this test.
std::uint8_t* AllocNear(const void* anchor) {
    const auto base = reinterpret_cast<std::uintptr_t>(anchor);
    for (std::uintptr_t offset = 0x100000; offset < 0x40000000; offset += 0x10000) {
        for (int dir = 0; dir < 2; ++dir) {
            const std::uintptr_t candidate =
                (dir == 0 ? base - offset : base + offset) & ~static_cast<std::uintptr_t>(0xFFFF);
            void* p = VirtualAlloc(reinterpret_cast<void*>(candidate), 4096, MEM_COMMIT | MEM_RESERVE,
                                   PAGE_EXECUTE_READWRITE);
            if (p != nullptr) {
                return static_cast<std::uint8_t*>(p);
            }
        }
    }
    return nullptr;
}

std::uint8_t* MakeRelay(const void* to, const void* anchor) {
    std::uint8_t* relay = AllocNear(anchor);
    REQUIRE(relay != nullptr);
    relay[0] = 0xFF;
    relay[1] = 0x25;
    std::memset(relay + 2, 0, 4);
    const auto addr = reinterpret_cast<std::uint64_t>(to);
    std::memcpy(relay + 6, &addr, sizeof(addr));
    return relay;
}

// Overwrite the rel32 of the E9 at `target` so it lands on `relay` -- what a
// foreign overlay that patches after us leaves behind.
void RepointJump(void* target, const void* relay, std::int32_t* savedRel) {
    auto* p = static_cast<std::uint8_t*>(target);
    REQUIRE(p[0] == 0xE9);
    std::memcpy(savedRel, p + 1, sizeof(*savedRel));
    const auto rel = static_cast<std::int32_t>(reinterpret_cast<const std::uint8_t*>(relay) - (p + 5));
    DWORD      old = 0;
    REQUIRE(VirtualProtect(p, 5, PAGE_EXECUTE_READWRITE, &old));
    std::memcpy(p + 1, &rel, sizeof(rel));
    VirtualProtect(p, 5, old, &old);
    FlushInstructionCache(GetCurrentProcess(), p, 5);
}

// --- the registry's cases (0.1.0-beta.42) ---------------------------------------------------------------------------
// Three more real functions, each with a body of its own so the linker cannot fold them into one address.
__declspec(noinline) int First(int x) {
    volatile int acc = x;
    for (int i = 0; i < 3; ++i) {
        acc = acc * 5 + i;
    }
    return acc;
}
__declspec(noinline) int Second(int x) {
    volatile int acc = x;
    for (int i = 0; i < 4; ++i) {
        acc = acc * 7 - i;
    }
    return acc;
}
__declspec(noinline) int Third(int x) {
    volatile int acc = x;
    for (int i = 0; i < 5; ++i) {
        acc = acc * 11 + 2 * i;
    }
    return acc;
}

TargetFn g_origFirst = nullptr;
TargetFn g_origSecond = nullptr;
TargetFn g_origThird = nullptr;

int DetourFirst(int x) {
    return g_origFirst(x) + 1000000;
}
int DetourSecond(int x) {
    return g_origSecond(x) + 2000000;
}
int DetourThird(int x) {
    return g_origThird(x) + 3000000;
}

// MinHook, exactly as the Overlay hands it to fl::patch::Install (dllmain.cpp's MinHookEngine).
struct RealMinHook {
    static bool Create(void* target, void* detour, void** original) noexcept {
        return MH_CreateHook(target, detour, original) == MH_OK;
    }
    static bool Enable(void* target) noexcept { return MH_EnableHook(target) == MH_OK; }
    static void Disable(void* target) noexcept { MH_DisableHook(target); }
};

// The same engine with one thing arranged: a stop that arrives between the enable and the installer's re-read.
struct StopArrivesWhileEnabling {
    std::atomic<std::uint32_t>* observing;
    static bool                 Create(void* target, void* detour, void** original) noexcept {
        return RealMinHook::Create(target, detour, original);
    }
    bool Enable(void* target) const noexcept {
        const bool ok = RealMinHook::Enable(target);
        observing->store(0u);
        return ok;
    }
    static void Disable(void* target) noexcept { RealMinHook::Disable(target); }
};

// An engine that fails at a chosen step and counts what it was asked: nothing may be created once a patch is refused.
struct FailingEngine {
    bool createOk;
    bool enableOk;
    int* created;
    bool Create(void*, void*, void**) const noexcept {
        ++*created;
        return createOk;
    }
    bool        Enable(void*) const noexcept { return enableOk; }
    static void Disable(void*) noexcept {}
};

// The stop's walk, as StopObserving writes it: restore each patch that is still ours.
template <std::uint32_t N>
int RestoreEveryLivePatch(const fl::patch::Registry<N>& registry) {
    int restored = 0;
    registry.ForEachLive([&restored](void* target, void* detour, const char*) noexcept {
        if (fl::patch::StillOurs(target, detour) && MH_DisableHook(target) == MH_OK) {
            ++restored;
        }
    });
    return restored;
}

void RestoreJump(void* target, std::int32_t savedRel) {
    auto* p = static_cast<std::uint8_t*>(target);
    DWORD old = 0;
    REQUIRE(VirtualProtect(p, 5, PAGE_EXECUTE_READWRITE, &old));
    std::memcpy(p + 1, &savedRel, sizeof(savedRel));
    VirtualProtect(p, 5, old, &old);
    FlushInstructionCache(GetCurrentProcess(), p, 5);
}

}    // namespace

TEST_CASE("an inline patch is recognised as ours, then not once a foreign hooker patches over it, then ours again",
          "[unhook][inline]") {
    void* target = reinterpret_cast<void*>(&Target);
    void* detour = reinterpret_cast<void*>(&Detour);

    // Unpatched: nothing is ours, and asking must not fault or claim otherwise.
    CHECK_FALSE(fl::patch::StillOurs(target, detour));

    REQUIRE(MH_Initialize() == MH_OK);
    REQUIRE(MH_CreateHook(target, detour, reinterpret_cast<void**>(&g_orig)) == MH_OK);
    REQUIRE(MH_EnableHook(target) == MH_OK);
    REQUIRE(Target(1) == Detour(1));    // the hook is live (the detour's own call goes through the trampoline)
    CHECK(Target(1) >= 1000);

    // 1. Our own untouched patch IS ours -- the half a compare-and-restore that never restores would fail.
    CHECK(fl::patch::StillOurs(target, detour));
    CHECK_FALSE(fl::patch::StillOurs(target, reinterpret_cast<void*>(&ForeignDetour)));

    // 2. A foreign overlay patches after us: the E9 now lands on THEIR relay. Not ours any more.
    std::uint8_t* foreignRelay = MakeRelay(reinterpret_cast<void*>(&ForeignDetour), target);
    std::int32_t  savedRel = 0;
    RepointJump(target, foreignRelay, &savedRel);
    CHECK(Target(5) == 4);    // their hook is what runs now
    CHECK_FALSE(fl::patch::StillOurs(target, detour));
    // ...and a MinHook disable here would have written our prologue back over them; the
    // Overlay's StopObserving consults the predicate first and leaves the slot alone.

    // 3. Put our jump back (the foreign overlay went away), and it is ours again -- so the
    //    predicate discriminates on the bytes, not on history.
    RestoreJump(target, savedRel);
    CHECK(fl::patch::StillOurs(target, detour));
    CHECK(Target(1) >= 1000);

    // 4. The restore half: with the slot ours, MH_DisableHook restores the original behaviour.
    REQUIRE(MH_DisableHook(target) == MH_OK);
    CHECK(Target(1) == 32);    // ((1*3+0)*3+1)*3+2
    CHECK_FALSE(fl::patch::StillOurs(target, detour));

    MH_Uninitialize();
    VirtualFree(foreignRelay, 0, MEM_RELEASE);
}

TEST_CASE("garbage at the target is 'not ours', never a fault", "[unhook][inline]") {
    // A jump whose rel32 points into unmapped space must read as not-ours, not crash
    // the process asking. Built on our own page rather than on a real function.
    auto* page =
        static_cast<std::uint8_t*>(VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
    REQUIRE(page != nullptr);
    page[0] = 0xE9;
    const std::int32_t rel = 0x7FFF0000;    // somewhere far away and almost certainly unmapped
    std::memcpy(page + 1, &rel, sizeof(rel));
    CHECK_FALSE(fl::patch::StillOurs(page, reinterpret_cast<void*>(&Detour)));
    CHECK_FALSE(fl::patch::StillOurs(nullptr, reinterpret_cast<void*>(&Detour)));
    CHECK_FALSE(fl::patch::StillOurs(page, nullptr));
    VirtualFree(page, 0, MEM_RELEASE);
}

TEST_CASE("a full registry refuses the patch: nothing is created and the target's bytes are untouched",
          "[unhook][registry]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<2>     registry;
    std::atomic<std::uint32_t> observing{1u};
    void*                      first = reinterpret_cast<void*>(&First);
    void*                      second = reinterpret_cast<void*>(&Second);
    void*                      third = reinterpret_cast<void*>(&Third);
    const int                  firstPlain = First(3);
    const int                  secondPlain = Second(3);
    const int                  thirdPlain = Third(3);

    std::uint8_t before[16]{};
    std::memcpy(before, third, sizeof(before));

    REQUIRE(MH_Initialize() == MH_OK);
    CHECK(fl::patch::Install(registry, observing, RealMinHook{}, first, reinterpret_cast<void*>(&DetourFirst),
                             reinterpret_cast<void**>(&g_origFirst), "first") == InstallResult::Installed);
    CHECK(fl::patch::Install(registry, observing, RealMinHook{}, second, reinterpret_cast<void*>(&DetourSecond),
                             reinterpret_cast<void**>(&g_origSecond), "second") == InstallResult::Installed);
    CHECK(First(3) == firstPlain + 1000000);
    CHECK(Second(3) == secondPlain + 2000000);
    CHECK(registry.LiveCount() == 2u);

    // The third has no slot. The array and counter this replaced patched it anyway and forgot it.
    g_origThird = nullptr;
    CHECK(fl::patch::Install(registry, observing, RealMinHook{}, third, reinterpret_cast<void*>(&DetourThird),
                             reinterpret_cast<void**>(&g_origThird), "third") == InstallResult::Full);
    CHECK(std::memcmp(before, third, sizeof(before)) == 0);
    CHECK(Third(3) == thirdPlain);
    CHECK(g_origThird == nullptr);    // MinHook was never asked: no trampoline exists for it
    CHECK(MH_EnableHook(third) == MH_ERROR_NOT_CREATED);
    CHECK(registry.LiveCount() == 2u);

    // And the stop takes out every patch that did go in -- which is now every patch there is.
    observing.store(0u);
    CHECK(RestoreEveryLivePatch(registry) == 2);
    CHECK(First(3) == firstPlain);
    CHECK(Second(3) == secondPlain);

    // Stopping is one-way: after it, nothing is created at all.
    CHECK(fl::patch::Install(registry, observing, RealMinHook{}, third, reinterpret_cast<void*>(&DetourThird),
                             reinterpret_cast<void**>(&g_origThird), "third") == InstallResult::Stopped);
    CHECK(MH_EnableHook(third) == MH_ERROR_NOT_CREATED);
    MH_Uninitialize();
}

TEST_CASE("a patch that goes in while the stop runs is taken out by its installer, and its slot is never reused",
          "[unhook][registry]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<1>     registry;
    std::atomic<std::uint32_t> observing{1u};
    void*                      first = reinterpret_cast<void*>(&First);
    const int                  firstPlain = First(4);

    REQUIRE(MH_Initialize() == MH_OK);
    // The window the old order left open: the flag read 1, the stop ran -- its walk found nothing, the patch was not
    // registered yet -- and the patch was then enabled and registered, live after "stopped".
    CHECK(fl::patch::Install(registry, observing, StopArrivesWhileEnabling{&observing}, first,
                             reinterpret_cast<void*>(&DetourFirst), reinterpret_cast<void**>(&g_origFirst),
                             "first") == InstallResult::StoppedDuring);
    CHECK(First(4) == firstPlain);    // not left in
    CHECK_FALSE(fl::patch::StillOurs(first, reinterpret_cast<void*>(&DetourFirst)));
    CHECK(registry.LiveCount() == 0u);    // and not left for a walk to report as somebody else's
    CHECK(registry.Claim(first, first, "again") == -1);
    MH_Uninitialize();
}

TEST_CASE("a patch that failed gives its slot back, and a slot is not seen by the walk until it is committed",
          "[unhook][registry]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<1>     registry;
    std::atomic<std::uint32_t> observing{1u};
    int                        target = 0;
    int                        detour = 0;
    void*                      original = nullptr;
    int                        created = 0;

    CHECK(fl::patch::Install(registry, observing, FailingEngine{false, true, &created}, &target, &detour, &original,
                             "x") == InstallResult::CreateFailed);
    CHECK(fl::patch::Install(registry, observing, FailingEngine{true, false, &created}, &target, &detour, &original,
                             "x") == InstallResult::EnableFailed);
    CHECK(created == 2);
    CHECK(registry.LiveCount() == 0u);
    CHECK(fl::patch::Install(registry, observing, FailingEngine{true, true, &created}, nullptr, &detour, &original,
                             "x") == InstallResult::NoTarget);
    CHECK(created == 2);

    // Both failures gave the one slot back, so it can still be claimed -- and a claimed slot is nobody's yet.
    const int slot = registry.Claim(&target, &detour, "x");
    REQUIRE(slot == 0);
    CHECK(registry.LiveCount() == 0u);
    CHECK(registry.Claim(&target, &detour, "y") == -1);
    CHECK(fl::patch::Install(registry, observing, FailingEngine{true, true, &created}, &target, &detour, &original,
                             "x") == InstallResult::Full);
    CHECK(created == 2);    // refused before the engine was asked for anything

    registry.Commit(slot);
    const void* seenTarget = nullptr;
    const char* seenName = nullptr;
    registry.ForEachLive([&seenTarget, &seenName](void* t, void*, const char* name) noexcept {
        seenTarget = t;
        seenName = name;
    });
    CHECK(seenTarget == &target);
    CHECK(seenName != nullptr);
    CHECK(registry.LiveCount() == 1u);

    // Release is for a slot that was never committed: on a live one it changes nothing.
    registry.Release(slot);
    CHECK(registry.LiveCount() == 1u);
    registry.Retire(slot);
    CHECK(registry.LiveCount() == 0u);
    registry.Commit(-1);
    registry.Release(7);
    registry.Retire(-3);
    CHECK(registry.LiveCount() == 0u);
}

// --- batches (0.1.0-beta.46, owner decision D134) --------------------------------------------------------------------
// Several patches for ONE stop of the process's threads, and every patch out the same way. Driven twice: against real
// MinHook, for what is at the target, and against a MODEL of MinHook's table (hook.c: each hook has a state and a
// mark, and an apply executes every hook whose two differ, stopping at the first that fails), for the things real
// MinHook cannot be made to do on demand -- an apply that fails halfway, a stop that lands inside one.
namespace {

// MinHook with its queue, as dllmain.cpp's MinHookEngine hands it to Batch and RestoreAll. The gate is the Overlay's;
// one thread patches here.
struct RealQueue {
    static bool Create(void* target, void* detour, void** original) noexcept {
        return MH_CreateHook(target, detour, original) == MH_OK;
    }
    static bool QueueEnable(void* target) noexcept { return MH_QueueEnableHook(target) == MH_OK; }
    static bool QueueDisable(void* target) noexcept { return MH_QueueDisableHook(target) == MH_OK; }
    static bool Apply() noexcept { return MH_ApplyQueued() == MH_OK; }
    static bool EnsureEnabled(void* target) noexcept {
        const MH_STATUS status = MH_EnableHook(target);
        return status == MH_OK || status == MH_ERROR_ENABLED;
    }
    static bool EnsureDisabled(void* target) noexcept {
        const MH_STATUS status = MH_DisableHook(target);
        return status == MH_OK || status == MH_ERROR_DISABLED;
    }
    static void BeginQueue() noexcept {}
    static void EndQueue() noexcept {}
};

bool RealOurs(const void* target, const void* detour) noexcept {
    return fl::patch::StillOurs(target, detour);
}

struct Model {
    static constexpr int kMax = 8;
    void*                target[kMax]{};
    bool                 enabled[kMax]{};
    bool                 marked[kMax]{};     // MinHook's queueEnable: the state the next apply gives the hook
    bool                 foreign[kMax]{};    // somebody else patched over this one
    int                  count = 0;

    void*                       neverEnables = nullptr;    // the hook at this target cannot be enabled
    bool                        applyFails = false;        // the apply does nothing and says so
    std::atomic<std::uint32_t>* stopsDuringApply = nullptr;

    int  applies = 0;
    int  gate = 0;
    bool touchedOutsideTheGate = false;
    bool wroteOverAForeignPatch = false;

    int Find(const void* t) const noexcept {
        for (int i = 0; i < count; ++i) {
            if (target[i] == t) {
                return i;
            }
        }
        return -1;
    }
    bool AMarkIsStanding() const noexcept {
        for (int i = 0; i < count; ++i) {
            if (enabled[i] != marked[i]) {
                return true;
            }
        }
        return false;
    }
    bool Enabled(const void* t) const noexcept {
        const int i = Find(t);
        return i >= 0 && enabled[i];
    }
    void Set(int i, bool on) noexcept {
        if (!on && enabled[i] && foreign[i]) {
            wroteOverAForeignPatch = true;    // our saved bytes over their jump: what §H7 forbids
        }
        enabled[i] = on;
        marked[i] = on;
    }
};

struct ModelEngine {
    Model* m;
    bool   Create(void* target, void*, void**) const noexcept {
        if (m->Find(target) >= 0 || m->count == Model::kMax) {
            return false;
        }
        m->target[m->count++] = target;
        return true;
    }
    bool Mark(void* target, bool on) const noexcept {
        const int i = m->Find(target);
        if (i < 0) {
            return false;
        }
        m->touchedOutsideTheGate = m->touchedOutsideTheGate || m->gate == 0;
        m->marked[i] = on;
        return true;
    }
    bool QueueEnable(void* target) const noexcept { return Mark(target, true); }
    bool QueueDisable(void* target) const noexcept { return Mark(target, false); }
    bool Apply() const noexcept {
        ++m->applies;
        m->touchedOutsideTheGate = m->touchedOutsideTheGate || m->gate == 0;
        if (m->stopsDuringApply != nullptr) {
            m->stopsDuringApply->store(0u);
        }
        if (m->applyFails) {
            return false;
        }
        for (int i = 0; i < m->count; ++i) {
            if (m->enabled[i] == m->marked[i]) {
                continue;
            }
            if (m->marked[i] && m->target[i] == m->neverEnables) {
                return false;    // hook.c: the first failure ends the apply, the rest keep their marks
            }
            m->Set(i, m->marked[i]);
        }
        return true;
    }
    bool EnsureEnabled(void* target) const noexcept {
        const int i = m->Find(target);
        if (i < 0 || target == m->neverEnables) {
            return false;
        }
        m->Set(i, true);
        return true;
    }
    bool EnsureDisabled(void* target) const noexcept {
        const int i = m->Find(target);
        if (i < 0) {
            return false;
        }
        m->Set(i, false);
        return true;
    }
    void BeginQueue() const noexcept { ++m->gate; }
    void EndQueue() const noexcept { --m->gate; }
};

// "Still ours", for the model: in, and nobody patched over it.
struct ModelOurs {
    const Model* m;
    bool         operator()(const void* target, const void*) const noexcept {
        const int i = m->Find(target);
        return i >= 0 && m->enabled[i] && !m->foreign[i];
    }
};

int g_a = 0;
int g_b = 0;
int g_c = 0;
int g_detour = 0;

}    // namespace

TEST_CASE("a batch puts three patches in under one apply, and the stop takes them out under one",
          "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<3>     registry;
    fl::patch::Batch<3>        batch;
    std::atomic<std::uint32_t> observing{1u};
    void*                      first = reinterpret_cast<void*>(&First);
    void*                      second = reinterpret_cast<void*>(&Second);
    void*                      third = reinterpret_cast<void*>(&Third);
    const int                  firstPlain = First(3);
    const int                  secondPlain = Second(3);
    const int                  thirdPlain = Third(3);

    REQUIRE(MH_Initialize() == MH_OK);
    CHECK(batch.Add(registry, observing, RealQueue{}, first, reinterpret_cast<void*>(&DetourFirst),
                    reinterpret_cast<void**>(&g_origFirst), "first", nullptr, 0u) == InstallResult::Pending);
    CHECK(batch.Add(registry, observing, RealQueue{}, second, reinterpret_cast<void*>(&DetourSecond),
                    reinterpret_cast<void**>(&g_origSecond), "second", nullptr, 0u) == InstallResult::Pending);
    // The same target a second time: the engine refuses it, its slot goes back, and the others are not held up.
    CHECK(batch.Add(registry, observing, RealQueue{}, second, reinterpret_cast<void*>(&DetourSecond),
                    reinterpret_cast<void**>(&g_origSecond), "second again", nullptr,
                    0u) == InstallResult::CreateFailed);
    CHECK(batch.Add(registry, observing, RealQueue{}, third, reinterpret_cast<void*>(&DetourThird),
                    reinterpret_cast<void**>(&g_origThird), "third", nullptr, 7u) == InstallResult::Pending);
    CHECK(batch.Add(registry, observing, RealQueue{}, nullptr, first, nullptr, "nothing", nullptr, 0u) ==
          InstallResult::NoTarget);
    REQUIRE(batch.Count() == 3u);

    // Added is not patched: nothing is at any target, and the stop's walk has nothing to see.
    CHECK(First(3) == firstPlain);
    CHECK(Third(3) == thirdPlain);
    CHECK(registry.LiveCount() == 0u);

    batch.Commit(registry, observing, RealQueue{}, RealOurs);
    for (std::uint32_t i = 0; i < batch.Count(); ++i) {
        CHECK(batch.At(i).result == InstallResult::Installed);
    }
    CHECK(batch.At(2).word == 7u);    // what the caller attached comes back with the item
    CHECK(First(3) == firstPlain + 1000000);
    CHECK(Second(3) == secondPlain + 2000000);
    CHECK(Third(3) == thirdPlain + 3000000);
    CHECK(registry.LiveCount() == 3u);
    batch.Clear();
    CHECK(batch.Count() == 0u);

    // The stop: one walk, one apply, every patch reported as ours and restored.
    observing.store(0u);
    int restored = 0;
    int declined = 0;
    fl::patch::RestoreAll(registry, RealQueue{}, RealOurs,
                          [&restored, &declined](const char*, void*, bool wasOurs, bool ok) noexcept {
                              restored += (wasOurs && ok) ? 1 : 0;
                              declined += wasOurs ? 0 : 1;
                          });
    CHECK(restored == 3);
    CHECK(declined == 0);
    CHECK(First(3) == firstPlain);
    CHECK(Second(3) == secondPlain);
    CHECK(Third(3) == thirdPlain);

    // Stopping is one-way for a batch as for a single patch: nothing is created after it.
    CHECK(batch.Add(registry, observing, RealQueue{}, first, reinterpret_cast<void*>(&DetourFirst),
                    reinterpret_cast<void**>(&g_origFirst), "first", nullptr, 0u) == InstallResult::Stopped);
    MH_Uninitialize();
}

TEST_CASE("the stop's batch leaves a patch a foreign overlay patched over exactly as it is, and restores the others",
          "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<2>     registry;
    fl::patch::Batch<2>        batch;
    std::atomic<std::uint32_t> observing{1u};
    void*                      first = reinterpret_cast<void*>(&First);
    void*                      second = reinterpret_cast<void*>(&Second);
    const int                  secondPlain = Second(3);

    REQUIRE(MH_Initialize() == MH_OK);
    REQUIRE(batch.Add(registry, observing, RealQueue{}, first, reinterpret_cast<void*>(&DetourFirst),
                      reinterpret_cast<void**>(&g_origFirst), "first", nullptr, 0u) == InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, RealQueue{}, second, reinterpret_cast<void*>(&DetourSecond),
                      reinterpret_cast<void**>(&g_origSecond), "second", nullptr, 0u) == InstallResult::Pending);
    batch.Commit(registry, observing, RealQueue{}, RealOurs);
    REQUIRE(registry.LiveCount() == 2u);

    // A foreign overlay patches First after us.
    std::uint8_t* foreignRelay = MakeRelay(reinterpret_cast<void*>(&ForeignDetour), first);
    std::int32_t  savedRel = 0;
    RepointJump(first, foreignRelay, &savedRel);
    std::uint8_t theirs[5]{};
    std::memcpy(theirs, first, sizeof(theirs));
    REQUIRE(First(5) == 4);    // their hook is what runs

    observing.store(0u);
    int restored = 0;
    int declined = 0;
    fl::patch::RestoreAll(registry, RealQueue{}, RealOurs,
                          [&restored, &declined](const char*, void*, bool wasOurs, bool ok) noexcept {
                              restored += (wasOurs && ok) ? 1 : 0;
                              declined += wasOurs ? 0 : 1;
                          });
    CHECK(restored == 1);
    CHECK(declined == 1);
    CHECK(std::memcmp(theirs, first, sizeof(theirs)) == 0);    // not one byte of theirs was written
    CHECK(First(5) == 4);
    CHECK(Second(3) == secondPlain);

    // ...and no mark was left on the declined one: an apply by anybody afterwards still does not touch it.
    CHECK(MH_ApplyQueued() == MH_OK);
    CHECK(std::memcmp(theirs, first, sizeof(theirs)) == 0);

    RestoreJump(first, savedRel);    // the foreign overlay goes away before MinHook is torn down
    MH_Uninitialize();
    VirtualFree(foreignRelay, 0, MEM_RELEASE);
}

TEST_CASE("an apply that fails halfway costs one patch, and leaves no mark for a later apply to act on",
          "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<3>     registry;
    fl::patch::Batch<3>        batch;
    std::atomic<std::uint32_t> observing{1u};
    Model                      model;
    model.neverEnables = &g_b;
    const ModelEngine engine{&model};

    REQUIRE(batch.Add(registry, observing, engine, &g_a, &g_detour, nullptr, "a", nullptr, 0u) ==
            InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, engine, &g_b, &g_detour, nullptr, "b", nullptr, 0u) ==
            InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, engine, &g_c, &g_detour, nullptr, "c", nullptr, 0u) ==
            InstallResult::Pending);
    CHECK_FALSE(model.AMarkIsStanding());    // adding marks nothing
    batch.Commit(registry, observing, engine, ModelOurs{&model});

    CHECK(batch.At(0).result == InstallResult::Installed);
    CHECK(batch.At(1).result == InstallResult::EnableFailed);
    CHECK(batch.At(2).result == InstallResult::Installed);    // the apply stopped at b; c went in by itself
    CHECK(model.Enabled(&g_a));
    CHECK_FALSE(model.Enabled(&g_b));
    CHECK(model.Enabled(&g_c));
    CHECK(registry.LiveCount() == 2u);
    CHECK(model.applies == 1);
    CHECK_FALSE(model.AMarkIsStanding());
    CHECK_FALSE(model.touchedOutsideTheGate);
    CHECK(model.gate == 0);

    // The stop's apply -- anybody's apply -- must not be what puts b in.
    model.neverEnables = nullptr;
    CHECK(engine.Apply());
    CHECK_FALSE(model.Enabled(&g_b));

    // b's slot went back: the third slot can be claimed again.
    CHECK(registry.Claim(&g_b, &g_detour, "b again") >= 0);
}

TEST_CASE("an apply that does nothing at all still gets every patch in, one at a time", "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<2>     registry;
    fl::patch::Batch<2>        batch;
    std::atomic<std::uint32_t> observing{1u};
    Model                      model;
    model.applyFails = true;    // MinHook could not stop the threads: nothing applied, one status for all
    const ModelEngine engine{&model};

    REQUIRE(batch.Add(registry, observing, engine, &g_a, &g_detour, nullptr, "a", nullptr, 0u) ==
            InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, engine, &g_b, &g_detour, nullptr, "b", nullptr, 0u) ==
            InstallResult::Pending);
    batch.Commit(registry, observing, engine, ModelOurs{&model});
    CHECK(batch.At(0).result == InstallResult::Installed);
    CHECK(batch.At(1).result == InstallResult::Installed);
    CHECK(registry.LiveCount() == 2u);
    CHECK_FALSE(model.AMarkIsStanding());

    // And the stop, with the same failing apply, still takes both out.
    observing.store(0u);
    int restored = 0;
    fl::patch::RestoreAll(
        registry, engine, ModelOurs{&model},
        [&restored](const char*, void*, bool wasOurs, bool ok) noexcept { restored += (wasOurs && ok) ? 1 : 0; });
    CHECK(restored == 2);
    CHECK_FALSE(model.Enabled(&g_a));
    CHECK_FALSE(model.Enabled(&g_b));
    CHECK_FALSE(model.AMarkIsStanding());
    CHECK_FALSE(model.touchedOutsideTheGate);
}

TEST_CASE("a stop before the batch commits enables nothing; a stop inside its apply is undone by the batch itself",
          "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    Model             model;
    const ModelEngine engine{&model};

    SECTION("the stop ran between the adds and the commit") {
        fl::patch::Registry<2>     registry;
        fl::patch::Batch<2>        batch;
        std::atomic<std::uint32_t> observing{1u};
        REQUIRE(batch.Add(registry, observing, engine, &g_a, &g_detour, nullptr, "a", nullptr, 0u) ==
                InstallResult::Pending);
        REQUIRE(batch.Add(registry, observing, engine, &g_b, &g_detour, nullptr, "b", nullptr, 0u) ==
                InstallResult::Pending);
        observing.store(0u);
        batch.Commit(registry, observing, engine, ModelOurs{&model});
        CHECK(batch.At(0).result == InstallResult::StoppedDuring);
        CHECK(batch.At(1).result == InstallResult::StoppedDuring);
        CHECK(model.applies == 0);    // nothing was marked, nothing applied
        CHECK_FALSE(model.Enabled(&g_a));
        CHECK_FALSE(model.Enabled(&g_b));
        CHECK(registry.LiveCount() == 0u);
        CHECK(registry.Claim(&g_a, &g_detour, "free again") >= 0);    // never live, so not retired
    }

    SECTION("the stop ran while the apply was putting them in") {
        fl::patch::Registry<2>     registry;
        fl::patch::Batch<2>        batch;
        std::atomic<std::uint32_t> observing{1u};
        model.stopsDuringApply = &observing;
        REQUIRE(batch.Add(registry, observing, engine, &g_a, &g_detour, nullptr, "a", nullptr, 0u) ==
                InstallResult::Pending);
        REQUIRE(batch.Add(registry, observing, engine, &g_b, &g_detour, nullptr, "b", nullptr, 0u) ==
                InstallResult::Pending);
        batch.Commit(registry, observing, engine, ModelOurs{&model});
        CHECK(batch.At(0).result == InstallResult::StoppedDuring);
        CHECK(batch.At(1).result == InstallResult::StoppedDuring);
        CHECK_FALSE(model.Enabled(&g_a));    // put in, and taken out again by the installer
        CHECK_FALSE(model.Enabled(&g_b));
        CHECK(model.applies == 2);    // one in, one out
        CHECK(registry.LiveCount() == 0u);
        CHECK(registry.Claim(&g_a, &g_detour, "again") == -1);    // they were live: the slots are never reused
        CHECK_FALSE(model.AMarkIsStanding());
        CHECK_FALSE(model.touchedOutsideTheGate);
        CHECK(model.gate == 0);
    }
}

TEST_CASE("the stop marks only what is still ours: a foreign patch is not written, a restored patch is not put back",
          "[unhook][registry][batch]") {
    using fl::patch::InstallResult;
    fl::patch::Registry<3>     registry;
    fl::patch::Batch<3>        batch;
    std::atomic<std::uint32_t> observing{1u};
    Model                      model;
    const ModelEngine          engine{&model};

    REQUIRE(batch.Add(registry, observing, engine, &g_a, &g_detour, nullptr, "a", nullptr, 0u) ==
            InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, engine, &g_b, &g_detour, nullptr, "b", nullptr, 0u) ==
            InstallResult::Pending);
    REQUIRE(batch.Add(registry, observing, engine, &g_c, &g_detour, nullptr, "c", nullptr, 0u) ==
            InstallResult::Pending);
    batch.Commit(registry, observing, engine, ModelOurs{&model});
    REQUIRE(registry.LiveCount() == 3u);

    model.foreign[model.Find(&g_a)] = true;    // a: somebody patched over us
    // c: committed, and already taken out -- an installer between its own disable and its Retire. The draft that
    // marked everything and then un-marked what it must not restore marked THIS one for enabling, and the stop's own
    // apply put the patch back in after the stop.
    model.Set(model.Find(&g_c), false);
    model.applies = 0;

    observing.store(0u);
    int ours = 0;
    int notOurs = 0;
    fl::patch::RestoreAll(registry, engine, ModelOurs{&model},
                          [&ours, &notOurs](const char*, void*, bool wasOurs, bool ok) noexcept {
                              ours += (wasOurs && ok) ? 1 : 0;
                              notOurs += wasOurs ? 0 : 1;
                          });
    CHECK(ours == 1);       // b
    CHECK(notOurs == 2);    // a and c
    CHECK(model.applies == 1);
    CHECK(model.Enabled(&g_a));    // theirs: still in
    CHECK_FALSE(model.wroteOverAForeignPatch);
    CHECK_FALSE(model.Enabled(&g_b));
    CHECK_FALSE(model.Enabled(&g_c));    // stayed out
    CHECK_FALSE(model.AMarkIsStanding());
    CHECK_FALSE(model.touchedOutsideTheGate);
    CHECK(model.gate == 0);

    // Nothing of ours left: a second stop's walk marks nothing and applies nothing.
    model.applies = 0;
    fl::patch::RestoreAll(registry, engine, ModelOurs{&model}, [](const char*, void*, bool, bool) noexcept {});
    CHECK(model.applies == 0);
}
