// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Watch;

namespace FrameLedger.Infrastructure.Tests.Watch;

/// <summary>
/// beta.19, D55: what the file system says about an executable that did not read — present, gone from a folder that is
/// still there, gone with its folder, nothing left but the drive's root, or a drive that is not connected — on real folders.
/// </summary>
public sealed class ExecutablePresenceProbeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-presence-" + Guid.NewGuid().ToString("N"));

    public ExecutablePresenceProbeTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ExecutablePresenceProbe Probe() => new(VolumeRoots.IsReady);

    [Fact]
    public void AFileThereIsPresentAndAFileGoneFromItsFolderIsGone()
    {
        string game = Directory.CreateDirectory(Path.Combine(_dir, "it", "a-game", "Game")).FullName;
        string exe = Path.Combine(game, "Game.exe");
        File.WriteAllBytes(exe, [1]);

        Probe().Probe(exe).Should().Be(ExecutablePresence.Present);

        File.Delete(exe);
        Probe().Probe(exe).Should().Be(ExecutablePresence.FileGone, "the folder is still there and lists no such file");
    }

    [Fact]
    public void AGameWhoseFolderWentWithItIsGoneWhileTheFolderItWasInstalledInIsThere()
    {
        string library = Directory.CreateDirectory(Path.Combine(_dir, "it")).FullName;

        Probe().Probe(Path.Combine(library, "a-dolls-locket-remake", "Dolls Locket", "css", "Game.exe")).Should().Be(ExecutablePresence.FileGone,
            "an uninstall takes the game's folder and leaves the library's");
    }

    [Fact]
    public void NothingLeftButTheDrivesRootIsNotAnUninstall()
    {
        string root = Path.GetPathRoot(_dir)!;
        string nowhere = Path.Combine(root, "fl-presence-absent-" + Guid.NewGuid().ToString("N"), "Games", "Title", "Title.exe");

        Probe().Probe(nowhere).Should().Be(ExecutablePresence.OnlyTheDriveRemains, "a different disk under the same letter looks exactly like this");
    }

    [Fact]
    public void ADriveThatIsNotConnectedOrAShareIsNeverGone()
    {
        string exe = Path.Combine(_dir, "Game.exe");

        new ExecutablePresenceProbe(static _ => false).Probe(exe).Should().Be(ExecutablePresence.DriveAbsent, "the drive is not ready");
        VolumeRoots.IsReady(Path.GetPathRoot(_dir)!).Should().BeTrue("the drive the temporary folder is on is a ready local drive");
        Probe().Probe(@"\\server\share\Games\Title.exe").Should().Be(ExecutablePresence.Unknown, "a share is not where a library is uninstalled from");
    }
}
