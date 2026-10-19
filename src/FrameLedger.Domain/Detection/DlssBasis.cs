// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>What established a <see cref="DlssGeneration"/>.</summary>
public enum DlssBasis
{
    /// <summary>Nothing. The zero value, so a default is not a claim.</summary>
    None = 0,

    /// <summary>The render preset the NVIDIA driver reported applied.</summary>
    Preset = 1,

    /// <summary>A measured DLSS Frame Generation state's factor.</summary>
    FrameGenerationFactor = 2,

    /// <summary>A loaded library's major version.</summary>
    LibraryVersion = 3,
}
