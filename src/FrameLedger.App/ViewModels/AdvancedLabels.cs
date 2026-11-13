// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Advanced;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The words of the Advanced page (owner decision D101): a catalog entry's label, a group's, a token's, a source's
/// state — each a key of <c>Strings.resx</c> made from the thing's own name — and a value in its unit.
/// </summary>
/// <remarks>
/// A key that is not there reads as the thing's own name, never as nothing: a ledger written by a newer build can
/// hold a token this build has no word for, and the page shows what was stored.
/// </remarks>
public static class AdvancedLabels
{
    /// <summary>A catalog entry's label; its English summary where no string names it.</summary>
    public static string Of(AdvancedMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        return Lookup(metric.LabelKey) ?? metric.Summary;
    }

    /// <summary>A member of a family: the family's label and what the hardware calls the member.</summary>
    public static string Member(string family, string? member) => string.IsNullOrWhiteSpace(member) ? family : family + " — " + member;

    public static string Group(AdvancedGroup group) => Lookup("AdvGroup_" + group) ?? group.ToString();

    public static string Scope(AdvancedScope scope) => Lookup("AdvScope_" + scope) ?? scope.ToString();

    /// <summary>A source's name. These are the vendors' own names and are the same in every language.</summary>
    public static string Source(AdvancedSource source) => source switch
    {
        AdvancedSource.Nvapi => "NVAPI",
        AdvancedSource.Lhm => "LibreHardwareMonitor",
        AdvancedSource.Streamline => "Streamline",
        AdvancedSource.FidelityFx => "FidelityFX",
        AdvancedSource.Ngx => "NGX",
        AdvancedSource.Xess => "XeSS",
        _ => source.ToString(),
    };

    /// <summary>Why an entry that is not read is not.</summary>
    public static string Reach(AdvancedReach reach) => Lookup("AdvReach_" + reach) ?? reach.ToString();

    public static string Status(AdvancedSourceStatus status) => Lookup("AdvSource_" + status) ?? status.ToString();

    /// <summary>What became, in one session, of the entries read inside the game (0.1.0-beta.40).</summary>
    public static string InGame(AdvancedInGameStatus status) => Lookup("AdvInGame_" + status) ?? status.ToString();

    /// <summary>What became of one call hooked only for Advanced metrics, in a session (0.1.0-beta.43).</summary>
    public static string InGameCall(AdvancedInGameCall call) => Lookup("AdvInGameCall_" + call) ?? call.ToString();

    /// <summary>
    /// The same for a call a game makes while it starts (0.1.0-beta.44): hooked and not seen is then "the game did not
    /// ask again after FrameLedger attached" - the usual case, and not a finding about the game.
    /// </summary>
    public static string InGameStartupCall(AdvancedInGameCall call) =>
        call == AdvancedInGameCall.NeverCalled ? Strings.AdvInGameCall_NotAskedAgain : InGameCall(call);

    /// <summary>A token the driver's answer was mapped to; the token itself where this build has no word for it.</summary>
    public static string Token(string token) => Lookup("AdvToken_" + token) ?? token;

    /// <summary>
    /// What became of the driver's latency report in a session, in words. A read stopped for being slow says how
    /// slow: <paramref name="slowestUs"/> is the session's slowest call into the driver, in microseconds.
    /// </summary>
    public static string DriverRead(AdvancedDriverRead read, int? slowestUs)
    {
        string text = Lookup("AdvDriverRead_" + read) ?? read.ToString();
        return read == AdvancedDriverRead.Slow ? string.Format(CultureInfo.CurrentCulture, text, slowestUs ?? 0) : text;
    }

    /// <summary>A number in its unit. A flag is yes or no; a count has no unit.</summary>
    public static string Number(double value, AdvancedUnit unit)
    {
        CultureInfo c = CultureInfo.CurrentCulture;
        return unit switch
        {
            AdvancedUnit.Flag => value != 0 ? Strings.Common_Yes : Strings.Common_No,
            AdvancedUnit.Celsius => value.ToString("0.#", c) + " °C",
            AdvancedUnit.Percent => value.ToString("0.#", c) + " %",
            AdvancedUnit.Megahertz => value.ToString("0.#", c) + " MHz",
            AdvancedUnit.Mebibytes => value.ToString("N0", c) + " MiB",
            AdvancedUnit.Watts => value.ToString("0.#", c) + " W",
            AdvancedUnit.Volts => value.ToString("0.###", c) + " V",
            AdvancedUnit.Amperes => value.ToString("0.##", c) + " A",
            AdvancedUnit.Rpm => value.ToString("N0", c) + " RPM",
            AdvancedUnit.Microseconds => value.ToString("N0", c) + " µs",
            AdvancedUnit.Milliseconds => value.ToString("0.##", c) + " ms",
            AdvancedUnit.Nits => value.ToString("0.####", c) + " cd/m²",
            AdvancedUnit.FramesPerSecond => value.ToString("0.#", c) + " FPS",
            AdvancedUnit.BytesPerSecond => Rate(value, c),
            AdvancedUnit.Bits => value.ToString("N0", c) + " bit",
            _ => value.ToString("0.###", c),
        };
    }

    /// <summary>The unit's symbol for a chart's axis; empty where the value has none.</summary>
    public static string Unit(AdvancedUnit unit) => unit switch
    {
        AdvancedUnit.Celsius => "°C",
        AdvancedUnit.Percent => "%",
        AdvancedUnit.Megahertz => "MHz",
        AdvancedUnit.Mebibytes => "MiB",
        AdvancedUnit.Watts => "W",
        AdvancedUnit.Volts => "V",
        AdvancedUnit.Amperes => "A",
        AdvancedUnit.Rpm => "RPM",
        AdvancedUnit.Microseconds => "µs",
        AdvancedUnit.Milliseconds => "ms",
        AdvancedUnit.Nits => "cd/m²",
        AdvancedUnit.FramesPerSecond => "FPS",
        AdvancedUnit.BytesPerSecond => "B/s",
        AdvancedUnit.Bits => "bit",
        _ => string.Empty,
    };

    /// <summary>
    /// A fact's value in one line: a number in its unit, a text as it is, tokens in this language. A table has no one
    /// line — its rows are shown under it — so it reads as how many rows it has.
    /// </summary>
    public static string Value(AdvancedValue value, AdvancedUnit unit)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Number is double number)
        {
            return Number(number, unit);
        }

        if (value.Tokens is { } tokens)
        {
            return tokens.Count == 0 ? Strings.Common_NotAvailable : string.Join(", ", tokens.Select(Token));
        }

        if (value.Rows is { } rows)
        {
            return rows.Count.ToString(CultureInfo.CurrentCulture);
        }

        return string.IsNullOrEmpty(value.Text) ? Strings.Common_NotAvailable : value.Text;
    }

    /// <summary>One row of a table-valued fact: its token in this language where it has one, else what was stored.</summary>
    public static string RowValue(AdvancedRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Token is { Length: > 0 } token ? Token(token) : row.Value;
    }

    private static string Rate(double bytesPerSecond, CultureInfo c) => Math.Abs(bytesPerSecond) switch
    {
        >= 1024 * 1024 => (bytesPerSecond / (1024 * 1024)).ToString("0.#", c) + " MiB/s",
        >= 1024 => (bytesPerSecond / 1024).ToString("0.#", c) + " KiB/s",
        _ => bytesPerSecond.ToString("0", c) + " B/s",
    };

    private static string? Lookup(string key)
    {
        string? found = Strings.ResourceManager.GetString(key, Strings.Culture);
        return string.IsNullOrEmpty(found) ? null : found;
    }
}
