using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;

namespace FrameLedger.Infrastructure.Settings;

/// <summary>
/// FR-2.4's global switch as one row in <c>settings</c>: <c>hooking.kill_switch = 1</c> is engaged, anything
/// else — including no row at all — is not (P2 PR-F, HANDOFF §P2 decision D7). Read fresh on every ask, so
/// the UI (P3) flipping the row takes effect at the next gate decision and the next scan boundary.
/// </summary>
public sealed class SettingsKillSwitch : IKillSwitch
{
    /// <summary>The <c>settings.key</c>; <c>06_DATA_MODEL</c> §Settings registry names it.</summary>
    public const string Key = "hooking.kill_switch";

    /// <summary>The one value that means engaged. Absent, empty or anything else means hooking is allowed.</summary>
    public const string EngagedValue = "1";

    private readonly ISettingsStore _settings;

    public SettingsKillSwitch(ISettingsStore settings) => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
    public async ValueTask<bool> IsEngagedAsync(CancellationToken ct = default) =>
        string.Equals(await _settings.GetAsync(Key, ct).ConfigureAwait(false), EngagedValue, StringComparison.Ordinal);

    /// <summary>Flip the switch; the console verb and, later, the UI's toggle.</summary>
    public ValueTask SetAsync(bool engaged, CancellationToken ct = default) =>
        _settings.SetAsync(Key, engaged ? EngagedValue : "0", ct);
}
