using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §Bug report flow, steps 2–4 in one place (P4 PR-3), reached from Help ▸ Report a bug, the Logs
/// page's button and the crash dialog (P4 PR-9): a recent crash dump offered as a checkbox that starts clear, the zip
/// where the user says (step 2, <see cref="BugBundleBuilder"/>), the preview listing every entry (step 3), then either
/// the GitHub issue form with the two short fields prefilled or the environment summary on the clipboard as Markdown
/// (step 4). Nothing is ever sent automatically; the zip is dragged in by hand.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed class BugReportFlow
{
    private readonly BugBundleBuilder _bundles;
    private readonly IFileSaver _saver;
    private readonly IAgentRequests _agent;
    private readonly IBugReportPreview _preview;
    private readonly IUrlOpener _urls;
    private readonly IClipboard _clipboard;
    private readonly IMessageStrip _strip;
    private readonly TimeProvider _clock;

    public BugReportFlow(BugBundleBuilder bundles, IFileSaver saver, IAgentRequests agent, IBugReportPreview preview, IUrlOpener urls, IClipboard clipboard, IMessageStrip strip, TimeProvider? clock = null)
    {
        _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The whole flow; the outcome says how far it went, for the tests and the caller's log.</summary>
    public async Task<BugReportOutcome> RunAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        (bool cancelled, CrashDumpInfo? dump) = await ChooseCrashDumpAsync(ct).ConfigureAwait(true);
        if (cancelled)
        {
            return new BugReportOutcome(null, BugReportChoice.Close, Written: false);
        }

        string? path = _saver.PickSavePath("Zip archive (*.zip)|*.zip", BugBundleBuilder.SuggestedName(now));
        if (path is null)
        {
            return new BugReportOutcome(null, BugReportChoice.Close, Written: false);
        }

        HelloAck? hello = _agent.Hello;
        IReadOnlyList<string> entries;
        try
        {
            entries = await _bundles.WriteAsync(path, hello, dump, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Warning(ex, "ui: the bug bundle could not be written");
            _strip.Warn(Strings.BugReport_Title, string.Format(CultureInfo.CurrentCulture, Strings.Logs_Bundle_Failed_Format, ex.Message));
            return new BugReportOutcome(path, BugReportChoice.Close, Written: false);
        }

        IReadOnlyDictionary<string, string> sysinfo = BugBundleBuilder.SysInfoOf(hello, now);
        Uri issueUrl = IssueLink.NewIssue(sysinfo["app_version"], IssueLink.OsText(Environment.OSVersion.Version));
        var model = new BugReportPreviewModel(path, entries, issueUrl, IssueLink.Markdown(sysinfo));

        BugReportChoice choice = await _preview.ShowAsync(model, ct).ConfigureAwait(true);
        switch (choice)
        {
            case BugReportChoice.OpenIssue:
                if (!_urls.Open(issueUrl))
                {
                    _strip.Warn(Strings.BugReport_Title, Strings.BugReport_BrowserRefused);
                }

                break;
            case BugReportChoice.CopyMarkdown:
                _strip.Info(Strings.BugReport_Title, _clipboard.SetText(model.Markdown) ? Strings.BugReport_Copied : Strings.BugReport_ClipboardRefused);
                break;
            default:
                _strip.Success(Strings.BugReport_Title, string.Format(CultureInfo.CurrentCulture, Strings.Logs_Bundle_Exported_Format, path, entries.Count));
                break;
        }

        return new BugReportOutcome(path, choice, Written: true);
    }

    /// <summary>
    /// Step 2's optional item (P4 PR-9): a crash dump of the last seven days is offered before the save dialog, in a
    /// checkbox that starts clear. No dump, no question; the dump comes back only when it was ticked.
    /// </summary>
    private async Task<(bool Cancelled, CrashDumpInfo? Dump)> ChooseCrashDumpAsync(CancellationToken ct)
    {
        CrashDumpInfo? dump = _bundles.LatestCrashDump();
        if (dump is null)
        {
            return (false, null);
        }

        CrashDumpChoice answer = await _preview.AskCrashDumpAsync(dump, ct).ConfigureAwait(true);
        return (answer == CrashDumpChoice.Cancel, answer == CrashDumpChoice.Include ? dump : null);
    }
}
