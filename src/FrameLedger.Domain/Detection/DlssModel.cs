// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>The super-resolution model a render preset selects (beta.24, D64).</summary>
public enum DlssModel
{
    /// <summary>Not placed. The zero value, so a default is not a claim.</summary>
    None = 0,

    /// <summary>Presets A–F: the convolutional model.</summary>
    Cnn = 1,

    /// <summary>Presets J and K: DLSS 4's transformer.</summary>
    Transformer = 2,

    /// <summary>Presets L and M: DLSS 4.5's second-generation transformer.</summary>
    SecondGenerationTransformer = 3,
}
