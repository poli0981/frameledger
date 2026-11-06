// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Detection;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Services;

/// <summary>
/// The DLSS a session ran, in words (beta.24, owner decision D64; <c>05_DETECTION</c> §DLSS generation): each DLSS
/// library's version and where it was loaded from, the model preset the NVIDIA driver reports, and a generation only
/// where the session shows one (<see cref="DlssGeneration"/>, strongest evidence first) — since beta.34 (D83) also the
/// NVIDIA App's Dynamic frame generation the driver reports applied, and (D84) DLSS 5 where its neural rendering was seen
/// running; a DLSS 5 runtime that was only loaded says so, and whether the Overlay looked.
/// </summary>
/// <remarks>Read from what the row stores; nothing here infers a generation from a version that does not name one.</remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class DlssText
{
    private const string _superResolutionFile = "nvngx_dlss.dll";
    private const string _rayReconstructionFile = "nvngx_dlssd.dll";
    private const string _frameGenerationFile = "nvngx_dlssg.dll";
    private const string _neuralRenderingFile = "nvngx_dlssnr.dll";
    private static readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    /// <summary>What the row observed about DLSS.</summary>
    public static DlssEvidence Evidence(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Dictionary<string, string?> modules = Modules(row.RuntimeModulesJson);
        return new DlssEvidence(VersionOf(modules, _superResolutionFile), VersionOf(modules, _frameGenerationFile), ReportedPreset(row), LargestFrameGenerationFactor(row),
            DynamicFrameGeneration(row), row.DlssNrPct);
    }

    /// <summary>The generation the row establishes, as a label with what it rests on (<c>DLSS 4.5 (preset L)</c>), or null.</summary>
    public static string? Label(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        DlssEvidence evidence = Evidence(row);
        return DlssGeneration.Of(evidence) is { } generation ? LabelOf(generation, evidence) : null;
    }

    /// <summary>
    /// The summary's DLSS line: the label first when one is established, then each library with its version, where it was
    /// loaded from and — for super resolution — the preset the driver reports, then DLSS 5's runtime when it was loaded —
    /// since beta.34 (D84) listed as a library too, and, where it is not the label, whether it was seen running.
    /// Null for a session with no DLSS library, no DLSS upscaler and no DLSS Frame Generation — and, since beta.27, where it
    /// would say only "DLSS": the header already names the upscaler. The NVIDIA App's override loads its libraries under
    /// names no census lists (Witcher 3, 2026-10-20: <c>160_E658700.bin</c>, which the loaded-libraries line prints), and an
    /// unreadable module list names none.
    /// </summary>
    public static string? Line(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Dictionary<string, string?> modules = Modules(row.RuntimeModulesJson);
        Dictionary<string, string?> sources = Modules(row.RuntimeModuleSourcesJson);
        bool any = modules.Keys.Any(static k => k.StartsWith("nvngx_dlss", StringComparison.OrdinalIgnoreCase))
            || Is(row.Upscaler, "dlss") || Is(row.UpscalerDriverReported, "dlss") || Is(row.FgMode, "dlssg");
        if (!any)
        {
            return null;
        }

        DlssEvidence evidence = Evidence(row);
        string? label = DlssGeneration.Of(evidence) is { } generation ? LabelOf(generation, evidence) : null;
        var parts = new List<string> { label ?? "DLSS" };
        AddLibrary(parts, modules, sources, _superResolutionFile, Strings.Dlss_Feature_SuperResolution, PresetText(evidence.Preset));
        AddLibrary(parts, modules, sources, _rayReconstructionFile, Strings.Dlss_Feature_RayReconstruction, detail: null);
        AddLibrary(parts, modules, sources, _frameGenerationFile, Strings.Dlss_Feature_FrameGeneration, detail: null);
        if (!modules.ContainsKey(_superResolutionFile) && PresetText(evidence.Preset) is { } preset)
        {
            parts.Add(preset);
        }

        AddLibrary(parts, modules, sources, _neuralRenderingFile, Strings.Dlss_Feature_NeuralRendering, detail: null);
        if (modules.ContainsKey(_neuralRenderingFile) && evidence.NeuralRenderingPct is not > 0)
        {
            // Loaded, and not the label: a writer that decodes it saw it evaluate nowhere (0 %), or nothing could tell
            // (null: an Overlay before beta.34, or a title that never went through Streamline) — never "off".
            parts.Add(evidence.NeuralRenderingPct is null ? Strings.Dlss_NeuralRendering_Loaded : Strings.Dlss_NeuralRendering_NotSeen);
        }

        return label is null && parts.Count == 1 ? null : string.Join(" · ", parts);
    }

    /// <summary>The CSV header's line, InvariantCulture: the generation and its basis, each DLSS library with its version and source, the preset.</summary>
    public static string ExportLine(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Dictionary<string, string?> modules = Modules(row.RuntimeModulesJson);
        Dictionary<string, string?> sources = Modules(row.RuntimeModuleSourcesJson);
        DlssEvidence evidence = Evidence(row);
        var parts = new List<string>();
        if (DlssGeneration.Of(evidence) is { } generation)
        {
            parts.Add("generation=" + KindText(generation.Kind) + " basis=" + BasisToken(generation.Basis));
        }

        foreach (string file in new[] { _superResolutionFile, _rayReconstructionFile, _frameGenerationFile, _neuralRenderingFile })
        {
            if (modules.TryGetValue(file, out string? version))
            {
                parts.Add(file + "=" + (version ?? "N/A") + (sources.TryGetValue(file, out string? source) && source is not null ? " " + source : string.Empty));
            }
        }

        if (DlssGeneration.PresetLetter(evidence.Preset) is char letter)
        {
            parts.Add("preset=" + letter.ToString(_inv));
        }

        if (row.DlssNrPct is double neural)
        {
            parts.Add("neural_rendering_pct=" + neural.ToString("0.0", _inv));
        }

        return parts.Count == 0 ? "N/A" : string.Join("; ", parts);
    }

    private static string LabelOf(DlssGeneration generation, DlssEvidence evidence) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Label_Format, KindText(generation.Kind), generation.Basis switch
        {
            DlssBasis.Preset => string.Format(CultureInfo.CurrentCulture, Strings.Nv_Preset_Format, DlssGeneration.PresetLetter(evidence.Preset)),
            DlssBasis.FrameGenerationFactor => string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Basis_Factor_Format, evidence.LargestFrameGenerationFactor),
            DlssBasis.DynamicFrameGeneration => Strings.Dlss_Basis_Dynamic,
            DlssBasis.NeuralRendering => string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Basis_NeuralRendering_Format, Formats.Share(evidence.NeuralRenderingPct)),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Basis_Library_Format, Short(evidence.SuperResolution is { Major: 2 or 3 } ? evidence.SuperResolution : evidence.FrameGeneration)),
        });

    private static string BasisToken(DlssBasis basis) => basis switch
    {
        DlssBasis.Preset => "preset",
        DlssBasis.FrameGenerationFactor => "frame_generation_factor",
        DlssBasis.DynamicFrameGeneration => "dynamic_frame_generation",
        DlssBasis.NeuralRendering => "neural_rendering",
        _ => "library_version",
    };

    private static string KindText(DlssGenerationKind kind) => kind switch
    {
        DlssGenerationKind.Dlss2 => "DLSS 2",
        DlssGenerationKind.Dlss3 => "DLSS 3",
        DlssGenerationKind.Dlss4 => "DLSS 4",
        DlssGenerationKind.Dlss45 => "DLSS 4.5",
        DlssGenerationKind.Dlss5 => "DLSS 5",
        _ => "DLSS",
    };

    private static string? PresetText(uint? preset) =>
        DlssGeneration.PresetLetter(preset) is char letter
            ? string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Preset_Format, letter, ModelText(DlssGeneration.ModelOf(preset)))
            : null;

    private static string ModelText(DlssModel? model) => model switch
    {
        DlssModel.Cnn => Strings.Dlss_Model_Cnn,
        DlssModel.Transformer => Strings.Dlss_Model_Transformer,
        DlssModel.SecondGenerationTransformer => Strings.Dlss_Model_SecondGenerationTransformer,
        _ => Strings.Dlss_Model_Unknown,
    };

    private static void AddLibrary(List<string> parts, Dictionary<string, string?> modules, Dictionary<string, string?> sources, string file, string feature, string? detail)
    {
        if (!modules.TryGetValue(file, out string? stated))
        {
            return;
        }

        string version = Short(Parse(stated)) ?? Strings.Common_NotAvailable;
        string text = string.Format(CultureInfo.CurrentCulture, Strings.Dlss_Library_Format, feature, version);
        string? from = sources.TryGetValue(file, out string? token) ? SourceText(RuntimeModuleSources.FromToken(token)) : null;
        parts.Add(string.Join(", ", new[] { text, from, detail }.Where(static p => p is not null)));
    }

    private static string? SourceText(RuntimeModuleSource source) => source switch
    {
        RuntimeModuleSource.Game => Strings.Dlss_From_Game,
        RuntimeModuleSource.GraphicsDriver => Strings.Dlss_From_GraphicsDriver,
        RuntimeModuleSource.Windows => Strings.Dlss_From_Windows,
        _ => null,
    };

    /// <summary>
    /// The super-resolution preset the driver reports applied, where super resolution ran in the session (a hook named
    /// DLSS, or the driver saw it created and evaluated): the word's PRESET bit set. Null otherwise — a preset the driver
    /// applied to a feature nobody saw run names the model of nothing.
    /// </summary>
    private static uint? ReportedPreset(SessionRow row)
    {
        bool ran = row.Upscaler is { Length: > 0 } and not "unknown" ? Is(row.Upscaler, "dlss") : Is(row.UpscalerDriverReported, "dlss");
        return ran && NgxDriverWords.Parse(row.NgxDriverWordsJson) is { Outcome: nameof(NgxProbeOutcome.Answered) } words
            && (Mask(words.Sr) & NgxOverrideFlags.Preset) != 0 && words.Preset is > 0
            ? words.Preset
            : null;
    }

    /// <summary>
    /// The NVIDIA App's Dynamic frame generation ran in the session (beta.34, D83): DLSS Frame Generation was counted, the
    /// game's driver profile sets <c>NGX_DLSSG_MODE</c> to Dynamic, and the driver's frame-generation word reports the mode
    /// override applied in the game's process (<see cref="NgxOverrideFlags.FgMode"/>). The driver's own mode number —
    /// 3 under Dynamic and 1 under Fixed on every stored session (<c>spike-notes</c> §26) — is not what is read: NVIDIA's
    /// header names the profile's values and not the report's. A profile that sets Dynamic the driver never applied, or a
    /// session where no DLSS Frame Generation was counted, is not evidence.
    /// </summary>
    private static bool DynamicFrameGeneration(SessionRow row) =>
        Is(row.FgMode, "dlssg")
        && DriverProfileRecord.Parse(row.DriverProfileJson) is { } profile && NvidiaOverrides.IsRead(profile)
        && profile.ValueOf(NvidiaDriverSettings.DlssgMode) == NvidiaDriverSettings.DlssgModeDynamic
        && NgxDriverWords.Parse(row.NgxDriverWordsJson) is { Outcome: nameof(NgxProbeOutcome.Answered) } words
        && (Mask(words.Fg) & NgxOverrideFlags.FgMode) != 0;

    /// <summary>The largest DLSS Frame Generation state the session measured: the mix's states, else its one factor.</summary>
    private static double? LargestFrameGenerationFactor(SessionRow row)
    {
        if (!Is(row.FgMode, "dlssg"))
        {
            return null;
        }

        FgMixRecord? mix = FgMixRecord.Parse(row.FgMixJson);
        double? largest = mix?.Rows.Where(static r => FgMixRecordRow.KindOf(r.Kind) == FgMixKind.Generated).Max(static r => r.Factor);
        return largest ?? row.FgFactor;
    }

    private static Dictionary<string, string?> Modules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, string>? parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString);
            return parsed is null
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : parsed.ToDictionary(static p => p.Key, static p => (string?)p.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Version? VersionOf(Dictionary<string, string?> modules, string file) =>
        modules.TryGetValue(file, out string? stated) ? Parse(stated) : null;

    /// <summary>A stated file version — <c>310,9,1,0</c> as some vendors stamp it, or <c>3.7.10.0</c> — as numbers.</summary>
    internal static Version? Parse(string? stated)
    {
        if (string.IsNullOrWhiteSpace(stated))
        {
            return null;
        }

        string[] numbers = stated.Replace(',', '.').Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int[] parts = [.. numbers.Take(4).Select(static n => int.TryParse(n, NumberStyles.None, _inv, out int v) ? v : -1)];
        return parts.Length >= 2 && parts.All(static p => p >= 0)
            ? parts.Length switch
            {
                2 => new Version(parts[0], parts[1]),
                3 => new Version(parts[0], parts[1], parts[2]),
                _ => new Version(parts[0], parts[1], parts[2], parts[3]),
            }
            : null;
    }

    /// <summary>A version without a trailing <c>.0</c> revision: <c>310.9.1</c>.</summary>
    private static string? Short(Version? version) => version is null ? null
        : version.Revision is 0 or -1 ? version.ToString(version.Build < 0 ? 2 : 3) : version.ToString();

    private static bool Is(string? token, string value) => string.Equals(token, value, StringComparison.Ordinal);

    private static ulong Mask(string? hex) =>
        hex is { Length: > 2 } && ulong.TryParse(hex.AsSpan(2), NumberStyles.HexNumber, _inv, out ulong value) ? value : 0;
}
