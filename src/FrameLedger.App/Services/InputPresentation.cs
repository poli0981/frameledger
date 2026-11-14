// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Controls;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Input;

namespace FrameLedger.App.Services;

/// <summary>
/// The session summary's "Played with" block (0.1.0-beta.50, owner decisions D155-D163; <c>08_UI</c> §Session summary):
/// the row's stored input totals as a bar and a table, the session's controllers from <c>sessions.input_devices</c>, each
/// maker's share, and why a session has none. Every figure is read from the row - nothing is recomputed from the series.
/// </summary>
/// <remarks>
/// What it can say is what was read from outside the game: a device was in use while the game was in front. Never what
/// the game did with it, never a button, a stick or a key (<c>docs/21_INPUT_DEVICES.md</c>).
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class InputPresentation
{
    private static readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    /// <summary>The order of the bar and the table: a controller, the keyboard and mouse, both at once, no input.</summary>
    private static readonly InputClass[] _order = [InputClass.Controller, InputClass.KeyboardMouse, InputClass.Both, InputClass.None];

    /// <summary>Whether the session has shares to show: something was read while the game was in front.</summary>
    public static bool HasShares(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.InputForegroundMs is > 0;
    }

    /// <summary>The table's four rows, in the bar's order; empty when the session has no share to show.</summary>
    public static IReadOnlyList<InputClassRowModel> Rows(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.InputForegroundMs is not { } front || front <= 0)
        {
            return [];
        }

        return [.. _order.Select(c =>
        {
            long ms = Milliseconds(row, c);
            return new InputClassRowModel(ClassLabel(c), Formats.Share(100d * ms / front), Formats.Duration(ms / 1000d), new InputShareSegment(c, (double)ms / front));
        })];
    }

    /// <summary>A class in the user's words.</summary>
    public static string ClassLabel(InputClass input) => input switch
    {
        InputClass.Controller => Strings.Input_Class_Controller,
        InputClass.KeyboardMouse => Strings.Input_Class_KeyboardMouse,
        InputClass.Both => Strings.Input_Class_Both,
        _ => Strings.Input_Class_None,
    };

    /// <summary>How long the session was read and how long the game was in front - what the shares are shares of.</summary>
    public static string? Context(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row is { InputObservedMs: { } observed, InputForegroundMs: { } front }
            ? string.Format(CultureInfo.CurrentCulture, Strings.Input_Context_Format, Formats.Duration(observed / 1000d), Formats.Duration(front / 1000d))
            : null;
    }

    /// <summary>The bar's spoken name: every row with its share, in the table's order.</summary>
    public static string AccessibleName(IReadOnlyList<InputClassRowModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return string.Format(CultureInfo.CurrentCulture, Strings.Input_Bar_Name_Format, string.Join(", ", rows.Select(static r => r.Label + " " + r.Share)));
    }

    /// <summary>How often the session went from a controller to the keyboard and mouse or back; null when nothing was read.</summary>
    public static string? Switches(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return HasShares(row) && row.InputMethodSwitches is { } switches
            ? string.Format(CultureInfo.CurrentCulture, Strings.Input_Switches_Format, switches)
            : null;
    }

    /// <summary>The session's controllers, in the order they were first seen; empty when none was.</summary>
    public static IReadOnlyList<InputControllerRowModel> Controllers(InputDevicesRecord? record) =>
        record is null ? [] : [.. record.Devices.Select(Controller)];

    /// <summary>One controller as its row.</summary>
    public static InputControllerRowModel Controller(InputDeviceRecord device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new InputControllerRowModel(
            ControllerName(device),
            ConnectionLabel(InputNames.ConnectionOf(device.Connection)),
            Formats.Duration(device.ActiveMs / 1000d),
            Formats.Duration(device.ConnectedMs / 1000d),
            string.Format(CultureInfo.CurrentCulture, Strings.Input_Events_Format, device.Connects, device.Disconnects),
            BatteryText(device.Battery));
    }

    /// <summary>
    /// The model's name where the ledger has one, the maker's otherwise; a pad a program presents, and one a remapper
    /// stood in front of, say so - they are what Windows showed, not a second controller in the player's hands.
    /// </summary>
    public static string ControllerName(InputDeviceRecord device)
    {
        ArgumentNullException.ThrowIfNull(device);
        string name = device.Model is { Length: > 0 } model
            ? model
            : string.Format(CultureInfo.CurrentCulture, Strings.Input_Controller_Unnamed_Format, FamilyLabel(InputNames.FamilyOf(device.Family)));
        return device.Virtual
            ? string.Format(CultureInfo.CurrentCulture, Strings.Input_Controller_Virtual_Format, name)
            : device.Remapped ? string.Format(CultureInfo.CurrentCulture, Strings.Input_Controller_Remapped_Format, name) : name;
    }

    /// <summary>A maker in the user's words.</summary>
    public static string FamilyLabel(ControllerFamily family) => family switch
    {
        ControllerFamily.Xbox => Strings.Input_Family_Xbox,
        ControllerFamily.PlayStation => Strings.Input_Family_PlayStation,
        ControllerFamily.Nintendo => Strings.Input_Family_Nintendo,
        _ => Strings.Input_Family_Other,
    };

    /// <summary>A connection in the user's words.</summary>
    public static string ConnectionLabel(ControllerConnection connection) => connection switch
    {
        ControllerConnection.Usb => Strings.Input_Connection_Usb,
        ControllerConnection.Bluetooth => Strings.Input_Connection_Bluetooth,
        ControllerConnection.Wireless => Strings.Input_Connection_Wireless,
        _ => Strings.Input_Connection_Unknown,
    };

    /// <summary>
    /// A controller's battery over the session: the state it was read in longest and for how long (the owner's fifth
    /// point), then the first and last level and how fast it ran down where those are known. <c>N/A</c> with the reason
    /// where no battery was read.
    /// </summary>
    public static string BatteryText(InputBatteryRecord? battery)
    {
        if (battery is null || battery.States.Count == 0)
        {
            return Strings.Input_Battery_NotRead;
        }

        BatteryScale scale = InputNames.ScaleOf(battery.Scale);
        InputBatteryStateRecord longest = battery.States[0];
        var parts = new List<string>(3)
        {
            string.Format(CultureInfo.CurrentCulture, Strings.Input_Battery_Longest_Format, StateText(InputNames.StateOf(longest.State), scale, longest.Level), Formats.Duration(longest.Ms / 1000d)),
        };
        if (scale != BatteryScale.None && battery is { First: { } first, Last: { } last })
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Input_Battery_Span_Format, LevelText(scale, first), LevelText(scale, last)));
        }

        if (battery.DrainPercentPerHour is { } drain)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Input_Battery_Drain_Format, drain));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>What a battery was doing, with its level where it has one.</summary>
    public static string StateText(BatteryState state, BatteryScale scale, int level) => state switch
    {
        BatteryState.Wired => Strings.Input_Battery_State_Wired,
        BatteryState.Full => Strings.Input_Battery_State_Full,
        BatteryState.Charging => scale == BatteryScale.None ? Strings.Input_Battery_State_Charging : string.Format(CultureInfo.CurrentCulture, Strings.Input_Battery_State_Charging_Format, LevelText(scale, level)),
        BatteryState.Discharging => scale == BatteryScale.None ? Strings.Input_Battery_State_Discharging : string.Format(CultureInfo.CurrentCulture, Strings.Input_Battery_State_Discharging_Format, LevelText(scale, level)),
        _ => scale == BatteryScale.None ? Strings.Input_Battery_State_Unknown : LevelText(scale, level),
    };

    /// <summary>A level in its own scale: a percentage, or XInput's four words.</summary>
    public static string LevelText(BatteryScale scale, int level) => scale switch
    {
        BatteryScale.Percent => level.ToString("0", CultureInfo.CurrentCulture) + "%",
        BatteryScale.FourLevels => level switch
        {
            <= 0 => Strings.Input_Battery_Level_Empty,
            1 => Strings.Input_Battery_Level_Low,
            2 => Strings.Input_Battery_Level_Medium,
            _ => Strings.Input_Battery_Level_Full,
        },
        _ => Strings.Common_NotAvailable,
    };

    /// <summary>
    /// Each maker's share of the time controllers were in use and how often the maker in use changed (the owner's seventh
    /// point); null with fewer than two makers - one maker's share of itself says nothing.
    /// </summary>
    public static string? Makers(InputDevicesRecord? record)
    {
        if (record is null || record.Families.Count < 2)
        {
            return null;
        }

        string shares = string.Join(" · ", record.Families.Select(static f => FamilyLabel(InputNames.FamilyOf(f.Family)) + " " + Formats.Share(f.Share * 100)));
        return string.Format(CultureInfo.CurrentCulture, Strings.Input_Makers_Format, shares, record.FamilySwitches);
    }

    /// <summary>
    /// What the reader could not do in this session and what Windows showed twice, one sentence each: nothing that would
    /// leave a figure looking more complete than it is goes unsaid. Empty for a session with nothing to add.
    /// </summary>
    public static IReadOnlyList<string> Notes(InputDevicesRecord? record)
    {
        var notes = new List<string>();
        if (record is null)
        {
            return notes;
        }

        if (record.VirtualPad || record.Devices.Any(static d => d.Remapped))
        {
            notes.Add(Strings.Input_Note_VirtualPad);
        }

        InputHealth health = record.Health;
        if (!health.RawInputRegistered)
        {
            notes.Add(Strings.Input_Note_NoRawInput);
        }

        if (!health.XInputAvailable)
        {
            notes.Add(Strings.Input_Note_NoXInput);
        }

        if (!health.LastInputAvailable)
        {
            notes.Add(Strings.Input_Note_NoLastInput);
        }

        if (health.Faults > 0)
        {
            notes.Add(string.Format(CultureInfo.CurrentCulture, Strings.Input_Note_Faults_Format, health.Faults));
        }

        return notes;
    }

    /// <summary>
    /// Why a session shows no "Played with": the terms that describe the reading were not accepted yet, the game was
    /// never in front, or nothing was recorded - the option was off, or the session is older than it. Null when the
    /// block is shown.
    /// </summary>
    public static string? Missing(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (HasShares(row))
        {
            return null;
        }

        if (CaptureNotes.Parse(row.CaptureNotes).InputHeldForTerms)
        {
            return Strings.Input_Missing_Terms;
        }

        return row.InputObservedMs is > 0 ? Strings.Input_Missing_NeverInFront : Strings.Input_Missing_NotRecorded;
    }

    /// <summary>The four shares in one line - Compare's cell; <c>N/A</c> for a session with none.</summary>
    public static string SharesText(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return HasShares(row)
            ? string.Format(
                CultureInfo.CurrentCulture,
                Strings.Input_Shares_Format,
                Formats.Share(row.InputSharePercent(row.InputControllerMs)),
                Formats.Share(row.InputSharePercent(row.InputKeyboardMouseMs)),
                Formats.Share(row.InputSharePercent(row.InputBothMs)),
                Formats.Share(row.InputSharePercent(row.InputIdleMs)))
            : Strings.Common_NotAvailable;
    }

    /// <summary>The session's controllers by name in one line - Compare's cell; <c>N/A</c> where nothing was read, "none" where none was seen.</summary>
    public static string ControllersText(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.InputObservedMs is not > 0)
        {
            return Strings.Common_NotAvailable;
        }

        InputDevicesRecord? record = InputDevicesRecord.Parse(row.InputDevicesJson);
        return record is { Devices.Count: > 0 }
            ? string.Join(" · ", record.Devices.Select(static d => ControllerName(d) + " (" + ConnectionLabel(InputNames.ConnectionOf(d.Connection)) + ")"))
            : Strings.Input_Controllers_None;
    }

    /// <summary>
    /// The CSV header's line (<c>03_METRICS</c> §Export schema), InvariantCulture: the milliseconds read, in front and in
    /// each class, the method switches, the sources, then each controller - its maker, model, connection, the time in
    /// use and connected, its connects and disconnects. <c>N/A</c> for a session with nothing read. What the export
    /// carries is what Privacy Policy §3A says it carries.
    /// </summary>
    public static string ExportLine(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.InputObservedMs is not { } observed)
        {
            return "N/A";
        }

        string totals = "observed_ms " + observed.ToString(_inv)
            + "; foreground_ms " + (row.InputForegroundMs ?? 0).ToString(_inv)
            + "; controller_ms " + (row.InputControllerMs ?? 0).ToString(_inv)
            + "; keyboard_mouse_ms " + (row.InputKeyboardMouseMs ?? 0).ToString(_inv)
            + "; both_ms " + (row.InputBothMs ?? 0).ToString(_inv)
            + "; idle_ms " + (row.InputIdleMs ?? 0).ToString(_inv)
            + "; method_switches " + (row.InputMethodSwitches ?? 0).ToString(_inv)
            + "; source " + (row.InputSource ?? "n/a");
        InputDevicesRecord? record = InputDevicesRecord.Parse(row.InputDevicesJson);
        if (record is null || record.Devices.Count == 0)
        {
            return totals + "; controllers none";
        }

        return totals + "; " + string.Join("; ", record.Devices.Select(static d =>
            "controller " + d.Index.ToString(_inv) + " " + d.Family + " " + (d.Model ?? "unnamed") + " " + d.Connection
            + " active_ms " + d.ActiveMs.ToString(_inv) + " connected_ms " + d.ConnectedMs.ToString(_inv)
            + " connects " + d.Connects.ToString(_inv) + " disconnects " + d.Disconnects.ToString(_inv)));
    }

    private static long Milliseconds(SessionRow row, InputClass input) => input switch
    {
        InputClass.Controller => row.InputControllerMs ?? 0,
        InputClass.KeyboardMouse => row.InputKeyboardMouseMs ?? 0,
        InputClass.Both => row.InputBothMs ?? 0,
        _ => row.InputIdleMs ?? 0,
    };
}
