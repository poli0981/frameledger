// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#include <fl_guard_scanset.h>

namespace fl::guard {
namespace {

const ScanSetRow* Find(const ScanSetRow* rows, std::size_t count, std::uint32_t pid) noexcept {
    for (std::size_t i = 0; i < count; ++i) {
        if (rows[i].pid == pid) {
            return &rows[i];
        }
    }
    return nullptr;
}

bool Contains(const std::uint32_t* pids, std::size_t count, std::uint32_t pid) noexcept {
    for (std::size_t i = 0; i < count; ++i) {
        if (pids[i] == pid) {
            return true;
        }
    }
    return false;
}

}    // namespace

Collected WalkScanSet(const ScanSetRow* rows, std::size_t count, std::uint32_t targetPid,
                      const ScanSetEnv& env) noexcept {
    const ScanSetRow* target = Find(rows, count, targetPid);
    if (target == nullptr) {
        return Collected::kFailed;    // we were asked about a process that is not there
    }

    // What went to the sink, so nothing goes twice: the target, at most kScanSetMaxDepth ancestors, the descendants.
    std::uint32_t sent[1 + kScanSetMaxDepth + kScanSetFrontier];
    std::size_t   sentCount = 0;
    if (!env.sink(env.sinkCtx, targetPid)) {
        return Collected::kOk;
    }
    sent[sentCount++] = targetPid;
    const std::uint64_t targetCreated = env.created(env.createdCtx, targetPid);

    // Ancestors, stopping below the first platform launcher - and at a parent created after its child: the real parent
    // has exited and its pid is reused, so what holds it now is no part of the game, and nor is anything above it.
    const ScanSetRow* cur = target;
    std::uint64_t     curCreated = targetCreated;
    for (std::size_t depth = 0; depth < kScanSetMaxDepth && cur->ppid != 0; ++depth) {
        const ScanSetRow* parent = Find(rows, count, cur->ppid);
        if (parent == nullptr || parent->launcher || Contains(sent, sentCount, parent->pid)) {
            break;
        }
        const std::uint64_t parentCreated = env.created(env.createdCtx, parent->pid);
        if (ProvenNotItsChild(parentCreated, curCreated)) {
            break;
        }
        if (!env.sink(env.sinkCtx, parent->pid)) {
            return Collected::kOk;
        }
        sent[sentCount++] = parent->pid;
        cur = parent;
        curCreated = parentCreated;
    }

    // Descendants, breadth-first. A process created before the one whose pid it names as its parent is not its child -
    // its real parent held that pid earlier. Each pid is walked once, so a cycle of reused pids ends; one already sent
    // (an ancestor the snapshot also lists as a child) is walked but not sent again - an unproven link is kept.
    std::uint32_t frontier[kScanSetFrontier];
    std::uint64_t frontierCreated[kScanSetFrontier];
    std::size_t   head = 0;
    std::size_t   tail = 0;
    frontier[tail] = targetPid;
    frontierCreated[tail++] = targetCreated;
    while (head < tail) {
        const std::uint32_t p = frontier[head];
        const std::uint64_t pCreated = frontierCreated[head++];
        for (std::size_t i = 0; i < count; ++i) {
            const std::uint32_t child = rows[i].pid;
            if (rows[i].ppid != p || Contains(frontier, tail, child)) {
                continue;
            }
            const std::uint64_t created = env.created(env.createdCtx, child);
            if (ProvenNotItsChild(pCreated, created)) {
                continue;
            }
            if (tail >= kScanSetFrontier) {
                return Collected::kFailed;
            }
            frontier[tail] = child;
            frontierCreated[tail++] = created;
            if (Contains(sent, sentCount, child)) {
                continue;
            }
            if (!env.sink(env.sinkCtx, child)) {
                return Collected::kOk;
            }
            sent[sentCount++] = child;
        }
    }
    return Collected::kOk;
}

}    // namespace fl::guard
