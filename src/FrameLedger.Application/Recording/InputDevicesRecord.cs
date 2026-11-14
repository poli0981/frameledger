// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using FrameLedger.Application.Input;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>sessions.input_devices</c> (schema 0024, owner decisions of 2026-11-14): the controllers of a session as JSON -
/// what each was, how long it was connected and in use, how often it connected and dropped, its battery - with each
/// maker's share, how often the maker changed, and what the reader's sources did.
/// </summary>
/// <remarks>
/// The words for a family, a connection and a battery state are <see cref="InputNames"/>'s, lower case, and are never
/// renamed: a ledger keeps them. A model is a product name or absent. No serial number, address or path is here, and
/// <c>InputSessionBuilderTests</c> hold that no key of the JSON could carry one.
/// </remarks>
public sealed record InputDevicesRecord
{
    /// <summary>The format's version; 1.</summary>
    public int V { get; init; } = 1;

    public IReadOnlyList<InputDeviceRecord> Devices { get; init; } = [];

    /// <summary>Each maker's part of the time controllers were in use, the largest first.</summary>
    public IReadOnlyList<InputFamilyRecord> Families { get; init; } = [];

    /// <summary>How many times the maker of the controller in use changed.</summary>
    public int FamilySwitches { get; init; }

    /// <summary>Whether a pad a program presents - Steam's virtual gamepad - was seen.</summary>
    public bool VirtualPad { get; init; }

    public InputHealth Health { get; init; } = new();

    public static string ToJson(InputDevicesRecord record) => JsonSerializer.Serialize(record, RecordingJsonContext.Default.InputDevicesRecord);

    /// <summary>The column's text back, or null for a null, empty or unparsable column.</summary>
    public static InputDevicesRecord? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.InputDevicesRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
