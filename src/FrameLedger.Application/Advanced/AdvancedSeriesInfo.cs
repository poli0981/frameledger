// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// One line of a session's series directory: a series that is one member of a family, the catalog entry it is a
/// member of, and what the hardware calls the sensor.
/// </summary>
/// <param name="Series">The series' name in <c>sensor_blobs</c> (<see cref="AdvancedMetricCatalog.InstanceName"/>).</param>
/// <param name="MetricId">The family's <see cref="AdvancedMetric.Id"/>.</param>
/// <param name="Label">
/// The hardware's own words — <c>Intel Core i7-14700K - CPU Core #3</c> — shown as they are. A family's members
/// differ from PC to PC, so their names cannot be translated strings; the family's name, which is, stands above them.
/// </param>
public sealed record AdvancedSeriesInfo(string Series, string MetricId, string Label);
