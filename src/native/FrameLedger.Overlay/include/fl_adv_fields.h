// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// What a hooked game told its upscaler, beyond what the frame record carries
// (Advanced metrics, train B; 18_GPU_VENDOR_APIS §Train B; fl_shm.h region 5).
//
// THE ONE PLACE A VENDOR STRUCT'S EXTRA MEMBERS ARE READ (owner decision D116).
// Every member this file reads is named in tools/overlay-allowed-fields.txt, and
// tools/overlay-fields-check.ps1 fails the build when a member is read here that
// is not on that list, or is on it and not read. The reads go through three
// parameters with fixed names, which is what makes that checkable:
//
//     o   const sl::DLSSOptions&            chained into slEvaluateFeature's inputs
//     d   const ffxDispatchDescUpscale&     the descriptor of an ffx upscale dispatch
//     m   const FfxApiEffectMemoryUsage&    what ffxQuery wrote, after it answered OK
//     q   the memory query's descriptor (its out-pointer), and `desc`, its header (its type)
//
// RULE 4. Each is an argument of a call the Overlay hooks, or what that call wrote
// through an out-pointer the game gave it. Nothing else is dereferenced: no
// resource, no command list, no callback, and never a camera's position, axes or
// matrices (the gate refuses those names anywhere in the Overlay).
//
// WHICH VERSION CARRIES WHICH MEMBER (owner decision D115; 17_HOOK_ENGINE §Which
// struct version carries which field). sl::DLSSOptions grew twice; a member is
// read only at the struct version that introduced it, because the game's struct
// ends where ITS version ends. ffxDispatchDescUpscale has had one declaration in
// every ffx-api release, so all of it may be read - on the ffx-api route only,
// never on the FSR 3.0 host's legacy descriptor.
//
// RULE 5. Everything a detour calls here is a handful of relaxed atomic
// operations on one process-local, cache-aligned struct: no allocation, no lock,
// no call out. Publish() runs on the watchdog, never on a game thread.
//
// SENTINELS AND VENDOR ZEROS never leave this file as values: Streamline's
// INVALID_FLOAT is FLT_MAX and its INVALID_UINT all-ones, a Boolean has a third
// state, and preset 0 is "default". See the encoding notes on FlAdvancedState.

#ifndef FRAMELEDGER_FL_ADV_FIELDS_H
#define FRAMELEDGER_FL_ADV_FIELDS_H

#include <atomic>
#include <bit>
#include <cstdint>
#include <fl_shm.h>
#include <sl.h>
// After sl.h, as fl_sl_inputs.h explains: sl_dlss.h has no includes of its own.
#include <ffx_api.h>
#include <ffx_upscale.h>
#include <sl_dlss.h>

namespace fl::adv {

// A float a detour may keep: finite, and not a vendor's "nobody set this". The
// test is on the magnitude's bits: everything below FLT_MAX passes, and FLT_MAX
// itself (Streamline's INVALID_FLOAT), both infinities and every NaN do not.
inline bool Usable(float v) noexcept {
    return (std::bit_cast<uint32_t>(v) & 0x7FFFFFFFu) < 0x7F7FFFFFu;
}

inline float Magnitude(float v) noexcept {
    return std::bit_cast<float>(std::bit_cast<uint32_t>(v) & 0x7FFFFFFFu);
}

// The highest / lowest value seen, kept as float bits. A compare-exchange only
// when the value is a new extreme, which after the first frames is almost never.
inline void KeepMax(std::atomic<uint32_t>& slot, float v) noexcept {
    const uint32_t bits = std::bit_cast<uint32_t>(v);
    uint32_t       seen = slot.load(std::memory_order_relaxed);
    while (seen == FL_ADV_FLOAT_NONE || std::bit_cast<float>(seen) < v) {
        if (slot.compare_exchange_weak(seen, bits, std::memory_order_relaxed)) {
            break;
        }
    }
}

inline void KeepMin(std::atomic<uint32_t>& slot, float v) noexcept {
    const uint32_t bits = std::bit_cast<uint32_t>(v);
    uint32_t       seen = slot.load(std::memory_order_relaxed);
    while (seen == FL_ADV_FLOAT_NONE || std::bit_cast<float>(seen) > v) {
        if (slot.compare_exchange_weak(seen, bits, std::memory_order_relaxed)) {
            break;
        }
    }
}

// OR bits into a word only when one of them is new: a flag every frame carries
// costs a load, not a locked instruction.
inline void NoteBits(std::atomic<uint32_t>& slot, uint32_t bits) noexcept {
    if ((slot.load(std::memory_order_relaxed) & bits) != bits) {
        slot.fetch_or(bits, std::memory_order_relaxed);
    }
}

// sl::Boolean PLUS ONE, or 0 for its third state ("invalid": the game set nothing).
inline uint8_t BooleanPlusOne(sl::Boolean b) noexcept {
    return (b == sl::Boolean::eFalse || b == sl::Boolean::eTrue) ? static_cast<uint8_t>(static_cast<uint8_t>(b) + 1u)
                                                                 : uint8_t{0};
}

// sl::DLSSPreset PLUS ONE, or 0 for a value a byte cannot carry.
inline uint8_t PresetPlusOne(sl::DLSSPreset p) noexcept {
    const auto v = static_cast<uint32_t>(p);
    return v < 0xFFu ? static_cast<uint8_t>(v + 1u) : uint8_t{0};
}

// A bool as the vendor's C struct stores it, read as the byte it is: any other
// read of a byte that is neither 0 nor 1 is undefined.
inline bool ByteIsSet(const bool& field) noexcept {
    return *reinterpret_cast<const unsigned char*>(&field) != 0u;
}

// What the detours accumulate between two publications. Process-local; one
// instance, in the Overlay, which aligns it to a cache line of its own (on the
// variable, not here: an aligned struct is a padded struct, and /W4 says so).
// Counts wrap, as the region's do.
struct Accumulators {
    std::atomic<uint32_t> validMask{0u};    // FlAdvancedValid

