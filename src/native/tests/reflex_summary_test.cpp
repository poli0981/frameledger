// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_adv_fields.h's reader of the driver's latency report (Advanced metrics train C; spike-notes §31), driven
// without the Overlay and without a driver: the header the Overlay compiles, on reports written here by hand.
//
// What it holds is what a real report made necessary. The newest frames of a report are not final -- one run of the
// probe read a span of half an hour in the newest -- so the two newest are never summed. An end point of zero is a
// span the driver did not report, not a fault of the frame: without a sleep mode it leaves driverStartTime zero in
// every frame, and a reader that refused those frames would refuse every frame of such a game. A span that runs
// backwards or lasts over a second is refused with its frame. And a game whose frame ids start again must not be
// read as a game that has stopped.

#include <catch2/catch_test_macros.hpp>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fl_adv_fields.h>
#include <memory>

namespace {

using Frame = NV_LATENCY_RESULT_PARAMS::FrameReport;

// A frame as the stand-in module writes one: every span reported, 7500 us from simulation start to the GPU's end.
Frame Whole(std::uint64_t id) {
    Frame               f{};
    const std::uint64_t t = 1'000'000ull + id * 4200ull;
    f.frameID = id;
    f.simStartTime = t + 100u;
    f.simEndTime = t + 900u;    // 800
    f.renderSubmitStartTime = t + 950u;
    f.renderSubmitEndTime = t + 2100u;    // 1150
    f.presentStartTime = t + 2150u;
    f.presentEndTime = t + 2300u;    // 150
    f.driverStartTime = t + 2300u;
    f.driverEndTime = t + 2700u;    // 400
    f.osRenderQueueStartTime = t + 2700u;
    f.osRenderQueueEndTime = t + 3900u;    // 1200
    f.gpuRenderStartTime = t + 3900u;
    f.gpuRenderEndTime = t + 7600u;    // 3700; total 7500
    return f;
}

// The report is 15 KB: on the heap here, as it is in static storage in the Overlay.
std::unique_ptr<NV_LATENCY_RESULT_PARAMS> Report(std::uint64_t firstId, std::uint32_t frames = 64u) {
    auto report = std::make_unique<NV_LATENCY_RESULT_PARAMS>();
    for (std::uint32_t i = 0; i < frames; ++i) {
        report->frameReport[64u - frames + i] = Whole(firstId + i);    // the newest last, as the driver writes it
    }
    return report;
}

}    // namespace

TEST_CASE("a report's two newest frames are left for the next read, and a frame is summed once", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;

    auto first = Report(5000u);
    fl::adv::ObserveLatencyReport(*first, cursor, tally);
    CHECK(tally.frames == 62u);    // 5000..5061; 5062 and 5063 are not final yet
    CHECK(tally.refused == 0u);
    CHECK(tally.lacking == 0u);
    CHECK(tally.sumUs[0] == 62u * 800u);
    CHECK(tally.sumUs[1] == 62u * 1150u);
    CHECK(tally.sumUs[2] == 62u * 150u);
    CHECK(tally.sumUs[3] == 62u * 400u);
    CHECK(tally.sumUs[4] == 62u * 1200u);
    CHECK(tally.sumUs[5] == 62u * 3700u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 62u * 7500u);
    CHECK(cursor.lastFrameId == 5061u);

    // The same report again: nothing is new.
    fl::adv::ObserveLatencyReport(*first, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 0u);
    CHECK(cursor.lastFrameId == 5061u);

    // Ten frames further on: the two that were held back and eight more, and again not the two newest.
    auto second = Report(5010u);
    fl::adv::ObserveLatencyReport(*second, cursor, tally);
    CHECK(tally.frames == 10u);    // 5062..5071
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 10u * 7500u);
    CHECK(cursor.lastFrameId == 5071u);
}

