// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Runtime.InteropServices;
using FrameLedger.Application.Advanced;
using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>
/// Turns what the Overlay published in region 5 into what a session stores: the session facts of the entries whose
/// reach is <see cref="AdvancedReach.Game"/>, and the readings of its two series (Advanced metrics train B, 0.1.0-beta.40).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is stored is what the game SAID</b> — to DLSS with an evaluation, to FSR with a dispatch, and what FSR
/// answered when the game asked it for its memory. Nothing here checks that the upscaler used it, and nothing is
/// derived: a size is the size, a preset the vendor's letter, a member the vendor's own name for it.
/// </para>
/// <para>
/// <b>Zero is "not read", never a value</b> (<c>07_IPC</c> §Region 5). A Boolean and a preset arrive as the vendor's
/// value plus one, a float as its bits with <see cref="FlAdvancedState.FloatNone"/> for "none seen", a size of 0 as "not
/// given" — and each of those is a fact that is absent here, not a fact that says no.
/// </para>
/// <para>
/// <b>The facts are the same list until something in them changes.</b> The recorder writes its context when a
/// reference changes, and a publication arrives every second: the list is rebuilt only when a field a fact is made of
/// moved — a field of view that widened, a flag seen for the first time.
/// </para>
/// <para>Not for two threads: the capture loop's task is its one caller.</para>
/// </remarks>
public sealed class InGameAdvancedSampler
{
    /// <summary>The frame time the game reported to FSR: the mean of the dispatches of one second, in milliseconds.</summary>
    public const string FrameTimeSeries = AdvancedMetric.SeriesPrefix + "ffx.upscale.frame_time_delta";

    /// <summary>FSR's own video memory as it answered the game, in mebibytes — a point for each second the game asked.</summary>
    public const string MemorySeries = AdvancedMetric.SeriesPrefix + "ffx.memory_usage";

    /// <summary>
    /// The stem of the seven series of the driver's latency report (train C): <c>adv.nvapi.reflex.frame_report.</c>
    /// and one of <see cref="LatencySpans"/>. Each point is the mean, in microseconds, of the frames one read summed.
    /// </summary>
    public const string LatencySeriesStem = AdvancedMetric.SeriesPrefix + "nvapi.reflex.frame_report.";

    private const double _mebibyte = 1024.0 * 1024.0;

    // The spans of a frame the driver reports, in the order the capture side publishes their sums.
    private static readonly string[] _latencySpans = ["simulation", "render_submit", "present", "driver", "os_queue", "gpu_render", "total"];

    // NV_GET_SLEEP_STATUS_PARAMS's five switches, by bit: the vendor's own names, shown as they are.
    private static readonly string[] _sleepSwitches = ["bLowLatencyMode", "bFsVrr", "bCplVsyncOn", "bUseGameSleep", "bFullscreenIFlip"];

    // What the second row of a field of view over pi says (owner decision D128).
    private const string _notRadians = "not_radians";

    private static readonly string[] _presetModes = ["DLAA", "Quality", "Balanced", "Performance", "Ultra Performance", "Ultra Quality"];

    // FfxApiDispatchUpscaleFlags (ffx_upscale.h), by bit.
    private static readonly string[] _flagTokens = ["draw_debug_view", "non_linear_color_srgb", "non_linear_color_pq"];

    // sl::Result (sl_result.h), by value.
    private static readonly string[] _results =
    [
        "eOk", "eErrorIO", "eErrorDriverOutOfDate", "eErrorOSOutOfDate", "eErrorOSDisabledHWS", "eErrorDeviceNotCreated",
        "eErrorNoSupportedAdapterFound", "eErrorAdapterNotSupported", "eErrorNoPlugins", "eErrorVulkanAPI", "eErrorDXGIAPI",
        "eErrorD3DAPI", "eErrorNRDAPI", "eErrorNVAPI", "eErrorReflexAPI", "eErrorNGXFailed", "eErrorJSONParsing",
        "eErrorMissingProxy", "eErrorMissingResourceState", "eErrorInvalidIntegration", "eErrorMissingInputParameter",
        "eErrorNotInitialized", "eErrorComputeFailed", "eErrorInitNotCalled", "eErrorExceptionHandler", "eErrorInvalidParameter",
        "eErrorMissingConstants", "eErrorDuplicatedConstants", "eErrorMissingOrInvalidAPI", "eErrorCommonConstantsMissing",
        "eErrorUnsupportedInterface", "eErrorFeatureMissing", "eErrorFeatureNotSupported", "eErrorFeatureMissingHooks",
        "eErrorFeatureFailedToLoad", "eErrorFeatureWrongPriority", "eErrorFeatureMissingDependency",
        "eErrorFeatureManagerInvalidState", "eErrorInvalidState", "eWarnOutOfVRAM",
    ];

    // sl::FeatureRequirementFlags (sl_core_types.h), by bit: the vendor's own names, shown as they are.
    private static readonly string[] _requirementFlags =
    [
        "eD3D11Supported", "eD3D12Supported", "eVulkanSupported", "eVSyncOffRequired", "eHardwareSchedulingRequired",
    ];

    // FfxApiDispatchFramegenerationFlags (ffx_framegeneration.h), by bit: the vendor's own names, shown as they are.
    private static readonly string[] _frameGenerationFlags =
    [
        "DRAW_DEBUG_TEAR_LINES", "DRAW_DEBUG_RESET_INDICATORS", "DRAW_DEBUG_VIEW", "NO_SWAPCHAIN_CONTEXT_NOTIFY", "DRAW_DEBUG_PACING_LINES",
    ];

