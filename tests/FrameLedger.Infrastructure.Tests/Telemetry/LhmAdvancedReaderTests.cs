// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Telemetry;
using LibreHardwareMonitor.Hardware;
using NSubstitute;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// LibreHardwareMonitor's part of "Advanced metrics" on a tree of substitutes: which sensor is a member of which
/// catalog family, what it is called, and what happens to a device that throws.
/// </summary>
public sealed class LhmAdvancedReaderTests
{
    private static LhmAdvancedReader Opened(FakeLhmComputer computer)
    {
        var reader = new LhmAdvancedReader(computer);
        reader.Open().Should().BeTrue();
        return reader;
    }

    [Theory]
    [InlineData(false, true, LhmGroups.Gpu)]
    [InlineData(true, false, LhmGroups.Gpu)]
    [InlineData(true, null, LhmGroups.Gpu)]
    [InlineData(true, true, LhmGroups.Gpu | LhmGroups.Cpu | LhmGroups.Memory | LhmGroups.Motherboard)]
    public void TheOtherGroupsAreOpenedOnlyAsAdministratorWithPawnIoAndAnUnknownAnswerIsNotAYes(bool elevated, bool? pawnIo, LhmGroups expected)
        => LhmAdvancedReader.GroupsFor(elevated, pawnIo).Should().Be(expected);

    [Fact]
    public void NoGroupThisProjectRefusedHasAMember()
    {
        // D103 and M13: storage, the power supply, controllers, the battery and the network are not groups this enum can
        // name, so no caller can ask for them by mistake.
        Enum.GetNames<LhmGroups>().Should().BeEquivalentTo("None", "Gpu", "Cpu", "Memory", "Motherboard");
    }

    [Theory]
    [InlineData(HardwareType.GpuNvidia, SensorType.Temperature, "lhm.gpu.temperature")]
    [InlineData(HardwareType.GpuAmd, SensorType.Load, "lhm.gpu.load")]
    [InlineData(HardwareType.GpuIntel, SensorType.SmallData, "lhm.gpu.memory")]
    [InlineData(HardwareType.GpuNvidia, SensorType.Control, "lhm.gpu.fan_control")]
    [InlineData(HardwareType.GpuNvidia, SensorType.Throughput, "lhm.gpu.throughput")]
    [InlineData(HardwareType.Cpu, SensorType.Clock, "lhm.cpu.clock")]
    [InlineData(HardwareType.Cpu, SensorType.Voltage, "lhm.cpu.voltage")]
    [InlineData(HardwareType.Motherboard, SensorType.Fan, "lhm.board.fan")]
    [InlineData(HardwareType.Motherboard, SensorType.Control, "lhm.board.fan_control")]
    [InlineData(HardwareType.Memory, SensorType.Temperature, "lhm.memory.temperature")]
    public void ASensorIsAMemberOfTheFamilyItsDeviceAndItsTypeName(HardwareType root, SensorType type, string family)
    {
        LhmAdvancedMap.FamilyOf(root, type).Should().Be(family);
        AdvancedMetricCatalog.Find(family).Should().NotBeNull("a family the map names is a family the catalog has");
    }

    [Fact]
    public void EveryFamilyTheMapNamesIsACatalogFamilyOfSeriesReadFromLibreHardwareMonitor()
    {
        string[] families = [.. Enum.GetValues<HardwareType>().SelectMany(static root => Enum.GetValues<SensorType>().Select(type => LhmAdvancedMap.FamilyOf(root, type))).OfType<string>().Distinct(StringComparer.Ordinal)];

        families.Should().NotBeEmpty();
        foreach (string family in families)
        {
            AdvancedMetric metric = AdvancedMetricCatalog.Find(family).Should().BeOfType<AdvancedMetric>(family).Subject;
            metric.IsFamily.Should().BeTrue(family);
            metric.Scope.Should().Be(AdvancedScope.Series, family);
            metric.Source.Should().Be(AdvancedSource.Lhm, family);
            metric.IsRead.Should().BeTrue(family);
        }

        // The other direction: a family the catalog says is read is one some sensor can be a member of.
        string[] readFamilies = [.. AdvancedMetricCatalog.All.Where(static m => m.Source == AdvancedSource.Lhm && m.IsRead && m.Scope == AdvancedScope.Series).Select(static m => m.Id)];
        families.Should().BeEquivalentTo(readFamilies);
    }

