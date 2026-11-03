// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Import;

namespace FrameLedger.Application.Play;

/// <summary>
/// The store's own way to start a game it installed (beta.21, Play, owner decision D59), read from the forms the stores
/// themselves write — measured on the owner's PC on 2026-10-13, read-only:
/// <list type="bullet">
/// <item><b>Steam</b>: <c>steam://rungameid/&lt;appid&gt;</c>, the link of every game shortcut Steam made there (20 of them);
/// Steam starts the game.</item>
/// <item><b>Epic Games Launcher</b>: <c>com.epicgames.launcher://apps/&lt;namespace&gt;%3A&lt;item&gt;%3A&lt;app&gt;?action=launch&amp;silent=true</c>,
/// the form the launcher's own desktop shortcuts carry, or the <c>AppName</c> alone where the manifest names no catalog —
/// UNMEASURED: no Epic shortcut was on the PC.</item>
/// <item><b>GOG GALAXY</b>: <c>GalaxyClient.exe /command=runGame /gameId=&lt;id&gt; /path="&lt;folder&gt;"</c>, the command line of
/// the game shortcut GALAXY made there; GALAXY starts the game.</item>
/// <item><b>itch</b>: <c>itch://install?game_id=&lt;id&gt;&amp;launch</c>, which the itch app (26.22.0, read from its own code)
/// answers by asking which installed copy to launch — the user confirms there.</item>
/// <item><b>Xbox</b> (beta.33, D80): <c>explorer.exe shell:AppsFolder\&lt;AUMID&gt;</c> — Windows activates the game's
/// package as its Start-menu tile does, the command measured on FINAL FANTASY (<c>spike-notes</c> §25: GameLaunchHelper's
/// parent was the user's shell, the game in its package). Started by its path, a GDK game is never that.</item>
/// </list>
/// Every id is checked against what that store's ids look like before it is put in a link the shell will open.
/// </summary>
public static class StoreLaunches
{
    /// <summary>The hand-off for <paramref name="record"/>, or null when its store has none FrameLedger may use.</summary>
    /// <param name="record">The store's record of the game (<see cref="StoreRecords.Owning"/>).</param>
    /// <param name="galaxyClientPath">GOG GALAXY's program, when it is installed; a GOG game without it has no hand-off.</param>
    public static StoreLaunch? For(StoreGame record, string? galaxyClientPath)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Platform switch
        {
            "steam" when IsDigits(record.StoreId) =>
                new StoreLaunch("steam", StoreLaunchKind.OpenLink, "steam://rungameid/" + record.StoreId),
            "epic" => Epic(record),
            "gog" when IsDigits(record.StoreId) && galaxyClientPath is { Length: > 0 } && IsPlainFolder(record.InstallDirectory) =>
                new StoreLaunch("gog", StoreLaunchKind.RunProgram, galaxyClientPath,
                    "/command=runGame /gameId=" + record.StoreId + " /path=\"" + record.InstallDirectory.TrimEnd('\\', '/') + "\"",
                    Path.GetDirectoryName(galaxyClientPath)),
            "itch" when IsDigits(record.StoreId) =>
                new StoreLaunch("itch", StoreLaunchKind.OpenLink, "itch://install?game_id=" + record.StoreId + "&launch"),
            "xbox" when record.LaunchKey is { } aumid && IsAumid(aumid) && aumid.StartsWith(record.StoreId + "!", StringComparison.Ordinal) =>
                new StoreLaunch("xbox", StoreLaunchKind.RunProgram, ExplorerPath, "shell:AppsFolder\\" + aumid),
            _ => null,
        };
    }

    /// <summary>The user's shell, which activates a package for an elevated caller too (it runs unelevated itself).</summary>
    public static string ExplorerPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    /// <summary>
    /// An AppUserModelID of the shape Windows gives a packaged app: <c>&lt;Name&gt;_&lt;13-character publisher id&gt;!&lt;Application
    /// Id&gt;</c> — the name 3–50 letters, digits, '.' or '-'; the id 1–64 letters, digits or '.', starting with a letter. Nothing
    /// that could be read as another argument or path.
    /// </summary>
    public static bool IsAumid(string aumid)
    {
        ArgumentNullException.ThrowIfNull(aumid);
        int bang = aumid.IndexOf('!', StringComparison.Ordinal);
        int underscore = bang < 0 ? -1 : aumid.AsSpan(0, bang).LastIndexOf('_');
        if (bang < 0 || underscore < 0 || bang - underscore - 1 != 13)
        {
            return false;
        }

        string name = aumid[..underscore];
        string publisherId = aumid[(underscore + 1)..bang];
        string appId = aumid[(bang + 1)..];
        return name.Length is >= 3 and <= 50 && name.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')
            && publisherId.All(static c => char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c))
            && appId.Length is >= 1 and <= 64 && char.IsAsciiLetter(appId[0]) && appId.All(static c => char.IsAsciiLetterOrDigit(c) || c == '.');
    }

    private static StoreLaunch? Epic(StoreGame record)
    {
        string? key = record.LaunchKey?.Split(':') is [var ns, var item, var app]
            && IsEpicToken(ns) && IsEpicToken(item) && IsEpicToken(app) && string.Equals(app, record.StoreId, StringComparison.Ordinal)
                ? ns + "%3A" + item + "%3A" + app
                : IsEpicToken(record.StoreId) ? record.StoreId : null;
        return key is null ? null : new StoreLaunch("epic", StoreLaunchKind.OpenLink, "com.epicgames.launcher://apps/" + key + "?action=launch&silent=true");
    }

    /// <summary>Steam's app ids, GOG's product ids and itch's game ids are whole numbers.</summary>
    private static bool IsDigits(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit);

    /// <summary>Epic's AppName, CatalogNamespace and CatalogItemId: letters, digits, '-', '_' and '.'.</summary>
    private static bool IsEpicToken(string token) =>
        token.Length is > 0 and <= 128 && token.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    /// <summary>A folder that can stand inside one pair of quotes on GALAXY's command line.</summary>
    private static bool IsPlainFolder(string folder) =>
        folder.Length is > 3 and < 1024 && Path.IsPathFullyQualified(folder) && !folder.Contains('"', StringComparison.Ordinal);
}
