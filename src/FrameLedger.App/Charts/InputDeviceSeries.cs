// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>One controller's series of a session (<c>input.dev.N.active</c>, <c>input.dev.N.battery</c>), a value a tick.</summary>
/// <param name="Index">The controller's number in the session, from 1 - <c>sessions.input_devices</c> names it the same.</param>
/// <param name="Active">The part of each tick's time in front the controller was in use (0..1); NaN while it was not connected.</param>
/// <param name="Battery">The battery's level on each tick, in its own scale; NaN where there was none. Null for a controller with no battery series.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the charts, as SensorSeries is")]
public sealed record InputDeviceSeries(int Index, float[] Active, float[]? Battery);