    [Theory]
    [InlineData(HardwareType.Storage, SensorType.Temperature)]
    [InlineData(HardwareType.Network, SensorType.Throughput)]
    [InlineData(HardwareType.Psu, SensorType.Power)]
    [InlineData(HardwareType.Battery, SensorType.Voltage)]
    [InlineData(HardwareType.Cooler, SensorType.Fan)]
    [InlineData(HardwareType.GpuNvidia, SensorType.Data)]
    [InlineData(HardwareType.Memory, SensorType.Data)]
    [InlineData(HardwareType.Cpu, SensorType.Factor)]
    public void ASensorWithNoFamilyIsNotRead(HardwareType root, SensorType type)
        => LhmAdvancedMap.FamilyOf(root, type).Should().BeNull();

    [Theory]
    [InlineData("/lpc/nct6798d/0/voltage/3", SensorType.Voltage, "lpc_nct6798d_0_3")]
    [InlineData("/gpu-nvidia/0/temperature/0", SensorType.Temperature, "gpu_nvidia_0_0")]
    [InlineData("/intelcpu/0/load/12", SensorType.Load, "intelcpu_0_12")]
    [InlineData("/ram/load/0", SensorType.Load, "ram_0")]
    [InlineData("/GPU-AMD/1/SmallData/2", SensorType.SmallData, "gpu_amd_1_2")]
    public void AnInstanceKeyIsTheIdentifierWithoutItsTypeInLowerCase(string identifier, SensorType type, string key)
    {
        LhmAdvancedMap.KeyOf(identifier, type).Should().Be(key);
        AdvancedMetricCatalog.IsInstanceKey(key).Should().BeTrue();
    }

    [Fact]
    public void AKeyTooLongKeepsItsEndWhereTheIndicesAre()
    {
        string identifier = "/" + new string('x', 80) + "/0/voltage/17";

        string? key = LhmAdvancedMap.KeyOf(identifier, SensorType.Voltage);

        key.Should().HaveLength(AdvancedMetricCatalog.MaxInstanceKeyLength);
        key.Should().EndWith("_0_17");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/voltage")]
    [InlineData("///")]
    public void AnIdentifierWithNothingUsableHasNoKey(string? identifier)
        => LhmAdvancedMap.KeyOf(identifier, SensorType.Voltage).Should().BeNull();

    [Fact]
    public void ALabelIsTheDevicesNameThenTheSensorsAndFallsBackToWhatThereIs()
    {
        IHardware[] pc = FakeSensorTree.WholePc();
        ISensor core = pc[1].Sensors[1];
        ISensor nameless = FakeSensorTree.Sensor(SensorType.Fan, "/lpc/x/0/fan/0", " ", 1f);
        ISensor orphan = FakeSensorTree.Sensor(SensorType.Fan, "/lpc/x/0/fan/1", "Fan #2", 1f);
        FakeSensorTree.Device(HardwareType.SuperIO, "Chip", nameless);

        LhmAdvancedMap.LabelOf(core).Should().Be("Intel Core i7-14700K - CPU Core #3");
        LhmAdvancedMap.LabelOf(nameless).Should().Be("Chip");
        LhmAdvancedMap.LabelOf(orphan).Should().Be("Fan #2");
    }

    [Fact]
    public void EverySensorWithAFamilyBecomesOneReadingNamedByItsFamilyAndItsKey()
    {
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(FakeSensorTree.WholePc()));

        reader.Refresh();
        (IReadOnlyList<AdvancedReading> readings, IReadOnlyList<AdvancedSeriesInfo> series) = reader.Read();

        readings.Should().BeEquivalentTo(
        [
            new AdvancedReading("adv.lhm.gpu.temperature.gpu_nvidia_0_0", 61f),
            new AdvancedReading("adv.lhm.gpu.fan_control.gpu_nvidia_0_1", 42f),
            new AdvancedReading("adv.lhm.gpu.voltage.gpu_nvidia_0_0", 1.05f),
            new AdvancedReading("adv.lhm.gpu.throughput.gpu_nvidia_0_0", 2048f),
            new AdvancedReading("adv.lhm.cpu.temperature.intelcpu_0_0", 71f),
            new AdvancedReading("adv.lhm.cpu.temperature.intelcpu_0_3", 68f),
            new AdvancedReading("adv.lhm.cpu.load.intelcpu_0_0", 37f),
            new AdvancedReading("adv.lhm.cpu.power.intelcpu_0_0", 96.5f),
            new AdvancedReading("adv.lhm.board.voltage.lpc_nct6798d_0_7", -11.97f),
            new AdvancedReading("adv.lhm.board.fan.lpc_nct6798d_0_1", 940f),
            new AdvancedReading("adv.lhm.board.temperature.lpc_nct6798d_0_2", 38f),
            new AdvancedReading("adv.lhm.memory.load.ram_0", 44f),
        ]);
        series.Select(static s => s.Series).Should().Equal(readings.Select(static r => r.Series), "every reading has its directory line, in the same order");
        series.Single(static s => string.Equals(s.Series, "adv.lhm.board.voltage.lpc_nct6798d_0_7", StringComparison.Ordinal))
            .Should().Be(new AdvancedSeriesInfo("adv.lhm.board.voltage.lpc_nct6798d_0_7", "lhm.board.voltage", "Nuvoton NCT6798D - -12V"));
    }

