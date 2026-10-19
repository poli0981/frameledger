// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// Which DLSS generation a session's evidence establishes (beta.24, owner decision D64; <c>05_DETECTION</c> §DLSS
/// generation) — and on what — or nothing. Evidence-bound: a label is given only from something the session observed,
/// strongest first; a library version is shown as a version and names a generation only where its major number does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table, and where each row comes from.</b> A render preset the NVIDIA driver reports applied to super
/// resolution: L or M are DLSS 4.5's second-generation transformer (added with DLL 310.5.0), J or K DLSS 4's transformer;
/// A–F are the CNN model and name no generation. A DLSS Frame Generation state measured at ×5 or more is DLSS 4.5's
/// multi-frame generation, ×3 or ×4 DLSS 4's. A super-resolution library 2.x is DLSS 2; 3.x, or a frame-generation
/// library 1.x–3.x, is DLSS 3. A 310.x library alone names nothing: DLSS 4 and 4.5 ship the same major number, and the
/// model it runs is the preset's to say. Never a default preset inferred from a version.
/// </para>
/// <para>
/// DLSS 5's neural rendering is not a generation of these features and is not in this table: a session states only that
/// its runtime was loaded.
/// </para>
/// </remarks>
/// <param name="Kind">The generation established.</param>
/// <param name="Basis">What established it.</param>
public sealed record DlssGeneration(DlssGenerationKind Kind, DlssBasis Basis)
{
    /// <summary>The generation the evidence establishes, or null when it establishes none.</summary>
    public static DlssGeneration? Of(DlssEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Preset is 12 or 13)
        {
            return new DlssGeneration(DlssGenerationKind.Dlss45, DlssBasis.Preset);
        }

        if (evidence.LargestFrameGenerationFactor is >= 4.5)
        {
            return new DlssGeneration(DlssGenerationKind.Dlss45, DlssBasis.FrameGenerationFactor);
        }

        if (evidence.Preset is 10 or 11)
        {
            return new DlssGeneration(DlssGenerationKind.Dlss4, DlssBasis.Preset);
        }

        if (evidence.LargestFrameGenerationFactor is >= 2.5)
        {
            return new DlssGeneration(DlssGenerationKind.Dlss4, DlssBasis.FrameGenerationFactor);
        }

        if (evidence.SuperResolution is { Major: 2 })
        {
            return new DlssGeneration(DlssGenerationKind.Dlss2, DlssBasis.LibraryVersion);
        }

        return evidence.SuperResolution is { Major: 3 } || evidence.FrameGeneration is { Major: >= 1 and <= 3 }
            ? new DlssGeneration(DlssGenerationKind.Dlss3, DlssBasis.LibraryVersion)
            : null;
    }

    /// <summary>A render preset as its letter (1 = A … 26 = Z), or null.</summary>
    public static char? PresetLetter(uint? preset) => preset is >= 1 and <= 26 ? (char)('A' + (int)preset.Value - 1) : null;

    /// <summary>The model a render preset selects, or null for one this table does not place.</summary>
    public static DlssModel? ModelOf(uint? preset) => preset switch
    {
        >= 1 and <= 6 => DlssModel.Cnn,
        10 or 11 => DlssModel.Transformer,
        12 or 13 => DlssModel.SecondGenerationTransformer,
        _ => null,
    };
}
