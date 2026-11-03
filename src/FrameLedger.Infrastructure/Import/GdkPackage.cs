// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Import;

/// <summary>What <see cref="GdkPackageFiles.Read"/> found in a GDK package root; every member but the identity may be null.</summary>
public sealed record GdkPackage
{
    /// <summary><c>Identity/@Name</c>, e.g. <c>LyricalGamesLLC.ValorMortis</c>.</summary>
    public required string Name { get; init; }

    /// <summary><c>Identity/@Publisher</c>, the certificate subject the family name is computed from.</summary>
    public required string Publisher { get; init; }

    /// <summary><c>Identity/@Version</c>, e.g. <c>1.3.5124.0</c>.</summary>
    public string? Version { get; init; }

    /// <summary><c>ShellVisuals/@DefaultDisplayName</c>, else the manifest's <c>DisplayName</c> — never an <c>ms-resource:</c> reference.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The first <c>ExecutableList/Executable/@Name</c>: the executable the package launches (the Unreal stub, the Unity player).</summary>
    public string? LaunchExecutable { get; init; }

    /// <summary>The manifest <c>Application/@Id</c> whose executable is <c>GameLaunchHelper.exe</c>, else the first.</summary>
    public string? ApplicationId { get; init; }

    /// <summary><c>StoreId</c>, the Microsoft Store's product id (<c>9P861V1542BL</c>).</summary>
    public string? StoreId { get; init; }

    /// <summary><c>ShellVisuals/@Square480x480Logo</c>, relative to the package root.</summary>
    public string? Square480Logo { get; init; }

    /// <summary><c>ShellVisuals/@Square150x150Logo</c>, relative to the package root.</summary>
    public string? Square150Logo { get; init; }

    /// <summary><c>ShellVisuals/@SplashScreenImage</c> (1920×1080 on the 11 measured), relative to the package root.</summary>
    public string? SplashScreen { get; init; }
}