    [Fact]
    public void ANegativeRailIsAReadingAndASensorWithNoValueOrANonNumberIsNot()
    {
        IHardware chip = FakeSensorTree.Device(HardwareType.SuperIO, "Chip",
            FakeSensorTree.Sensor(SensorType.Voltage, "/lpc/c/0/voltage/0", "-12V", -11.97f),
            FakeSensorTree.Sensor(SensorType.Voltage, "/lpc/c/0/voltage/1", "none", null),
            FakeSensorTree.Sensor(SensorType.Voltage, "/lpc/c/0/voltage/2", "nan", float.NaN),
            FakeSensorTree.Sensor(SensorType.Voltage, "/lpc/c/0/voltage/3", "inf", float.PositiveInfinity),
            FakeSensorTree.Sensor(SensorType.Temperature, "/lpc/c/0/temperature/0", "zero", 0f));
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(FakeSensorTree.Device(HardwareType.Motherboard, "Board").WithSub(chip)));

        IReadOnlyList<AdvancedReading> readings = reader.Read().Readings;

        readings.Select(static r => r.Value).Should().Equal([-11.97f, 0f], "a negative voltage and a zero are readings (D102); no value and no number are not");
    }

    [Fact]
    public void TwoSensorsWhoseIdentifiersShortenToOneKeyAreOneReading()
    {
        IHardware gpu = FakeSensorTree.Device(HardwareType.GpuNvidia, "Card",
            FakeSensorTree.Sensor(SensorType.Fan, "/gpu-nvidia/0/fan/1", "first", 900f),
            FakeSensorTree.Sensor(SensorType.Fan, "/gpu.nvidia/0/fan/1", "second", 950f));
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(gpu));

        (IReadOnlyList<AdvancedReading> readings, IReadOnlyList<AdvancedSeriesInfo> series) = reader.Read();

        readings.Should().ContainSingle().Which.Value.Should().Be(900f);
        series.Should().ContainSingle().Which.Label.Should().Be("Card - first");
    }

    [Fact]
    public void ADeviceThatThrowsTwiceIsLeftAloneAndTheOthersGoOnBeingRead()
    {
        IHardware[] pc = FakeSensorTree.WholePc();
        IHardware board = pc[2];
        board.When(static b => b.Update()).Throw(new InvalidOperationException("the bus did not answer"));
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(pc));

        reader.Refresh();
        reader.SkippedDevices.Should().Be(0, "one fault is not two");
        reader.Read().Readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.board.", StringComparison.Ordinal), "after one fault the device's last values are still its values");
        reader.Refresh();

        reader.SkippedDevices.Should().Be(1);
        reader.LastFault.Should().Contain("Motherboard").And.Contain("the bus did not answer");
        IReadOnlyList<AdvancedReading> readings = reader.Read().Readings;
        readings.Should().NotContain(static r => r.Series.StartsWith("adv.lhm.board.", StringComparison.Ordinal));
        readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.cpu.", StringComparison.Ordinal));
        readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.gpu.", StringComparison.Ordinal));

        reader.Refresh();
        reader.Refresh();
        board.Received(2).Update();
        pc[1].Received(4).Update();
    }

    [Fact]
    public void ASubDeviceIsRefreshedWithItsParent()
    {
        IHardware[] pc = FakeSensorTree.WholePc();
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(pc));

        reader.Refresh();

        pc[2].SubHardware[0].Received(1).Update();
    }

    [Fact]
    public void TheMemoryGroupIsRefreshedEveryFifthTick()
    {
        IHardware[] pc = FakeSensorTree.WholePc();
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(pc));

        for (int i = 0; i < 11; i++)
        {
            reader.Refresh();
        }

        pc[3].Received(3).Update();
        pc[0].Received(11).Update();
    }

    [Fact]
    public void TheFactsAreTheBoardsModelItsSensorChipAndTheMemoryDevices()
    {
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(FakeSensorTree.WholePc()));

        IReadOnlyList<AdvancedFact> facts = reader.ReadFacts();

        facts.Single(static f => string.Equals(f.Id, "lhm.board.model", StringComparison.Ordinal)).Value.Text.Should().Be("ASUS ROG STRIX Z790-E");
        facts.Single(static f => string.Equals(f.Id, "lhm.board.sensor_chip", StringComparison.Ordinal)).Value.Text.Should().Be("Nuvoton NCT6798D");
        facts.Single(static f => string.Equals(f.Id, "lhm.memory.modules", StringComparison.Ordinal)).Value.Rows.Should().Equal(new AdvancedRow("Total Memory", string.Empty));
        facts.Where(static f => AdvancedMetricCatalog.Find(f.Id) is not { IsRead: true, Scope: AdvancedScope.MachineFact }).Should().BeEmpty();
    }

    [Fact]
    public void WithOnlyTheGraphicsCardOpenThereAreNoSuchFacts()
    {
        using LhmAdvancedReader reader = Opened(new FakeLhmComputer(FakeSensorTree.WholePc()[0]));

        reader.ReadFacts().Should().BeEmpty();
    }

    [Fact]
    public void ALibraryThatDoesNotOpenReadsNothingAndSaysWhy()
    {
        var computer = new FakeLhmComputer(FakeSensorTree.WholePc()) { OpenThrows = true };
        using var reader = new LhmAdvancedReader(computer);

        reader.Open().Should().BeFalse();

        reader.LastFault.Should().Contain("the driver refused");
        reader.Refresh();
        reader.Read().Readings.Should().BeEmpty();
        reader.ReadFacts().Should().BeEmpty();
    }

    [Fact]
    public void OpeningTwiceOpensOnceAndDisposingClosesOnceEvenWhenClosingThrows()
    {
        var computer = new FakeLhmComputer(FakeSensorTree.WholePc()) { CloseThrows = true };
        var reader = new LhmAdvancedReader(computer);
        reader.Open().Should().BeTrue();
        reader.Open().Should().BeTrue();

        reader.Dispose();
        reader.Dispose();

        computer.Opens.Should().Be(1);
        computer.Closes.Should().Be(1);
        reader.LastFault.Should().Contain("close");
        reader.Read().Readings.Should().BeEmpty();
        reader.ReadFacts().Should().BeEmpty();
        FluentActions.Invoking(reader.Open).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AReaderNeverOpenedClosesNothing()
    {
        var computer = new FakeLhmComputer();
        var reader = new LhmAdvancedReader(computer);

        reader.Refresh();
        reader.Dispose();

        computer.Closes.Should().Be(0);
    }
}
