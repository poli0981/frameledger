// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// When the Overlay's watchdog makes its PERIODIC tick (0.1.0-beta.49, owner decision D150).
//
// WHAT WAS WRONG. The watchdog waited a second AFTER each tick's work, so its ticks were a second and that work
// apart -- and a tick the LoadLibrary detour woke early counted as a tick like any other. The driver's latency
// report is read once for each tick that arms it, and region 5 is published once a tick; the Agent samples on a
// period of exactly one second. So the Overlay fell behind the Agent a little with every tick, a woken tick took a
// read and armed none, and a session's samples had holes: 0.4 to 16 % of them carried no report (HANDOFF, item 46).
//
// WHAT THIS IS. A grid in performance-counter ticks: the periodic tick is due at a deadline, and the next deadline is
// one period after the one that was due -- not after the moment the tick ran, so nothing accumulates. A tick woken
// early is still a tick (the loader's signals must be acted on at once) but not a periodic one: it does not move the
// grid. A periodic tick that ran more than half a period late does not get a second one right behind it: the grid
// starts again from that tick, so two periodic ticks are never less than half a period apart.
//
// Pure arithmetic, no call: the watchdog's loop (dllmain.cpp) is the only user, and fl_watchdog_tick_test drives it
// without a thread.

#ifndef FRAMELEDGER_FL_WATCHDOG_TICK_H
#define FRAMELEDGER_FL_WATCHDOG_TICK_H

#include <cstdint>

namespace fl::watchdog {

struct Grid {
    int64_t period = 1;      // performance-counter ticks between two periodic ticks
    int64_t deadline = 0;    // the counter value the next periodic tick is due at
};

inline void StartGrid(Grid& grid, int64_t now, int64_t period) noexcept {
    grid.period = period > 0 ? period : 1;
    grid.deadline = now + grid.period;
}

// How long to wait for the periodic tick, in whole milliseconds ROUNDED UP -- a wait rounded down ends before the
// deadline and would have to be followed by another of a millisecond -- and never more than `ceilingMs`. Zero when
// the tick is due.
inline uint32_t WaitMs(const Grid& grid, int64_t now, int64_t frequency, uint32_t ceilingMs) noexcept {
    if (now >= grid.deadline || frequency <= 0) {
        return 0u;
    }
    const int64_t ms = ((grid.deadline - now) * 1000 + frequency - 1) / frequency;
    return ms > static_cast<int64_t>(ceilingMs) ? ceilingMs : static_cast<uint32_t>(ms);
}

// Whether the tick that begins at `now` is the periodic one. A wait that timed out is: Windows counts a timeout in
// its own clock's ticks, so it may end a little short of the deadline by this counter, and that tick is the one the
// wait was for. A wait a signal ended is periodic only when the deadline has passed all the same.
inline bool IsPeriodic(const Grid& grid, int64_t now, bool timedOut) noexcept {
    return timedOut || now >= grid.deadline;
}

// The grid after a periodic tick that began at `now`.
inline void Advance(Grid& grid, int64_t now) noexcept {
    const int64_t next = grid.deadline + grid.period;
    grid.deadline = next - now < grid.period / 2 ? now + grid.period : next;
}

}    // namespace fl::watchdog

#endif    // FRAMELEDGER_FL_WATCHDOG_TICK_H
