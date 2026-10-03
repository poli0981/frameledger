// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Windows;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Windows;

/// <summary>
/// Help ▸ About (beta.12, D45): the program's Appropriate Legal Notices — the copyright line, that there is no warranty,
/// the licence with the NOTICE terms and how to read both, where this version's source is — then the third-party licences
/// and the legal documents, each rendered from what this build embeds. Until beta.12 About was one line in a message box,
/// and <c>THIRD_PARTY_NOTICES</c>' "About → Third-party tab" was an open item.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public partial class AboutWindow : FluentWindow
{
    public const int AppTab = 0;
    public const int ThirdPartyTab = 1;
    public const int LegalTab = 2;

    private readonly IUrlOpener _urls;

    public AboutWindow(IUrlOpener urls, IThemeApplier theme, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        theme.Apply(appearance.Theme, null);
        InitializeComponent();
        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.About_Version_Format, UiIdentity.Version);
        SourceText.Text = string.Format(CultureInfo.CurrentCulture, Strings.About_Notices_Source_Format, SourceCode.AbsoluteUri);
        ThirdParty.UrlOpener = urls;
        ThirdParty.Pages = EmbeddedDocuments.ThirdParty();
        Legal.UrlOpener = urls;
        Legal.Pages = EmbeddedDocuments.Legal();
    }

    /// <summary>This version's source: the repository at the tag (or commit) the build was made from.</summary>
    public static Uri SourceCode => RepositoryLinks.Tree(string.Empty);

    /// <summary>The licences folder a published App carries beside it (<c>FrameLedger.App.csproj</c>: publish only).</summary>
    public static string LicencesFolder => Path.Combine(AppContext.BaseDirectory, "licenses");

    /// <summary>The tab control, for the host and the tests.</summary>
    public System.Windows.Controls.TabControl TabView => Tabs;

    /// <summary>The legal documents tab's pages.</summary>
    public DocumentBrowser LegalPages => Legal;

    /// <summary>The third-party tab's pages.</summary>
    public DocumentBrowser ThirdPartyPages => ThirdParty;

    /// <summary>Opens the legal documents tab on <paramref name="path"/> (<c>LICENSE</c>, <c>NOTICE</c>, …).</summary>
    public void ShowLegal(string path)
    {
        Tabs.SelectedIndex = LegalTab;
        _ = Legal.Show(path);
    }

    private void OnViewLicence(object sender, RoutedEventArgs e) => ShowLegal("LICENSE");

    private void OnViewNotice(object sender, RoutedEventArgs e) => ShowLegal(EmbeddedDocuments.Notice);

    private void OnOpenSource(object sender, RoutedEventArgs e) => _ = _urls.Open(SourceCode);

    private void OnOpenLicencesFolder(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(LicencesFolder))
        {
            NoLicencesFolder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo("explorer.exe", "\"" + LicencesFolder + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not open the licences folder");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
