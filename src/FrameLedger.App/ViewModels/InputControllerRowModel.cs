// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One controller of a session in the summary's table (0.1.0-beta.50), from <c>sessions.input_devices</c>: what it was,
/// how it was connected, how long it was in use and connected, how often it connected and dropped, and its battery.
/// Never a serial number, an address or a device path - the ledger has none to show.
/// </summary>
/// <param name="Name">The model's name, or the maker's with "controller"; a pad a program presents says so.</param>
/// <param name="Connection">USB, Bluetooth, a wireless adapter, or unknown.</param>
/// <param name="InUse">The time it was in use while the game was in front.</param>
/// <param name="Connected">The time it was connected.</param>
/// <param name="Events">How many times it connected and dropped after the session's first reading.</param>
/// <param name="Battery">The state held longest and for how long, the first and last level, the drain - or why there is none.</param>
public sealed record InputControllerRowModel(string Name, string Connection, string InUse, string Connected, string Events, string Battery)
{
    /// <summary>The row in one line, as a screen reader reads it: each figure with the heading of its column.</summary>
    public override string ToString() => Services.SpokenText.Pair(Name, Services.SpokenText.List(
        Connection,
        Services.SpokenText.Pair(Strings.Input_Col_InUse, InUse),
        Services.SpokenText.Pair(Strings.Input_Col_Connected, Connected),
        Services.SpokenText.Pair(Strings.Input_Col_Events, Events),
        Services.SpokenText.Pair(Strings.Input_Col_Battery, Battery)));
}
