// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Input;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>
/// The summary's "Played with" block (0.1.0-beta.50, D155-D163): the row's stored totals as four rows and a bar, the
/// session's controllers with the battery state held longest, each maker's share, what the reader could not do, and why
/// a session has none - all over a synthetic session; no controller was read to make it.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class InputPresentationTests
{
    private static T In<T>(string culture, Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static SessionRow Bare(CaptureTier tier = CaptureTier.Hooked, string? notes = null) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = tier,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = tier == CaptureTier.Hooked ? 1000 : 0,
        FgMode = "none",
        CaptureNotes = notes,
    };

    private static SessionRow Played(CaptureTier tier = CaptureTier.Hooked) => InputFixtures.With(Bare(tier));

    [Fact]
    public void TheFourClassesAreRowsInTheBarsOrderEachWithItsShareOfTheTimeInFront() => In("en", () =>
    {
        IReadOnlyList<InputClassRowModel> rows = InputPresentation.Rows(Played());

        rows.Select(static r => r.Label).Should().Equal("Controller", "Keyboard and mouse", "Both at once", "No input");
        rows.Select(static r => r.Share).Should().Equal("42.9%", "28.6%", "14.3%", "14.3%");
        rows.Select(static r => r.Duration).Should().Equal("0m 03s", "0m 02s", "0m 01s", "0m 01s");
        rows.Select(static r => r.Segment.Class).Should().Equal(InputClass.Controller, InputClass.KeyboardMouse, InputClass.Both, InputClass.None);
        rows.Sum(static r => r.Segment.Share).Should().BeApproximately(1, 1e-9, "the four classes are the whole time in front");
        rows[0].Swatch.Should().Equal(new InputShareSegment(InputClass.Controller, 1));
        rows[0].ToString().Should().Be("Controller: 42.9%, 0m 03s");
        InputPresentation.AccessibleName(rows).Should().Be(
            "Share of the time in front by what the game was played with: Controller 42.9%, Keyboard and mouse 28.6%, Both at once 14.3%, No input 14.3%");
        InputPresentation.Context(Played()).Should().Be("Read for 0m 08s; the game was in front for 0m 07s. The shares are of the time in front.");
        InputPresentation.Switches(Played()).Should().Be("Changes between a controller and the keyboard and mouse: 2");
        InputPresentation.Missing(Played()).Should().BeNull();
        return 0;
    });

    [Fact]
    public void ATierTwoSessionHasTheSameBlock() => In("en", () =>
    {
        // Read from outside the game (D155): a session that was not hooked is played with something too.
        InputPresentation.Rows(Played(CaptureTier.NotHooked)).Should().HaveCount(4);
        InputPresentation.SharesText(Played(CaptureTier.NotHooked)).Should().Be("Controller 42.9% · keyboard and mouse 28.6% · both 14.3% · no input 14.3%");
        return 0;
    });

    [Fact]
    public void ASessionWithNoShareSaysWhy() => In("en", () =>
    {
        InputPresentation.Rows(Bare()).Should().BeEmpty();
        InputPresentation.Context(Bare()).Should().BeNull();
        InputPresentation.Switches(Bare()).Should().BeNull();
        InputPresentation.SharesText(Bare()).Should().Be("N/A");
        InputPresentation.ControllersText(Bare()).Should().Be("N/A");
        InputPresentation.Missing(Bare()).Should().Be("Not recorded for this session: Settings ▸ Controllers and input was off, or the session is from before 0.1.0-beta.50.");

        // Held because the Privacy Policy that describes the reading was not accepted yet (D156): the row says so.
        InputPresentation.Missing(Bare(notes: "reason=x;" + CaptureNotes.InputHeldSlot))
            .Should().Be("Not read in this session: the Privacy Policy that describes this reading had not been accepted on this PC yet.");

        // Read, and the game never in front: no share is made up out of nothing.
        SessionRow background = Bare() with { InputObservedMs = 5000, InputForegroundMs = 0, InputDevicesJson = InputDevicesRecord.ToJson(new InputDevicesRecord()) };
        InputPresentation.Rows(background).Should().BeEmpty();
        InputPresentation.Missing(background).Should().Be("Read, but the game's window was never in front while it was, so there is no share to show.");
        InputPresentation.ControllersText(background).Should().Be("none");
        return 0;
    });

    [Fact]
    public void AControllerIsARowWithItsConnectionItsTimesItsEventsAndItsBattery() => In("en", () =>
    {
        IReadOnlyList<InputControllerRowModel> rows = InputPresentation.Controllers(InputFixtures.Record());

        rows.Should().HaveCount(2);
        rows[0].Should().Be(new InputControllerRowModel("DualSense Wireless Controller", "Bluetooth", "0m 04s", "0m 08s", "0 / 0",
            "Longest: on battery at 40% for 0m 03s · 40% → 10% · 13.5% an hour"));
        rows[1].Should().Be(new InputControllerRowModel("Xbox Wireless Controller", "Wireless adapter", "0m 00s", "0m 04s", "2 / 1",
            "Longest: on battery at medium for 0m 02s · medium → low"));
        rows[1].ToString().Should().Be("Xbox Wireless Controller: Wireless adapter, In use: 0m 00s, Connected: 0m 04s, Connected / dropped: 2 / 1, Battery: Longest: on battery at medium for 0m 02s · medium → low");
        InputPresentation.Controllers(null).Should().BeEmpty();
        InputPresentation.ControllersText(Played()).Should().Be("DualSense Wireless Controller (Bluetooth) · Xbox Wireless Controller (Wireless adapter)");
        return 0;
    });

    [Fact]
    public void AControllerWithNoModelIsNamedByItsMakerAndAPadAProgramPresentsSaysSo() => In("en", () =>
    {
        InputPresentation.ControllerName(new InputDeviceRecord { Family = "nintendo" }).Should().Be("Nintendo controller");
        InputPresentation.ControllerName(new InputDeviceRecord { Family = "something-new" }).Should().Be("Other controller");
        InputPresentation.ControllerName(new InputDeviceRecord { Family = "xbox", Model = "Steam Virtual Gamepad", Virtual = true })
            .Should().Be("Steam Virtual Gamepad (a virtual pad a program presents)");
        InputPresentation.ControllerName(new InputDeviceRecord { Family = "playstation", Model = "DualSense Wireless Controller", Remapped = true })
            .Should().Be("DualSense Wireless Controller (also shown to games as an Xbox pad)");
        InputPresentation.ConnectionLabel(ControllerConnection.Usb).Should().Be("USB");
        InputPresentation.ConnectionLabel(ControllerConnection.Unknown).Should().Be("Unknown");
        return 0;
    });

    [Fact]
    public void ABatteryIsSaidInItsOwnScaleAndAWiredPadHasNone() => In("en", () =>
    {
        InputPresentation.BatteryText(null).Should().Be("N/A - no battery level was read");
        InputPresentation.BatteryText(new InputBatteryRecord()).Should().Be("N/A - no battery level was read");
        InputPresentation.BatteryText(new InputBatteryRecord { Scale = "none", States = [new InputBatteryStateRecord("wired", 0, 60_000)] })
            .Should().Be("Longest: wired for 1m 00s", "a pad on its cable has no level: no span and no drain are made up for it");
        InputPresentation.BatteryText(new InputBatteryRecord { Scale = "percent", States = [new InputBatteryStateRecord("charging", 85, 5000)], First = 80, Last = 90 })
            .Should().Be("Longest: charging at 85% for 0m 05s · 80% → 90%");
        InputPresentation.StateText(BatteryState.Full, BatteryScale.Percent, 100).Should().Be("full");
        InputPresentation.StateText(BatteryState.Unknown, BatteryScale.None, 0).Should().Be("state unknown");
        InputPresentation.StateText(BatteryState.Unknown, BatteryScale.FourLevels, 3).Should().Be("full");
        Enumerable.Range(0, 4).Select(static l => InputPresentation.LevelText(BatteryScale.FourLevels, l)).Should().Equal("empty", "low", "medium", "full");
        InputPresentation.LevelText(BatteryScale.None, 0).Should().Be("N/A");
        return 0;
    });

    [Fact]
    public void TwoMakersHaveSharesAndOneHasNothingToCompare() => In("en", () =>
    {
        InputPresentation.Makers(InputFixtures.Record()).Should().Be("By maker, of the time controllers were in use: PlayStation 80.0% · Xbox 20.0%. Changes of maker: 2.");
        InputPresentation.Makers(InputFixtures.Record() with { Families = [new InputFamilyRecord("xbox", 1000, 1)] }).Should().BeNull();
        InputPresentation.Makers(null).Should().BeNull();
        return 0;
    });

    [Fact]
    public void WhatTheReaderCouldNotDoIsSaid() => In("en", () =>
    {
        InputPresentation.Notes(Played(), InputFixtures.Record()).Should().BeEmpty("every source worked and Windows showed no pad twice");
        InputPresentation.Notes(Played(), null).Should().BeEmpty();

        InputDevicesRecord broken = InputFixtures.Record() with
        {
            VirtualPad = true,
            Health = new InputHealth { RawInputRegistered = false, XInputAvailable = false, LastInputAvailable = false, Faults = 3 },
        };
        IReadOnlyList<string> notes = InputPresentation.Notes(Played(), broken);
        notes.Should().HaveCount(5);
        notes[0].Should().StartWith("Steam Input, or another remapper,");
        notes[1].Should().StartWith("Windows did not accept the registration");
        notes[2].Should().StartWith("XInput could not be read");
        notes[3].Should().StartWith("Windows did not report the time of the last keyboard or mouse input");
        notes[4].Should().Be("The reader faulted 3 time(s) in this session; the figures cover what it read.");
        return 0;
    });

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("ja")]
    public void TheExportLineIsTheSameInEveryLanguage(string culture) => In(culture, () =>
    {
        InputPresentation.ExportLine(Played()).Should().Be(
            "observed_ms 8000; foreground_ms 7000; controller_ms 3000; keyboard_mouse_ms 2000; both_ms 1000; idle_ms 1000; method_switches 2; source rawinput+xinput+lastinput; "
            + "controller 1 playstation DualSense Wireless Controller bluetooth active_ms 4000 connected_ms 8000 connects 0 disconnects 0; "
            + "controller 2 xbox Xbox Wireless Controller wireless active_ms 500 connected_ms 4000 connects 2 disconnects 1");
        InputPresentation.ExportLine(Bare()).Should().Be("N/A");
        InputPresentation.ExportLine(Bare() with { InputObservedMs = 5000, InputForegroundMs = 0 })
            .Should().Be("observed_ms 5000; foreground_ms 0; controller_ms 0; keyboard_mouse_ms 0; both_ms 0; idle_ms 0; method_switches 0; source n/a; controllers none");
        return 0;
    });

    [Fact]
    public void NoFigureOfTheBlockCanCarryAnIdentifier()
    {
        // What the ledger keeps of a controller is its maker, model, connection, times, counts and battery (21_INPUT_DEVICES):
        // the row model has a column for each and for nothing else.
        typeof(InputControllerRowModel).GetProperties().Select(static p => p.Name)
            .Should().BeEquivalentTo("Name", "Connection", "InUse", "Connected", "Events", "Battery");
    }

    [Theory]
    [InlineData("ChartPalette.Dark.xaml")]
    [InlineData("ChartPalette.Light.xaml")]
    public void TheFourClassesTakeFourRolesThatReadApartInEachTheme(string file)
    {
        ScottPlot.Color[] c = [.. Enumerable.Range(1, 16).Select(static i => ScottPlot.Color.FromARGB((uint)(0xFF000000 | i)))];
        var p = new ChartPalette(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10], c[11], c[12], c[13], c[14], c[15]);
        p.InputFill(InputClass.Controller).Should().Be(p.Native);
        p.InputFill(InputClass.KeyboardMouse).Should().Be(p.Displayed);
        p.InputFill(InputClass.Both).Should().Be(p.Sensor);
        p.InputFill(InputClass.None).Should().Be(p.MixNone);

        // The same bar the sensors' lines clear (beta.8): every pair of the four at least 60 apart in RGB.
        Dictionary<string, (int R, int G, int B)> colours = Colours(file);
        string[] roles = ["Chart_Native", "Chart_Displayed", "Chart_Sensor", "Chart_MixNone"];
        foreach (string a in roles)
        {
            foreach (string b in roles.Where(r => string.CompareOrdinal(r, a) > 0))
            {
                (int R, int G, int B) m = colours[a];
                (int R, int G, int B) n = colours[b];
                Math.Sqrt(((m.R - n.R) * (m.R - n.R)) + ((m.G - n.G) * (m.G - n.G)) + ((m.B - n.B) * (m.B - n.B)))
                    .Should().BeGreaterThanOrEqualTo(60, $"{a} and {b} are two segments of one bar under {file}");
            }
        }
    }

    private static Dictionary<string, (int R, int G, int B)> Colours(string file)
    {
        string dir = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(dir, "FrameLedger.slnx")))
        {
            dir = System.IO.Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("FrameLedger.slnx not found above the test binary");
        }

        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(dir, "src", "FrameLedger.App", "Styles", file))
            .Root!.Elements().Where(e => e.Attribute(x + "Key") is not null).ToDictionary(
                e => (string)e.Attribute(x + "Key")!,
                static e => (Convert.ToInt32(e.Value.Substring(3, 2), 16), Convert.ToInt32(e.Value.Substring(5, 2), 16), Convert.ToInt32(e.Value.Substring(7, 2), 16)),
                StringComparer.Ordinal);
    }
}
