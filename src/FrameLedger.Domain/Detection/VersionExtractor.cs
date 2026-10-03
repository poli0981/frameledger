// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>How to recover a version string once a rule matched.</summary>
public sealed record VersionExtractor
{
    /// <summary>Which extraction to run.</summary>
    public required VersionExtractorType Type { get; init; }

    /// <summary>Regex source, for the regex extractors.</summary>
    public string? Value { get; init; }

    /// <summary>Sibling file to read, for <see cref="VersionExtractorType.PeFileVersion"/>.</summary>
    public string? From { get; init; }

    /// <summary>Dotted manifest path, for <see cref="VersionExtractorType.ManifestField"/>.</summary>
    public string? Field { get; init; }
}
