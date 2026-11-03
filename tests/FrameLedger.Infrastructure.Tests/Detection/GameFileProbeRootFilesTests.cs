// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;
using FrameLedger.Infrastructure.Detection;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Detection;

/// <summary>
/// beta.33: what a <c>root_file</c> signal is answered from — the install root's own listing, files only, taken once —
/// and a root that cannot be listed is no listing at all, never an empty one (an empty one would read every
/// <c>root_file</c> as a miss).
/// </summary>
public sealed class GameFileProbeRootFilesTests : IDisposable
{
    private static readonly DetectionRuleSet _rules = new()
    {
        SchemaVersion = 2,
        RulesVersion = "t",
        Engines = [],
        Platforms = [],
        Capabilities = [],
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fl-rootfiles-" + Guid.NewGuid().ToString("N"));

    public GameFileProbeRootFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePackageRootsOwnFilesAreListedAndNothingBelowIt()
    {
        string content = Path.Combine(_root, "Valor Mortis", "Content");
        string binaries = Path.Combine(content, "ValorMortis", "Binaries", "Win64");
        Directory.CreateDirectory(binaries);
        await File.WriteAllTextAsync(Path.Combine(content, GdkPackageFiles.ConfigFileName), "<Game />", Ct);
        await File.WriteAllTextAsync(Path.Combine(content, GdkPackageFiles.ManifestFileName), "<Package />", Ct);
        string exe = Path.Combine(binaries, "ValorMortisMSG-Win64-Shipping.exe");
        await File.WriteAllBytesAsync(exe, [0x4D, 0x5A], Ct);

        GameFileSnapshot snapshot = await new GameFileProbe().SnapshotAsync(exe, _rules, Ct);

        snapshot.GameDirectory.Should().Be(content.Replace('\\', '/'), "a GDK package root is the install root of an executable three levels down");
        snapshot.RootFiles.Should().BeEquivalentTo([GdkPackageFiles.ConfigFileName, GdkPackageFiles.ManifestFileName]);
        snapshot.RootFiles!.Contains("MICROSOFTGAME.CONFIG").Should().BeTrue("a file name is matched as NTFS matches it");
    }

    [Fact]
    public async Task ARootThatCannotBeListedIsNoListing()
    {
        GameFileSnapshot snapshot = await new GameFileProbe().SnapshotAsync(Path.Combine(_root, "gone", "game.exe"), _rules, Ct);

        snapshot.RootFiles.Should().BeNull("a folder that is not there answers Unknown, never \"no such file\"");
    }
}
