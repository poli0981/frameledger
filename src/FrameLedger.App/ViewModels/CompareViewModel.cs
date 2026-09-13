using CommunityToolkit.Mvvm.ComponentModel;

namespace FrameLedger.App.ViewModels;

/// <summary>Compare (08_UI §Compare) arrives with P3 PR-7; until then the page is its empty state.</summary>
public sealed class CompareViewModel : ObservableObject
{
    public static string Header => Strings.Compare_Header;

    public static string EmptyText => Strings.Compare_Empty;
}
