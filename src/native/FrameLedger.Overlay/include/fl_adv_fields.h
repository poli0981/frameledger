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
// (and since train C three more, for what the driver wrote into the Overlay's own buffers: `lr`, `lf` and `ls` --
// the section "REFLEX LATENCY" below says what they are and why they are here.)
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
#include <fl_reflex.h>
#include <fl_shm.h>
#include <sl.h>
// After sl.h, as fl_sl_inputs.h explains: sl_dlss.h has no includes of its own.
#include <ffx_api.h>
#include <ffx_framegeneration.h>
#include <ffx_upscale.h>
#include <sl_dlss.h>

namespace fl::adv {

// A float a detour may keep: finite, and not a vendor's "nobody set this". The
// test is on the magnitude's bits: everything below FLT_MAX passes, and FLT_MAX
// itself (Streamline's INVALID_FLOAT), both infinities and every NaN do not.
inline bool Usable(float v) noexcept {
    return (std::bit_cast<uint32_t>(v) & 0x7FFFFFFFu) < 0x7F7FFFFFu;
}

// A depth plane as FSR is handed one (owner decision D119): an ordinary value as its bits; FLT_MAX or +infinity --
// how an engine with an infinite depth says "this plane is at infinity" -- as FL_ADV_FLOAT_INFINITE; anything else
// (a NaN, a negative infinity, -FLT_MAX) as "no value". FOR THE ffx ROUTE ONLY: Streamline's INVALID_FLOAT is
// FLT_MAX and means nobody set it, which is why Usable refuses it everywhere else.
inline uint32_t FfxDepthPlaneBits(float v) noexcept {
    const uint32_t bits = std::bit_cast<uint32_t>(v);
    if (Usable(v)) {
        return bits;
    }
    return bits == 0x7F7FFFFFu || bits == FL_ADV_FLOAT_INFINITE ? FL_ADV_FLOAT_INFINITE : FL_ADV_FLOAT_NONE;
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

    // ffxConfigure with a frame-generation descriptor (0.1.0-beta.43)
    std::atomic<uint32_t> ffxFgOwner{0u};    // leaf + 1 of the module whose configures are counted
    std::atomic<uint32_t> ffxFgConfigures{0u};
    std::atomic<uint32_t> ffxFgEnabledSeen{0u};          // bit 0: seen off; bit 1: seen on
    std::atomic<uint32_t> ffxFgAsyncSeen{0u};            // the same two bits
    std::atomic<uint32_t> ffxFgOnlyGeneratedSeen{0u};    // the same two bits
    std::atomic<uint32_t> ffxFgFlagsSeen{0u};
    std::atomic<uint64_t> ffxFgRect{0u};    // PackRect: the last rectangle, in ONE word so it is never half of two

    // sl::Constants (0.1.0-beta.43), summarised - from the first viewport seen; the calls count every viewport
    std::atomic<uint64_t> slViewport{0u};    // that viewport's id + 1; 0 = none yet
    std::atomic<uint32_t> slConstantsCalls{0u};
    std::atomic<uint32_t> slResets{0u};
    std::atomic<uint32_t> slJitterMaxXBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slJitterMaxYBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slMvecScaleXBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slMvecScaleYBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slNearBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slFarBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slFovMinBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slFovMaxBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slAspectBits{FL_ADV_FLOAT_NONE};
    std::atomic<uint32_t> slFlagsSeenTrue{0u};
    std::atomic<uint32_t> slFlagsSeenFalse{0u};

    // What Streamline answered about each feature the game asked about (0.1.0-beta.44): FL_ADV_FEATURE_SLOTS slots
    // of FL_ADV_FEATURE_WORDS words, laid out as the region's `features` (fl_shm.h). Kept across a new request.
    std::atomic<uint32_t> features[FL_ADV_FEATURE_SLOTS * FL_ADV_FEATURE_WORDS]{};

    // slShutdown calls since the request (0.1.0-beta.45)
    std::atomic<uint32_t> slShutdowns{0u};
};

// Word 1 of a feature slot: which of the two questions has an answer in it.
inline constexpr uint32_t kFeatureVersionAnswered = 1u << 0;
inline constexpr uint32_t kFeatureRequirementsAnswered = 1u << 1;
// 0.1.0-beta.45: whether it is supported (and the sl::Result that said so, in bits 8..15), whether it is loaded (and
// what the answer was), and whether the game was seen to switch it off or on.
inline constexpr uint32_t kFeatureSupportAnswered = 1u << 2;
inline constexpr uint32_t kFeatureLoadedAnswered = 1u << 3;
inline constexpr uint32_t kFeatureLoadedValue = 1u << 4;
inline constexpr uint32_t kFeatureSwitchedOff = 1u << 5;
inline constexpr uint32_t kFeatureSwitchedOn = 1u << 6;
inline constexpr uint32_t kFeatureSupportResultShift = 8u;
inline constexpr uint32_t kFeatureSupportResultMask = 0xFFu << kFeatureSupportResultShift;
// What of word 1 is a SESSION's -- what the game did while it was watched -- and is cleared by a new request. The
// rest is a fact about the process (which version, what it requires, whether it is supported and loaded) and stays.
// How many times the game allocated and freed the feature's resources: four bits each, stopping at 15.
inline constexpr uint32_t kFeatureAllocShift = 16u;
inline constexpr uint32_t kFeatureFreeShift = 20u;
inline constexpr uint32_t kFeatureCountMask = 0xFu;
inline constexpr uint32_t kFeatureSessionBits = kFeatureSwitchedOff | kFeatureSwitchedOn |
                                                (kFeatureCountMask << kFeatureAllocShift) |
                                                (kFeatureCountMask << kFeatureFreeShift);

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

