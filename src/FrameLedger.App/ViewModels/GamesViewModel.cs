using CommunityToolkit.Mvvm.ComponentModel;

namespace FrameLedger.App.ViewModels;

/// <summary>Games (08_UI §Games) arrives with P3 PR-5; this build shows the empty state so the shell is honest about what it has.</summary>
public sealed class GamesViewModel : ObservableObject
{
    public static string Header => Strings.Games_Header;

    public static string EmptyText => Strings.Games_Empty;
}
