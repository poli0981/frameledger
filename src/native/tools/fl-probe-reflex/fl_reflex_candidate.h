// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// What the probe adds to the call sequence it measures (docs/spike-notes §30, §31).
//
// THE SEQUENCE ITSELF MOVED. Until the owner said go (D136) the candidate lived here and nowhere that ships; it is now
// FrameLedger.Overlay/include/fl_reflex.h, the header the Overlay is built from, and this probe includes that one --
// so its ctest keeps running the shipped sequence, under the Overlay's flags and with Control Flow Guard enforced.
// What stays here is the probe's own way of printing a report, which the Overlay does not use: its reader is
// fl_adv_fields.h's, with the rules a game needs (the newest frames are not final, an impossible span is refused).

#ifndef FRAMELEDGER_FL_REFLEX_CANDIDATE_H
#define FRAMELEDGER_FL_REFLEX_CANDIDATE_H

#include <fl_reflex.h>

namespace fl::reflex {

// What a report holds, in numbers: how many of its sixty-four frames carry anything, and the newest one's id and
// two spans in the report's own unit -- simulation start to present end (the game's side) and to the end of GPU work.
struct Digest {
    std::uint32_t valid = 0;
    std::uint64_t newestFrameId = 0;
    std::uint64_t newestSimToPresentEnd = 0;
    std::uint64_t newestSimToGpuEnd = 0;
    std::uint32_t newestGpuActiveUs = 0;
};

inline Digest Summarise(const NV_LATENCY_RESULT_PARAMS& report) noexcept {
    Digest d{};
    for (int i = 0; i < 64; ++i) {
        const auto& r = report.frameReport[i];
        if (r.frameID == 0u && r.simStartTime == 0u && r.gpuRenderEndTime == 0u) {
            continue;
        }
        ++d.valid;
        d.newestFrameId = r.frameID;
        d.newestSimToPresentEnd = r.presentEndTime >= r.simStartTime ? r.presentEndTime - r.simStartTime : 0u;
        d.newestSimToGpuEnd = r.gpuRenderEndTime >= r.simStartTime ? r.gpuRenderEndTime - r.simStartTime : 0u;
        d.newestGpuActiveUs = r.gpuActiveRenderTimeUs;
    }
    return d;
}

}    // namespace fl::reflex

#endif    // FRAMELEDGER_FL_REFLEX_CANDIDATE_H