TEST_CASE("a report of nothing, of one frame and of two sums nothing", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    tally.frames = 9u;    // whatever was there is replaced, not added to

    auto empty = Report(0u, 0u);
    fl::adv::ObserveLatencyReport(*empty, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(cursor.lastFrameId == 0u);

    auto one = Report(700u, 1u);
    fl::adv::ObserveLatencyReport(*one, cursor, tally);
    CHECK(tally.frames == 0u);

    auto two = Report(700u, 2u);
    fl::adv::ObserveLatencyReport(*two, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 0u);

    auto three = Report(700u, 3u);
    fl::adv::ObserveLatencyReport(*three, cursor, tally);
    CHECK(tally.frames == 1u);    // 700; 701 and 702 wait
    CHECK(cursor.lastFrameId == 700u);
}

TEST_CASE("an end point of zero is a span the driver did not report: the frame is summed and the span reads none",
          "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);
    for (auto& f : report->frameReport) {
        f.driverStartTime = 0u;    // what the driver gives a game that sets markers and no sleep mode
    }
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(tally.refused == 0u);
    CHECK(tally.lacking == (1u << 3));
    CHECK(tally.sumUs[3] == 0u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 62u * 7500u);

    // What is published for it: the sum of a span some summed frame lacked is all ones, never a smaller number.
    CHECK(fl::adv::LatencySum(tally, 3u) == FL_LAT_SUM_NONE);
    CHECK(fl::adv::LatencySum(tally, 0u) == 62u * 800u);
    CHECK(fl::adv::LatencySum(fl::adv::LatencyTally{}, 0u) == 0u);    // no frame: zero, not "none"
}

TEST_CASE("a span no frame can have refuses the frame whole, and so does a frame with no total", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);
    report->frameReport[0].presentEndTime = report->frameReport[0].presentStartTime - 50u;               // backwards
    report->frameReport[1].gpuRenderEndTime = report->frameReport[1].simStartTime + 1'844'674'519ull;    // half an hour
    report->frameReport[2].gpuRenderEndTime = 0u;                                                        // no total
    report->frameReport[3].simStartTime = 0u;                                                            // no total
    report->frameReport[4].osRenderQueueEndTime = report->frameReport[4].osRenderQueueStartTime + 1'000'001ull;
    report->frameReport[5].osRenderQueueEndTime = report->frameReport[5].osRenderQueueStartTime + 1'000'000ull;

    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.refused == 5u);
    CHECK(tally.frames == 57u);    // 62 that are not the two newest, less the five; frame 5's second is still a span
    CHECK(tally.lacking == 0u);
    CHECK(tally.sumUs[2] == 57u * 150u);                  // the backwards present added nothing
    CHECK(tally.sumUs[4] == 56u * 1200u + 1'000'000u);    // exactly a second is allowed; a microsecond more is not
    CHECK(cursor.lastFrameId == 161u);                    // a refused frame is not looked at again either
}

