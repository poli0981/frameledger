using CommunityToolkit.Mvvm.ComponentModel;

namespace FrameLedger.App.ViewModels;

/// <summary>Logs (08_UI §Logs) arrives with P3 PR-8; until then the page is its empty state.</summary>
public sealed class LogsViewModel : ObservableObject
{
    public static string Header => Strings.Logs_Header;

    public static string EmptyText => Strings.Logs_Empty;
}
