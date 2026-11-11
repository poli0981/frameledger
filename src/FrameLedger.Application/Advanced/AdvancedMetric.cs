// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// One row of <see cref="AdvancedMetricCatalog"/>: a number, a fact or a family of sensors that a vendor interface
/// makes available, with the scope it belongs to and where it can be read from.
/// </summary>
/// <param name="Id">Lower case, dotted, starting with the source's prefix. It is on disk once a release stores it, and then never changes.</param>
/// <param name="Scope">The machine or one session.</param>
/// <param name="Source">The vendor interface.</param>
/// <param name="Reach">Where it can be read from, or why it is not read.</param>
/// <param name="Unit">What its value is.</param>
/// <param name="Group">The heading it is shown under.</param>
/// <param name="Summary">
/// What it is, in English, for <c>docs/18_GPU_VENDOR_APIS.md</c> §The inventory — the table there is held to this
/// text by a test. The App shows the translated string under <see cref="LabelKey"/>, never this.
/// </param>
public sealed record AdvancedMetric(
    string Id,
    AdvancedScope Scope,
    AdvancedSource Source,
    AdvancedReach Reach,
    AdvancedUnit Unit,
    AdvancedGroup Group,
    string Summary)
{
    /// <summary>The prefix of every advanced series in <c>sensor_blobs</c> (D102): a reader tells them from the fixed series by it.</summary>
    public const string SeriesPrefix = "adv.";

    /// <summary>
    /// True when the entry is not one value but as many as the hardware has — a temperature per core, a fan per
    /// header, a set of facts per display. Each instance is named by <see cref="AdvancedMetricCatalog.InstanceName"/>
    /// and labelled by what the hardware calls it.
    /// </summary>
    public bool IsFamily { get; init; }

    /// <summary>True when a build with the option on reads it: every other reach is a reason it reads N/A.</summary>
    public bool IsRead => Reach is AdvancedReach.Agent or AdvancedReach.AgentElevated or AdvancedReach.Game;

    /// <summary>The name of its series in <c>sensor_blobs</c> — for a family, the stem of its instances' names; null when it is not a series.</summary>
    public string? SeriesName => Scope == AdvancedScope.Series ? SeriesPrefix + Id : null;

    /// <summary>The key of its translated label in the App's resources.</summary>
    public string LabelKey => "Adv_" + Id.Replace('.', '_');
}
