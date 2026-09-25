using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;

namespace FrameLedger.Infrastructure.Settings;

/// <summary>
/// D33 (owner decision 2026-09-26): the user-mode exception option as one row in <c>settings</c> —
/// <c>hooking.usermode_ac_exceptions = 1</c> is on, anything else, including no row at all, is off. Read fresh on every
/// ask, like <see cref="SettingsKillSwitch"/>: the App flips the row, and the next session start and command take it.
/// </summary>
public sealed class SettingsUserModeExceptionSwitch : IUserModeExceptionSwitch
{
    /// <summary>The one value that means on.</summary>
    public const string OnValue = "1";

    private readonly ISettingsStore _settings;

    public SettingsUserModeExceptionSwitch(ISettingsStore settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
    public async ValueTask<bool> IsOnAsync(CancellationToken ct = default) =>
        string.Equals(await _settings.GetAsync(SettingsRegistry.HookingUserModeExceptions.Key, ct).ConfigureAwait(false), OnValue, StringComparison.Ordinal);
}
