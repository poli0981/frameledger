// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#pragma once

// The §S16 scan set's walk, as a function of one process snapshot (beta.15, owner decision D51).
//
// The guard scans the injection target, its ancestors up to (excluding) the first platform launcher, and its
// descendants. A snapshot links a process to its parent by a pid alone, and Windows reuses pids: a process whose parent
// has exited names a pid that may now belong to anything. Until beta.15 the walk followed that pid - an unrelated
// process scanned as the game's "launcher", a protected one refusing the session as unreadable, a blocklisted one
// turning the game's hooking off for good (19_SAFETY §What a finding does to the game) - and a cycle of reused pids
// sent the same processes again and again.
//
// D51: a parent/child link is cut ONLY when it is proven wrong - both creation times read, and the child created before
// the process whose pid it names as its parent. A time that cannot be read keeps the link: the guard fails closed
// (CLAUDE.md rule 2), and a member it then cannot read refuses as before. Each pid is walked once and sent once.
//
// Pure over its inputs and allocation-free, so guard_test.cpp drives it with rows of its own; EnumerateScanSetImpl
// (fl_guard_sources.cpp) feeds it a Toolhelp snapshot and ProcessCreatedAt.

#include <cstddef>
#include <cstdint>

#include "fl_guard.h"

namespace fl::guard {

// One process of the snapshot.
struct ScanSetRow {
    std::uint32_t pid;
    std::uint32_t ppid;
    bool          launcher;    // a platform launcher: the ancestor walk stops below it
};

// What the walk asks of its caller: a process's creation time (FILETIME ticks; 0 when it could not be read), and where
// each member goes (false stops the walk, as the scan-set sink does when its set is full).
struct ScanSetEnv {
    std::uint64_t (*created)(void* ctx, std::uint32_t pid);
    void* createdCtx;
    bool (*sink)(void* ctx, std::uint32_t pid);
    void* sinkCtx;
};

inline constexpr std::size_t kScanSetMaxDepth = 16;     // ancestors walked at most
inline constexpr std::size_t kScanSetFrontier = 512;    // the target and its descendants; one more is kFailed

// The target first, then its ancestors nearest first, then its descendants breadth-first. kFailed when the target is
// not in `rows` or the descendants overflow kScanSetFrontier; kOk otherwise, including when the sink stopped the walk.
Collected WalkScanSet(const ScanSetRow* rows, std::size_t count, std::uint32_t targetPid,
                      const ScanSetEnv& env) noexcept;

// D51's one rule: a link is proven wrong when both times were read and the child was created before its parent. Equal
// times keep it - a parent and a child can be created within one tick of the clock.
constexpr bool ProvenNotItsChild(std::uint64_t parentCreated, std::uint64_t childCreated) noexcept {
    return parentCreated != 0 && childCreated != 0 && childCreated < parentCreated;
}

// A process's creation time, read with the module scan's own read-only right (PROCESS_QUERY_LIMITED_INFORMATION) and
// GetProcessTimes, which reads nothing inside the process (rule 4). 0 when it cannot be read. fl_guard_sources.cpp.
std::uint64_t ProcessCreatedAt(std::uint32_t pid) noexcept;

}    // namespace fl::guard
