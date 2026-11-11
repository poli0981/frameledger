// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FrameLedger.App.Services;

/// <summary>
/// What a screen reader says for a thing the App shows in pieces (beta.35, D88) — a label and its value, a notice's title
/// and text, a list of facts — in the UI's language. WPF reads a list's item by its <c>ToString()</c> and a template's
/// text only through its control, so the item types and the controls that draw text in a template speak through these.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class SpokenText
{
    /// <summary><c>Runs as: Direct3D 12</c>.</summary>
    public static string Pair(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? label : string.Format(CultureInfo.CurrentCulture, Strings.A11y_Pair_Format, label, value);

    /// <summary><c>Game: hooking refused. Easy Anti-Cheat was detected in this game.</c></summary>
    public static string TitleAndBody(string title, string? body) =>
        string.IsNullOrWhiteSpace(body) ? title : string.Format(CultureInfo.CurrentCulture, Strings.A11y_TitleAndBody_Format, title, body);

    /// <summary>The non-empty parts, in order, separated as the language separates a list.</summary>
    public static string List(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return string.Join(Strings.A11y_List_Separator, parts.Where(static p => !string.IsNullOrWhiteSpace(p)).Select(static p => p!.Trim()));
    }
}