    // sl::DLSSOptions, as last seen
    std::atomic<uint32_t> dlssStructVersion{0u};
    std::atomic<uint32_t> dlssOptionsSeen{0u};
    std::atomic<uint32_t> dlssOutputWidth{0u};
    std::atomic<uint32_t> dlssOutputHeight{0u};
    std::atomic<uint32_t> dlssPreExposureBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> dlssExposureScaleBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint64_t> dlssPresets{0u};     // six bytes, DLAA in the lowest
    std::atomic<uint32_t> dlssBooleans{0u};    // three bytes: HDR input, auto-exposure, alpha upscaling

    // the ffx upscale dispatch
    std::atomic<uint32_t> ffxOwner{0u};    // leaf + 1 of the module whose dispatches are counted
    std::atomic<uint32_t> ffxDispatches{0u};
    std::atomic<uint32_t> ffxSharpenSeen{0u};
    std::atomic<uint32_t> ffxSharpnessMinBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxSharpnessMaxBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxJitterMaxXBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxJitterMaxYBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxPreExposureMinBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxPreExposureMaxBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxNearBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxFarBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxFovMinBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxFovMaxBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> ffxFlagsSeen{0u};
    std::atomic<uint32_t> ffxResets{0u};
    std::atomic<uint32_t> ffxFrameTimeCount{0u};    // drained by Publish
    std::atomic<uint64_t> ffxFrameTimeSumUs{0u};    // drained by Publish