    /// <summary>
    /// Every word a fact made here may carry for the App to translate (<c>AdvToken_*</c>): the three states of a
    /// switch seen over a session, the flags of the dispatch that have a name, "none", and what a field of view
    /// that cannot be radians is said to be (D128). A flag bit nobody named is <c>bit_N</c> and is shown as it is.
    /// </summary>
    public static IReadOnlyList<string> Tokens { get; } = ["on", "off", "on_and_off", "none", _notRadians, .. _flagTokens, .. _latencySpans];

    /// <summary>The seven spans of a frame, as the last part of a series' name; the App translates each (<c>AdvToken_*</c>).</summary>
    public static IReadOnlyList<string> LatencySpans => _latencySpans;

    private InGameAdvanced? _last;
    private FlLatencyState _latencyState;
    private bool _latencyFrames;
    private uint _latencySlowestUs;
    private uint _memoryQueries;
    private FactKey _key;
    private uint _builtResets;
    private FlAdvancedValid _everValid;
    private uint _featureAnswers;

    /// <summary>The session facts as of the last publication taken; empty until the game made a call they are read from.</summary>
    public IReadOnlyList<AdvancedFact> Facts { get; private set; } = [];

    /// <summary>True once a publication carried anything the game said.</summary>
    public bool SawAnything { get; private set; }

    /// <summary>What a session stores as its in-game state: <paramref name="hooked"/> is whether it was measured inside the game.</summary>
    public AdvancedInGameStatus Status(bool hooked) =>
        !hooked ? AdvancedInGameStatus.NotHooked : SawAnything ? AdvancedInGameStatus.Reading : AdvancedInGameStatus.NothingSeen;

    /// <summary>
    /// What became of the driver's latency report (train C), for a session measured inside the game: null when it
    /// was not, and when the capture side said nothing about it - a game it publishes no Advanced metrics for.
    /// "Reported frames" wins over an error or a stop that came later: the series are there up to it, and the
    /// stops say themselves.
    /// </summary>
    public AdvancedDriverRead? DriverRead(bool hooked)
    {
        if (!hooked)
        {
            return null;
        }

        return _latencyState switch
        {
            FlLatencyState.NoDevice => AdvancedDriverRead.NoDirect3D12,
            FlLatencyState.NoModule => AdvancedDriverRead.NoModule,
            FlLatencyState.Refused => AdvancedDriverRead.Refused,
            FlLatencyState.NoGetter => AdvancedDriverRead.NoGetter,
            FlLatencyState.Reading => _latencyFrames ? AdvancedDriverRead.Read : AdvancedDriverRead.NothingReported,
            FlLatencyState.Error => _latencyFrames ? AdvancedDriverRead.Read : AdvancedDriverRead.Error,
            FlLatencyState.Slow => AdvancedDriverRead.Slow,
            FlLatencyState.Faulted => AdvancedDriverRead.Faulted,
            FlLatencyState.Stalled => AdvancedDriverRead.Stalled,
            _ => null,
        };
    }

    /// <summary>The slowest call into the driver the publications carried, in microseconds; null while none was made.</summary>
    public int? DriverSlowestUs => _latencySlowestUs == 0 ? null : (int)_latencySlowestUs;

    /// <summary>
    /// What became of the entries read from one call hooked only for Advanced metrics (0.1.0-beta.43):
    /// <paramref name="group"/> is the bit a publication carries once the call was read, <paramref name="hookedIn"/>
    /// whether the capture side said its hook was in. Read wins over the hook's bit: a publication is the proof.
    /// </summary>
    public AdvancedInGameCall Call(FlAdvancedValid group, bool hookedIn) =>
        (_everValid & group) != FlAdvancedValid.None ? AdvancedInGameCall.Read : hookedIn ? AdvancedInGameCall.NeverCalled : AdvancedInGameCall.NotHooked;

    /// <summary>
    /// The same for one of the two questions a game asks Streamline about a feature (0.1.0-beta.44; one bit of the
    /// mask covers both, so the question is told by what the slots carry): <paramref name="question"/> is
    /// <see cref="FlAdvancedState.FeatureVersionAnswered"/> or <see cref="FlAdvancedState.FeatureRequirementsAnswered"/>.
    /// A game asks while it starts, so <see cref="AdvancedInGameCall.NeverCalled"/> here is the usual answer of a
    /// session attached later, and the App words it that way. Since 0.1.0-beta.45 <paramref name="question"/> may be
    /// any bits of a slot's word 1 that one call writes: the answer bit of <c>slIsFeatureSupported</c> or
    /// <c>slIsFeatureLoaded</c>, the two switch bits of <c>slSetFeatureLoaded</c>, a count's four bits.
    /// </summary>
    public AdvancedInGameCall FeatureCall(uint question, bool hookedIn) =>
        (_featureAnswers & question) != 0 ? AdvancedInGameCall.Read : hookedIn ? AdvancedInGameCall.NeverCalled : AdvancedInGameCall.NotHooked;

    /// <summary>What a word 1 carries when the game switched a feature through <c>slSetFeatureLoaded</c>, either way.</summary>
    public const uint FeatureSwitched = FlAdvancedState.FeatureSwitchedOff | FlAdvancedState.FeatureSwitchedOn;

    /// <summary>The four bits of a word 1 that count <c>slAllocateResources</c> calls.</summary>
    public const uint FeatureAllocated = FlAdvancedState.FeatureCountMask << FlAdvancedState.FeatureAllocShift;

    /// <summary>The four bits of a word 1 that count <c>slFreeResources</c> calls.</summary>
    public const uint FeatureFreed = FlAdvancedState.FeatureCountMask << FlAdvancedState.FeatureFreeShift;