TEST_CASE("frame ids that start again restart the reader instead of silencing it", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   late = Report(90'000u);
    fl::adv::ObserveLatencyReport(*late, cursor, tally);
    REQUIRE(cursor.lastFrameId == 90'061u);

    auto again = Report(1u);    // the game went back to its menu and counts from one
    fl::adv::ObserveLatencyReport(*again, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(cursor.lastFrameId == 62u);
}

TEST_CASE("the frames are found whatever order the driver left them in", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(300u);
    const Frame            newest = report->frameReport[63];
    report->frameReport[63] = report->frameReport[10];
    report->frameReport[10] = newest;    // the newest id is in the middle now
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(cursor.lastFrameId == 361u);    // 362 and 363 are the two newest wherever they sit
}

TEST_CASE("the sleep status is the driver's five switches and its multiplier, and nothing else of it", "[latency]") {
    NV_GET_SLEEP_STATUS_PARAMS status{};
    CHECK(fl::adv::ObserveSleepStatus(status).on == 0u);
    status.bLowLatencyMode = 1;
    status.bCplVsyncOn = 1;
    status.bFullscreenIFlip = 1;
    status.fgMultiplier = 3;
    status.sleepIntervalUs = 4166u;    // changes with every frame: not kept
    const fl::adv::LatencySleep seen = fl::adv::ObserveSleepStatus(status);
    CHECK(seen.on == (1u | 4u | 16u));
    CHECK(seen.multiplier == 3u);
    status = NV_GET_SLEEP_STATUS_PARAMS{};
    status.bFsVrr = 1;
    status.bUseGameSleep = 1;
    CHECK(fl::adv::ObserveSleepStatus(status).on == (2u | 8u));
}

TEST_CASE("the driver's two own times are summed over the frames that gave one, each with its own count", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;

    // Twelve frames, 5000..5011: ten are summed, the two newest wait.
    auto report = Report(5000u, 12u);
    for (std::uint32_t i = 52u; i < 64u; ++i) {
        report->frameReport[i].gpuActiveRenderTimeUs = 3500u;
        report->frameReport[i].gpuFrameTimeUs = 4200u;
    }
    report->frameReport[52].gpuActiveRenderTimeUs = 0u;            // 5000: the driver gave no active time
    report->frameReport[53].gpuFrameTimeUs = 1'000'001u;           // 5001: a frame time no frame can have
    report->frameReport[54].gpuActiveRenderTimeUs = 1'000'000u;    // 5002: a second, to the microsecond, is kept
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 10u);    // neither of the two refuses a frame
    CHECK(tally.refused == 0u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 10u * 7500u);
    CHECK(tally.lacking == 0u);
    CHECK(tally.activeFrames == 9u);
    CHECK(tally.activeSumUs == 8u * 3500u + 1'000'000u);
    CHECK(tally.frameTimeFrames == 9u);
    CHECK(tally.frameTimeSumUs == 9u * 4200u);

    // A frame that is refused gives neither, and a driver that fills in neither leaves both counts at zero.
    cursor = fl::adv::LatencyCursor{};
    auto refused = Report(6000u, 3u);
    refused->frameReport[61].gpuActiveRenderTimeUs = 3500u;
    refused->frameReport[61].gpuFrameTimeUs = 4200u;
    refused->frameReport[61].presentEndTime = refused->frameReport[61].presentStartTime - 50u;
    fl::adv::ObserveLatencyReport(*refused, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 1u);
    CHECK(tally.activeFrames == 0u);
    CHECK(tally.frameTimeFrames == 0u);

    cursor = fl::adv::LatencyCursor{};
    auto older = Report(7000u);
    fl::adv::ObserveLatencyReport(*older, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(tally.activeFrames == 0u);
    CHECK(tally.activeSumUs == 0u);
    CHECK(tally.frameTimeFrames == 0u);
}

TEST_CASE("a multiplier the status gave has a bit of its own, and thirty-one and more share the last", "[latency]") {
    CHECK(fl::adv::LatencyMultiplierBit(0u) == 1u);
    CHECK(fl::adv::LatencyMultiplierBit(1u) == 2u);
    CHECK(fl::adv::LatencyMultiplierBit(4u) == 16u);
    CHECK(fl::adv::LatencyMultiplierBit(31u) == 0x80000000u);
    CHECK(fl::adv::LatencyMultiplierBit(255u) == 0x80000000u);
}

TEST_CASE("a publication carries one read, packed as fl_shm.h says, and carries it once", "[latency]") {
    fl::adv::Accumulators a;
    fl::FlAdvancedState   region{};
    fl::adv::LatencyOut   out;
    out.state = fl::FL_LAT_READING;
    out.sleepSeenOn = 1u | 8u;
    out.sleepSeenOff = 2u | 4u | 16u;
    out.multiplier = 2u;
    out.multipliersSeen = (1u << 1) | (1u << 2);
    out.reads = 70'000u;    // stops at 65535
    out.worstUs = 66u;
    out.tally.frames = 10u;
    out.tally.refused = 1u;
    for (std::uint32_t i = 0; i < fl::adv::kLatSpans; ++i) {
        out.tally.sumUs[i] = 1000u * (i + 1u);
    }
    out.tally.lacking = 1u << 3;
    out.tally.activeSumUs = 31'500u;
    out.tally.activeFrames = 9u;
    out.tally.frameTimeSumUs = 42'000u;
    out.tally.frameTimeFrames = 10u;

    fl::adv::Publish(a, &region, 7u, 1234u, &out);
    CHECK((region.latState & FL_LAT_STATE_MASK) == fl::FL_LAT_READING);
    CHECK(((region.latState >> FL_LAT_SLEEP_SEEN_ON_SHIFT) & FL_LAT_SLEEP_BITS) == (1u | 8u));
    CHECK(((region.latState >> FL_LAT_SLEEP_SEEN_OFF_SHIFT) & FL_LAT_SLEEP_BITS) == (2u | 4u | 16u));
    CHECK((region.latState >> FL_LAT_FG_MULTIPLIER_SHIFT) == 2u);
    CHECK((region.latReads & 0xFFFFu) == 0xFFFFu);
    CHECK((region.latReads >> 16) == 66u);
    CHECK((region.latFrames & 0xFFFFu) == 10u);
    CHECK((region.latFrames >> 16) == 1u);
    CHECK(region.latSimulationSumUs == 1000u);
    CHECK(region.latSubmitSumUs == 2000u);
    CHECK(region.latPresentSumUs == 3000u);
    CHECK(region.latDriverSumUs == FL_LAT_SUM_NONE);
    CHECK(region.latOsQueueSumUs == 5000u);
    CHECK(region.latGpuSumUs == 6000u);
    CHECK(region.latTotalSumUs == 7000u);
    CHECK(region.latGpuActiveSumUs == 31'500u);
    CHECK(region.latGpuFrameSumUs == 42'000u);
    CHECK((region.latGpuCounts & 0xFFFFu) == 9u);
    CHECK((region.latGpuCounts >> 16) == 10u);
    CHECK(region.latMultipliersSeen == ((1u << 1) | (1u << 2)));
    CHECK(region.request == 7u);

    // The next publication, with no read in between: the state stands, the frames and the sums are gone.
    fl::adv::Publish(a, &region, 7u, 1235u, &out);
    CHECK((region.latState & FL_LAT_STATE_MASK) == fl::FL_LAT_READING);
    CHECK(region.latFrames == 0u);
    CHECK(region.latTotalSumUs == 0u);
    CHECK(region.latDriverSumUs == 0u);
    CHECK(region.latGpuActiveSumUs == 0u);
    CHECK(region.latGpuFrameSumUs == 0u);
    CHECK(region.latGpuCounts == 0u);
    CHECK(region.latMultipliersSeen == ((1u << 1) | (1u << 2)));    // the session's, like the switches
    CHECK((region.latReads >> 16) == 66u);

    // A caller with no latency summary leaves the latency words alone.
    fl::FlAdvancedState untouched{};
    fl::adv::Publish(a, &untouched, 7u, 1236u);
    CHECK(untouched.latState == 0u);
    CHECK(untouched.latReads == 0u);
    CHECK(untouched.latTotalSumUs == 0u);
    CHECK(untouched.latGpuActiveSumUs == 0u);
    CHECK(untouched.latGpuCounts == 0u);
    CHECK(untouched.latMultipliersSeen == 0u);
}

// ---------------------------------------------------------------------------------------------------------------------
// THE COUNTS FOR THE NATIVE LOG (0.1.0-beta.49, owner decisions D148, D151).
// ---------------------------------------------------------------------------------------------------------------------

TEST_CASE("each summed frame is counted by its GPU span and the driver's active time, and what is summed does not move",
          "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);    // 100..163: 100..161 are summed
    for (std::uint32_t i = 0; i < 64u; ++i) {
        Frame& f = report->frameReport[i];
        switch (i % 6u) {
        case 0u:    // a span of 3700 and an active time that is the shorter
            f.gpuActiveRenderTimeUs = 3500u;
            break;
        case 1u:    // the two marks at one instant, and no active time
            f.gpuRenderStartTime = f.gpuRenderEndTime;
            break;
        case 2u:    // an active time longer than the span
            f.gpuActiveRenderTimeUs = 4000u;
            break;
        case 3u:    // the span, and no active time
            break;
        case 4u:    // the two marks at one instant, and an active time
            f.gpuRenderStartTime = f.gpuRenderEndTime;
            f.gpuActiveRenderTimeUs = 3500u;
            break;
        default:    // no start mark, and an active time
            f.gpuRenderStartTime = 0u;
            f.gpuActiveRenderTimeUs = 3500u;
            break;
        }
    }
    fl::adv::ObserveLatencyReport(*report, cursor, tally);

    // Sixty-two frames: eleven each of the first two shapes, ten each of the other four.
    REQUIRE(tally.frames == 62u);
    CHECK(tally.refused == 0u);
    CHECK(tally.gpu.withinFrames == 11u);
    CHECK(tally.gpu.withinSpanUs == 11u * 3700u);
    CHECK(tally.gpu.withinActiveUs == 11u * 3500u);
    CHECK(tally.gpu.longerFrames == 10u);
    CHECK(tally.gpu.longerSpanUs == 10u * 3700u);
    CHECK(tally.gpu.longerActiveUs == 10u * 4000u);
    CHECK(tally.gpu.spanZero == 11u + 10u);
    CHECK(tally.gpu.spanAbsent == 10u);
    CHECK(tally.gpu.spanNoActive == 10u);
    CHECK(tally.gpu.activeNoSpan == 10u + 10u);
    // Every summed frame is in exactly one place: the four counted here, or neither a span nor an active time -- the
    // eleven whose two marks are one instant and that gave no time.
    const std::uint32_t placed =
        tally.gpu.withinFrames + tally.gpu.longerFrames + tally.gpu.spanNoActive + tally.gpu.activeNoSpan;
    CHECK(placed + 11u == tally.frames);

    // WHAT IS PUBLISHED IS WHAT IT WAS: the span over every frame that reported one, the time over those that gave one.
    CHECK(tally.sumUs[fl::adv::kLatGpu] == (11u + 10u + 10u) * 3700u);
    CHECK((tally.lacking & (1u << fl::adv::kLatGpu)) != 0u);
    CHECK(tally.activeFrames == 11u + 10u + 10u + 10u);
    CHECK(tally.activeSumUs == 31u * 3500u + 10u * 4000u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 62u * 7500u);
}

TEST_CASE(
    "an active time as long as the span is not the longer; one of over a second was not given; a refused frame is "
    "not counted",
    "[latency]") {
    fl::adv::LatencyGpuCounts counts;
    fl::adv::CountGpuTimes(fl::adv::kLatSpanReported, 3700u, 3700u, counts);
    CHECK(counts.withinFrames == 1u);
    CHECK(counts.longerFrames == 0u);
    fl::adv::CountGpuTimes(fl::adv::kLatSpanReported, 1u, 2u, counts);    // a span of a microsecond is a span
    CHECK(counts.longerFrames == 1u);
    CHECK(counts.longerSpanUs == 1u);
    CHECK(counts.longerActiveUs == 2u);
    CHECK(counts.spanZero == 0u);
    fl::adv::CountGpuTimes(fl::adv::kLatSpanReported, 3700u, 1'000'001u, counts);    // no frame has such a time
    CHECK(counts.spanNoActive == 1u);
    CHECK(counts.longerFrames == 1u);

    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);
    for (auto& f : report->frameReport) {
        f.gpuActiveRenderTimeUs = 3500u;
    }
    report->frameReport[5].presentEndTime = report->frameReport[5].presentStartTime - 50u;    // refused whole
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 61u);
    CHECK(tally.refused == 1u);
    CHECK(tally.gpu.withinFrames == 61u);
    CHECK(tally.gpu.withinSpanUs == 61u * 3700u);
}

TEST_CASE("a batch of the log's counts is written after ten reads, after thirty, and every sixty from there",
          "[latency]") {
    std::uint32_t due = 0u;
    for (std::uint32_t reads = 1u; reads <= 400u; ++reads) {
        const bool expected = reads == 10u || reads == 30u || reads == 90u || reads == 150u || reads == 210u ||
                              reads == 270u || reads == 330u || reads == 390u;
        CHECK(fl::adv::LatencyBatchDue(reads) == expected);
        due += fl::adv::LatencyBatchDue(reads) ? 1u : 0u;
    }
    CHECK(due == 8u);
    CHECK_FALSE(fl::adv::LatencyBatchDue(0u));
}

TEST_CASE("a batch belongs to one state of the sleep status: another state closes it, until a request has closed "
          "thirty-two that way",
          "[latency]") {
    fl::adv::LatencyTally tally;
    tally.frames = 10u;
    tally.refused = 1u;
    tally.gpu.spanZero = 2u;
    tally.gpu.longerFrames = 3u;
    tally.gpu.longerSpanUs = 300u;
    tally.gpu.longerActiveUs = 450u;
    tally.gpu.withinFrames = 4u;
    tally.gpu.withinSpanUs = 4000u;
    tally.gpu.withinActiveUs = 3600u;

    // The state: the low-latency switch, "the game sleeps", and the multiplier -- not the other three switches.
    const fl::adv::LatencySleep reflexTwo{1u | 8u, 2u};
    const fl::adv::LatencySleep reflexTwoVrr{1u | 2u | 8u | 16u, 2u};
    const fl::adv::LatencySleep overrideOne{1u, 1u};
    const fl::adv::LatencySleep manyFrames{0u, 900u};
    CHECK(fl::adv::LatencyStateOf(true, reflexTwo) == ((1u | 8u) | (2u << 8)));
    CHECK(fl::adv::LatencyStateOf(true, reflexTwoVrr) == fl::adv::LatencyStateOf(true, reflexTwo));
    CHECK(fl::adv::LatencyStateOf(true, overrideOne) != fl::adv::LatencyStateOf(true, reflexTwo));
    CHECK(fl::adv::LatencyStateOf(false, reflexTwo) == fl::adv::kLatStateUnknown);
    CHECK(fl::adv::LatencyStateOf(true, manyFrames) == (255u << 8));

    fl::adv::LatencyBatchBook book;
    CHECK_FALSE(fl::adv::LatencyBatchTurns(book, fl::adv::LatencyStateOf(true, reflexTwo)));    // nothing to close
    fl::adv::LatencyBatchAdd(book, tally, true, reflexTwo);
    fl::adv::LatencyBatchAdd(book, tally, true, reflexTwoVrr);
    CHECK(book.okReads == 2u);
    CHECK(book.batch.reads == 2u);
    CHECK(book.batch.frames == 20u);
    CHECK(book.batch.refused == 2u);
    CHECK(book.batch.gpu.spanZero == 4u);
    CHECK(book.batch.gpu.longerFrames == 6u);
    CHECK(book.batch.gpu.longerActiveUs == 900u);
    CHECK(book.batch.gpu.withinSpanUs == 8000u);
    CHECK(book.batch.state == fl::adv::LatencyStateOf(true, reflexTwo));
    CHECK(book.batch.sleepOn == (1u | 2u | 8u | 16u));
    CHECK(book.batch.sleepOff == (2u | 4u | 16u));    // off in one read or the other
    CHECK(book.batch.multipliers == (1u << 2));
    CHECK_FALSE(fl::adv::LatencyBatchTurns(book, fl::adv::LatencyStateOf(true, reflexTwoVrr)));
    CHECK(fl::adv::LatencyBatchTurns(book, fl::adv::LatencyStateOf(true, overrideOne)));
    CHECK(fl::adv::LatencyBatchTurns(book, fl::adv::kLatStateUnknown));    // a read with no status is a state too

    // A read with no status adds its frames and nothing to the "seen" words.
    fl::adv::LatencyBatchAdd(book, tally, false, overrideOne);
    CHECK(book.batch.reads == 3u);
    CHECK(book.batch.sleepOn == (1u | 2u | 8u | 16u));
    CHECK(book.batch.multipliers == (1u << 2));

    // A status that flickers: after thirty-two closings the batch runs on, and says what it held.
    book.closures = fl::adv::kLatStateClosures;
    CHECK_FALSE(fl::adv::LatencyBatchTurns(book, fl::adv::LatencyStateOf(true, overrideOne)));
    fl::adv::LatencyBatchAdd(book, tally, true, overrideOne);
    CHECK(book.batch.state == fl::adv::LatencyStateOf(true, reflexTwo));    // its first read's
    CHECK(book.batch.multipliers == ((1u << 1) | (1u << 2)));
    CHECK(book.batch.sleepOff == (2u | 4u | 8u | 16u));

    // A sum stops at the top of its word instead of wrapping into a small number.
    std::uint32_t sum = 0xFFFFFFF0u;
    fl::adv::LatencyAddStop(sum, 0x20u);
    CHECK(sum == 0xFFFFFFFFu);
}

TEST_CASE("a batch is seven lines of the log: a text that fits, a count in decimal, and a word in hex", "[latency]") {
    fl::adv::LatencyBatch batch;
    batch.reads = 10u;
    batch.frames = 152u;
    batch.refused = 3u;
    batch.sleepOn = 1u | 8u;
    batch.sleepOff = 2u | 4u | 16u;
    batch.multipliers = 1u << 2;
    batch.gpu.spanAbsent = 5u;
    batch.gpu.spanZero = 61u;
    batch.gpu.activeNoSpan = 30u;
    batch.gpu.spanNoActive = 31u;
    batch.gpu.longerFrames = 30u;
    batch.gpu.longerSpanUs = 111000u;
    batch.gpu.longerActiveUs = 120000u;
    batch.gpu.withinFrames = 26u;
    batch.gpu.withinSpanUs = 96200u;
    batch.gpu.withinActiveUs = 91000u;
    batch.spannedMs = 10012u;
    batch.ticksOnTime = 10u;
    batch.ticksEarly = 3u;

    const std::uint64_t expectedB[fl::adv::kLatBatchLines] = {
        0x41609ull,          3ull, 61ull, 31ull, (111000ull << 32) | 120000ull, (96200ull << 32) | 91000ull,
        (10ull << 32) | 3ull};
    const std::uint32_t expectedA[fl::adv::kLatBatchLines] = {10u, 152u, 5u, 30u, 30u, 26u, 10012u};
    for (std::uint32_t line = 0; line < fl::adv::kLatBatchLines; ++line) {
        const fl::adv::LatencyLogLine entry = fl::adv::LatencyBatchLine(batch, line);
        INFO("line " << line << ": " << entry.text);
        CHECK(entry.a == expectedA[line]);
        CHECK(entry.b == expectedB[line]);
        CHECK(std::strlen(entry.text) <= fl::adv::LatencyBatchLongestText());
        CHECK(std::strlen(entry.text) < 72u);    // the native log's event has 72 characters of text, the last a zero
        for (std::uint32_t other = 0; other < line; ++other) {
            CHECK(std::strcmp(entry.text, fl::adv::LatencyBatchLine(batch, other).text) != 0);
        }
    }
}

// What summing one report costs: the reader runs on a game's presenting thread, once a second. Hidden -- a number
// for a pull request, not a gate: `fl_reflex_summary_test "[.bench]"`.
TEST_CASE("what summing one report of sixty-four frames costs", "[.bench]") {
    auto report = Report(5000u);
    for (auto& f : report->frameReport) {
        f.gpuActiveRenderTimeUs = 3500u;
        f.gpuFrameTimeUs = 4200u;
    }
    constexpr std::uint32_t kRuns = 2'000'000u;
    std::uint64_t           frames = 0u;
    for (int pass = 0; pass < 5; ++pass) {
        const auto began = std::chrono::steady_clock::now();
        for (std::uint32_t run = 0; run < kRuns; ++run) {
            fl::adv::LatencyCursor cursor;    // every run sums the report's sixty-two frames anew
            fl::adv::LatencyTally  tally;
            fl::adv::ObserveLatencyReport(*report, cursor, tally);
            frames += tally.frames + tally.gpu.withinFrames;
        }
        const auto nanos =
            std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now() - began).count();
        std::printf("pass %d: %.1f ns a report\n", pass, static_cast<double>(nanos) / kRuns);
    }
    CHECK(frames == 5ull * kRuns * (62u + 62u));
}