    // The two depth planes, the last of each; one of them may be at infinity (D119).
    if (const uint32_t nearBits = FfxDepthPlaneBits(d.cameraNear); nearBits != FL_ADV_FLOAT_NONE) {
        a.ffxNearBits.store(nearBits, std::memory_order_relaxed);
    }
    if (const uint32_t farBits = FfxDepthPlaneBits(d.cameraFar); farBits != FL_ADV_FLOAT_NONE) {
        a.ffxFarBits.store(farBits, std::memory_order_relaxed);
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

// A rectangle in one 64-bit word, sixteen bits a side: left, top, width, height from the lowest. The detour stores
// it with one store and the watchdog reads it with one load, so a publication never carries the left edge of one
// rectangle beside the width of the next. A side that is negative or does not fit in fifteen bits makes the whole
// word kRectUnfit, which Publish writes as four -1: a rectangle nobody can state rather than a clipped one.
inline constexpr uint64_t kRectUnfit = 0xFFFFFFFFFFFFFFFFull;

inline uint64_t PackRect(int32_t left, int32_t top, int32_t width, int32_t height) noexcept {
    const auto fits = [](int32_t v) noexcept { return v >= 0 && v <= 0x7FFF; };
    if (!fits(left) || !fits(top) || !fits(width) || !fits(height)) {
        return kRectUnfit;
    }
    return static_cast<uint64_t>(left) | (static_cast<uint64_t>(top) << 16) | (static_cast<uint64_t>(width) << 32) |
           (static_cast<uint64_t>(height) << 48);
}

// How the game configures FSR frame generation (ffxConfigure, 0.1.0-beta.43). Read only when the header's type is
// the frame-generation configuration -- a key-value pair, a distortion field or the camera info arriving through the
// same call is passed over with nothing but its type looked at.
//
// WHAT IS READ: whether generation is on, whether it may run asynchronously, the flags, whether only generated
// frames are presented, and the rectangle generated in. WHAT IS NOT, EVER: the swapchain, the two callbacks and
// their contexts, the HUD-less resource and the frame id that sit in the same descriptor.
//
// ONE MODULE'S CONFIGURES ARE COUNTED (Owns), with a slot of its own: behind a loader the same call reaches the
// loader and the leaf, and a title's upscale may be owned by another module than its frame generation.
inline void ObserveFfxConfigure(uint32_t leaf, const ffxConfigureDescHeader* desc, Accumulators& a) noexcept {
    if (desc == nullptr || desc->type != FFX_API_CONFIGURE_DESC_TYPE_FRAMEGENERATION) {
        return;
    }
    if (!Owns(a.ffxFgOwner, leaf)) {
        return;
    }
    const auto& g = *reinterpret_cast<const ffxConfigureDescFrameGeneration*>(desc);

    a.ffxFgConfigures.fetch_add(1u, std::memory_order_relaxed);
    NoteBits(a.ffxFgEnabledSeen, ByteIsSet(g.frameGenerationEnabled) ? 2u : 1u);
    NoteBits(a.ffxFgAsyncSeen, ByteIsSet(g.allowAsyncWorkloads) ? 2u : 1u);
    NoteBits(a.ffxFgOnlyGeneratedSeen, ByteIsSet(g.onlyPresentGenerated) ? 2u : 1u);
    if (g.flags != 0u) {
        NoteBits(a.ffxFgFlagsSeen, g.flags);
    }
    a.ffxFgRect.store(
        PackRect(g.generationRect.left, g.generationRect.top, g.generationRect.width, g.generationRect.height),
        std::memory_order_relaxed);

    NoteBits(a.validMask, FL_ADV_VALID_FFX_FG_CONFIG);
}

// One sl::Boolean of sl::Constants into the two "seen" words: true sets its bit in `seenTrue`, false in `seenFalse`,
// the third state ("invalid": the game set nothing) in neither.
inline void NoteSlBoolean(sl::Boolean value, uint32_t bit, uint32_t& seenTrue, uint32_t& seenFalse) noexcept {
    if (value == sl::Boolean::eTrue) {
        seenTrue |= bit;
    } else if (value == sl::Boolean::eFalse) {
        seenFalse |= bit;
    }
}

// What the game told Streamline about a frame (slSetConstants, 0.1.0-beta.43). The caller has matched the struct's
// GUID and found its version at least 1, on THIS call (fl_sl_inputs.h IsReadable): every member read below is one
// version 1 carries (17_HOOK_ENGINE "Which struct version carries which field").
//
// WHAT IS READ: the jitter, the motion-vector scale, the two depth planes, the field of view, the aspect ratio and
// the seven Booleans. WHAT IS NOT, EVER: the five matrices that precede them and the pinhole offset, the camera's
// position and its three axes that sit between the scale and the planes (rule 4). Nothing here names them, and
// tools/overlay-fields-check.ps1 fails the build if anything in the code loaded into a game does.
//
// THE SUMMARIES ARE THE FIRST VIEWPORT'S. A title with two viewports -- a scene and a picture-in-picture, a
// split screen -- sets constants for each every frame; "the last value seen" across them would flip twice a frame
// and a range would span two cameras. The first viewport that calls is the one summarised, for the life of the
// process. The call count is of every viewport: it says how often the game called.
//
// FLT_MAX IS "NOT SET" HERE (Streamline's INVALID_FLOAT) and is kept nowhere: the plane at infinity of D119 is
// FidelityFX's convention and stays on that route.
inline void ObserveSlConstants(const sl::Constants& k, uint32_t viewport, Accumulators& a) noexcept {
    a.slConstantsCalls.fetch_add(1u, std::memory_order_relaxed);

    const uint64_t me = static_cast<uint64_t>(viewport) + 1u;
    uint64_t       owner = a.slViewport.load(std::memory_order_relaxed);
    if (owner == 0u && a.slViewport.compare_exchange_strong(owner, me, std::memory_order_relaxed)) {
        owner = me;
    }
    if (owner != me) {
        return;
    }

    if (Usable(k.jitterOffset.x) && Usable(k.jitterOffset.y)) {
        KeepMax(a.slJitterMaxXBits, Magnitude(k.jitterOffset.x));
        KeepMax(a.slJitterMaxYBits, Magnitude(k.jitterOffset.y));
    }
    if (Usable(k.mvecScale.x) && Usable(k.mvecScale.y)) {
        a.slMvecScaleXBits.store(std::bit_cast<uint32_t>(k.mvecScale.x), std::memory_order_relaxed);
        a.slMvecScaleYBits.store(std::bit_cast<uint32_t>(k.mvecScale.y), std::memory_order_relaxed);
    }
    if (Usable(k.cameraNear)) {
        a.slNearBits.store(std::bit_cast<uint32_t>(k.cameraNear), std::memory_order_relaxed);
    }
    if (Usable(k.cameraFar)) {
        a.slFarBits.store(std::bit_cast<uint32_t>(k.cameraFar), std::memory_order_relaxed);
    }
    // A range, lowest to highest, never a series (D114).
    if (Usable(k.cameraFOV) && k.cameraFOV > 0.0f) {
        KeepMin(a.slFovMinBits, k.cameraFOV);
        KeepMax(a.slFovMaxBits, k.cameraFOV);
    }
    if (Usable(k.cameraAspectRatio) && k.cameraAspectRatio > 0.0f) {
        a.slAspectBits.store(std::bit_cast<uint32_t>(k.cameraAspectRatio), std::memory_order_relaxed);
    }

    // One bit per Boolean, in the header's order (FlAdvancedState::slFlagsSeenTrue).
    uint32_t seenTrue = 0u;
    uint32_t seenFalse = 0u;
    NoteSlBoolean(k.depthInverted, 1u << 0, seenTrue, seenFalse);
    NoteSlBoolean(k.cameraMotionIncluded, 1u << 1, seenTrue, seenFalse);
    NoteSlBoolean(k.motionVectors3D, 1u << 2, seenTrue, seenFalse);
    NoteSlBoolean(k.reset, 1u << 3, seenTrue, seenFalse);
    NoteSlBoolean(k.orthographicProjection, 1u << 4, seenTrue, seenFalse);
    NoteSlBoolean(k.motionVectorsDilated, 1u << 5, seenTrue, seenFalse);
    NoteSlBoolean(k.motionVectorsJittered, 1u << 6, seenTrue, seenFalse);
    NoteBits(a.slFlagsSeenTrue, seenTrue);
    NoteBits(a.slFlagsSeenFalse, seenFalse);
    if ((seenTrue & (1u << 3)) != 0u) {
        a.slResets.fetch_add(1u, std::memory_order_relaxed);
    }

    NoteBits(a.validMask, FL_ADV_VALID_SL_CONSTANTS);
}

// The slot of `feature`: the one that holds it already, else the first empty one, claimed here. nullptr when every
// slot holds another feature -- a thirteenth is not kept -- and for the one id that cannot be stored plus one.
// Slots fill from 0 and are never emptied, so the first empty slot ends the search.
inline std::atomic<uint32_t>* FeatureSlot(uint32_t feature, Accumulators& a) noexcept {
    const uint32_t id = feature + 1u;
    if (id == 0u) {
        return nullptr;
    }
    for (uint32_t i = 0; i < FL_ADV_FEATURE_SLOTS; ++i) {
        std::atomic<uint32_t>* slot = &a.features[i * FL_ADV_FEATURE_WORDS];
        uint32_t               held = slot[0].load(std::memory_order_relaxed);
        if (held == 0u && slot[0].compare_exchange_strong(held, id, std::memory_order_relaxed)) {
            return slot;
        }
        if (held == id) {    // it was there, or another thread asking about the same feature claimed it just now
            return slot;
        }
    }
    return nullptr;
}

// Three words of a slot from one sl::Version: major, minor, build.
inline void PutVersion(std::atomic<uint32_t>* words, const sl::Version& number) noexcept {
    words[0].store(number.major, std::memory_order_relaxed);
    words[1].store(number.minor, std::memory_order_relaxed);
    words[2].store(number.build, std::memory_order_relaxed);
}

// What slGetFeatureVersion ANSWERED (0.1.0-beta.44): the caller is the detour, after the original returned OK and
// the struct's GUID and version were checked. Both members are version 1's (17_HOOK_ENGINE "Which struct version
// carries which field"). A feature asked about again overwrites its own slot: the last answer is the one kept.
inline void ObserveSlFeatureVersion(uint32_t feature, const sl::FeatureVersion& fv, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    PutVersion(slot + 2, fv.versionSL);
    PutVersion(slot + 5, fv.versionNGX);
    NoteBits(slot[1], kFeatureVersionAnswered);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// What slGetFeatureRequirements ANSWERED (0.1.0-beta.44), under the same conditions. WHAT IS READ: the flags, three
// counts, the number of required buffer tags, and four versions -- every one a member of struct version 1, the last
// of them `driverVersionRequired`. WHAT IS NOT, EVER: `requiredTags`, the pointer the count belongs to, and every
// member after the versions -- the Vulkan queue counts and the pointers to the names of extensions and features.
// Nothing here follows a pointer of the game's.
inline void ObserveSlFeatureRequirements(uint32_t feature, const sl::FeatureRequirements& fr,
                                         Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    slot[8].store(static_cast<uint32_t>(fr.flags), std::memory_order_relaxed);
    slot[9].store(fr.maxNumCPUThreads, std::memory_order_relaxed);
    slot[10].store(fr.maxNumViewports, std::memory_order_relaxed);
    slot[11].store(fr.numRequiredTags, std::memory_order_relaxed);
    PutVersion(slot + 12, fr.osVersionDetected);
    PutVersion(slot + 15, fr.osVersionRequired);
    PutVersion(slot + 18, fr.driverVersionDetected);
    PutVersion(slot + 21, fr.driverVersionRequired);
    NoteBits(slot[1], kFeatureRequirementsAnswered);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// Some bits of a word replaced by others, the rest left as they are: two detours may be writing different fields of
// the same slot's word 1.
inline void PutBits(std::atomic<uint32_t>& word, uint32_t mask, uint32_t bits) noexcept {
    uint32_t seen = word.load(std::memory_order_relaxed);
    while ((seen & mask) != bits) {
        if (word.compare_exchange_weak(seen, (seen & ~mask) | bits, std::memory_order_relaxed)) {
            break;
        }
    }
}

// What slIsFeatureSupported ANSWERED about a feature (0.1.0-beta.45): the sl::Result itself, whatever it is -- a
// refusal is the answer to this question, where for a version or a requirement it is the absence of one. The last
// answer is the one kept. The adapter the game asked about is not an argument of this function: the detour never
// hands it over, and nothing here could read it (owner decision D131).
inline void ObserveSlFeatureSupported(uint32_t feature, uint32_t result, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    const uint32_t code = result > 0xFEu ? 0xFFu : result;
    PutBits(slot[1], kFeatureSupportResultMask | kFeatureSupportAnswered,
            (code << kFeatureSupportResultShift) | kFeatureSupportAnswered);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// What slIsFeatureLoaded ANSWERED (0.1.0-beta.45): the caller is the detour, after the original said OK and wrote
// the bool. The last answer is the one kept.
inline void ObserveSlFeatureLoaded(uint32_t feature, bool loaded, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    PutBits(slot[1], kFeatureLoadedAnswered | kFeatureLoadedValue,
            kFeatureLoadedAnswered | (loaded ? kFeatureLoadedValue : 0u));
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// What the game told slSetFeatureLoaded (0.1.0-beta.45): off, on, or over a session both. What the game ASKED --
// whether Streamline did it is slIsFeatureLoaded's to say.
inline void ObserveSlSetFeatureLoaded(uint32_t feature, bool loaded, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    NoteBits(slot[1], loaded ? kFeatureSwitchedOn : kFeatureSwitchedOff);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// One more in a four-bit count of a slot's word 1, which stops at 15 and never carries into its neighbour.
inline void BumpCount(std::atomic<uint32_t>& word, uint32_t shift) noexcept {
    uint32_t seen = word.load(std::memory_order_relaxed);
    while (((seen >> shift) & kFeatureCountMask) != kFeatureCountMask) {
        if (word.compare_exchange_weak(seen, seen + (1u << shift), std::memory_order_relaxed)) {
            break;
        }
    }
}

// The game allocating, and freeing, a feature's resources (0.1.0-beta.45): counted for the feature and nothing else
// -- the command buffer and the viewport of the same calls are not arguments of these functions.
inline void ObserveSlAllocateResources(uint32_t feature, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    BumpCount(slot[1], kFeatureAllocShift);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

inline void ObserveSlFreeResources(uint32_t feature, Accumulators& a) noexcept {
    std::atomic<uint32_t>* slot = FeatureSlot(feature, a);
    if (slot == nullptr) {
        return;
    }
    BumpCount(slot[1], kFeatureFreeShift);
    NoteBits(a.validMask, FL_ADV_VALID_SL_FEATURES);
}

// The game shutting Streamline down (0.1.0-beta.45): a count.
inline void ObserveSlShutdown(Accumulators& a) noexcept {
    a.slShutdowns.fetch_add(1u, std::memory_order_relaxed);
    NoteBits(a.validMask, FL_ADV_VALID_SL_SHUTDOWN);
}

// ---------------------------------------------------------------------------------------------------------------------
// REFLEX LATENCY (train C; owner decisions D132, D136, D138-D141; fl_reflex.h; fl_shm.h's latency words).
//
// THE ONE GROUP HERE THAT IS NOT AN ARGUMENT OF A HOOKED CALL. The two structs below are what the driver wrote into
// buffers the OVERLAY handed to its two read-only getters (fl_reflex.h makes the calls). The members read are on
// the same list as every other, through three more parameters with fixed names:
//
//     lr  const NV_LATENCY_RESULT_PARAMS&               the report: sixty-four frames
//     lf  const NV_LATENCY_RESULT_PARAMS::FrameReport&  one frame of it, handed on by index and never aliased
//     ls  const NV_GET_SLEEP_STATUS_PARAMS&             the driver's sleep status
//
// WHAT IS NOT READ: inputSampleTime (the probe never saw it set, and "when the player pressed something" is nothing
// a frame-time tool needs), the camera, cross-adapter and frame-generation times, and the sleep interval -- it
// changes with every frame, so it is not a fact of a session.
//
// TWO TIMES THE DRIVER WORKS OUT ITSELF (0.1.0-beta.48, owner decision D142): gpuActiveRenderTimeUs -- the header:
// the GPU's start to its end "excluding the idles in between" -- and gpuFrameTimeUs, one frame's GPU end to the
// next's. In a real game the span between the two GPU marks was longer than the frame's whole (HANDOFF, item 45):
// it includes the waiting. They are members of the report already in hand; no call is added.
//
// THE RULES A GAME NEEDS (spike-notes §31). The newest frames of a report are not final -- one run read a span of
// half an hour in the newest -- so the two newest ids are never summed. A span whose end point is zero was not
// reported, which is not a fault of the frame: without a sleep mode the driver leaves driverStartTime zero in every
// frame. A span that runs backwards or lasts more than a second is one no frame can have, and that frame is
// refused whole; so is a frame with no total. Frame ids that go backwards are a game that started counting again.
//
// The caller owns the tally and the cursor (dllmain.cpp's gate says who that is at any moment): nothing here is
// atomic, and nothing here calls out.
// ---------------------------------------------------------------------------------------------------------------------
inline constexpr uint32_t kLatSpans =
    7u;    // simulation, submit, present, driver, OS queue, GPU, total: the region's order
inline constexpr uint32_t kLatGpu = 5u;
inline constexpr uint32_t kLatTotal = 6u;
inline constexpr uint32_t kLatReportFrames = 64u;
inline constexpr uint64_t kLatSpanLimitUs = 1000000u;
static_assert(sizeof(NV_LATENCY_RESULT_PARAMS::frameReport) / sizeof(NV_LATENCY_RESULT_PARAMS::FrameReport) ==
              kLatReportFrames);

// HOW A FRAME'S GPU SPAN AND THE DRIVER'S ACTIVE TIME STAND TO EACH OTHER (0.1.0-beta.49, owner decision D148). In one
// real game the mean active time was LONGER than the mean span between the two GPU marks (HANDOFF, item 46), and the
// two means cannot say why: the span's is over every summed frame, one of no length included, the active time's over
// the frames that gave one. So each summed frame is put in one place here, by its own two numbers, and the frames
// that have both are kept in two heaps -- the active time longer than the span, or not -- each with its count and
// both sums. No threshold: a span of one microsecond is a span. FOR THE NATIVE LOG ONLY: nothing of it is published,
// and which frames are summed does not depend on it.
struct LatencyGpuCounts {
    uint32_t spanAbsent = 0;        // no GPU start mark
    uint32_t spanZero = 0;          // the two GPU marks are the same instant
    uint32_t activeNoSpan = 0;      // an active time, and no span with a length
    uint32_t spanNoActive = 0;      // a span with a length, and no active time
    uint32_t longerFrames = 0;      // both, and the active time is the LONGER
    uint32_t longerSpanUs = 0;      // ... their spans
    uint32_t longerActiveUs = 0;    // ... their active times
    uint32_t withinFrames = 0;      // both, and the active time is no longer than the span
    uint32_t withinSpanUs = 0;
    uint32_t withinActiveUs = 0;
};

// What one read of the report added up to.
struct LatencyTally {
    uint32_t frames = 0;            // frames summed
    uint32_t refused = 0;           // frames refused
    uint32_t sumUs[kLatSpans]{};    // microseconds, over the frames summed
    uint32_t lacking = 0;           // bit n: a summed frame did not report span n
    // The driver's two own times, each over the summed frames that gave one: a count apiece.
    uint32_t         activeSumUs = 0;
    uint32_t         activeFrames = 0;
    uint32_t         frameTimeSumUs = 0;
    uint32_t         frameTimeFrames = 0;
    LatencyGpuCounts gpu;    // the summed frames, each by its GPU span and its active time: the native log's
};

// Where the reader is in the game's frame ids: the newest one already summed or refused. 0 = none yet.
struct LatencyCursor {
    uint64_t lastFrameId = 0;
};

inline constexpr uint32_t kLatSpanReported = 0u;
inline constexpr uint32_t kLatSpanAbsent = 1u;
inline constexpr uint32_t kLatSpanImpossible = 2u;

inline uint32_t LatencySpan(uint64_t start, uint64_t end, uint32_t& us) noexcept {
    us = 0u;
    if (start == 0u || end == 0u) {
        return kLatSpanAbsent;
    }
    if (end < start || end - start > kLatSpanLimitUs) {
        return kLatSpanImpossible;
    }
    us = static_cast<uint32_t>(end - start);
    return kLatSpanReported;
}

// A slot of the report the driver put nothing in.
inline bool LatencyFrameIsEmpty(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf) noexcept {
    return lf.frameID == 0u && lf.simStartTime == 0u && lf.gpuRenderEndTime == 0u;
}

inline uint64_t LatencyFrameId(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf) noexcept {
    return lf.frameID;
}

// One of the driver's own times, of a frame that is summed. Zero is a frame the driver gave no such time for
// (an older driver leaves both zero, and so did the probe's frames, which had no work for the card); more than a
// second is a time no frame can have. Neither is summed, and neither refuses the frame: which frames the spans are
// made of does not depend on these.
inline bool LatencyTimeGiven(uint32_t us) noexcept {
    return us != 0u && us <= kLatSpanLimitUs;
}

inline void LatencyAddTime(uint32_t us, uint32_t& sumUs, uint32_t& frames) noexcept {
    if (LatencyTimeGiven(us)) {
        sumUs += us;
        ++frames;
    }
}

// One summed frame's place in LatencyGpuCounts: its GPU span as LatencySpan gave it, and the driver's active time
// of the same frame, "given" by the rule LatencyAddTime sums it by. An impossible span never comes here -- its frame
// was refused.
inline void CountGpuTimes(uint32_t spanKind, uint32_t spanUs, uint32_t activeUs, LatencyGpuCounts& counts) noexcept {
    const bool span = spanKind == kLatSpanReported && spanUs != 0u;
    const bool active = LatencyTimeGiven(activeUs);
    if (spanKind == kLatSpanAbsent) {
        ++counts.spanAbsent;
    } else if (spanUs == 0u) {
        ++counts.spanZero;
    }
    if (span && active) {
        if (activeUs > spanUs) {
            ++counts.longerFrames;
            counts.longerSpanUs += spanUs;
            counts.longerActiveUs += activeUs;
        } else {
            ++counts.withinFrames;
            counts.withinSpanUs += spanUs;
            counts.withinActiveUs += activeUs;
        }
    } else if (active) {
        ++counts.activeNoSpan;
    } else if (span) {
        ++counts.spanNoActive;
    }
}

inline void ObserveLatencyFrame(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf, LatencyTally& tally) noexcept {
    uint32_t       us[kLatSpans]{};
    const uint32_t kind[kLatSpans] = {
        LatencySpan(lf.simStartTime, lf.simEndTime, us[0]),
        LatencySpan(lf.renderSubmitStartTime, lf.renderSubmitEndTime, us[1]),
        LatencySpan(lf.presentStartTime, lf.presentEndTime, us[2]),
        LatencySpan(lf.driverStartTime, lf.driverEndTime, us[3]),
        LatencySpan(lf.osRenderQueueStartTime, lf.osRenderQueueEndTime, us[4]),
        LatencySpan(lf.gpuRenderStartTime, lf.gpuRenderEndTime, us[5]),
        LatencySpan(lf.simStartTime, lf.gpuRenderEndTime, us[kLatTotal]),
    };
    bool refuse = kind[kLatTotal] != kLatSpanReported;    // no total, or one no frame can have
    for (uint32_t i = 0; i < kLatTotal; ++i) {
        refuse = refuse || kind[i] == kLatSpanImpossible;
    }
    if (refuse) {
        ++tally.refused;
        return;
    }
    ++tally.frames;
    for (uint32_t i = 0; i < kLatSpans; ++i) {
        if (kind[i] == kLatSpanAbsent) {
            tally.lacking |= 1u << i;
        } else {
            tally.sumUs[i] += us[i];
        }
    }
    LatencyAddTime(lf.gpuActiveRenderTimeUs, tally.activeSumUs, tally.activeFrames);
    LatencyAddTime(lf.gpuFrameTimeUs, tally.frameTimeSumUs, tally.frameTimeFrames);
    CountGpuTimes(kind[kLatGpu], us[kLatGpu], lf.gpuActiveRenderTimeUs, tally.gpu);
}

// One report: the frames newer than the cursor, the two newest left for the next read.
inline void ObserveLatencyReport(const NV_LATENCY_RESULT_PARAMS& lr, LatencyCursor& cursor,
                                 LatencyTally& tally) noexcept {
    tally = LatencyTally{};
    uint64_t newest = 0u;
    uint64_t second = 0u;
    for (uint32_t i = 0; i < kLatReportFrames; ++i) {
        if (LatencyFrameIsEmpty(lr.frameReport[i])) {
            continue;
        }
        const uint64_t id = LatencyFrameId(lr.frameReport[i]);
        if (id > newest) {
            second = newest;
            newest = id;
        } else if (id > second && id != newest) {
            second = id;
        }
    }
    if (newest == 0u) {
        return;    // a report of nothing
    }
    if (newest < cursor.lastFrameId) {
        cursor.lastFrameId = 0u;    // the game's frame ids started again
    }
    uint64_t last = cursor.lastFrameId;
    for (uint32_t i = 0; i < kLatReportFrames; ++i) {
        if (LatencyFrameIsEmpty(lr.frameReport[i])) {
            continue;
        }
        const uint64_t id = LatencyFrameId(lr.frameReport[i]);
        if (id <= cursor.lastFrameId || id >= second) {
            continue;    // summed already, or one of the two newest
        }
        ObserveLatencyFrame(lr.frameReport[i], tally);
        last = id > last ? id : last;
    }
    cursor.lastFrameId = last;
}

// The driver's sleep status: which of its five switches are on (bit n, in the struct's order -- fl_shm.h) and its
// frame-generation multiplier. The driver's words, not the game's: see FlLatencyState.
struct LatencySleep {
    uint32_t on = 0;
    uint32_t multiplier = 0;    // its frame-generation multiplier
};

inline LatencySleep ObserveSleepStatus(const NV_GET_SLEEP_STATUS_PARAMS& ls) noexcept {
    LatencySleep seen{};
    seen.on = (ls.bLowLatencyMode != 0u ? 1u : 0u) | (ls.bFsVrr != 0u ? 2u : 0u) | (ls.bCplVsyncOn != 0u ? 4u : 0u) |
              (ls.bUseGameSleep != 0u ? 8u : 0u) | (ls.bFullscreenIFlip != 0u ? 16u : 0u);
    seen.multiplier = static_cast<uint32_t>(ls.fgMultiplier);
    return seen;
}

// The bit of fl_shm.h's latMultipliersSeen for one multiplier the status gave: bit n for n, bit 31 for 31 and more.
inline uint32_t LatencyMultiplierBit(uint32_t value) noexcept {
    return 1u << (value > 31u ? 31u : value);
}

// ---------------------------------------------------------------------------------------------------------------------
// THE COUNTS, IN BATCHES, FOR THE NATIVE LOG (0.1.0-beta.49, owner decisions D148, D151). The watchdog's.
//
// A BATCH BELONGS TO ONE STATE of the driver's sleep status -- its low-latency switch, its "the game sleeps" switch
// and its frame-generation multiplier -- because the counts mean nothing apart from it: the owner's games switch
// Reflex on only with frame generation, and the low-latency switch may be the NVIDIA App's override and not the
// game's. A read in another state closes the batch before it is added. A state that flickers would write a batch a
// read, so after kLatStateClosures such closings a request's batches run to their length whatever the state, and the
// "seen" words of the batch say what it held.
//
// A batch is also closed after the request's tenth read, its thirtieth, and every sixtieth from there: a game that
// exits takes the watchdog with it, and what was not written by then is lost -- under a minute of it.
//
// AND HOW EVENLY THE READS CAME (owner decision D150): the milliseconds the batch spans and the watchdog's ticks in
// them, periodic and woken early. A read a second is a thousand milliseconds a read and as many periodic ticks as
// reads; anything else says which side of it was uneven.
// ---------------------------------------------------------------------------------------------------------------------
inline constexpr uint32_t kLatBatchFirst = 10u;
inline constexpr uint32_t kLatBatchSecond = 30u;
inline constexpr uint32_t kLatBatchEvery = 60u;
inline constexpr uint32_t kLatStateClosures = 32u;
inline constexpr uint32_t kLatStateUnknown = 0xFFFFFFFFu;    // a read whose sleep status did not come back
inline constexpr uint32_t kLatStateSwitches = 1u | 8u;       // bLowLatencyMode, bUseGameSleep (ObserveSleepStatus)

struct LatencyBatch {
    uint32_t         reads = 0;      // reads whose report came back, in this batch
    uint32_t         frames = 0;     // frames they summed
    uint32_t         refused = 0;    // frames they refused
    LatencyGpuCounts gpu;
    uint32_t         sleepOn = 0;                 // switches the status showed on in this batch
    uint32_t         sleepOff = 0;                // ... and off
    uint32_t         multipliers = 0;             // LatencyMultiplierBit of every multiplier it gave
    uint32_t         state = kLatStateUnknown;    // LatencyStateOf its first read
    // The watchdog's own, set by it: not a read's.
    uint32_t spannedMs = 0;      // from the batch before -- or the request -- to this one's writing
    uint32_t ticksOnTime = 0;    // its periodic ticks in that time
    uint32_t ticksEarly = 0;     // ... and the ticks something woke early
};

struct LatencyBatchBook {
    LatencyBatch batch;
    uint32_t     okReads = 0;     // reads whose report came back, since the request
    uint32_t     closures = 0;    // batches a change of state closed, since the request
};

inline uint32_t LatencyStateOf(bool sleepValid, const LatencySleep& sleep) noexcept {
    if (!sleepValid) {
        return kLatStateUnknown;
    }
    return (sleep.on & kLatStateSwitches) | ((sleep.multiplier > 255u ? 255u : sleep.multiplier) << 8);
}

// Whether the batch is to be written BEFORE a read in `state` is added to it.
inline bool LatencyBatchTurns(const LatencyBatchBook& book, uint32_t state) noexcept {
    return book.batch.reads != 0u && book.batch.state != state && book.closures < kLatStateClosures;
}

// Whether the batch is to be written AFTER the request's okReads-th read was added.
inline bool LatencyBatchDue(uint32_t okReads) noexcept {
    if (okReads <= kLatBatchSecond) {
        return okReads == kLatBatchFirst || okReads == kLatBatchSecond;
    }
    return (okReads - kLatBatchSecond) % kLatBatchEvery == 0u;
}

// A batch is sixty reads of sixty-two frames at most, and a time is a second at most: under 2^32 microseconds. The
// sums stop there all the same, so that a word never wraps into a small number.
inline void LatencyAddStop(uint32_t& sum, uint32_t value) noexcept {
    sum = value > 0xFFFFFFFFu - sum ? 0xFFFFFFFFu : sum + value;
}

inline void LatencyBatchAdd(LatencyBatchBook& book, const LatencyTally& tally, bool sleepValid,
                            const LatencySleep& sleep) noexcept {
    LatencyBatch& batch = book.batch;
    if (batch.reads == 0u) {
        batch.state = LatencyStateOf(sleepValid, sleep);
    }
    ++batch.reads;
    ++book.okReads;
    LatencyAddStop(batch.frames, tally.frames);
    LatencyAddStop(batch.refused, tally.refused);
    LatencyAddStop(batch.gpu.spanAbsent, tally.gpu.spanAbsent);
    LatencyAddStop(batch.gpu.spanZero, tally.gpu.spanZero);
    LatencyAddStop(batch.gpu.activeNoSpan, tally.gpu.activeNoSpan);
    LatencyAddStop(batch.gpu.spanNoActive, tally.gpu.spanNoActive);
    LatencyAddStop(batch.gpu.longerFrames, tally.gpu.longerFrames);
    LatencyAddStop(batch.gpu.longerSpanUs, tally.gpu.longerSpanUs);
    LatencyAddStop(batch.gpu.longerActiveUs, tally.gpu.longerActiveUs);
    LatencyAddStop(batch.gpu.withinFrames, tally.gpu.withinFrames);
    LatencyAddStop(batch.gpu.withinSpanUs, tally.gpu.withinSpanUs);
    LatencyAddStop(batch.gpu.withinActiveUs, tally.gpu.withinActiveUs);
    if (sleepValid) {
        batch.sleepOn |= sleep.on & FL_LAT_SLEEP_BITS;
        batch.sleepOff |= ~sleep.on & FL_LAT_SLEEP_BITS;
        batch.multipliers |= LatencyMultiplierBit(sleep.multiplier);
    }
}

// The batch as lines of the native log: a text, the line's `a` (printed in decimal) and its `b` (printed in hex).
inline constexpr uint32_t kLatBatchLines = 7u;

struct LatencyLogLine {
    const char* text;
    uint32_t    a;
    uint64_t    b;
};

constexpr uint64_t LatencyPair(uint32_t high, uint32_t low) noexcept {
    return (static_cast<uint64_t>(high) << 32) | low;
}

constexpr LatencyLogLine LatencyBatchLine(const LatencyBatch& batch, uint32_t line) noexcept {
    switch (line) {
    case 0u:
        return {"reads / sleep switches on | off<<8 | multipliers seen<<16", batch.reads,
                batch.sleepOn | (batch.sleepOff << 8) | (static_cast<uint64_t>(batch.multipliers) << 16)};
    case 1u:
        return {"frames summed / refused", batch.frames, batch.refused};
    case 2u:
        return {"GPU span absent / of no length", batch.gpu.spanAbsent, batch.gpu.spanZero};
    case 3u:
        return {"active time with no span / span with no active time", batch.gpu.activeNoSpan, batch.gpu.spanNoActive};
    case 4u:
        return {"active longer than span: frames / span us<<32 | active us", batch.gpu.longerFrames,
                LatencyPair(batch.gpu.longerSpanUs, batch.gpu.longerActiveUs)};
    case 5u:
        return {"active within span: frames / span us<<32 | active us", batch.gpu.withinFrames,
                LatencyPair(batch.gpu.withinSpanUs, batch.gpu.withinActiveUs)};
    default:
        return {"ms spanned / watchdog ticks on time<<32 | woken early", batch.spannedMs,
                LatencyPair(batch.ticksOnTime, batch.ticksEarly)};
    }
}

// The longest of those texts, for whoever writes them into a line of fixed length.
constexpr uint32_t LatencyBatchLongestText() noexcept {
    uint32_t longest = 0u;
    for (uint32_t line = 0u; line < kLatBatchLines; ++line) {
        const char* text = LatencyBatchLine(LatencyBatch{}, line).text;
        uint32_t    length = 0u;
        while (text[length] != '\0') {
            ++length;
        }
        longest = length > longest ? length : longest;
    }
    return longest;
}

// What the watchdog publishes of it. THE WATCHDOG'S: filled from what a read left behind while nobody is reading.
struct LatencyOut {
    uint32_t     state = FL_LAT_NONE;    // FlLatencyState
    uint32_t     sleepSeenOn = 0;        // switches the status showed on, since the request
    uint32_t     sleepSeenOff = 0;       // ... and off
    uint32_t     multiplier = 0;         // the last frame-generation multiplier the status gave
    uint32_t     multipliersSeen = 0;    // every one it gave since the request (LatencyMultiplierBit)
    uint32_t     reads = 0;              // reads made since the request
    uint32_t     worstUs = 0;            // the slowest call since the request
    LatencyTally tally;                  // of the newest read not yet published; Publish empties it
};

inline uint32_t LatencySum(const LatencyTally& tally, uint32_t span) noexcept {
    if (tally.frames == 0u) {
        return 0u;
    }
    return (tally.lacking & (1u << span)) != 0u ? FL_LAT_SUM_NONE : tally.sumUs[span];
}

inline uint32_t Stop16(uint32_t value) noexcept {
    return value > 0xFFFFu ? 0xFFFFu : value;
}

// A NEW REQUEST: the summaries start afresh, so a session's ranges, masks and
// counts are its own and not those of the session before it in the same game.
// THE WATCHDOG'S. A detour running beside it loses nothing that matters: what it
// stores is of the call it is in, which belongs to the request that is starting.
// The two owners stay - which module's calls are counted is a fact about the
// process, not about a session. SO DO THE FEATURE SLOTS (0.1.0-beta.44): what
// version a feature is and what it requires do not change while the game runs,
// and a game asks once, while it starts -- a second session of the same process
// would otherwise have nothing where the first had the answer. Their bit of the
// mask is therefore kept with them.
inline void Reset(Accumulators& a) noexcept {
    const auto relaxed = std::memory_order_relaxed;
    a.validMask.store(a.features[0].load(relaxed) != 0u ? static_cast<uint32_t>(FL_ADV_VALID_SL_FEATURES) : 0u,
                      relaxed);
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
    a.ffxFgConfigures.store(0u, relaxed);
    a.ffxFgEnabledSeen.store(0u, relaxed);
    a.ffxFgAsyncSeen.store(0u, relaxed);
    a.ffxFgOnlyGeneratedSeen.store(0u, relaxed);
    a.ffxFgFlagsSeen.store(0u, relaxed);
    a.ffxFgRect.store(0u, relaxed);
    // Which viewport is summarised stays, as the owners do: a fact about the process.
    a.slConstantsCalls.store(0u, relaxed);
    a.slResets.store(0u, relaxed);
    a.slJitterMaxXBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slJitterMaxYBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slMvecScaleXBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slMvecScaleYBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slNearBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slFarBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slFovMinBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slFovMaxBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slAspectBits.store(FL_ADV_FLOAT_NONE, relaxed);
    a.slFlagsSeenTrue.store(0u, relaxed);
    a.slFlagsSeenFalse.store(0u, relaxed);
    a.slShutdowns.store(0u, relaxed);
    // The feature slots stay; what of their word 1 is a session's does not (0.1.0-beta.45).
    for (uint32_t i = 0; i < FL_ADV_FEATURE_SLOTS; ++i) {
        std::atomic<uint32_t>* slot = &a.features[static_cast<size_t>(i) * FL_ADV_FEATURE_WORDS];
        if (slot[0].load(relaxed) == 0u) {
            break;
        }
        if ((slot[1].load(relaxed) & kFeatureSessionBits) != 0u) {
            slot[1].fetch_and(~kFeatureSessionBits, relaxed);
        }
    }
}

// Copies the accumulators into region 5, under the request they answer. THE
// WATCHDOG'S, and only the watchdog's: one writer, so the seqlock is odd -> stores
// -> even with no compare-exchange. The frame-time pair is drained, so each
// publication carries the dispatches since the last one.
//
// `latency` is the watchdog's summary of the driver's latency report, or null for a caller that has none: the
// fourteen latency words are then left as they are -- zero, in a region nobody else writes.
inline void Publish(Accumulators& a, FlAdvancedState* region, uint32_t request, uint64_t qpc,
                    LatencyOut* latency = nullptr) noexcept {
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

    if (latency != nullptr) {
        LatencyOut& out = *latency;
        put32(region->latState, (out.state & FL_LAT_STATE_MASK) |
                                    ((out.sleepSeenOn & FL_LAT_SLEEP_BITS) << FL_LAT_SLEEP_SEEN_ON_SHIFT) |
                                    ((out.sleepSeenOff & FL_LAT_SLEEP_BITS) << FL_LAT_SLEEP_SEEN_OFF_SHIFT) |
                                    ((out.multiplier > 255u ? 255u : out.multiplier) << FL_LAT_FG_MULTIPLIER_SHIFT));
        put32(region->latReads, Stop16(out.reads) | (Stop16(out.worstUs) << 16));
        put32(region->latFrames, Stop16(out.tally.frames) | (Stop16(out.tally.refused) << 16));
        put32(region->latSimulationSumUs, LatencySum(out.tally, 0u));
        put32(region->latSubmitSumUs, LatencySum(out.tally, 1u));
        put32(region->latPresentSumUs, LatencySum(out.tally, 2u));
        put32(region->latDriverSumUs, LatencySum(out.tally, 3u));
        put32(region->latOsQueueSumUs, LatencySum(out.tally, 4u));
        put32(region->latGpuSumUs, LatencySum(out.tally, 5u));
        put32(region->latTotalSumUs, LatencySum(out.tally, kLatTotal));
        put32(region->latGpuActiveSumUs, out.tally.activeSumUs);
        put32(region->latGpuFrameSumUs, out.tally.frameTimeSumUs);
        put32(region->latGpuCounts, Stop16(out.tally.activeFrames) | (Stop16(out.tally.frameTimeFrames) << 16));
        put32(region->latMultipliersSeen, out.multipliersSeen);
        out.tally = LatencyTally{};    // one publication carries one read: a reader never adds two up
    }

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

    put32(region->ffxFgConfigures, a.ffxFgConfigures.load(relaxed));
    put32(region->ffxFgEnabledSeen, a.ffxFgEnabledSeen.load(relaxed));
    put32(region->ffxFgAsyncSeen, a.ffxFgAsyncSeen.load(relaxed));
    put32(region->ffxFgOnlyGeneratedSeen, a.ffxFgOnlyGeneratedSeen.load(relaxed));
    put32(region->ffxFgFlagsSeen, a.ffxFgFlagsSeen.load(relaxed));
    const uint64_t rect = a.ffxFgRect.load(relaxed);
    for (uint32_t i = 0; i < 4u; ++i) {
        const auto side = rect == kRectUnfit ? int32_t{-1} : static_cast<int32_t>((rect >> (16u * i)) & 0xFFFFu);
        std::atomic_ref<int32_t>{region->ffxFgRect[i]}.store(side, relaxed);
    }

    put32(region->slConstantsCalls, a.slConstantsCalls.load(relaxed));
    put32(region->slResets, a.slResets.load(relaxed));
    put32(region->slJitterMaxXBits, a.slJitterMaxXBits.load(relaxed));
    put32(region->slJitterMaxYBits, a.slJitterMaxYBits.load(relaxed));
    put32(region->slMvecScaleXBits, a.slMvecScaleXBits.load(relaxed));
    put32(region->slMvecScaleYBits, a.slMvecScaleYBits.load(relaxed));
    put32(region->slNearBits, a.slNearBits.load(relaxed));
    put32(region->slFarBits, a.slFarBits.load(relaxed));
    put32(region->slFovMinBits, a.slFovMinBits.load(relaxed));
    put32(region->slFovMaxBits, a.slFovMaxBits.load(relaxed));
    put32(region->slAspectBits, a.slAspectBits.load(relaxed));
    put32(region->slFlagsSeenTrue, a.slFlagsSeenTrue.load(relaxed));
    put32(region->slFlagsSeenFalse, a.slFlagsSeenFalse.load(relaxed));
    put32(region->slShutdowns, a.slShutdowns.load(relaxed));

    for (uint32_t i = 0; i < FL_ADV_FEATURE_SLOTS * FL_ADV_FEATURE_WORDS; ++i) {
        put32(region->features[i], a.features[i].load(relaxed));
    }

    std::atomic_ref<uint32_t> samples{region->samples};
    samples.store(samples.load(relaxed) + 1u, relaxed);
    seq.store(odd + 1u, std::memory_order_release);
}

}    // namespace fl::adv

#endif    // FRAMELEDGER_FL_ADV_FIELDS_H
