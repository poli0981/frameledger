using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.Tests;

/// <summary>
/// Settings ▸ System (2026-09-21): this PC through the same source every session's snapshot comes from. A field the
/// source could not read is N/A, a source that throws is a card of N/A, and Copy is the same lines as text.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class SystemInfoViewModelTests
{
    private sealed class FixedSource(Func<HardwareSnapshot> take) : IHardwareSnapshotSource
    {
        public HardwareSnapshot Take() => take();
    }

    private sealed class FakeClipboard(bool accepts) : IClipboard
    {
        public string? Text { get; private set; }

        public bool SetText(string text)
        {
            Text = text;
            return accepts;
        }
    }

    private sealed class FakeStrip : IMessageStrip
    {
        public List<string> Lines { get; } = [];

        public void Info(string title, string body) => Lines.Add("info:" + body);

        public void Success(string title, string body) => Lines.Add("success:" + body);

        public void Warn(string title, string body) => Lines.Add("warn:" + body);
    }

    private static async Task<T> InEnglishAsync<T>(Func<Task<T>> work)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousThread = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            return await work().ConfigureAwait(false);
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousThread;
        }
    }

    [Fact]
    public async Task TheSnapshotIsStatedLineByLineAndCopyIsTheSameLines()
    {
        var clipboard = new FakeClipboard(accepts: true);
        var strip = new FakeStrip();
        (SystemInfoViewModel vm, string[] lines) = await InEnglishAsync(async () =>
        {
            var model = new SystemInfoViewModel(new FixedSource(static () => new HardwareSnapshot
            {
                CpuName = "AMD Ryzen 7 9800X3D",
                GpuName = "NVIDIA GeForce RTX 5080",
                GpuDriver = "32.0.15.8180",
                RamGb = 31.9,
                OsBuild = "10.0.26200",
                DisplayRes = "2560x1440",
                DisplayHz = 239.96,
            }), clipboard, strip);
            await model.Pending.ConfigureAwait(false);
            model.CopyCommand.Execute(null);
            return (model, model.Lines.Select(static l => l.Label + "=" + l.Value).ToArray());
        });

        lines.Should().Equal(
            "Processor=AMD Ryzen 7 9800X3D",
            "Graphics card=NVIDIA GeForce RTX 5080",
            "Graphics driver=32.0.15.8180",
            "Memory=31.9 GB",
            "Windows build=10.0.26200",
            "Primary display=2560x1440 @ 240 Hz");
        clipboard.Text.Should().Contain("Processor: AMD Ryzen 7 9800X3D").And.Contain("Primary display: 2560x1440 @ 240 Hz");
        strip.Lines.Should().ContainSingle().Which.Should().StartWith("info:");
        vm.Lines.Should().HaveCount(6);
    }

    [Fact]
    public async Task WhatCouldNotBeReadIsNotAvailableAndAThrowingSourceIsACardOfNotAvailable()
    {
        string[] partial = await InEnglishAsync(async () =>
        {
            var model = new SystemInfoViewModel(new FixedSource(static () => new HardwareSnapshot { GpuName = "Some GPU", DisplayRes = "1920x1080" }), new FakeClipboard(true), new FakeStrip());
            await model.Pending.ConfigureAwait(false);
            return model.Lines.Select(static l => l.Value).ToArray();
        });
        partial.Should().Equal("N/A", "Some GPU", "N/A", "N/A", "N/A", "1920x1080");

        var strip = new FakeStrip();
        string[] thrown = await InEnglishAsync(async () =>
        {
            var model = new SystemInfoViewModel(new FixedSource(static () => throw new InvalidOperationException("DXGI is not answering")), new FakeClipboard(accepts: false), strip);
            await model.Pending.ConfigureAwait(false);
            model.CopyCommand.Execute(null);
            return model.Lines.Select(static l => l.Value).ToArray();
        });
        thrown.Should().AllBe("N/A").And.HaveCount(6);
        strip.Lines.Should().ContainSingle().Which.Should().StartWith("warn:", "a clipboard another process holds is a refusal, never a crash");
    }
}
