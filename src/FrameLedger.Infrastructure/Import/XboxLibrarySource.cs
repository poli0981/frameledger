// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Import;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// The games the Xbox app and the Microsoft Store installed — PC Game Pass included (beta.33, owner decisions D78/D79,
/// <c>spike-notes</c> §25): every <c>&lt;games folder&gt;\&lt;Game&gt;\Content\</c> that is a GDK package root, under the
/// folders the drives' <c>.GamingRoot</c> files name (<see cref="GamingRoots"/>), whose package Windows says is installed
/// for this user (<see cref="IInstalledPackages"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The record:</b> the store id is the package family name (stable across the game's updates, unlike the WindowsApps
/// folder whose name carries the version); the install directory is <c>Content</c>, the spelling the game's process
/// reports for its own image (§25 — never the <c>C:\Program Files\WindowsApps</c> junction); the launch key is the
/// AppUserModelID <c>&lt;family name&gt;!&lt;Application Id&gt;</c>, exactly what Windows' Start menu lists, which Play
/// activates (D80). The executable is left to <c>ExecutableLocator</c>, because the one the package launches is the
/// Unreal stub, not the shipping executable that presents.
/// </para>
/// <para>
/// GDK titles only (D78): a UWP app has no <c>MicrosoftGame.config</c> and lives in no games folder, so it is not listed.
/// </para>
/// </remarks>
public sealed class XboxLibrarySource(IReadOnlyList<string>? gamingRoots = null, IInstalledPackages? packages = null) : IStoreLibrarySource
{
    /// <summary>The package root's folder name under each game's folder.</summary>
    public const string ContentFolder = "Content";

    public string Platform => "xbox";

    public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> roots = gamingRoots ?? GamingRoots.OnThisPc();
        IInstalledPackages registry = packages ?? WindowsInstalledPackages.Instance;
        List<StoreGame> games = [];
        foreach (string root in roots)
        {
            string[] folders;
            try
            {
                folders = Directory.GetDirectories(root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string folder in folders)
            {
                ct.ThrowIfCancellationRequested();
                if (Read(folder, registry) is { } game)
                {
                    games.Add(game);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    /// <summary>The game in <paramref name="gameFolder"/>, or null when it holds no installed GDK package.</summary>
    public static StoreGame? Read(string gameFolder, IInstalledPackages registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFolder);
        ArgumentNullException.ThrowIfNull(registry);
        string content = Path.Combine(gameFolder, ContentFolder);
        if (GdkPackageFiles.Read(content) is not { } package)
        {
            return null;
        }

        string? family = registry.FamilyName(package.Name, package.Publisher);
        if (family is null || !registry.IsInstalled(family))
        {
            return null;
        }

        string name = package.DisplayName ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(gameFolder));
        string? launchKey = package.ApplicationId is { } appId ? family + "!" + appId : null;
        return new StoreGame("xbox", family, name, content, ExePath: null, package.Version, launchKey);
    }
}