    // the ffx upscaler's own memory, as the last answered query gave it
    std::atomic<uint64_t> ffxMemoryTotalBytes{0u};
    std::atomic<uint64_t> ffxMemoryAliasableBytes{0u};
    std::atomic<uint32_t> ffxMemoryQueries{0u};
    std::atomic<uint32_t> ffxMemoryOwner{0u};    // leaf + 1 of the module whose answered queries are counted
};

// Whether `leaf` is the module whose calls a summary counts: the first one seen, and
// from then on only it. A title that goes through the loader has the same call
// arrive at two hooked modules; a count is a count of calls and not of hooks.
inline bool Owns(std::atomic<uint32_t>& ownerSlot, uint32_t leaf) noexcept {
    const uint32_t me = leaf + 1u;
    uint32_t       owner = ownerSlot.load(std::memory_order_relaxed);
    if (owner == 0u && ownerSlot.compare_exchange_strong(owner, me, std::memory_order_relaxed)) {
        owner = me;
    }
    return owner == me;
}

// sl::DLSSOptions, chained into slEvaluateFeature's inputs. The caller has already
// matched the struct's GUID and found its version at least 1 (fl_sl_inputs.h).
inline void ObserveDlssOptions(const sl::DLSSOptions& o, Accumulators& a) noexcept {
    const size_t version = o.structVersion;
    if (version < sl::kStructVersion1) {
        return;
    }

    a.dlssStructVersion.store(version > 0xFFFFu ? 0xFFFFu : static_cast<uint32_t>(version), std::memory_order_relaxed);
    a.dlssOptionsSeen.fetch_add(1u, std::memory_order_relaxed);

    // Version 1's members. A size the game left at INVALID_UINT, or one no display has, is "not given".
    const uint32_t w = o.outputWidth;
    const uint32_t h = o.outputHeight;
    const bool     sized = w != 0u && h != 0u && w <= 0xFFFFu && h <= 0xFFFFu;
    a.dlssOutputWidth.store(sized ? w : 0u, std::memory_order_relaxed);
    a.dlssOutputHeight.store(sized ? h : 0u, std::memory_order_relaxed);
    a.dlssPreExposureBits.store(Usable(o.preExposure) ? std::bit_cast<uint32_t>(o.preExposure) : FL_ADV_FLOAT_NONE,
                                std::memory_order_relaxed);
    a.dlssExposureScaleBits.store(Usable(o.exposureScale) ? std::bit_cast<uint32_t>(o.exposureScale)
                                                          : FL_ADV_FLOAT_NONE,
                                  std::memory_order_relaxed);

    uint64_t presets = static_cast<uint64_t>(PresetPlusOne(o.dlaaPreset)) |
                       (static_cast<uint64_t>(PresetPlusOne(o.qualityPreset)) << 8) |
                       (static_cast<uint64_t>(PresetPlusOne(o.balancedPreset)) << 16) |
                       (static_cast<uint64_t>(PresetPlusOne(o.performancePreset)) << 24) |
                       (static_cast<uint64_t>(PresetPlusOne(o.ultraPerformancePreset)) << 32);
    uint32_t booleans = BooleanPlusOne(o.colorBuffersHDR);

    // Version 2 added two members; a version-1 struct ends before them.
    if (version >= sl::kStructVersion2) {
        presets |= static_cast<uint64_t>(PresetPlusOne(o.ultraQualityPreset)) << 40;
        booleans |= static_cast<uint32_t>(BooleanPlusOne(o.useAutoExposure)) << 8;
    }
    // Version 3 added one.
    if (version >= sl::kStructVersion3) {
        booleans |= static_cast<uint32_t>(BooleanPlusOne(o.alphaUpscalingEnabled)) << 16;
    }

    a.dlssPresets.store(presets, std::memory_order_relaxed);
    a.dlssBooleans.store(booleans, std::memory_order_relaxed);
    NoteBits(a.validMask, FL_ADV_VALID_DLSS_OPTIONS);
}

// The ffx upscale dispatch, beyond the two sizes the frame record already has.
//
// ONE MODULE'S DISPATCHES ARE COUNTED (Owns): the first module seen owns the
// accumulators and the other's calls are passed over.
inline void ObserveFfxUpscale(uint32_t leaf, const ffxDispatchDescUpscale& d, Accumulators& a) noexcept {
    if (!Owns(a.ffxOwner, leaf)) {
        return;
    }

    a.ffxDispatches.fetch_add(1u, std::memory_order_relaxed);

    const bool sharpening = ByteIsSet(d.enableSharpening);
    NoteBits(a.ffxSharpenSeen, sharpening ? 2u : 1u);
    if (sharpening && Usable(d.sharpness)) {
        KeepMin(a.ffxSharpnessMinBits, d.sharpness);
        KeepMax(a.ffxSharpnessMaxBits, d.sharpness);
    }

    if (Usable(d.jitterOffset.x) && Usable(d.jitterOffset.y)) {
        KeepMax(a.ffxJitterMaxXBits, Magnitude(d.jitterOffset.x));
        KeepMax(a.ffxJitterMaxYBits, Magnitude(d.jitterOffset.y));
    }

    if (Usable(d.preExposure)) {
        KeepMin(a.ffxPreExposureMinBits, d.preExposure);
        KeepMax(a.ffxPreExposureMaxBits, d.preExposure);
    }

    if (Usable(d.cameraNear)) {
        a.ffxNearBits.store(std::bit_cast<uint32_t>(d.cameraNear), std::memory_order_relaxed);
    }
    if (Usable(d.cameraFar)) {
        a.ffxFarBits.store(std::bit_cast<uint32_t>(d.cameraFar), std::memory_order_relaxed);
    }
    // The field of view as a range (owner decision D114): the lowest and the highest, never a series.
    if (Usable(d.cameraFovAngleVertical) && d.cameraFovAngleVertical > 0.0f) {
        KeepMin(a.ffxFovMinBits, d.cameraFovAngleVertical);
        KeepMax(a.ffxFovMaxBits, d.cameraFovAngleVertical);
    }

    if (d.flags != 0u) {
        NoteBits(a.ffxFlagsSeen, d.flags);
    }
    if (ByteIsSet(d.reset)) {
        a.ffxResets.fetch_add(1u, std::memory_order_relaxed);
    }

    // Milliseconds, as the game reports them. A value outside (0, 10 s) is not a frame time.
    if (Usable(d.frameTimeDelta) && d.frameTimeDelta > 0.0f && d.frameTimeDelta < 10000.0f) {
        a.ffxFrameTimeCount.fetch_add(1u, std::memory_order_relaxed);
        a.ffxFrameTimeSumUs.fetch_add(static_cast<uint64_t>(d.frameTimeDelta * 1000.0f), std::memory_order_relaxed);
    }

    NoteBits(a.validMask, FL_ADV_VALID_FFX_UPSCALE);
}

// The upscaler's own memory, from the struct an answered ffxQuery wrote.
inline void ObserveFfxMemory(const FfxApiEffectMemoryUsage& m, Accumulators& a) noexcept {
    a.ffxMemoryTotalBytes.store(m.totalUsageInBytes, std::memory_order_relaxed);
    a.ffxMemoryAliasableBytes.store(m.aliasableUsageInBytes, std::memory_order_relaxed);
    a.ffxMemoryQueries.fetch_add(1u, std::memory_order_relaxed);
    NoteBits(a.validMask, FL_ADV_VALID_FFX_MEMORY);
}

// An ffxQuery the vendor answered OK: when it is the upscaler's memory query, either
// version, what it wrote through the game's own out-pointer is kept. Every other
// query type is passed over with only its header's type read. One module's
// queries are counted, as one module's dispatches are.
inline void ObserveFfxMemoryQuery(uint32_t leaf, const ffxQueryDescHeader* desc, Accumulators& a) noexcept {
    if (desc == nullptr) {
        return;
    }
    const FfxApiEffectMemoryUsage* usage = nullptr;
    if (desc->type == FFX_API_QUERY_DESC_TYPE_UPSCALE_GPU_MEMORY_USAGE) {
        const auto& q = *reinterpret_cast<const ffxQueryDescUpscaleGetGPUMemoryUsage*>(desc);
        usage = q.gpuMemoryUsageUpscaler;
    } else if (desc->type == FFX_API_QUERY_DESC_TYPE_UPSCALE_GPU_MEMORY_USAGE_V2) {
        const auto& q = *reinterpret_cast<const ffxQueryDescUpscaleGetGPUMemoryUsageV2*>(desc);
        usage = q.gpuMemoryUsageUpscaler;
    }
    if (usage != nullptr && Owns(a.ffxMemoryOwner, leaf)) {
        ObserveFfxMemory(*usage, a);
    }
}

// A NEW REQUEST: the summaries start afresh, so a session's ranges, masks and
// counts are its own and not those of the session before it in the same game.
// THE WATCHDOG'S. A detour running beside it loses nothing that matters: what it
// stores is of the call it is in, which belongs to the request that is starting.
// The two owners stay - which module's calls are counted is a fact about the
// process, not about a session.
inline void Reset(Accumulators& a) noexcept {
    const auto relaxed = std::memory_order_relaxed;
    a.validMask.store(0u, relaxed);
    a.dlssStructVersion.store(0u, relaxed);
    a.dlssOptionsSeen.store(0u, relaxed);
    a.dlssOutputWidth.store(0u, relaxed);
    a.dlssOutputHeight.store(0u, relaxed);
    a.dlssPreExposureBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.dlssExposureScaleBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.dlssPresets.store(0u, relaxed);
    a.dlssBooleans.store(0u, relaxed);
    a.ffxDispatches.store(0u, relaxed);
    a.ffxSharpenSeen.store(0u, relaxed);
    a.ffxSharpnessMinBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxSharpnessMaxBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxJitterMaxXBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxJitterMaxYBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxPreExposureMinBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxPreExposureMaxBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxNearBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxFarBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxFovMinBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxFovMaxBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.ffxFlagsSeen.store(0u, relaxed);
    a.ffxResets.store(0u, relaxed);
    a.ffxFrameTimeCount.store(0u, relaxed);
    a.ffxFrameTimeSumUs.store(0u, relaxed);
    a.ffxMemoryTotalBytes.store(0u, relaxed);
    a.ffxMemoryAliasableBytes.store(0u, relaxed);
    a.ffxMemoryQueries.store(0u, relaxed);
}

// Copies the accumulators into region 5, under the request they answer. THE
// WATCHDOG'S, and only the watchdog's: one writer, so the seqlock is odd -> stores
// -> even with no compare-exchange. The frame-time pair is drained, so each
// publication carries the dispatches since the last one.
inline void Publish(Accumulators& a, FlAdvancedState* region, uint32_t request, uint64_t qpc) noexcept {
    if (region == nullptr) {
        return;
    }

    const auto put32 = [](uint32_t& field, uint32_t value) noexcept {
        std::atomic_ref<uint32_t>{field}.store(value, std::memory_order_relaxed);
    };
    const auto put64 = [](uint64_t& field, uint64_t value) noexcept {
        std::atomic_ref<uint64_t>{field}.store(value, std::memory_order_relaxed);
    };
    const auto put8 = [](uint8_t& field, uint8_t value) noexcept {
        std::atomic_ref<uint8_t>{field}.store(value, std::memory_order_relaxed);
    };
    const auto relaxed = std::memory_order_relaxed;

    std::atomic_ref<uint32_t> seq{region->seq};
    const uint32_t            odd = seq.load(relaxed) | 1u;
    seq.store(odd, std::memory_order_release);

    put32(region->validMask, a.validMask.load(relaxed));
    put32(region->request, request);
    put64(region->sampleQpc, qpc);

    put32(region->dlssStructVersion, a.dlssStructVersion.load(relaxed));
    put32(region->dlssOptionsSeen, a.dlssOptionsSeen.load(relaxed));
    put32(region->dlssOutputWidth, a.dlssOutputWidth.load(relaxed));
    put32(region->dlssOutputHeight, a.dlssOutputHeight.load(relaxed));
    put32(region->dlssPreExposureBits, a.dlssPreExposureBits.load(relaxed));
    put32(region->dlssExposureScaleBits, a.dlssExposureScaleBits.load(relaxed));
    const uint64_t presets = a.dlssPresets.load(relaxed);
    for (uint32_t i = 0; i < 6u; ++i) {
        put8(region->dlssPresets[i], static_cast<uint8_t>(presets >> (8u * i)));
    }
    const uint32_t booleans = a.dlssBooleans.load(relaxed);
    put8(region->dlssColorBuffersHdr, static_cast<uint8_t>(booleans));
    put8(region->dlssAutoExposure, static_cast<uint8_t>(booleans >> 8));
    put8(region->dlssAlphaUpscaling, static_cast<uint8_t>(booleans >> 16));

    put32(region->ffxDispatches, a.ffxDispatches.load(relaxed));
    put32(region->ffxSharpenSeen, a.ffxSharpenSeen.load(relaxed));
    put32(region->ffxSharpnessMinBits, a.ffxSharpnessMinBits.load(relaxed));
    put32(region->ffxSharpnessMaxBits, a.ffxSharpnessMaxBits.load(relaxed));
    put32(region->ffxJitterMaxXBits, a.ffxJitterMaxXBits.load(relaxed));
    put32(region->ffxJitterMaxYBits, a.ffxJitterMaxYBits.load(relaxed));
    put32(region->ffxPreExposureMinBits, a.ffxPreExposureMinBits.load(relaxed));
    put32(region->ffxPreExposureMaxBits, a.ffxPreExposureMaxBits.load(relaxed));
    put32(region->ffxNearBits, a.ffxNearBits.load(relaxed));
    put32(region->ffxFarBits, a.ffxFarBits.load(relaxed));
    put32(region->ffxFovMinBits, a.ffxFovMinBits.load(relaxed));
    put32(region->ffxFovMaxBits, a.ffxFovMaxBits.load(relaxed));
    put32(region->ffxFlagsSeen, a.ffxFlagsSeen.load(relaxed));
    put32(region->ffxResets, a.ffxResets.load(relaxed));
    const uint64_t sumUs = a.ffxFrameTimeSumUs.exchange(0u, relaxed);
    put32(region->ffxFrameTimeCount, a.ffxFrameTimeCount.exchange(0u, relaxed));
    put32(region->ffxFrameTimeSumUs, sumUs > 0xFFFFFFFFull ? 0xFFFFFFFFu : static_cast<uint32_t>(sumUs));

    put64(region->ffxMemoryTotalBytes, a.ffxMemoryTotalBytes.load(relaxed));
    put64(region->ffxMemoryAliasableBytes, a.ffxMemoryAliasableBytes.load(relaxed));
    put32(region->ffxMemoryQueries, a.ffxMemoryQueries.load(relaxed));

    std::atomic_ref<uint32_t> samples{region->samples};
    samples.store(samples.load(relaxed) + 1u, relaxed);
    seq.store(odd + 1u, std::memory_order_release);
}

}    // namespace fl::adv

#endif    // FRAMELEDGER_FL_ADV_FIELDS_H
