// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.Services;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace FrameLedger.App.Update;

/// <summary><see cref="IUpdatePrompts"/> over WPF-UI's <see cref="MessageBox"/>, the way About and the removal confirmation are.</summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed class UpdatePrompts(IUrlOpener urls, BugReportFlow bugReports) : IUpdatePrompts
{
    /// <summary>Where a 404 sends the user: the releases page itself, which is what a moved feed would have changed.</summary>
    public static Uri ReleasesPage { get; } = new(VelopackUpdateClient.RepositoryUrl + "/releases");

    public async Task<bool> OfferAsync(UpdateCandidate candidate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var body = new StackPanel();
        body.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = OfferText(candidate),
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(candidate.NotesMarkdown))
        {
            body.Children.Add(new System.Windows.Controls.TextBlock { Text = Strings.Update_Notes_Header, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            // beta.12: the release's notes are the CHANGELOG section, rendered rather than shown as raw Markdown; its relative
            // links resolve the way they do in the repository, against CHANGELOG.md at the root.
            var notes = new Controls.MarkdownView { MaxHeight = 260, MinHeight = 80, UrlOpener = urls, DocumentPath = "CHANGELOG.md", MarkdownText = candidate.NotesMarkdown };
            System.Windows.Automation.AutomationProperties.SetName(notes, Strings.Update_Notes_Header);
            body.Children.Add(notes);
        }

        body.Children.Add(new System.Windows.Controls.TextBlock { Text = Strings.Update_Unsigned_Footer, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), Opacity = 0.7 });
        var box = new MessageBox
        {
            Title = string.Format(CultureInfo.CurrentCulture, Strings.Update_Available_Title_Format, candidate.Version),
            Content = body,
            PrimaryButtonText = Strings.Update_Download,
            CloseButtonText = Strings.Update_Later,
            MaxWidth = 640,
        };
        MessageBoxResult result = await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
        return result == MessageBoxResult.Primary;
    }

    /// <summary>
    /// The offer's first line. Since beta.14 it says the download Velopack will make: the deltas from the installed version
    /// when the feed has them — 1.6 MB for beta.12 → beta.13 — with the full package named as the fallback; it said the full
    /// package's ~102 MB even when the delta was what came.
    /// </summary>
    public static string OfferText(UpdateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        string full = Megabytes(candidate.SizeBytes);
        return candidate.DeltaBytes is { } delta && delta < candidate.SizeBytes
            ? string.Format(CultureInfo.CurrentCulture, Strings.Update_Available_Body_Delta_Format, candidate.Version, Megabytes(delta), full)
            : string.Format(CultureInfo.CurrentCulture, Strings.Update_Available_Body_Format, candidate.Version, full);
    }

    /// <summary>Whole megabytes, one decimal under ten ("1.5", "102").</summary>
    private static string Megabytes(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return mb.ToString(mb < 10 ? "0.#" : "0", CultureInfo.CurrentCulture);
    }

    public Task UpToDateAsync(string version, CancellationToken ct = default) =>
        ShowAsync(Strings.Menu_Help_CheckUpdates, string.Format(CultureInfo.CurrentCulture, Strings.Update_UpToDate_Format, version), ct);

    public Task NotInstalledAsync(CancellationToken ct = default) => ShowAsync(Strings.Menu_Help_CheckUpdates, Strings.Update_NotInstalled, ct);

    /// <summary>The table's extra behaviour: a 404 offers the releases page, an unknown failure offers the bug report.</summary>
    public async Task FailedAsync(UpdateFailure failure, CancellationToken ct = default)
    {
        var box = new MessageBox
        {
            Title = Strings.Update_Err_Title,
            Content = new System.Windows.Controls.TextBlock { Text = Text(failure), TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Strings.Common_Ok,
            MaxWidth = 560,
        };
        switch (failure)
        {
            case UpdateFailure.NotFound:
                box.PrimaryButtonText = Strings.Update_OpenReleases;
                break;
            case UpdateFailure.Unknown:
                box.PrimaryButtonText = Strings.Menu_Help_ReportBug;
                break;
            default:
                break;
        }

        MessageBoxResult result = await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
        if (result != MessageBoxResult.Primary)
        {
            return;
        }

        if (failure == UpdateFailure.NotFound)
        {
            _ = urls.Open(ReleasesPage);
        }
        else if (failure == UpdateFailure.Unknown)
        {
            _ = await bugReports.RunAsync(ct).ConfigureAwait(true);
        }
    }

    /// <summary>The row's string (<c>11_UPDATER</c> §Error mapping).</summary>
    public static string Text(UpdateFailure failure) => failure switch
    {
        UpdateFailure.NotFound => Strings.Update_Err404,
        UpdateFailure.RateLimited => Strings.Update_Err_RateLimited,
        UpdateFailure.Server => Strings.Update_Err_Server,
        UpdateFailure.Offline => Strings.Update_Err_Offline,
        UpdateFailure.Corrupt => Strings.Update_Err_Corrupt,
        _ => Strings.Update_Err_Unknown,
    };

    private static async Task ShowAsync(string title, string body, CancellationToken ct)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Strings.Common_Ok,
            MaxWidth = 560,
        };
        await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
    }
}
