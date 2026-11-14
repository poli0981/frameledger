// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_watchdog_tick.h: when the Overlay's watchdog makes its periodic tick (0.1.0-beta.49, owner decision D150),
// driven without a thread and without a clock -- the header the Overlay compiles, on counter values written here.
//
// What it holds: the grid does not drift with the work of a tick or with the lateness of a wake; a tick woken early
// is not the periodic one and does not move the grid; a wait is never rounded down to end before its deadline; and
// two periodic ticks are never less than half a period apart, however late one ran.

#include <catch2/catch_test_macros.hpp>
#include <cstdint>
#include <fl_watchdog_tick.h>

namespace {

constexpr std::int64_t kFrequency = 10'000'000;    // the performance counter's on the machines this runs on
constexpr std::int64_t kPeriod = kFrequency;       // one second
constexpr std::int64_t kMs = kFrequency / 1000;

}    // namespace

TEST_CASE("the periodic tick keeps a grid of one period whatever each tick's work and lateness were", "[watchdog]") {
    fl::watchdog::Grid grid;
    fl::watchdog::StartGrid(grid, 5 * kPeriod, kPeriod);
    CHECK(grid.deadline == 6 * kPeriod);

    // Sixty ticks, each begun 3 to 17 ms after its deadline (the timer's own granularity and a busy machine) and each
    // followed by work of up to 240 ms: the deadlines stay whole seconds.
    std::int64_t now = 5 * kPeriod;
    for (std::int64_t tick = 0; tick < 60; ++tick) {
        const std::uint32_t wait = fl::watchdog::WaitMs(grid, now, kFrequency, 1000u);
        CHECK(wait <= 1000u);
        CHECK(now + wait * kMs >= grid.deadline);    // never rounded down to end short of it
        now = grid.deadline + (3 + tick % 15) * kMs;
        REQUIRE(fl::watchdog::IsPeriodic(grid, now, true));
        fl::watchdog::Advance(grid, now);
        CHECK(grid.deadline == (7 + tick) * kPeriod);
        now += (tick % 5) * 60 * kMs;    // the tick's work
    }
}

TEST_CASE("a wait is whole milliseconds rounded up, zero when the tick is due, and never longer than its ceiling",
          "[watchdog]") {
    fl::watchdog::Grid grid;
    fl::watchdog::StartGrid(grid, 0, kPeriod);
    CHECK(fl::watchdog::WaitMs(grid, 0, kFrequency, 1000u) == 1000u);
    CHECK(fl::watchdog::WaitMs(grid, kPeriod - 5 * kMs, kFrequency, 1000u) == 5u);
    CHECK(fl::watchdog::WaitMs(grid, kPeriod - 5 * kMs + 1, kFrequency, 1000u) == 5u);    // 4.9999 ms: five, not four
    CHECK(fl::watchdog::WaitMs(grid, kPeriod - 1, kFrequency, 1000u) == 1u);
    CHECK(fl::watchdog::WaitMs(grid, kPeriod, kFrequency, 1000u) == 0u);
    CHECK(fl::watchdog::WaitMs(grid, kPeriod + 40 * kMs, kFrequency, 1000u) == 0u);
    CHECK(fl::watchdog::WaitMs(grid, -3 * kPeriod, kFrequency, 1000u) == 1000u);    // a counter that went back
    CHECK(fl::watchdog::WaitMs(grid, 0, 0, 1000u) == 0u);                           // no frequency: no wait to compute
}

TEST_CASE("a tick something woke early is not the periodic one and leaves the grid where it was", "[watchdog]") {
    fl::watchdog::Grid grid;
    fl::watchdog::StartGrid(grid, 0, kPeriod);
    const std::int64_t deadline = grid.deadline;

    // Five wakes inside one period: none is periodic, and the wait after each is what is left to the same deadline.
    for (std::int64_t wake = 1; wake <= 5; ++wake) {
        const std::int64_t now = wake * 150 * kMs;
        CHECK_FALSE(fl::watchdog::IsPeriodic(grid, now, false));
        CHECK(grid.deadline == deadline);
        CHECK(fl::watchdog::WaitMs(grid, now, kFrequency, 1000u) == static_cast<std::uint32_t>(1000 - wake * 150));
    }
    // A signal that came when the deadline had passed all the same: that tick is the periodic one.
    CHECK(fl::watchdog::IsPeriodic(grid, deadline + 2 * kMs, false));
    // And a wait that timed out a little short of the deadline, by this counter, is the tick the wait was for.
    CHECK(fl::watchdog::IsPeriodic(grid, deadline - kMs / 2, true));
    fl::watchdog::Advance(grid, deadline - kMs / 2);
    CHECK(grid.deadline == deadline + kPeriod);    // on the grid, not a period after the early moment
}

TEST_CASE("a periodic tick that ran late is not followed by another within half a period", "[watchdog]") {
    fl::watchdog::Grid grid;
    fl::watchdog::StartGrid(grid, 0, kPeriod);

    // 400 ms late: the next is on the grid, 600 ms away.
    fl::watchdog::Advance(grid, kPeriod + 400 * kMs);
    CHECK(grid.deadline == 2 * kPeriod);

    // 600 ms late: the grid's next would be 400 ms away, so the grid starts again from this tick.
    fl::watchdog::Advance(grid, 2 * kPeriod + 600 * kMs);
    CHECK(grid.deadline == 3 * kPeriod + 600 * kMs);

    // A tick whose work took seconds (an install batch on a loaded machine): one tick follows, a period on -- not
    // one for each deadline that passed.
    fl::watchdog::Advance(grid, 9 * kPeriod);
    CHECK(grid.deadline == 10 * kPeriod);
    CHECK(fl::watchdog::WaitMs(grid, 9 * kPeriod, kFrequency, 1000u) == 1000u);

    // A period that is not a positive number is one tick, not a division by zero or a wait without end.
    fl::watchdog::StartGrid(grid, 100, 0);
    CHECK(grid.period == 1);
    CHECK(grid.deadline == 101);
}
