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

    private const double _mebibyte = 1024.0 * 1024.0;

    private static readonly string[] _presetModes = ["DLAA", "Quality", "Balanced", "Performance", "Ultra Performance", "Ultra Quality"];

    // FfxApiDispatchUpscaleFlags (ffx_upscale.h), by bit.
    private static readonly string[] _flagTokens = ["draw_debug_view", "non_linear_color_srgb", "non_linear_color_pq"];

    /// <summary>
    /// Every word a fact made here may carry for the App to translate (<c>AdvToken_*</c>): the three states of a
    /// switch seen over a session, the flags of the dispatch that have a name, and "none". A flag bit nobody named
    /// is <c>bit_N</c> and is shown as it is.
    /// </summary>
    public static IReadOnlyList<string> Tokens { get; } = ["on", "off", "on_and_off", "none", .. _flagTokens];

    private InGameAdvanced? _last;
    private uint _memoryQueries;
    private FactKey _key;

    /// <summary>The session facts as of the last publication taken; empty until the game made a call they are read from.</summary>
    public IReadOnlyList<AdvancedFact> Facts { get; private set; } = [];

    /// <summary>True once a publication carried anything the game said.</summary>
    public bool SawAnything { get; private set; }

    /// <summary>What a session stores as its in-game state: <paramref name="hooked"/> is whether it was measured inside the game.</summary>
    public AdvancedInGameStatus Status(bool hooked) =>
        !hooked ? AdvancedInGameStatus.NotHooked : SawAnything ? AdvancedInGameStatus.Reading : AdvancedInGameStatus.NothingSeen;

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
        if (s.ValidMask == FlAdvancedValid.None)
        {
            return null;
        }

        SawAnything = true;
        FactKey key = FactKey.Of(published);
        if (key != _key || Facts.Count == 0)
        {
            _key = key;
            Facts = Build(published);
        }

        var readings = new List<AdvancedReading>(2);
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

        return facts;
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
        AddNumber(depth, "cameraNear", s.FfxNearBits);
        AddNumber(depth, "cameraFar", s.FfxFarBits);
        // A range, lowest to highest - never a series, which would say when the player aimed (D114).
        if (Number(s.FfxFovMinBits) is double low && Number(s.FfxFovMaxBits) is double high)
        {
            // One value or two: told by the bits, which is what "the same float" means (and what csharpsquid:S1244 asks).
            depth.Add(new AdvancedRow("cameraFovAngleVertical", s.FfxFovMinBits == s.FfxFovMaxBits
                ? string.Create(CultureInfo.InvariantCulture, $"{low:0.####} rad ({Degrees(low):0.#}°)")
                : string.Create(CultureInfo.InvariantCulture, $"{low:0.####} – {high:0.####} rad ({Degrees(low):0.#}° – {Degrees(high):0.#}°)")));
        }

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

    /// <summary>The fields the facts are made of, and nothing that moves every second: no count, no frame time, no clock.</summary>
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
        uint Flags)
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
                s.ValidMask & (FlAdvancedValid.DlssOptions | FlAdvancedValid.FfxUpscale),
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
                s.FfxFlagsSeen);
        }
    }
}
