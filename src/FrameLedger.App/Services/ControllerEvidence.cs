// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.Json;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Input;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// "Does this game support controllers?" as the game's page answers it (owner decision D163): three pieces of
/// evidence, each with where it comes from, and no verdict. What the store's own file lists; which input libraries the
/// game had loaded when it was last measured from inside; how it has actually been played. From outside a game one
/// can know a controller was in use while the game was in front - not what the game did with it - so none of the
/// three is turned into a Yes or a No of FrameLedger's.
/// </summary>
public static class ControllerEvidence
{
    /// <summary>What the store lists: Steam's word, or that no store file on this PC describes the game.</summary>
    public static string StoreText(GameRow game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.ControllerSupport switch
        {
            "full" => Strings.GameDetail_Controller_Store_Full,
            "partial" => Strings.GameDetail_Controller_Store_Partial,
            "none" => Strings.GameDetail_Controller_Store_None,
            _ => Strings.GameDetail_Controller_Store_Unknown,
        };
    }

    /// <summary>
    /// The input libraries the game had loaded in its newest session measured from inside that looked for them, each
    /// interface with its files; that none was seen; or that no such session exists - a session that was not hooked
    /// takes no module snapshot, and one recorded before 0.1.0-beta.50 did not look for these.
    /// </summary>
    public static string LibrariesText(IReadOnlyList<SessionRow> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        foreach (SessionRow session in sessions)
        {
            if (session.Tier != CaptureTier.Hooked || Modules(session.RuntimeModulesJson) is not { } modules)
            {
                continue;
            }

            // Whether the session looked cannot be read from one that found none of them: a session recorded before the
            // names were added has none either. The newest hooked session with modules is the one asked.
            string[] found = [.. modules.Keys
                .Where(static m => InputLibraries.Is(m))
                .GroupBy(static m => InputLibraries.ApiOf(m)!, StringComparer.Ordinal)
                .OrderBy(static g => g.Key, StringComparer.Ordinal)
                .Select(static g => g.Key + " (" + string.Join(", ", g.OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)) + ")")];
            return found.Length > 0 ? string.Join(Environment.NewLine, found) : Strings.GameDetail_Controller_Libraries_NoneSeen;
        }

        return Strings.GameDetail_Controller_Libraries_Unknown;
    }

    /// <summary>How the game has been played, over every session that recorded it: each class's share of the time in front, and how many sessions that is.</summary>
    public static string PlayedWithText(IReadOnlyList<SessionRow> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        SessionRow[] recorded = [.. sessions.Where(static s => s.InputForegroundMs is > 0)];
        long front = recorded.Sum(static s => s.InputForegroundMs ?? 0);
        if (front <= 0)
        {
            return Strings.GameDetail_Controller_Played_Unknown;
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            recorded.Length == 1 ? Strings.GameDetail_Controller_Played_One_Format : Strings.GameDetail_Controller_Played_Many_Format,
            Formats.Share(100d * recorded.Sum(static s => s.InputControllerMs ?? 0) / front),
            Formats.Share(100d * recorded.Sum(static s => s.InputKeyboardMouseMs ?? 0) / front),
            Formats.Share(100d * recorded.Sum(static s => s.InputBothMs ?? 0) / front),
            Formats.Share(100d * recorded.Sum(static s => s.InputIdleMs ?? 0) / front),
            recorded.Length);
    }

    /// <summary>Whether a module is one of the input libraries - the session's Libraries line leaves those to this page.</summary>
    public static bool IsInputLibrary(string moduleFileName) => InputLibraries.Is(moduleFileName);

    private static Dictionary<string, string?>? Modules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) is { Count: > 0 } modules
                ? modules.ToDictionary(static m => m.Key, static m => (string?)m.Value, StringComparer.OrdinalIgnoreCase)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
