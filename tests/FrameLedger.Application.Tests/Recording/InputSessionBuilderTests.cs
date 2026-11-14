// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// What a session stores of its input readings (<c>03_METRICS</c> §Input): the totals, the controllers with their
/// batteries and makers, the series of the timeline - and nothing at all when no sample carries a reading. Every
/// reading here is made by hand: <b>none came from a controller</b>.
/// </summary>
public sealed class InputSessionBuilderTests
{
    private static readonly InputDevice _xbox = new() { Id = 7, Family = ControllerFamily.Xbox, VendorId = ControllerCatalog.Microsoft, ProductId = 0x0B12, Connection = ControllerConnection.Usb, XInputSlot = 0 };

    private static readonly InputDevice _steamPad = new() { Id = 8, Family = ControllerFamily.Xbox, VendorId = ControllerCatalog.Microsoft, ProductId = 0x0B12, Connection = ControllerConnection.Usb, XInputSlot = 1 };

    private static readonly InputDevice _dualSense = new() { Id = 9, Family = ControllerFamily.PlayStation, VendorId = ControllerCatalog.Sony, ProductId = ControllerCatalog.DualSense, Connection = ControllerConnection.Bluetooth };

    private static BatteryReading Percent(int level) => new(BatteryState.Discharging, BatteryScale.Percent, level);

    /// <summary>A tick of one second with the game in front the whole time.</summary>
    private static InputReading Tick(int controller = 0, int keyboardMouse = 0, int both = 0, bool virtualPad = false, InputHealth? health = null, params (InputDevice Device, InputDeviceReading Reading)[] devices) => new()
    {
        SpanMs = 1000,
        ObservedMs = 1000,
        ForegroundMs = 1000,
        ControllerMs = controller,
        KeyboardMouseMs = keyboardMouse,
        BothMs = both,
        IdleMs = 1000 - controller - keyboardMouse - both,
        VirtualPad = virtualPad,
        Devices = [.. devices.Select(static d => d.Reading)],
        Table = [.. devices.Select(static d => d.Device)],
        Health = health ?? new InputHealth { RawInputRegistered = true, XInputAvailable = true, LastInputAvailable = true },
    };

    private static (InputDevice, InputDeviceReading) Pad(InputDevice device, int activeMs = 0, bool connected = true, BatteryReading? battery = null, int connects = 0, int disconnects = 0) =>
        (device, new InputDeviceReading(device.Id, connected, activeMs, battery, connects, disconnects));

    private static List<TelemetrySample> Samples(params InputReading?[] readings)
    {
        List<TelemetrySample> samples = SessionFixtures.Sensors(readings.Length);
        for (int i = 0; i < readings.Length; i++)
        {
            samples[i] = samples[i] with { Input = readings[i] };
        }

        return samples;
    }

    private static float[] Series(IReadOnlyList<SensorBlob> blobs, string name) => RawSeriesCodec.Floats(blobs.Single(b => string.Equals(b.Series, name, StringComparison.Ordinal)).Data);

    [Fact]
    public void WithNoReadingNothingIsStoredAtAll()
    {
        (IReadOnlyList<SensorBlob> blobs, InputSessionSummary? summary) = InputSessionBuilder.Build(SessionFixtures.Sensors(5), new RawSeriesCodec());

        blobs.Should().BeEmpty();
        summary.Should().BeNull("no summary is how a session says the option was off, or the terms not accepted yet");
    }

    [Fact]
    public void TheTotalsAreTheTicksAddedUpAndTheSharesAreOfTheTimeInFront()
    {
        List<TelemetrySample> samples = Samples(
            Tick(controller: 1000, devices: Pad(_xbox, 1000)),
            Tick(controller: 600, keyboardMouse: 200, both: 100, devices: Pad(_xbox, 700)),
            null,
            Tick(keyboardMouse: 1000, devices: Pad(_xbox)),
            Tick(devices: Pad(_xbox)) with { ObservedMs = 400, ForegroundMs = 0, IdleMs = 0 });

        (_, InputSessionSummary? summary) = InputSessionBuilder.Build(samples, new RawSeriesCodec());

        summary!.ObservedMs.Should().Be(3400);
        summary.ForegroundMs.Should().Be(3000);
        summary.ControllerMs.Should().Be(1600);
        summary.KeyboardMouseMs.Should().Be(1200);
        summary.BothMs.Should().Be(100);
        summary.IdleMs.Should().Be(100);
        (summary.ControllerMs + summary.KeyboardMouseMs + summary.BothMs + summary.IdleMs).Should().Be(summary.ForegroundMs);
        summary.Source.Should().Be("rawinput+xinput+lastinput");
    }

