// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Watch;

namespace FrameLedger.Infrastructure.Tests.Watch;

/// <summary>
/// beta.19, D55: what the file system says about an executable that did not read — present, gone from a folder that is
/// still there, gone with its folder, nothing left but the drive's root, a drive that is not connected, or a folder Windows
/// will not list — on real folders.
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

    /// <summary>
    /// A game folder Windows will not list says nothing about what is in it (beta.19 review of D55): the probe looks for the
    /// file in its folder's listing, and a listing it is refused is <see cref="ExecutablePresence.Unknown"/> — never
    /// <see cref="ExecutablePresence.FileGone"/>, the one answer that lets the Agent remove the entry.
    /// </summary>
    [Fact]
    public void AGameFolderWindowsWillNotListIsNeverGone()
    {
        string game = Directory.CreateDirectory(Path.Combine(_dir, "it", "a-game", "Game")).FullName;
        string exe = Path.Combine(game, "Game.exe");
        File.WriteAllBytes(exe, [1]);

        using (new Denied(game, FileSystemRights.ListDirectory))
        {
            Assert.SkipUnless(CannotList(game), "this process lists a folder it is denied (a backup privilege enabled?), so the case cannot be made here");
            Probe().Probe(exe).Should().Be(ExecutablePresence.Unknown, "the file cannot be looked for in a folder that will not list");
            Probe().Probe(Path.Combine(game, "Gone.exe")).Should().Be(ExecutablePresence.Unknown, "nor can its absence be read there");
        }

        Probe().Probe(exe).Should().Be(ExecutablePresence.Present, "the same folder, listed again");
    }

    /// <summary>
    /// A game folder that reads as missing because Windows will not say (its attributes denied, and the library folder it is in
    /// will not list) is not a game whose folder went with it: <c>Directory.Exists</c> says false for both, and only the
    /// parent's listing tells them apart. A probe that took the missing folder at its word would remove an installed game.
    /// </summary>
    [Fact]
    public void AGameFolderItsLibraryWillNotListIsNeverGone()
    {
        string library = Directory.CreateDirectory(Path.Combine(_dir, "it")).FullName;
        string game = Directory.CreateDirectory(Path.Combine(library, "a-game")).FullName;
        string exe = Path.Combine(game, "Game.exe");
        File.WriteAllBytes(exe, [1]);

        using (new Denied(game, FileSystemRights.ListDirectory | FileSystemRights.ReadAttributes))
        using (new Denied(library, FileSystemRights.ListDirectory))
        {
            Assert.SkipUnless(!Directory.Exists(game) && CannotList(library),
                "this process can still see a folder it is denied (a backup privilege enabled?), so the case cannot be made here");
            Probe().Probe(exe).Should().Be(ExecutablePresence.Unknown, "the folder reads as missing, and the library it is in will not say whether it is");
        }

        Probe().Probe(exe).Should().Be(ExecutablePresence.Present, "the same folders, readable again");
    }

    private static bool CannotList(string folder)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(folder).Any();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// The current user denied <c>rights</c> on one folder until disposed — always given back, so the folder can be deleted.
    /// The owner can always change the folder's access list, so giving it back needs none of the rights taken.
    /// </summary>
    private sealed class Denied : IDisposable
    {
        private readonly DirectoryInfo _folder;
        private readonly FileSystemAccessRule _rule;

        public Denied(string folder, FileSystemRights rights)
        {
            _folder = new DirectoryInfo(folder);
            using WindowsIdentity me = WindowsIdentity.GetCurrent();
            _rule = new FileSystemAccessRule(me.User!, rights, AccessControlType.Deny);
            DirectorySecurity security = _folder.GetAccessControl();
            security.AddAccessRule(_rule);
            _folder.SetAccessControl(security);
        }

        public void Dispose()
        {
            DirectorySecurity security = _folder.GetAccessControl();
            security.RemoveAccessRuleSpecific(_rule);
            _folder.SetAccessControl(security);
        }
    }
}
