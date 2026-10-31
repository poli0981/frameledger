// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;

namespace FrameLedger.Shared;

/// <summary>
/// The release line this build belongs to (owner decision D73, 2026-10-31): <see cref="Main"/>, the betas and releases on
/// the .NET that <c>global.json</c> pins, or <see cref="Alpha"/>, a beta's source built on a prerelease .NET from the
/// long-lived <c>alpha</c> branch (<c>docs/12_BUILD.md</c> §The alpha line). The <c>RELEASE_LINE</c> file at the repository
/// root names it; <c>Directory.Build.props</c> stamps it into every assembly as the <see cref="MetadataKey"/> metadata and
/// refuses any other value; <c>release.yml</c> refuses a tag that disagrees with it.
/// </summary>
/// <remarks>
/// Composition reads <see cref="Current"/> once and hands it on, so a test gives either line to the code it tests: an
/// alpha's behaviour (its notice, its banner, its ledger backup) is exercised on <c>main</c>, where no alpha is built.
/// </remarks>
public sealed class BuildLine
{
    /// <summary>The metadata key <c>Directory.Build.props</c> writes.</summary>
    public const string MetadataKey = "FrameLedger.ReleaseLine";

    private BuildLine(string name) => Name = name;

    /// <summary>The betas and releases.</summary>
    public static BuildLine Main { get; } = new("main");

    /// <summary>A beta's source on a prerelease .NET.</summary>
    public static BuildLine Alpha { get; } = new("alpha");

    /// <summary>The line this build was stamped with; <see cref="Main"/> for an assembly that carries no stamp.</summary>
    public static BuildLine Current { get; } = Parse(typeof(BuildLine).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(static a => string.Equals(a.Key, MetadataKey, StringComparison.Ordinal))?.Value);

    /// <summary>As <c>RELEASE_LINE</c> spells it.</summary>
    public string Name { get; }

    public bool IsAlpha => ReferenceEquals(this, Alpha);

    /// <summary>The line a stamp names. Only <c>alpha</c> is the alpha: the build refuses any value but the two, so anything else is an assembly built outside it.</summary>
    public static BuildLine Parse(string? stamped) => string.Equals(stamped, Alpha.Name, StringComparison.Ordinal) ? Alpha : Main;

    public override string ToString() => Name;
}
