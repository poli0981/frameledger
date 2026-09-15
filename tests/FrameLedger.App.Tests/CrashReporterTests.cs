using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>10_LOGGING</c> §Crash handling in the App (P4 PR-9): the dump before the dialog, the report only on yes, one dialog
/// however many exceptions follow, and neither a failed dump nor a failed report becoming a second crash.
/// </summary>
public sealed class CrashReporterTests
{
    private sealed class Script
    {
        public List<string> Steps { get; } = [];

        public string? Dump { get; init; } = @"C:\data\crashdumps\ui-20260915-010203-42.dmp";

        public bool Yes { get; init; } = true;

        public Func<Task>? Report { get; init; }

        public CrashReporter Build() => new(
            () =>
            {
                Steps.Add("dump");
                return Dump;
            },
            dump =>
            {
                Steps.Add("ask:" + (dump ?? "none"));
                return Yes;
            },
            () =>
            {
                Steps.Add("report");
                return Report?.Invoke() ?? Task.CompletedTask;
            });
    }

    [Fact]
    public async Task TheFirstExceptionIsDumpedThenAskedThenReported()
    {
        var script = new Script();
        CrashReporter reporter = script.Build();
        reporter.Crashed.Should().BeFalse();

        bool reported = await reporter.HandleAsync(new InvalidOperationException("boom"), "Dispatcher");

        reported.Should().BeTrue();
        reporter.Crashed.Should().BeTrue("the caller exits with code 1");
        script.Steps.Should().Equal("dump", @"ask:C:\data\crashdumps\ui-20260915-010203-42.dmp", "report");
    }

    [Fact]
    public async Task NoMeansNoReportAndTheExitCodeIsStillOne()
    {
        var script = new Script { Yes = false };
        CrashReporter reporter = script.Build();

        (await reporter.HandleAsync(new InvalidOperationException("boom"), "Dispatcher")).Should().BeTrue();

        script.Steps.Should().Equal("dump", @"ask:C:\data\crashdumps\ui-20260915-010203-42.dmp");
        reporter.Crashed.Should().BeTrue();
    }

    [Fact]
    public async Task FurtherExceptionsWhileTheFirstIsReportedShowNothingAndAreCounted()
    {
        using var gate = new SemaphoreSlim(0);
        var script = new Script { Report = () => gate.WaitAsync(TestContext.Current.CancellationToken) };
        CrashReporter reporter = script.Build();

        Task<bool> first = reporter.HandleAsync(new InvalidOperationException("layout"), "Dispatcher");
        for (int i = 0; i < 3; i++)
        {
            (await reporter.HandleAsync(new InvalidOperationException("layout again"), "Dispatcher")).Should().BeFalse("a dialog loop over a broken dispatcher is what this prevents");
        }

        gate.Release();
        (await first).Should().BeTrue();
        script.Steps.Should().Equal("dump", @"ask:C:\data\crashdumps\ui-20260915-010203-42.dmp", "report");
        reporter.FurtherExceptions.Should().Be(3);
    }

    [Fact]
    public async Task AFailedDumpStillAsksAndAFailedReportIsNotASecondCrash()
    {
        var steps = new List<string>();
        var reporter = new CrashReporter(
            () => throw new IOException("disk full"),
            dump =>
            {
                steps.Add("ask:" + (dump ?? "none"));
                return true;
            },
            () => throw new InvalidOperationException("no dialog host"));

        Func<Task<bool>> handle = () => reporter.HandleAsync(new InvalidOperationException("boom"), "Dispatcher");

        (await handle.Should().NotThrowAsync()).Which.Should().BeTrue();
        steps.Should().Equal("ask:none");
    }

    [Fact]
    public void TheDialogSaysWhereTheDumpIsOrThatThereIsNone()
    {
        const string dump = @"C:\data\crashdumps\ui-20260915-010203-42.dmp";

        CrashDialog.Body(dump).Should().Contain(dump).And.NotContain(Strings.Crash_NoDump);
        CrashDialog.Body(null).Should().Contain(Strings.Crash_NoDump);
        CrashDialog.Body(null).Should().NotContain("{0}", "both formats are filled");
    }
}
