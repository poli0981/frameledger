// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>A DLSS generation, as <see cref="DlssGeneration"/> names one.</summary>
public enum DlssGenerationKind
{
    /// <summary>None established. The zero value, so a default is not a claim.</summary>
    None = 0,
    Dlss2 = 2,
    Dlss3 = 3,
    Dlss4 = 4,

    /// <summary>DLSS 4.5.</summary>
    Dlss45 = 45,
}