    /// <summary>
    /// The session is ending: the facts are brought up to the last publication where they carry a COUNT. A count
    /// is not part of what decides a rebuild while the session runs - it would rewrite the session's context every
    /// second for a number that only matters at the end - so the one fact that is a count
    /// (<c>sl.constants.resets</c>) is as of the last rebuild until this is called.
    /// </summary>
    public void Conclude()
    {
        if (_last is not null && Facts.Count != 0 && _last.State.SlResets != _builtResets)
        {
            Facts = Build(_last);
            _builtResets = _last.State.SlResets;
        }
    }

    /// <summary>
    /// Takes a publication. Returns the readings it carries when it is one not taken before and it carries any;
    /// null otherwise — nothing published, the same publication as last time, or a second in which the game neither
    /// dispatched nor asked.
    /// </summary>
    public IReadOnlyList<AdvancedReading>? Take(InGameAdvanced? published)
    {
        if (published is null || ReferenceEquals(published, _last))
        {
            return null;
        }

        _last = published;
        ref readonly FlAdvancedState s = ref published.State;

        // The driver's latency report first, and whatever the mask says: where that read stands is published even
        // when nothing was read, and it is not one of "the game's calls".
        List<AdvancedReading>? readings = TakeLatency(published);
        if (s.ValidMask == FlAdvancedValid.None)
        {
            return readings;
        }

        // "The game's calls were read" is about the game's calls: the driver's report is not one of them.
        SawAnything |= (s.ValidMask & ~FlAdvancedValid.Latency) != FlAdvancedValid.None;
        _everValid |= s.ValidMask;
        if (s.ValidMask.HasFlag(FlAdvancedValid.SlFeatures))
        {
            for (int slot = 0; slot < FlAdvancedState.FeatureSlots && s.FeatureWordAt(slot, 0) != 0; slot++)
            {
                _featureAnswers |= s.FeatureWordAt(slot, 1);
            }
        }

        FactKey key = FactKey.Of(published);
        if (key != _key || Facts.Count == 0)
        {
            _key = key;
            Facts = Build(published);
            _builtResets = s.SlResets;
        }

        readings ??= new List<AdvancedReading>(2);
        // The pair is drained by each publication: the dispatches of the second before it. A sum that saturated its
        // word is not a mean of anything.
        if (s.ValidMask.HasFlag(FlAdvancedValid.FfxUpscale) && s.FfxFrameTimeCount != 0 && s.FfxFrameTimeSumUs != uint.MaxValue)
        {
            readings.Add(new AdvancedReading(FrameTimeSeries, s.FfxFrameTimeSumUs / (double)s.FfxFrameTimeCount / 1000.0));
        }

        // A point where the game asked, not a value held between two questions: the answer is of that moment.
        if (s.ValidMask.HasFlag(FlAdvancedValid.FfxMemory) && s.FfxMemoryQueries != _memoryQueries)
        {
            _memoryQueries = s.FfxMemoryQueries;
            readings.Add(new AdvancedReading(MemorySeries, s.FfxMemoryTotalBytes / _mebibyte));
        }

        return readings.Count == 0 ? null : readings;
    }

    // The ten latency words of a publication (07_IPC, region 5): the state and the slowest call are kept; the frames
    // and the sums are of ONE read and become one point a span - the mean over the frames that read summed. A span
    // some summed frame did not report has no point: without a sleep mode the driver reports no driver span at all.
    private List<AdvancedReading>? TakeLatency(InGameAdvanced published)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var state = (FlLatencyState)(s.LatState & FlAdvancedState.LatStateMask);
        if (state != FlLatencyState.None)
        {
            _latencyState = state;
        }

        _latencySlowestUs = Math.Max(_latencySlowestUs, s.LatReads >> 16);
        uint frames = s.LatFrames & 0xFFFFu;
        if (!s.ValidMask.HasFlag(FlAdvancedValid.Latency) || frames == 0)
        {
            return null;
        }

        _latencyFrames = true;
        uint[] sums = [s.LatSimulationSumUs, s.LatSubmitSumUs, s.LatPresentSumUs, s.LatDriverSumUs, s.LatOsQueueSumUs, s.LatGpuSumUs, s.LatTotalSumUs];
        var readings = new List<AdvancedReading>(sums.Length);
        for (int span = 0; span < sums.Length; span++)
        {
            if (sums[span] != FlAdvancedState.LatSumNone)
            {
                readings.Add(new AdvancedReading(LatencySeriesStem + _latencySpans[span], sums[span] / (double)frames));
            }
        }

