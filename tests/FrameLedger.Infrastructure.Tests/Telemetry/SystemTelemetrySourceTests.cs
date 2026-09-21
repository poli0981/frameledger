using FluentAssertions;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Telemetry;
using LibreHardwareMonitor.Hardware;
using NSubstitute;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The machine beside the GPU (2026-09-21): busy time is an interval, so the first read has none; kernel time includes
/// idle, as <c>GetSystemTimes</c> reports it; a field nobody measured is null and never zero; a sensor that throws costs
/// its own field and nothing else.
/// </summary>
public sealed class SystemTelemetrySourceTests
{
    private sealed class FakeCounters : ISystemCounters
    {
        public Queue<SystemTimes?> Times { get; } = new();

        public ulong? MemoryInUse { get; set; } = 8UL * 1024 * 1024 * 1024;

        public bool TryReadTimes(out SystemTimes times)
        {
            SystemTimes? next = Times.Count > 0 ? Times.Dequeue() : null;
            times = next ?? default;
            return next is not null;
        }

        public bool TryReadMemoryInUseBytes(out ulong inUse)
        {
            inUse = MemoryInUse ?? 0;
            return MemoryInUse is not null;
        }
    }

    private sealed class FakeTemperature(Func<double?> read) : ICpuTemperatureReader
    {
        public bool Disposed { get; private set; }

        public double? Read() => read();

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void TheFirstReadHasNoIntervalAndTheSecondIsBusyOverElapsed()
    {
        var counters = new FakeCounters();
        counters.Times.Enqueue(new SystemTimes(Idle: 1000, Kernel: 1500, User: 500));
        // +1000 elapsed across kernel+user (kernel INCLUDES idle), of which +750 idle: 25% busy.
        counters.Times.Enqueue(new SystemTimes(Idle: 1750, Kernel: 2300, User: 700));
        using var source = new SystemTelemetrySource(counters, temperature: null);

        source.TryRead(out SystemReading first).Should().BeTrue("memory answered even though load has no interval yet");
        first.CpuLoadPct.Should().BeNull("a first read has nothing to subtract from: null, never 0");
        first.RamUsedMb.Should().Be(8192);
        first.CpuTempC.Should().BeNull();
        source.CpuTemperatureAvailable.Should().BeFalse();

        source.TryRead(out SystemReading second).Should().BeTrue();
        second.CpuLoadPct.Should().BeApproximately(25, 1e-9);
    }

    [Fact]
    public void CountersThatDidNotAdvanceOrWentBackwardsAreNoReading()
    {
        var t = new SystemTimes(10, 20, 30);
        SystemTelemetrySource.Load(t, t).Should().BeNull("two reads inside one clock tick");
        SystemTelemetrySource.Load(t, new SystemTimes(Idle: 110, Kernel: 30, User: 40)).Should().BeNull("idle cannot exceed the total it is part of");
        SystemTelemetrySource.Load(new SystemTimes(0, 0, 0), new SystemTimes(Idle: 0, Kernel: 50, User: 50)).Should().Be(100);
        SystemTelemetrySource.Load(new SystemTimes(0, 0, 0), new SystemTimes(Idle: 100, Kernel: 100, User: 0)).Should().Be(0);
    }

    [Fact]
    public void NothingAnsweringIsNoReadingAtAll()
    {
        var counters = new FakeCounters { MemoryInUse = null };
        using var source = new SystemTelemetrySource(counters, temperature: null);

        source.TryRead(out SystemReading reading).Should().BeFalse();
        reading.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AThrowingTemperatureSensorCostsItsOwnFieldOnlyAndTheReaderIsDisposedWithTheSource()
    {
        var counters = new FakeCounters();
        int calls = 0;
        using var temperature = new FakeTemperature(() => ++calls == 1 ? throw new InvalidOperationException("ring0 went away") : 71.5);
        var source = new SystemTelemetrySource(counters, temperature);

        source.CpuTemperatureAvailable.Should().BeTrue();
        source.TryRead(out SystemReading thrown).Should().BeTrue();
        thrown.CpuTempC.Should().BeNull();
        thrown.RamUsedMb.Should().NotBeNull();
        source.TryRead(out SystemReading read).Should().BeTrue();
        read.CpuTempC.Should().Be(71.5);

        source.Dispose();
        temperature.Disposed.Should().BeTrue();
        source.TryRead(out _).Should().BeFalse("a disposed source reads nothing");
    }

    [Fact]
    public async Task TheRealCountersAnswerOnThisMachine()
    {
        using SystemTelemetrySource source = SystemTelemetrySource.Create();
        source.TryRead(out SystemReading first).Should().BeTrue();
        first.RamUsedMb.Should().BeGreaterThan(256, "this process alone is running on more than that");
        await Task.Delay(150, TestContext.Current.CancellationToken);
        source.TryRead(out SystemReading second).Should().BeTrue();
        second.CpuLoadPct.Should().NotBeNull().And.BeInRange(0, 100);
    }

    private static ISensor Sensor(string name, float? value, SensorType type = SensorType.Temperature)
    {
        ISensor s = Substitute.For<ISensor>();
        s.SensorType.Returns(type);
        s.Name.Returns(name);
        s.Value.Returns(value);
        return s;
    }

    private static IHardware Hardware(HardwareType type, params ISensor[] sensors)
    {
        IHardware h = Substitute.For<IHardware>();
        h.HardwareType.Returns(type);
        h.Sensors.Returns(sensors);
        h.SubHardware.Returns([]);
        return h;
    }

    [Fact]
    public void ThePackageSensorIsTheCpuTemperatureAndTheHottestCoreIsTheFallback()
    {
        IHardware intel = Hardware(HardwareType.Cpu, Sensor("CPU Core #1", 60f), Sensor("CPU Core #2", 66f), Sensor("CPU Package", 70f), Sensor("CPU Core #1 Distance to TjMax", 40f), Sensor("CPU Total", 12f, SensorType.Load));
        IHardware amd = Hardware(HardwareType.Cpu, Sensor("Core (Tctl/Tdie)", 64.5f), Sensor("CCD1 (Tdie)", 58f));
        IHardware coresOnly = Hardware(HardwareType.Cpu, Sensor("CPU Core #1", 55f), Sensor("CPU Core #2", 59f), Sensor("CPU Core #3", null));
        IHardware gpu = Hardware(HardwareType.GpuNvidia, Sensor("GPU Core", 80f));

        LhmCpuTemperatureReader.Pick([intel]).Should().Be(70);
        LhmCpuTemperatureReader.Pick([amd]).Should().Be(64.5);
        LhmCpuTemperatureReader.Pick([coresOnly]).Should().Be(59, "no package sensor: the hottest core is the hottest the part got");
        LhmCpuTemperatureReader.Pick([gpu]).Should().BeNull("a GPU's temperature is not the CPU's");
        LhmCpuTemperatureReader.Pick([]).Should().BeNull();
    }

    [Fact]
    public void TheReaderOpensUpdatesAndClosesItsComputer()
    {
        ILhmComputer computer = Substitute.For<ILhmComputer>();
        IHardware cpu = Hardware(HardwareType.Cpu, Sensor("CPU Package", 68f));
        computer.Hardware.Returns([cpu]);

        var reader = new LhmCpuTemperatureReader(computer);
        reader.Read().Should().Be(68);
        reader.Dispose();
        reader.Read().Should().BeNull();

        computer.Received(1).Open();
        computer.Received(1).Update();
        computer.Received(1).Close();
    }
}
