using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>The preview dialog's state (P4 PR-3): the sentence naming the zip, its entries, the drag instruction, and "show it in Explorer".</summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed partial class BugReportPreviewViewModel
{
    private readonly BugReportPreviewModel _model;

    public BugReportPreviewViewModel(BugReportPreviewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Intro = string.Format(CultureInfo.CurrentCulture, Strings.BugReport_Preview_Intro_Format, model.ZipPath, model.Entries.Count);
    }

    public string Intro { get; }

    public IReadOnlyList<string> Entries => _model.Entries;

    public static string DragInstruction => Strings.BugReport_Preview_Drag;

    public static string ShowZipText => Strings.BugReport_OpenZipFolder;

    /// <summary>Explorer with the zip selected — the file the user is about to drag.</summary>
    [RelayCommand]
    private void ShowZip()
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _model.ZipPath + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not show the bug bundle in Explorer");
        }
    }
}
