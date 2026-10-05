// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using Xunit;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.15 (D52): <c>FrameLedger.exe --data-dir &lt;folder&gt;</c> makes the App a viewer over a COPY — and never over the
/// profile's own folder, however that folder is spelled. The "profile" here is a scratch folder of the test's own.
/// </summary>
public sealed class DataFolderArgumentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fl-datadir-" + Guid.NewGuid().ToString("N"));

    public DataFolderArgumentTests()
    {
        Directory.CreateDirectory(Profile);
        Directory.CreateDirectory(Copy);
    }

    private string Profile => Path.Combine(_root, "FrameLedger");

    private string Copy => Path.Combine(_root, "copy");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void WithoutTheFlagTheAppIsTheProfiles() =>
        DataFolderArgument.Read(["--diag"], Profile).Kind.Should().Be(DataFolderChoiceKind.Profile);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFolderThatExistsMakesAViewerOverIt(bool joined)
    {
        string[] args = joined ? [$"--data-dir={Copy}{Path.DirectorySeparatorChar}"] : ["--data-dir", Copy + Path.DirectorySeparatorChar];

        DataFolderChoice choice = DataFolderArgument.Read(args, Profile);

        choice.Kind.Should().Be(DataFolderChoiceKind.Viewer);
        choice.Folder.Should().Be(Copy, "the full path, without its trailing separator");
    }

    [Fact]
    public void TheProfilesOwnFolderIsRefusedHoweverItIsSpelled()
    {
        string[] spellings =
        [
            Profile,
            Profile + Path.DirectorySeparatorChar,
            Profile.ToUpperInvariant(),
            Profile.Replace(Path.DirectorySeparatorChar, '/'),
            Path.GetRelativePath(Environment.CurrentDirectory, Profile),
            Path.Combine(Copy, "..", "FrameLedger"),
        ];

        foreach (string spelling in spellings)
        {
            DataFolderChoice choice = DataFolderArgument.Read(["--data-dir", spelling], Profile);
            choice.Kind.Should().Be(DataFolderChoiceKind.Refused, spelling);
            choice.Problem.Should().ContainEquivalentOf(Profile, "the refusal names the folder it refused");
        }
    }

    /// <summary>A junction is a second name for the same folder: the comparison resolves it, as the Agent's identity does.</summary>
    [Fact]
    public void AJunctionToTheProfilesFolderIsRefused()
    {
        string link = Path.Combine(_root, "link");
        var start = new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false };
        foreach (string arg in new[] { "/c", "mklink", "/J", link, Profile })
        {
            start.ArgumentList.Add(arg);
        }

        using (Process mklink = Process.Start(start)!)
        {
            mklink.WaitForExit();
            mklink.ExitCode.Should().Be(0, "a junction needs no privilege");
        }

        DataFolderArgument.Read(["--data-dir", link], Profile).Kind.Should().Be(DataFolderChoiceKind.Refused);
    }

    [Fact]
    public void AFolderThatDoesNotExistIsRefused()
    {
        string missing = Path.Combine(_root, "missing");

        DataFolderChoice choice = DataFolderArgument.Read(["--data-dir", missing], Profile);

        choice.Kind.Should().Be(DataFolderChoiceKind.Refused);
        choice.Problem.Should().Contain(missing);
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--data-dir", "--diag")]
    [InlineData("--data-dir=")]
    [InlineData("--data-dir", " ")]
    public void AFlagWithoutAFolderIsRefused(params string[] args) =>
        DataFolderArgument.Read(args, Profile).Kind.Should().Be(DataFolderChoiceKind.Refused);

    [Fact]
    public void TwoFoldersAreRefusedRatherThanOneChosen() =>
        DataFolderArgument.Read(["--data-dir", Copy, "--data-dir=" + Copy], Profile).Kind.Should().Be(DataFolderChoiceKind.Refused);

    /// <summary>Only the profile's folder ITSELF is refused: a copy kept inside it is a folder like any other, with its own ledger.</summary>
    [Fact]
    public void ACopyKeptInsideTheProfilesFolderIsAViewer()
    {
        string inside = Path.Combine(Profile, "copy");
        Directory.CreateDirectory(inside);

        DataFolderArgument.Read(["--data-dir", inside], Profile).Kind.Should().Be(DataFolderChoiceKind.Viewer);
    }

    /// <summary>The second lock on the door: the path holder refuses the real profile folder too, and is left unset.</summary>
    [Fact]
    public void ThePathsRefuseTheRealProfileFolderAndStayTheProfiles()
    {
        FluentActions.Invoking(static () => UiPaths.UseViewerFolder(UiPaths.ProfileDirectory)).Should().Throw<InvalidOperationException>();
        UiPaths.IsViewer.Should().BeFalse();
    }
}
