// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>An engine signature. Order in <see cref="DetectionRuleSet.Engines"/> is the precedence.</summary>
public sealed record EngineRule
{
    /// <summary>Stable id, also a database and <c>.resx</c> key.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>What identifies this engine on disk.</summary>
    public required SignalGroup Signals { get; init; }

    /// <summary>
    /// How to recover the version, or <c>null</c> for "this engine has no
    /// version extractor".
    /// </summary>
    /// <remarks>
    /// An explicit null is a statement; a missing key is a malformed file. The
    /// schema makes <c>version</c> required-and-nullable precisely so the two
    /// cannot be confused, and the reader refuses the latter rather than
    /// defaulting it.
    /// </remarks>
    public VersionExtractor? Version { get; init; }
}
