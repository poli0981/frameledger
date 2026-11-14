// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>One mark on a session's charts: what happened to which controller, and when.</summary>
/// <param name="TimeS">Seconds from the session's start (the sensors' clock).</param>
/// <param name="Kind">What happened.</param>
/// <param name="Device">The controller's number in the session, from 1.</param>
public sealed record InputEvent(double TimeS, InputEventKind Kind, int Device);
