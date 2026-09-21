using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Settings ▸ System (2026-09-21): this PC as a session's hardware snapshot would record it right now — the same
/// <see cref="IHardwareSnapshotSource"/> the Agent stamps every session with (DXGI adapter 0, the registry's processor
/// name, physical memory, the OS build, the primary display). Until this existed the snapshot was stored on every
/// session and shown nowhere but the trend's change markers and the exports.
/// </summary>
/// <remarks>
/// Read once when the page opens, on the thread pool (DXGI and the registry are quick, never instant), and never sent
/// anywhere: <c>Copy</c> puts the same lines on the clipboard for a bug report the user writes themselves. A field the
/// source could not read is N/A, never a guess.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class SystemInfoViewModel : ObservableObject
{
    private readonly IHardwareSnapshotSource _source;
    private readonly IClipboard _clipboard;
    private readonly IMessageStrip _strip;

    public SystemInfoViewModel(IHardwareSnapshotSource source, IClipboard clipboard, IMessageStrip strip)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        Pending = LoadAsync();
    }

    public static string Header => Strings.Settings_System_Header;

    public static string Note => Strings.Settings_System_Note;

    public static string CopyText => Strings.Settings_System_Copy;

    public ObservableCollection<LabelledValue> Lines { get; } = [];

    /// <summary>The load in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    /// <summary>The snapshot as label/value lines, in the order the page and the clipboard state them.</summary>
    public static IReadOnlyList<LabelledValue> Describe(HardwareSnapshot? snapshot)
    {
        string na = Strings.Common_NotAvailable;
        string display = snapshot?.DisplayRes is { Length: > 0 } res
            ? snapshot.DisplayHz is double hz ? string.Format(CultureInfo.CurrentCulture, Strings.System_Display_Format, res, hz) : res
            : na;
        return
        [
            new(Strings.System_Cpu, Text(snapshot?.CpuName)),
            new(Strings.System_Gpu, Text(snapshot?.GpuName)),
            new(Strings.System_GpuDriver, Text(snapshot?.GpuDriver)),
            new(Strings.System_Ram, snapshot?.RamGb is double gb ? string.Format(CultureInfo.CurrentCulture, Strings.System_Ram_Format, gb) : na),
            new(Strings.System_Os, Text(snapshot?.OsBuild)),
            new(Strings.System_Display, display),
        ];

        string Text(string? value) => string.IsNullOrWhiteSpace(value) ? na : value;
    }

    [RelayCommand]
    private void Copy()
    {
        var text = new StringBuilder();
        foreach (LabelledValue line in Lines)
        {
            text.Append(line.Label).Append(": ").AppendLine(line.Value);
        }

        if (_clipboard.SetText(text.ToString()))
        {
            _strip.Info(Header, Strings.Settings_System_Copied);
        }
        else
        {
            _strip.Warn(Header, Strings.BugReport_ClipboardRefused);
        }
    }

    private async Task LoadAsync()
    {
        HardwareSnapshot? snapshot;
        try
        {
            snapshot = await Task.Run(_source.Take).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A machine that will not describe itself is a page of N/A, never a Settings page that does not open.
            snapshot = null;
        }

        Lines.Clear();
        foreach (LabelledValue line in Describe(snapshot))
        {
            Lines.Add(line);
        }
    }
}
