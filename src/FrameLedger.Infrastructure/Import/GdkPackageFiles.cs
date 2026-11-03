// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Xml;
using System.Xml.Linq;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// The two files a Microsoft GDK game's package root carries (beta.33, <c>spike-notes</c> §25): <c>MicrosoftGame.config</c>
/// — the game's identity, its shell visuals and the executable its package launches — and <c>appxmanifest.xml</c>, whose
/// <c>Application</c> names the id Windows activates. The Xbox app and the Microsoft Store install every such game into
/// <c>&lt;drive&gt;\&lt;games folder&gt;\&lt;Game&gt;\Content\</c>, with both files at the root of <c>Content</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read with a hardened reader</b>: no DTD, no resolver, a size cap — the files are the publisher's, and an
/// unexpected shape costs that one game, never the import. Nothing here opens the game's executable: every game
/// <c>.exe</c> under <c>Content</c> refuses <c>FILE_READ_DATA</c> to a process outside its package (§25), while these XML
/// files, the images and the DLLs read.
/// </para>
/// <para>
/// <b>A package root is the folder holding both files</b> (<see cref="IsPackageRoot"/>). The install-root resolvers use
/// that test only where no store boundary is recognised (<c>InstallRoot</c>, native <c>ResolveInstallRoot</c>), so it
/// can widen what detection and the guard's check 4 walk, never narrow it.
/// </para>
/// </remarks>
public static class GdkPackageFiles
{
    /// <summary>The GDK's own description of the game; the FINAL FANTASY titles spell it <c>MicrosoftGame.Config</c> (NTFS does not mind).</summary>
    public const string ConfigFileName = "MicrosoftGame.config";

    /// <summary>The package manifest Windows registers.</summary>
    public const string ManifestFileName = "appxmanifest.xml";

    /// <summary>How far above an executable's folder <see cref="NearestPackageRoot"/> looks: §25 measured three levels (Unreal's <c>&lt;Project&gt;\Binaries\WinGDK</c>).</summary>
    public const int MaxLevelsUp = 8;

    /// <summary>The largest description file read; the 11 measured on the owner's PC are 1.5–30 KB.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    private const string _manifestNamespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private const string _launchHelper = "GameLaunchHelper.exe";
    private const string _resourcePrefix = "ms-resource:";

    private static readonly XmlReaderSettings _xml = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxFileBytes,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>True when <paramref name="folder"/> holds both a <c>MicrosoftGame.config</c> and an <c>appxmanifest.xml</c> file.</summary>
    public static bool IsPackageRoot(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        try
        {
            return File.Exists(Path.Combine(folder, ConfigFileName)) && File.Exists(Path.Combine(folder, ManifestFileName));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// <paramref name="folder"/> itself or the nearest of its parents, at most <see cref="MaxLevelsUp"/> levels up and never a
    /// drive's root, that <see cref="IsPackageRoot"/>; null when there is none.
    /// </summary>
    public static string? NearestPackageRoot(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        DirectoryInfo? current = new(folder);
        for (int level = 0; level <= MaxLevelsUp && current is { Parent: not null }; level++)
        {
            if (IsPackageRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>The package <paramref name="contentFolder"/> is the root of, or null when it is not one or its files will not parse.</summary>
    public static GdkPackage? Read(string contentFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFolder);
        if (!IsPackageRoot(contentFolder))
        {
            return null;
        }

        XDocument? config = Load(Path.Combine(contentFolder, ConfigFileName));
        XDocument? manifest = Load(Path.Combine(contentFolder, ManifestFileName));
        if (config?.Root is not { } game || manifest?.Root is not { } package)
        {
            return null;
        }

        XElement? identity = Child(game, "Identity");
        string? name = Attribute(identity, "Name");
        string? publisher = Attribute(identity, "Publisher");
        if (name is null || publisher is null)
        {
            return null;
        }

        XElement? visuals = Child(game, "ShellVisuals");
        XNamespace ns = _manifestNamespace;
        List<XElement> applications = [.. package.Elements(ns + "Applications").Elements(ns + "Application")];
        XElement? application = applications.Find(static a => string.Equals(Attribute(a, "Executable"), _launchHelper, StringComparison.OrdinalIgnoreCase))
            ?? applications.FirstOrDefault();

        return new GdkPackage
        {
            Name = name,
            Publisher = publisher,
            Version = Attribute(identity, "Version"),
            DisplayName = Plain(Attribute(visuals, "DefaultDisplayName")) ?? Plain(package.Element(ns + "Properties")?.Element(ns + "DisplayName")?.Value),
            LaunchExecutable = Attribute(Child(Child(game, "ExecutableList"), "Executable"), "Name"),
            ApplicationId = Attribute(application, "Id"),
            StoreId = Plain(Child(game, "StoreId")?.Value),
            Square480Logo = Attribute(visuals, "Square480x480Logo"),
            Square150Logo = Attribute(visuals, "Square150x150Logo"),
            SplashScreen = Attribute(visuals, "SplashScreenImage"),
        };
    }

    private static XDocument? Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            using XmlReader reader = XmlReader.Create(path, _xml);
            return XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    /// <summary>The first child element named <paramref name="localName"/> in any namespace — <c>MicrosoftGame.config</c> declares none.</summary>
    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));

    private static string? Attribute(XElement? element, string name) =>
        element?.Attribute(name)?.Value is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    /// <summary>A name as text, or null for an empty one or an <c>ms-resource:</c> reference this reader does not resolve.</summary>
    private static string? Plain(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith(_resourcePrefix, StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
}
