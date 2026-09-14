using CommunityToolkit.Mvvm.Input;

namespace FrameLedger.App.ViewModels;

/// <summary>One row of the Settings page's "games with hooking on" list (FR-10): the name and the per-game revoke.</summary>
public sealed partial class HookedGameViewModel(long gameId, string name, Func<HookedGameViewModel, Task> revoke)
{
    public long GameId { get; } = gameId;

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    [RelayCommand]
    private Task RevokeAsync() => revoke(this);
}
