using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §Crash handling for the App's own fatal exceptions (P4 PR-9): Serilog <c>Fatal</c> with the whole
/// exception, a minidump, the crash dialog offering the bug-report flow; the caller then ends the process with exit code
/// 1. The three effects are delegates so the order and the one-dialog rule are tested without a crash.
/// </summary>
/// <remarks>
/// A second fatal exception while the first is being reported shows nothing: a dialog loop over a broken dispatcher is
/// the failure this prevents. Only the first of those is logged whole. A fault in a layout pass repeats on every pass,
/// and the dialog's own message loop keeps running passes, so logging each would flood <c>ui-*.log</c>; the rest are
/// counted and the count is logged when the report ends.
/// </remarks>
public sealed class CrashReporter
{
    private readonly Func<string?> _writeDump;
    private readonly Func<string?, bool> _askToReport;
    private readonly Func<Task> _report;
    private int _handling;
    private int _further;

    /// <summary>A reporter over the three effects, in the order they run.</summary>
    /// <param name="writeDump">Writes the minidump; the path, or null when none was written.</param>
    /// <param name="askToReport">The crash dialog, given the dump's path; true when the user wants the bug report.</param>
    /// <param name="report">The bug-report flow (<c>10_LOGGING</c> §Bug report flow).</param>
    public CrashReporter(Func<string?> writeDump, Func<string?, bool> askToReport, Func<Task> report)
    {
        _writeDump = writeDump ?? throw new ArgumentNullException(nameof(writeDump));
        _askToReport = askToReport ?? throw new ArgumentNullException(nameof(askToReport));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>True once the first fatal exception is being reported; the caller shuts the App down with exit code 1.</summary>
    public bool Crashed => Volatile.Read(ref _handling) == 1;

    /// <summary>How many further fatal exceptions arrived while the first was being reported.</summary>
    public int FurtherExceptions => Volatile.Read(ref _further);

    /// <summary>
    /// Reports <paramref name="exception"/>. True when this call reported it (the first); false when a crash was already
    /// being reported, in which case nothing is shown.
    /// </summary>
    public async Task<bool> HandleAsync(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            if (Interlocked.Increment(ref _further) == 1)
            {
                Log.Fatal(exception, "ui: a further unhandled exception ({Source}) while the first was being reported; later ones are counted", source);
            }

            return false;
        }

        Log.Fatal(exception, "ui: unhandled exception ({Source}); FrameLedger closes with exit code 1", source);
        string? dump = null;
        try
        {
            dump = _writeDump();
        }
        catch (Exception dumpFailure) when (dumpFailure is not OutOfMemoryException)
        {
            // The dialog still comes: a report without a dump is worth more than no report.
            Log.Error(dumpFailure, "ui: the crash dump was not written");
        }

        Log.Information("ui: crash dump {Dump}", dump ?? "not written");
        try
        {
            if (_askToReport(dump))
            {
                await _report().ConfigureAwait(true);
            }
        }
        catch (Exception reportFailure) when (reportFailure is not OutOfMemoryException)
        {
            // The report is a courtesy after a crash; its own failure must not become a second crash.
            Log.Error(reportFailure, "ui: the crash report did not complete (dump {Dump})", dump ?? "not written");
        }

        int further = FurtherExceptions;
        if (further > 0)
        {
            Log.Warning("ui: {Count} further unhandled exception(s) arrived while the crash was reported", further);
        }

        // No CloseAndFlush here: the file sink is unbuffered, the teardown that follows still logs, and OnExit closes it.
        return true;
    }
}
