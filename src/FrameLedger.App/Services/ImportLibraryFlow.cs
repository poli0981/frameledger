using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Pages;
using FrameLedger.Application.Import;

namespace FrameLedger.App.Services;

/// <summary>
/// File ▸ Import library… (FR-1.2, P4 PR-4): discover every installed store's titles, show the review checklist,
/// add what the user ticked — hooking off for every one, as File ▸ Add game… would — and land on the Games page.
/// Nothing is launched, nothing is fetched, nothing is added without a tick.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed class ImportLibraryFlow
{
    private readonly LibraryImporter _importer;
    private readonly IImportReview _review;
    private readonly IMessageStrip _strip;
    private readonly IPageNavigator _navigator;

    public ImportLibraryFlow(LibraryImporter importer, IImportReview review, IMessageStrip strip, IPageNavigator navigator)
    {
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _review = review ?? throw new ArgumentNullException(nameof(review));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }

    /// <summary>The report, or null when nothing was found or the user cancelled.</summary>
    public async Task<ImportReport?> RunAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ImportCandidate> candidates = await _importer.DiscoverAsync(ct).ConfigureAwait(true);
        if (candidates.Count == 0)
        {
            _strip.Info(Strings.Import_Title, Strings.Import_NothingFound);
            return null;
        }

        IReadOnlyList<ImportCandidate>? selected = await _review.ReviewAsync(candidates, ct).ConfigureAwait(true);
        if (selected is null || selected.Count == 0)
        {
            return null;
        }

        ImportReport report = await _importer.ImportAsync(selected, ct).ConfigureAwait(true);
        _strip.Success(Strings.Import_Title, string.Format(CultureInfo.CurrentCulture, Strings.Import_Done_Format, report.Added, report.Skipped));
        if (report.Added > 0)
        {
            _navigator.Navigate<GamesPage>();
        }

        return report;
    }
}
