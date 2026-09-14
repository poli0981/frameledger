using CommunityToolkit.Mvvm.Input;

namespace FrameLedger.App.ViewModels;

/// <summary>One row of the Settings page's "games with hooking on" list (FR-10): the name and the per-game revoke.</summary>
public sealed partial class HookedGameViewModel(long gameId, string name, Func<HookedGameViewModel, Task> revoke, bool usesVulkan = false)
{
    public long GameId { get; } = gameId;

    /// <summary>The row's <c>capability_flags</c> carries <c>vulkan</c> (P4 PR-2): the layer is this game's capture side, and its registration follows this.</summary>
    public bool UsesVulkan { get; } = usesVulkan;

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    [RelayCommand]
    private Task RevokeAsync() => revoke(this);
}
