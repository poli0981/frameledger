// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_ffx_types.h: the descriptor types an ffx-api module was handed, kept by the
// detours and written to the native log by the watchdog (0.1.0-beta.42, D121).
// The header is what the Overlay compiles, so these cases are about the shipped
// code: each type once, in the order it arrived, nothing lost when two threads
// meet a new type at the same moment, and what does not fit SAID rather than
// dropped.

#include <catch2/catch_test_macros.hpp>
#include <cstdint>
#include <fl_ffx_types.h>
#include <thread>
#include <vector>

namespace {

std::vector<std::uint64_t> Kept(const fl::ffxtypes::Seen& seen) {
    std::vector<std::uint64_t> out;
    for (const auto& slot : seen.types) {
        if (const std::uint64_t v = slot.load(); v != 0u) {
            out.push_back(v);
        }
    }
    return out;
}

}    // namespace

TEST_CASE("a type is kept once, in the order it first arrived", "[ffxtypes]") {
    fl::ffxtypes::Seen seen;
    for (int frame = 0; frame < 1000; ++frame) {
        fl::ffxtypes::Note(seen, 0x00020004u);    // PREPARE
        fl::ffxtypes::Note(seen, 0x00020003u);    // FRAMEGENERATION
        if (frame >= 10) {
            fl::ffxtypes::Note(seen, 0x00010001u);    // UPSCALE, from the eleventh frame on
        }
    }
    const std::vector<std::uint64_t> expected{0x00020004u, 0x00020003u, 0x00010001u};
    CHECK(Kept(seen) == expected);
    CHECK(seen.more.load() == 0u);
}

TEST_CASE("what does not fit is said: a ninth type, and a type of zero", "[ffxtypes]") {
    fl::ffxtypes::Seen seen;
    for (std::uint64_t type = 1; type <= fl::ffxtypes::kSlots; ++type) {
        fl::ffxtypes::Note(seen, type);
    }
    CHECK(Kept(seen).size() == fl::ffxtypes::kSlots);
    CHECK(seen.more.load() == 0u);

    fl::ffxtypes::Note(seen, 3u);    // already kept: still nothing to say
    CHECK(seen.more.load() == 0u);

    fl::ffxtypes::Note(seen, 99u);
    CHECK(seen.more.load() == fl::ffxtypes::kMoreTypes);
    CHECK(Kept(seen).size() == fl::ffxtypes::kSlots);    // and none of the eight was displaced

    fl::ffxtypes::Note(seen, 0u);
    const std::uint32_t both = fl::ffxtypes::kMoreTypes | fl::ffxtypes::kMoreZero;
    CHECK(seen.more.load() == both);

    fl::ffxtypes::Seen zeroOnly;
    fl::ffxtypes::Note(zeroOnly, 0u);
    CHECK(Kept(zeroOnly).empty());    // zero is the empty slot: it takes none
    CHECK(zeroOnly.more.load() == fl::ffxtypes::kMoreZero);
}

TEST_CASE("the watchdog's report: each kept type once, new ones on a later pass, the rest untouched", "[ffxtypes]") {
    fl::ffxtypes::Seen         seen;
    fl::ffxtypes::Reported     reported;
    std::vector<std::uint64_t> types;
    std::vector<std::uint32_t> more;
    const auto                 report = [&] {
        fl::ffxtypes::ReportNew(
            seen, reported, [&types](std::uint64_t t) noexcept { types.push_back(t); },
            [&more](std::uint32_t m) noexcept { more.push_back(m); });
    };

    report();
    CHECK(types.empty());
    CHECK(more.empty());

    fl::ffxtypes::Note(seen, 0x20004u);
    fl::ffxtypes::Note(seen, 0x20003u);
    report();
    report();    // nothing new: nothing said again
    const std::vector<std::uint64_t> firstTwo{0x20004u, 0x20003u};
    CHECK(types == firstTwo);

    fl::ffxtypes::Note(seen, 0x10001u);
    fl::ffxtypes::Note(seen, 0u);
    report();
    const std::vector<std::uint64_t> allThree{0x20004u, 0x20003u, 0x10001u};
    CHECK(types == allThree);
    const std::vector<std::uint32_t> zeroSaid{fl::ffxtypes::kMoreZero};
    CHECK(more == zeroSaid);

    for (std::uint64_t t = 100; t < 110; ++t) {
        fl::ffxtypes::Note(seen, t);
    }
    report();
    report();
    CHECK(types.size() == fl::ffxtypes::kSlots);
    const std::vector<std::uint32_t> bothSaid{fl::ffxtypes::kMoreZero, fl::ffxtypes::kMoreTypes};
    CHECK(more == bothSaid);
}

TEST_CASE("threads that meet the same new types at once keep each exactly once", "[ffxtypes]") {
    // The detours run on whatever threads the game dispatches from. Six types, eight threads, each noting all six in
    // its own order, many times over: six slots filled, no type twice, none lost, nothing said to be missing.
    for (int round = 0; round < 200; ++round) {
        fl::ffxtypes::Seen       seen;
        std::vector<std::thread> threads;
        for (int t = 0; t < 8; ++t) {
            threads.emplace_back([&seen, t] {
                for (int i = 0; i < 6; ++i) {
                    fl::ffxtypes::Note(seen, 0x10000u + static_cast<std::uint64_t>((i + t) % 6) + 1u);
                }
            });
        }
        for (std::thread& th : threads) {
            th.join();
        }
        std::vector<std::uint64_t> kept = Kept(seen);
        REQUIRE(kept.size() == 6u);
        std::uint64_t sum = 0;
        for (std::size_t i = 0; i < kept.size(); ++i) {
            sum += kept[i] - 0x10000u;
            for (std::size_t j = i + 1; j < kept.size(); ++j) {
                REQUIRE(kept[i] != kept[j]);
            }
        }
        REQUIRE(sum == 21u);    // 1 + 2 + ... + 6: every one of the six, once
        REQUIRE(seen.more.load() == 0u);
    }
}
