// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.App.Tests;

/// <summary>
/// "Does this game support controllers?" on the game's page (owner decision D163): three pieces of evidence, each with
/// where it comes from, and never a verdict - what the store's own file lists, which input libraries the game had
/// loaded, how it has been played. And where the store's word comes from: the Steam app's file on this PC, read when
/// the page opens, never in a viewer, never over the network.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ControllerEvidenceTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SessionRow Session(long front = 0, long controller = 0, long keyboardMouse = 0, long both = 0, long idle = 0, bool hooked = true, string? modules = null) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        QpcEpoch = 0,
        QpcFrequency = 10_000_000,
        Mode = CaptureMode.Attach,
        ExitStatus = ExitStatus.Normal,
        StartedAt = _t0,
        EndedAt = _t0.AddMinutes(10),
        Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
        RuntimeModulesJson = modules,
        InputForegroundMs = front == 0 ? null : front,
        InputControllerMs = front == 0 ? null : controller,
        InputKeyboardMouseMs = front == 0 ? null : keyboardMouse,
        InputBothMs = front == 0 ? null : both,
        InputIdleMs = front == 0 ? null : idle,
    };

    private static GameRow Game(string? support) => new()
    {
        Id = 1,
        Name = "A Game",
        Fingerprint = new() { ExePath = @"C:\Games\A\a.exe", SizeBytes = 1, MtimeUnixMs = 1 },
        HookEnabled = false,
        HookCrashCount = 0,
        AddedAt = _t0,
        UpdatedAt = _t0,
        ControllerSupport = support,
    };

    [Theory]
    [InlineData("full", "Full - as Steam lists it")]
    [InlineData("partial", "Partial - as Steam lists it")]
    [InlineData("none", "Steam lists none")]
    [InlineData(null, "N/A - no store file on this PC describes this game")]
    [InlineData("a word a later release wrote", "N/A - no store file on this PC describes this game")]
    public void TheStoresWordIsShownAsTheStoresAndItsAbsenceAsNotKnown(string? stored, string shown)
    {
        using var culture = new CultureScope("en");

        ControllerEvidence.StoreText(Game(stored)).Should().Be(shown);
    }

    [Fact]
    public void TheInputLibrariesAreThoseOfTheNewestSessionMeasuredFromInside()
    {
        using var culture = new CultureScope("en");
        SessionRow[] sessions =
        [
            Session(hooked: false),
            Session(modules: """{"nvngx_dlss.dll":"3.7.10.0","XINPUT1_4.dll":"10.0.26100.1","xinput9_1_0.dll":null,"SDL2.dll":"2.30.0.0"}"""),
            Session(modules: """{"dinput8.dll":"10.0"}"""),
        ];

        ControllerEvidence.LibrariesText(sessions).Should().Be("SDL (SDL2.dll)" + Environment.NewLine + "XInput (XINPUT1_4.dll, xinput9_1_0.dll)");
    }

    [Fact]
    public void ASessionThatLoadedNoneSaysSoAndAGameNeverMeasuredFromInsideIsNotKnown()
    {
        using var culture = new CultureScope("en");

        ControllerEvidence.LibrariesText([Session(modules: """{"nvngx_dlss.dll":"3.7.10.0"}""")]).Should().Be("None seen in the last session measured from inside");
        ControllerEvidence.LibrariesText([Session(hooked: false, modules: """{"xinput1_4.dll":"1"}"""), Session(modules: null), Session(modules: "{ not json")])
            .Should().Be("N/A - known only from a session measured from inside the game", "a session that was not hooked takes no snapshot, and one with none stored says nothing");
        ControllerEvidence.LibrariesText([]).Should().Be("N/A - known only from a session measured from inside the game");
    }

    [Fact]
    public void HowAGameWasPlayedIsTheSharesOfTheTimeInFrontOverEverySessionThatRecordedIt()
    {
        using var culture = new CultureScope("en");
        SessionRow[] sessions =
        [
            Session(front: 600_000, controller: 500_000, keyboardMouse: 40_000, both: 10_000, idle: 50_000),
            Session(),
            Session(front: 400_000, controller: 330_000, keyboardMouse: 20_000, both: 0, idle: 50_000, hooked: false),
        ];

        ControllerEvidence.PlayedWithText(sessions).Should().Be("Controller 83.0% · keyboard and mouse 6.0% · both 1.0% · no input 10.0% - of the time in front, over 2 sessions");
        ControllerEvidence.PlayedWithText([sessions[0]]).Should().EndWith("over 1 session");
        ControllerEvidence.PlayedWithText([Session(), Session(hooked: false)]).Should().Be("N/A - no session has recorded it yet");
        ControllerEvidence.PlayedWithText([]).Should().Be("N/A - no session has recorded it yet");
    }

    /// <summary>The input libraries ride the session's module snapshot and are this page's to show: the summary's Libraries line stays what it was.</summary>
    [Fact]
    public void TheSessionsLibrariesLineLeavesTheInputLibrariesOut()
    {
        using var culture = new CultureScope("en");

        SessionSummaryViewModel.LoadedLibraries("""{"nvngx_dlss.dll":"3.7.10.0","xinput1_4.dll":"10.0","SDL3.dll":"3.2"}""", [])
            .Should().Contain("nvngx_dlss.dll").And.NotContain("xinput").And.NotContain("SDL3");
        SessionSummaryViewModel.LoadedLibraries("""{"xinput1_4.dll":"10.0"}""", []).Should().BeNull("a session that loaded only input libraries has no Libraries line");
    }

    /// <summary>A file in Steam's format 41 with one record, for application 730, that lists <paramref name="support"/>.</summary>
    private static string AppInfoFile(string directory, string? support)
    {
        string[] keys = ["appinfo", "common", "controller_support"];
        byte[] Key(string name) => BitConverter.GetBytes(Array.IndexOf(keys, name));
        byte[] value = support is null ? [] : [0x01, .. Key("controller_support"), .. Encoding.UTF8.GetBytes(support), 0];
        byte[] tree = [0x00, .. Key("appinfo"), 0x00, .. Key("common"), .. value, 0x08, 0x08, 0x08];
        byte[] records = [.. BitConverter.GetBytes(0x07564429u), .. BitConverter.GetBytes(1u), .. new byte[8],
            .. BitConverter.GetBytes(730u), .. BitConverter.GetBytes((uint)(60 + tree.Length)), .. new byte[60], .. tree, .. BitConverter.GetBytes(0u)];
        BinaryPrimitives.WriteInt64LittleEndian(records.AsSpan(8), records.Length);
        string path = Path.Combine(directory, "appinfo.vdf");
        File.WriteAllBytes(path, [.. records, .. BitConverter.GetBytes((uint)keys.Length), .. keys.SelectMany(static k => Encoding.UTF8.GetBytes(k).Append((byte)0))]);
        return path;
    }

    private static StoreControllerSupport Reader(ScratchLedger s, string? file, bool viewer = false, params StoreGame[] installed) => new(
        new StoreRecordsReader([new TestImages.Store("steam", [.. installed.Where(static g => string.Equals(g.Platform, "steam", StringComparison.Ordinal))]),
            new TestImages.Store("gog", [.. installed.Where(static g => string.Equals(g.Platform, "gog", StringComparison.Ordinal))])]),
        s.Games,
        viewer ? new UiMode(IsViewer: true, Path.GetTempPath()) : UiMode.Profile,
        () => file,
        new FixedClock(_t0));

    [Fact]
    public async Task OpeningASteamGamesPageReadsWhatSteamsFileListsAndStoresIt()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string folder = Directory.CreateTempSubdirectory("fl-steam-").FullName;
        try
        {
            GameRow game = await s.GameAsync("Counter", Path.Combine(folder, "common", "Counter", "game.exe"));
            var steam = new StoreGame("steam", "730", "Counter", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);
            StoreControllerSupport reader = Reader(s, AppInfoFile(folder, "partial"), viewer: false, steam);

            GameRow read = await reader.RefreshAsync(game, Ct);

            read.ControllerSupport.Should().Be("partial");
            read.ControllerSupportSource.Should().Be(SteamAppInfo.Source);
            read.ControllerSupportAt.Should().Be(_t0);
            GameRow stored = (await s.Games.FindByIdAsync(game.Id, Ct))!;
            stored.ControllerSupport.Should().Be("partial");
            stored.ControllerSupportAt.Should().Be(_t0);
            stored.UpdatedAt.Should().Be(game.UpdatedAt, "nothing the user or the detection did changed");

            // Steam's record changes: the page follows it. The same answer again writes nothing.
            GameRow again = await Reader(s, AppInfoFile(folder, null), viewer: false, steam).RefreshAsync(stored, Ct);
            again.ControllerSupport.Should().Be("none", "Steam knows the game and lists no support");
            (await Reader(s, AppInfoFile(folder, null), viewer: false, steam).RefreshAsync(again, Ct)).Should().BeSameAs(again);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AGameNoSteamRecordOwnsAViewerAndAFileThatCannotBeReadLeaveTheRowAsItWas()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string folder = Directory.CreateTempSubdirectory("fl-steam-").FullName;
        try
        {
            string file = AppInfoFile(folder, "full");
            GameRow game = await s.GameAsync("Counter", Path.Combine(folder, "common", "Counter", "game.exe"));
            var steam = new StoreGame("steam", "730", "Counter", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);
            var gog = new StoreGame("gog", "730", "Counter", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);
            var other = new StoreGame("steam", "999", "Other", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);
            var named = new StoreGame("steam", "not-a-number", "Counter", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);

            (await Reader(s, file).RefreshAsync(game, Ct)).Should().BeSameAs(game, "no store installed it");
            (await Reader(s, file, viewer: false, gog).RefreshAsync(game, Ct)).Should().BeSameAs(game, "another store's game: Steam's file is not asked about it");
            (await Reader(s, file, viewer: false, other).RefreshAsync(game, Ct)).Should().BeSameAs(game, "Steam's file has no record of that application");
            (await Reader(s, file, viewer: false, named).RefreshAsync(game, Ct)).Should().BeSameAs(game);
            (await Reader(s, file, viewer: true, steam).RefreshAsync(game, Ct)).Should().BeSameAs(game, "a viewer reads nothing of this PC (D52)");
            (await Reader(s, file: null, viewer: false, steam).RefreshAsync(game, Ct)).Should().BeSameAs(game, "Steam is not installed");
            (await Reader(s, Path.Combine(folder, "absent.vdf"), viewer: false, steam).RefreshAsync(game, Ct)).Should().BeSameAs(game, "a file that cannot be read says nothing");
            (await s.Games.FindByIdAsync(game.Id, Ct))!.ControllerSupport.Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task TheGamesPageLoadsWithTheStoresWordAndWithoutAReaderAsBefore()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string folder = Directory.CreateTempSubdirectory("fl-steam-").FullName;
        try
        {
            GameRow game = await s.GameAsync("Counter", Path.Combine(folder, "common", "Counter", "game.exe"));
            var steam = new StoreGame("steam", "730", "Counter", Path.Combine(folder, "common", "Counter"), ExePath: null, Version: null);
            var library = new GameLibrary(s.Games, s.Sessions, s.Annotations, clock: null, Reader(s, AppInfoFile(folder, "full"), viewer: false, steam));

            (await library.LoadAsync(game.Id, ct: Ct))!.Row.ControllerSupport.Should().Be("full");
            (await s.Library.LoadAsync(game.Id, ct: Ct))!.Row.ControllerSupport.Should().Be("full", "it was stored, and a library with no reader shows what the row holds");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The strings' language and the numbers' culture, for the length of one test.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo? _strings = Strings.Culture;
        private readonly CultureInfo _numbers = CultureInfo.CurrentCulture;

        public CultureScope(string culture)
        {
            Strings.Culture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            Strings.Culture = _strings;
            CultureInfo.CurrentCulture = _numbers;
        }
    }
}