        return readings;
    }

    private static List<AdvancedFact> Build(InGameAdvanced published)
    {
        var facts = new List<AdvancedFact>(11);
        if (published.State.ValidMask.HasFlag(FlAdvancedValid.DlssOptions))
        {
            AddDlss(published, facts);
        }

        if (published.State.ValidMask.HasFlag(FlAdvancedValid.FfxUpscale))
        {
            AddFfx(published, facts);
        }

        if (published.State.ValidMask.HasFlag(FlAdvancedValid.FfxFgConfig))
        {
            AddFrameGeneration(published, facts);
        }

        if (published.State.ValidMask.HasFlag(FlAdvancedValid.SlConstants))
        {
            AddStreamlineConstants(published, facts);
        }

        if (published.State.ValidMask.HasFlag(FlAdvancedValid.SlFeatures))
        {
            AddStreamlineFeatures(published, facts);
        }

        if ((published.State.ValidMask & (FlAdvancedValid.SlFeatures | FlAdvancedValid.SlShutdown)) != FlAdvancedValid.None)
        {
            AddStreamlineFeatureState(published, facts);
        }

        if (published.State.ValidMask.HasFlag(FlAdvancedValid.Latency))
        {
            AddDriverSleepStatus(published, facts);
        }

        return facts;
    }

    // The driver's sleep status (train C): its five switches as seen over the session, by the vendor's own names, and
    // the frame-generation multiplier it last gave. THE DRIVER'S WORDS: on the driver the probe ran against,
    // bLowLatencyMode read off for a device whose game had just switched low latency on (spike-notes §31) - so this
    // is never worded as "the game uses Reflex".
    private static void AddDriverSleepStatus(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        uint on = (s.LatState >> FlAdvancedState.LatSleepSeenOnShift) & FlAdvancedState.LatSleepBits;
        uint off = (s.LatState >> FlAdvancedState.LatSleepSeenOffShift) & FlAdvancedState.LatSleepBits;
        if ((on | off) == 0)
        {
            return;    // the report was read and the status never was
        }

        var rows = new List<AdvancedRow>(_sleepSwitches.Length + 1);
        for (int bit = 0; bit < _sleepSwitches.Length; bit++)
        {
            // Bit 0 seen off, bit 1 seen on: the two bits AddSwitch reads.
            AddSwitch(rows, _sleepSwitches[bit], ((off >> bit) & 1u) | (((on >> bit) & 1u) << 1));
        }

        uint multiplier = s.LatState >> FlAdvancedState.LatFgMultiplierShift;
        if (multiplier != 0)
        {
            rows.Add(new AdvancedRow("fgMultiplier", multiplier.ToString(CultureInfo.InvariantCulture)));
        }

        facts.Add(Fact("nvapi.reflex.sleep_status", AdvancedValue.OfRows(rows)));
    }

    // Whether Streamline says each feature is supported and loaded, and what the game did with it while the session
    // watched (0.1.0-beta.45): two facts, rows named as the versions' are. A refusal is the answer to "is it
    // supported", so the result is said by the vendor's own name whatever it is. A count that reached 15 stopped there.
    private static void AddStreamlineFeatureState(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var support = new List<AdvancedRow>(8);
        var lifecycle = new List<AdvancedRow>(8);
        for (int slot = 0; slot < FlAdvancedState.FeatureSlots && s.ValidMask.HasFlag(FlAdvancedValid.SlFeatures); slot++)
        {
            uint id = s.FeatureWordAt(slot, 0);
            if (id == 0)
            {
                break;
            }

            string feature = FeatureName(id - 1);
            uint word = s.FeatureWordAt(slot, 1);
            if ((word & FlAdvancedState.FeatureSupportAnswered) != 0)
            {
                support.Add(new AdvancedRow(feature + " · slIsFeatureSupported", ResultName((word >> FlAdvancedState.FeatureSupportResultShift) & 0xFFu)));
            }

            if ((word & FlAdvancedState.FeatureLoadedAnswered) != 0)
            {
                string loaded = (word & FlAdvancedState.FeatureLoadedValue) != 0 ? "on" : "off";
                support.Add(new AdvancedRow(feature + " · slIsFeatureLoaded", loaded, loaded));
            }

            // Bit 0 seen off, bit 1 seen on: the two bits AddSwitch reads.
            uint switched = ((word & FlAdvancedState.FeatureSwitchedOff) != 0 ? 1u : 0u) | ((word & FlAdvancedState.FeatureSwitchedOn) != 0 ? 2u : 0u);
            AddSwitch(lifecycle, feature + " · slSetFeatureLoaded", switched);
            AddCount(lifecycle, feature + " · slAllocateResources", (word >> FlAdvancedState.FeatureAllocShift) & FlAdvancedState.FeatureCountMask);
            AddCount(lifecycle, feature + " · slFreeResources", (word >> FlAdvancedState.FeatureFreeShift) & FlAdvancedState.FeatureCountMask);
        }

        if (s.ValidMask.HasFlag(FlAdvancedValid.SlShutdown))
        {
            lifecycle.Add(new AdvancedRow("slShutdown", s.SlShutdowns.ToString(CultureInfo.InvariantCulture)));
        }

        if (support.Count > 0)
        {
            facts.Add(Fact("sl.feature.support", AdvancedValue.OfRows(support)));
        }

        if (lifecycle.Count > 0)
        {
            facts.Add(Fact("sl.feature.lifecycle", AdvancedValue.OfRows(lifecycle)));
        }
    }

    // A four-bit count of calls: nothing where the game made none, the number, or "15 or more" in signs no language
    // needs translated - the count stops at 15.
    private static void AddCount(List<AdvancedRow> rows, string name, uint count)
    {
        if (count != 0)
        {
            rows.Add(new AdvancedRow(name, count >= FlAdvancedState.FeatureCountMask ? "≥ 15" : count.ToString(CultureInfo.InvariantCulture)));
        }
    }

    // sl::Result by the vendor's own name (sl_result.h): forty codes, the same ones in the same order at every
    // Streamline tag from v2.0.0 to v2.14.16 (17_HOOK_ENGINE, the row of slIsFeatureSupported). A number past the list
    // is said as a number; 0xFF is what the capture side writes for a code a byte could not carry.
    private static string ResultName(uint code) =>
        code < (uint)_results.Length ? _results[code] : string.Create(CultureInfo.InvariantCulture, $"result {code}");

    // What Streamline answered when the game asked about a feature (slGetFeatureVersion, slGetFeatureRequirements;
    // 0.1.0-beta.44): a slot per feature in the order the game first asked, the vendor's own names throughout. A
    // version of all zeros is the vendor's "none" - a feature that uses no NGX model has no versionNGX, a requirement
    // nobody stated has no row - except versionSL, which an answer always carries. The counts are said as they are:
    // 0 threads is "it creates none".
    private static void AddStreamlineFeatures(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var versions = new List<AdvancedRow>(8);
        var requirements = new List<AdvancedRow>(16);
        for (int slot = 0; slot < FlAdvancedState.FeatureSlots; slot++)
        {
            uint id = s.FeatureWordAt(slot, 0);
            if (id == 0)
            {
                break;
            }

            string feature = FeatureName(id - 1);
            uint answered = s.FeatureWordAt(slot, 1);
            if ((answered & FlAdvancedState.FeatureVersionAnswered) != 0)
            {
                versions.Add(new AdvancedRow(feature + " · versionSL", VersionText(published, slot, 2)));
                AddVersion(versions, feature, "versionNGX", published, slot, 5);
            }

            if ((answered & FlAdvancedState.FeatureRequirementsAnswered) != 0)
            {
                uint flags = s.FeatureWordAt(slot, 8);
                var names = new List<string>(5);
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((flags & (1u << bit)) != 0)
                    {
                        names.Add(bit < _requirementFlags.Length ? _requirementFlags[bit] : string.Create(CultureInfo.InvariantCulture, $"bit {bit}"));
                    }
                }

                requirements.Add(names.Count == 0 ? new AdvancedRow(feature + " · flags", "none", "none") : new AdvancedRow(feature + " · flags", string.Join(", ", names)));
                requirements.Add(new AdvancedRow(feature + " · maxNumCPUThreads", s.FeatureWordAt(slot, 9).ToString(CultureInfo.InvariantCulture)));
                requirements.Add(new AdvancedRow(feature + " · maxNumViewports", s.FeatureWordAt(slot, 10).ToString(CultureInfo.InvariantCulture)));
                requirements.Add(new AdvancedRow(feature + " · numRequiredTags", s.FeatureWordAt(slot, 11).ToString(CultureInfo.InvariantCulture)));
                AddVersion(requirements, feature, "osVersionDetected", published, slot, 12);
                AddVersion(requirements, feature, "osVersionRequired", published, slot, 15);
                AddVersion(requirements, feature, "driverVersionDetected", published, slot, 18);
                AddVersion(requirements, feature, "driverVersionRequired", published, slot, 21);
            }
        }

        if (versions.Count > 0)
        {
            facts.Add(Fact("sl.feature.versions", AdvancedValue.OfRows(versions)));
        }

        if (requirements.Count > 0)
        {
            facts.Add(Fact("sl.feature.requirements", AdvancedValue.OfRows(requirements)));
        }
    }

    private static string VersionText(InGameAdvanced published, int slot, int word)
    {
        ref readonly FlAdvancedState s = ref published.State;
        return string.Create(CultureInfo.InvariantCulture, $"{s.FeatureWordAt(slot, word)}.{s.FeatureWordAt(slot, word + 1)}.{s.FeatureWordAt(slot, word + 2)}");
    }

    private static void AddVersion(List<AdvancedRow> rows, string feature, string member, InGameAdvanced published, int slot, int word)
    {
        ref readonly FlAdvancedState s = ref published.State;
        if ((s.FeatureWordAt(slot, word) | s.FeatureWordAt(slot, word + 1) | s.FeatureWordAt(slot, word + 2)) != 0)
        {
            rows.Add(new AdvancedRow(feature + " · " + member, VersionText(published, slot, word)));
        }
    }

    // sl::Feature by the vendor's own constant (sl_core_types.h); an id this build has no name for is its number.
    private static string FeatureName(uint feature) => feature switch
    {
        0 => "kFeatureDLSS",
        1 => "kFeatureNRD_INVALID",
        2 => "kFeatureNIS",
        3 => "kFeatureReflex",
        4 => "kFeaturePCL",
        5 => "kFeatureDeepDVC",
        6 => "kFeatureLatewarp",
        1000 => "kFeatureDLSS_G",
        1001 => "kFeatureDLSS_RR",
        1002 => "kFeatureNvPerf",
        1003 => "kFeatureDirectSR",
        1004 => "kFeatureDLSS_NR",
        9999 => "kFeatureImGUI",
        _ => string.Create(CultureInfo.InvariantCulture, $"feature {feature}"),
    };

    // How the game configures FSR frame generation (ffxConfigure, 0.1.0-beta.43): three switches as seen over the
    // session, the flags any configure carried, and the last rectangle it generates in.
    private static void AddFrameGeneration(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var rows = new List<AdvancedRow>(5);
        AddSwitch(rows, "frameGenerationEnabled", s.FfxFgEnabledSeen);
        AddSwitch(rows, "allowAsyncWorkloads", s.FfxFgAsyncSeen);
        AddSwitch(rows, "onlyPresentGenerated", s.FfxFgOnlyGeneratedSeen);
        var names = new List<string>(4);
        for (int bit = 0; bit < 32; bit++)
        {
            if ((s.FfxFgFlagsSeen & (1u << bit)) != 0)
            {
                names.Add(bit < _frameGenerationFlags.Length ? _frameGenerationFlags[bit] : string.Create(CultureInfo.InvariantCulture, $"bit {bit}"));
            }
        }

        // This is reached only once a configure was read, so no flag seen is "the game set none" - said, as an
        // upscale that set none says it (0.1.0-beta.44: Lies of P's session of 2026-11-12 had no row at all).
        rows.Add(names.Count == 0 ? new AdvancedRow("flags", "none", "none") : new AdvancedRow("flags", string.Join(", ", names)));

        // left, top, width, height. A rectangle with no area is one the game did not give; four -1 is one the capture
        // side could not state (a side that is negative or past 32,767).
        int left = s.FfxFgRectAt(0);
        int top = s.FfxFgRectAt(1);
        int width = s.FfxFgRectAt(2);
        int height = s.FfxFgRectAt(3);
        if (width > 0 && height > 0 && left >= 0 && top >= 0)
        {
            rows.Add(new AdvancedRow("generationRect", string.Create(CultureInfo.InvariantCulture, $"{width} × {height} at {left}, {top}")));
        }

        if (rows.Count > 0)
        {
            facts.Add(Fact("ffx.framegen.config", AdvancedValue.OfRows(rows)));
        }
    }

    // What the game told Streamline about each frame (slSetConstants, 0.1.0-beta.43), for the first viewport that
    // called. FLT_MAX is Streamline's "not set" and never reaches here; a plane at infinity is FSR's convention (D119).
    private static void AddStreamlineConstants(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var jitter = new List<AdvancedRow>(2);
        AddUpTo(jitter, "jitterOffset.x", s.SlJitterMaxXBits);
        AddUpTo(jitter, "jitterOffset.y", s.SlJitterMaxYBits);
        if (jitter.Count > 0)
        {
            facts.Add(Fact("sl.constants.jitter", AdvancedValue.OfRows(jitter)));
        }

        // Bits in the vendor header's order: depthInverted 0, cameraMotionIncluded 1, motionVectors3D 2, reset 3,
        // orthographicProjection 4, motionVectorsDilated 5, motionVectorsJittered 6.
        var motion = new List<AdvancedRow>(6);
        AddNumber(motion, "mvecScale.x", s.SlMvecScaleXBits);
        AddNumber(motion, "mvecScale.y", s.SlMvecScaleYBits);
        AddSwitch(motion, "cameraMotionIncluded", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 1));
        AddSwitch(motion, "motionVectors3D", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 2));
        AddSwitch(motion, "motionVectorsDilated", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 5));
        AddSwitch(motion, "motionVectorsJittered", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 6));
        if (motion.Count > 0)
        {
            facts.Add(Fact("sl.constants.motion_vectors", AdvancedValue.OfRows(motion)));
        }

        var depth = new List<AdvancedRow>(3);
        AddNumber(depth, "cameraNear", s.SlNearBits);
        AddNumber(depth, "cameraFar", s.SlFarBits);
        AddSwitch(depth, "depthInverted", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 0));
        if (depth.Count > 0)
        {
            facts.Add(Fact("sl.constants.depth", AdvancedValue.OfRows(depth)));
        }

        var projection = new List<AdvancedRow>(4);
        AddFieldOfView(projection, "cameraFOV", s.SlFovMinBits, s.SlFovMaxBits);
        AddNumber(projection, "cameraAspectRatio", s.SlAspectBits);
        AddSwitch(projection, "orthographicProjection", SwitchOf(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse, 4));
        if (projection.Count > 0)
        {
            facts.Add(Fact("sl.constants.projection", AdvancedValue.OfRows(projection)));
        }

        // A count, of the first viewport's calls: 0 is "the game never reset the history", which is a fact.
        facts.Add(Fact("sl.constants.resets", AdvancedValue.Of(s.SlResets)));
    }

    // The two bits a switch seen over a session leaves: bit 0 seen off, bit 1 seen on.
    private static uint SwitchOf(uint seenTrue, uint seenFalse, int bit) =>
        ((seenFalse >> bit) & 1u) | (((seenTrue >> bit) & 1u) << 1);

    private static void AddSwitch(List<AdvancedRow> rows, string member, uint seen)
    {
        string? state = (seen & 3u) switch { 1u => "off", 2u => "on", 3u => "on_and_off", _ => null };
        if (state is not null)
        {
            rows.Add(new AdvancedRow(member, state, state));
        }
    }

    private static void AddDlss(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        if (s.DlssOutputWidth != 0 && s.DlssOutputHeight != 0)
        {
            facts.Add(Fact("sl.dlss.output_size", AdvancedValue.OfText(string.Create(CultureInfo.InvariantCulture, $"{s.DlssOutputWidth} × {s.DlssOutputHeight}"))));
        }

        var presets = new List<AdvancedRow>(_presetModes.Length);
        for (int mode = 0; mode < _presetModes.Length; mode++)
        {
            if (s.DlssPresetAt(mode) is byte plusOne and not 0)
            {
                presets.Add(new AdvancedRow(_presetModes[mode], PresetName(plusOne - 1)));
            }
        }

        if (presets.Count > 0)
        {
            facts.Add(Fact("sl.dlss.presets", AdvancedValue.OfRows(presets)));
        }

        AddFlag(facts, "sl.dlss.hdr_input", s.DlssColorBuffersHdr);
        AddFlag(facts, "sl.dlss.auto_exposure", s.DlssAutoExposure);
        AddFlag(facts, "sl.dlss.alpha_upscaling", s.DlssAlphaUpscaling);

        var exposure = new List<AdvancedRow>(2);
        AddNumber(exposure, "preExposure", s.DlssPreExposureBits);
        AddNumber(exposure, "exposureScale", s.DlssExposureScaleBits);
        if (exposure.Count > 0)
        {
            facts.Add(Fact("sl.dlss.exposure", AdvancedValue.OfRows(exposure)));
        }
    }

    private static void AddFfx(InGameAdvanced published, List<AdvancedFact> facts)
    {
        ref readonly FlAdvancedState s = ref published.State;
        var sharpening = new List<AdvancedRow>(2);
        string? seen = (s.FfxSharpenSeen & 3u) switch { 1u => "off", 2u => "on", 3u => "on_and_off", _ => null };
        if (seen is not null)
        {
            sharpening.Add(new AdvancedRow("enableSharpening", seen, seen));
        }

        AddRange(sharpening, "sharpness", s.FfxSharpnessMinBits, s.FfxSharpnessMaxBits);
        if (sharpening.Count > 0)
        {
            facts.Add(Fact("ffx.upscale.sharpening", AdvancedValue.OfRows(sharpening)));
        }

        // The largest magnitude on each axis: a jitter changes every frame, and its size is what a session can say of it.
        var jitter = new List<AdvancedRow>(2);
        AddUpTo(jitter, "jitterOffset.x", s.FfxJitterMaxXBits);
        AddUpTo(jitter, "jitterOffset.y", s.FfxJitterMaxYBits);
        if (jitter.Count > 0)
        {
            facts.Add(Fact("ffx.upscale.jitter", AdvancedValue.OfRows(jitter)));
        }

        if (Range(s.FfxPreExposureMinBits, s.FfxPreExposureMaxBits) is string preExposure)
        {
            facts.Add(Fact("ffx.upscale.exposure", AdvancedValue.OfText(preExposure)));
        }

        var depth = new List<AdvancedRow>(3);
        AddDepthPlane(depth, "cameraNear", s.FfxNearBits);
        AddDepthPlane(depth, "cameraFar", s.FfxFarBits);
        AddFieldOfView(depth, "cameraFovAngleVertical", s.FfxFovMinBits, s.FfxFovMaxBits);
        if (depth.Count > 0)
        {
            facts.Add(Fact("ffx.upscale.depth", AdvancedValue.OfRows(depth)));
        }

        var flags = new List<string>(4);
        for (int bit = 0; bit < 32; bit++)
        {
            if ((s.FfxFlagsSeen & (1u << bit)) != 0)
            {
                flags.Add(bit < _flagTokens.Length ? _flagTokens[bit] : string.Create(CultureInfo.InvariantCulture, $"bit_{bit}"));
            }
        }

        facts.Add(Fact("ffx.upscale.flags", AdvancedValue.OfTokens(flags.Count == 0 ? ["none"] : flags)));
    }

    private static AdvancedFact Fact(string id, AdvancedValue value) => new(id, Instance: null, value);

    private static void AddFlag(List<AdvancedFact> facts, string id, byte plusOne)
    {
        // The vendor's Boolean plus one: 1 is its false, 2 its true; anything else was not read.
        if (plusOne is 1 or 2)
        {
            facts.Add(Fact(id, AdvancedValue.Of(plusOne == 2)));
        }
    }

    // A depth plane of FSR's: a number, or the plane at infinity (owner decision D119) - which Number, rightly for every
    // other field, takes for no value at all.
    private static void AddDepthPlane(List<AdvancedRow> rows, string member, uint bits)
    {
        if (bits == FlAdvancedState.FloatInfinite)
        {
            rows.Add(new AdvancedRow(member, "∞"));
            return;
        }

        AddNumber(rows, member, bits);
    }

    private static void AddNumber(List<AdvancedRow> rows, string member, uint bits)
    {
        if (Number(bits) is double value)
        {
            rows.Add(new AdvancedRow(member, Text(value)));
        }
    }

    private static void AddUpTo(List<AdvancedRow> rows, string member, uint bits)
    {
        if (Number(bits) is double value)
        {
            rows.Add(new AdvancedRow(member, "≤ " + Text(value)));
        }
    }

    private static void AddRange(List<AdvancedRow> rows, string member, uint minBits, uint maxBits)
    {
        if (Range(minBits, maxBits) is string range)
        {
            rows.Add(new AdvancedRow(member, range));
        }
    }

    // A field of view as the game gave it: a range, lowest to highest - never a series, which would say when the
    // player aimed (D114). Both vendors' headers say radians, and a value over pi cannot be one: Cyberpunk 2077 gives
    // Streamline 51 to 90 (the owner's sessions of 2026-11-12, where this printed "51.0055 - 90 rad (2922.4 deg ...)").
    // Such a value is said as it was given, and a second row says what it is not. It is never converted: which unit
    // the game meant is this project's guess, and the row would then carry the guess as the game's word (D128).
    private static void AddFieldOfView(List<AdvancedRow> rows, string member, uint minBits, uint maxBits)
    {
        if (Number(minBits) is not double low || Number(maxBits) is not double high)
        {
            return;
        }

        // One value or two: told by the bits, which is what "the same float" means (and what csharpsquid:S1244 asks).
        bool one = minBits == maxBits;
        if (high > Math.PI)
        {
            rows.Add(new AdvancedRow(member, one ? Text(low) : Text(low) + " – " + Text(high)));
            rows.Add(new AdvancedRow(member + " unit", _notRadians, _notRadians));
            return;
        }

        rows.Add(new AdvancedRow(member, one
            ? string.Create(CultureInfo.InvariantCulture, $"{low:0.####} rad ({Degrees(low):0.#}°)")
            : string.Create(CultureInfo.InvariantCulture, $"{low:0.####} – {high:0.####} rad ({Degrees(low):0.#}° – {Degrees(high):0.#}°)")));
    }

    private static string? Range(uint minBits, uint maxBits) =>
        Number(minBits) is double low && Number(maxBits) is double high
            ? minBits == maxBits ? Text(low) : Text(low) + " – " + Text(high)
            : null;

    private static double? Number(uint bits)
    {
        if (bits == FlAdvancedState.FloatNone)
        {
            return null;
        }

        float value = BitConverter.UInt32BitsToSingle(bits);
        return float.IsFinite(value) ? value : null;
    }

    // As short as tells the value: a float's 0.35 is 0.3499999940395355 as a double.
    private static string Text(double value) => ((float)value).ToString("0.####", CultureInfo.InvariantCulture);

    private static double Degrees(double radians) => radians * 180.0 / Math.PI;

    // sl::DLSSPreset: 0 is the vendor's "default"; the letters start at 1.
    private static string PresetName(int preset) => preset switch
    {
        0 => "Default",
        >= 1 and <= 26 => ((char)('A' + preset - 1)).ToString(),
        _ => preset.ToString(CultureInfo.InvariantCulture),
    };

    private static ulong Pair(uint low, uint high) => low | ((ulong)high << 32);

    private static ulong Pair(int low, int high) => Pair(unchecked((uint)low), unchecked((uint)high));

    /// <summary>
    /// The fields the facts are made of, and nothing that moves every second: no count, no frame time, no clock. The
    /// one fact that IS a count - the resets told to Streamline - is brought up to date by <see cref="Conclude"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct FactKey(
        FlAdvancedValid Valid,
        uint OutputWidth,
        uint OutputHeight,
        uint DlssPreExposure,
        uint DlssExposureScale,
        ulong Presets,
        uint Booleans,
        uint SharpenSeen,
        uint SharpnessMin,
        uint SharpnessMax,
        uint JitterX,
        uint JitterY,
        uint PreExposureMin,
        uint PreExposureMax,
        uint Near,
        uint Far,
        uint FovMin,
        uint FovMax,
        uint Flags,
        ulong FrameGenerationSwitches,
        uint FrameGenerationFlags,
        ulong FrameGenerationRectLow,
        ulong FrameGenerationRectHigh,
        ulong SlJitter,
        ulong SlMvecScale,
        ulong SlDepth,
        ulong SlFov,
        uint SlAspect,
        ulong SlFlags,
        ulong SlFeatures,
        uint DriverSleep)
    {
        public static FactKey Of(InGameAdvanced published)
        {
            ref readonly FlAdvancedState s = ref published.State;
            ulong presets = 0;
            for (int mode = 0; mode < 6; mode++)
            {
                presets |= (ulong)s.DlssPresetAt(mode) << (8 * mode);
            }

            return new FactKey(
                s.ValidMask & (FlAdvancedValid.DlssOptions | FlAdvancedValid.FfxUpscale | FlAdvancedValid.FfxFgConfig | FlAdvancedValid.SlConstants | FlAdvancedValid.SlFeatures | FlAdvancedValid.SlShutdown),
                s.DlssOutputWidth,
                s.DlssOutputHeight,
                s.DlssPreExposureBits,
                s.DlssExposureScaleBits,
                presets,
                s.DlssColorBuffersHdr | ((uint)s.DlssAutoExposure << 8) | ((uint)s.DlssAlphaUpscaling << 16),
                s.FfxSharpenSeen,
                s.FfxSharpnessMinBits,
                s.FfxSharpnessMaxBits,
                s.FfxJitterMaxXBits,
                s.FfxJitterMaxYBits,
                s.FfxPreExposureMinBits,
                s.FfxPreExposureMaxBits,
                s.FfxNearBits,
                s.FfxFarBits,
                s.FfxFovMinBits,
                s.FfxFovMaxBits,
                s.FfxFlagsSeen,
                s.FfxFgEnabledSeen | ((ulong)s.FfxFgAsyncSeen << 8) | ((ulong)s.FfxFgOnlyGeneratedSeen << 16),
                s.FfxFgFlagsSeen,
                Pair(s.FfxFgRectAt(0), s.FfxFgRectAt(1)),
                Pair(s.FfxFgRectAt(2), s.FfxFgRectAt(3)),
                Pair(s.SlJitterMaxXBits, s.SlJitterMaxYBits),
                Pair(s.SlMvecScaleXBits, s.SlMvecScaleYBits),
                Pair(s.SlNearBits, s.SlFarBits),
                Pair(s.SlFovMinBits, s.SlFovMaxBits),
                s.SlAspectBits,
                Pair(s.SlFlagsSeenTrue, s.SlFlagsSeenFalse),
                FeaturesHash(published),
                // The driver's sleep status as seen and its multiplier: what the one latency fact is made of.
                s.ValidMask.HasFlag(FlAdvancedValid.Latency) ? s.LatState >> FlAdvancedState.LatSleepSeenOnShift : 0u);
        }

        // The feature slots as one number (FNV-1a over the words of the slots in use): an answer that arrives, or one
        // that changes, makes the facts new; 0 where the group was not published.
        private static ulong FeaturesHash(InGameAdvanced published)
        {
            ref readonly FlAdvancedState s = ref published.State;
            if ((s.ValidMask & (FlAdvancedValid.SlFeatures | FlAdvancedValid.SlShutdown)) == FlAdvancedValid.None)
            {
                return 0;
            }

            // The shutdowns first (0.1.0-beta.45): a count, but one that moves a handful of times in a session at most.
            ulong hash = (14695981039346656037UL ^ s.SlShutdowns) * 1099511628211UL;
            for (int slot = 0; slot < FlAdvancedState.FeatureSlots && s.ValidMask.HasFlag(FlAdvancedValid.SlFeatures) && s.FeatureWordAt(slot, 0) != 0; slot++)
            {
                for (int word = 0; word < FlAdvancedState.FeatureWords; word++)
                {
                    hash = (hash ^ s.FeatureWordAt(slot, word)) * 1099511628211UL;
                }
            }

            return hash;
        }
    }
}
