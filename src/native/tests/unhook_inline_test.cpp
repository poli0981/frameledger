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