    /// <summary>A tick played with both is no one thing; it does not end the run before it.</summary>
    [Fact]
    public void AMethodSwitchIsFromAControllerToTheKeyboardAndMouseOrBack()
    {
        List<TelemetrySample> samples = Samples(
            Tick(controller: 1000),
            Tick(),
            Tick(controller: 900, both: 100),
            Tick(controller: 1000),
            Tick(keyboardMouse: 1000),
            null,
            Tick(keyboardMouse: 300),
            Tick(controller: 400));

        InputSessionBuilder.Build(samples, new RawSeriesCodec()).Summary!.MethodSwitches.Should().Be(2);
    }

    [Fact]
    public void TheTimelineIsWhatEachTickWasMostlyPlayedWithAndNaNWhereThereIsNothingToSay()
    {
        List<TelemetrySample> samples = Samples(
            Tick(controller: 800, keyboardMouse: 100),
            Tick(keyboardMouse: 700),
            Tick(both: 600),
            Tick(),
            null,
            Tick() with { ForegroundMs = 0, IdleMs = 0 });

        (IReadOnlyList<SensorBlob> blobs, _) = InputSessionBuilder.Build(samples, new RawSeriesCodec(), hz: 0.98);

        float[] timeline = Series(blobs, InputSessionBuilder.ClassSeries);
        timeline[..4].Should().Equal((float)InputClass.Controller, (float)InputClass.KeyboardMouse, (float)InputClass.Both, (float)InputClass.None);
        float.IsNaN(timeline[4]).Should().BeTrue("a tick with no reading");
        float.IsNaN(timeline[5]).Should().BeTrue("a tick with the game not in front");
        blobs.Should().OnlyContain(static b => b.Hz == 0.98 && b.Series.StartsWith(InputSessionBuilder.SeriesPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void AControllerHasItsTimesItsCountsAndItsSeries()
    {
        List<TelemetrySample> samples = Samples(
            Tick(controller: 500, devices: Pad(_dualSense, 500, battery: Percent(80))),
            Tick(controller: 1000, devices: Pad(_dualSense, 1000, battery: Percent(80))),
            Tick(devices: Pad(_dualSense, connected: false, disconnects: 1)),
            Tick(),
            Tick(controller: 250, devices: Pad(_dualSense, 250, battery: Percent(70), connects: 1)) with { ForegroundMs = 500, IdleMs = 250 });

        (IReadOnlyList<SensorBlob> blobs, InputSessionSummary? summary) = InputSessionBuilder.Build(samples, new RawSeriesCodec());
        InputDevicesRecord record = InputDevicesRecord.Parse(summary!.DevicesJson)!;

        InputDeviceRecord pad = record.Devices.Should().ContainSingle().Subject;
        pad.Index.Should().Be(1);
        pad.Family.Should().Be("playstation");
        pad.Model.Should().Be("DualSense");
        pad.Connection.Should().Be("bluetooth");
        pad.XInputSlot.Should().BeNull();
        pad.ConnectedMs.Should().Be(3000);
        pad.ActiveMs.Should().Be(1750);
        pad.Connects.Should().Be(1);
        pad.Disconnects.Should().Be(1);
        pad.Battery!.Scale.Should().Be("percent");
        pad.Battery.States.Should().Equal(new InputBatteryStateRecord("discharging", 80, 2000), new InputBatteryStateRecord("discharging", 70, 1000));
        pad.Battery.First.Should().Be(80);
        pad.Battery.Last.Should().Be(70);
        pad.Battery.DrainPercentPerHour.Should().BeNull("three seconds on battery say nothing about a rate");

        float[] active = Series(blobs, InputSessionBuilder.ActiveSeries(1));
        active[0].Should().Be(0.5f);
        active[1].Should().Be(1f);
        float.IsNaN(active[2]).Should().BeTrue("disconnected");
        float.IsNaN(active[3]).Should().BeTrue("not there");
        active[4].Should().Be(0.5f, "250 ms of the 500 the game was in front");
        float[] battery = Series(blobs, InputSessionBuilder.BatterySeries(1));
        battery[0].Should().Be(80f);
        float.IsNaN(battery[2]).Should().BeTrue();
        battery[4].Should().Be(70f);
    }

    [Fact]
    public void AWiredPadHasABatteryStateAndNoBatterySeries()
    {
        List<TelemetrySample> samples = Samples(Tick(devices: Pad(_xbox, battery: BatteryReading.Wired)), Tick(devices: Pad(_xbox, battery: BatteryReading.Wired)));

        (IReadOnlyList<SensorBlob> blobs, InputSessionSummary? summary) = InputSessionBuilder.Build(samples, new RawSeriesCodec());
        InputBatteryRecord battery = InputDevicesRecord.Parse(summary!.DevicesJson)!.Devices[0].Battery!;

        battery.Scale.Should().Be("none");
        battery.States.Should().Equal(new InputBatteryStateRecord("wired", 0, 2000));
        battery.First.Should().BeNull();
        blobs.Select(static b => b.Series).Should().Equal(InputSessionBuilder.ClassSeries, InputSessionBuilder.ActiveSeries(1));
    }

    [Fact]
    public void EachMakerHasItsShareAndAChangeOfMakerIsCounted()
    {
        var ticks = new List<InputReading?>();
        for (int i = 0; i < 6; i++)
        {
            ticks.Add(Tick(controller: 1000, devices: [Pad(_xbox, 1000), Pad(_dualSense)]));
        }

        ticks.Add(Tick(controller: 1000, devices: [Pad(_xbox, 500), Pad(_dualSense, 500)]));
        for (int i = 0; i < 2; i++)
        {
            ticks.Add(Tick(controller: 1000, devices: [Pad(_xbox), Pad(_dualSense, 1000)]));
        }

        ticks.Add(Tick(controller: 1000, devices: [Pad(_xbox, 1000), Pad(_dualSense)]));

        InputDevicesRecord record = InputDevicesRecord.Parse(InputSessionBuilder.Build(Samples([.. ticks]), new RawSeriesCodec()).Summary!.DevicesJson)!;

        record.Devices.Select(static d => d.Family).Should().Equal("xbox", "playstation");
        record.Families.Should().Equal(new InputFamilyRecord("xbox", 7500, 0.75), new InputFamilyRecord("playstation", 2500, 0.25));
        record.FamilySwitches.Should().Be(2, "to the DualSense and back; the tick with both in use is no one maker's");
        record.VirtualPad.Should().BeFalse();
        record.Devices.Should().OnlyContain(static d => !d.Remapped);
    }

    /// <summary>
    /// Steam Input in front of a DualSense: Windows shows the DualSense and an Xbox pad that moves with it. The Xbox
    /// pad is the DualSense, and the session has one controller - Sony's.
    /// </summary>
    [Fact]
    public void AnXInputPadThatMovesWithAnotherControllerIsThatController()
    {
        var ticks = new List<InputReading?>();
        for (int i = 0; i < 12; i++)
        {
            ticks.Add(Tick(controller: 1000, virtualPad: true, devices: [Pad(_dualSense, 1000), Pad(_xbox, 950)]));
        }

        InputDevicesRecord record = InputDevicesRecord.Parse(InputSessionBuilder.Build(Samples([.. ticks]), new RawSeriesCodec()).Summary!.DevicesJson)!;

        InputDeviceRecord pad = record.Devices.Should().ContainSingle().Subject;
        pad.Family.Should().Be("playstation");
        pad.Remapped.Should().BeTrue();
        record.Families.Should().Equal(new InputFamilyRecord("playstation", 12_000, 1));
        record.FamilySwitches.Should().Be(0);
        record.VirtualPad.Should().BeTrue();
    }

    /// <summary>Two of XInput's pads that move together are one only where Steam's virtual gamepad was seen: two players pressing in step are two players.</summary>
    [Fact]
    public void TwoXInputPadsThatMoveTogetherAreOneOnlyWhereSteamsPadWasSeen()
    {
        InputReading?[] Session(bool virtualPad) => [.. Enumerable.Range(0, 12).Select(_ => Tick(controller: 1000, virtualPad: virtualPad, devices: [Pad(_xbox, 1000), Pad(_steamPad, 1000)]))];

        InputDevicesRecord with = InputDevicesRecord.Parse(InputSessionBuilder.Build(Samples(Session(virtualPad: true)), new RawSeriesCodec()).Summary!.DevicesJson)!;
        InputDevicesRecord without = InputDevicesRecord.Parse(InputSessionBuilder.Build(Samples(Session(virtualPad: false)), new RawSeriesCodec()).Summary!.DevicesJson)!;

        InputDeviceRecord kept = with.Devices.Should().ContainSingle().Subject;
        kept.XInputSlot.Should().Be(0, "the higher of the two is the one folded");
        kept.Remapped.Should().BeTrue();
        without.Devices.Should().HaveCount(2);
        without.Devices.Should().OnlyContain(static d => !d.Remapped);
    }

    [Fact]
    public void OnlyTheFirstEightControllersHaveSeriesAndAllAreListed()
    {
        (InputDevice, InputDeviceReading)[] pads = [.. Enumerable.Range(0, InputSessionBuilder.MaxDevices + 2)
            .Select(static i => Pad(new InputDevice { Id = 100 + i, Family = ControllerFamily.Other, VendorId = 0x1234, ProductId = i }))];

        (IReadOnlyList<SensorBlob> blobs, InputSessionSummary? summary) = InputSessionBuilder.Build(Samples(Tick(devices: pads)), new RawSeriesCodec());

        InputDevicesRecord.Parse(summary!.DevicesJson)!.Devices.Should().HaveCount(InputSessionBuilder.MaxDevices + 2);
        blobs.Should().HaveCount(1 + InputSessionBuilder.MaxDevices);
    }

    [Fact]
    public void TheHealthStoredIsTheLastReadingsAndTheSourceNamesWhatAnswered()
    {
        var early = new InputHealth { RawInputRegistered = true, RawReports = 10, Slices = 10 };
        var late = new InputHealth { RawInputRegistered = true, LastInputAvailable = true, RawReports = 2500, Slices = 30, XInputSilent = 4, ThreadCpuMs = 12 };

        InputSessionSummary summary = InputSessionBuilder.Build(Samples(Tick(health: early), null, Tick(health: late), null), new RawSeriesCodec()).Summary!;

        summary.Source.Should().Be("rawinput+lastinput");
        InputDevicesRecord.Parse(summary.DevicesJson)!.Health.Should().Be(late);
    }

    /// <summary>
    /// Nothing that could name a person or a device is stored: the JSON has the keys listed here and no other, so a
    /// serial number, an address or a path would need a key added to this test first.
    /// </summary>
    [Fact]
    public void TheStoredJsonHasNoKeyThatCouldCarryASerialNumberAnAddressOrAPath()
    {
        string json = InputSessionBuilder.Build(Samples(Tick(controller: 1000, devices: Pad(_dualSense, 1000, battery: Percent(50)))), new RawSeriesCodec()).Summary!.DevicesJson;
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        Collect(JsonElement.Parse(json), keys);

        keys.Should().Equal(
            "ActiveMs", "Battery", "ConnectedMs", "Connection", "Connects", "Devices", "Disconnects", "DrainPercentPerHour", "Families", "Family",
            "FamilySwitches", "Faults", "First", "Health", "Index", "Last", "LastInputAvailable", "Level", "Model", "Ms", "ProductId", "RawInputRegistered",
            "RawReports", "RawReportsUnread", "Remapped", "Scale", "Share", "Slices", "State", "States", "ThreadCpuMs", "V", "VendorId", "Virtual",
            "VirtualPad", "XInputAvailable", "XInputReads", "XInputSilent", "XInputSlot");
        json.Should().NotContainAny("\\\\", "HID#", "VID_", "serial", "Serial");
    }

    [Fact]
    public void AColumnThatIsEmptyOrNotJsonReadsAsNothing()
    {
        InputDevicesRecord.Parse(null).Should().BeNull();
        InputDevicesRecord.Parse("  ").Should().BeNull();
        InputDevicesRecord.Parse("{ not json").Should().BeNull();
        InputDevicesRecord.Parse("{}")!.Devices.Should().BeEmpty();
    }

    private static void Collect(JsonElement element, SortedSet<string> keys)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                keys.Add(property.Name);
                Collect(property.Value, keys);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                Collect(item, keys);
            }
        }
    }
}
